using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Reception;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of the read side of ReceptionService: OptionsAsync, AppointmentsAsync, DetailAsync, QueueAsync, VisitAsync and the
/// private helpers they use (ActorAsync, ScopeAsync, Own, Query validation, Ineligible, the DTO builders).
/// Input = actor + query + stored data; Expected = DTO content or BusinessException (status + code).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReceptionQueryTests
{
    // Now = 09:00 on 10/01/2030 clinic time (UTC+7).
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2030, 1, 10);

    private sealed class Access(bool owner, int[] branches, string[] permissions) : ISettingsAccess
    {
        public readonly List<string> Asked = [];
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, false, [], permissions, branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) { Asked.Add(permission); return Task.CompletedTask; }
    }

    private sealed class Env : IDisposable
    {
        public required CreateTestKit.PostgresDb Pg; public required int Branch, OtherBranch;
        public required int Assistant, Dentist, Dentist2, ElsewhereAssistant, Customer, NoEmployee, DentistEmp, Dentist2Emp, ChairId;
        public required PatientProfile Mai, Nam;
        public required TestQueries Queries;
        public DcmsDbContext Db => Pg.Db;
        public Access Access = null!;
        public void Dispose() => Db.Dispose();
        public ReceptionService Service(bool owner = false, string[]? permissions = null, int[]? branches = null, TimeProvider? clock = null)
        {
            Access = new Access(owner, branches ?? [Branch, OtherBranch], permissions ?? []);
            return new(Db, CreateTestKit.NewRoleUserManager(Db), Queries, new TestAvailability(), clock ?? new TestClock(Now), Access);
        }
        public ReceptionQuery Q(int? branch = null, DateOnly? date = null, int? dentist = null, string? status = null, string? keyword = null, int page = 1, int size = 20) =>
            new(branch ?? Branch, date ?? Today, dentist, status, keyword, page, size);

        public async Task<Appointment> Appt(DateTimeOffset start, string status = "Confirmed", string name = "Trần Thị Mai", int? dentist = null, int? patient = null, int minutes = 60)
        {
            var a = new Appointment
            {
                Code = "LH" + CreateTestKit.NextId(), BranchId = Branch, DentistId = dentist ?? DentistEmp, ChairId = ChairId, BookerAccountId = Customer, AttendeeName = name,
                PatientId = patient, StartAt = start, EndAt = start.AddMinutes(minutes), Status = status, Version = 3, CreatedAt = Now,
                BookingInfo = new AppointmentBookingInfo { BookerName = "Người đặt", BookerContact = "0987654321" }
            };
            Db.Appointments.Add(a); await Db.SaveChangesAsync(); return a;
        }
        public async Task<Visit> VisitFor(Appointment a, string status = VisitStatuses.Queued, DateTimeOffset? arrival = null)
        {
            a.PatientId ??= Mai.Id;
            var v = new Visit { AppointmentId = a.Id, BranchId = a.BranchId, PatientId = a.PatientId!.Value, DentistId = a.DentistId, ArrivalAt = arrival ?? Now.AddMinutes(-30), RecordedBy = Assistant, Status = status };
            Db.Visits.Add(v); await Db.SaveChangesAsync(); return v;
        }
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
        var assistant = await CreateTestKit.SeedStaffAsync(db, "as" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dn" + n, AccountRoles.Dentist, n);
        var dentist2 = await CreateTestKit.SeedStaffAsync(db, "dm" + n, AccountRoles.Dentist, n);
        var elsewhere = await CreateTestKit.SeedStaffAsync(db, "el" + n, AccountRoles.ClinicAssistant, other);
        var noEmployee = await CreateTestKit.SeedStaffAsync(db, "ne" + n, AccountRoles.ClinicAssistant, n, member: false);
        var customer = new ApplicationUser { UserName = "cu" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        db.Users.Add(customer);
        PatientProfile P(string code, string name) => new() { PatientCode = code + n, FullName = name, NormalizedName = name.ToUpperInvariant(), BirthDate = new DateOnly(1985, 5, 5), CreatedAt = Now, UpdatedAt = Now };
        var mai = P("BN", "Trần Thị Mai"); var nam = P("BM", "Lê Văn Nam");
        db.Patients.AddRange(mai, nam);
        await db.SaveChangesAsync();
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        branch.Chairs.Add(new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        await db.SaveChangesAsync();
        var emp = async (ApplicationUser u) => (await db.Employees.SingleAsync(x => x.AccountId == u.Id)).Id;
        var dentistEmp = await emp(dentist); var dentist2Emp = await emp(dentist2);
        var queries = new TestQueries
        {
            Options = new AppointmentOptions(
                [new CalendarOption(n, "CN " + n), new CalendarOption(other, "CN khác")],
                [new CalendarDentist(dentistEmp, n, "BS A"), new CalendarDentist(dentist2Emp, n, "BS B"), new CalendarDentist(999, other, "BS C")],
                [new CalendarChair(n, n, "Ghế 1"), new CalendarChair(998, other, "Ghế 2")],
                [new CalendarService(1, n, "Trám", 30), new CalendarService(2, other, "Nhổ", 30)], false)
        };
        return new Env { Pg = pg, Branch = n, OtherBranch = other, Assistant = assistant.Id, Dentist = dentist.Id, Dentist2 = dentist2.Id, ElsewhereAssistant = elsewhere.Id,
            Customer = customer.Id, NoEmployee = noEmployee.Id, DentistEmp = dentistEmp, Dentist2Emp = dentist2Emp, ChairId = n, Mai = mai, Nam = nam, Queries = queries };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= Query validation (private Query) =================
    // Input: one bad field of the query -> Expected: 400 MSG04.
    public static TheoryData<string, string> BadQueries => new()
    {
        { "UT-RCQ-01 Năm 1999 (dưới 2000)", "yearLow" },
        { "UT-RCQ-02 Năm 2101 (trên 2100)", "yearHigh" },
        { "UT-RCQ-03 Trang 0", "pageLow" },
        { "UT-RCQ-04 Trang 100001", "pageHigh" },
        { "UT-RCQ-05 Cỡ trang 10 (chỉ 20/50/100)", "size" },
        { "UT-RCQ-06 Từ khóa 101 ký tự", "keyword" },
        { "UT-RCQ-07 DentistId = 0", "dentist" },
        { "UT-RCQ-08 Trạng thái lạ", "status" },
        { "UT-RCQ-09 Trạng thái của hàng đợi dùng ở danh sách lịch", "queueStatusOnList" },
    };

    [Theory, MemberData(nameof(BadQueries))]
    public async Task Appointments_rejects_invalid_queries(string caseName, string problem)
    {
        using var env = await NewEnv();
        var q = problem switch
        {
            "yearLow" => env.Q(date: new DateOnly(1999, 12, 31)),
            "yearHigh" => env.Q(date: new DateOnly(2101, 1, 1)),
            "pageLow" => env.Q(page: 0),
            "pageHigh" => env.Q(page: 100001),
            "size" => env.Q(size: 10),
            "keyword" => env.Q(keyword: new string('a', 101)),
            "dentist" => env.Q(dentist: 0),
            "status" => env.Q(status: "Bogus"),
            _ => env.Q(status: "Queued")
        };
        await Rejects(() => env.Service().AppointmentsAsync(env.Assistant, q, default), 400, "MSG04");
    }

    [Fact]
    public async Task Queue_UT_RCQ_10_uses_visit_statuses_and_rejects_appointment_statuses()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().QueueAsync(env.Assistant, env.Q(status: "Confirmed"), default), 400, "MSG04");
        await env.Service().QueueAsync(env.Assistant, env.Q(status: "Queued"), default);
    }

    [Fact]
    public async Task Appointments_UT_RCQ_11_boundary_values_are_accepted()
    {
        using var env = await NewEnv();
        var s = env.Service();
        foreach (var q in new[] { env.Q(date: new DateOnly(2000, 1, 1)), env.Q(date: new DateOnly(2100, 12, 31)), env.Q(page: 100000, size: 100), env.Q(keyword: new string('a', 100), size: 50) })
            Assert.NotNull(await s.AppointmentsAsync(env.Assistant, q, default));
    }

    // ================= Actor / scope guards =================
    [Fact]
    public async Task Scope_UT_RCQ_12_unknown_inactive_or_locked_accounts_get_401_MSG07()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().AppointmentsAsync(987654, env.Q(), default), 401, "MSG07");
        var user = await env.Db.Users.SingleAsync(x => x.Id == env.Assistant);
        user.LockoutEnabled = true; user.LockoutEnd = Now.AddMinutes(5); await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service().AppointmentsAsync(env.Assistant, env.Q(), default), 401, "MSG07");
        user.LockoutEnd = Now.AddMinutes(-1); await env.Db.SaveChangesAsync(); // lock expired
        Assert.NotNull(await env.Service().AppointmentsAsync(env.Assistant, env.Q(), default));
        user.IsActive = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service().AppointmentsAsync(env.Assistant, env.Q(), default), 401, "MSG07");
    }

    [Fact]
    public async Task Scope_UT_RCQ_13_customer_account_staff_without_employee_and_other_branch_get_403()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().AppointmentsAsync(env.Customer, env.Q(), default), 403, "MSG06");
        await Rejects(() => env.Service().AppointmentsAsync(env.NoEmployee, env.Q(), default), 403, "MSG06");
        await Rejects(() => env.Service().AppointmentsAsync(env.ElsewhereAssistant, env.Q(), default), 403, "MSG06");
        await Rejects(() => env.Service().AppointmentsAsync(env.Assistant, env.Q(branch: env.OtherBranch), default), 403, "MSG06");
        Assert.NotNull(await env.Service().AppointmentsAsync(env.Dentist, env.Q(), default)); // a dentist member may read
    }

    [Fact]
    public async Task Scope_UT_RCQ_14_dentist_flagged_as_non_dentist_member_and_inactive_branch()
    {
        using var env = await NewEnv();
        var branch = await env.Db.Set<SchedulingBranch>().SingleAsync(x => x.Id == env.Branch);
        branch.Members.Single(m => m.EmployeeId == env.DentistEmp).CanDentist = false;
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service().AppointmentsAsync(env.Dentist, env.Q(), default), 403, "MSG06");
        Assert.NotNull(await env.Service().AppointmentsAsync(env.Assistant, env.Q(), default)); // an assistant needs no CanDentist
        branch.Active = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service().AppointmentsAsync(env.Assistant, env.Q(), default), 403, "MSG06");
    }

    [Fact]
    public async Task Scope_UT_RCQ_15_owner_may_read_any_active_branch_but_not_an_inactive_one()
    {
        using var env = await NewEnv();
        // the assistant is not a member of OtherBranch, yet an owner permission state lets them in once that branch exists and is active
        await env.Db.Set<SchedulingBranch>().Where(x => x.Id == env.OtherBranch).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, true));
        env.Db.ChangeTracker.Clear();
        Assert.NotNull(await env.Service(owner: true).AppointmentsAsync(env.Assistant, env.Q(branch: env.OtherBranch), default));
        Assert.Equal(["reception.read"], env.Access.Asked);
        await Rejects(() => env.Service(owner: true).AppointmentsAsync(env.Assistant, env.Q(branch: 88888888), default), 403, "MSG06");
        await env.Db.Set<SchedulingBranch>().Where(x => x.Id == env.OtherBranch).ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false));
        await Rejects(() => env.Service(owner: true).AppointmentsAsync(env.Assistant, env.Q(branch: env.OtherBranch), default), 403, "MSG06");
    }

    // ================= AppointmentsAsync =================
    [Fact]
    public async Task Appointments_UT_RCQ_16_returns_only_the_day_and_branch_ordered_by_start_with_paging()
    {
        using var env = await NewEnv();
        var late = await env.Appt(Now.AddHours(5)); var early = await env.Appt(Now.AddHours(1));
        await env.Appt(Now.AddDays(1));                                   // tomorrow
        await env.Appt(new DateTimeOffset(2030, 1, 9, 16, 59, 0, TimeSpan.Zero)); // 23:59 clinic time yesterday
        await env.Appt(new DateTimeOffset(2030, 1, 10, 17, 0, 0, TimeSpan.Zero)); // exactly midnight tomorrow
        var first = await env.Service().AppointmentsAsync(env.Assistant, env.Q(size: 20), default);
        Assert.Equal((2, 1, 20, Today, Now), (first.Total, first.Page, first.PageSize, first.ClinicDate, first.ServerNow));
        Assert.Equal([early.Id, late.Id], first.Items.Select(x => x.AppointmentId));
        var p2 = await env.Service().AppointmentsAsync(env.Assistant, env.Q(page: 2, size: 20), default);
        Assert.Equal((2, 0), (p2.Total, p2.Items.Count));
    }

    [Fact]
    public async Task Appointments_UT_RCQ_17_filters_by_status_dentist_and_keyword()
    {
        using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(1), name: "Nguyễn Văn Xuyến");
        var b = await env.Appt(Now.AddHours(2), "Cancelled", "Người khác", env.Dentist2Emp);
        var c = await env.Appt(Now.AddHours(3), name: "Không tên", patient: env.Nam.Id);
        var s = env.Service();
        async Task<Guid[]> Ids(ReceptionQuery q) => (await s.AppointmentsAsync(env.Assistant, q, default)).Items.Select(x => x.AppointmentId).ToArray();
        Assert.Equal([b.Id], await Ids(env.Q(status: "Cancelled")));
        Assert.Equal([b.Id], await Ids(env.Q(dentist: env.Dentist2Emp)));
        Assert.Equal([a.Id], await Ids(env.Q(keyword: "  xuyến ")));       // ILIKE + trimmed
        Assert.Equal([a.Id], await Ids(env.Q(keyword: a.Code)));
        Assert.Equal([c.Id], await Ids(env.Q(keyword: env.Nam.FullName)));     // matches the linked patient
        Assert.Equal([c.Id], await Ids(env.Q(keyword: env.Nam.PatientCode)));
        Assert.Equal(3, (await Ids(env.Q(keyword: "   "))).Length);           // blank keyword = no filter
        Assert.Empty(await Ids(env.Q(keyword: "không-có")));
    }

    [Fact]
    public async Task Appointments_UT_RCQ_18_a_dentist_only_sees_own_appointments_whatever_the_filter()
    {
        using var env = await NewEnv();
        var own = await env.Appt(Now.AddHours(1)); await env.Appt(Now.AddHours(2), dentist: env.Dentist2Emp);
        var rows = (await env.Service().AppointmentsAsync(env.Dentist, env.Q(dentist: env.Dentist2Emp), default)).Items;
        Assert.Equal([own.Id], rows.Select(x => x.AppointmentId));
        Assert.False(rows[0].CanCheckIn); // only assistants can check in
    }

    [Fact]
    public async Task Appointments_UT_RCQ_19_dto_carries_patient_visit_and_check_in_eligibility()
    {
        using var env = await NewEnv();
        var plain = await env.Appt(Now.AddHours(1));
        var withVisit = await env.Appt(Now.AddHours(2), "CheckedIn", patient: env.Mai.Id); var visit = await env.VisitFor(withVisit);
        var items = (await env.Service().AppointmentsAsync(env.Assistant, env.Q(), default)).Items.ToDictionary(x => x.AppointmentId);
        var p = items[plain.Id];
        Assert.Equal((env.Branch, env.DentistEmp, 3, "Người đặt", null, true, null), (p.BranchId, p.DentistId, p.Version, p.BookerName, p.PatientCode, p.CanCheckIn, p.IneligibleReason));
        Assert.Null(p.VisitId);
        var v = items[withVisit.Id];
        Assert.Equal((env.Mai.PatientCode, "Trần Thị Mai", visit.Id, false, "Lịch không còn ở trạng thái đã xác nhận."), (v.PatientCode, v.PatientName, v.VisitId, v.CanCheckIn, v.IneligibleReason));
    }

    // Input: appointment state vs clock -> Expected: IneligibleReason text (null when eligible).
    [Fact]
    public async Task Appointments_UT_RCQ_20_ineligible_reasons()
    {
        using var env = await NewEnv();
        var ended = await env.Appt(Now.AddHours(-3), minutes: 60);                     // ended 2 h ago
        var justEnded = await env.Appt(Now.AddMinutes(-60), minutes: 60);              // EndAt == now -> ended (<=)
        var running = await env.Appt(Now.AddMinutes(-59), minutes: 60);                // ends in 1 minute -> still eligible
        var items = (await env.Service().AppointmentsAsync(env.Assistant, env.Q(), default)).Items.ToDictionary(x => x.AppointmentId);
        const string over = "Khoảng đặt lịch đã kết thúc. Vui lòng xếp lại lịch hợp lệ trước khi tiếp nhận.";
        Assert.Equal((over, over, null), (items[ended.Id].IneligibleReason, items[justEnded.Id].IneligibleReason, items[running.Id].IneligibleReason));
        Assert.Equal((false, false, true), (items[ended.Id].CanCheckIn, items[justEnded.Id].CanCheckIn, items[running.Id].CanCheckIn));
        // another clinic day: view tomorrow's list while the clock is today
        var tomorrow = await env.Appt(Now.AddDays(1));
        var other = (await env.Service().AppointmentsAsync(env.Assistant, env.Q(date: Today.AddDays(1)), default)).Items.Single(x => x.AppointmentId == tomorrow.Id);
        Assert.Equal(("Chỉ tiếp nhận lịch trong ngày hiện tại của phòng khám.", false), (other.IneligibleReason, other.CanCheckIn));
    }

    private static async Task Claim(Env env, int userId)
    {
        var users = CreateTestKit.NewRoleUserManager(env.Db);
        var user = (await users.FindByIdAsync(userId.ToString()))!; user.SecurityStamp ??= Guid.NewGuid().ToString("N");
        var result = await users.AddClaimAsync(user, new System.Security.Claims.Claim("permission", "reception.backdate"));
        Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(e => e.Description)));
    }

    // ================= DetailAsync =================
    [Fact]
    public async Task Detail_UT_RCQ_21_unknown_appointment_and_foreign_dentist()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().DetailAsync(env.Assistant, Guid.NewGuid(), default), 404, "MSG113");
        var theirs = await env.Appt(Now.AddHours(1), dentist: env.Dentist2Emp);
        await Rejects(() => env.Service().DetailAsync(env.Dentist, theirs.Id, default), 403, "MSG06");   // Own()
        Assert.Equal(theirs.Id, (await env.Service().DetailAsync(env.Assistant, theirs.Id, default)).Appointment.AppointmentId);
        await Rejects(() => env.Service().DetailAsync(env.ElsewhereAssistant, theirs.Id, default), 403, "MSG06");
    }

    [Fact]
    public async Task Detail_UT_RCQ_22_includes_the_visit_when_checked_in_and_backdate_permission_flag()
    {
        using var env = await NewEnv();
        var plain = await env.Appt(Now.AddHours(1));
        var d = await env.Service().DetailAsync(env.Assistant, plain.Id, default);
        Assert.Equal((null, false, Today, Now), (d.Visit, d.CanBackdate, d.ClinicDate, d.ServerNow));
        await Claim(env, env.Assistant);
        await env.Db.SaveChangesAsync();
        var booked = await env.Appt(Now.AddHours(2), "CheckedIn", patient: env.Mai.Id); var visit = await env.VisitFor(booked);
        var full = await env.Service().DetailAsync(env.Assistant, booked.Id, default);
        Assert.Equal((true, visit.Id, "Queued", $"/visits/{visit.Id}", 30), (full.CanBackdate, full.Visit!.VisitId, full.Visit.Status, full.Visit.OpenVisitPath, full.Visit.WaitingMinutes));
        // a dentist holding the claim cannot backdate (assistant roles only)
        await Claim(env, env.Dentist);
        await env.Db.SaveChangesAsync();
        Assert.False((await env.Service().DetailAsync(env.Dentist, plain.Id, default)).CanBackdate);
    }

    // ================= QueueAsync =================
    [Fact]
    public async Task Queue_UT_RCQ_23_lists_visits_by_arrival_with_waiting_minutes_only_while_queued()
    {
        using var env = await NewEnv();
        var a1 = await env.Appt(Now.AddHours(1)); var a2 = await env.Appt(Now.AddHours(2)); var a3 = await env.Appt(Now.AddHours(3));
        var late = await env.VisitFor(a1, VisitStatuses.Queued, Now.AddMinutes(-5));
        var first = await env.VisitFor(a2, VisitStatuses.Queued, Now.AddMinutes(-45).AddSeconds(-30));
        var busy = await env.VisitFor(a3, VisitStatuses.InProgress, Now.AddMinutes(-90));
        var all = await env.Service().QueueAsync(env.Assistant, env.Q(), default);
        Assert.Equal([busy.Id, first.Id, late.Id], all.Items.Select(x => x.VisitId));
        Assert.Equal([0, 45, 5], all.Items.Select(x => x.WaitingMinutes));      // fractional minutes truncate; non-queued = 0
        Assert.Equal(3, all.Total);
        var queued = await env.Service().QueueAsync(env.Assistant, env.Q(status: "Queued"), default);
        Assert.Equal([first.Id, late.Id], queued.Items.Select(x => x.VisitId));
        var future = await env.VisitFor(await env.Appt(Now.AddHours(4)), VisitStatuses.Queued, Now.AddMinutes(10)); // arrival after now -> waiting clamps at 0
        Assert.Equal(0, (await env.Service().QueueAsync(env.Assistant, env.Q(), default)).Items.Single(x => x.VisitId == future.Id).WaitingMinutes);
        var paged = await env.Service().QueueAsync(env.Assistant, env.Q(page: 2, size: 20), default);
        Assert.Empty(paged.Items);
    }

    [Fact]
    public async Task Queue_UT_RCQ_24_dentist_sees_own_queue_and_other_days_are_excluded()
    {
        using var env = await NewEnv();
        var own = await env.VisitFor(await env.Appt(Now.AddHours(1)));
        await env.VisitFor(await env.Appt(Now.AddHours(2), dentist: env.Dentist2Emp));
        await env.VisitFor(await env.Appt(Now.AddDays(1)));
        Assert.Equal([own.Id], (await env.Service().QueueAsync(env.Dentist, env.Q(dentist: env.Dentist2Emp), default)).Items.Select(x => x.VisitId));
        Assert.Equal(2, (await env.Service().QueueAsync(env.Assistant, env.Q(), default)).Total);
    }

    // ================= VisitAsync =================
    [Fact]
    public async Task Visit_UT_RCQ_25_unknown_visit_and_own_scope()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().VisitAsync(env.Assistant, Guid.NewGuid(), default), 404, "MSG113");
        var theirs = await env.VisitFor(await env.Appt(Now.AddHours(1), dentist: env.Dentist2Emp));
        await Rejects(() => env.Service().VisitAsync(env.Dentist, theirs.Id, default), 403, "MSG06");
        var result = await env.Service().VisitAsync(env.Assistant, theirs.Id, default);
        Assert.Equal((theirs.Id, env.Mai.PatientCode, false, "InitialExamination", "Người đặt".Length > 0), (result.VisitId, result.PatientCode, result.Replayed, result.Purpose, true));
        Assert.Equal(["reception.read"], env.Access.Asked);
    }

    [Theory]
    [InlineData("UT-RCQ-26 Có examinations.read", new[] { "examinations.read", "treatment.read" }, "examinations.read")]
    [InlineData("UT-RCQ-27 Chỉ có treatment.read", new[] { "treatment.read" }, "treatment.read")]
    [InlineData("UT-RCQ-28 Không có quyền lâm sàng nào", new string[0], "treatment.read")]
    public async Task Visit_clinical_mode_checks_the_clinical_permission_and_skips_the_own_dentist_rule(string caseName, string[] granted, string expectedPermission)
    {
        using var env = await NewEnv();
        var theirs = await env.VisitFor(await env.Appt(Now.AddHours(1), dentist: env.Dentist2Emp));
        var service = env.Service(permissions: granted);
        var result = await service.VisitAsync(env.Dentist, theirs.Id, default, clinical: true); // another dentist's visit is readable clinically
        Assert.Equal(theirs.Id, result.VisitId);
        Assert.Equal([expectedPermission], env.Access.Asked);
    }
    // ================= OptionsAsync =================
    [Fact]
    public async Task Options_UT_RCQ_29_assistant_gets_member_branches_intersected_with_granted_branches()
    {
        using var env = await NewEnv();
        var all = await env.Service().OptionsAsync(env.Assistant, default);
        Assert.Equal([env.Branch], all.Calendar.Branches.Select(x => x.Id));
        Assert.Equal([env.DentistEmp, env.Dentist2Emp], all.Calendar.Dentists.Select(x => x.Id));
        Assert.Equal([env.Branch], all.Calendar.Chairs.Select(x => x.BranchId));
        Assert.Equal([1], all.Calendar.Services.Select(x => x.Id));
        Assert.Equal((true, false, Today, Now), (all.CanCheckIn, all.Calendar.OwnScheduleOnly, all.ClinicDate, all.ServerNow));
        var none = await env.Service(branches: [env.OtherBranch]).OptionsAsync(env.Assistant, default); // member of Branch only -> intersection empty
        Assert.Empty(none.Calendar.Branches); Assert.Empty(none.Calendar.Dentists);
    }

    [Fact]
    public async Task Options_UT_RCQ_30_owner_sees_every_branch_of_the_calendar_options()
    {
        using var env = await NewEnv();
        var o = await env.Service(owner: true, branches: []).OptionsAsync(env.Assistant, default);
        Assert.Equal([env.Branch, env.OtherBranch], o.Calendar.Branches.Select(x => x.Id));
        Assert.Equal(3, o.Calendar.Dentists.Length);
    }

    [Fact]
    public async Task Options_UT_RCQ_31_dentist_only_gets_own_schedule()
    {
        using var env = await NewEnv();
        var o = await env.Service().OptionsAsync(env.Dentist, default);
        Assert.Equal([env.DentistEmp], o.Calendar.Dentists.Select(x => x.Id));
        Assert.Equal((true, false), (o.Calendar.OwnScheduleOnly, o.CanCheckIn));
    }

    [Fact]
    public async Task Options_UT_RCQ_32_rejects_unknown_customer_and_employee_less_accounts()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service().OptionsAsync(987654, default), 401, "MSG07");
        await Rejects(() => env.Service().OptionsAsync(env.Customer, default), 403, "MSG06");
        await Rejects(() => env.Service().OptionsAsync(env.NoEmployee, default), 403, "MSG06");
    }
}
