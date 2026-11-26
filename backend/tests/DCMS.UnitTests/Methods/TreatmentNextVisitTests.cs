using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
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
/// Method-level tests of TreatmentNextVisit.cs: NextVisitCandidatesAsync, PlannedDatePreviewAsync, ApplyPlannedDatesAsync and helpers
/// (RoundVisitMinutes, PerformedItemIdsAsync / TreatmentBookingQueries, TreatmentItemRules.BookingWindow through the DTO).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TreatmentNextVisitTests
{
    // 09:00 on 10/01/2030 clinic time.
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2030, 1, 10);
    private const int Version = 5;

    private sealed class Scope(bool owner, bool assigned, int[] branches) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, assigned, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Dentist, Dentist2, Assistant, NoEmployee, Customer, DentistEmp, Dentist2Emp, PatientId, ChairId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public TreatmentDossierService Svc(bool owner = false, bool assigned = false, int[]? branches = null) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new UnavailableTreatmentFinancialSummaryPort(), null!, new Scope(owner, assigned, branches ?? [N, Other]));

        public async Task<TreatmentDossier> Dossier(string state = "Open", int? branch = null, int? patient = null, int version = Version, int? dentist = null, int createdMinutes = 0)
        {
            var d = new TreatmentDossier { Code = "HS" + N + "-" + Guid.NewGuid().ToString("N")[..6], PatientId = patient ?? PatientId, BranchId = branch ?? N, DentistId = dentist ?? DentistEmp,
                StartedOn = new DateOnly(2030, 1, 1), Diagnosis = "d", State = state, Version = version, CreatedAt = Now.AddMinutes(createdMinutes), UpdatedAt = Now };
            Db.TreatmentDossiers.Add(d); await Db.SaveChangesAsync(); return d;
        }
        public async Task<TreatmentItem> Item(Guid dossier, int order = 1, DateOnly? start = null, bool done = false, int? minutes = null, int? responsible = null, string name = "Trám răng")
        {
            var i = new TreatmentItem { DossierId = dossier, PlannedOrder = order, ServiceId = 1, ServiceName = name, Region = "11", Unit = "lần", Quantity = 1, AgreedUnitPriceVnd = 100000,
                EstimatedStart = start, DisplayCompleted = done, EstimatedMinutes = minutes, ResponsibleDentistId = responsible };
            Db.TreatmentItems.Add(i); await Db.SaveChangesAsync(); return i;
        }
        public async Task<ClinicalActivity> Perform(Guid dossier, Guid item)
        {
            var a = new ClinicalActivity { DossierId = dossier, ItemId = item, ServiceId = 1, ServiceNameSnapshot = "x", Unit = "lần", Quantity = 1, PriceSnapshotVnd = 1, PerformedAt = Now, PerformerId = DentistEmp, Outcome = "ok", CreatedAt = Now };
            Db.ClinicalActivities.Add(a); await Db.SaveChangesAsync(); return a;
        }
        public async Task<Appointment> Appointment(Guid dossier, string status = "Confirmed", int startHours = 24)
        {
            var a = new Appointment { Code = "LH" + CreateTestKit.NextId(), BranchId = N, DentistId = DentistEmp, ChairId = ChairId, BookerAccountId = Customer, AttendeeName = "Mai", DossierId = dossier,
                StartAt = Now.AddHours(startHours), EndAt = Now.AddHours(startHours + 1), Status = status, Version = 1, CreatedAt = Now,
                BookingInfo = new AppointmentBookingInfo { BookerName = "Mai", BookerContact = "0987654321" } };
            Db.Appointments.Add(a); await Db.SaveChangesAsync(); return a;
        }
        public Task<NextVisitCandidates> Candidates(int? actor = null, int? patient = null, TreatmentDossierService? svc = null) =>
            (svc ?? Svc()).NextVisitCandidatesAsync(actor ?? Dentist, patient ?? PatientId, default);
        public Task<PlannedDatePreview> Preview(Guid dossier, Guid item, DateOnly on, int? actor = null) => Svc().PlannedDatePreviewAsync(actor ?? Dentist, dossier, item, on, default);
        public Task<TreatmentChange> Apply(Guid dossier, IReadOnlyList<PlannedDateInput> changes, int version = Version, Guid? key = null, int? actor = null) =>
            Svc().ApplyPlannedDatesAsync(actor ?? Dentist, dossier, new ApplyPlannedDatesRequest(version, changes, key ?? Guid.NewGuid()), default);
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
        var dentist2 = await CreateTestKit.SeedStaffAsync(db, "bm" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var noEmployee = await CreateTestKit.SeedStaffAsync(db, "ne" + n, AccountRoles.Dentist, n, member: false);
        await CreateTestKit.SeedStaffAsync(db, "ot" + n, AccountRoles.Dentist, other); // makes the other branch exist
        var customer = new ApplicationUser { UserName = "cu" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        db.Users.Add(customer);
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1990, 1, 1), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n)).Chairs.Add(new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Other = other, Dentist = dentist.Id, Dentist2 = dentist2.Id, Assistant = assistant.Id, NoEmployee = noEmployee.Id, Customer = customer.Id,
            DentistEmp = (await db.Employees.SingleAsync(x => x.AccountId == dentist.Id)).Id, Dentist2Emp = (await db.Employees.SingleAsync(x => x.AccountId == dentist2.Id)).Id, PatientId = patient.Id, ChairId = n };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= NextVisitCandidatesAsync: guards =================
    [Fact]
    public async Task Candidates_UT_NXV_01_guards()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Candidates(patient: 987654321), 404, "MSG113");
        await Rejects(() => env.Candidates(actor: 987654321), 403, "MSG06");           // unknown account
        await Rejects(() => env.Candidates(actor: env.Customer), 403, "MSG06");        // not staff
        await Rejects(() => env.Candidates(actor: env.Assistant), 403, "MSG06");       // staff without the dentist role
        await Rejects(() => env.Candidates(actor: env.NoEmployee), 403, "MSG06");      // dentist account without an employee record
        (await env.Db.Users.SingleAsync(x => x.Id == env.Dentist2)).IsActive = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Candidates(actor: env.Dentist2), 403, "MSG06");        // inactive
        Assert.Empty((await env.Candidates()).Items);
    }

    // ================= NextVisitCandidatesAsync: which dossiers =================
    [Fact]
    public async Task Candidates_UT_NXV_02_only_open_dossiers_with_items_in_the_dentists_branches()
    {
        await using var env = await NewEnv();
        var open = await env.Dossier(); await env.Item(open.Id);
        var closed = await env.Dossier("Closed"); await env.Item(closed.Id);
        var deleted = await env.Dossier("Deleted"); await env.Item(deleted.Id);
        await env.Dossier();                                                          // open but without items
        var elsewhere = await env.Dossier(branch: env.Other); await env.Item(elsewhere.Id);   // the dentist is no member of that branch
        var result = await env.Candidates();
        Assert.Equal([open.Id], result.Items.Select(x => x.DossierId));
        Assert.Empty((await env.Candidates(patient: (await NewPatient(env)))).Items);  // another patient has nothing
    }

    private static async Task<int> NewPatient(Env env)
    {
        var p = new PatientProfile { PatientCode = "BZ" + CreateTestKit.NextId(), FullName = "Người khác", NormalizedName = "NGUOI KHAC", CreatedAt = Now, UpdatedAt = Now };
        env.Db.Patients.Add(p); await env.Db.SaveChangesAsync(); return p.Id;
    }

    [Fact]
    public async Task Candidates_UT_NXV_03_assigned_scope_limits_branches_but_owner_or_unassigned_does_not()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); await env.Item(d.Id);
        Assert.Single((await env.Candidates(svc: env.Svc(assigned: false, branches: []))).Items);               // not assigned -> membership only
        Assert.Single((await env.Candidates(svc: env.Svc(assigned: true, branches: [env.N]))).Items);
        Assert.Empty((await env.Candidates(svc: env.Svc(assigned: true, branches: [env.Other]))).Items);        // assigned elsewhere -> filtered out
        Assert.Single((await env.Candidates(svc: env.Svc(owner: true, assigned: true, branches: []))).Items);   // owner ignores the list
    }

    // ================= NextVisitCandidatesAsync: nearest item and DTO =================
    [Fact]
    public async Task Candidates_UT_NXV_04_picks_the_nearest_pending_item_per_dossier()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        await env.Item(d.Id, 1, name: "Không ngày");
        var later = await env.Item(d.Id, 2, Today.AddDays(9), name: "Muộn");
        var nearest = await env.Item(d.Id, 3, Today.AddDays(2), name: "Gần nhất");
        await env.Item(d.Id, 4, Today.AddDays(1), done: true, name: "Đã xong");           // completed -> skipped
        var performed = await env.Item(d.Id, 5, Today, name: "Đã làm"); await env.Perform(d.Id, performed.Id);   // performed -> skipped
        var c = Assert.Single((await env.Candidates()).Items);
        Assert.Equal((nearest.Id, "Gần nhất", Today.AddDays(2), env.N, "Ready"), (c.ItemId, c.ServiceName, c.TargetOn, c.BranchId, c.Status));
        Assert.Null(c.Appointment);
        // when every dated item is gone the item without a date is suggested
        await env.Db.TreatmentItems.Where(x => x.Id == nearest.Id || x.Id == later.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DisplayCompleted, true));
        env.Db.ChangeTracker.Clear();
        var fallback = Assert.Single((await env.Candidates()).Items);
        Assert.Equal(("Không ngày", null, null, null), (fallback.ServiceName, fallback.TargetOn, fallback.WindowFrom, fallback.WindowTo));
    }

    [Fact]
    public async Task Candidates_UT_NXV_05_ties_on_the_date_follow_the_planned_order()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var second = await env.Item(d.Id, 2, Today.AddDays(3), name: "Thứ hai");
        var first = await env.Item(d.Id, 1, Today.AddDays(3), name: "Thứ nhất");
        Assert.Equal(first.Id, Assert.Single((await env.Candidates()).Items).ItemId);
        Assert.NotEqual(second.Id, first.Id);
    }

    [Fact]
    public async Task Candidates_UT_NXV_06_a_deleted_activity_makes_the_item_pending_again()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var item = await env.Item(d.Id, 1, Today.AddDays(1));
        var activity = await env.Perform(d.Id, item.Id);
        Assert.Empty((await env.Candidates()).Items);                                   // the only item was performed
        var snapshot = ActivityDto.From(activity) with { SheetDeleted = true };
        var payload = System.Text.Json.JsonSerializer.Serialize(new { activityId = activity.Id, sheetVersion = 1, activity = snapshot }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        env.Db.TreatmentEvents.Add(new TreatmentEvent { DossierId = d.Id, Kind = "SheetActivityDeleted", PayloadJson = payload, ActorId = env.Dentist, OccurredAt = Now, DossierVersion = Version });
        await env.Db.SaveChangesAsync();
        Assert.Equal(item.Id, Assert.Single((await env.Candidates()).Items).ItemId);
    }

    // Input: EstimatedMinutes -> Expected: DurationMinutes (rounded up to 15, clamped to 15..480, default 30).
    [Theory]
    [InlineData("UT-NXV-07 Không có thời lượng", null, 30)]
    [InlineData("UT-NXV-08 0 phút", 0, 15)]
    [InlineData("UT-NXV-09 1 phút", 1, 15)]
    [InlineData("UT-NXV-10 15 phút", 15, 15)]
    [InlineData("UT-NXV-11 16 phút", 16, 30)]
    [InlineData("UT-NXV-12 45 phút", 45, 45)]
    [InlineData("UT-NXV-13 480 phút", 480, 480)]
    [InlineData("UT-NXV-14 481 phút", 481, 480)]
    [InlineData("UT-NXV-15 1000 phút", 1000, 480)]
    public async Task Candidates_round_the_visit_duration(string caseName, int? minutes, int expected)
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); await env.Item(d.Id, minutes: minutes);
        Assert.Equal(expected, Assert.Single((await env.Candidates()).Items).DurationMinutes);
    }

    // Input: EstimatedStart vs today (2030-01-10) -> Expected: booking window (tolerance 4 days, never before today).
    [Theory]
    [InlineData("UT-NXV-16 Mục tiêu sau hôm nay 2 ngày", 2, 0, 2, 6)]
    [InlineData("UT-NXV-17 Mục tiêu hôm nay", 0, 0, 0, 4)]
    [InlineData("UT-NXV-18 Mục tiêu 4 ngày trước (biên, cửa sổ kết thúc hôm nay)", -4, 0, 0, 0)]
    [InlineData("UT-NXV-19 Mục tiêu 5 ngày trước (quá hạn, dời về hôm nay)", -5, 0, 0, 4)]
    [InlineData("UT-NXV-20 Mục tiêu sau hôm nay 10 ngày", 10, 6, 10, 14)]
    public async Task Candidates_compute_the_booking_window(string caseName, int targetOffset, int fromOffset, int targetShown, int toOffset)
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); await env.Item(d.Id, start: Today.AddDays(targetOffset));
        var c = Assert.Single((await env.Candidates()).Items);
        Assert.Equal((Today.AddDays(targetOffset), Today.AddDays(fromOffset), Today.AddDays(toOffset)), (c.TargetOn, c.WindowFrom, c.WindowTo));
        _ = targetShown;
    }

    [Fact]
    public async Task Candidates_UT_NXV_21_dentist_name_and_responsible_dentist_fallback()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); await env.Item(d.Id, 1, Today, responsible: env.Dentist2Emp);
        var d2 = await env.Dossier(createdMinutes: 5); await env.Item(d2.Id, 1, Today.AddDays(1));
        var d3 = await env.Dossier(createdMinutes: 6); await env.Item(d3.Id, 1, Today.AddDays(1), responsible: 987654);
        var list = (await env.Candidates()).Items.ToDictionary(x => x.DossierId);
        var name2 = (await env.Db.Employees.SingleAsync(x => x.Id == env.Dentist2Emp)).FullName;
        var name1 = (await env.Db.Employees.SingleAsync(x => x.Id == env.DentistEmp)).FullName;
        Assert.Equal((env.Dentist2Emp, name2), (list[d.Id].DentistId, list[d.Id].DentistName));
        Assert.Equal((env.DentistEmp, name1), (list[d2.Id].DentistId, list[d2.Id].DentistName));  // dossier dentist when the item has none
        Assert.Equal("—", list[d3.Id].DentistName);                                               // unknown employee
    }

    [Fact]
    public async Task Candidates_UT_NXV_22_orders_dossiers_by_target_date_then_creation_and_reports_booked_appointments()
    {
        await using var env = await NewEnv();
        var noDate = await env.Dossier(createdMinutes: 1); await env.Item(noDate.Id);
        var far = await env.Dossier(createdMinutes: 2); await env.Item(far.Id, start: Today.AddDays(8));
        var nearB = await env.Dossier(createdMinutes: 4); await env.Item(nearB.Id, start: Today.AddDays(1));
        var nearA = await env.Dossier(createdMinutes: 3); await env.Item(nearA.Id, start: Today.AddDays(1));
        var booked = await env.Appointment(nearA.Id, "Confirmed", 48); await env.Appointment(nearA.Id, "Confirmed", 72);   // the earliest is reported
        await env.Appointment(nearB.Id, "Cancelled"); await env.Appointment(nearB.Id, "NoShow");                       // do not count
        await env.Appointment(far.Id, "InProgress");
        var items = (await env.Candidates()).Items;
        Assert.Equal([nearA.Id, nearB.Id, far.Id, noDate.Id], items.Select(x => x.DossierId));
        Assert.Equal(("Booked", booked.Id, booked.Code), (items[0].Status, items[0].Appointment!.Id, items[0].Appointment!.Code));
        Assert.Equal(("Ready", null), (items[1].Status, items[1].Appointment));
        Assert.Equal("Booked", items[2].Status);
        Assert.Equal("Ready", items[3].Status);
    }

    // ================= PlannedDatePreviewAsync =================
    [Fact]
    public async Task Preview_UT_NXV_23_guards()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var item = await env.Item(d.Id, start: Today);
        await Rejects(() => env.Preview(Guid.NewGuid(), item.Id, Today), 404, "MSG113");
        await Rejects(() => env.Preview(d.Id, Guid.NewGuid(), Today), 404, "MSG113");
        await Rejects(() => env.Preview(d.Id, item.Id, Today, actor: env.Assistant), 403, "MSG06");
    }

    [Fact]
    public async Task Preview_UT_NXV_24_no_change_when_closed_or_the_date_is_the_same()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var item = await env.Item(d.Id, start: Today);
        var same = await env.Preview(d.Id, item.Id, Today);
        Assert.Equal((Version, 0), (same.DossierVersion, same.Items.Count));
        var closed = await env.Dossier("Closed"); var closedItem = await env.Item(closed.Id, start: Today);
        Assert.Empty((await env.Preview(closed.Id, closedItem.Id, Today.AddDays(3))).Items);
    }

    [Fact]
    public async Task Preview_UT_NXV_25_booking_earlier_moves_only_the_item_and_booking_later_pushes_the_pending_items_behind()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var before = await env.Item(d.Id, 1, Today.AddDays(1), name: "Trước");
        var booked = await env.Item(d.Id, 2, Today.AddDays(5), name: "Đặt");
        var after = await env.Item(d.Id, 3, Today.AddDays(10), name: "Sau");
        var undated = await env.Item(d.Id, 4, null, name: "Không ngày");
        var done = await env.Item(d.Id, 5, Today.AddDays(11), done: true, name: "Xong");
        var performed = await env.Item(d.Id, 6, Today.AddDays(12), name: "Đã làm"); await env.Perform(d.Id, performed.Id);
        var earlier = await env.Preview(d.Id, booked.Id, Today.AddDays(3));
        Assert.Equal([booked.Id], earlier.Items.Select(x => x.ItemId));
        Assert.Equal((Today.AddDays(5), Today.AddDays(3), true), (earlier.Items[0].CurrentPlannedOn, earlier.Items[0].ProposedPlannedOn, earlier.Items[0].IsBookedItem));
        var later = await env.Preview(d.Id, booked.Id, Today.AddDays(8));                 // +3 days
        Assert.Equal([booked.Id, after.Id], later.Items.Select(x => x.ItemId));            // earlier order, completed, performed, undated excluded
        Assert.Equal((Today.AddDays(13), false), (later.Items[1].ProposedPlannedOn, later.Items[1].IsBookedItem));
        Assert.Equal(("Sau", Today.AddDays(10)), (later.Items[1].ServiceName, later.Items[1].CurrentPlannedOn));
        _ = (before, undated, done);
    }

    [Fact]
    public async Task Preview_UT_NXV_26_an_item_without_a_planned_date_only_gets_a_date()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var item = await env.Item(d.Id, 1, null); await env.Item(d.Id, 2, Today.AddDays(4));
        var preview = await env.Preview(d.Id, item.Id, Today.AddDays(2));
        var change = Assert.Single(preview.Items);
        Assert.Equal((null, Today.AddDays(2), true), (change.CurrentPlannedOn, change.ProposedPlannedOn, change.IsBookedItem));
    }

    // ================= ApplyPlannedDatesAsync =================
    [Fact]
    public async Task Apply_UT_NXV_27_updates_the_dates_bumps_the_version_and_records_an_event()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var a = await env.Item(d.Id, 1, Today.AddDays(1)); var b = await env.Item(d.Id, 2, null);
        var result = await env.Apply(d.Id, [new(a.Id, Today.AddDays(6)), new(b.Id, Today.AddDays(9))]);
        Assert.Equal(Version + 1, result.Dossier.Version);
        Assert.Equal("MSG222", result.Notification.Code);
        var items = await env.Db.TreatmentItems.AsNoTracking().Where(x => x.DossierId == d.Id).ToDictionaryAsync(x => x.Id);
        Assert.Equal((Today.AddDays(6), Today.AddDays(9)), (items[a.Id].EstimatedStart, items[b.Id].EstimatedStart));
        var saved = await env.Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == d.Id);
        Assert.Equal((Version + 1, Now), (saved.Version, saved.UpdatedAt));
        var evt = Assert.Single(env.Db.TreatmentEvents.Where(x => x.DossierId == d.Id && x.Kind == "ItemPlannedDatesChanged"));
        Assert.Contains(a.Id.ToString(), evt.PayloadJson);
    }

    [Fact]
    public async Task Apply_UT_NXV_28_same_key_replays_and_a_changed_request_conflicts()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var a = await env.Item(d.Id, 1, Today.AddDays(1));
        var key = Guid.NewGuid();
        var first = await env.Apply(d.Id, [new(a.Id, Today.AddDays(6))], key: key);
        var again = await env.Apply(d.Id, [new(a.Id, Today.AddDays(6))], key: key);
        Assert.Equal(first.Dossier.Version, again.Dossier.Version);
        Assert.Equal(Version + 1, (await env.Db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.Id == d.Id)).Version);
        await Rejects(() => env.Apply(d.Id, [new(a.Id, Today.AddDays(7))], key: key), 409, "MSG111");
    }

    [Fact]
    public async Task Apply_UT_NXV_29_guards()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var a = await env.Item(d.Id, 1, Today.AddDays(1));
        await Rejects(() => env.Apply(d.Id, [new(a.Id, Today)], key: Guid.Empty), 400, "MSG04");
        await Rejects(() => env.Apply(Guid.NewGuid(), [new(a.Id, Today)]), 404, "MSG113");
        await Rejects(() => env.Apply(d.Id, [new(a.Id, Today)], actor: env.Assistant), 403, "MSG06");
        await Rejects(() => env.Apply(d.Id, [new(a.Id, Today)], version: Version - 1), 409, "MSG111");
        await Rejects(() => env.Apply(d.Id, []), 400, "MSG04");
        await Rejects(() => env.Apply(d.Id, [new(a.Id, Today), new(a.Id, Today.AddDays(1))]), 400, "MSG04");        // duplicate item
        await Rejects(() => env.Apply(d.Id, [new(a.Id, new DateOnly(1999, 12, 31))]), 400, "MSG04");
        await Rejects(() => env.Apply(d.Id, Enumerable.Range(0, 51).Select(i => new PlannedDateInput(Guid.NewGuid(), Today)).ToArray()), 400, "MSG04");
        await Rejects(() => env.Apply(d.Id, [new(Guid.NewGuid(), Today)]), 404, "MSG113");
        var closed = await env.Dossier("Closed"); var ci = await env.Item(closed.Id, 1, Today);
        await Rejects(() => env.Apply(closed.Id, [new(ci.Id, Today.AddDays(1))]), 400, "MSG04");
        Assert.Equal(Today.AddDays(1), (await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == a.Id)).EstimatedStart);
    }

    [Fact]
    public async Task Apply_UT_NXV_30_boundaries_50_changes_and_year_2000_are_accepted()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier(); var items = new List<PlannedDateInput>();
        for (var i = 0; i < 50; i++) items.Add(new((await env.Item(d.Id, i + 1, Today)).Id, i == 0 ? new DateOnly(2000, 1, 1) : Today.AddDays(i)));
        var result = await env.Apply(d.Id, items);
        Assert.Equal(Version + 1, result.Dossier.Version);
        Assert.Equal(new DateOnly(2000, 1, 1), (await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == items[0].ItemId)).EstimatedStart);
    }

    [Fact]
    public async Task Apply_UT_NXV_31_completed_or_performed_items_cannot_be_rescheduled()
    {
        await using var env = await NewEnv();
        var d = await env.Dossier();
        var open = await env.Item(d.Id, 1, Today);
        var done = await env.Item(d.Id, 2, Today, done: true);
        var performed = await env.Item(d.Id, 3, Today); await env.Perform(d.Id, performed.Id);
        await Rejects(() => env.Apply(d.Id, [new(done.Id, Today.AddDays(1))]), 409, "MSG111");
        await Rejects(() => env.Apply(d.Id, [new(performed.Id, Today.AddDays(1))]), 409, "MSG111");
        await Rejects(() => env.Apply(d.Id, [new(open.Id, Today.AddDays(1)), new(done.Id, Today.AddDays(1))]), 409, "MSG111");   // all or nothing
        Assert.Equal(Today, (await env.Db.TreatmentItems.AsNoTracking().SingleAsync(x => x.Id == open.Id)).EstimatedStart);
    }
}
