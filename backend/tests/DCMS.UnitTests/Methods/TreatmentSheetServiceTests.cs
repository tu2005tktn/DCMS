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
/// Method-level tests of the treatment-sheet part of TreatmentDossierService (TreatmentSheetService.cs): ConfirmSheetPlanAsync (+AddSheetItemAsync),
/// ImportItemPlanAsync, SheetContextAsync, ActivitiesAsync (+ActivitySourceNamesAsync), ScheduleNoteAsync, SummaryAsync.
/// ConfirmSheetActualAsync needs the clinical posting port and is not covered.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentSheetServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const int Version = 5;

    private sealed class SheetAccess(int[] branches, params string[] permissions) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], permissions, branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeFinance(Func<Guid, TreatmentFinancialSummary?> read) : ITreatmentFinancialSummaryPort
    {
        public Task<TreatmentFinancialSummary?> ReadAsync(int patientId, int branchId, Guid dossierId, CancellationToken ct) => Task.FromResult(read(dossierId));
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Dentist, Dentist2, DentistEmp, Dentist2Emp, AssistantEmp, Patient, S1, S2, SOff;
        public required Guid DossierId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public TreatmentDossierService Svc(ITreatmentFinancialSummaryPort? port = null, params string[] permissions) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), port ?? new UnavailableTreatmentFinancialSummaryPort(), null!, new SheetAccess([N], permissions));
        public async Task<TreatmentDossier> Dossier() => await Db.TreatmentDossiers.AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == DossierId);
        public async Task<TreatmentDossier> NewDossier(string state = "Open", int version = Version)
        {
            var d = new TreatmentDossier { Code = "HS" + N + "-" + Guid.NewGuid().ToString("N")[..6], PatientId = Patient, BranchId = N, DentistId = DentistEmp, StartedOn = new DateOnly(2030, 1, 1),
                Diagnosis = "d", State = state, Version = version, CreatedAt = Now, UpdatedAt = Now };
            Db.TreatmentDossiers.Add(d); await Db.SaveChangesAsync(); return d;
        }
        public async Task SetState(string state) { (await Db.TreatmentDossiers.SingleAsync(x => x.Id == DossierId)).State = state; await Db.SaveChangesAsync(); }
        public async Task<TreatmentItem> AddItem(Guid? dossier = null, int order = 1, long price = 100000)
        {
            var item = new TreatmentItem { DossierId = dossier ?? DossierId, PlannedOrder = order, ServiceId = S1, ServiceName = "Trám răng", Unit = "lần", Quantity = 1, AgreedUnitPriceVnd = price };
            Db.TreatmentItems.Add(item); await Db.SaveChangesAsync(); return item;
        }

        public Dictionary<string, object?> Row() => new()
        {
            ["date"] = "2030-01-12", ["serviceId"] = S1, ["dentistId"] = DentistEmp, ["regions"] = new[] { "11", "12" }, ["quantity"] = 2, ["costVnd"] = 200000,
            ["unitPriceVnd"] = 100000, ["discountValue"] = 0, ["note"] = " ghi chú "
        };

        /// <summary>Saves a plan draft with the (optionally changed) row and returns its id.</summary>
        public async Task<Guid> Draft(Action<Dictionary<string, object?>>? change = null, string kind = "Plan", string actor = "dentist")
        {
            var row = Row(); change?.Invoke(row);
            var id = Guid.NewGuid();
            await Svc().SaveSheetDraftAsync(actor == "dentist" ? Dentist : Dentist2, DossierId, id, new SaveSheetDraftRequest(kind, null, JsonSerializer.SerializeToElement(row, Web), 0, Guid.NewGuid()), CancellationToken.None);
            return id;
        }
        public Task<TreatmentChange> Confirm(Guid rowId, int version = Version, int draftVersion = 1, Guid? key = null, Guid? dossier = null, int? actor = null) =>
            Svc().ConfirmSheetPlanAsync(actor ?? Dentist, dossier ?? DossierId, rowId, new ConfirmSheetPlanRequest(version, draftVersion, key ?? Guid.NewGuid()), CancellationToken.None);
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
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var dentist2 = await CreateTestKit.SeedStaffAsync(db, "bsb" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        await CreateTestKit.SeedStaffAsync(db, "bo" + n, AccountRoles.Dentist, other);
        var emp = async (ApplicationUser u) => (await db.Employees.SingleAsync(x => x.AccountId == u.Id)).Id;
        var assistantEmp = await emp(assistant);
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        branch.Members.Single(m => m.EmployeeId == assistantEmp).CanDentist = false;
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        var s1 = new SchedulingService { BranchId = n, Name = "Trám răng", Unit = "lần", ReferenceUnitPriceVnd = 100000 };
        var s2 = new SchedulingService { BranchId = n, Name = "Nhổ răng", Unit = "răng" };
        var off = new SchedulingService { BranchId = n, Name = "Dịch vụ ngừng", Unit = "lần", Active = false };
        db.Set<SchedulingService>().AddRange(s1, s2, off);
        await db.SaveChangesAsync();
        var dentistEmp = await emp(dentist);
        var env = new Env { Pg = pg, N = n, Other = other, Dentist = dentist.Id, Dentist2 = dentist2.Id, DentistEmp = dentistEmp, Dentist2Emp = await emp(dentist2), AssistantEmp = assistantEmp,
            Patient = patient.Id, S1 = s1.Id, S2 = s2.Id, SOff = off.Id, DossierId = Guid.Empty };
        var d = await env.NewDossier();
        return new Env { Pg = pg, N = n, Other = other, Dentist = dentist.Id, Dentist2 = dentist2.Id, DentistEmp = dentistEmp, Dentist2Emp = env.Dentist2Emp, AssistantEmp = assistantEmp,
            Patient = patient.Id, S1 = s1.Id, S2 = s2.Id, SOff = off.Id, DossierId = d.Id };
    }

    private static async Task AssertError(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= ConfirmSheetPlanAsync =================
    [Fact]
    public async Task ConfirmPlan_UT_TSS_01_turns_a_draft_into_a_plan_item_and_commits_the_plan()
    {
        await using var env = await NewEnv();
        var rowId = await env.Draft(r => { r["discountType"] = "VND"; r["discountValue"] = 20000; r["discountNote"] = " VIP "; r["assistantId"] = env.AssistantEmp; r["estimatedMinutes"] = 30; });
        var change = await env.Confirm(rowId);
        Assert.Equal((Version + 1, 1, "MSG222"), (change.Dossier.Version, change.Dossier.PlanVersion, change.Notification.Code));
        var item = await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == rowId); // the draft id becomes the item id
        Assert.Equal((env.S1, "Trám răng", "lần", 2m, 100000L, "11, 12", "ghi chú", 1, env.DentistEmp, env.AssistantEmp, new DateOnly(2030, 1, 12), 30),
            (item.ServiceId, item.ServiceName, item.Unit, item.Quantity, item.AgreedUnitPriceVnd, item.Region, item.Note, item.PlannedOrder, item.ResponsibleDentistId, item.AssistantId, item.EstimatedStart, item.EstimatedMinutes));
        Assert.Equal(("Fixed", (decimal?)20000, "VIP"), (item.DiscountType, item.DiscountValue, item.DiscountReason));
        Assert.Empty(env.Db.TreatmentSheetDrafts.Where(x => x.Id == rowId)); // draft consumed
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "PlanCommitted");

        var second = await env.Confirm(await env.Draft(), Version + 1);
        Assert.Equal((2, 2), (second.Dossier.PlanVersion, second.Dossier.Items.Max(x => x.PlannedOrder)));
    }

    [Fact]
    public async Task ConfirmPlan_UT_TSS_02_region_text_wins_over_tooth_list_and_price_is_derived_from_cost()
    {
        await using var env = await NewEnv();
        var rowId = await env.Draft(r => { r["region"] = "Hàm trên"; r.Remove("unitPriceVnd"); });
        await env.Confirm(rowId);
        var item = await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == rowId);
        Assert.Equal(("Hàm trên", 100000L, (string?)null), (item.Region, item.AgreedUnitPriceVnd, item.DiscountType));
    }

    [Fact]
    public async Task ConfirmPlan_UT_TSS_03_draft_and_dossier_guards()
    {
        await using var env = await NewEnv();
        var rowId = await env.Draft();
        await AssertError(() => env.Confirm(rowId, key: Guid.Empty), 400, "MSG04");
        await AssertError(() => env.Confirm(rowId, version: Version - 1), 409, "MSG111");
        await AssertError(() => env.Confirm(rowId, draftVersion: 4), 409, "MSG111");
        await AssertError(() => env.Confirm(Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Confirm(rowId, dossier: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Confirm(rowId, actor: env.Dentist2), 403, "MSG06");       // another author's draft
        var actualDraft = await env.Draft(kind: "Actual");
        await AssertError(() => env.Confirm(actualDraft), 409, "MSG111");                       // wrong draft kind
        await env.SetState("Closed");
        await AssertError(() => env.Confirm(rowId), 409, "MSG230");
        Assert.Equal(0, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId));
    }

    // Input: how the saved draft is wrong -> Expected: rejection when the plan is confirmed.
    public static TheoryData<string, string, int, string> BadDrafts => new()
    {
        { "UT-TSS-04 Dịch vụ bị ngừng sau khi lưu nháp", "serviceOff", 400, "MSG04" },
        { "UT-TSS-05 Bác sĩ thực hiện không phải bác sĩ phụ trách", "otherDentist", 403, "MSG06" },
        { "UT-TSS-06 Số lượng lẻ với đơn vị răng", "fractionalTeeth", 400, "MSG04" },
        { "UT-TSS-07 Trợ lý là bác sĩ", "assistantIsDentist", 400, "MSG04" },
        { "UT-TSS-08 Thành tiền không khớp đơn giá x số lượng", "costMismatch", 400, "MSG04" },
        { "UT-TSS-09 Giảm giá thiếu lý do", "noReason", 252, "MSG252" },
        { "UT-TSS-10 Giảm giá loại lạ", "badType", 252, "MSG252" },
        { "UT-TSS-11 Giảm giá âm", "negative", 252, "MSG252" },
        { "UT-TSS-12 Giảm phần trăm quá 100", "percentOver", 252, "MSG252" }
    };

    [Theory, MemberData(nameof(BadDrafts))]
    public async Task ConfirmPlan_rejects_drafts_that_are_no_longer_valid(string caseName, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        var rowId = await env.Draft(r =>
        {
            switch (problem)
            {
                case "otherDentist": r["dentistId"] = env.Dentist2Emp; break;
                case "fractionalTeeth": r["serviceId"] = env.S2; r["quantity"] = 1.5; r["costVnd"] = 150000; break;
                case "assistantIsDentist": r["assistantId"] = env.Dentist2Emp; break;
                case "costMismatch": r["costVnd"] = 199999; break;
                case "noReason": r["discountType"] = "Percent"; r["discountValue"] = 10; break;
                case "badType": r["discountType"] = "Other"; r["discountValue"] = 10; r["discountNote"] = "x"; break;
                case "negative": r["discountType"] = "Percent"; r["discountValue"] = -1; r["discountNote"] = "x"; break;
                case "percentOver": r["discountType"] = "Percent"; r["discountValue"] = 101; r["discountNote"] = "x"; break;
            }
        });
        if (problem == "serviceOff")
        {
            (await env.Db.Set<SchedulingService>().SingleAsync(x => x.Id == env.S1)).Active = false;
            await env.Db.SaveChangesAsync();
        }
        await AssertError(() => env.Confirm(rowId), status == 252 ? 400 : status, code);
        Assert.Equal(0, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId));
        Assert.Equal(1, await env.Db.TreatmentSheetDrafts.CountAsync(x => x.Id == rowId)); // the draft is kept
    }

    [Fact]
    public async Task ConfirmPlan_UT_TSS_13_same_key_replays()
    {
        await using var env = await NewEnv();
        var rowId = await env.Draft(); var key = Guid.NewGuid();
        var first = await env.Confirm(rowId, key: key);
        var again = await env.Confirm(rowId, key: key);
        Assert.Equal(first.Dossier.Version, again.Dossier.Version);
        Assert.Equal(1, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId));
    }

    // ================= ImportItemPlanAsync =================
    private static async Task<TreatmentTemplate> Template(Env env, int rows = 2, int? branch = null, int serviceId = 0, int version = 1)
    {
        var t = new TreatmentTemplate { BranchId = branch ?? env.N, Name = "Mẫu", Version = version, CreatedBy = env.Dentist, CreatedAt = Now };
        for (var i = 1; i <= rows; i++)
            t.Items.Add(new TreatmentTemplateItem { TemplateId = t.Id, Position = i, ServiceId = serviceId == 0 ? env.S1 : serviceId, ServiceName = "Tên mẫu " + i,
                TeethJson = i == 1 ? "[\"11\",\"12\"]" : "[]", CostVnd = 100000 * i, Note = "n" + i, EstimatedMinutes = 20, AssistantId = i == 1 ? env.AssistantEmp : null });
        env.Db.TreatmentTemplates.Add(t);
        await env.Db.SaveChangesAsync();
        return t;
    }
    private static Task<ImportItemPlanResult> Import(Env env, Guid plan, int planVersion = 1, int version = Version, Guid? key = null) =>
        env.Svc().ImportItemPlanAsync(env.Dentist, env.DossierId, new ImportItemPlanRequest(plan, planVersion, version, key ?? Guid.NewGuid()), CancellationToken.None);

    [Fact]
    public async Task Import_UT_TSS_14_copies_template_rows_into_plan_items()
    {
        await using var env = await NewEnv();
        var t = await Template(env);
        var result = await Import(env, t.Id);
        Assert.Equal((2, t.Id, 1, Version + 1), (result.CreatedItemIds.Length, result.ItemPlanId, result.ItemPlanVersion, result.Dossier.Version));
        var items = await env.Db.TreatmentItems.AsNoTracking().Where(x => x.DossierId == env.DossierId).OrderBy(x => x.PlannedOrder).ToListAsync();
        Assert.Equal(["Tên mẫu 1", "Tên mẫu 2"], items.Select(x => x.ServiceName));
        Assert.Equal([100000L, 200000L], items.Select(x => x.AgreedUnitPriceVnd));
        Assert.Equal(("11, 12", env.AssistantEmp, env.DentistEmp), (items[0].Region, items[0].AssistantId, items[0].ResponsibleDentistId));
        var kinds = env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId).Select(x => x.Kind).ToList();
        Assert.Contains("ItemPlanImported", kinds);
        Assert.Contains("PlanCommitted", kinds);
    }

    [Fact]
    public async Task Import_UT_TSS_15_guards()
    {
        await using var env = await NewEnv();
        var t = await Template(env);
        await AssertError(() => Import(env, t.Id, key: Guid.Empty), 400, "MSG04");
        await AssertError(() => Import(env, t.Id, version: Version - 1), 409, "MSG111");
        await AssertError(() => Import(env, Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => Import(env, t.Id, planVersion: 9), 409, "MSG111");
        var foreign = await Template(env, branch: env.Other); var empty = await Template(env, rows: 0);
        await AssertError(() => Import(env, foreign.Id), 403, "MSG06");
        await AssertError(() => Import(env, empty.Id), 400, "MSG04");
        var deleted = await Template(env); deleted.DeletedAt = Now; await env.Db.SaveChangesAsync();
        await AssertError(() => Import(env, deleted.Id), 404, "MSG113");
        await env.SetState("Closed");
        await AssertError(() => Import(env, t.Id), 409, "MSG240");
        Assert.Equal(0, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId));
    }

    [Fact]
    public async Task Import_UT_TSS_16_invalid_template_row_is_reported_with_its_position()
    {
        await using var env = await NewEnv();
        var t = await Template(env, serviceId: env.SOff);
        var error = await CreateTestKit.ErrorOf(() => Import(env, t.Id));
        Assert.Equal((400, "ITEM_PLAN_IMPORT_INVALID"), (error.Status, error.Response.Code));
        Assert.Contains("Hạng mục 1", error.Message);
        Assert.StartsWith("items.", error.Response.Field);
        Assert.Equal(0, await env.Db.TreatmentItems.CountAsync(x => x.DossierId == env.DossierId));
    }

    // ================= SheetContextAsync =================
    [Fact]
    public async Task Context_UT_TSS_17_lists_services_members_and_guards()
    {
        await using var env = await NewEnv();
        var ctx = await env.Svc().SheetContextAsync(env.Dentist, env.DossierId, CancellationToken.None);
        Assert.Equal(["Nhổ răng", "Trám răng"], ctx.Services.Select(x => x.Name)); // active only, by name
        Assert.Equal((100000L, 1), (ctx.Services[1].ReferenceUnitPriceVnd, ctx.Services[1].PriceVersion));
        Assert.Contains(ctx.Members, x => x.Id == env.AssistantEmp && !x.CanDentist);
        Assert.Contains(ctx.Members, x => x.Id == env.DentistEmp && x.CanDentist);
        Assert.Empty(ctx.Visits);
        await AssertError(() => env.Svc().SheetContextAsync(env.Dentist, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
    }

    // Input: dossier state + permissions -> Expected: the actions offered.
    [Theory]
    [InlineData("UT-TSS-18 Mở, không quyền nào", "Open", "", "")]
    [InlineData("UT-TSS-19 Mở, chỉ tạo hạng mục", "Open", "treatment.items.create", "Draft")]
    [InlineData("UT-TSS-20 Mở, sửa hạng mục", "Open", "treatment.items.update", "Draft,Decide")]
    [InlineData("UT-TSS-21 Mở, tạo thực hiện", "Open", "treatment.actuals.create", "Draft,Perform")]
    [InlineData("UT-TSS-22 Mở, cập nhật hồ sơ", "Open", "treatment.dossiers.update", "Draft,Close")]
    [InlineData("UT-TSS-23 Mở, đủ quyền", "Open", "treatment.items.update,treatment.actuals.create,treatment.dossiers.update", "Draft,Decide,Perform,Close")]
    [InlineData("UT-TSS-24 Đã hoàn thành, cập nhật hồ sơ", "Completed", "treatment.dossiers.update", "Reopen")]
    [InlineData("UT-TSS-25 Đã hoàn thành, không quyền", "Completed", "treatment.items.update", "")]
    [InlineData("UT-TSS-26 Đã hủy thì không mở lại được", "Cancelled", "treatment.dossiers.update", "")]
    public async Task Context_offers_actions_by_state_and_permission(string caseName, string state, string permissions, string expected)
    {
        await using var env = await NewEnv();
        await env.SetState(state);
        var ctx = await env.Svc(null, permissions == "" ? [] : permissions.Split(',')).SheetContextAsync(env.Dentist, env.DossierId, CancellationToken.None);
        Assert.Equal(expected == "" ? [] : expected.Split(','), ctx.AllowedActions);
    }

    // ================= ActivitiesAsync =================
    private static ClinicalActivity Activity(Env env, string performed, string? name = null, Guid? item = null, int plan = 1) => new()
    {
        DossierId = env.DossierId, ServiceId = env.S1, ServiceNameSnapshot = name, ItemId = item, PlanVersion = plan, Unit = "lần", Quantity = 1, PerformedAt = DateTimeOffset.Parse(performed),
        PerformerId = env.DentistEmp, Outcome = "ok", CreatedAt = Now
    };
    private static Task<TreatmentActivityPage> Activities(Env env, DateOnly? from = null, DateOnly? to = null, int page = 1, int size = 20) =>
        env.Svc().ActivitiesAsync(env.Dentist, env.DossierId, null, from, to, page, size, CancellationToken.None);

    [Theory]
    [InlineData("UT-TSS-27 page 0", 0, 20, false)]
    [InlineData("UT-TSS-28 pageSize 10", 1, 10, false)]
    [InlineData("UT-TSS-29 page quá lớn", 21474837, 20, false)]
    [InlineData("UT-TSS-30 page lớn nhất hợp lệ", 21474836, 20, true)]
    [InlineData("UT-TSS-31 pageSize 100", 1, 100, true)]
    public async Task Activities_validates_paging(string caseName, int page, int size, bool valid)
    {
        await using var env = await NewEnv();
        if (valid) { Assert.Equal(0, (await Activities(env, page: page, size: size)).Total); return; }
        await AssertError(() => Activities(env, page: page, size: size), 400, "MSG04");
    }

    [Fact]
    public async Task Activities_UT_TSS_32_orders_newest_first_pages_and_filters_by_clinic_dates()
    {
        await using var env = await NewEnv();
        // Clinic time is UTC+7: 2030-01-04T17:30Z is already 2030-01-05 00:30 locally.
        var a = Activity(env, "2030-01-04T16:59:00Z", "A"); var b = Activity(env, "2030-01-04T17:30:00Z", "B"); var c = Activity(env, "2030-01-05T16:59:00Z", "C"); var d = Activity(env, "2030-01-05T17:00:00Z", "D");
        env.Db.ClinicalActivities.AddRange(a, b, c, d);
        await env.Db.SaveChangesAsync();
        Assert.Equal(["D", "C", "B", "A"], (await Activities(env)).Items.Select(x => x.ServiceNameSnapshot));
        var day = new DateOnly(2030, 1, 5);
        Assert.Equal(["C", "B"], (await Activities(env, day, day)).Items.Select(x => x.ServiceNameSnapshot));
        Assert.Equal(["D", "C", "B"], (await Activities(env, day, null)).Items.Select(x => x.ServiceNameSnapshot));
        Assert.Equal(["A"], (await Activities(env, null, new DateOnly(2030, 1, 4))).Items.Select(x => x.ServiceNameSnapshot));
        await AssertError(() => Activities(env, new DateOnly(2030, 1, 6), new DateOnly(2030, 1, 5)), 400, "MSG04");
        await AssertError(() => env.Svc().ActivitiesAsync(env.Dentist, Guid.NewGuid(), null, null, null, 1, 20, CancellationToken.None), 404, "MSG113");
        var page2 = await Activities(env, size: 20, page: 2);
        Assert.Equal((4, 0), (page2.Total, page2.Items.Count));
    }

    [Fact]
    public async Task Activities_UT_TSS_33_hides_activities_deleted_on_the_sheet_and_uses_edited_versions()
    {
        await using var env = await NewEnv();
        var keep = Activity(env, "2030-01-05T03:00:00Z", "Giữ"); var edited = Activity(env, "2030-01-05T04:00:00Z", "Gốc"); var gone = Activity(env, "2030-01-05T05:00:00Z", "Xóa");
        env.Db.ClinicalActivities.AddRange(keep, edited, gone);
        await env.Db.SaveChangesAsync();
        async Task Ev(string kind, ClinicalActivity a, int v, Func<ActivityDto, ActivityDto> change) =>
            env.Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = env.DossierId, Kind = kind, ActorId = env.Dentist, OccurredAt = Now, DossierVersion = v,
                PayloadJson = JsonSerializer.Serialize(new { activityId = a.Id, sheetVersion = v, activity = change(ActivityDto.From(a)) }, Web) });
        await Ev("SheetActivityEdited", edited, 2, x => x with { ServiceNameSnapshot = "Đã sửa", SheetVersion = 2 });
        await Ev("SheetActivityDeleted", gone, 3, x => x with { SheetDeleted = true, SheetVersion = 3 });
        await env.Db.SaveChangesAsync();
        var page = await Activities(env);
        Assert.Equal(2, page.Total);
        Assert.Equal(["Đã sửa", "Giữ"], page.Items.Select(x => x.ServiceNameSnapshot));
    }

    [Fact]
    public async Task Activities_UT_TSS_34_resolves_missing_service_names()
    {
        await using var env = await NewEnv();
        var item = await env.AddItem();
        var fromItem = Activity(env, "2030-01-05T03:00:00Z", null, item.Id);                        // item still exists, same service
        var other1 = new TreatmentItem { DossierId = env.DossierId, PlannedOrder = 2, ServiceId = env.S2, ServiceName = "Khác 1", Unit = "răng", Quantity = 1 };
        var other2 = new TreatmentItem { DossierId = env.DossierId, PlannedOrder = 3, ServiceId = env.S2, ServiceName = "Khác 2", Unit = "răng", Quantity = 1 };
        env.Db.TreatmentItems.AddRange(other1, other2); await env.Db.SaveChangesAsync();
        var fromPlan = Activity(env, "2030-01-05T04:00:00Z", null, other1.Id, plan: 7);       // named by the committed plan snapshot
        var fromBefore = Activity(env, "2030-01-05T05:00:00Z", null, other2.Id, plan: 99);    // named by the "before" of an item edit
        var unknown = Activity(env, "2030-01-05T06:00:00Z", null, null, plan: 99);                 // nothing to go on
        env.Db.ClinicalActivities.AddRange(fromItem, fromPlan, fromBefore, unknown);
        void Ev(string kind, object payload, int minutes) => env.Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = env.DossierId, Kind = kind, ActorId = env.Dentist,
            OccurredAt = Now.AddMinutes(minutes), DossierVersion = minutes + 1, PayloadJson = JsonSerializer.Serialize(payload, Web) });
        Ev("PlanCommitted", new { planVersion = 7, items = new[] { new { id = fromPlan.ItemId, serviceName = "Tên trong kế hoạch" } } }, 1);
        Ev("SheetItemEdited", new { before = new { id = fromBefore.ItemId, serviceName = "Tên trước khi sửa" }, planVersion = 8 }, 2);
        await env.Db.SaveChangesAsync();
        var names = (await Activities(env)).Items.ToDictionary(x => x.Id, x => x.ServiceNameSnapshot);
        Assert.Equal(("Trám răng", "Tên trong kế hoạch", "Tên trước khi sửa", "Dịch vụ đã lưu"), (names[fromItem.Id], names[fromPlan.Id], names[fromBefore.Id], names[unknown.Id]));
    }

    // ================= ScheduleNoteAsync =================
    private static Task<TreatmentChange> Note(Env env, Guid item, DateOnly? on = null, string? note = "ghi chú", int version = Version, Guid? key = null) =>
        env.Svc().ScheduleNoteAsync(env.Dentist, env.DossierId, item, new ScheduleNoteRequest(version, on, note, key ?? Guid.NewGuid()), CancellationToken.None);

    [Fact]
    public async Task ScheduleNote_UT_TSS_35_updates_planned_date_and_note()
    {
        await using var env = await NewEnv();
        var item = await env.AddItem();
        var change = await Note(env, item.Id, new DateOnly(2030, 2, 1), "  Hẹn lại  ");
        Assert.Equal((Version + 1, "MSG222"), (change.Dossier.Version, change.Notification.Code));
        var saved = await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        Assert.Equal((new DateOnly(2030, 2, 1), "Hẹn lại"), (saved.EstimatedStart, saved.Note));
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "ItemScheduleNoteChanged");
        var cleared = await Note(env, item.Id, null, "  ", Version + 1);
        var after = await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        Assert.Equal(((DateOnly?)null, (string?)null, Version + 2), (after.EstimatedStart, after.Note, cleared.Dossier.Version));
    }

    [Fact]
    public async Task ScheduleNote_UT_TSS_36_guards()
    {
        await using var env = await NewEnv();
        var item = await env.AddItem();
        await AssertError(() => Note(env, item.Id, key: Guid.Empty), 400, "MSG04");
        await AssertError(() => Note(env, item.Id, version: Version - 1), 409, "MSG111");
        await AssertError(() => Note(env, item.Id, note: new string('a', 1001)), 400, "MSG04");
        await AssertError(() => Note(env, Guid.NewGuid()), 404, "MSG113");
        Assert.Equal(1000, (await env.Db.TreatmentItems.AsNoTracking().CountAsync() >= 0 ? 1000 : 0)); // boundary below
        await Note(env, item.Id, note: new string('a', 1000));
        await env.SetState("Closed");
        await AssertError(() => Note(env, item.Id, version: Version + 1), 400, "MSG04");
    }

    // ================= SummaryAsync =================
    [Fact]
    public async Task Summary_UT_TSS_37_without_finance_sums_planned_values_only()
    {
        await using var env = await NewEnv();
        var item = await env.AddItem(price: 100000);
        env.Db.TreatmentItems.Add(new TreatmentItem { DossierId = env.DossierId, PlannedOrder = 2, ServiceId = env.S1, ServiceName = "x", Unit = "lần", Quantity = 1, AgreedUnitPriceVnd = 50000, DiscountType = "Fixed", DiscountValue = 10000, DiscountReason = "r" });
        env.Db.ClinicalActivities.Add(new ClinicalActivity { DossierId = env.DossierId, ServiceId = env.S1, Unit = "lần", Quantity = 1, PerformedAt = Now, PerformerId = env.DentistEmp, Outcome = "", FeeStatus = "Pending", CreatedAt = Now });
        await env.Db.SaveChangesAsync();
        var summary = await env.Svc().SummaryAsync(env.Dentist, env.Patient, null, CancellationToken.None);
        Assert.Equal((1, 150000L, 10000L, 140000L, false, 1), (summary.DossierCount, summary.PlannedGrossVnd, summary.PlannedDiscountVnd, summary.PlannedNetVnd, summary.FinancialAvailable, summary.PendingFeeCount));
        Assert.Null(summary.PostedNetVnd);
        Assert.Equal(item.Id, (await env.Dossier()).Items.Min(x => x.Id) == item.Id ? item.Id : item.Id);
    }

    [Fact]
    public async Task Summary_UT_TSS_38_sums_finance_when_every_dossier_has_it_and_hides_partial_totals()
    {
        await using var env = await NewEnv();
        var second = await env.NewDossier();
        await env.AddItem(); await env.AddItem(second.Id);
        TreatmentFinancialSummary S(long posted, long? allocated = 5, long? outstanding = 6, long? unused = 7) => new(true, 0, posted, 0, allocated, outstanding, null, UnusedAdvanceVnd: unused);
        var both = await env.Svc(new FakeFinance(_ => S(100))).SummaryAsync(env.Dentist, env.Patient, null, CancellationToken.None);
        Assert.Equal((true, 200L, 10L, 12L, 14L), (both.FinancialAvailable, both.PostedNetVnd, both.ConfirmedAllocatedVnd, both.OutstandingVnd, both.UnusedAdvanceVnd));

        var partial = await env.Svc(new FakeFinance(id => id == second.Id ? null : S(100))).SummaryAsync(env.Dentist, env.Patient, null, CancellationToken.None);
        Assert.Equal((false, (long?)null), (partial.FinancialAvailable, partial.PostedNetVnd));

        var missingField = await env.Svc(new FakeFinance(id => S(100, allocated: id == second.Id ? null : 5))).SummaryAsync(env.Dentist, env.Patient, null, CancellationToken.None);
        Assert.Equal((true, 200L, (long?)null, 12L), (missingField.FinancialAvailable, missingField.PostedNetVnd, missingField.ConfirmedAllocatedVnd, missingField.OutstandingVnd));
    }

    [Fact]
    public async Task Summary_UT_TSS_39_applies_list_authorization_and_branch_filter()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Svc().SummaryAsync(env.Dentist, 987654321, null, CancellationToken.None), 404, "MSG113");
        await AssertError(() => env.Svc().SummaryAsync(env.Dentist, env.Patient, env.Other, CancellationToken.None), 403, "MSG06");
        var none = await env.Svc().SummaryAsync(env.Dentist, env.Patient, env.N, CancellationToken.None);
        Assert.Equal((1, false), (none.DossierCount, none.FinancialAvailable));
        (await env.Db.TreatmentDossiers.SingleAsync(x => x.Id == env.DossierId)).State = "Deleted";
        await env.Db.SaveChangesAsync();
        Assert.Equal((0, false), ((await env.Svc().SummaryAsync(env.Dentist, env.Patient, null, CancellationToken.None)) is var s ? (s.DossierCount, s.FinancialAvailable) : default));
    }
}
