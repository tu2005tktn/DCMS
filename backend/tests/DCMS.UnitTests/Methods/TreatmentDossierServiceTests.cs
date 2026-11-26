using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
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
/// Method-level tests of TreatmentDossierService (the dossier core): CreateContextAsync, CreateAsync, QuickCreateAsync extras, ListAsync, GetAsync,
/// UpdateNoteAsync, SheetDraftsAsync, SaveSheetDraftAsync, DeleteSheetDraftAsync, DeleteAsync, HistoryAsync, CloseAsync, ReopenAsync,
/// PricePreviewAsync, OwnSummaryAsync. The sheet-item commands are in TreatmentSheetItemTests.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentDossierServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const int Version = 5;

    private sealed class DossierAccess(bool owner, bool assigned, int[] branches) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, assigned, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, DentistEmp, Dentist2Emp, Patient, ArchivedPatient, S1, SOff;
        public required Dictionary<string, int> Actors;
        public required Guid DossierId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public int Actor(string key) => key == "ghost" ? 987654321 : Actors[key];
        public TreatmentDossierService Svc(ISettingsAccess? access = null) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new UnavailableTreatmentFinancialSummaryPort(), null!, access ?? new DossierAccess(false, false, [N]));
        public async Task<TreatmentDossier> Dossier() => await Db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == DossierId);
        public async Task<TreatmentDossier> SetState(string state, Guid? id = null)
        {
            var d = await Db.TreatmentDossiers.SingleAsync(x => x.Id == (id ?? DossierId));
            d.State = state; await Db.SaveChangesAsync(); return d;
        }

        public async Task<TreatmentDossier> NewDossier(string state = "Open", int? patient = null, int version = Version, int updatedMinutes = 0, int? branch = null)
        {
            var d = new TreatmentDossier { Code = "HS" + N + "-" + Guid.NewGuid().ToString("N")[..6], PatientId = patient ?? Patient, BranchId = branch ?? N, DentistId = DentistEmp,
                StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = state, Version = version, CreatedAt = Now, UpdatedAt = Now.AddMinutes(updatedMinutes) };
            Db.TreatmentDossiers.Add(d);
            await Db.SaveChangesAsync();
            return d;
        }

        public async Task<TreatmentItem> AddItem(Guid? dossier = null, long price = 100000, string? discountType = null, decimal? discount = null)
        {
            var item = new TreatmentItem { DossierId = dossier ?? DossierId, PlannedOrder = 1, ServiceId = S1, ServiceName = "Trám răng", Unit = "lần", Quantity = 2, AgreedUnitPriceVnd = price,
                DiscountType = discountType, DiscountValue = discount, DiscountReason = discount > 0 ? "VIP" : null };
            Db.TreatmentItems.Add(item);
            await Db.SaveChangesAsync();
            return item;
        }

        public async Task<TreatmentChange> Close(string outcome = "CompletedAsAgreed", string? reason = null, int version = Version, Guid? key = null, string actor = "dentist") =>
            await Svc().CloseAsync(Actor(actor), DossierId, new CloseDossierRequest(version, outcome, reason, key ?? Guid.NewGuid()), CancellationToken.None);
        public async Task<TreatmentChange> Reopen(string? reason = "Làm lại", int version = Version, Guid? key = null) =>
            await Svc().ReopenAsync(Actor("dentist"), DossierId, new ReopenDossierRequest(version, reason, key ?? Guid.NewGuid()), CancellationToken.None);
        public async Task<TreatmentChange> Delete(int version = Version, Guid? key = null, Guid? dossier = null) =>
            await Svc().DeleteAsync(Actor("dentist"), dossier ?? DossierId, new DeleteDossierRequest(version, key ?? Guid.NewGuid()), CancellationToken.None);
        public async Task<TreatmentChange> Note(string? note, int version = Version, Guid? key = null) =>
            await Svc().UpdateNoteAsync(Actor("dentist"), DossierId, new UpdateDossierNoteRequest(version, note, key ?? Guid.NewGuid()), CancellationToken.None);
        public CreateDossierRequest NewRequest(string? diagnosis = "Viêm tủy", Guid? key = null) =>
            new(N, DentistEmp, diagnosis, null, null, key ?? Guid.NewGuid());
        public Task<TreatmentChange> Create(CreateDossierRequest request, string actor = "dentist", int? patient = null) =>
            Svc().CreateAsync(Actor(actor), patient ?? Patient, request, CancellationToken.None);

        public JsonElement Row(Action<Dictionary<string, object?>>? change = null)
        {
            var row = new Dictionary<string, object?> { ["date"] = "2030-01-12", ["serviceId"] = S1, ["dentistId"] = DentistEmp, ["regions"] = new[] { "11" }, ["quantity"] = 1, ["costVnd"] = 100000, ["unitPriceVnd"] = 100000, ["discountValue"] = 0 };
            change?.Invoke(row);
            return JsonSerializer.SerializeToElement(row, Web);
        }
        public Task<SheetDraftDto> SaveDraft(Guid? rowId = null, string kind = "Plan", Guid? visit = null, JsonElement? row = null, int version = 0, Guid? key = null, string actor = "dentist") =>
            Svc().SaveSheetDraftAsync(Actor(actor), DossierId, rowId ?? Guid.NewGuid(), new SaveSheetDraftRequest(kind, visit, row ?? Row(), version, key ?? Guid.NewGuid()), CancellationToken.None);
        public Task Ledger(Guid dossier) => Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AUDIT_LOG" ("StorageId","AuditType","Action","Target","PayloadJson") VALUES ({Guid.NewGuid()}, 'FinanceLedger', 'TreatmentCashPayment', {dossier.ToString()}, CAST({"{}"} AS jsonb))
            """);
    }

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
        async Task<int> Staff(string key, string role, int branch, bool member = true)
        {
            var user = await CreateTestKit.SeedStaffAsync(db, key + n, role, branch, member);
            actors[key] = user.Id;
            return member ? (await db.Employees.SingleAsync(x => x.AccountId == user.Id)).Id : 0;
        }
        var dentistEmp = await Staff("dentist", AccountRoles.Dentist, n);
        var dentist2Emp = await Staff("dentist2", AccountRoles.Dentist, n);
        await Staff("assistant", AccountRoles.ClinicAssistant, n);
        await Staff("elsewhere", AccountRoles.Dentist, other);
        await Staff("noEmployee", AccountRoles.Dentist, n, member: false);
        await Staff("inactive", AccountRoles.Dentist, n);
        (await db.Users.SingleAsync(x => x.Id == actors["inactive"])).IsActive = false;
        var customer = new ApplicationUser { UserName = "kh" + n, FullName = "Khách", AccountType = "Public", IsActive = true, AssignedRoles = [AccountRoles.Dentist] };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        actors["customer"] = customer.Id;
        PatientProfile P(string code, bool archived = false) => new() { PatientCode = code + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), IsArchived = archived, CreatedAt = Now, UpdatedAt = Now };
        var patient = P("BN"); var archived = P("BA", true);
        db.Patients.AddRange(patient, archived);
        var s1 = new SchedulingService { BranchId = n, Name = "Trám răng", Unit = "lần" };
        var off = new SchedulingService { BranchId = n, Name = "Dịch vụ ngừng", Unit = "lần", Active = false };
        db.Set<SchedulingService>().AddRange(s1, off);
        await db.SaveChangesAsync();
        var env = new Env { Pg = pg, N = n, Other = other, Actors = actors, DentistEmp = dentistEmp, Dentist2Emp = dentist2Emp, Patient = patient.Id, ArchivedPatient = archived.Id, S1 = s1.Id, SOff = off.Id, DossierId = Guid.Empty };
        var dossier = await env.NewDossier();
        return new Env { Pg = pg, N = n, Other = other, Actors = actors, DentistEmp = dentistEmp, Dentist2Emp = dentist2Emp, Patient = patient.Id, ArchivedPatient = archived.Id, S1 = s1.Id, SOff = off.Id, DossierId = dossier.Id };
    }

    private static async Task AssertError(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= CreateContextAsync =================
    [Theory]
    [InlineData("UT-TDS-01 Trợ lý (không phải bác sĩ)", "assistant", 403, "MSG06")]
    [InlineData("UT-TDS-02 Tài khoản khóa", "inactive", 403, "MSG06")]
    [InlineData("UT-TDS-03 Không phải tài khoản nhân viên", "customer", 403, "MSG06")]
    [InlineData("UT-TDS-04 Tài khoản không tồn tại", "ghost", 403, "MSG06")]
    [InlineData("UT-TDS-05 Bác sĩ chưa có hồ sơ nhân viên", "noEmployee", 403, "MSG06")]
    public async Task CreateContext_rejects_non_dentists(string caseName, string actor, int status, string code)
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Svc().CreateContextAsync(env.Actor(actor), env.Patient, CancellationToken.None), status, code);
    }

    [Fact]
    public async Task CreateContext_UT_TDS_06_unknown_or_archived_patient_is_not_found()
    {
        await using var env = await NewEnv();
        foreach (var id in new[] { 987654321, env.ArchivedPatient })
            await AssertError(() => env.Svc().CreateContextAsync(env.Actor("dentist"), id, CancellationToken.None), 404, "MSG113");
    }

    [Fact]
    public async Task CreateContext_UT_TDS_07_lists_active_branches_where_the_dentist_may_examine_in_name_order()
    {
        await using var env = await NewEnv();
        var second = await env.Db.Set<SchedulingBranch>().SingleAsync(x => x.Id == env.Other);
        second.Members.Add(new() { BranchId = env.Other, EmployeeId = env.DentistEmp, CanDentist = true, Active = true });
        await env.Db.SaveChangesAsync();
        var ctx = await env.Svc().CreateContextAsync(env.Actor("dentist"), env.Patient, CancellationToken.None);
        Assert.Equal(env.DentistEmp, ctx.DentistId);
        Assert.Equal([env.N, env.Other], ctx.Branches.Select(x => x.Id).Order());

        second.Active = false; await env.Db.SaveChangesAsync();
        Assert.Equal([env.N], (await env.Svc().CreateContextAsync(env.Actor("dentist"), env.Patient, CancellationToken.None)).Branches.Select(x => x.Id));
        var scoped = await env.Svc(new DossierAccess(false, true, [env.Other])).CreateContextAsync(env.Actor("dentist"), env.Patient, CancellationToken.None);
        Assert.Empty(scoped.Branches); // the assigned scope removes the branch
    }

    // ================= CreateAsync / QuickCreateAsync =================
    [Fact]
    public async Task Create_UT_TDS_08_creates_a_dossier_with_defaults_and_a_creation_event()
    {
        await using var env = await NewEnv();
        var change = await env.Create(env.NewRequest("  Viêm tủy  ") with { Note = "  ghi chú  " });
        var dossier = change.Dossier;
        Assert.Equal(("Open", 1, "Viêm tủy", "ghi chú", new DateOnly(2030, 1, 10), env.N, env.DentistEmp, "MSG222"),
            (dossier.State, dossier.Version, dossier.Diagnosis, dossier.Note, dossier.StartedOn, dossier.BranchId, dossier.DentistId, change.Notification.Code));
        Assert.StartsWith("HS", dossier.Code);
        Assert.Contains(dossier.Timeline, e => e.Kind == "DossierCreated");
    }

    [Fact]
    public async Task Create_UT_TDS_09_explicit_start_date_is_kept_and_blank_note_is_dropped()
    {
        await using var env = await NewEnv();
        var change = await env.Create(env.NewRequest() with { StartedOn = new DateOnly(2029, 12, 1), Note = "   " });
        Assert.Equal((new DateOnly(2029, 12, 1), (string?)null), (change.Dossier.StartedOn, change.Dossier.Note));
    }

    // Input: a mutation of the request / actor -> Expected: BusinessException.
    public static TheoryData<string, string, int, string> CreateRejections => new()
    {
        { "UT-TDS-10 RequestKey rỗng", "emptyKey", 400, "MSG04" },
        { "UT-TDS-11 Thiếu chẩn đoán", "noDiagnosis", 400, "MSG04" },
        { "UT-TDS-12 Chẩn đoán 4001 ký tự", "longDiagnosis", 400, "MSG04" },
        { "UT-TDS-13 Ghi chú 4001 ký tự", "longNote", 400, "MSG04" },
        { "UT-TDS-14 Bác sĩ được chọn không thuộc chi nhánh", "badDentist", 403, "MSG06" },
        { "UT-TDS-15 Người gọi không phải bác sĩ", "assistant", 403, "MSG06" },
        { "UT-TDS-16 Bệnh nhân không tồn tại", "noPatient", 404, "MSG113" },
        { "UT-TDS-17 Bệnh nhân đã lưu trữ", "archived", 404, "MSG113" },
        { "UT-TDS-18 Có phiên bản lượt khám mong đợi nhưng không có lượt khám", "versionOnly", 400, "MSG04" },
        { "UT-TDS-19 Lượt khám nguồn không tồn tại", "noVisit", 404, "MSG113" }
    };

    [Theory, MemberData(nameof(CreateRejections))]
    public async Task Create_rejects_invalid_requests(string caseName, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        var request = problem switch
        {
            "emptyKey" => env.NewRequest(key: Guid.Empty),
            "noDiagnosis" => env.NewRequest(diagnosis: "   "),
            "longDiagnosis" => env.NewRequest(new string('a', 4001)),
            "longNote" => env.NewRequest() with { Note = new string('a', 4001) },
            "badDentist" => env.NewRequest() with { DentistId = env.Dentist2Emp + 100000 },
            "versionOnly" => env.NewRequest() with { ExpectedSourceVisitVersion = 1 },
            "noVisit" => env.NewRequest() with { SourceVisitId = Guid.NewGuid(), ExpectedSourceVisitVersion = 1 },
            _ => env.NewRequest()
        };
        var actor = problem == "assistant" ? "assistant" : "dentist";
        var patient = problem == "noPatient" ? 987654321 : problem == "archived" ? env.ArchivedPatient : env.Patient;
        await AssertError(() => env.Create(request, actor, patient), status, code);
        Assert.Equal(1, await env.Db.TreatmentDossiers.CountAsync(x => x.BranchId == env.N));
    }

    [Fact]
    public async Task Create_UT_TDS_20_same_key_replays_and_a_changed_request_conflicts()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Create(env.NewRequest(key: key));
        var again = await env.Create(env.NewRequest(key: key));
        Assert.Equal(first.Dossier.Id, again.Dossier.Id);
        Assert.Equal(2, await env.Db.TreatmentDossiers.CountAsync(x => x.BranchId == env.N)); // seeded + one created
        await AssertError(() => env.Create(env.NewRequest("Chẩn đoán khác", key)), 409, "MSG111");
    }

    [Fact]
    public async Task QuickCreate_UT_TDS_21_branch_must_be_one_of_the_dentists_branches_and_diagnosis_is_optional()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Svc().QuickCreateAsync(env.Actor("dentist"), env.Patient, new QuickDossierRequest(env.Other, Guid.NewGuid()), CancellationToken.None), 403, "MSG06");
        var change = await env.Svc().QuickCreateAsync(env.Actor("dentist"), env.Patient, new QuickDossierRequest(env.N, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(("", env.DentistEmp), (change.Dossier.Diagnosis, change.Dossier.DentistId));
    }

    // ================= ListAsync =================
    [Theory]
    [InlineData("UT-TDS-22 page 0", 0, 20)]
    [InlineData("UT-TDS-23 pageSize 10", 1, 10)]
    [InlineData("UT-TDS-24 page tràn int", 107374183, 20)]
    public async Task List_rejects_invalid_paging(string caseName, int page, int size)
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Svc().ListAsync(env.Actor("dentist"), env.Patient, null, page, size, CancellationToken.None), 400, "MSG04");
    }

    [Fact]
    public async Task List_UT_TDS_25_guards()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Svc().ListAsync(env.Actor("dentist"), 987654321, null, 1, 20, CancellationToken.None), 404, "MSG113");
        await AssertError(() => env.Svc().ListAsync(env.Actor("customer"), env.Patient, null, 1, 20, CancellationToken.None), 403, "MSG06");
        await AssertError(() => env.Svc().ListAsync(env.Actor("assistant"), env.Patient, null, 1, 20, CancellationToken.None), 403, "MSG06"); // no branch given: role check
        await AssertError(() => env.Svc().ListAsync(env.Actor("assistant"), env.Patient, env.N, 1, 20, CancellationToken.None), 403, "MSG06"); // branch given: dentist check
        await AssertError(() => env.Svc().ListAsync(env.Actor("dentist"), env.Patient, env.Other, 1, 20, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task List_UT_TDS_26_returns_visible_dossiers_newest_update_first_and_pages()
    {
        await using var env = await NewEnv();
        var newest = await env.NewDossier(updatedMinutes: 30);
        var deleted = await env.NewDossier(state: "Deleted", updatedMinutes: 60);
        var elsewhere = await env.NewDossier(patient: env.Patient, branch: env.Other, updatedMinutes: 90);
        var otherPatient = await env.NewDossier(patient: env.ArchivedPatient, updatedMinutes: 120);
        var all = await env.Svc().ListAsync(env.Actor("dentist"), env.Patient, null, 1, 20, CancellationToken.None);
        Assert.Equal([newest.Id, env.DossierId], all.Items.Select(x => x.Id));
        Assert.Equal((2, 1, 20), (all.Total, all.Page, all.PageSize));
        Assert.DoesNotContain(all.Items, x => x.Id == deleted.Id || x.Id == elsewhere.Id || x.Id == otherPatient.Id);
        Assert.Equal([newest.Id], (await env.Svc().ListAsync(env.Actor("dentist"), env.Patient, env.N, 1, 20, CancellationToken.None)).Items.Select(x => x.Id).Take(1));
        Assert.Empty((await env.Svc().ListAsync(env.Actor("dentist"), env.Patient, null, 2, 20, CancellationToken.None)).Items);
        // The assigned scope narrows the list; an Owner scope uses its own branch list instead of memberships.
        Assert.Equal(0, (await env.Svc(new DossierAccess(false, true, [env.Other])).ListAsync(env.Actor("dentist"), env.Patient, null, 1, 20, CancellationToken.None)).Total);
        Assert.Equal(3, (await env.Svc(new DossierAccess(true, true, [env.N, env.Other])).ListAsync(env.Actor("dentist"), env.Patient, null, 1, 20, CancellationToken.None)).Total);
    }

    // ================= GetAsync =================
    [Fact]
    public async Task Get_UT_TDS_27_returns_detail_or_not_found()
    {
        await using var env = await NewEnv();
        var item = await env.AddItem(discountType: "Percent", discount: 10);
        var dto = await env.Svc().GetAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None);
        Assert.Equal((Version, 1), (dto.Version, dto.PlanVersion));
        var shown = Assert.Single(dto.Items);
        Assert.Equal((item.Id, 20000L), (shown.Id, shown.Discount!.AmountVnd)); // 10% of 2 x 100,000
        Assert.False(dto.FinancialSummary!.Available);
        Assert.Equal(180000L, dto.FinancialSummary.PlannedVnd);

        await AssertError(() => env.Svc().GetAsync(env.Actor("dentist"), Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
        await AssertError(() => env.Svc().GetAsync(env.Actor("elsewhere"), env.DossierId, CancellationToken.None), 403, "MSG06");
        await env.SetState("Deleted");
        await AssertError(() => env.Svc().GetAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None), 404, "MSG113");
    }

    // ================= UpdateNoteAsync =================
    [Fact]
    public async Task UpdateNote_UT_TDS_28_changes_the_note_once_and_records_an_event()
    {
        await using var env = await NewEnv();
        var change = await env.Note("  Dặn tái khám  ");
        Assert.Equal((Version + 1, "Dặn tái khám"), (change.Dossier.Version, change.Dossier.Note));
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "DossierNoteUpdated");

        var same = await env.Note("Dặn tái khám", Version + 1);
        Assert.Equal(Version + 1, same.Dossier.Version); // unchanged note: no version bump
        var cleared = await env.Note("   ", Version + 1);
        Assert.Equal(((string?)null, Version + 2), (cleared.Dossier.Note, cleared.Dossier.Version));
    }

    [Fact]
    public async Task UpdateNote_UT_TDS_29_validation_conflict_and_replay()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Note(new string('a', 4001)), 400, "MSG04");
        await AssertError(() => env.Note("x", Version - 1), 409, "MSG111");
        await AssertError(() => env.Note("x", key: Guid.Empty), 400, "MSG04");
        var key = Guid.NewGuid();
        var first = await env.Note("a", key: key);
        var again = await env.Note("a", key: key);
        Assert.Equal(first.Dossier.Version, again.Dossier.Version);
        await AssertError(() => env.Note("b", key: key), 409, "MSG111");
        Assert.Equal(new string('a', 4000), (await env.Note(new string('a', 4000), Version + 1)).Dossier.Note); // boundary
    }

    // ================= Sheet drafts =================
    // Input: a mutation of the save-draft call -> Expected: BusinessException.
    public static TheoryData<string, string, int, string> DraftRejections => new()
    {
        { "UT-TDS-30 rowId rỗng", "emptyRow", 400, "MSG04" },
        { "UT-TDS-31 RequestKey rỗng", "emptyKey", 400, "MSG04" },
        { "UT-TDS-32 Loại bản nháp lạ", "badKind", 400, "MSG04" },
        { "UT-TDS-33 Dòng không phải đối tượng JSON", "notObject", 400, "MSG04" },
        { "UT-TDS-34 Dòng sai dữ liệu (số lượng 0)", "badRow", 400, "MSG04" },
        { "UT-TDS-35 Kế hoạch mà có VisitId", "planWithVisit", 400, "MSG04" },
        { "UT-TDS-36 Hồ sơ đã đóng", "closed", 409, "MSG240" },
        { "UT-TDS-37 rowId trùng một hoạt động đã có", "rowIsActivity", 409, "MSG111" },
        { "UT-TDS-38 rowId trùng một hạng mục kế hoạch", "rowIsItem", 409, "MSG111" },
        { "UT-TDS-39 Dịch vụ đã ngừng", "serviceOff", 400, "MSG04" },
        { "UT-TDS-40 Bác sĩ thực hiện không thuộc chi nhánh", "dentistOut", 400, "MSG04" },
        { "UT-TDS-41 Lượt khám không tồn tại", "noVisit", 404, "MSG113" },
        { "UT-TDS-42 Tạo mới nhưng ExpectedVersion khác 0", "version", 409, "MSG111" },
        { "UT-TDS-43 Hồ sơ không tồn tại", "noDossier", 404, "MSG113" },
        { "UT-TDS-44 Người gọi là trợ lý", "assistant", 403, "MSG06" }
    };

    [Theory, MemberData(nameof(DraftRejections))]
    public async Task SaveDraft_rejects_invalid_calls(string caseName, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        var rowId = Guid.NewGuid();
        if (problem == "closed") await env.SetState("Closed");
        if (problem == "rowIsActivity")
        {
            env.Db.ClinicalActivities.Add(new ClinicalActivity { Id = rowId, DossierId = env.DossierId, ServiceId = env.S1, Unit = "lần", Quantity = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "", CreatedAt = Now });
            await env.Db.SaveChangesAsync();
        }
        if (problem == "rowIsItem")
        {
            env.Db.TreatmentItems.Add(new TreatmentItem { Id = rowId, DossierId = env.DossierId, PlannedOrder = 1, ServiceId = env.S1, ServiceName = "x", Unit = "lần", Quantity = 1 });
            await env.Db.SaveChangesAsync();
        }
        var row = problem switch
        {
            "notObject" => JsonSerializer.SerializeToElement(new[] { 1, 2 }),
            "badRow" => env.Row(r => r["quantity"] = 0),
            "serviceOff" => env.Row(r => r["serviceId"] = env.SOff),
            "dentistOut" => env.Row(r => r["dentistId"] = env.Dentist2Emp + 100000),
            _ => env.Row()
        };
        var kind = problem is "planWithVisit" or "rowIsActivity" or "badKind" ? (problem == "badKind" ? "Other" : "Plan") : problem == "noVisit" ? "Actual" : "Plan";
        Guid? visit = problem is "planWithVisit" or "noVisit" ? Guid.NewGuid() : null;
        var call = () => env.Svc().SaveSheetDraftAsync(env.Actor(problem == "assistant" ? "assistant" : "dentist"), problem == "noDossier" ? Guid.NewGuid() : env.DossierId,
            problem == "emptyRow" ? Guid.Empty : rowId, new SaveSheetDraftRequest(kind, visit, row, problem == "version" ? 3 : 0, problem == "emptyKey" ? Guid.Empty : Guid.NewGuid()), CancellationToken.None);
        await AssertError(call, status, code);
        Assert.Empty(env.Db.TreatmentSheetDrafts.Where(x => x.DossierId == env.DossierId));
    }

    [Fact]
    public async Task Drafts_UT_TDS_45_create_update_replay_and_list()
    {
        await using var env = await NewEnv();
        var rowId = Guid.NewGuid(); var key = Guid.NewGuid();
        var created = await env.SaveDraft(rowId, key: key);
        Assert.Equal((rowId, "Plan", 1), (created.Id, created.Kind, created.Version));
        var replay = await env.SaveDraft(rowId, key: key);
        Assert.Equal(created.Version, replay.Version);
        await AssertError(() => env.SaveDraft(rowId, key: key, row: env.Row(r => r["note"] = "khác")), 409, "MSG111"); // same key, different payload

        await AssertError(() => env.SaveDraft(rowId, version: 7), 409, "MSG111");               // stale version
        await AssertError(() => env.SaveDraft(rowId, kind: "Actual", version: 1), 409, "MSG111"); // kind cannot change
        await AssertError(() => env.SaveDraft(rowId, version: 1, actor: "dentist2"), 409, "MSG111"); // another author
        var updated = await env.SaveDraft(rowId, version: 1);
        Assert.Equal(2, updated.Version);

        await env.SaveDraft(); // a second draft
        await env.SaveDraft(actor: "dentist2");
        Assert.Equal(2, (await env.Svc().SheetDraftsAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None)).Count); // only my drafts
        await AssertError(() => env.Svc().SheetDraftsAsync(env.Actor("dentist"), Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
    }

    [Fact]
    public async Task Drafts_UT_TDS_46_delete_checks_existence_owner_and_version()
    {
        await using var env = await NewEnv();
        var rowId = Guid.NewGuid();
        await env.SaveDraft(rowId);
        await AssertError(() => env.Svc().DeleteSheetDraftAsync(env.Actor("dentist"), env.DossierId, Guid.NewGuid(), 1, CancellationToken.None), 404, "MSG113");
        await AssertError(() => env.Svc().DeleteSheetDraftAsync(env.Actor("dentist2"), env.DossierId, rowId, 1, CancellationToken.None), 403, "MSG06");
        await AssertError(() => env.Svc().DeleteSheetDraftAsync(env.Actor("dentist"), env.DossierId, rowId, 9, CancellationToken.None), 409, "MSG111");
        await env.Svc().DeleteSheetDraftAsync(env.Actor("dentist"), env.DossierId, rowId, 1, CancellationToken.None);
        Assert.Empty(env.Db.TreatmentSheetDrafts.Where(x => x.DossierId == env.DossierId));
    }

    // ================= DeleteAsync =================
    [Fact]
    public async Task Delete_UT_TDS_47_deletes_an_untouched_open_dossier_and_replays()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var change = await env.Delete(key: key);
        Assert.Equal(("Deleted", Version + 1, "MSG222"), (change.Dossier.State, change.Dossier.Version, change.Notification.Code));
        var again = await env.Delete(key: key);
        Assert.Equal(change.Dossier.Version, again.Dossier.Version);
        Assert.Equal(Version + 1, (await env.Dossier()).Version);
        await AssertError(() => env.Delete(Version + 1), 404, "MSG113"); // already deleted
    }

    [Fact]
    public async Task Delete_UT_TDS_48_guards()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Delete(key: Guid.Empty), 400, "MSG04");
        await AssertError(() => env.Delete(dossier: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Delete(Version - 1), 409, "MSG111");
    }

    // Input: what the dossier already has -> Expected: MSG233 (409), nothing is deleted.
    [Theory]
    [InlineData("UT-TDS-49 Đã có thanh toán", "ledger")]
    [InlineData("UT-TDS-50 Đã đóng", "closed")]
    [InlineData("UT-TDS-51 Có hạng mục điều trị", "item")]
    [InlineData("UT-TDS-52 Có sự kiện ngoài lúc tạo", "event")]
    [InlineData("UT-TDS-53 Có lịch hẹn", "appointment")]
    [InlineData("UT-TDS-54 Có hoạt động đã thực hiện", "activity")]
    [InlineData("UT-TDS-55 Có bản nháp", "draft")]
    public async Task Delete_refuses_dossiers_with_history(string caseName, string reason)
    {
        await using var env = await NewEnv();
        switch (reason)
        {
            case "ledger": await env.Ledger(env.DossierId); break;
            case "closed": await env.SetState("Closed"); break;
            case "item": await env.AddItem(); break;
            case "event": env.Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = env.DossierId, Kind = "DossierNoteUpdated", ActorId = env.Actor("dentist"), OccurredAt = Now, DossierVersion = 2 }); break;
            case "appointment":
                env.Db.Appointments.Add(new Appointment { Code = "AP" + env.N, BranchId = env.N, DentistId = env.DentistEmp, ChairId = 1, AttendeeName = "An", Status = "Confirmed", DossierId = env.DossierId, StartAt = Now, EndAt = Now.AddHours(1), CreatedAt = Now }); break;
            case "activity":
                env.Db.ClinicalActivities.Add(new ClinicalActivity { DossierId = env.DossierId, ServiceId = env.S1, Unit = "lần", Quantity = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "", CreatedAt = Now }); break;
            case "draft": await env.SaveDraft(); break;
        }
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.Delete(), 409, "MSG233");
        Assert.NotEqual("Deleted", (await env.Dossier()).State);
    }

    // ================= HistoryAsync / PricePreviewAsync =================
    [Fact]
    public async Task History_UT_TDS_56_lists_plan_snapshots_newest_first()
    {
        await using var env = await NewEnv();
        Assert.Empty(await env.Svc().HistoryAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None));
        TreatmentEvent E(string kind, int plan, int minutes) => new() { DossierId = env.DossierId, Kind = kind, ActorId = env.Actor("dentist"), OccurredAt = Now.AddMinutes(minutes), DossierVersion = plan + 1, PayloadJson = $"{{\"planVersion\":{plan}}}" };
        env.Db.TreatmentEvents.AddRange(E("PlanCommitted", 1, 0), E("PlanRevised", 2, 10), E("DossierNoteUpdated", 9, 20));
        await env.Db.SaveChangesAsync();
        var history = await env.Svc().HistoryAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None);
        Assert.Equal([2, 1], history.Select(x => x.Version));
        await AssertError(() => env.Svc().HistoryAsync(env.Actor("dentist"), Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
    }

    [Fact]
    public async Task PricePreview_UT_TDS_57_sums_gross_discount_and_net()
    {
        await using var env = await NewEnv();
        var empty = await env.Svc().PricePreviewAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None);
        Assert.Equal((0, 0L, 0L, 0L), (empty.PlanVersion, empty.GrossVnd, empty.ApprovedDiscountVnd, empty.NetBeforeTaxVnd));
        await env.AddItem(price: 100000, discountType: "Percent", discount: 10);          // gross 200,000, discount 20,000
        await env.AddItem(price: 50000, discountType: "Fixed", discount: 30000);           // gross 100,000, discount 30,000
        await env.AddItem(price: 10000);                                                   // gross 20,000, no discount
        var preview = await env.Svc().PricePreviewAsync(env.Actor("dentist"), env.DossierId, CancellationToken.None);
        Assert.Equal((1, Version, 320000L, 50000L, 270000L), (preview.PlanVersion, preview.DossierVersion, preview.GrossVnd, preview.ApprovedDiscountVnd, preview.NetBeforeTaxVnd));
        Assert.Equal(3, preview.Items.Count);
    }

    // ================= CloseAsync =================
    [Fact]
    public async Task Close_UT_TDS_58_completes_a_dossier_with_a_plan()
    {
        await using var env = await NewEnv();
        await env.AddItem();
        var change = await env.Close();
        Assert.Equal(("Completed", Version + 1, "MSG222"), (change.Dossier.State, change.Dossier.Version, change.Notification.Code));
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "DossierClosed");
    }

    [Fact]
    public async Task Close_UT_TDS_59_stopped_incomplete_needs_a_reason_and_ends_as_stopped()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Close("StoppedIncomplete"), 400, "MSG04");
        await AssertError(() => env.Close("StoppedIncomplete", "   "), 400, "MSG04");
        Assert.Equal("Stopped", (await env.Close("StoppedIncomplete", " Bệnh nhân bỏ dở ")).Dossier.State);
    }

    [Fact]
    public async Task Close_UT_TDS_60_other_rejections()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Close(version: Version - 1), 409, "MSG111");
        await AssertError(() => env.Close("Unknown", "x"), 400, "MSG04");
        await AssertError(() => env.Close("StoppedIncomplete", new string('a', 1001)), 400, "MSG04");
        await AssertError(() => env.Close("CompletedAsAgreed"), 409, "MSG243");               // no committed plan
        await AssertError(() => env.Close("CompletedRevisedScope", "đổi phạm vi"), 409, "MSG233"); // no PlanRevised event
        await env.AddItem();
        env.Db.Appointments.Add(new Appointment { Code = "AP" + env.N, BranchId = env.N, DentistId = env.DentistEmp, ChairId = 1, AttendeeName = "An", Status = "CheckedIn", DossierId = env.DossierId, StartAt = Now, EndAt = Now.AddHours(1), CreatedAt = Now });
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.Close(), 409, "MSG233"); // an upcoming appointment of the dossier
        await AssertError(() => env.Close(actor: "elsewhere"), 403, "MSG06");
    }

    [Fact]
    public async Task Close_UT_TDS_61_revised_scope_after_a_plan_revision_and_state_must_be_open()
    {
        await using var env = await NewEnv();
        await env.AddItem();
        env.Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = env.DossierId, Kind = "PlanRevised", ActorId = env.Actor("dentist"), OccurredAt = Now, DossierVersion = 2, PayloadJson = "{\"planVersion\":2}" });
        await env.Db.SaveChangesAsync();
        var key = Guid.NewGuid();
        var done = await env.Close("CompletedRevisedScope", "Rút gọn", key: key);
        Assert.Equal("Completed", done.Dossier.State);
        Assert.Equal(done.Dossier.Version, (await env.Close("CompletedRevisedScope", "Rút gọn", key: key)).Dossier.Version); // replay
        await AssertError(() => env.Close("CompletedRevisedScope", "Rút gọn", Version + 1), 400, "MSG04"); // no longer open
    }

    // ================= ReopenAsync =================
    [Theory]
    [InlineData("UT-TDS-62 Đã hoàn thành, có lý do", "Completed", "Làm lại", true)]
    [InlineData("UT-TDS-63 Đã dừng", "Stopped", "Tiếp tục", true)]
    [InlineData("UT-TDS-64 Đang mở", "Open", "Làm lại", false)]
    [InlineData("UT-TDS-65 Đã hủy", "Cancelled", "Làm lại", false)]
    [InlineData("UT-TDS-66 Thiếu lý do", "Completed", null, false)]
    [InlineData("UT-TDS-67 Lý do chỉ có khoảng trắng", "Completed", "   ", false)]
    public async Task Reopen_depends_on_state_and_reason(string caseName, string state, string? reason, bool allowed)
    {
        await using var env = await NewEnv();
        await env.SetState(state);
        if (allowed)
        {
            var change = await env.Reopen(reason);
            Assert.Equal(("Open", Version + 1), (change.Dossier.State, change.Dossier.Version));
            Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "DossierReopened");
            return;
        }
        await AssertError(() => env.Reopen(reason), 400, "MSG122");
    }

    [Fact]
    public async Task Reopen_UT_TDS_68_boundary_reason_stale_version_and_replay()
    {
        await using var env = await NewEnv();
        await env.SetState("Completed");
        await AssertError(() => env.Reopen(new string('a', 1001)), 400, "MSG122");
        await AssertError(() => env.Reopen("ok", Version - 1), 409, "MSG111");
        var key = Guid.NewGuid();
        var first = await env.Reopen(new string('a', 1000), key: key);
        Assert.Equal(first.Dossier.Version, (await env.Reopen(new string('a', 1000), key: key)).Dossier.Version);
        await AssertError(() => env.Reopen("khác", key: key), 409, "MSG111");
    }

    // ================= OwnSummaryAsync =================
    private static async Task<ApplicationUser> Customer(Env env, string status = PatientLinkStatus.Verified)
    {
        var user = new ApplicationUser { UserName = "own" + CreateTestKit.NextId(), FullName = "Chủ hồ sơ", AccountType = "Public", IsActive = true };
        env.Db.Users.Add(user);
        await env.Db.SaveChangesAsync();
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = user.Id, PatientId = env.Patient, Status = status, RequestCode = "LK" + CreateTestKit.NextId() });
        await env.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task OwnSummary_UT_TDS_69_only_verified_links_see_dossiers_with_a_plan()
    {
        await using var env = await NewEnv();
        var noPlan = await env.NewDossier();   // dossier without items is hidden
        var deleted = await env.NewDossier(state: "Deleted"); await env.AddItem(deleted.Id);
        var item = await env.AddItem();
        env.Db.ClinicalActivities.AddRange(
            new ClinicalActivity { DossierId = env.DossierId, ItemId = item.Id, ServiceId = env.S1, Unit = "lần", Quantity = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "Hoàn tất", CreatedAt = Now },
            new ClinicalActivity { DossierId = env.DossierId, ServiceId = env.S1, ServiceNameSnapshot = "Tên lưu", Unit = "lần", Quantity = 1, PerformedAt = Now.AddHours(-1), PerformerId = env.DentistEmp, Outcome = " ", CreatedAt = Now },
            new ClinicalActivity { DossierId = env.DossierId, ServiceId = env.S1, Unit = "lần", Quantity = 1, PerformedAt = Now.AddHours(-2), PerformerId = env.DentistEmp, Outcome = "", CreatedAt = Now });
        await env.Db.SaveChangesAsync();

        var verified = await Customer(env);
        var summary = Assert.Single(await env.Svc().OwnSummaryAsync(verified.Id, CancellationToken.None));
        Assert.Equal((env.DossierId, 1), (summary.DossierId, summary.PlanVersion));
        Assert.Equal(["Trám răng", "Tên lưu", "Dịch vụ điều trị"], summary.PerformedItems.Select(x => x.ServiceName)); // newest first, name fallbacks
        Assert.Equal(["Hoàn tất", null, null], summary.PerformedItems.Select(x => x.Outcome));
        Assert.Single(summary.AgreedItems);
        Assert.Empty(await env.Svc().OwnSummaryAsync((await Customer(env, PatientLinkStatus.Pending)).Id, CancellationToken.None));
        Assert.Empty(await env.Svc().OwnSummaryAsync(env.Actor("customer"), CancellationToken.None));
        Assert.DoesNotContain(await env.Svc().OwnSummaryAsync(verified.Id, CancellationToken.None), x => x.DossierId == noPlan.Id || x.DossierId == deleted.Id);
    }

    [Fact]
    public async Task OwnSummary_UT_TDS_70_includes_archived_legacy_payments_of_the_patient()
    {
        await using var env = await NewEnv();
        await env.AddItem();
        var customer = await Customer(env);
        string Archive(object row) => JsonSerializer.Serialize(new { rows = new[] { row } }, Web);
        async Task Insert(string target, object row) => await env.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AUDIT_LOG" ("StorageId","AuditType","Target","PayloadJson") VALUES ({Guid.NewGuid()}, 'FinanceLegacyArchive', {target}, CAST({Archive(row)} AS jsonb))
            """);
        var d = env.DossierId.ToString();
        await Insert("TREATMENT_DOSSIER_PAYMENT", new Dictionary<string, object> { ["Id"] = Guid.NewGuid().ToString(), ["DossierId"] = d, ["PatientId"] = env.Patient, ["PaidOn"] = "2030-01-05", ["Method"] = "Cash", ["TotalVnd"] = 1000 });
        await Insert("PAYMENT_RECEIPT", new Dictionary<string, object> { ["Id"] = Guid.NewGuid().ToString(), ["DossierId"] = d, ["PatientId"] = env.Patient, ["State"] = "Posted", ["ConfirmedAt"] = "2030-01-06T03:00:00Z", ["CreatedAt"] = "2030-01-01T00:00:00Z", ["Method"] = "Transfer", ["AmountVnd"] = 2000 });
        await Insert("PAYMENT_RECEIPT", new Dictionary<string, object?> { ["Id"] = Guid.NewGuid().ToString(), ["DossierId"] = d, ["PatientId"] = env.Patient, ["State"] = "Posted", ["ConfirmedAt"] = null, ["CreatedAt"] = "2030-01-02T00:00:00Z", ["Method"] = "Cash", ["AmountVnd"] = 500 });
        await Insert("PAYMENT_RECEIPT", new Dictionary<string, object> { ["Id"] = Guid.NewGuid().ToString(), ["DossierId"] = d, ["PatientId"] = env.Patient, ["State"] = "Draft", ["CreatedAt"] = "2030-01-02T00:00:00Z", ["Method"] = "Cash", ["AmountVnd"] = 9999 });
        await Insert("PAYMENT_RECEIPT", new Dictionary<string, object?> { ["Id"] = Guid.NewGuid().ToString(), ["DossierId"] = null, ["PatientId"] = env.Patient, ["State"] = "Posted", ["CreatedAt"] = "2030-01-02T00:00:00Z", ["Method"] = "Cash", ["AmountVnd"] = 7777 });

        var payments = Assert.Single(await env.Svc().OwnSummaryAsync(customer.Id, CancellationToken.None)).Payments;
        Assert.Equal([2000L, 1000L, 500L], payments.Select(x => x.TotalVnd)); // newest first; unposted and dossier-less rows are skipped
        Assert.Equal(["PaymentReceipt", "DossierPayment", "PaymentReceipt"], payments.Select(x => x.Source));
        Assert.Equal(new DateOnly(2030, 1, 6), payments[0].PaidOn);
    }
}
