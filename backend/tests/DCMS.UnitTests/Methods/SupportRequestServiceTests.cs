using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SupportRequests;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Appointments;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of SupportRequestService: ListAsync, DetailAsync, VerifyRequesterAsync, ChangeAppointmentAsync,
/// ResolveAsync, CreateCustomerRequestAsync, WithdrawCustomerRequestAsync (private helpers are reached through them).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SupportRequestServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private readonly Guid Key = Guid.NewGuid();
    private const string Code = "LH-0001";

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class BranchAccess(params int[] ids) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], ids));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRealtime : IRealtimeNotifier
    {
        public bool Throw; public int Sent;
        public Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default)
        { Sent++; return Throw ? throw new InvalidOperationException("hub down") : Task.CompletedTask; }
        public Task SendToAllAsync(string method, object payload, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChanges : IAppointmentChangeService
    {
        public AvailabilitySlot[] Slots = [];
        public CancelAppointmentRequest? Cancelled; public RescheduleAppointmentRequest? Rescheduled;
        private static AppointmentChangeResult Result(string code) => new(new MyAppointmentDetail(Code, Now, Now, 30, "BS", "Khách", "Self",
            "Confirmed", "Booker", 2, 1, "CN", 1, null, "", "Khách", "***", new MyEligibility(true, 48, null, true), [], [],
            new MyClinicContact("", "")), SystemMessages.Get(code, value: Code));
        public Task<AvailabilityResult> AvailabilityAsync(int actorId, string code, DateOnly date, int? dentistId, CancellationToken ct) =>
            Task.FromResult(new AvailabilityResult("tz", date, Slots));
        public Task<AppointmentChangeResult> RescheduleAsync(int actorId, string code, RescheduleAppointmentRequest request, CancellationToken ct)
        { Rescheduled = request; return Task.FromResult(Result("MSG35")); }
        public Task<AppointmentChangeResult> CancelAsync(int actorId, string code, CancelAppointmentRequest request, CancellationToken ct)
        { Cancelled = request; return Task.FromResult(Result("MSG37")); }
        public Task<AppointmentChangeResult> MarkNoShowAsync(int actorId, string code, NoShowAppointmentRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AppointmentChangeResult> UndoNoShowAsync(int actorId, string code, NoShowAppointmentRequest request, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Env : IDisposable
    {
        public required CreateTestKit.PostgresDb Pg; public required SupportRequestService Service;
        public required FakeChanges Changes; public required FakeRealtime Realtime;
        public required int Assistant, Dentist, Customer, Stranger, Branch, PatientId, DentistEmployeeId, Suffix;
        public Guid AppointmentId;
        public DcmsDbContext Db => Pg.Db;
        public string Code => "LH" + Suffix;
        /// <summary>Support request code, unique per test because every test shares the PostgreSQL schema.</summary>
        public string Sr(string n) => $"S{n}x{Suffix}";
        public void Dispose() => Pg.Db.Dispose();
        public AppointmentSupportRequest Request(string n = "1") => Db.Appointments
            .Single(x => x.Id == AppointmentId).SupportRequests.Single(x => x.Code == Sr(n));
        public Appointment Appointment() => Db.Appointments.Single(x => x.Id == AppointmentId);

        /// <summary>Adds a support request (default: pending "Change", already verified by staff) to the appointment.</summary>
        public async Task<AppointmentSupportRequest> Add(string n = "1", string kind = "Change", string status = "Pending",
            int version = 1, bool verified = true, DateTimeOffset? proposed = null, Guid? actionKey = null)
        {
            var appointment = Db.Appointments.Single(x => x.Id == AppointmentId);
            var request = new AppointmentSupportRequest
            {
                Code = Sr(n), AppointmentId = AppointmentId, Kind = kind, Status = status, Version = version, RequestKey = Guid.NewGuid(),
                RequesterAccountId = Customer, RequesterName = "Khách", Reason = "Bận", ProposedStartAt = proposed,
                RequesterVerifiedAt = verified ? Now : null, RequesterVerification = verified ? "Đã gọi lại SĐT" : null,
                AppointmentActionRequestKey = actionKey, CreatedAt = Now, UpdatedAt = Now
            };
            appointment.SupportRequests.Add(request);
            await Db.SaveChangesAsync();
            return request;
        }
    }

    /// <summary>Real PostgreSQL (shared test schema); every call makes its own branch, users, patient and appointment.</summary>
    private static async Task<Env> NewEnv(Action<Appointment>? tune = null, int[]? branches = null, bool patient = false)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        var n = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "assistant" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dentist" + n, AccountRoles.Dentist, n);
        var dentistEmployee = await db.Employees.SingleAsync(x => x.AccountId == dentist.Id);
        var customer = new ApplicationUser { UserName = "customer" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        var stranger = new ApplicationUser { UserName = "stranger" + n, FullName = "Người lạ", AccountType = "Public", IsActive = true };
        db.Users.AddRange(customer, stranger);
        var patientProfile = new DCMS.Domain.Features.Patients.PatientProfile { PatientCode = "BN" + n, FullName = "Khách", NormalizedName = "KHACH",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Patients.Add(patientProfile);
        await db.SaveChangesAsync();
        var appointment = new Appointment
        {
            Code = "LH" + n, BranchId = n, DentistId = dentistEmployee.Id, ChairId = 1, BookerAccountId = customer.Id, AttendeeName = "Khách",
            StartAt = Now.AddHours(48), EndAt = Now.AddHours(48).AddMinutes(30), Status = "Confirmed", Version = 1, CreatedAt = Now,
            PatientId = patient ? patientProfile.Id : null
        };
        tune?.Invoke(appointment);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        var changes = new FakeChanges(); var realtime = new FakeRealtime();
        var service = new SupportRequestService(db, CreateTestKit.NewRoleUserManager(db), changes, realtime,
            NullLogger<SupportRequestService>.Instance, new FixedClock(Now), new BranchAccess(branches ?? [n]));
        return new Env { Pg = pg, Service = service, Changes = changes, Realtime = realtime, AppointmentId = appointment.Id, Suffix = n,
            Assistant = assistant.Id, Dentist = dentist.Id, Customer = customer.Id, Stranger = stranger.Id, Branch = n,
            PatientId = patientProfile.Id, DentistEmployeeId = dentistEmployee.Id };
    }

    private static async Task Rejects(Func<Task> action, int status, string code, string? message = null)
    {
        var error = await CreateTestKit.ErrorOf(action);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
    }

    // =====================================================================================
    // 1. ListAsync (+ StaffBranchesAsync)
    // =====================================================================================
    [Fact]
    public async Task List_UT_SRQ_01_dentist_without_assistant_role_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.ListAsync(env.Dentist, null, null, 1, 20, default), 403, "MSG06");
    }

    [Fact]
    public async Task List_UT_SRQ_02_customer_account_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.ListAsync(env.Customer, null, null, 1, 20, default), 403, "MSG06");
    }

    [Fact]
    public async Task List_UT_SRQ_03_unknown_actor_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.ListAsync(9999, null, null, 1, 20, default), 403, "MSG06");
    }

    [Fact]
    public async Task List_UT_SRQ_04_branch_filter_outside_the_staff_branches_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.ListAsync(env.Assistant, null, 999999, 1, 20, default), 403, "MSG06");
    }

    [Fact]
    public async Task List_UT_SRQ_05_staff_whose_permission_grants_no_branch_sees_nothing()
    {
        using var env = await NewEnv(branches: [-1]);
        await env.Add();
        var page = await env.Service.ListAsync(env.Assistant, null, null, 1, 20, default);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task List_UT_SRQ_06_default_status_filter_is_Pending()
    {
        using var env = await NewEnv();
        await env.Add("1", status: "Pending"); await env.Add("2", status: "Resolved");
        var page = await env.Service.ListAsync(env.Assistant, null, null, 1, 20, default);
        Assert.Equal(1, page.Total);
        Assert.Equal(env.Sr("1"), page.Items[0].Id);
    }

    [Fact]
    public async Task List_UT_SRQ_07_status_filter_accepts_several_comma_separated_values()
    {
        using var env = await NewEnv();
        await env.Add("1", status: "Pending"); await env.Add("2", status: "Resolved"); await env.Add("3", status: "Rejected");
        var page = await env.Service.ListAsync(env.Assistant, "Pending, Resolved", null, 1, 20, default);
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task List_UT_SRQ_08_paging_is_clamped_to_page_1_and_size_1_to_100()
    {
        using var env = await NewEnv();
        await env.Add();
        var low = await env.Service.ListAsync(env.Assistant, null, env.Branch, -5, 0, default);
        Assert.Equal((1, 1), (low.Page, low.PageSize));
        var high = await env.Service.ListAsync(env.Assistant, null, null, 1, 1000, default);
        Assert.Equal(100, high.PageSize);
    }

    [Fact]
    public async Task List_UT_SRQ_09_appointment_filter_keeps_only_that_appointment()
    {
        using var env = await NewEnv();
        await env.Add();
        Assert.Equal(1, (await env.Service.ListAsync(env.Assistant, null, null, 1, 20, default, env.AppointmentId)).Total);
        Assert.Equal(0, (await env.Service.ListAsync(env.Assistant, null, null, 1, 20, default, Guid.NewGuid())).Total);
    }

    // =====================================================================================
    // 2. DetailAsync (+ AuthorizedAsync, ToDetailAsync)
    // =====================================================================================
    [Fact]
    public async Task Detail_UT_SRQ_10_unknown_request_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.DetailAsync(env.Assistant, env.Sr("X"), default), 404, "MSG102");
    }

    [Fact]
    public async Task Detail_UT_SRQ_11_staff_of_another_branch_is_MSG06()
    {
        using var env = await NewEnv(branches: [-1]);
        await env.Add();
        await Rejects(() => env.Service.DetailAsync(env.Assistant, env.Sr("1"), default), 403, "MSG06");
    }

    [Fact]
    public async Task Detail_UT_SRQ_12_returns_the_request_with_history_and_no_action_committed()
    {
        using var env = await NewEnv();
        await env.Add();
        var detail = await env.Service.DetailAsync(env.Assistant, env.Sr("1"), default);
        Assert.Equal(env.Sr("1"), detail.Id);
        Assert.Equal(env.Code, detail.AppointmentCode);
        Assert.False(detail.AppointmentActionCommitted);
        Assert.Contains(detail.History, x => x.Action == "Created");
    }

    [Fact]
    public async Task Detail_UT_SRQ_13_action_is_committed_only_when_a_matching_appointment_change_exists()
    {
        using var env = await NewEnv();
        var actionKey = Guid.NewGuid();
        await env.Add("1", "Cancel", actionKey: actionKey);
        Assert.False((await env.Service.DetailAsync(env.Assistant, env.Sr("1"), default)).AppointmentActionCommitted);
        env.Db.AppointmentChanges.Add(new AppointmentChange { AppointmentId = env.AppointmentId, ActorAccountId = env.Assistant, Action = "CANCEL",
            RequestKey = actionKey, Reason = "x", OccurredAt = Now });
        await env.Db.SaveChangesAsync();
        Assert.True((await env.Service.DetailAsync(env.Assistant, env.Sr("1"), default)).AppointmentActionCommitted);
    }

    // =====================================================================================
    // 3. VerifyRequesterAsync
    // =====================================================================================
    public static TheoryData<string, bool, int, string?, int, string> VerifyInvalid => new()
    {
        { "UT-SRQ-14 RequestKey rỗng", true, 1, "ok", 400, "MSG04" },
        { "UT-SRQ-15 ExpectedVersion = 0", false, 0, "ok", 400, "MSG04" },
        { "UT-SRQ-16 Ghi chú null", false, 1, null, 400, "MSG02" },
        { "UT-SRQ-17 Ghi chú toàn khoảng trắng", false, 1, "   ", 400, "MSG02" },
        { "UT-SRQ-18 Ghi chú 501 ký tự", false, 1, "#501", 400, "MSG03" },
    };

    [Theory, MemberData(nameof(VerifyInvalid))]
    public async Task Verify_rejects_invalid_input(string caseName, bool emptyKey, int version, string? notes, int status, string code)
    {
        using var env = await NewEnv();
        await env.Add(verified: false);
        if (notes == "#501") notes = new string('a', 501);
        await Rejects(() => env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(emptyKey ? Guid.Empty : Key, version, notes), default), status, code);
    }

    [Fact]
    public async Task Verify_UT_SRQ_19_unknown_request_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("X"), new(Key, 1, "ok"), default), 404, "MSG102");
    }

    [Theory]
    [InlineData("UT-SRQ-20 Sai version", "Pending", 1, 2)]
    [InlineData("UT-SRQ-21 Yêu cầu đã xử lý", "Resolved", 1, 1)]
    [InlineData("UT-SRQ-22 Yêu cầu đã rút", "Withdrawn", 1, 1)]
    public async Task Verify_rejects_a_request_that_is_not_pending_at_that_version(string caseName, string status, int version, int expected)
    {
        using var env = await NewEnv();
        await env.Add(status: status, version: version, verified: false);
        await Rejects(() => env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, expected, "ok"), default), 409, "MSG111");
    }

    [Fact]
    public async Task Verify_UT_SRQ_23_success_stamps_verification_bumps_version_and_notifies()
    {
        using var env = await NewEnv();
        await env.Add(verified: false);
        var detail = await env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, 1, "  Đã gọi lại SĐT  "), default);
        var request = env.Request();
        Assert.Equal("Đã gọi lại SĐT", request.RequesterVerification);
        Assert.Equal(env.Assistant, request.RequesterVerifiedBy);
        Assert.Equal(2, request.Version);
        Assert.Equal(Now, request.RequesterVerifiedAt);
        Assert.Equal(2, detail.ExpectedVersion);
        Assert.Contains(env.Db.AppointmentSupportHistories.Where(h => h.AppointmentId == env.AppointmentId), x => x.Action == "RequesterVerified");
        Assert.Equal(1, env.Realtime.Sent);
    }

    [Fact]
    public async Task Verify_UT_SRQ_24_retry_replays_and_same_key_with_other_notes_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Add(verified: false);
        await env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, 1, "A"), default);
        var again = await env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, 1, "A"), default);
        Assert.Equal(2, again.ExpectedVersion);
        Assert.Equal(2, env.Request().Version);
        await Rejects(() => env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, 1, "B"), default), 409, "MSG111");
    }

    [Fact]
    public async Task Verify_UT_SRQ_25_realtime_failure_does_not_fail_the_command()
    {
        using var env = await NewEnv();
        env.Realtime.Throw = true;
        await env.Add(verified: false);
        await env.Service.VerifyRequesterAsync(env.Assistant, env.Sr("1"), new(Key, 1, "A"), default);
        Assert.Equal(2, env.Request().Version);
    }

    // =====================================================================================
    // 4. ChangeAppointmentAsync
    // =====================================================================================
    private StaffSupportChangeCommand Staff(Guid? key = null, int version = 1, DateTimeOffset? start = null, string? notes = "Đã xếp lại") =>
        new(key ?? Key, version, "Khách yêu cầu", notes, start);

    [Theory]
    [InlineData("UT-SRQ-26 RequestKey rỗng", true, 1)]
    [InlineData("UT-SRQ-27 ExpectedVersion = 0", false, 0)]
    public async Task ChangeAppointment_rejects_invalid_input(string caseName, bool emptyKey, int version)
    {
        using var env = await NewEnv();
        await env.Add();
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(emptyKey ? Guid.Empty : Key, version), default), 400, "MSG04");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_28_unknown_request_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("X"), Staff(), default), 404, "MSG102");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_29_version_mismatch_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Add();
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(version: 3), default), 409, "MSG111");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_30_requester_must_be_verified_first()
    {
        using var env = await NewEnv();
        await env.Add(verified: false);
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(), default), 400, "MSG02",
            "Vui lòng nhập Xác minh người yêu cầu.");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_31_request_kind_other_than_Change_or_Cancel_is_MSG04()
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(), default), 400, "MSG04");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_32_cancel_request_cancels_with_notes_as_the_reason()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Cancel");
        var result = await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(notes: "Hủy theo yêu cầu"), default);
        Assert.True(result.Ok);
        Assert.Equal("MSG37", result.NotificationCode);
        Assert.Equal("Hủy theo yêu cầu", env.Changes.Cancelled!.Reason);
        Assert.Equal("Đã gọi lại SĐT", env.Changes.Cancelled.RequesterVerification);
        Assert.Equal(Key, env.Request().AppointmentActionRequestKey);
        Assert.Equal(2, env.Request().Version);
        Assert.Contains(env.Db.AppointmentSupportHistories.Where(h => h.AppointmentId == env.AppointmentId), x => x.Action == "Cancelled");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_33_cancel_without_notes_falls_back_to_the_command_reason()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Cancel");
        await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(notes: " "), default);
        Assert.Equal("Khách yêu cầu", env.Changes.Cancelled!.Reason);
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_34_change_request_without_any_start_is_MSG04()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Change", proposed: null);
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(start: null), default), 400, "MSG04");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_35_proposed_slot_not_available_is_MSG34()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Change", proposed: Now.AddHours(72));
        env.Changes.Slots = [new(Now.AddHours(73), Now.AddHours(74), 2, 1, null, 1, null)];
        await Rejects(() => env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(start: null), default), 409, "MSG34");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_36_reschedules_to_the_proposed_start_when_a_slot_matches()
    {
        using var env = await NewEnv();
        var start = Now.AddHours(72);
        await env.Add(kind: "Change", proposed: start);
        env.Changes.Slots = [new(start, start.AddMinutes(30), 2, 1, null, 5, null)];
        var result = await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(start: null), default);
        Assert.Equal("MSG35", result.NotificationCode);
        Assert.Equal(2, env.Changes.Rescheduled!.ChairId);
        Assert.Equal(5, env.Changes.Rescheduled.ScheduleVersion);
        Assert.Contains(env.Db.AppointmentSupportHistories.Where(h => h.AppointmentId == env.AppointmentId), x => x.Action == "Rescheduled");
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_37_command_start_overrides_the_customer_proposal()
    {
        using var env = await NewEnv();
        var chosen = Now.AddHours(96);
        await env.Add(kind: "Change", proposed: Now.AddHours(72));
        env.Changes.Slots = [new(chosen, chosen.AddMinutes(30), 3, 1, null, 1, null)];
        await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(start: chosen), default);
        Assert.Equal(chosen, env.Changes.Rescheduled!.NewStart);
    }

    [Fact]
    public async Task ChangeAppointment_UT_SRQ_38_retry_replays_the_stored_result()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Cancel");
        await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(), default);
        env.Changes.Cancelled = null;
        var again = await env.Service.ChangeAppointmentAsync(env.Assistant, env.Sr("1"), Staff(), default);
        Assert.True(again.Ok);
        Assert.Null(env.Changes.Cancelled);
    }

    // =====================================================================================
    // 5. ResolveAsync
    // =====================================================================================
    private ResolveSupportCommand Resolve(string decision = "Resolve", Guid? key = null, int version = 1, string? notes = "Đã xử lý", string? reason = null) =>
        new(key ?? Key, version, decision, notes, reason);

    [Theory]
    [InlineData("UT-SRQ-39 RequestKey rỗng", true, 1, "Resolve")]
    [InlineData("UT-SRQ-40 ExpectedVersion = 0", false, 0, "Resolve")]
    [InlineData("UT-SRQ-41 Quyết định không hợp lệ", false, 1, "Approve")]
    public async Task Resolve_rejects_invalid_input(string caseName, bool emptyKey, int version, string decision)
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(decision, emptyKey ? Guid.Empty : Key, version), default), 400, "MSG04");
    }

    [Fact]
    public async Task Resolve_UT_SRQ_42_reject_needs_a_reason()
    {
        using var env = await NewEnv();
        await env.Add();
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve("Reject", reason: " "), default), 400, "MSG02", "Vui lòng nhập Lý do từ chối.");
    }

    [Theory]
    [InlineData("UT-SRQ-43 Lý do từ chối 501 ký tự", true)]
    [InlineData("UT-SRQ-44 Ghi chú 501 ký tự", false)]
    public async Task Resolve_rejects_text_longer_than_500(string caseName, bool inReason)
    {
        using var env = await NewEnv();
        await env.Add();
        var long501 = new string('a', 501);
        var command = inReason ? Resolve("Reject", reason: long501) : Resolve(notes: long501);
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), command, default), 400, "MSG04");
    }

    [Fact]
    public async Task Resolve_UT_SRQ_45_version_mismatch_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(version: 4), default), 409, "MSG111");
    }

    [Theory]
    [InlineData("UT-SRQ-46 Yêu cầu Đổi lịch, chưa thực hiện đổi", "Change")]
    [InlineData("UT-SRQ-47 Yêu cầu Hủy, chưa thực hiện hủy", "Cancel")]
    public async Task Resolve_a_change_or_cancel_request_needs_the_appointment_action_first(string caseName, string kind)
    {
        using var env = await NewEnv();
        await env.Add(kind: kind);
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(), default), 409, "MSG111");
    }

    [Fact]
    public async Task Resolve_UT_SRQ_48_action_key_without_a_matching_change_row_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Cancel", actionKey: Guid.NewGuid());
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(), default), 409, "MSG111");
    }

    [Fact]
    public async Task Resolve_UT_SRQ_49_cancel_request_with_the_committed_cancel_can_be_resolved()
    {
        using var env = await NewEnv();
        var actionKey = Guid.NewGuid();
        await env.Add(kind: "Cancel", actionKey: actionKey);
        env.Db.AppointmentChanges.Add(new AppointmentChange { AppointmentId = env.AppointmentId, ActorAccountId = env.Assistant, Action = "CANCEL",
            RequestKey = actionKey, Reason = "x", OccurredAt = Now });
        await env.Db.SaveChangesAsync();
        var result = await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(), default);
        Assert.Equal("MSG37", result.NotificationCode);
        Assert.Equal("Resolved", env.Request().Status);
    }

    [Fact]
    public async Task Resolve_UT_SRQ_50_a_change_row_of_the_wrong_action_does_not_count()
    {
        using var env = await NewEnv();
        var actionKey = Guid.NewGuid();
        await env.Add(kind: "Cancel", actionKey: actionKey);
        env.Db.AppointmentChanges.Add(new AppointmentChange { AppointmentId = env.AppointmentId, ActorAccountId = env.Assistant, Action = "RESCHEDULE",
            RequestKey = actionKey, Reason = "x", OccurredAt = Now });
        await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(), default), 409, "MSG111");
    }

    [Fact]
    public async Task Resolve_UT_SRQ_51_general_request_resolves_and_writes_history_outbox_and_receipt()
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        var result = await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(notes: "  Đã tư vấn  "), default);
        var request = env.Request();
        Assert.True(result.Ok);
        Assert.Equal("Resolved", request.Status);
        Assert.Equal("Đã tư vấn", request.PublicNote);
        Assert.Equal("Đã tư vấn", request.DecisionReason);
        Assert.Equal(env.Assistant, request.DecidedBy);
        Assert.Equal(2, request.Version);
        Assert.Equal("support.resolved", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.AppointmentId).EventType);
        Assert.Contains(env.Db.AppointmentSupportHistories.Where(h => h.AppointmentId == env.AppointmentId), x => x.Action == "RequestResolved");
        Assert.Equal(1, await env.Db.SupportRequestReceipts.CountAsync(x => x.ActorAccountId == env.Assistant || x.ActorAccountId == env.Customer));
    }

    [Fact]
    public async Task Resolve_UT_SRQ_52_resolve_without_notes_uses_the_default_decision_text()
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve(notes: null), default);
        Assert.Equal("Đã xử lý yêu cầu.", env.Request().DecisionReason);
        Assert.Null(env.Request().PublicNote);
    }

    [Fact]
    public async Task Resolve_UT_SRQ_53_reject_stores_the_reason_and_returns_MSG38()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Change");
        var result = await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve("Reject", reason: "Hết giờ trống"), default);
        Assert.Equal("MSG38", result.NotificationCode);
        Assert.Equal("Rejected", env.Request().Status);
        Assert.Equal("Hết giờ trống", env.Request().DecisionReason);
        Assert.Equal("support.rejected", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.AppointmentId).EventType);
    }

    [Fact]
    public async Task Resolve_UT_SRQ_54_retry_replays_and_same_key_with_other_reason_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Add(kind: "General");
        await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve("Reject", reason: "A"), default);
        var again = await env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve("Reject", reason: "A"), default);
        Assert.Equal("MSG38", again.NotificationCode);
        Assert.Equal(1, await env.Db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == env.AppointmentId));
        await Rejects(() => env.Service.ResolveAsync(env.Assistant, env.Sr("1"), Resolve("Reject", reason: "B"), default), 409, "MSG111");
    }

    // =====================================================================================
    // 6. CreateCustomerRequestAsync (+ AuthorizeCustomerAppointmentAsync)
    // =====================================================================================
    private CustomerSupportRequestCommand Customer(Guid? key = null, string kind = "Change", string reason = "Bận việc", DateTimeOffset? start = null) =>
        new(key ?? Key, kind, reason, start);

    [Fact]
    public async Task CreateRequest_UT_SRQ_55_empty_request_key_is_MSG04()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(Guid.Empty), default), 400, "MSG04");
    }

    [Theory]
    [InlineData("UT-SRQ-56 Lý do rỗng", "", "MSG02")]
    [InlineData("UT-SRQ-57 Lý do toàn khoảng trắng", "   ", "MSG02")]
    [InlineData("UT-SRQ-58 Lý do 501 ký tự", "#501", "MSG03")]
    public async Task CreateRequest_validates_the_reason(string caseName, string reason, string code)
    {
        using var env = await NewEnv();
        if (reason == "#501") reason = new string('a', 501);
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(reason: reason), default), 400, code);
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_59_unknown_actor_is_MSG07()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(9999, env.Code, Customer(), default), 401, "MSG07");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_60_inactive_and_locked_accounts_are_MSG07()
    {
        using var env = await NewEnv();
        var user = (await env.Db.Users.FindAsync(env.Customer))!;
        user.IsActive = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(), default), 401, "MSG07");
        user.IsActive = true; user.LockoutEnabled = true; user.LockoutEnd = Now.AddHours(1); await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(Guid.NewGuid()), default), 401, "MSG07");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_61_other_account_type_is_MSG06()
    {
        using var env = await NewEnv();
        (await env.Db.Users.FindAsync(env.Customer))!.AccountType = "Admin"; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(), default), 403, "MSG06");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_62_unknown_appointment_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, "LH-9999", Customer(), default), 404, "MSG102");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_63_not_the_booker_and_not_linked_is_MSG102()
    {
        using var env = await NewEnv(patient: true);
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Stranger, env.Code, Customer(), default), 404, "MSG102");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_64_account_linked_to_the_patient_may_request()
    {
        using var env = await NewEnv(patient: true);
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = env.Stranger, PatientId = env.PatientId, Status = PatientLinkStatus.Verified, RequestCode = "LK-" + Guid.NewGuid().ToString("N")[..8] });
        await env.Db.SaveChangesAsync();
        var result = await env.Service.CreateCustomerRequestAsync(env.Stranger, env.Code, Customer(), default);
        Assert.Equal("MSG33", result.Notification.Code);
    }

    [Theory]
    [InlineData("UT-SRQ-65 Lịch đã hủy", "Cancelled", 48)]
    [InlineData("UT-SRQ-66 Lịch đã bắt đầu (StartAt = hiện tại)", "Confirmed", 0)]
    [InlineData("UT-SRQ-67 Lịch đã qua", "Confirmed", -3)]
    public async Task CreateRequest_rejects_an_appointment_that_is_not_a_future_confirmed_one(string caseName, string status, int hours)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(hours); a.EndAt = a.StartAt.AddMinutes(30); });
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(), default), 409, "MSG31");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_68_another_pending_request_blocks_a_new_one()
    {
        using var env = await NewEnv();
        await env.Add(status: "Pending");
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(Guid.NewGuid()), default), 409, "MSG111");
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_69_a_resolved_request_does_not_block_a_new_one()
    {
        using var env = await NewEnv();
        await env.Add(status: "Resolved");
        var result = await env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(Guid.NewGuid()), default);
        Assert.Equal("Pending", result.Request.Status);
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_70_request_key_already_used_for_another_request_is_MSG111()
    {
        using var env = await NewEnv();
        var existing = await env.Add(status: "Resolved");
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(existing.RequestKey), default), 409, "MSG111");
    }

    [Theory]
    [InlineData("UT-SRQ-71 Kind = Change", "Change", "Change", "RESCHEDULE")]
    [InlineData("UT-SRQ-72 Kind = Cancel (không phân biệt hoa thường)", "cancel", "Cancel", "CANCEL")]
    [InlineData("UT-SRQ-73 Kind lạ được coi là Change", "Other", "Change", "RESCHEDULE")]
    public async Task CreateRequest_maps_the_kind(string caseName, string kind, string stored, string dtoKind)
    {
        using var env = await NewEnv();
        var result = await env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(kind: kind), default);
        Assert.Equal(dtoKind, result.Request.Kind);
        Assert.Equal(stored, env.Appointment().SupportRequests.Single().Kind);
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_74_success_writes_request_history_outbox_and_receipt()
    {
        using var env = await NewEnv();
        var start = Now.AddHours(72);
        var result = await env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(reason: "  Bận việc  ", start: start), default);
        var request = env.Appointment().SupportRequests.Single();
        Assert.StartsWith("SR-", request.Code);
        Assert.Equal("Bận việc", request.Reason);
        Assert.Equal(start, request.ProposedStartAt);
        Assert.Equal("Pending", request.Status);
        Assert.Equal(1, request.Version);
        Assert.Equal("MSG33", result.Notification.Code);
        Assert.Equal(env.Code, result.Request.AppointmentCode);
        Assert.Equal("appointment.support_requested", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.AppointmentId).EventType);
        Assert.Equal("SELF", env.Db.AppointmentSupportHistories.Single(x => x.AppointmentId == env.AppointmentId).ActorKind);
        Assert.Equal(1, await env.Db.SupportRequestReceipts.CountAsync(x => x.ActorAccountId == env.Assistant || x.ActorAccountId == env.Customer));
        Assert.Equal(1, env.Realtime.Sent);
    }

    [Fact]
    public async Task CreateRequest_UT_SRQ_75_retry_replays_and_same_key_with_other_reason_is_MSG111()
    {
        using var env = await NewEnv();
        var first = await env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(), default);
        var again = await env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(), default);
        Assert.Equal(first.Request.Id, again.Request.Id);
        Assert.Equal(1, env.Appointment().SupportRequests.Count);
        await Rejects(() => env.Service.CreateCustomerRequestAsync(env.Customer, env.Code, Customer(reason: "Lý do khác"), default), 409, "MSG111");
    }

    // =====================================================================================
    // 7. WithdrawCustomerRequestAsync
    // =====================================================================================
    [Fact]
    public async Task Withdraw_UT_SRQ_76_expected_version_zero_is_MSG04()
    {
        using var env = await NewEnv();
        await env.Add();
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(0), default), 400, "MSG04");
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_77_unknown_request_is_MSG102()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("X"), new(1), default), 404, "MSG102");
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_78_unknown_or_inactive_user_is_MSG07()
    {
        using var env = await NewEnv();
        await env.Add();
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(9999, env.Sr("1"), new(1), default), 401, "MSG07");
        (await env.Db.Users.FindAsync(env.Customer))!.IsActive = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(1), default), 401, "MSG07");
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_79_stranger_without_link_is_MSG102()
    {
        using var env = await NewEnv(patient: true);
        await env.Add();
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(env.Stranger, env.Sr("1"), new(1), default), 404, "MSG102");
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_80_account_linked_to_the_patient_may_withdraw()
    {
        using var env = await NewEnv(patient: true);
        await env.Add();
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = env.Stranger, PatientId = env.PatientId, Status = PatientLinkStatus.Verified, RequestCode = "LK-" + Guid.NewGuid().ToString("N")[..8] });
        await env.Db.SaveChangesAsync();
        var dto = await env.Service.WithdrawCustomerRequestAsync(env.Stranger, env.Sr("1"), new(1), default);
        Assert.Equal("Withdrawn", dto.Status);
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_81_the_booker_may_withdraw_a_request_made_by_someone_else()
    {
        using var env = await NewEnv();
        await env.Add();
        env.Request().RequesterAccountId = env.Stranger; await env.Db.SaveChangesAsync();
        var dto = await env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(1), default);
        Assert.Equal("Withdrawn", dto.Status);
    }

    [Theory]
    [InlineData("UT-SRQ-82 Yêu cầu đã xử lý", "Resolved", 1, 1)]
    [InlineData("UT-SRQ-83 Sai version", "Pending", 1, 2)]
    public async Task Withdraw_rejects_a_request_that_is_not_pending_at_that_version(string caseName, string status, int version, int expected)
    {
        using var env = await NewEnv();
        await env.Add(status: status, version: version);
        await Rejects(() => env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(expected), default), 409, "MSG111");
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_84_success_sets_Withdrawn_and_writes_history_and_outbox()
    {
        using var env = await NewEnv();
        await env.Add(kind: "Cancel");
        var dto = await env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(1), default);
        var request = env.Request();
        Assert.Equal("Withdrawn", request.Status);
        Assert.Equal(2, request.Version);
        Assert.Equal(Now, request.DecidedAt);
        Assert.Equal("CANCEL", dto.Kind);
        Assert.Equal("appointment.support_withdrawn", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.AppointmentId).EventType);
        Assert.Equal("RequestWithdrawn", env.Db.AppointmentSupportHistories.Single(x => x.AppointmentId == env.AppointmentId).Action);
    }

    [Fact]
    public async Task Withdraw_UT_SRQ_85_realtime_failure_does_not_fail_the_command()
    {
        using var env = await NewEnv();
        env.Realtime.Throw = true;
        await env.Add();
        await env.Service.WithdrawCustomerRequestAsync(env.Customer, env.Sr("1"), new(1), default);
        Assert.Equal("Withdrawn", env.Request().Status);
    }
}
