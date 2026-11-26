using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Prescriptions;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of PrescriptionService: OptionsAsync, PatientContextAsync, PatientProfilePrescriptionsAsync,
/// PatientProfilePrescriptionAsync, SavePatientProfilePrescriptionAsync, OwnPatientPrescriptionsAsync, OwnPatientPrescriptionAsync.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PrescriptionServiceTests
{
    // Clinic time is UTC+7, so the clinic date is 2030-01-10.
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2030, 1, 10);

    /// <summary>Permission fake: fixed roles/branches; a denied permission or a branch outside the list throws MSG06 (403).</summary>
    private sealed class RxAccess(string[] roles, int[] branches, params string[] denied) : ISettingsAccess
    {
        private static BusinessException Forbidden() => new(403, SystemMessages.Get("MSG06"));
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, true, roles, [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw Forbidden() : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) =>
            denied.Contains(permission) || !branches.Contains(branchId) ? throw Forbidden() : Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, OtherBranch, GhostBranch;
        public required ApplicationUser Dentist, Dentist2, Locked, Assistant, Customer, Stranger;
        public required int DentistEmployee, Dentist2Employee, LockedEmployee;
        public required int PatientId, OtherPatientId, ArchivedPatientId;
        public required CatalogMedicine Med1, Med2, MedInactive, MedNotEligible;
        public CatalogMedicine[] Bulk = [];
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();

        /// <summary>The service seen by a user with the given roles/branches (default: a dentist of the main branch).</summary>
        public PrescriptionService Svc(string[]? roles = null, int[]? branches = null, params string[] denied) =>
            new(Db, new RxAccess(roles ?? [AccountRoles.Dentist], branches ?? [N], denied), new TestClock(Now));

        public SavePatientPrescription Valid(Action<List<SavePatientPrescriptionItem>>? items = null)
        {
            var list = new List<SavePatientPrescriptionItem> { new(Med1.Id, 10, "Uống 2 lần/ngày", "viên") };
            items?.Invoke(list);
            return new(Guid.NewGuid(), 0, N, Today, "Viêm tủy răng 36", null, null, list.ToArray());
        }
    }

    private static async Task<Env> NewEnv(int bulk = 0)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        var n = CreateTestKit.NextId();
        var other = CreateTestKit.NextId();
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dr" + n, AccountRoles.Dentist, n);
        var dentist2 = await CreateTestKit.SeedStaffAsync(db, "drb" + n, AccountRoles.Dentist, n);
        var locked = await CreateTestKit.SeedStaffAsync(db, "drl" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "as" + n, AccountRoles.ClinicAssistant, n);
        await CreateTestKit.SeedStaffAsync(db, "dro" + n, AccountRoles.Dentist, other);
        locked.IsActive = false; // an inactive account is never offered as prescriber
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        branch.Address = "12 Nguyễn Trãi "; branch.Phone = "  "; branch.Email = "pk@example.com";
        var customer = new ApplicationUser { UserName = "kh" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        var stranger = new ApplicationUser { UserName = "st" + n, FullName = "Người lạ", AccountType = "Public", IsActive = true };
        db.Users.AddRange(customer, stranger);
        PatientProfile P(string code, string name, bool archived = false) => new()
        {
            PatientCode = code + n, FullName = name, NormalizedName = name.ToUpperInvariant(), BirthDate = new DateOnly(1990, 3, 3), Gender = PatientGenders.Female,
            IsArchived = archived, CreatedAt = Now, UpdatedAt = Now
        };
        var patient = P("BN", "Trần Thị Mai"); var otherPatient = P("BM", "Người khác"); var archived = P("BA", "Đã lưu trữ", true);
        db.Patients.AddRange(patient, otherPatient, archived);
        CatalogMedicine M(string suffix, string code, string? ingredient, bool active = true, bool eligible = true, string? instructions = null) => new()
        {
            Code = code + n, Name = $"ZQ{n} {suffix}", BaseUnit = "viên", ReferencePrice = 5000, IsActive = active, PrescriptionEligible = eligible,
            Spec = new() { ActiveIngredient = ingredient, Strength = "500mg", DosageForm = "Viên nén", Packaging = "Hộp 10 vỉ", Instructions = instructions }
        };
        var med1 = M("Amoxicillin 500", "AMX", "Amoxicillin" + n, instructions: "Uống sau ăn");
        var med2 = M("Paracetamol", "PCM", null);
        var inactive = M("Ngừng", "OFF", null, active: false);
        var notEligible = M("Không kê đơn", "NOP", null, eligible: false);
        var bulkRows = Enumerable.Range(0, bulk).Select(i => M($"bulk{i:D2}", "BLK" + i, null)).ToArray();
        db.CatalogMedicines.AddRange([med1, med2, inactive, notEligible, .. bulkRows]);
        await db.SaveChangesAsync();
        var emp = async (ApplicationUser u) => (await db.Employees.SingleAsync(x => x.AccountId == u.Id)).Id;
        return new Env
        {
            Pg = pg, N = n, OtherBranch = other, GhostBranch = CreateTestKit.NextId(), Dentist = dentist, Dentist2 = dentist2, Locked = locked, Assistant = assistant,
            Customer = customer, Stranger = stranger, DentistEmployee = await emp(dentist), Dentist2Employee = await emp(dentist2), LockedEmployee = await emp(locked),
            PatientId = patient.Id, OtherPatientId = otherPatient.Id, ArchivedPatientId = archived.Id,
            Med1 = med1, Med2 = med2, MedInactive = inactive, MedNotEligible = notEligible, Bulk = bulkRows
        };
    }

    // ================= OptionsAsync =================
    // Input: search text (the placeholder {n} is this test's unique number) -> Expected: which of the four seeded medicines come back.
    public static TheoryData<string, string, string> OptionSearches => new()
    {
        { "UT-RXO-01 Không từ khóa: chỉ thuốc đang dùng và được kê đơn", "", "med1,med2" },
        { "UT-RXO-02 Từ khóa null", null!, "med1,med2" },
        { "UT-RXO-03 Từ khóa trùng tên (không phân biệt hoa thường, cắt khoảng trắng)", "  zq{n}  ", "med1,med2" },
        { "UT-RXO-04 Từ khóa theo mã thuốc", "amx{n}", "med1" },
        { "UT-RXO-05 Từ khóa theo hoạt chất", "AMOXICILLIN{n}", "med1" },
        { "UT-RXO-06 Từ khóa một phần của tên", "{n} paracetamol", "med2" },
        { "UT-RXO-07 Thuốc ngừng dùng không xuất hiện", "{n} ngừng", "" },
        { "UT-RXO-08 Thuốc không cho kê đơn không xuất hiện", "{n} không", "" },
        { "UT-RXO-09 Không khớp", "{n}-khong-co", "" }
    };

    [Theory, MemberData(nameof(OptionSearches))]
    public async Task Options_filters_by_search(string caseName, string? search, string expected)
    {
        await using var env = await NewEnv();
        var result = await env.Svc().OptionsAsync(env.Dentist.Id, null, search?.Replace("{n}", env.N.ToString()), CancellationToken.None);
        var byId = new Dictionary<Guid, string> { [env.Med1.Id] = "med1", [env.Med2.Id] = "med2", [env.MedInactive.Id] = "inactive", [env.MedNotEligible.Id] = "noeligible" };
        var keys = result.Where(x => byId.ContainsKey(x.Id)).Select(x => byId[x.Id]).Order().ToArray();
        Assert.Equal(expected == "" ? [] : expected.Split(','), keys);
    }

    [Fact]
    public async Task Options_UT_RXO_10_returns_fields_sorted_by_name()
    {
        await using var env = await NewEnv();
        var rows = (await env.Svc().OptionsAsync(env.Dentist.Id, null, "zq" + env.N, CancellationToken.None)).ToArray();
        Assert.Equal([env.Med1.Id, env.Med2.Id], rows.Select(x => x.Id));
        var first = rows[0];
        Assert.Equal(("AMX" + env.N, $"ZQ{env.N} Amoxicillin 500", "Amoxicillin" + env.N, "500mg", "Viên nén", "viên", "Hộp 10 vỉ", "Uống sau ăn", 5000L),
            (first.Code, first.Name, first.ActiveIngredient, first.Strength, first.DosageForm, first.BaseUnit, first.Packaging, first.DefaultInstructions, first.ReferencePrice));
    }

    [Fact]
    public async Task Options_UT_RXO_11_returns_at_most_50_rows()
    {
        await using var env = await NewEnv(bulk: 60);
        var rows = await env.Svc().OptionsAsync(env.Dentist.Id, null, "zq" + env.N, CancellationToken.None);
        Assert.Equal(50, rows.Length);
    }

    [Theory]
    [InlineData("UT-RXO-12 Từ khóa dài đúng 100 ký tự (biên)", 100, false)]
    [InlineData("UT-RXO-13 Từ khóa dài 101 ký tự", 101, true)]
    public async Task Options_search_length_limit(string caseName, int length, bool rejected)
    {
        await using var env = await NewEnv();
        var call = () => env.Svc().OptionsAsync(env.Dentist.Id, null, new string('x', length), CancellationToken.None);
        if (!rejected) { Assert.Empty(await call()); return; }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((400, "MSG04", "q"), (error.Status, error.Response.Code, error.Response.Field));
        Assert.Equal("Từ khóa không đúng định dạng.", error.Message);
    }

    [Fact]
    public async Task Options_UT_RXO_14_with_branch_checks_branch_access()
    {
        await using var env = await NewEnv();
        Assert.NotNull(await env.Svc().OptionsAsync(env.Dentist.Id, env.N, null, CancellationToken.None));
        var error = await CreateTestKit.ErrorOf(() => env.Svc().OptionsAsync(env.Dentist.Id, env.OtherBranch, null, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Options_UT_RXO_15_without_permission_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(denied: "prescriptions.manage").OptionsAsync(env.Dentist.Id, null, null, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    // ================= PatientContextAsync =================
    [Fact]
    public async Task Context_UT_RXC_01_unknown_patient_is_not_found()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().PatientContextAsync(env.Dentist.Id, 987654321, CancellationToken.None));
        Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Context_UT_RXC_02_archived_patient_is_not_found()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().PatientContextAsync(env.Dentist.Id, env.ArchivedPatientId, CancellationToken.None));
        Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Context_UT_RXC_03_without_permission_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(denied: "prescriptions.manage").PatientContextAsync(env.Dentist.Id, env.PatientId, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Context_UT_RXC_04_non_dentist_role_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc([AccountRoles.ClinicAssistant]).PatientContextAsync(env.Assistant.Id, env.PatientId, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", error.Message);
    }

    [Fact]
    public async Task Context_UT_RXC_05_lists_branches_and_doctors()
    {
        await using var env = await NewEnv();
        var ctx = await env.Svc().PatientContextAsync(env.Dentist.Id, env.PatientId, CancellationToken.None);
        Assert.Equal([env.N], ctx.BranchIds);
        Assert.Equal(env.DentistEmployee, ctx.PrescriberEmployeeId);
        var branch = Assert.Single(ctx.Branches);
        Assert.Equal((env.N, "Chi nhánh " + env.N, "12 Nguyễn Trãi", (string?)null, "pk@example.com"), (branch.Id, branch.Name, branch.Address, branch.Phone, branch.Email));
        var doctors = ctx.Doctors.Select(x => x.EmployeeId).ToArray();
        Assert.Contains(env.DentistEmployee, doctors);
        Assert.Contains(env.Dentist2Employee, doctors);
        Assert.DoesNotContain(env.LockedEmployee, doctors); // inactive account
    }

    [Fact]
    public async Task Context_UT_RXC_06_signed_in_prescriber_is_added_to_branches_without_membership()
    {
        await using var env = await NewEnv();
        // The second branch has a dentist member, but the signed-in dentist is not a member of it.
        await CreateTestKit.SeedStaffAsync(env.Db, "mem" + env.N, AccountRoles.Dentist, env.OtherBranch, member: true);
        var ctx = await env.Svc(branches: [env.N, env.OtherBranch, env.GhostBranch]).PatientContextAsync(env.Dentist.Id, env.PatientId, CancellationToken.None);
        Assert.Equal([env.N, env.OtherBranch], ctx.Branches.Select(x => x.Id).Order()); // the ghost branch does not exist
        Assert.Contains(ctx.Doctors, x => x.EmployeeId == env.DentistEmployee && x.BranchId == env.OtherBranch);
    }

    // ================= PatientProfilePrescriptionsAsync / PatientProfilePrescriptionAsync =================
    [Fact]
    public async Task List_UT_RXL_01_unknown_and_archived_patients_are_not_found()
    {
        await using var env = await NewEnv();
        foreach (var id in new[] { 987654321, env.ArchivedPatientId })
        {
            var error = await CreateTestKit.ErrorOf(() => env.Svc().PatientProfilePrescriptionsAsync(env.Dentist.Id, id, CancellationToken.None));
            Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
        }
    }

    [Fact]
    public async Task List_UT_RXL_02_without_read_permission_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(denied: "prescriptions.read").PatientProfilePrescriptionsAsync(env.Dentist.Id, env.PatientId, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task List_UT_RXL_03_returns_only_own_branches_newest_first()
    {
        await using var env = await NewEnv();
        var wide = env.Svc(branches: [env.N, env.OtherBranch]);
        await CreateTestKit.SeedStaffAsync(env.Db, "mem" + env.N, AccountRoles.Dentist, env.OtherBranch);
        var older = await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { PrescribedOn = Today.AddDays(-2) }, CancellationToken.None);
        var newer = await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { PrescribedOn = Today }, CancellationToken.None);
        await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { BranchId = env.OtherBranch }, CancellationToken.None);
        await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.OtherPatientId, null, env.Valid(), CancellationToken.None);

        var list = await env.Svc().PatientProfilePrescriptionsAsync(env.Dentist.Id, env.PatientId, CancellationToken.None);
        Assert.Equal([newer.Id, older.Id], list.Select(x => x.Id));
    }

    [Fact]
    public async Task Detail_UT_RXD_01_found_and_not_found_cases()
    {
        await using var env = await NewEnv();
        var wide = env.Svc(branches: [env.N, env.OtherBranch]);
        await CreateTestKit.SeedStaffAsync(env.Db, "mem" + env.N, AccountRoles.Dentist, env.OtherBranch);
        var mine = await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        var elsewhere = await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { BranchId = env.OtherBranch }, CancellationToken.None);
        var ofOther = await wide.SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.OtherPatientId, null, env.Valid(), CancellationToken.None);

        var found = await env.Svc().PatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, mine.Id, CancellationToken.None);
        Assert.Equal((mine.Id, env.PatientId, env.N), (found.Id, found.PatientId, found.BranchId));
        foreach (var (patientId, id) in new[] { (env.PatientId, elsewhere.Id), (env.PatientId, ofOther.Id), (env.PatientId, Guid.NewGuid()), (env.ArchivedPatientId, mine.Id) })
        {
            var error = await CreateTestKit.ErrorOf(() => env.Svc().PatientProfilePrescriptionAsync(env.Dentist.Id, patientId, id, CancellationToken.None));
            Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
        }
        var denied = await CreateTestKit.ErrorOf(() => env.Svc(denied: "prescriptions.read").PatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, mine.Id, CancellationToken.None));
        Assert.Equal(403, denied.Status);
    }

    // ================= SavePatientProfilePrescriptionAsync: validation =================
    // Input: a mutation of the valid request -> Expected: BusinessException status / code / field.
    public static TheoryData<string, Func<SavePatientPrescription, SavePatientPrescription>, int, string, string?> Invalid => new()
    {
        { "UT-RXS-01 RequestKey rỗng", r => r with { RequestKey = Guid.Empty }, 400, "MSG04", "items" },
        { "UT-RXS-02 Không có thuốc nào", r => r with { Items = [] }, 400, "MSG04", "items" },
        { "UT-RXS-03 31 thuốc (quá 30)", r => r with { Items = Enumerable.Repeat(r.Items[0], 31).ToArray() }, 400, "MSG04", "items" },
        { "UT-RXS-04 Chẩn đoán 1001 ký tự", r => r with { Diagnosis = new string('a', 1001) }, 400, "MSG04", "diagnosis" },
        { "UT-RXS-05 Chẩn đoán khác 1001 ký tự", r => r with { OtherDiagnosis = new string('a', 1001) }, 400, "MSG04", "diagnosis" },
        { "UT-RXS-06 Lời dặn 4001 ký tự", r => r with { Advice = new string('a', 4001) }, 400, "MSG04", "diagnosis" },
        { "UT-RXS-07 Thuốc có MedicineId rỗng", r => r with { Items = [r.Items[0] with { MedicineId = Guid.Empty }] }, 400, "MSG04", "items" },
        { "UT-RXS-08 Số lượng 0", r => r with { Items = [r.Items[0] with { Quantity = 0 }] }, 400, "MSG04", "items" },
        { "UT-RXS-09 Số lượng 10001", r => r with { Items = [r.Items[0] with { Quantity = 10001 }] }, 400, "MSG04", "items" },
        { "UT-RXS-10 Cách dùng 1001 ký tự", r => r with { Items = [r.Items[0] with { Instructions = new string('a', 1001) }] }, 400, "MSG04", "items" },
        { "UT-RXS-11 Đơn vị null", r => r with { Items = [r.Items[0] with { Unit = null }] }, 400, "MSG04", "items" },
        { "UT-RXS-12 Đơn vị chỉ có khoảng trắng", r => r with { Items = [r.Items[0] with { Unit = "   " }] }, 400, "MSG04", "items" },
        { "UT-RXS-13 Đơn vị 41 ký tự", r => r with { Items = [r.Items[0] with { Unit = new string('a', 41) }] }, 400, "MSG04", "items" },
        { "UT-RXS-14 Ngày kê đơn ngày mai", r => r with { PrescribedOn = Today.AddDays(1) }, 400, "MSG04", "prescribedOn" },
        { "UT-RXS-15 Ngày kê đơn trước năm 2000", r => r with { PrescribedOn = new DateOnly(1999, 12, 31) }, 400, "MSG04", "prescribedOn" },
        { "UT-RXS-16 Bác sĩ kê đơn không thuộc chi nhánh", r => r with { PrescriberEmployeeId = 987654321 }, 400, "MSG04", "prescriberEmployeeId" },
        { "UT-RXS-17 Tạo mới nhưng ExpectedVersion khác 0", r => r with { ExpectedVersion = 3 }, 409, "MSG111", null },
        { "UT-RXS-18 Thuốc không có trong danh mục", r => r with { Items = [r.Items[0] with { MedicineId = Guid.NewGuid() }] }, 404, "MSG113", "items" }
    };

    [Theory, MemberData(nameof(Invalid))]
    public async Task Save_rejects_invalid_request(string caseName, Func<SavePatientPrescription, SavePatientPrescription> mutate, int status, string code, string? field)
    {
        await using var env = await NewEnv();
        var request = mutate(env.Valid());
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None));
        Assert.Equal((status, code, field), (error.Status, error.Response.Code, error.Response.Field));
        Assert.Equal(0, await env.Db.PatientPrescriptions.CountAsync(x => x.PatientId == env.PatientId));
    }

    [Theory]
    [InlineData("UT-RXS-19 Thuốc đã ngừng dùng", "inactive")]
    [InlineData("UT-RXS-20 Thuốc không cho kê đơn", "noeligible")]
    public async Task Save_rejects_medicine_that_cannot_be_prescribed(string caseName, string which)
    {
        await using var env = await NewEnv();
        var med = which == "inactive" ? env.MedInactive : env.MedNotEligible;
        var request = env.Valid(items => items.Add(new(med.Id, 1, null, "viên")));
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None));
        Assert.Equal((404, "MSG113", "items"), (error.Status, error.Response.Code, error.Response.Field));
        Assert.Equal(0, await env.Db.PatientPrescriptions.CountAsync(x => x.PatientId == env.PatientId));
    }

    // ================= Save: permission and patient checks =================
    [Fact]
    public async Task Save_UT_RXS_21_patient_checks_come_first()
    {
        await using var env = await NewEnv();
        foreach (var id in new[] { 987654321, env.ArchivedPatientId })
        {
            var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, id, null, env.Valid(), CancellationToken.None));
            Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
        }
    }

    [Fact]
    public async Task Save_UT_RXS_22_without_manage_permission_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(denied: "prescriptions.manage").SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Save_UT_RXS_23_non_dentist_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc([AccountRoles.ClinicAssistant]).SavePatientProfilePrescriptionAsync(env.Assistant.Id, env.PatientId, null, env.Valid(), CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Save_UT_RXS_24_branch_outside_the_dentists_branches_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { BranchId = env.OtherBranch }, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    // ================= Save: create =================
    [Fact]
    public async Task Save_UT_RXS_25_create_trims_text_and_defaults_instructions()
    {
        await using var env = await NewEnv();
        var request = env.Valid(items =>
        {
            items[0] = new(env.Med1.Id, 1, "  Uống 1 viên  ", "  viên ");
            items.Add(new(env.Med1.Id, 2, null, "vỉ"));        // no instructions: the medicine default "Uống sau ăn"
            items.Add(new(env.Med2.Id, 3, "   ", "hộp"));       // blank instructions: kept as an empty string
            items.Add(new(env.Med2.Id, 10000, null, "hộp"));   // upper quantity bound; medicine without default instructions
        }) with { Diagnosis = "  Viêm tủy  ", OtherDiagnosis = " Sâu răng ", Advice = " Súc miệng " };
        var dto = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None);

        Assert.Equal((1, env.N, "Chi nhánh " + env.N, Today, "Viêm tủy", "Sâu răng", "Súc miệng", env.DentistEmployee, "BN" + env.N, "Trần Thị Mai", "12 Nguyễn Trãi", (string?)null),
            (dto.Version, dto.BranchId, dto.BranchName, dto.PrescribedOn, dto.Diagnosis, dto.OtherDiagnosis, dto.Advice, dto.PrescriberEmployeeId, dto.PatientCode, dto.PatientName, dto.BranchAddress, dto.BranchPhone));
        Assert.Equal(["viên", "vỉ", "hộp", "hộp"], dto.Items.Select(x => x.Unit));
        Assert.Equal(["Uống 1 viên", "Uống sau ăn", "", ""], dto.Items.Select(x => x.Instructions));
        Assert.Equal([1, 2, 3, 10000], dto.Items.Select(x => x.Quantity));
        Assert.Equal(("AMX" + env.N, "500mg", (string?)null), (dto.Items[0].MedicineCode, dto.Items[0].Strength, dto.Items[0].DosageForm));
        var audit = await env.Db.PrescriptionAudits.SingleAsync(x => x.ActorId == env.Dentist.Id && x.BranchId == env.N && x.Action == "PatientPrescriptionCreate");
        Assert.Equal("Kê đơn tại hồ sơ bệnh nhân", audit.Reason);
    }

    [Fact]
    public async Task Save_UT_RXS_26_boundaries_are_accepted()
    {
        await using var env = await NewEnv();
        var request = new SavePatientPrescription(Guid.NewGuid(), 0, env.N, new DateOnly(2000, 1, 1), new string('a', 1000), new string('b', 1000), new string('c', 4000),
            Enumerable.Repeat(new SavePatientPrescriptionItem(env.Med1.Id, 1, new string('d', 1000), new string('e', 40)), 30).ToArray());
        var dto = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None);
        Assert.Equal(30, dto.Items.Length);
        Assert.Equal(new DateOnly(2000, 1, 1), dto.PrescribedOn);
    }

    [Fact]
    public async Task Save_UT_RXS_27_null_diagnosis_is_stored_as_empty()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { Diagnosis = null! }, CancellationToken.None);
        Assert.Equal("", dto.Diagnosis);
    }

    [Fact]
    public async Task Save_UT_RXS_28_prescriber_can_be_another_dentist_of_the_branch()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { PrescriberEmployeeId = env.Dentist2Employee }, CancellationToken.None);
        Assert.Equal(env.Dentist2Employee, dto.PrescriberEmployeeId);
        var self = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { PrescriberEmployeeId = env.DentistEmployee }, CancellationToken.None);
        Assert.Equal(env.DentistEmployee, self.PrescriberEmployeeId);
    }

    [Fact]
    public async Task Save_UT_RXS_29_prescriber_with_inactive_account_is_rejected()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { PrescriberEmployeeId = env.LockedEmployee }, CancellationToken.None));
        Assert.Equal((400, "MSG04", "prescriberEmployeeId"), (error.Status, error.Response.Code, error.Response.Field));
        Assert.Equal("Bác sĩ kê đơn không đúng định dạng.", error.Message);
    }

    // ================= Save: idempotency =================
    [Fact]
    public async Task Save_UT_RXS_30_same_key_and_same_request_replays_the_result()
    {
        await using var env = await NewEnv();
        var request = env.Valid();
        var first = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None);
        var again = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await env.Db.PatientPrescriptions.CountAsync(x => x.PatientId == env.PatientId));
        Assert.Equal(1, await env.Db.Set<PatientPrescriptionReceipt>().CountAsync(x => x.RequestKey == request.RequestKey));
    }

    [Fact]
    public async Task Save_UT_RXS_31_same_key_with_different_request_conflicts()
    {
        await using var env = await NewEnv();
        var request = env.Valid();
        await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request, CancellationToken.None);
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, request with { Advice = "Khác" }, CancellationToken.None));
        Assert.Equal((409, "MSG111"), (error.Status, error.Response.Code));
        Assert.Equal(1, await env.Db.PatientPrescriptions.CountAsync(x => x.PatientId == env.PatientId));
    }

    // ================= Save: update =================
    [Fact]
    public async Task Save_UT_RXS_32_update_replaces_items_and_bumps_version()
    {
        await using var env = await NewEnv();
        var created = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        var update = new SavePatientPrescription(Guid.NewGuid(), 1, env.N, Today.AddDays(-1), "Chẩn đoán mới", null, "Nghỉ ngơi", [new(env.Med2.Id, 4, "Uống khi đau", "viên")]);
        var dto = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, created.Id, update, CancellationToken.None);
        Assert.Equal((created.Id, 2, Today.AddDays(-1), "Chẩn đoán mới"), (dto.Id, dto.Version, dto.PrescribedOn, dto.Diagnosis));
        var item = Assert.Single(dto.Items);
        Assert.Equal((env.Med2.Id, 4, "Uống khi đau"), (item.MedicineId, item.Quantity, item.Instructions));
        Assert.Equal(1, await env.Db.PatientPrescriptions.CountAsync(x => x.PatientId == env.PatientId));
        var audit = await env.Db.PrescriptionAudits.SingleAsync(x => x.ActorId == env.Dentist.Id && x.BranchId == env.N && x.Action == "PatientPrescriptionUpdate");
        Assert.Equal("Cập nhật đơn tại hồ sơ bệnh nhân", audit.Reason);
        Assert.NotEqual("{}", audit.BeforeJson);
    }

    [Fact]
    public async Task Save_UT_RXS_33_update_with_stale_version_conflicts_and_reports_current_version()
    {
        await using var env = await NewEnv();
        var created = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, created.Id, env.Valid() with { ExpectedVersion = 5 }, CancellationToken.None));
        Assert.Equal((409, "MSG111", 1), (error.Status, error.Response.Code, error.Response.CurrentVersion));
    }

    [Fact]
    public async Task Save_UT_RXS_34_update_of_unknown_or_foreign_prescription_is_not_found()
    {
        await using var env = await NewEnv();
        var ofOther = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.OtherPatientId, null, env.Valid(), CancellationToken.None);
        foreach (var id in new[] { Guid.NewGuid(), ofOther.Id })
        {
            var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, id, env.Valid() with { ExpectedVersion = 1 }, CancellationToken.None));
            Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
        }
    }

    [Fact]
    public async Task Save_UT_RXS_35_update_of_prescription_in_another_branch_is_forbidden()
    {
        await using var env = await NewEnv();
        await CreateTestKit.SeedStaffAsync(env.Db, "mem" + env.N, AccountRoles.Dentist, env.OtherBranch);
        var elsewhere = await env.Svc(branches: [env.N, env.OtherBranch]).SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid() with { BranchId = env.OtherBranch }, CancellationToken.None);
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, elsewhere.Id, env.Valid() with { ExpectedVersion = 1 }, CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    // ================= Own* (customer portal) =================
    private static async Task Link(Env env, ApplicationUser account, int patientId, string status)
    {
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = account.Id, PatientId = patientId, Status = status, RequestCode = "LK" + CreateTestKit.NextId() });
        await env.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Own_UT_RXW_01_lists_only_prescriptions_of_verified_linked_patients()
    {
        await using var env = await NewEnv();
        var mine = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.OtherPatientId, null, env.Valid(), CancellationToken.None);
        await Link(env, env.Customer, env.PatientId, PatientLinkStatus.Verified);
        await Link(env, env.Customer, env.OtherPatientId, PatientLinkStatus.Pending); // not verified: no access

        var list = await env.Svc().OwnPatientPrescriptionsAsync(env.Customer.Id, CancellationToken.None);
        Assert.Equal([mine.Id], list.Select(x => x.Id));
    }

    [Fact]
    public async Task Own_UT_RXW_02_account_without_link_sees_nothing()
    {
        await using var env = await NewEnv();
        await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        Assert.Empty(await env.Svc().OwnPatientPrescriptionsAsync(env.Stranger.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData("UT-RXW-03 Liên kết đã xác minh", PatientLinkStatus.Verified, true)]
    [InlineData("UT-RXW-04 Liên kết đang chờ", PatientLinkStatus.Pending, false)]
    [InlineData("UT-RXW-05 Liên kết đã thu hồi", PatientLinkStatus.Revoked, false)]
    [InlineData("UT-RXW-06 Liên kết bị từ chối", PatientLinkStatus.Rejected, false)]
    public async Task OwnDetail_depends_on_link_status(string caseName, string status, bool visible)
    {
        await using var env = await NewEnv();
        var rx = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        await Link(env, env.Customer, env.PatientId, status);
        var call = () => env.Svc().OwnPatientPrescriptionAsync(env.Customer.Id, rx.Id, CancellationToken.None);
        if (visible) { Assert.Equal(rx.Id, (await call()).Id); return; }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task OwnDetail_UT_RXW_07_unknown_prescription_and_other_account_are_not_found()
    {
        await using var env = await NewEnv();
        var rx = await env.Svc().SavePatientProfilePrescriptionAsync(env.Dentist.Id, env.PatientId, null, env.Valid(), CancellationToken.None);
        await Link(env, env.Customer, env.PatientId, PatientLinkStatus.Verified);
        foreach (var (account, id) in new[] { (env.Customer.Id, Guid.NewGuid()), (env.Stranger.Id, rx.Id) })
        {
            var error = await CreateTestKit.ErrorOf(() => env.Svc().OwnPatientPrescriptionAsync(account, id, CancellationToken.None));
            Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
        }
    }
}
