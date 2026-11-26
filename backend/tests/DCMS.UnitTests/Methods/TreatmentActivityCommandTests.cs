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
/// Method-level tests of the actual-work (activity) commands of TreatmentDossierService: EditSheetActivityAsync, DeleteSheetActivityAsync,
/// PerformSheetItemAsync, ConfirmSheetActualAsync and helpers (EditableSheetActivityAsync, EditSheetActivityCoreAsync, RecordDossierActivityAsync,
/// RecordSheetFeeCorrection). The clinical posting port is replaced by a fake that returns the stored activity.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentActivityCommandTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const int Version = 5;

    private sealed class Access(int[] branches, params string[] denied) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClinical(DcmsDbContext db) : IClinicalWorkService
    {
        public ClinicalFeeRequest? Fee; public int Calls;
        public async Task<ActivityDto> PostWithinTransactionAsync(int actorId, Guid id, PostActivityRequest request, CancellationToken ct, ClinicalFeeRequest? feeSnapshot = null)
        {
            Fee = feeSnapshot; Calls++;
            return ActivityDto.From(await db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == id, ct));
        }
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Dentist, Dentist2, DentistEmp, Dentist2Emp, AssistantEmp, S1, S2, SOff;
        public required Guid DossierId, ItemId, ActivityId;
        public FakeClinical Clinical = null!;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public TreatmentDossierService Svc(params string[] denied) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new UnavailableTreatmentFinancialSummaryPort(), Clinical ??= new FakeClinical(Db), new Access([N], denied));
        public Dictionary<string, object?> Row() => new()
        {
            ["serviceId"] = S1, ["dentistId"] = DentistEmp, ["regions"] = new[] { "11" }, ["quantity"] = 2, ["costVnd"] = 200000, ["unitPriceVnd"] = 100000, ["discountValue"] = 0, ["note"] = "Đã trám"
        };
        public static JsonElement J(Dictionary<string, object?> row) => JsonSerializer.SerializeToElement(row, Web);

        public Task<ConfirmSheetActualResult> Edit(Action<Dictionary<string, object?>>? change = null, int version = Version, int activityVersion = 1, int sheetVersion = 0, Guid? key = null, Guid? activity = null, Guid? dossier = null, int? actor = null, params string[] denied)
        {
            var row = Row(); change?.Invoke(row);
            return Svc(denied).EditSheetActivityAsync(actor ?? Dentist, dossier ?? DossierId, activity ?? ActivityId, new EditSheetActivityRequest(version, activityVersion, sheetVersion, J(row), key ?? Guid.NewGuid()), CancellationToken.None);
        }
        public Task<ConfirmSheetActualResult> DeleteActivity(int version = Version, int activityVersion = 1, int sheetVersion = 0, Guid? key = null, Guid? activity = null) =>
            Svc().DeleteSheetActivityAsync(Dentist, DossierId, activity ?? ActivityId, new SheetActivityActionRequest(version, activityVersion, sheetVersion, key ?? Guid.NewGuid()), CancellationToken.None);
        public Task<ConfirmSheetActualResult> Perform(Action<Dictionary<string, object?>>? row = null, bool withRow = false, int version = Version, Guid? key = null, Guid? item = null, int? actor = null)
        {
            JsonElement? json = null;
            if (withRow) { var r = Row(); row?.Invoke(r); json = J(r); }
            return Svc().PerformSheetItemAsync(actor ?? Dentist, DossierId, item ?? ItemId, new PerformSheetItemRequest(version, key ?? Guid.NewGuid(), json), CancellationToken.None);
        }
        public async Task<Guid> ActualDraft(Action<Dictionary<string, object?>>? change = null, string kind = "Actual")
        {
            var row = Row(); change?.Invoke(row);
            var id = Guid.NewGuid();
            await Svc().SaveSheetDraftAsync(Dentist, DossierId, id, new SaveSheetDraftRequest(kind, null, J(row), 0, Guid.NewGuid()), CancellationToken.None);
            return id;
        }
        public Task<ConfirmSheetActualResult> Confirm(Guid rowId, int version = Version, int draftVersion = 1, Guid? key = null) =>
            Svc().ConfirmSheetActualAsync(Dentist, DossierId, rowId, new ConfirmSheetActualRequest(version, draftVersion, key ?? Guid.NewGuid()), CancellationToken.None);
        public async Task SetState(string state) { (await Db.TreatmentDossiers.SingleAsync(x => x.Id == DossierId)).State = state; await Db.SaveChangesAsync(); }
        public async Task<ClinicalActivity> Source(Guid? id = null) => await Db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == (id ?? ActivityId));
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
        var n = CreateTestKit.NextId();
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var dentist2 = await CreateTestKit.SeedStaffAsync(db, "bsb" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var emp = async (ApplicationUser u) => (await db.Employees.SingleAsync(x => x.AccountId == u.Id)).Id;
        var assistantEmp = await emp(assistant);
        (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n)).Members.Single(m => m.EmployeeId == assistantEmp).CanDentist = false;
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        var s1 = new SchedulingService { BranchId = n, Name = "Trám răng", Unit = "lần" };
        var s2 = new SchedulingService { BranchId = n, Name = "Nhổ răng", Unit = "răng" };
        var off = new SchedulingService { BranchId = n, Name = "Dịch vụ ngừng", Unit = "lần", Active = false };
        db.Set<SchedulingService>().AddRange(s1, s2, off);
        await db.SaveChangesAsync();
        var dentistEmp = await emp(dentist);
        var dossier = new TreatmentDossier { Code = "HS" + n, PatientId = patient.Id, BranchId = n, DentistId = dentistEmp, StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = "Open", Version = Version, CreatedAt = Now, UpdatedAt = Now };
        var item = new TreatmentItem { DossierId = dossier.Id, PlannedOrder = 1, ServiceId = s1.Id, ServiceName = "Trám răng", Unit = "lần", Quantity = 2, AgreedUnitPriceVnd = 100000,
            Region = "11, 12", ResponsibleDentistId = dentistEmp, DiscountType = "Percent", DiscountValue = 10, DiscountReason = "VIP" };
        dossier.Items.Add(item);
        db.TreatmentDossiers.Add(dossier);
        var activity = new ClinicalActivity { DossierId = dossier.Id, ServiceId = s1.Id, ServiceNameSnapshot = "Trám răng", Unit = "lần", Quantity = 2, PriceSnapshotVnd = 100000, PerformedAt = Now,
            PerformerId = dentistEmp, Outcome = "ok", CreatedAt = Now };
        db.ClinicalActivities.Add(activity);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Dentist = dentist.Id, Dentist2 = dentist2.Id, DentistEmp = dentistEmp, Dentist2Emp = await emp(dentist2), AssistantEmp = assistantEmp,
            S1 = s1.Id, S2 = s2.Id, SOff = off.Id, DossierId = dossier.Id, ItemId = item.Id, ActivityId = activity.Id };
    }

    private static async Task AssertError(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= EditSheetActivityAsync =================
    [Fact]
    public async Task EditActivity_UT_TAC_01_updates_the_sheet_view_without_touching_the_posted_row()
    {
        await using var env = await NewEnv();
        var result = await env.Edit(r => { r["date"] = "2030-01-12"; r["labWork"] = " Mão sứ "; r["assistantId"] = env.AssistantEmp; r["regions"] = new[] { "11", "12" }; });
        var a = result.Activity;
        Assert.Equal((1, "Trám răng", "11, 12", "Đã trám", "Mão sứ", env.AssistantEmp, DateTimeOffset.Parse("2030-01-11T17:00:00Z"), false, "Unavailable"),
            (a.SheetVersion, a.ServiceNameSnapshot, a.Region, a.Outcome, a.LabWorkSnapshot, a.AssistantId, a.PerformedAt, a.SheetDeleted, a.FeeStatus));
        Assert.Equal(Version + 1, result.Dossier.Version);
        var source = await env.Source();
        Assert.Equal(("ok", 1), (source.Outcome, source.Version)); // the original activity row is unchanged
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "SheetActivityEdited");
        var second = await env.Edit(r => r["note"] = "Lần 2", Version + 1, 1, 1);
        Assert.Equal((2, "Lần 2"), (second.Activity.SheetVersion, second.Activity.Outcome));
    }

    // Input: a mutation of the valid row -> Expected: BusinessException.
    public static TheoryData<string, string, int, string> BadActivityRows => new()
    {
        { "UT-TAC-02 Số lượng khác hoạt động gốc", "quantity", 400, "MSG04" },
        { "UT-TAC-03 Bác sĩ khác bác sĩ phụ trách hồ sơ", "dentist", 400, "MSG04" },
        { "UT-TAC-04 Dịch vụ không tồn tại", "noService", 400, "MSG04" },
        { "UT-TAC-05 Dịch vụ đang ngừng (khác dịch vụ cũ)", "inactiveService", 400, "MSG04" },
        { "UT-TAC-07 Trợ lý là bác sĩ", "assistantDentist", 400, "MSG04" },
        { "UT-TAC-08 Thành tiền không khớp", "cost", 400, "MSG04" },
        { "UT-TAC-09 Giảm giá âm", "negative", 400, "MSG252" },
        { "UT-TAC-10 Loại giảm giá lạ", "badType", 400, "MSG252" },
        { "UT-TAC-11 Phần trăm quá 100", "over", 400, "MSG252" },
        { "UT-TAC-12 Có giảm giá nhưng thiếu lý do", "noReason", 400, "MSG252" }
    };

    [Theory, MemberData(nameof(BadActivityRows))]
    public async Task EditActivity_rejects_invalid_rows(string caseName, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Edit(r =>
        {
            switch (problem)
            {
                case "quantity": r["quantity"] = 3; r["costVnd"] = 300000; break;
                case "dentist": r["dentistId"] = env.Dentist2Emp; break;
                case "noService": r["serviceId"] = 987654321; break;
                case "inactiveService": r["serviceId"] = env.SOff; break;
                case "assistantDentist": r["assistantId"] = env.Dentist2Emp; break;
                case "cost": r["costVnd"] = 199999; break;
                case "negative": r["discountType"] = "Percent"; r["discountValue"] = -1; break;
                case "badType": r["discountType"] = "Other"; r["discountValue"] = 5; break;
                case "over": r["discountType"] = "Percent"; r["discountValue"] = 101; break;
                case "noReason": r["discountType"] = "Percent"; r["discountValue"] = 10; break;
            }
        }), status, code is "MSG04" && problem == "fractional" ? "MSG04" : code);
    }

    [Fact]
    public async Task EditActivity_UT_TAC_13_fractional_quantity_with_a_whole_number_unit_is_rejected()
    {
        await using var env = await NewEnv();
        await env.Db.ClinicalActivities.Where(x => x.Id == env.ActivityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, 1.5m));
        env.Db.ChangeTracker.Clear();
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.Edit(r => { r["serviceId"] = env.S2; r["quantity"] = 1.5; r["costVnd"] = 150000; }), 400, "MSG04");
    }

    [Fact]
    public async Task EditActivity_UT_TAC_14_a_service_deactivated_since_may_stay_on_the_activity()
    {
        await using var env = await NewEnv();
        (await env.Db.Set<SchedulingService>().SingleAsync(x => x.Id == env.S1)).Active = false;
        await env.Db.SaveChangesAsync();
        Assert.Equal(env.S1, (await env.Edit()).Activity.ServiceId);
    }

    [Fact]
    public async Task EditActivity_UT_TAC_15_guards()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Edit(key: Guid.Empty), 400, "MSG04");
        await AssertError(() => env.Edit(dossier: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Edit(actor: env.Dentist2), 403, "MSG06");
        await AssertError(() => env.Edit(version: Version - 1), 409, "MSG111");
        await AssertError(() => env.Edit(activity: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Edit(activityVersion: 9), 409, "MSG111");
        await AssertError(() => env.Edit(sheetVersion: 9), 409, "MSG111");
        Assert.Equal(Version, (await env.Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == env.DossierId)).Version);
    }

    [Fact]
    public async Task EditActivity_UT_TAC_16_a_price_change_needs_permission_and_asks_for_a_fee_correction()
    {
        await using var env = await NewEnv();
        void Cheaper(Dictionary<string, object?> r) { r["unitPriceVnd"] = 80000; r["costVnd"] = 160000; }
        await AssertError(() => env.Edit(Cheaper, denied: "treatment.items.price"), 403, "MSG06");
        env.Db.ChangeTracker.Clear();
        var result = await env.Edit(Cheaper);
        Assert.Equal((80000m, "NeedsAdjustment"), (result.Activity.PriceSnapshotVnd, result.Activity.FeeStatus));
        var correction = await env.Db.TreatmentProgressionEvents.AsNoTracking().SingleAsync(x => x.AggregateId == env.ActivityId);
        Assert.Equal(("clinical.fee.correction-requested", $"clinical-fee-correction:{env.ActivityId}:1"), (correction.Kind, correction.StableKey));
    }

    [Fact]
    public async Task EditActivity_UT_TAC_17_a_new_discount_with_a_reason_is_accepted()
    {
        await using var env = await NewEnv();
        var a = (await env.Edit(r => { r["discountType"] = "VND"; r["discountValue"] = 50000; r["discountNote"] = "Khách quen"; })).Activity;
        Assert.Equal((50000L, "Fixed", "Khách quen", "NeedsAdjustment"), (a.DiscountSnapshotVnd, a.SheetDiscountType, a.SheetDiscountReason, a.FeeStatus));
    }

    // ================= DeleteSheetActivityAsync =================
    [Fact]
    public async Task DeleteActivity_UT_TAC_18_hides_the_activity_and_asks_for_a_fee_correction_once()
    {
        await using var env = await NewEnv();
        var result = await env.DeleteActivity();
        Assert.Equal((true, 1, Version + 1), (result.Activity.SheetDeleted, result.Activity.SheetVersion, result.Dossier.Version));
        Assert.Equal(1, await env.Db.TreatmentProgressionEvents.CountAsync(x => x.AggregateId == env.ActivityId));
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "SheetActivityDeleted");
        await env.DeleteActivity(Version + 1, 1, 1); // deleting again changes nothing financially
        Assert.Equal(1, await env.Db.TreatmentProgressionEvents.CountAsync(x => x.AggregateId == env.ActivityId));
    }

    [Fact]
    public async Task DeleteActivity_UT_TAC_19_free_activity_needs_no_fee_correction_and_guards_apply()
    {
        await using var env = await NewEnv();
        await env.Db.ClinicalActivities.Where(x => x.Id == env.ActivityId).ExecuteUpdateAsync(s => s.SetProperty(x => x.PriceSnapshotVnd, 0m));
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.DeleteActivity(Version - 1), 409, "MSG111");
        await AssertError(() => env.DeleteActivity(activity: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.DeleteActivity(sheetVersion: 3), 409, "MSG111");
        await env.DeleteActivity();
        Assert.Equal(0, await env.Db.TreatmentProgressionEvents.CountAsync(x => x.AggregateId == env.ActivityId));
    }

    [Fact]
    public async Task EditActivity_UT_TAC_20_editing_a_deleted_activity_restores_it_and_requests_a_correction()
    {
        await using var env = await NewEnv();
        await env.DeleteActivity();
        var restored = await env.Edit(version: Version + 1, sheetVersion: 1);
        Assert.Equal((false, 2), (restored.Activity.SheetDeleted, restored.Activity.SheetVersion));
        Assert.Equal(2, await env.Db.TreatmentProgressionEvents.CountAsync(x => x.AggregateId == env.ActivityId));
    }

    // ================= PerformSheetItemAsync =================
    [Fact]
    public async Task Perform_UT_TAC_21_records_the_planned_item_as_done()
    {
        await using var env = await NewEnv();
        var result = await env.Perform();
        var a = await env.Db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == result.Activity.Id);
        Assert.Equal((env.ItemId, true, 100000m, 20000L, 2m, "Trám răng", "11, 12", Now, "NotRequested", env.DentistEmp, 1),
            (a.ItemId, a.IsPlanTransfer, a.PriceSnapshotVnd, a.DiscountSnapshotVnd, a.Quantity, a.ServiceNameSnapshot, a.Region, a.PerformedAt, a.FeeStatus, a.PerformerId, a.PlanVersion));
        Assert.True((await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == env.ItemId)).DisplayCompleted);
        Assert.Equal(Version + 1, result.Dossier.Version);
        Assert.Null(env.Clinical.Fee); // no sheet row: the fee is taken from the plan
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "SheetItemPerformed");
    }

    [Fact]
    public async Task Perform_UT_TAC_22_with_a_sheet_row_uses_its_values_and_sends_a_fee_request()
    {
        await using var env = await NewEnv();
        var result = await env.Perform(r => { r["date"] = "2030-01-12"; r["note"] = " Xong "; r["labWork"] = "Lab A"; }, withRow: true);
        var a = await env.Db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == result.Activity.Id);
        Assert.Equal((DateTimeOffset.Parse("2030-01-11T17:00:00Z"), "Xong", "Lab A", "11", 0L), (a.PerformedAt, a.Outcome, a.LabWorkSnapshot, a.Region, a.DiscountSnapshotVnd));
        Assert.NotNull(env.Clinical.Fee);
    }

    [Fact]
    public async Task Perform_UT_TAC_23_second_perform_returns_or_edits_the_existing_transfer()
    {
        await using var env = await NewEnv();
        var first = await env.Perform();
        var again = await env.Perform(version: Version + 1);
        Assert.Equal(first.Activity.Id, again.Activity.Id);
        Assert.Equal(1, await env.Db.ClinicalActivities.CountAsync(x => x.ItemId == env.ItemId));
        var edited = await env.Perform(r => r["note"] = "Sửa lại", withRow: true, version: Version + 2);
        Assert.Equal(("Sửa lại", 1), (edited.Activity.Outcome, edited.Activity.SheetVersion));
        Assert.Equal(1, await env.Db.ClinicalActivities.CountAsync(x => x.ItemId == env.ItemId));
    }

    [Fact]
    public async Task Perform_UT_TAC_24_guards()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Perform(key: Guid.Empty), 400, "MSG04");
        await AssertError(() => env.Perform(version: Version - 1), 409, "MSG111");
        await AssertError(() => env.Perform(item: Guid.NewGuid()), 404, "MSG113");
        await AssertError(() => env.Perform(actor: env.Dentist2), 403, "MSG06");
        (await env.Db.TreatmentItems.SingleAsync(x => x.Id == env.ItemId)).ResponsibleDentistId = env.Dentist2Emp;
        await env.Db.SaveChangesAsync();
        await AssertError(() => env.Perform(), 403, "MSG06"); // the item belongs to another dentist
        (await env.Db.TreatmentItems.SingleAsync(x => x.Id == env.ItemId)).ResponsibleDentistId = env.DentistEmp;
        await env.SetState("Closed");
        await AssertError(() => env.Perform(), 409, "MSG230");
        Assert.Equal(0, env.Clinical?.Calls ?? 0);
    }

    [Theory]
    [InlineData("UT-TAC-25 Bác sĩ trong dòng khác bác sĩ gọi", "dentist", 400, "MSG04")]
    [InlineData("UT-TAC-26 Số lượng khác hạng mục", "quantity", 400, "MSG04")]
    [InlineData("UT-TAC-27 Thành tiền không khớp", "cost", 400, "MSG04")]
    [InlineData("UT-TAC-28 Dịch vụ không tồn tại", "noService", 400, "MSG04")]
    [InlineData("UT-TAC-29 Trợ lý là bác sĩ", "assistant", 400, "MSG04")]
    [InlineData("UT-TAC-30 Loại giảm giá lạ", "badType", 400, "MSG252")]
    [InlineData("UT-TAC-31 Giảm cố định lớn hơn thành tiền", "tooMuch", 400, "MSG252")]
    [InlineData("UT-TAC-32 Ghi chú dài 1001 ký tự", "longNote", 400, "MSG04")]
    public async Task Perform_with_a_row_rejects_invalid_rows(string caseName, string problem, int status, string code)
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Perform(r =>
        {
            switch (problem)
            {
                case "dentist": r["dentistId"] = env.Dentist2Emp; break;
                case "quantity": r["quantity"] = 3; r["costVnd"] = 300000; break;
                case "cost": r["costVnd"] = 1; break;
                case "noService": r["serviceId"] = 987654321; break;
                case "assistant": r["assistantId"] = env.Dentist2Emp; break;
                case "badType": r["discountType"] = "Other"; r["discountValue"] = 5; r["discountNote"] = "x"; break;
                case "tooMuch": r["discountType"] = "Fixed"; r["discountValue"] = 200001; r["discountNote"] = "x"; break;
                case "longNote": r["note"] = new string('a', 1001); break;
            }
        }, withRow: true), status, code);
        Assert.False((await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == env.ItemId)).DisplayCompleted);
    }

    // ================= ConfirmSheetActualAsync =================
    [Fact]
    public async Task ConfirmActual_UT_TAC_33_records_a_standalone_activity_from_a_draft()
    {
        await using var env = await NewEnv();
        var rowId = await env.ActualDraft(r => { r["serviceId"] = env.S1; r["date"] = "2030-01-12"; });
        var result = await env.Confirm(rowId);
        var a = await env.Db.ClinicalActivities.AsNoTracking().SingleAsync(x => x.Id == rowId); // the draft id becomes the activity id
        Assert.Equal((env.DossierId, (Guid?)null, false, "Trám răng", 100000m, DateTimeOffset.Parse("2030-01-11T17:00:00Z")),
            (a.DossierId, a.ItemId, a.IsPlanTransfer, a.ServiceNameSnapshot, a.PriceSnapshotVnd, a.PerformedAt));
        Assert.Equal((rowId, Version + 1), (result.Activity.Id, result.Dossier.Version));
        Assert.Empty(env.Db.TreatmentSheetDrafts.Where(x => x.Id == rowId));
        Assert.Contains(env.Db.TreatmentEvents.Where(x => x.DossierId == env.DossierId), e => e.Kind == "DossierActualRecorded");
    }

    [Fact]
    public async Task ConfirmActual_UT_TAC_34_a_draft_for_a_plan_item_must_match_the_item()
    {
        await using var env = await NewEnv();
        await AssertError(() => env.Confirm(env.ActualDraft(r => r["sourceItemId"] = Guid.NewGuid()).Result), 404, "MSG113");
        await AssertError(() => env.Confirm(env.ActualDraft(r => { r["sourceItemId"] = env.ItemId; r["serviceId"] = env.S2; }).Result), 400, "MSG04");
        await AssertError(() => env.Confirm(env.ActualDraft(r => { r["sourceItemId"] = env.ItemId; r["quantity"] = 3; r["costVnd"] = 300000; }).Result), 400, "MSG04");
        await AssertError(() => env.Confirm(env.ActualDraft(r => { r["sourceItemId"] = env.ItemId; r["regions"] = new[] { "13" }; }).Result), 400, "MSG04"); // outside the agreed region
        var ok = await env.Confirm(await env.ActualDraft(r => { r["sourceItemId"] = env.ItemId; r["regions"] = new[] { "12" }; }));
        Assert.Equal(env.ItemId, ok.Activity.ItemId);
    }

    [Fact]
    public async Task ConfirmActual_UT_TAC_35_draft_guards_and_replay()
    {
        await using var env = await NewEnv();
        var rowId = await env.ActualDraft();
        await AssertError(() => env.Confirm(rowId, key: Guid.Empty), 400, "MSG04");
        await AssertError(() => env.Confirm(rowId, version: Version - 1), 409, "MSG111");
        await AssertError(() => env.Confirm(rowId, draftVersion: 4), 409, "MSG111");
        await AssertError(() => env.Confirm(Guid.NewGuid()), 404, "MSG113");
        var planDraft = await env.ActualDraft(kind: "Plan");
        await AssertError(() => env.Confirm(planDraft), 409, "MSG111"); // wrong draft kind
        var key = Guid.NewGuid();
        var first = await env.Confirm(rowId, key: key);
        var again = await env.Confirm(rowId, key: key);
        Assert.Equal(first.Activity.Id, again.Activity.Id);
        Assert.Equal(1, await env.Db.ClinicalActivities.CountAsync(x => x.Id == rowId));
    }
}
