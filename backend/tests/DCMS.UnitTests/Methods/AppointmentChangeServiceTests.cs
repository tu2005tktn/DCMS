using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level (white-box) tests of AppointmentChangeService: one section per method, every if/throw branch has a case.
/// Sections: AvailabilityAsync, AuthorizeAsync (private, reached through the public methods), RescheduleAsync,
/// CancelAsync (both share ChangeAsync), MarkNoShowAsync. Message codes are asserted by code, key texts by literal.
/// </summary>
public sealed class AppointmentChangeServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Key = new("11111111-1111-1111-1111-111111111111");
    private const string Code = "LH-0001";

    // ---------- test doubles ----------
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class BranchAccess(params int[] branchIds) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], branchIds));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAvailability : IAvailabilityService
    {
        public Func<ResourceCheck, AvailabilitySlot>? OnCheck;
        public ResourceCheck? LastCheck;
        public AvailabilitySlot[] Slots = [];
        public AvailabilityQuery? LastQuery;
        public Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct) => throw new NotSupportedException();
        public Task<AvailabilityResult> QueryForAppointmentAsync(AvailabilityQuery query, Guid appointmentId, int? patientId, CancellationToken ct, bool requireLeadTime = true)
        {
            LastQuery = query;
            return Task.FromResult(new AvailabilityResult("SE Asia Standard Time", query.Date, Slots));
        }
        public Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct)
        {
            LastCheck = request;
            return Task.FromResult(OnCheck != null ? OnCheck(request)
                : new AvailabilitySlot(request.Start, request.Start.AddMinutes(request.DurationMinutes), request.ChairId ?? 1, request.DentistId, null, request.ExpectedScheduleVersion, null));
        }
        public Task ValidateReceptionAsync(Guid appointmentId, int patientId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeQueries : IAppointmentQueryService
    {
        public int[] DentistIds = [1, 2];
        public Task<AppointmentOptions> OptionsAsync(int actorId, CancellationToken ct) => Task.FromResult(new AppointmentOptions([],
            DentistIds.Select(x => new CalendarDentist(x, 1, "BS " + x)).ToArray(), [], [], false));
        public Task<StaffAppointmentDetail> DetailAsync(int actorId, Guid id, CancellationToken ct) => Task.FromResult<StaffAppointmentDetail>(null!);
        public Task<MyAppointmentDetail> ChangeDetailAsync(int accountId, string code, CancellationToken ct) => Task.FromResult(new MyAppointmentDetail(
            code, Now, Now, 30, "BS 1", "Khách", "Self", "Confirmed", "Booker", 2, 1, "Chi nhánh 1", 1, null, "", "Khách", "***",
            new MyEligibility(true, 48, null, true), [], [], new MyClinicContact("", "")));
        public Task<CalendarView> CalendarAsync(int actorId, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<AppointmentListPage> ListAsync(int actorId, StaffAppointmentQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<AppointmentListPage> ForPatientAsync(int actorId, int patientId, string scope, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<MyAppointmentPage> MineAsync(int accountId, MyAppointmentQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<MyAppointmentDetail> MineDetailAsync(int accountId, string code, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Env : IDisposable
    {
        public required DcmsDbContext Db;
        public required AppointmentChangeService Service;
        public required FakeAvailability Availability;
        public required FakeQueries Queries;
        public required int Assistant, Dentist, Customer, Stranger;
        public void Dispose() => Db.Dispose();

        public Appointment Appointment(string code = Code) => Db.Appointments.Single(x => x.Code == code);
        public Task<int> Count<T>() where T : class => Db.Set<T>().CountAsync();
    }

    /// <summary>Appointment: confirmed, starts in 48 h (Now + 2 days), 30 min, branch 1, dentist 1, chair 1, booked by Customer.</summary>
    private static async Task<Env> NewEnv(Action<Appointment>? tune = null, int[]? branches = null)
    {
        var db = CreateTestKit.NewSqliteDb();
        db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF"); // unit test: only the service logic, not DB constraints
        foreach (var role in AccountRoles.Staff)
            db.Set<DCMS.Domain.Features.Settings.AccessGroup>().Add(new() { RecordKind = "IdentityRole", IdentityRoleId = Array.IndexOf(AccountRoles.Staff, role) + 1, Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "assistant", AccountRoles.ClinicAssistant, 1);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dentist", AccountRoles.Dentist, 1);
        var customer = new ApplicationUser { UserName = "customer", FullName = "Khách", AccountType = "Public", IsActive = true };
        var stranger = new ApplicationUser { UserName = "stranger", FullName = "Người lạ", AccountType = "Public", IsActive = true };
        db.Users.AddRange(customer, stranger);
        await db.SaveChangesAsync();
        var appointment = new Appointment
        {
            Code = Code, BranchId = 1, DentistId = 1, ChairId = 1, BookerAccountId = customer.Id, AttendeeName = "Khách",
            StartAt = Now.AddHours(48), EndAt = Now.AddHours(48).AddMinutes(30), Status = "Confirmed", Version = 1, CreatedAt = Now
        };
        tune?.Invoke(appointment);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        var availability = new FakeAvailability();
        var queries = new FakeQueries();
        var service = new AppointmentChangeService(db, availability, queries, CreateTestKit.NewRoleUserManager(db),
            new FixedClock(Now), new BranchAccess(branches ?? [1]));
        return new Env { Db = db, Service = service, Availability = availability, Queries = queries,
            Assistant = assistant.Id, Dentist = dentist.Id, Customer = customer.Id, Stranger = stranger.Id };
    }

    private static CancelAppointmentRequest Cancel(int version = 1, Guid? key = null, string? reason = "Bận việc đột xuất",
        string? verification = null, bool clinic = false) => new(version, key ?? Key, reason, verification, clinic);

    private static RescheduleAppointmentRequest Move(int version = 1, Guid? key = null, DateTimeOffset? start = null, int chair = 2,
        int scheduleVersion = 1, int? assistantVersion = null, string? reason = "Đổi lịch", string? verification = null,
        int? dentistId = null, bool clinic = false) =>
        new(version, key ?? Key, start ?? Now.AddHours(72), chair, scheduleVersion, assistantVersion, reason, verification, dentistId, null, clinic);

    private static async Task Rejects(Func<Task> action, int status, string code, string? message = null)
    {
        var error = await CreateTestKit.ErrorOf(action);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
    }

    // =====================================================================================
    // 1. AvailabilityAsync(actorId, code, date, dentistId)
    // =====================================================================================
    public static TheoryData<string, string, int, int, int, int, string> AvailabilityInvalidInput => new()
    {
        { "UT-AVL-01 Mã lịch rỗng", "", 2030, 1, 12, 400, "MSG04" },
        { "UT-AVL-02 Mã lịch dài 41 ký tự", new string('A', 41), 2030, 1, 12, 400, "MSG04" },
        { "UT-AVL-03 Năm 1999 (dưới 2000)", Code, 1999, 1, 12, 400, "MSG04" },
        { "UT-AVL-04 Năm 2101 (trên 2100)", Code, 2101, 1, 12, 400, "MSG04" },
    };

    [Theory, MemberData(nameof(AvailabilityInvalidInput))]
    public async Task Availability_rejects_invalid_input(string caseName, string code, int year, int month, int day, int status, string msg)
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.AvailabilityAsync(env.Assistant, code, new DateOnly(year, month, day), null, default), status, msg);
    }

    [Fact]
    public async Task Availability_UT_AVL_05_boundary_year_2000_and_2100_are_valid()
    {
        using var env = await NewEnv();
        await env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2000, 1, 1), null, default);
        await env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2100, 12, 31), null, default);
    }

    [Fact]
    public async Task Availability_UT_AVL_06_unknown_actor_is_MSG07()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.AvailabilityAsync(9999, Code, new DateOnly(2030, 1, 12), null, default), 401, "MSG07");
    }

    [Theory]
    [InlineData("UT-AVL-07 Lịch đã hủy", "Cancelled", 48)]
    [InlineData("UT-AVL-08 Lịch đã bắt đầu từ hôm trước", "Confirmed", -24)]
    [InlineData("UT-AVL-09 Lịch đã qua nhiều ngày", "Confirmed", -48)]
    public async Task Availability_rejects_appointment_that_cannot_be_changed(string caseName, string status, int startHours)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(startHours); a.EndAt = a.StartAt.AddMinutes(30); });
        await Rejects(() => env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2030, 1, 12), null, default), 409, "MSG31");
    }

    [Fact]
    public async Task Availability_UT_AVL_10_dentist_id_zero_is_MSG04()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2030, 1, 12), 0, default), 400, "MSG04");
    }

    [Fact]
    public async Task Availability_UT_AVL_11_same_dentist_hides_the_current_slot_and_uses_appointment_duration()
    {
        using var env = await NewEnv();
        var current = Now.AddHours(48);
        env.Availability.Slots = [new(current, current.AddMinutes(30), 1, 1, null, 1, null), new(current.AddHours(1), current.AddHours(1).AddMinutes(30), 1, 1, null, 1, null)];
        var result = await env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2030, 1, 12), null, default);
        Assert.Single(result.Slots);
        Assert.Equal(current.AddHours(1), result.Slots[0].Start);
        Assert.Equal(30, env.Availability.LastQuery!.DurationMinutes);
        Assert.Equal(1, env.Availability.LastQuery.DentistId);
    }

    [Fact]
    public async Task Availability_UT_AVL_12_other_dentist_keeps_every_slot()
    {
        using var env = await NewEnv();
        var current = Now.AddHours(48);
        env.Availability.Slots = [new(current, current.AddMinutes(30), 1, 2, null, 1, null), new(current.AddHours(1), current.AddHours(1).AddMinutes(30), 1, 2, null, 1, null)];
        var result = await env.Service.AvailabilityAsync(env.Assistant, Code, new DateOnly(2030, 1, 12), 2, default);
        Assert.Equal(2, result.Slots.Length);
        Assert.Equal(2, env.Availability.LastQuery!.DentistId);
    }

    // =====================================================================================
    // 2. AuthorizeAsync (private) — reached through CancelAsync (non clinic-initiated) and MarkNoShowAsync (clinic-initiated)
    // =====================================================================================
    [Fact]
    public async Task Authorize_UT_AUT_01_unknown_user_is_MSG07()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(9999, Code, Cancel(), default), 401, "MSG07",
            "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    }

    [Fact]
    public async Task Authorize_UT_AUT_02_inactive_user_is_MSG07()
    {
        using var env = await NewEnv();
        (await env.Db.Users.FindAsync(env.Customer))!.IsActive = false;
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(), default), 401, "MSG07");
    }

    [Fact]
    public async Task Authorize_UT_AUT_03_locked_user_is_MSG07()
    {
        using var env = await NewEnv();
        var user = (await env.Db.Users.FindAsync(env.Customer))!;
        user.LockoutEnabled = true; user.LockoutEnd = Now.AddHours(1);
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(), default), 401, "MSG07");
    }

    [Fact]
    public async Task Authorize_UT_AUT_04_expired_lockout_is_allowed()
    {
        using var env = await NewEnv();
        var user = (await env.Db.Users.FindAsync(env.Customer))!;
        user.LockoutEnabled = true; user.LockoutEnd = Now.AddHours(-1);
        await env.Db.SaveChangesAsync();
        await env.Service.CancelAsync(env.Customer, Code, Cancel(), default);
    }

    [Fact]
    public async Task Authorize_UT_AUT_05_account_type_other_than_staff_or_public_is_MSG06()
    {
        using var env = await NewEnv();
        (await env.Db.Users.FindAsync(env.Customer))!.AccountType = "Admin";
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(), default), 403, "MSG06");
    }

    [Fact]
    public async Task Authorize_UT_AUT_06_staff_without_assistant_role_cannot_do_a_customer_requested_change()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Dentist, Code, Cancel(verification: "CCCD"), default), 403, "MSG06");
    }

    [Fact]
    public async Task Authorize_UT_AUT_07_unknown_appointment_code_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Customer, "LH-9999", Cancel(), default), 404, "MSG102");
    }

    [Fact]
    public async Task Authorize_UT_AUT_08_staff_outside_the_appointment_branch_is_MSG102()
    {
        using var env = await NewEnv(branches: [2]);
        await Rejects(() => env.Service.CancelAsync(env.Assistant, Code, Cancel(verification: "CCCD"), default), 404, "MSG102");
    }

    [Fact]
    public async Task Authorize_UT_AUT_09_public_user_who_is_not_the_booker_and_has_no_link_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Stranger, Code, Cancel(), default), 404, "MSG102");
    }

    [Fact]
    public async Task Authorize_UT_AUT_10_public_user_with_a_verified_patient_link_is_allowed()
    {
        using var env = await NewEnv(a => a.PatientId = 7);
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = env.Stranger, PatientId = 7, Status = PatientLinkStatus.Verified, RequestCode = "LK-1" });
        await env.Db.SaveChangesAsync();
        var result = await env.Service.CancelAsync(env.Stranger, Code, Cancel(), default);
        Assert.Equal("MSG37", result.Notification.Code);
    }

    [Fact]
    public async Task Authorize_UT_AUT_11_public_user_with_a_pending_link_is_MSG102()
    {
        using var env = await NewEnv(a => a.PatientId = 7);
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = env.Stranger, PatientId = 7, Status = PatientLinkStatus.Pending, RequestCode = "LK-1" });
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CancelAsync(env.Stranger, Code, Cancel(), default), 404, "MSG102");
    }

    // =====================================================================================
    // 3. CancelAsync -> ChangeAsync (action CANCEL)
    // =====================================================================================
    public static TheoryData<string, Guid, int, string, string?, string?, int, string, string> CancelInvalidInput => new()
    {
        { "UT-CAN-01 RequestKey rỗng", Guid.Empty, 1, Code, "Bận", null, 400, "MSG04", "Thông tin không đúng định dạng." },
        { "UT-CAN-02 ExpectedVersion = 0", Key, 0, Code, "Bận", null, 400, "MSG04", "Thông tin không đúng định dạng." },
        { "UT-CAN-03 Mã lịch rỗng", Key, 1, "", "Bận", null, 400, "MSG04", "Thông tin không đúng định dạng." },
        { "UT-CAN-04 Mã lịch dài 41 ký tự", Key, 1, "L123456789L123456789L123456789L123456789L", "Bận", null, 400, "MSG04", "Thông tin không đúng định dạng." },
        { "UT-CAN-05 Lý do null", Key, 1, Code, null, null, 400, "MSG02", "Vui lòng nhập Lý do." },
        { "UT-CAN-06 Lý do toàn khoảng trắng", Key, 1, Code, "   ", null, 400, "MSG02", "Vui lòng nhập Lý do." },
        { "UT-CAN-07 Lý do 501 ký tự", Key, 1, Code, "a", null, 400, "MSG03", "Lý do không được vượt quá 500 ký tự." },
        { "UT-CAN-08 Xác minh 501 ký tự", Key, 1, Code, "Bận", "x", 400, "MSG03", "Xác minh người yêu cầu không được vượt quá 500 ký tự." },
    };

    [Theory, MemberData(nameof(CancelInvalidInput))]
    public async Task Cancel_rejects_invalid_input(string caseName, Guid key, int version, string code, string? reason, string? verification,
        int status, string msg, string text)
    {
        using var env = await NewEnv();
        if (caseName.Contains("UT-CAN-07")) reason = new string('a', 501);
        if (caseName.Contains("UT-CAN-08")) verification = new string('x', 501);
        await Rejects(() => env.Service.CancelAsync(env.Assistant, code, Cancel(version, key, reason, verification), default), status, msg, text);
        Assert.Equal(0, await env.Count<AppointmentChange>());
    }

    [Fact]
    public async Task Cancel_UT_CAN_09_reason_of_exactly_500_characters_is_accepted()
    {
        using var env = await NewEnv();
        await env.Service.CancelAsync(env.Customer, Code, Cancel(reason: new string('a', 500)), default);
        Assert.Equal("Cancelled", env.Appointment().Status);
    }

    [Fact]
    public async Task Cancel_UT_CAN_10_non_clinic_staff_must_give_the_requester_verification()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Assistant, Code, Cancel(verification: null), default), 400, "MSG02",
            "Vui lòng nhập Xác minh người yêu cầu.");
    }

    [Fact]
    public async Task Cancel_UT_CAN_11_public_user_cannot_use_the_clinic_initiated_flag()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(clinic: true), default), 403, "MSG06");
    }

    [Fact]
    public async Task Cancel_UT_CAN_12_version_mismatch_is_MSG111()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(version: 5), default), 409, "MSG111",
            "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.");
    }

    [Theory]
    [InlineData("UT-CAN-13 Khách: lịch đã hủy", "Cancelled", 48)]
    [InlineData("UT-CAN-14 Khách: lịch đã hoàn thành", "Completed", 48)]
    [InlineData("UT-CAN-15 Khách: còn 11 giờ (dưới 12 giờ)", "Confirmed", 11)]
    public async Task Cancel_public_user_is_rejected_when_not_self_service_eligible(string caseName, string status, int startHours)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(startHours); a.EndAt = a.StartAt.AddMinutes(30); });
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(), default), 409, "MSG31",
            "Không thể tự đổi hoặc hủy lịch này. Vui lòng liên hệ phòng khám để được hỗ trợ.");
    }

    [Fact]
    public async Task Cancel_UT_CAN_16_public_boundary_exactly_12_hours_is_allowed()
    {
        using var env = await NewEnv(a => { a.StartAt = Now.AddHours(12); a.EndAt = a.StartAt.AddMinutes(30); });
        var result = await env.Service.CancelAsync(env.Customer, Code, Cancel(), default);
        Assert.Equal("MSG37", result.Notification.Code);
    }

    [Fact]
    public async Task Cancel_UT_CAN_17_staff_may_cancel_a_future_appointment_inside_12_hours()
    {
        using var env = await NewEnv(a => { a.StartAt = Now.AddHours(2); a.EndAt = a.StartAt.AddMinutes(30); });
        await env.Service.CancelAsync(env.Assistant, Code, Cancel(verification: "Khách gọi điện, đã xác minh SĐT"), default);
        Assert.Equal("Cancelled", env.Appointment().Status);
        Assert.Equal("STAFF", env.Db.AppointmentChanges.Single().ActorKind);
    }

    [Theory]
    [InlineData("UT-CAN-18 Nhân viên: lịch đã bắt đầu (StartAt = hiện tại)", "Confirmed", 0)]
    [InlineData("UT-CAN-19 Nhân viên: lịch đã hủy", "Cancelled", 48)]
    public async Task Cancel_staff_is_rejected_when_appointment_is_not_a_future_confirmed_one(string caseName, string status, int startHours)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(startHours); a.EndAt = a.StartAt.AddMinutes(30); });
        await Rejects(() => env.Service.CancelAsync(env.Assistant, Code, Cancel(verification: "CCCD"), default), 409, "MSG31");
    }

    [Fact]
    public async Task Cancel_UT_CAN_20_clinic_initiated_staff_does_not_need_the_assistant_role_or_verification()
    {
        using var env = await NewEnv();
        await env.Service.CancelAsync(env.Dentist, Code, Cancel(clinic: true), default);
        Assert.Equal("Phòng khám chủ động điều chỉnh lịch hẹn.", env.Db.AppointmentChanges.Single().RequesterVerification);
    }

    [Fact]
    public async Task Cancel_UT_CAN_21_success_updates_appointment_and_writes_change_outbox_and_receipt()
    {
        using var env = await NewEnv();
        var result = await env.Service.CancelAsync(env.Customer, Code, Cancel(reason: "  Bận việc đột xuất  "), default);

        var appointment = env.Appointment();
        Assert.Equal("Cancelled", appointment.Status);
        Assert.Equal(2, appointment.Version);
        Assert.Equal("MSG37", result.Notification.Code);
        Assert.Equal("Đã hủy lịch hẹn LH-0001.", result.Notification.Message);
        var change = env.Db.AppointmentChanges.Single();
        Assert.Equal("CANCEL", change.Action);
        Assert.Equal("SELF", change.ActorKind);
        Assert.Equal("Bận việc đột xuất", change.Reason);
        Assert.Null(change.NewStart);
        Assert.Equal(Now.AddHours(48), change.OldStart);
        Assert.Equal("appointment.cancelled", env.Db.AppointmentOutboxEvents.Single().EventType);
        Assert.Equal(1, await env.Count<AppointmentChangeReceipt>());
    }

    [Fact]
    public async Task Cancel_UT_CAN_22_retry_with_same_key_and_body_replays_the_first_result_without_a_second_change()
    {
        using var env = await NewEnv();
        var first = await env.Service.CancelAsync(env.Customer, Code, Cancel(), default);
        var second = await env.Service.CancelAsync(env.Customer, Code, Cancel(), default);
        Assert.Equal(first.Notification, second.Notification);
        Assert.Equal(1, await env.Count<AppointmentChange>());
        Assert.Equal(2, env.Appointment().Version);
    }

    [Fact]
    public async Task Cancel_UT_CAN_23_same_key_with_another_reason_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.CancelAsync(env.Customer, Code, Cancel(reason: "Lý do A"), default);
        await Rejects(() => env.Service.CancelAsync(env.Customer, Code, Cancel(reason: "Lý do B"), default), 409, "MSG111");
    }

    // =====================================================================================
    // 4. RescheduleAsync -> ChangeAsync (action RESCHEDULE)
    // =====================================================================================
    [Theory]
    [InlineData("UT-RES-01 NewStart chưa chọn (default)", true, 2, 1, null)]
    [InlineData("UT-RES-02 ChairId = 0", false, 0, 1, null)]
    [InlineData("UT-RES-03 ScheduleVersion = 0", false, 2, 0, null)]
    [InlineData("UT-RES-04 AssistantScheduleVersion = 0", false, 2, 1, 0)]
    public async Task Reschedule_rejects_invalid_move_fields(string caseName, bool noStart, int chair, int scheduleVersion, int? assistantVersion)
    {
        using var env = await NewEnv();
        var request = Move(start: noStart ? default(DateTimeOffset) : null, chair: chair, scheduleVersion: scheduleVersion, assistantVersion: assistantVersion);
        if (noStart) request = request with { NewStart = default };
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, request, default), 400, "MSG04");
    }

    [Fact]
    public async Task Reschedule_UT_RES_05_missing_reason_is_MSG02()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(reason: ""), default), 400, "MSG02", "Vui lòng nhập Lý do.");
    }

    [Fact]
    public async Task Reschedule_UT_RES_06_clinic_initiated_to_a_dentist_outside_the_branch_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.RescheduleAsync(env.Assistant, Code, Move(dentistId: 99, clinic: true), default), 403, "MSG06");
    }

    [Fact]
    public async Task Reschedule_UT_RES_07_dentist_id_zero_is_MSG34()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(dentistId: 0), default), 409, "MSG34",
            "Không thể đổi sang khung giờ đã chọn. Lịch hẹn cũ của bạn vẫn được giữ nguyên.");
    }

    [Theory]
    [InlineData("UT-RES-08 Giờ mới cách hiện tại 59 phút (dưới 60 phút)", 59)]
    [InlineData("UT-RES-09 Giờ mới trong quá khứ", -30)]
    public async Task Reschedule_rejects_a_new_start_inside_the_lead_time(string caseName, int minutesFromNow)
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(start: Now.AddMinutes(minutesFromNow)), default), 409, "MSG34");
    }

    [Fact]
    public async Task Reschedule_UT_RES_10_boundary_exactly_60_minutes_passes_the_lead_time_check()
    {
        using var env = await NewEnv();
        await env.Service.RescheduleAsync(env.Customer, Code, Move(start: Now.AddMinutes(60)), default);
        Assert.Equal(Now.AddMinutes(60), env.Appointment().StartAt);
    }

    [Fact]
    public async Task Reschedule_UT_RES_11_same_time_chair_and_dentist_is_MSG34()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(start: Now.AddHours(48), chair: 1), default), 409, "MSG34");
    }

    [Theory]
    [InlineData("UT-RES-12 Cùng giờ, khác ghế", 48, 2, null)]
    [InlineData("UT-RES-13 Cùng giờ và ghế, khác bác sĩ", 48, 1, 2)]
    [InlineData("UT-RES-14 Khác giờ, cùng ghế và bác sĩ", 72, 1, null)]
    public async Task Reschedule_changing_any_one_of_time_chair_or_dentist_is_a_real_move(string caseName, int hours, int chair, int? dentist)
    {
        using var env = await NewEnv();
        var result = await env.Service.RescheduleAsync(env.Customer, Code, Move(start: Now.AddHours(hours), chair: chair, dentistId: dentist), default);
        Assert.Equal("MSG35", result.Notification.Code);
    }

    [Fact]
    public async Task Reschedule_UT_RES_15_availability_rejection_becomes_MSG34()
    {
        using var env = await NewEnv();
        env.Availability.OnCheck = _ => throw new BusinessException(409, SystemMessages.Get("MSG25"));
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(), default), 409, "MSG34");
        Assert.Equal(1, env.Appointment().Version);
        Assert.Equal(0, await env.Count<AppointmentChange>());
    }

    [Fact]
    public async Task Reschedule_UT_RES_16_schedule_version_conflict_MSG111_is_passed_through()
    {
        using var env = await NewEnv();
        env.Availability.OnCheck = _ => throw new BusinessException(409, SystemMessages.Get("MSG111"));
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(), default), 409, "MSG111");
    }

    [Fact]
    public async Task Reschedule_UT_RES_17_success_moves_the_appointment_and_records_old_and_new_values()
    {
        using var env = await NewEnv();
        var newStart = Now.AddHours(72);
        var result = await env.Service.RescheduleAsync(env.Customer, Code, Move(start: newStart, chair: 2, dentistId: 2), default);

        var appointment = env.Appointment();
        Assert.Equal(newStart, appointment.StartAt);
        Assert.Equal(newStart.AddMinutes(30), appointment.EndAt);
        Assert.Equal(2, appointment.ChairId);
        Assert.Equal(2, appointment.DentistId);
        Assert.Equal(2, appointment.Version);
        Assert.Equal("MSG35", result.Notification.Code);
        Assert.Equal("Đã đổi lịch hẹn sang 09:00 13/01/2030. Mọi thông báo nhắc lịch sẽ theo giờ hẹn mới.", result.Notification.Message);
        var change = env.Db.AppointmentChanges.Single();
        Assert.Equal("RESCHEDULE", change.Action);
        Assert.Equal(Now.AddHours(48), change.OldStart);
        Assert.Equal(newStart, change.NewStart);
        Assert.Equal(1, change.OldDentistId);
        Assert.Equal(2, change.NewDentistId);
        Assert.Equal("appointment.rescheduled", env.Db.AppointmentOutboxEvents.Single().EventType);
        Assert.Equal(30, env.Availability.LastCheck!.DurationMinutes);
        Assert.Equal(appointment.Id, env.Availability.LastCheck.ExcludeAppointmentId);
    }

    [Fact]
    public async Task Reschedule_UT_RES_18_dentist_not_given_keeps_the_current_dentist()
    {
        using var env = await NewEnv();
        await env.Service.RescheduleAsync(env.Customer, Code, Move(), default);
        Assert.Equal(1, env.Appointment().DentistId);
        Assert.Equal(1, env.Availability.LastCheck!.DentistId);
    }

    [Fact]
    public async Task Reschedule_UT_RES_19_clinic_initiated_staff_to_a_dentist_of_the_branch_is_allowed()
    {
        using var env = await NewEnv();
        await env.Service.RescheduleAsync(env.Assistant, Code, Move(dentistId: 2, clinic: true), default);
        Assert.Equal(2, env.Appointment().DentistId);
    }

    [Fact]
    public async Task Reschedule_UT_RES_20_retry_replays_and_a_changed_chair_with_the_same_key_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.RescheduleAsync(env.Customer, Code, Move(chair: 2), default);
        var again = await env.Service.RescheduleAsync(env.Customer, Code, Move(chair: 2), default);
        Assert.Equal("MSG35", again.Notification.Code);
        Assert.Equal(1, await env.Count<AppointmentChange>());
        await Rejects(() => env.Service.RescheduleAsync(env.Customer, Code, Move(chair: 3), default), 409, "MSG111");
    }

    // =====================================================================================
    // 5. MarkNoShowAsync
    // =====================================================================================
    private static Action<Appointment> Started => a => { a.StartAt = Now.AddHours(-1); a.EndAt = a.StartAt.AddMinutes(30); };

    public static TheoryData<string, Guid, int, string, string?, string> NoShowInvalidInput => new()
    {
        { "UT-NSH-01 RequestKey rỗng", Guid.Empty, 1, Code, "Khách không đến", "MSG04" },
        { "UT-NSH-02 ExpectedVersion = 0", Key, 0, Code, "Khách không đến", "MSG04" },
        { "UT-NSH-03 Mã lịch rỗng", Key, 1, "", "Khách không đến", "MSG04" },
        { "UT-NSH-04 Mã lịch dài 41 ký tự", Key, 1, "L123456789L123456789L123456789L123456789L", "Khách không đến", "MSG04" },
        { "UT-NSH-05 Lý do rỗng", Key, 1, Code, " ", "MSG02" },
        { "UT-NSH-06 Lý do 501 ký tự", Key, 1, Code, "#501", "MSG03" },
    };

    [Theory, MemberData(nameof(NoShowInvalidInput))]
    public async Task NoShow_rejects_invalid_input(string caseName, Guid key, int version, string code, string? reason, string msg)
    {
        using var env = await NewEnv(Started);
        if (reason == "#501") reason = new string('a', 501);
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, code, new NoShowAppointmentRequest(version, key, reason), default), 400, msg);
    }

    [Fact]
    public async Task NoShow_UT_NSH_07_public_booker_cannot_mark_no_show()
    {
        using var env = await NewEnv(Started);
        await Rejects(() => env.Service.MarkNoShowAsync(env.Customer, Code, new(1, Key, "Khách không đến"), default), 403, "MSG06");
    }

    [Fact]
    public async Task NoShow_UT_NSH_08_staff_of_another_branch_is_MSG102()
    {
        using var env = await NewEnv(Started, branches: [2]);
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default), 404, "MSG102");
    }

    [Fact]
    public async Task NoShow_UT_NSH_09_version_mismatch_is_MSG111()
    {
        using var env = await NewEnv(Started);
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, Code, new(9, Key, "Khách không đến"), default), 409, "MSG111");
    }

    [Theory]
    [InlineData("UT-NSH-10 Lịch chưa tới giờ (StartAt > hiện tại)", "Confirmed", 1)]
    [InlineData("UT-NSH-11 Lịch đã hủy", "Cancelled", -1)]
    [InlineData("UT-NSH-12 Lịch đã vắng mặt", "NoShow", -1)]
    public async Task NoShow_rejects_appointment_that_is_not_a_started_confirmed_one(string caseName, string status, int startHours)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(startHours); a.EndAt = a.StartAt.AddMinutes(30); });
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default), 409, "MSG31");
    }

    [Fact]
    public async Task NoShow_UT_NSH_13_boundary_start_equal_to_now_is_allowed()
    {
        using var env = await NewEnv(a => { a.StartAt = Now; a.EndAt = Now.AddMinutes(30); });
        var result = await env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default);
        Assert.Equal("MSG222", result.Notification.Code);
    }

    [Fact]
    public async Task NoShow_UT_NSH_14_appointment_that_already_has_a_visit_is_MSG31()
    {
        using var env = await NewEnv(Started);
        env.Db.Visits.Add(new Visit { AppointmentId = env.Appointment().Id, PatientId = 1, BranchId = 1, DentistId = 1, ArrivalAt = Now.AddHours(-1), RecordedBy = env.Assistant });
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default), 409, "MSG31");
    }

    [Fact]
    public async Task NoShow_UT_NSH_15_success_sets_NoShow_and_writes_change_outbox_and_receipt()
    {
        using var env = await NewEnv(Started);
        var result = await env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "  Khách không đến  "), default);

        var appointment = env.Appointment();
        Assert.Equal("NoShow", appointment.Status);
        Assert.Equal(2, appointment.Version);
        Assert.Equal("MSG222", result.Notification.Code);
        Assert.Equal("Đã ghi nhận trạng thái vắng mặt với mã LH-0001.", result.Notification.Message);
        var change = env.Db.AppointmentChanges.Single();
        Assert.Equal("NO_SHOW", change.Action);
        Assert.Equal("STAFF", change.ActorKind);
        Assert.Equal("Khách không đến", change.Reason);
        Assert.Equal("appointment.no_show", env.Db.AppointmentOutboxEvents.Single().EventType);
        Assert.Equal(1, await env.Count<AppointmentChangeReceipt>());
    }

    [Fact]
    public async Task NoShow_UT_NSH_16_dentist_without_assistant_role_may_mark_no_show()
    {
        using var env = await NewEnv(Started);
        await env.Service.MarkNoShowAsync(env.Dentist, Code, new(1, Key, "Khách không đến"), default);
        Assert.Equal("NoShow", env.Appointment().Status);
    }

    [Fact]
    public async Task NoShow_UT_NSH_17_retry_replays_and_same_key_with_another_reason_is_MSG111()
    {
        using var env = await NewEnv(Started);
        await env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default);
        var again = await env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Khách không đến"), default);
        Assert.Equal("MSG222", again.Notification.Code);
        Assert.Equal(1, await env.Count<AppointmentChange>());
        await Rejects(() => env.Service.MarkNoShowAsync(env.Assistant, Code, new(1, Key, "Lý do khác"), default), 409, "MSG111");
    }
}
