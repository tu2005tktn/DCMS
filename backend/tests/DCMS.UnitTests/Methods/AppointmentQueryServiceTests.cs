using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of AppointmentQueryService (read side of UC-05): OptionsAsync, CalendarAsync, ListAsync, ForPatientAsync, DetailAsync,
/// MineAsync, MineDetailAsync, ChangeDetailAsync and helpers (ActorAsync, ScopeAsync, Status, Range, Card, MaskPhone/MaskEmail, eligibility).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AppointmentQueryServiceTests
{
    // Now = 09:00 on 10/01/2030 clinic time (UTC+7).
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2030, 1, 10);

    private sealed class Access : ISettingsAccess
    {
        public bool Owner, Assigned; public int[] Branches = [];
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(Owner, Assigned, [], [], Branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int Branch, Other, Assistant, Dentist, Dentist2, Elsewhere, Customer, Customer2, Staffless, DentistEmp, Dentist2Emp, ChairId, ServiceId, PatientId;
        public readonly Access Access = new();
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public AppointmentQueryService Svc(DateTimeOffset? now = null, Dictionary<string, string?>? config = null)
        {
            Db.ChangeTracker.Clear();
            return new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(now ?? Now),
                new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build(), Access);
        }
        public async Task<Appointment> Appt(DateTimeOffset start, string status = "Confirmed", int? dentist = null, int? booker = null, int? patient = null, string name = "Trần Thị Mai",
            string contact = "0987654321", string relation = "Self", int minutes = 60, int? service = null, string? reason = null)
        {
            var a = new Appointment
            {
                Code = "LH" + CreateTestKit.NextId(), BranchId = Branch, DentistId = dentist ?? DentistEmp, ChairId = ChairId, BookerAccountId = booker ?? Customer, PatientId = patient,
                AttendeeName = name, StartAt = start, EndAt = start.AddMinutes(minutes), Status = status, Version = 3, CreatedAt = Now, ServiceId = service, Reason = reason,
                BookingInfo = new AppointmentBookingInfo { BookerName = "Người đặt", BookerContact = contact, AttendeeRelation = relation, ExceptionReason = "Ngoại lệ" }
            };
            Db.Appointments.Add(a); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return a;
        }
        public async Task SetMember(int employee, Action<SchedulingMember> change) { Db.ChangeTracker.Clear(); var branch = await Db.Set<SchedulingBranch>().SingleAsync(x => x.Id == Branch); change(branch.Members.Single(x => x.EmployeeId == employee)); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public async Task Mutate(int id, Action<ApplicationUser> change) { Db.ChangeTracker.Clear(); var u = await Db.Users.SingleAsync(x => x.Id == id); change(u); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
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
        var staffless = await CreateTestKit.SeedStaffAsync(db, "ne" + n, AccountRoles.ClinicAssistant, n, member: false);
        var customer = new ApplicationUser { UserName = "cu" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        var customer2 = new ApplicationUser { UserName = "cv" + n, FullName = "Khách 2", AccountType = "Public", IsActive = true };
        db.Users.AddRange(customer, customer2);
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI", BirthDate = new DateOnly(1985, 5, 5), CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        branch.Chairs.Add(new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        db.Set<SchedulingService>().Add(new SchedulingService { Id = n, BranchId = n, Name = "Khám tổng quát " + n, IsInitialExamination = true, PublicDurationMinutes = 30 });
        await db.SaveChangesAsync();
        var emp = async (ApplicationUser u) => (await db.Employees.SingleAsync(x => x.AccountId == u.Id)).Id;
        var env = new Env { Pg = pg, Branch = n, Other = other, Assistant = assistant.Id, Dentist = dentist.Id, Dentist2 = dentist2.Id, Elsewhere = elsewhere.Id, Staffless = staffless.Id,
            Customer = customer.Id, Customer2 = customer2.Id, DentistEmp = await emp(dentist), Dentist2Emp = await emp(dentist2), ChairId = n, ServiceId = n, PatientId = patient.Id };
        env.Access.Branches = [n, other];
        return env;
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= ActorAsync (shared by every staff method) =================
    [Fact]
    public async Task Options_UT_AQS_01_rejects_unknown_inactive_public_or_employee_less_accounts()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OptionsAsync(2147483000, default), 403, "MSG06");
        await Rejects(() => env.Svc().OptionsAsync(env.Customer, default), 403, "MSG06");           // not a staff account
        await Rejects(() => env.Svc().OptionsAsync(env.Staffless, default), 403, "MSG06");          // no employee profile
        await env.Mutate(env.Assistant, u => u.IsActive = false);
        await Rejects(() => env.Svc().OptionsAsync(env.Assistant, default), 403, "MSG06");
    }

    // ================= OptionsAsync =================
    [Fact]
    public async Task Options_UT_AQS_02_assistant_sees_branch_dentists_chairs_and_initial_examination_services()
    {
        await using var env = await NewEnv();
        var options = await env.Svc().OptionsAsync(env.Assistant, default);
        Assert.Equal([env.Branch], options.Branches.Select(x => x.Id));
        Assert.Equal(new[] { env.DentistEmp, env.Dentist2Emp }.Order(), options.Dentists.Select(x => x.Id).Intersect([env.DentistEmp, env.Dentist2Emp]).Order());
        Assert.Contains(options.Chairs, x => x.Id == env.ChairId);
        Assert.Contains(options.Services, x => x.Id == env.ServiceId && x.PublicDurationMinutes == 30);
        Assert.False(options.OwnScheduleOnly);
    }

    [Fact]
    public async Task Options_UT_AQS_03_dentist_sees_only_themselves_and_is_marked_own_schedule_only()
    {
        await using var env = await NewEnv();
        var options = await env.Svc().OptionsAsync(env.Dentist, default);
        Assert.Equal([env.DentistEmp], options.Dentists.Select(x => x.Id));
        Assert.True(options.OwnScheduleOnly);
        Assert.Equal(env.DentistEmp, options.CurrentDentistId);
    }

    [Fact]
    public async Task Options_UT_AQS_04_dynamic_scope_limits_branches_and_owner_sees_every_active_branch()
    {
        await using var env = await NewEnv();
        env.Access.Assigned = true; env.Access.Branches = [env.Other];
        await Rejects(() => env.Svc().OptionsAsync(env.Assistant, default), 403, "MSG06");           // assigned scope excludes the member branch
        env.Access.Owner = true;
        var options = await env.Svc().OptionsAsync(env.Assistant, default);
        Assert.Contains(options.Branches, x => x.Id == env.Branch);
        Assert.Contains(options.Branches, x => x.Id == env.Other);
    }

    [Fact]
    public async Task Options_UT_AQS_05_owner_with_no_active_branch_is_rejected()
    {
        await using var env = await NewEnv();
        env.Access.Owner = true;
        await env.Db.Set<SchedulingBranch>().ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, false));
        try { await Rejects(() => env.Svc().OptionsAsync(env.Assistant, default), 403, "MSG06"); }
        finally { await env.Db.Set<SchedulingBranch>().ExecuteUpdateAsync(s => s.SetProperty(x => x.Active, true)); }
    }

    [Fact]
    public async Task Options_UT_AQS_06_employee_without_any_active_membership_is_rejected()
    {
        await using var env = await NewEnv();
        await env.SetMember(env.DentistEmp, m => m.Active = false);
        await Rejects(() => env.Svc().OptionsAsync(env.Dentist, default), 403, "MSG06");
    }

    // ================= ScopeAsync via CalendarAsync / ListAsync / DetailAsync =================
    [Fact]
    public async Task Scope_UT_AQS_07_branch_outside_the_staff_membership_is_forbidden()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ListAsync(env.Elsewhere, new(env.Branch, Today, Today, null, null, 1, 20), default), 403, "MSG06");
        await Rejects(() => env.Svc().CalendarAsync(env.Elsewhere, env.Branch, Today, Today, null, null, default), 403, "MSG06");
    }

    [Fact]
    public async Task Scope_UT_AQS_08_dynamic_scope_and_owner_rules()
    {
        await using var env = await NewEnv();
        env.Access.Assigned = true; env.Access.Branches = [env.Other];
        await Rejects(() => env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today, null, null, 1, 20), default), 403, "MSG06");
        env.Access.Owner = true;                                                                // owner: branch must exist and be active, membership not needed
        Assert.NotNull(await env.Svc().ListAsync(env.Elsewhere, new(env.Branch, Today, Today, null, null, 1, 20), default));
        await Rejects(() => env.Svc().ListAsync(env.Assistant, new(2147483000, Today, Today, null, null, 1, 20), default), 403, "MSG06");
    }

    [Fact]
    public async Task Scope_UT_AQS_09_member_who_is_neither_dentist_nor_schedule_manager_is_forbidden()
    {
        await using var env = await NewEnv();
        await env.SetMember(env.DentistEmp, m => m.CanDentist = false);
        await Rejects(() => env.Svc().ListAsync(env.Dentist, new(env.Branch, Today, Today, null, null, 1, 20), default), 403, "MSG06");
        await env.SetMember(env.DentistEmp, m => m.CanManageSchedules = true);
        Assert.NotNull(await env.Svc().ListAsync(env.Dentist, new(env.Branch, Today, Today, null, null, 1, 20), default));   // schedule managers see the whole branch
    }

    // ================= CalendarAsync =================
    [Theory]
    [InlineData("UT-AQS-10 Đến ngày trước từ ngày", 0, -1)]
    [InlineData("UT-AQS-11 Khoảng 8 ngày", 0, 7)]
    public async Task Calendar_rejects_invalid_date_ranges(string caseName, int fromOffset, int toOffset)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().CalendarAsync(env.Assistant, env.Branch, Today.AddDays(fromOffset), Today.AddDays(toOffset), null, null, default), 400, "MSG04");
    }

    [Fact]
    public async Task Calendar_UT_AQS_12_returns_appointments_in_range_with_filters_and_published_periods()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(1), service: env.ServiceId);
        var b = await env.Appt(Now.AddHours(3), "Completed", env.Dentist2Emp);
        await env.Appt(Now.AddDays(30));                                                         // outside the range
        var monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        env.Db.Set<WorkingWeek>().Add(new WorkingWeek { BranchId = env.Branch, EmployeeId = env.DentistEmp, WeekStart = monday, PublishedBy = env.Assistant, PublishedAt = Now,
            Periods = [new WorkingPeriod { BranchId = env.Branch, Day = (int)(Today.DayNumber - monday.DayNumber), StartMinute = 480, EndMinute = 720, Kind = "Work" },
                       new WorkingPeriod { BranchId = env.Branch, Day = (int)(Today.DayNumber - monday.DayNumber), StartMinute = 720, EndMinute = 780, Kind = "Break", Reason = "Nghỉ trưa" }] });
        await env.Db.SaveChangesAsync();

        var view = await env.Svc().CalendarAsync(env.Assistant, env.Branch, Today, Today.AddDays(1), null, null, default);
        Assert.Equal(new[] { a.Id, b.Id }, view.Appointments.Select(x => x.Id).Intersect([a.Id, b.Id]));
        Assert.False(view.OwnScheduleOnly);
        var day = view.Days.Single(x => x.DentistId == env.DentistEmp && x.Date == Today);
        Assert.Equal([(480, 720, "Work", (string?)null), (720, 780, "Break", "Nghỉ trưa")], day.Periods.Select(x => (x.StartMinute, x.EndMinute, x.Kind, x.Reason)));
        Assert.Empty(view.Days.Single(x => x.DentistId == env.Dentist2Emp && x.Date == Today).Periods);

        var filtered = await env.Svc().CalendarAsync(env.Assistant, env.Branch, Today, Today, env.Dentist2Emp, "completed", default);
        Assert.Equal([b.Id], filtered.Appointments.Select(x => x.Id));
        Assert.Equal([env.Dentist2Emp], filtered.Days.Select(x => x.DentistId).Distinct());
        Assert.Equal("Khám tổng quát " + env.Branch, view.Appointments.Single(x => x.Id == a.Id).ServiceName);
    }

    [Fact]
    public async Task Calendar_UT_AQS_13_dentist_is_limited_to_their_own_appointments_and_unknown_status_is_rejected()
    {
        await using var env = await NewEnv();
        var mine = await env.Appt(Now.AddHours(1));
        await env.Appt(Now.AddHours(2), dentist: env.Dentist2Emp);
        var view = await env.Svc().CalendarAsync(env.Dentist, env.Branch, Today, Today, env.Dentist2Emp, "ALL", default);   // the dentist filter from the client is ignored
        Assert.Equal([mine.Id], view.Appointments.Select(x => x.Id).Intersect([mine.Id]));
        Assert.DoesNotContain(view.Appointments, x => x.DentistId == env.Dentist2Emp);
        Assert.True(view.OwnScheduleOnly);
        await Rejects(() => env.Svc().CalendarAsync(env.Assistant, env.Branch, Today, Today, null, "Maybe", default), 400, "MSG04");
    }

    // ================= ListAsync =================
    [Theory]
    [InlineData("UT-AQS-14 Trang 0", 0, 20, 0, "MSG04")]
    [InlineData("UT-AQS-15 Cỡ trang 10", 1, 10, 0, "MSG04")]
    [InlineData("UT-AQS-16 Khoảng ngày 62 ngày", 1, 20, 62, "MSG04")]
    [InlineData("UT-AQS-17 Đến ngày trước từ ngày", 1, 20, -1, "MSG04")]
    public async Task List_rejects_invalid_paging_and_ranges(string caseName, int page, int size, int days, string code)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today.AddDays(days), null, null, page, size), default), 400, code);
    }

    [Fact]
    public async Task List_UT_AQS_18_pages_in_start_order_and_filters_by_status_and_dentist()
    {
        await using var env = await NewEnv();
        var first = await env.Appt(Now.AddHours(1)); var second = await env.Appt(Now.AddHours(2), "NoShow", env.Dentist2Emp); var third = await env.Appt(Now.AddHours(3));
        var ids = new[] { first.Id, second.Id, third.Id };
        var all = await env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today.AddDays(61), null, null, 1, 100), default);
        Assert.Equal(ids, all.Items.Select(x => x.Id).Where(ids.Contains));
        Assert.Equal([second.Id], (await env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today, null, "noshow", 1, 20), default)).Items.Select(x => x.Id).Where(ids.Contains));
        Assert.Equal([first.Id, third.Id], (await env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today, env.DentistEmp, null, 1, 20), default)).Items.Select(x => x.Id).Where(ids.Contains));
        var page = await env.Svc().ListAsync(env.Assistant, new(env.Branch, Today, Today, env.DentistEmp, null, 2, 20), default);
        Assert.Equal((2, 20), (page.Page, page.PageSize));
        Assert.Equal("Trần Thị Mai", all.Items.First(x => x.Id == first.Id).AttendeeName);
    }

    // ================= ForPatientAsync =================
    [Theory]
    [InlineData("UT-AQS-19 Phạm vi lạ", "SOON", 1, 20)]
    [InlineData("UT-AQS-20 Trang 0", "ALL", 0, 20)]
    [InlineData("UT-AQS-21 Cỡ trang 7", "ALL", 1, 7)]
    public async Task ForPatient_rejects_invalid_arguments(string caseName, string scope, int page, int size)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ForPatientAsync(env.Assistant, env.PatientId, scope, page, size, default), 400, "MSG04");
    }

    [Fact]
    public async Task ForPatient_UT_AQS_22_unknown_patient_returns_404_MSG113()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ForPatientAsync(env.Assistant, 2147483000, "ALL", 1, 20, default), 404, "MSG113");
    }

    [Fact]
    public async Task ForPatient_UT_AQS_23_all_upcoming_and_history_scopes()
    {
        await using var env = await NewEnv();
        var past = await env.Appt(Now.AddDays(-2), "Completed", patient: env.PatientId);
        var cancelled = await env.Appt(Now.AddDays(1), "Cancelled", patient: env.PatientId);
        var soon = await env.Appt(Now.AddHours(5), patient: env.PatientId);
        var later = await env.Appt(Now.AddDays(3), patient: env.PatientId);
        await env.Appt(Now.AddDays(2), patient: null);                                            // another patient
        async Task<Guid[]> Ids(string scope, int? by = null) => (await env.Svc().ForPatientAsync(by ?? env.Assistant, env.PatientId, scope, 1, 20, default)).Items.Select(x => x.Id).ToArray();
        Assert.Equal(new[] { past.Id, cancelled.Id, soon.Id, later.Id }.OrderBy(x => x), (await Ids("ALL")).OrderBy(x => x));
        Assert.Equal([soon.Id, later.Id], await Ids("UPCOMING"));                                  // ascending by start
        Assert.Equal([cancelled.Id, past.Id], await Ids("HISTORY"));                               // descending by start; cancelled counts as history
        Assert.Equal([soon.Id, later.Id], await Ids("UPCOMING", env.Dentist));                    // the dentist sees only own appointments
        await Rejects(() => env.Svc().ForPatientAsync(env.Elsewhere, env.PatientId, "ALL", 1, 20, default), 403, "MSG06").ContinueWith(_ => { }, TaskContinuationOptions.None);
    }

    // ================= DetailAsync =================
    [Fact]
    public async Task Detail_UT_AQS_24_unknown_appointment_returns_404_and_another_dentists_appointment_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().DetailAsync(env.Assistant, Guid.NewGuid(), default), 404, "MSG113");
        var other = await env.Appt(Now.AddHours(1), dentist: env.Dentist2Emp);
        await Rejects(() => env.Svc().DetailAsync(env.Dentist, other.Id, default), 403, "MSG06");
    }

    [Theory]
    [InlineData("UT-AQS-25 Liên hệ là số điện thoại", "0987654321", "09******21", null)]
    [InlineData("UT-AQS-26 Liên hệ là email", "nam@gmail.com", null, "n**@gmail.com")]
    [InlineData("UT-AQS-27 Email có phần tên 1 ký tự", "a@x.vn", null, "***@x.vn")]
    [InlineData("UT-AQS-28 Số điện thoại quá ngắn không che", "123", null, null)]
    public async Task Detail_masks_the_booker_contact(string caseName, string contact, string? phone, string? email)
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(1), contact: contact, reason: "Đau răng");
        var detail = await env.Svc().DetailAsync(env.Assistant, a.Id, default);
        Assert.Equal((phone, email, true, "Đau răng", "Ngoại lệ"), (detail.BookerPhoneMasked, detail.BookerEmailMasked, detail.BookedFromApp, detail.Reason, detail.ExceptionReason));
        Assert.Equal(a.Code, detail.Appointment.Code);
    }

    [Fact]
    public async Task Detail_UT_AQS_29_walk_in_booking_without_an_account_is_not_from_the_app()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(1));
        await env.Db.Appointments.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.BookerAccountId, (int?)null));
        Assert.False((await env.Svc().DetailAsync(env.Assistant, a.Id, default)).BookedFromApp);
    }

    // ================= MineAsync / MineDetailAsync / ChangeDetailAsync (patient app) =================
    private static MyAppointmentQuery Mine(string scope = "UPCOMING", string? status = null, DateOnly? from = null, DateOnly? to = null, int page = 1, int size = 20) => new(scope, status, from, to, page, size);

    [Theory]
    [InlineData("UT-AQS-30 Trang 0", "UPCOMING", 0, 20)]
    [InlineData("UT-AQS-31 Cỡ trang 0", "UPCOMING", 1, 0)]
    [InlineData("UT-AQS-32 Cỡ trang 101", "UPCOMING", 1, 101)]
    [InlineData("UT-AQS-33 Nhóm thời gian lạ", "ALL", 1, 20)]
    public async Task Mine_rejects_invalid_arguments(string caseName, string scope, int page, int size)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().MineAsync(env.Customer, Mine(scope, page: page, size: size), default), 400, "MSG04");
    }

    [Fact]
    public async Task Mine_UT_AQS_34_from_after_to_is_rejected_and_only_public_active_accounts_are_allowed()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().MineAsync(env.Customer, Mine(from: Today.AddDays(1), to: Today), default), 400, "MSG04");
        await Rejects(() => env.Svc().MineAsync(env.Assistant, Mine(), default), 403, "MSG06");
        await Rejects(() => env.Svc().MineAsync(2147483000, Mine(), default), 403, "MSG06");
        await env.Mutate(env.Customer, u => u.IsActive = false);
        await Rejects(() => env.Svc().MineAsync(env.Customer, Mine(), default), 403, "MSG06");
    }

    [Fact]
    public async Task Mine_UT_AQS_35_lists_own_bookings_and_those_of_the_verified_linked_patient_with_the_right_access_label()
    {
        await using var env = await NewEnv();
        var booked = await env.Appt(Now.AddDays(1), relation: "Spouse", name: "Người thân");
        var own = await env.Appt(Now.AddDays(2), booker: env.Customer2, patient: env.PatientId);
        var both = await env.Appt(Now.AddDays(3), booker: env.Customer, patient: env.PatientId);
        var stranger = await env.Appt(Now.AddDays(4), booker: env.Customer2);
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { RequestCode = "PL" + CreateTestKit.NextId(), AccountId = env.Customer, PatientId = env.PatientId, DeclaredFullName = "Trần Thị Mai", Status = PatientLinkStatus.Verified, RequestedAt = Now });
        await env.Db.SaveChangesAsync();

        var page = await env.Svc().MineAsync(env.Customer, Mine(), default);

        var byCode = page.Items.ToDictionary(x => x.Code);
        Assert.Equal(("BOOKED_BY_ME", "RELATIVE"), (byCode[booked.Code].Access, byCode[booked.Code].AttendeeKind));
        Assert.Equal(("MY_OWN", "SELF"), (byCode[own.Code].Access, byCode[own.Code].AttendeeKind));
        Assert.Equal("BOTH", byCode[both.Code].Access);
        Assert.DoesNotContain(stranger.Code, byCode.Keys);
        Assert.Equal(60, byCode[booked.Code].DurationMinutes);
        Assert.Equal(3, byCode[booked.Code].Version);
    }

    [Fact]
    public async Task Mine_UT_AQS_36_upcoming_history_status_and_date_filters_with_paging()
    {
        await using var env = await NewEnv();
        var future = await env.Appt(Now.AddDays(1));
        var future2 = await env.Appt(Now.AddDays(2));
        var done = await env.Appt(Now.AddDays(-3), "Completed");
        var cancelled = await env.Appt(Now.AddDays(5), "Cancelled");
        var customer = env.Customer;
        Assert.Equal([future.Code, future2.Code], (await env.Svc().MineAsync(customer, Mine(), default)).Items.Select(x => x.Code));
        Assert.Equal([cancelled.Code, done.Code], (await env.Svc().MineAsync(customer, Mine("HISTORY"), default)).Items.Select(x => x.Code));
        Assert.Equal([done.Code], (await env.Svc().MineAsync(customer, Mine("HISTORY", "completed"), default)).Items.Select(x => x.Code));
        Assert.Equal([future2.Code], (await env.Svc().MineAsync(customer, Mine(from: Today.AddDays(2), to: Today.AddDays(2)), default)).Items.Select(x => x.Code));
        var second = await env.Svc().MineAsync(customer, Mine(page: 2, size: 1), default);
        Assert.Equal((2, 2, 1), (second.Total, second.Page, second.PageSize)); Assert.Equal([future2.Code], second.Items.Select(x => x.Code));
        await Rejects(() => env.Svc().MineAsync(customer, Mine(status: "Maybe"), default), 400, "MSG04");
    }

    [Fact]
    public async Task MineDetail_UT_AQS_37_unknown_or_foreign_code_returns_404_MSG102()
    {
        await using var env = await NewEnv();
        var foreign = await env.Appt(Now.AddDays(1), booker: env.Customer2);
        await Rejects(() => env.Svc().MineDetailAsync(env.Customer, "LH-NONE", default), 404, "MSG102");
        await Rejects(() => env.Svc().MineDetailAsync(env.Customer, foreign.Code, default), 404, "MSG102");
    }

    [Theory]
    [InlineData("UT-AQS-38 Còn hơn 12 giờ và đã xác nhận", "Confirmed", 30, null)]
    [InlineData("UT-AQS-39 Dưới 12 giờ", "Confirmed", 5, "WITHIN_12H")]
    [InlineData("UT-AQS-40 Đã đến giờ hẹn", "Confirmed", -1, "STARTED")]
    [InlineData("UT-AQS-41 Đã check-in", "CheckedIn", 30, "STARTED")]
    [InlineData("UT-AQS-42 Đang khám", "InProgress", 30, "STARTED")]
    [InlineData("UT-AQS-43 Lịch đã hủy", "Cancelled", 30, "NOT_CONFIRMED")]
    public async Task MineDetail_computes_the_self_service_eligibility(string caseName, string status, int hours, string? reason)
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(hours), status);
        var detail = await env.Svc().MineDetailAsync(env.Customer, a.Code, default);
        Assert.Equal((reason == null, reason, reason == "WITHIN_12H"), (detail.Eligibility.CanSelfService, detail.Eligibility.ReasonCode, detail.Eligibility.CanRequestSupport));
    }

    [Fact]
    public async Task MineDetail_UT_AQS_44_returns_branch_service_masked_contact_history_support_requests_and_clinic_contact()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(30), service: env.ServiceId);
        env.Db.AppointmentChanges.Add(new AppointmentChange { AppointmentId = a.Id, ActorAccountId = env.Customer, Action = "Reschedule", OldStart = a.StartAt.AddDays(-1), OldEnd = a.EndAt.AddDays(-1), NewStart = a.StartAt, NewEnd = a.EndAt, OccurredAt = Now, Reason = "Bận" });
        var tracked = await env.Db.Appointments.SingleAsync(x => x.Id == a.Id);
        tracked.SupportRequests.Add(new AppointmentSupportRequest { Code = "YC" + CreateTestKit.NextId(), AppointmentId = a.Id, Kind = "Change", Status = "Pending", RequesterName = "Khách", Reason = "Đổi giờ", CreatedAt = Now });
        tracked.SupportRequests.Add(new AppointmentSupportRequest { Code = "YC" + CreateTestKit.NextId(), AppointmentId = a.Id, Kind = "Cancel", Status = "Pending", RequesterName = "Khách", CreatedAt = Now.AddMinutes(-1) });
        await env.Db.SaveChangesAsync();

        var detail = await env.Svc().MineDetailAsync(env.Customer, a.Code, default);

        Assert.Equal(("Khám tổng quát " + env.Branch, "09******21", "Người đặt"), (detail.ServiceName, detail.BookerContactMasked, detail.BookerName));
        Assert.StartsWith("Branch", "Branch" + detail.BranchName);
        Assert.Equal(2, detail.SupportRequests.Count);
        Assert.Equal("Reschedule", Assert.Single(detail.History).Action);
        Assert.Equal(("0234 3 888 999", "Thứ 2 – Thứ 7, 08:00 – 17:00"), (detail.ClinicContact.Phone, detail.ClinicContact.Hours));
        var custom = await env.Svc(config: new() { ["Clinic:Phone"] = "1900", ["Clinic:Hours"] = "24/7" }).MineDetailAsync(env.Customer, a.Code, default);
        Assert.Equal(("1900", "24/7"), (custom.ClinicContact.Phone, custom.ClinicContact.Hours));
    }

    [Fact]
    public async Task MineDetail_UT_AQS_45_appointment_without_a_service_uses_the_default_name_and_email_contact_is_masked()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(30), contact: "khach@mail.vn");
        var detail = await env.Svc().MineDetailAsync(env.Customer, a.Code, default);
        Assert.Equal(("Khám và tư vấn", "k****@mail.vn"), (detail.ServiceName, detail.BookerContactMasked));
    }

    [Fact]
    public async Task ChangeDetail_UT_AQS_46_customers_use_the_own_detail_and_staff_get_the_staff_access_label()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(Now.AddHours(30));
        Assert.Equal("BOOKED_BY_ME", (await env.Svc().ChangeDetailAsync(env.Customer, a.Code, default)).Access);
        Assert.Equal("STAFF", (await env.Svc().ChangeDetailAsync(env.Assistant, a.Code, default)).Access);
        await Rejects(() => env.Svc().ChangeDetailAsync(env.Assistant, "LH-NONE", default), 404, "MSG102");
        await Rejects(() => env.Svc().ChangeDetailAsync(env.Elsewhere, a.Code, default), 403, "MSG06");
    }

    [Fact]
    public async Task ChangeDetail_UT_AQS_47_a_dentist_cannot_see_another_dentists_appointment()
    {
        await using var env = await NewEnv();
        var other = await env.Appt(Now.AddHours(30), dentist: env.Dentist2Emp);
        await Rejects(() => env.Svc().ChangeDetailAsync(env.Dentist, other.Code, default), 404, "MSG102");
        var own = await env.Appt(Now.AddHours(30));
        Assert.Equal("STAFF", (await env.Svc().ChangeDetailAsync(env.Dentist, own.Code, default)).Access);
    }
}
