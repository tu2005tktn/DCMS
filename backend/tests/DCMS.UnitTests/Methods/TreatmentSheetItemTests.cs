using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of the treatment-sheet item commands of TreatmentDossierService (TreatmentDirectSheetService.cs):
/// MarkSheetItemAsync, DeleteSheetItemAsync, EditSheetItemAsync and their private helpers (EditableSheetItemAsync, SheetCommandAsync,
/// DossierAsync, DentistAsync, ReadRow). PerformSheetItemAsync needs the clinical posting port and is not covered here.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentSheetItemTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const int Version = 5;

    private sealed class DossierAccess(bool owner, bool assigned, int[] branches, params string[] denied) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, assigned, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Dentist, Dentist2, Assistant, DentistEmp, Dentist2Emp, AssistantEmp, Patient;
        public required Dictionary<string, int> Actors;
        public required int S1, S2, SOff, SElsewhere;
        public required Guid DossierId, ItemId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public TreatmentDossierService Svc(ISettingsAccess? access = null) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new UnavailableTreatmentFinancialSummaryPort(), null!, access ?? new DossierAccess(false, false, [N]));
        public int Actor(string key) => key == "ghost" ? 987654321 : Actors[key];
        public Task<TreatmentChange> Mark(string actor = "dentist", bool completed = true, int version = Version, Guid? key = null, Guid? dossier = null, Guid? item = null, ISettingsAccess? access = null) =>
            Svc(access).MarkSheetItemAsync(Actor(actor), dossier ?? DossierId, item ?? ItemId, new MarkSheetItemRequest(version, completed, key ?? Guid.NewGuid()), CancellationToken.None);
        public Task<TreatmentChange> Delete(string actor = "dentist", int version = Version, Guid? key = null, Guid? dossier = null, Guid? item = null) =>
            Svc().DeleteSheetItemAsync(Actor(actor), dossier ?? DossierId, item ?? ItemId, new SheetItemActionRequest(version, key ?? Guid.NewGuid()), CancellationToken.None);
        public Task<TreatmentChange> Edit(JsonElement row, string actor = "dentist", int version = Version, Guid? key = null, Guid? dossier = null, Guid? item = null, ISettingsAccess? access = null) =>
            Svc(access).EditSheetItemAsync(Actor(actor), dossier ?? DossierId, item ?? ItemId, new EditSheetItemRequest(version, row, key ?? Guid.NewGuid()), CancellationToken.None);
        public async Task<TreatmentItem> Item(Guid? id = null) => await Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == (id ?? ItemId));
        public async Task<TreatmentDossier> Dossier() => await Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == DossierId);

        /// <summary>A row the edit accepts: same service, quantity 2, price 100,000, no discount, performed by the dossier's dentist.</summary>
        public Dictionary<string, object?> Row() => new()
        {
            ["date"] = "2030-01-12", ["serviceId"] = S1, ["dentistId"] = DentistEmp, ["regions"] = new[] { "11", "12" }, ["quantity"] = 2, ["costVnd"] = 200000,
            ["note"] = "  ghi chú  ", ["unitPriceVnd"] = 100000, ["discountValue"] = 0, ["estimatedMinutes"] = 45
        };
    }

    private static JsonElement Json(Dictionary<string, object?> row) => JsonSerializer.SerializeToElement(row, Web);

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        var n = CreateTestKit.NextId(); var other = CreateTestKit.NextId();
        var actors = new Dictionary<string, int>();
        async Task<int> Staff(string key, string role, int branch)
        {
            var user = await CreateTestKit.SeedStaffAsync(db, key + n, role, branch);
            actors[key] = user.Id;
            return (await db.Employees.SingleAsync(x => x.AccountId == user.Id)).Id;
        }
        var dentistEmp = await Staff("dentist", AccountRoles.Dentist, n);
        var dentist2Emp = await Staff("dentist2", AccountRoles.Dentist, n);
        var assistantEmp = await Staff("assistant", AccountRoles.ClinicAssistant, n);
        await Staff("elsewhere", AccountRoles.Dentist, other);
        var inactive = await Staff("inactive", AccountRoles.Dentist, n);
        (await db.Users.SingleAsync(x => x.Id == actors["inactive"])).IsActive = false;
        var customer = new ApplicationUser { UserName = "kh" + n, FullName = "Khách", AccountType = "Public", IsActive = true, AssignedRoles = [AccountRoles.Dentist] };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        actors["customer"] = customer.Id;
        // The assistant is a member that may NOT examine (so it can be the assistant of a sheet row).
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        branch.Members.Single(m => m.EmployeeId == assistantEmp).CanDentist = false;
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        SchedulingService S(int b, string name, string unit, bool active = true) => new() { BranchId = b, Name = name, Unit = unit, Active = active };
        var s1 = S(n, "Trám răng", "lần"); var s2 = S(n, "Nhổ răng", "răng"); var off = S(n, "Dịch vụ ngừng", "lần", false); var far = S(other, "Dịch vụ chi nhánh khác", "lần");
        db.Set<SchedulingService>().AddRange(s1, s2, off, far);
        await db.SaveChangesAsync();
        var dossier = new TreatmentDossier
        {
            Code = "HS" + n, PatientId = patient.Id, BranchId = n, DentistId = dentistEmp, StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = "Open",
            Version = Version, CreatedAt = Now, UpdatedAt = Now
        };
        var item = new TreatmentItem
        {
            DossierId = dossier.Id, PlannedOrder = 1, ServiceId = s1.Id, ServiceName = "Trám răng", Unit = "lần", Quantity = 2, AgreedUnitPriceVnd = 100000,
            ResponsibleDentistId = dentistEmp
        };
        dossier.Items.Add(item);
        db.TreatmentDossiers.Add(dossier);
        await db.SaveChangesAsync();
        return new Env
        {
            Pg = pg, N = n, Other = other, Actors = actors, Dentist = actors["dentist"], Dentist2 = actors["dentist2"], Assistant = actors["assistant"],
            DentistEmp = dentistEmp, Dentist2Emp = dentist2Emp, AssistantEmp = assistantEmp, Patient = patient.Id,
            S1 = s1.Id, S2 = s2.Id, SOff = off.Id, SElsewhere = far.Id, DossierId = dossier.Id, ItemId = item.Id
        };
    }

    private static async Task AssertError(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= Guards shared by Mark / Delete / Edit =================
    // Input: command name + the argument that is wrong -> Expected: BusinessException (status, code).
    public static TheoryData<string, string, string, int, string> Guards()
    {
        var data = new TheoryData<string, string, string, int, string>();
        var n = 0;
        foreach (var command in new[] { "mark", "delete", "edit" })
        {
            void Add(string what, string problem, int status, string code) => data.Add($"UT-TSI-{++n:D2} {command}: {what}", command, problem, status, code);
            Add("RequestKey rỗng", "emptyKey", 400, "MSG04");
            Add("Hồ sơ không tồn tại", "noDossier", 404, "MSG113");
            Add("Hồ sơ đã xóa", "deletedDossier", 404, "MSG113");
            Add("Hạng mục không tồn tại", "noItem", 404, "MSG113");
            Add("Phiên bản hồ sơ cũ", "staleVersion", 409, "MSG111");
            Add("Tài khoản không phải bác sĩ (trợ lý)", "assistant", 403, "MSG06");
            Add("Bác sĩ khác trong chi nhánh (không phụ trách hồ sơ)", "otherDentist", 403, "MSG06");
            Add("Bác sĩ chi nhánh khác", "elsewhere", 403, "MSG06");
            Add("Tài khoản bị khóa", "inactive", 403, "MSG06");
            Add("Tài khoản không phải nhân viên", "customer", 403, "MSG06");
            Add("Tài khoản không tồn tại", "ghost", 403, "MSG06");
            Add("Phạm vi được giao không có chi nhánh", "scopeOutside", 403, "MSG06");
        }
        return data;
    }

    [Theory, MemberData(nameof(Guards))]
    public async Task Commands_reject_invalid_calls(string caseName, string command, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        if (problem == "deletedDossier")
        {
            (await env.Db.TreatmentDossiers.SingleAsync(x => x.Id == env.DossierId)).State = "Deleted";
            await env.Db.SaveChangesAsync();
        }
        var actor = problem is "assistant" or "otherDentist" or "elsewhere" or "inactive" or "customer" or "ghost"
            ? (problem == "otherDentist" ? "dentist2" : problem) : "dentist";
        var key = problem == "emptyKey" ? Guid.Empty : Guid.NewGuid();
        var dossier = problem == "noDossier" ? Guid.NewGuid() : (Guid?)null;
        var item = problem == "noItem" ? Guid.NewGuid() : (Guid?)null;
        var version = problem == "staleVersion" ? Version - 1 : Version;
        ISettingsAccess? access = problem == "scopeOutside" ? new DossierAccess(false, true, [env.Other]) : null;
        Func<Task> call = command switch
        {
            "mark" => () => env.Mark(actor, true, version, key, dossier, item, access),
            "delete" => problem == "scopeOutside" ? () => env.Svc(access).DeleteSheetItemAsync(env.Actor(actor), env.DossierId, env.ItemId, new SheetItemActionRequest(version, key), CancellationToken.None)
                                                  : () => env.Delete(actor, version, key, dossier, item),
            _ => () => env.Edit(Json(env.Row()), actor, version, key, dossier, item, access)
        };
        await AssertError(call, status, code);
        Assert.Equal((Version, 1), ((await env.Dossier()).Version, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId)));
    }

    // ================= MarkSheetItemAsync =================
    [Fact]
    public async Task Mark_UT_TSI_37_marks_and_unmarks_the_item()
    {
        await using var env = await NewEnv();
        var change = await env.Mark(completed: true);
        Assert.Equal((Version + 1, "MSG222"), (change.Dossier.Version, change.Notification.Code));
        Assert.True((await env.Item()).DisplayCompleted);
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "SheetItemMarked" && e.DossierVersion == Version + 1);

        await env.Mark(completed: false, version: Version + 1);
        Assert.False((await env.Item()).DisplayCompleted);
    }

    [Fact]
    public async Task Mark_UT_TSI_38_owner_scope_bypasses_membership_but_needs_the_branch()
    {
        await using var env = await NewEnv();
        await env.Mark(access: new DossierAccess(true, true, [env.N]));
        await AssertError(() => env.Mark(version: Version + 1, access: new DossierAccess(true, true, [env.Other])), 403, "MSG06");
    }

    [Fact]
    public async Task Mark_UT_TSI_39_same_key_replays_and_a_changed_request_conflicts()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Mark(completed: true, key: key);
        var again = await env.Mark(completed: true, key: key);
        Assert.Equal(first.Dossier.Version, again.Dossier.Version);
        Assert.Equal(Version + 1, (await env.Dossier()).Version); // applied once
        await AssertError(() => env.Mark(completed: false, key: key), 409, "MSG111");
        await AssertError(() => env.Delete(key: key, version: Version + 1), 409, "MSG111");
    }

    // ================= DeleteSheetItemAsync =================
    [Fact]
    public async Task Delete_UT_TSI_40_removes_the_item_and_detaches_its_activities()
    {
        await using var env = await NewEnv();
        var keep = new TreatmentItem { DossierId = env.DossierId, PlannedOrder = 2, ServiceId = env.S2, ServiceName = "Nhổ răng", Unit = "răng", Quantity = 1, AgreedUnitPriceVnd = 50000 };
        env.Db.TreatmentItems.Add(keep);
        ClinicalActivity A(string? snapshot) => new()
        {
            DossierId = env.DossierId, ItemId = env.ItemId, ServiceId = env.S1, ServiceNameSnapshot = snapshot, Unit = "lần", Quantity = 1, PerformedAt = Now,
            PerformerId = env.DentistEmp, Outcome = "ok", IsPlanTransfer = true, CreatedAt = Now
        };
        var named = A("Tên đã lưu"); var unnamed = A(null);
        env.Db.ClinicalActivities.AddRange(named, unnamed);
        await env.Db.SaveChangesAsync();

        var change = await env.Delete();
        Assert.Equal(Version + 1, change.Dossier.Version);
        Assert.Equal([keep.Id], env.Db.TreatmentItems.Where(x => x.DossierId == env.DossierId).Select(x => x.Id));
        var rows = await env.Db.ClinicalActivities.AsNoTracking().Where(x => x.DossierId == env.DossierId).ToDictionaryAsync(x => x.Id);
        Assert.All(rows.Values, a => Assert.Equal((null, false), ((Guid?)a.ItemId, a.IsPlanTransfer)));
        Assert.Equal("Tên đã lưu", rows[named.Id].ServiceNameSnapshot);
        Assert.Equal("Trám răng", rows[unnamed.Id].ServiceNameSnapshot); // filled from the deleted item
        var plan = await env.Db.TreatmentEvents.AsNoTracking().SingleAsync(x => x.DossierId == env.DossierId && x.Kind == "PlanCommitted");
        Assert.Equal(1, JsonDocument.Parse(plan.PayloadJson).RootElement.GetProperty("planVersion").GetInt32());
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "SheetItemDeleted");
    }

    // ================= EditSheetItemAsync: row validation =================
    // Input: a mutation of the valid row -> Expected: MSG04 (400).
    public static TheoryData<string, Action<Dictionary<string, object?>>> BadRows => new()
    {
        { "UT-TSI-41 Ghi chú lab dài 1001 ký tự", r => r["labWork"] = new string('a', 1001) },
        { "UT-TSI-42 Thời gian ước tính 0 phút", r => r["estimatedMinutes"] = 0 },
        { "UT-TSI-43 Thời gian ước tính 481 phút", r => r["estimatedMinutes"] = 481 },
        { "UT-TSI-44 serviceId 0", r => r["serviceId"] = 0 },
        { "UT-TSI-45 dentistId 0", r => r["dentistId"] = 0 },
        { "UT-TSI-46 Thành tiền âm", r => r["costVnd"] = -1 },
        { "UT-TSI-47 Số lượng 0", r => r["quantity"] = 0 },
        { "UT-TSI-48 Số lượng 1.0001 (4 chữ số thập phân)", r => r["quantity"] = 1.0001m },
        { "UT-TSI-49 Số lượng lớn hơn 999999", r => r["quantity"] = 1000000 },
        { "UT-TSI-50 Đơn giá âm", r => r["unitPriceVnd"] = -1 },
        { "UT-TSI-51 Đơn giá quá 1.000 tỷ", r => r["unitPriceVnd"] = 1_000_000_000_001L },
        { "UT-TSI-52 Ghi chú dài 1001 ký tự", r => r["note"] = new string('a', 1001) },
        { "UT-TSI-53 Vùng điều trị dài 81 ký tự", r => r["region"] = new string('a', 81) },
        { "UT-TSI-54 Hơn 32 răng", r => r["regions"] = Enumerable.Repeat("11", 33).ToArray() },
        { "UT-TSI-55 Mã răng không phải số", r => r["regions"] = new[] { "abc" } },
        { "UT-TSI-56 Mã răng không hợp lệ (19)", r => r["regions"] = new[] { "19" } },
        { "UT-TSI-57 Danh sách răng nối lại quá 80 ký tự", r => r["regions"] = Enumerable.Repeat("11", 32).ToArray() },
        { "UT-TSI-58 Không có đơn giá và thành tiền không chia hết", r => { r.Remove("unitPriceVnd"); r["costVnd"] = 100001; } },
        { "UT-TSI-59 serviceId sai kiểu dữ liệu", r => r["serviceId"] = "abc" },
        { "UT-TSI-60 Số lượng khác số lượng của hạng mục", r => { r["quantity"] = 3; r["costVnd"] = 300000; } },
        { "UT-TSI-61 Bác sĩ thực hiện khác bác sĩ phụ trách hồ sơ", r => r["dentistId"] = 999999 },
        { "UT-TSI-62 Thành tiền không bằng đơn giá nhân số lượng", r => r["costVnd"] = 199999 }
    };

    [Theory, MemberData(nameof(BadRows))]
    public async Task Edit_rejects_invalid_rows(string caseName, Action<Dictionary<string, object?>> mutate)
    {
        await using var env = await NewEnv();
        var row = env.Row(); mutate(row);
        await AssertError(() => env.Edit(Json(row)), 400, "MSG04");
        Assert.Equal(Version, (await env.Dossier()).Version);
    }

    [Fact]
    public async Task Edit_UT_TSI_63_dossier_must_be_open()
    {
        await using var env = await NewEnv();
        (await env.Db.TreatmentDossiers.SingleAsync(x => x.Id == env.DossierId)).State = "Closed";
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.Edit(Json(env.Row())), 409, "MSG230");
    }

    [Fact]
    public async Task Edit_UT_TSI_64_service_must_be_active_and_belong_to_the_branch()
    {
        await using var env = await NewEnv();
        foreach (var service in new[] { env.SOff, env.SElsewhere, 987654321 })
        {
            var row = env.Row(); row["serviceId"] = service;
            await AssertError(() => env.Edit(Json(row)), 400, "MSG04");
        }
    }

    [Fact]
    public async Task Edit_UT_TSI_65_quantity_must_suit_the_unit_of_the_service()
    {
        await using var env = await NewEnv();
        var item = await env.Db.TreatmentItems.SingleAsync(x => x.Id == env.ItemId);
        item.Quantity = 1.5m; // "răng" is a whole-number unit
        await env.Db.SaveChangesAsync();
        var row = env.Row(); row["serviceId"] = env.S2; row["quantity"] = 1.5; row["costVnd"] = 150000;
        await AssertError(() => env.Edit(Json(row)), 400, "MSG04");
    }

    [Fact]
    public async Task Edit_UT_TSI_66_assistant_must_be_an_active_non_dentist_member_of_the_branch()
    {
        await using var env = await NewEnv();
        foreach (var assistant in new[] { env.Dentist2Emp, 987654321 }) // a dentist, and a stranger
        {
            var row = env.Row(); row["assistantId"] = assistant;
            await AssertError(() => env.Edit(Json(row)), 400, "MSG04");
        }
        var ok = env.Row(); ok["assistantId"] = env.AssistantEmp;
        await env.Edit(Json(ok));
        Assert.Equal(env.AssistantEmp, (await env.Item()).AssistantId);
    }

    // Input: price/discount change + the "treatment.items.price" permission -> Expected: allowed or MSG06.
    [Theory]
    [InlineData("UT-TSI-67 Đổi đơn giá, có quyền", 120000, 0, false, true)]
    [InlineData("UT-TSI-68 Đổi đơn giá, không có quyền", 120000, 0, true, false)]
    [InlineData("UT-TSI-69 Thêm giảm giá, không có quyền", 100000, 10, true, false)]
    [InlineData("UT-TSI-70 Không đổi giá/giảm giá, không cần quyền", 100000, 0, true, true)]
    public async Task Edit_price_or_discount_change_needs_the_price_permission(string caseName, long unitPrice, int discount, bool denied, bool allowed)
    {
        await using var env = await NewEnv();
        var row = env.Row(); row["unitPriceVnd"] = unitPrice; row["costVnd"] = unitPrice * 2;
        if (discount > 0) { row["discountType"] = "Percent"; row["discountValue"] = discount; row["discountNote"] = "Khách thân thiết"; }
        var access = new DossierAccess(false, false, [env.N], denied ? ["treatment.items.price"] : []);
        if (allowed) { await env.Edit(Json(row), access: access); return; }
        await AssertError(() => env.Edit(Json(row), access: access), 403, "MSG06");
        Assert.Equal(100000L, (await env.Item()).AgreedUnitPriceVnd);
    }

    // Input: discount type/value/note -> Expected: MSG252 when invalid.
    [Theory]
    [InlineData("UT-TSI-71 Giảm giá âm", "Percent", -1, "Lý do", false)]
    [InlineData("UT-TSI-72 Có giá trị nhưng thiếu loại", null, 10, "Lý do", false)]
    [InlineData("UT-TSI-73 Loại giảm giá lạ", "Other", 10, "Lý do", false)]
    [InlineData("UT-TSI-74 Thiếu lý do", "Percent", 10, "  ", false)]
    [InlineData("UT-TSI-75 Phần trăm lớn hơn 100", "Percent", 101, "Lý do", false)]
    [InlineData("UT-TSI-76 Giảm cố định lớn hơn thành tiền", "Fixed", 200001, "Lý do", false)]
    [InlineData("UT-TSI-77 Phần trăm 100% (biên)", "Percent", 100, "Lý do", true)]
    [InlineData("UT-TSI-78 Loại VND được hiểu là Fixed", "VND", 50000, "Lý do", true)]
    [InlineData("UT-TSI-79 Giảm cố định đúng bằng thành tiền (biên)", "Fixed", 200000, "Lý do", true)]
    public async Task Edit_validates_discount(string caseName, string? type, int value, string note, bool valid)
    {
        await using var env = await NewEnv();
        var row = env.Row(); row["discountType"] = type; row["discountValue"] = value; row["discountNote"] = note;
        if (valid)
        {
            await env.Edit(Json(row));
            var item = await env.Item();
            Assert.Equal((type == "VND" ? "Fixed" : type, (decimal?)value, "Lý do"), (item.DiscountType, item.DiscountValue, item.DiscountReason));
            return;
        }
        await AssertError(() => env.Edit(Json(row)), 400, "MSG252");
        Assert.Null((await env.Item()).DiscountType);
    }

    // ================= EditSheetItemAsync: success =================
    [Fact]
    public async Task Edit_UT_TSI_80_updates_the_item_and_records_events()
    {
        await using var env = await NewEnv();
        var row = env.Row(); row["serviceId"] = env.S1; row["assistantId"] = env.AssistantEmp;
        var change = await env.Edit(Json(row));

        Assert.Equal((Version + 1, "MSG222"), (change.Dossier.Version, change.Notification.Code));
        var item = await env.Item();
        Assert.Equal((env.S1, "Trám răng", "lần", "11, 12", "ghi chú", new DateOnly(2030, 1, 12), 45, env.DentistEmp, env.AssistantEmp, 100000L, (string?)null, (decimal?)null),
            (item.ServiceId, item.ServiceName, item.Unit, item.Region, item.Note, item.EstimatedStart, item.EstimatedMinutes, item.ResponsibleDentistId, item.AssistantId, item.AgreedUnitPriceVnd, item.DiscountType, item.DiscountValue));
        var events = await env.Db.TreatmentEvents.AsNoTracking().Where(x => x.DossierId == env.DossierId).ToListAsync();
        Assert.Contains(events, e => e.Kind == "SheetItemEdited" && e.DossierVersion == Version + 1);
        Assert.Equal(2, JsonDocument.Parse(events.Single(e => e.Kind == "PlanCommitted").PayloadJson).RootElement.GetProperty("planVersion").GetInt32());
    }

    [Fact]
    public async Task Edit_UT_TSI_81_without_unit_price_derives_it_from_cost_and_quantity()
    {
        await using var env = await NewEnv();
        var row = env.Row(); row.Remove("unitPriceVnd"); row["costVnd"] = 200000; row["region"] = null; row["regions"] = null; row["note"] = "   ";
        await env.Edit(Json(row));
        var item = await env.Item();
        Assert.Equal((100000L, (string?)null, (string?)null), (item.AgreedUnitPriceVnd, item.Region, item.Note));
    }

    [Fact]
    public async Task Edit_UT_TSI_82_removing_the_discount_clears_it()
    {
        await using var env = await NewEnv();
        var withDiscount = env.Row(); withDiscount["discountType"] = "Percent"; withDiscount["discountValue"] = 10; withDiscount["discountNote"] = "VIP";
        await env.Edit(Json(withDiscount));
        await env.Edit(Json(env.Row()), version: Version + 1);
        var item = await env.Item();
        Assert.Equal(((string?)null, (decimal?)null, (string?)null), (item.DiscountType, item.DiscountValue, item.DiscountReason));
    }
}
