using System.Security.Claims;
using DCMS.Application.Common;
using DCMS.Application.Features.Reception;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
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
/// Method-level tests of ReceptionService.CheckInAsync (the receptionist confirms the patient who arrives for an appointment).
/// Runs on PostgreSQL because the patient row is locked with a raw "FOR UPDATE" query.
/// Input = actor + appointment state + request; Expected = status / code / text or the saved state.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReceptionCheckInTests
{
    // Now = 09:00 on 10/01/2030 in clinic time (UTC+7).
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Key = new("44444444-4444-4444-4444-444444444444");

    private sealed class Env : IDisposable
    {
        public required CreateTestKit.PostgresDb Pg; public required ReceptionService Service; public required TestAvailability Availability;
        public required int Assistant, Dentist, OtherBranchAssistant, Customer, PatientId, OtherPatientId, ArchivedPatientId, Branch, Suffix;
        public required Guid AppointmentId;
        public DcmsDbContext Db => Pg.Db;
        public void Dispose() => Pg.Db.Dispose();
        public Appointment Appointment => Db.Appointments.AsNoTracking().Single(x => x.Id == AppointmentId);
        public CheckInRequest Request(int? patientId = null, int version = 1, bool identity = true, string purpose = "InitialExamination",
            DateTimeOffset? arrival = null, string? reason = null, string? note = null, Guid? key = null) =>
            new(patientId ?? PatientId, version, key ?? RequestKey, identity, arrival, reason, purpose, note);
        public Guid RequestKey { get; } = Guid.NewGuid();
    }

    /// <summary>Appointment: confirmed, today 10:00-11:00 (clinic time), no patient yet, attendee "Trần Thị Mai" born 05/05/1985.</summary>
    private static async Task<Env> NewEnv(Action<Appointment>? tune = null)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        var n = CreateTestKit.NextId(); var otherBranch = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "assistant" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dentist" + n, AccountRoles.Dentist, n);
        var elsewhere = await CreateTestKit.SeedStaffAsync(db, "elsewhere" + n, AccountRoles.ClinicAssistant, otherBranch);
        var customer = new ApplicationUser { UserName = "customer" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        db.Users.Add(customer);
        PatientProfile P(string code, string name, DateOnly? birth, bool archived = false) => new()
        { PatientCode = code + n, FullName = name, NormalizedName = name.ToUpperInvariant(), BirthDate = birth, IsArchived = archived, CreatedAt = Now, UpdatedAt = Now };
        var patient = P("BN", "Trần Thị Mai", new DateOnly(1985, 5, 5));
        var other = P("BM", "Lê Văn Nam", new DateOnly(1990, 1, 1));
        var archived = P("BA", "Trần Thị Mai", new DateOnly(1985, 5, 5), true);
        db.Patients.AddRange(patient, other, archived);
        await db.SaveChangesAsync();
        var dentistEmployee = await db.Employees.SingleAsync(x => x.AccountId == dentist.Id);
        (await db.Set<DCMS.Domain.Features.Scheduling.SchedulingBranch>().SingleAsync(x => x.Id == n)).Chairs.Add(new DCMS.Domain.Features.Scheduling.SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n });
        await db.SaveChangesAsync();
        var appointment = new Appointment
        {
            Code = "LH" + n, BranchId = n, DentistId = dentistEmployee.Id, ChairId = n, BookerAccountId = customer.Id, AttendeeName = "Trần Thị Mai",
            StartAt = Now.AddHours(1), EndAt = Now.AddHours(2), Status = "Confirmed", Version = 1, CreatedAt = Now,
            BookingInfo = new AppointmentBookingInfo { BookerName = "Mai", BookerContact = "0987654321", AttendeeBirthDate = new DateOnly(1985, 5, 5) }
        };
        tune?.Invoke(appointment);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        var availability = new TestAvailability();
        var service = new ReceptionService(db, CreateTestKit.NewRoleUserManager(db), new TestQueries(), availability, new TestClock(Now), new TestBranchAccess(n, otherBranch));
        return new Env { Pg = pg, Service = service, Availability = availability, Assistant = assistant.Id, Dentist = dentist.Id, OtherBranchAssistant = elsewhere.Id,
            Customer = customer.Id, PatientId = patient.Id, OtherPatientId = other.Id, ArchivedPatientId = archived.Id, Branch = n, Suffix = n, AppointmentId = appointment.Id };
    }

    private static async Task Rejects(Func<Task> action, int status, string code, string? message = null)
    {
        var error = await CreateTestKit.ErrorOf(action);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
    }

    // ---------- Input validation: Input = one bad field, Expected = 400 MSG04 ----------
    public static TheoryData<string, string> InvalidRequest => new()
    {
        { "UT-CHK-01 PatientId = 0", "patient" },
        { "UT-CHK-02 ExpectedVersion = 0", "version" },
        { "UT-CHK-03 RequestKey rỗng", "key" },
        { "UT-CHK-04 Chưa xác nhận đối chiếu người đến khám", "identity" },
        { "UT-CHK-05 Mục đích không hợp lệ (Other)", "purpose" },
        { "UT-CHK-06 Ghi chú 1.001 ký tự", "note" },
        { "UT-CHK-07 Lý do đến 501 ký tự", "reason" },
        { "UT-CHK-08 Mã lịch hẹn rỗng", "appointment" },
    };

    [Theory, MemberData(nameof(InvalidRequest))]
    public async Task CheckIn_rejects_invalid_input(string caseName, string field)
    {
        using var env = await NewEnv();
        var request = field switch
        {
            "patient" => env.Request() with { PatientId = 0 },
            "version" => env.Request(version: 0),
            "key" => env.Request() with { RequestKey = Guid.Empty },
            "identity" => env.Request(identity: false),
            "purpose" => env.Request(purpose: "Other"),
            "note" => env.Request(note: new string('a', 1001)),
            "reason" => env.Request(reason: new string('a', 501)),
            _ => env.Request()
        };
        var id = field == "appointment" ? Guid.Empty : env.AppointmentId;
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, id, request, default), 400, "MSG04", "Thông tin không đúng định dạng.");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_09_note_of_1000_and_reason_of_500_characters_are_accepted()
    {
        using var env = await NewEnv();
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(note: new string('a', 1000)), default);
        Assert.Equal("CheckedIn", env.Appointment.Status);
    }

    // ---------- Lookup and permission ----------
    [Fact]
    public async Task CheckIn_UT_CHK_10_unknown_appointment_is_MSG113()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, Guid.NewGuid(), env.Request(), default), 404, "MSG113",
            "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_11_unknown_actor_is_MSG07()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(9999, env.AppointmentId, env.Request(), default), 401, "MSG07");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_12_customer_account_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Customer, env.AppointmentId, env.Request(), default), 403, "MSG06");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_13_dentist_cannot_check_a_patient_in()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Dentist, env.AppointmentId, env.Request(), default), 403, "MSG06");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_14_assistant_of_another_branch_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.OtherBranchAssistant, env.AppointmentId, env.Request(), default), 403, "MSG06");
    }

    // ---------- Patient matching ----------
    [Fact]
    public async Task CheckIn_UT_CHK_15_unknown_patient_is_MSG113()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(patientId: 999999), default), 404, "MSG113");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_16_archived_patient_is_MSG202()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(patientId: env.ArchivedPatientId), default), 409, "MSG202",
            "Bản ghi đã ngừng sử dụng. Vui lòng khôi phục hoặc chọn bản ghi hợp lệ.");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_17_appointment_already_linked_to_another_patient_is_MSG43()
    {
        using var env = await NewEnv();
        var link = await env.Db.Appointments.SingleAsync(x => x.Id == env.AppointmentId);
        link.PatientId = env.OtherPatientId; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default), 409, "MSG43");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_18_unlinked_appointment_with_a_different_attendee_name_is_MSG43()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(patientId: env.OtherPatientId), default), 409, "MSG43");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_19_unlinked_appointment_with_a_different_birth_date_is_MSG43()
    {
        using var env = await NewEnv(a => a.BookingInfo.AttendeeBirthDate = new DateOnly(1999, 9, 9));
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default), 409, "MSG43");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_20_name_match_ignores_case_and_accents_when_the_appointment_has_no_birth_date()
    {
        using var env = await NewEnv(a => { a.AttendeeName = "TRAN THI MAI"; a.BookingInfo.AttendeeBirthDate = null; });
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        Assert.Equal(env.PatientId, env.Appointment.PatientId);
    }

    // ---------- Version and eligibility ----------
    [Fact]
    public async Task CheckIn_UT_CHK_21_version_mismatch_is_MSG111()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(version: 7), default), 409, "MSG111");
    }

    [Theory]
    [InlineData("UT-CHK-22 Lịch đã hủy", "Cancelled", 1, 2, "MSG42")]
    [InlineData("UT-CHK-23 Lịch đã vắng mặt", "NoShow", 1, 2, "MSG47")]
    [InlineData("UT-CHK-24 Lịch của ngày khác (ngày mai)", "Confirmed", 25, 26, "MSG42")]
    [InlineData("UT-CHK-25 Lịch của ngày hôm qua", "Confirmed", -22, -21, "MSG42")]
    [InlineData("UT-CHK-26 Lịch đã kết thúc (EndAt = hiện tại)", "Confirmed", -1, 0, "MSG42")]
    public async Task CheckIn_rejects_an_appointment_that_is_not_eligible_today(string caseName, string status, int startHours, int endHours, string code)
    {
        using var env = await NewEnv(a => { a.Status = status; a.StartAt = Now.AddHours(startHours); a.EndAt = Now.AddHours(endHours); });
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default), 409, code);
    }

    [Fact]
    public async Task CheckIn_UT_CHK_27_appointment_that_started_earlier_but_has_not_ended_is_eligible()
    {
        using var env = await NewEnv(a => { a.StartAt = Now.AddHours(-1); a.EndAt = Now.AddMinutes(1); });
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        Assert.Equal("CheckedIn", env.Appointment.Status);
    }

    // ---------- Arrival time ----------
    [Fact]
    public async Task CheckIn_UT_CHK_28_arrival_time_in_the_future_is_MSG04()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(arrival: Now.AddMinutes(1), reason: "Nhập lại"), default), 400, "MSG04");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_29_arrival_time_on_another_day_is_MSG04()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(arrival: Now.AddDays(-1), reason: "Nhập lại"), default), 400, "MSG04");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_30_entering_an_arrival_time_without_the_backdate_permission_is_MSG06()
    {
        using var env = await NewEnv();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(arrival: Now.AddMinutes(-20), reason: "Quên nhập"), default), 403, "MSG06");
    }

    private static async Task GrantBackdate(Env env)
    {
        var users = CreateTestKit.NewRoleUserManager(env.Db);
        var user = (await users.FindByIdAsync(env.Assistant.ToString()))!;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await users.AddClaimAsync(user, new Claim("permission", "reception.backdate"));
    }

    [Fact]
    public async Task CheckIn_UT_CHK_31_backdate_permission_without_a_reason_is_MSG02()
    {
        using var env = await NewEnv();
        await GrantBackdate(env);
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(arrival: Now.AddMinutes(-20)), default), 400, "MSG02");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_32_backdate_permission_with_a_reason_stores_the_entered_arrival_time()
    {
        using var env = await NewEnv();
        await GrantBackdate(env);
        var result = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(arrival: Now.AddMinutes(-20), reason: "  Quên nhập  "), default);
        Assert.Equal(Now.AddMinutes(-20), result.ArrivalAt);
        Assert.Equal("Quên nhập", result.ArrivalReason);
    }

    // ---------- Success ----------
    [Fact]
    public async Task CheckIn_UT_CHK_33_success_creates_the_visit_and_updates_the_appointment()
    {
        using var env = await NewEnv();
        var result = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(note: "  Đau răng hàm  "), default);

        Assert.False(result.Replayed);
        Assert.Equal("Queued", result.Status);
        Assert.Equal("CheckedIn", result.AppointmentStatus);
        Assert.Equal(2, result.AppointmentVersion);
        Assert.Equal(Now, result.ArrivalAt);
        Assert.Equal("InitialExamination", result.Purpose);
        Assert.Equal("Đau răng hàm", result.Note);
        Assert.Equal(env.PatientId, result.PatientId);
        var appointment = env.Appointment;
        Assert.Equal("CheckedIn", appointment.Status);
        Assert.Equal(2, appointment.Version);
        Assert.Equal(env.PatientId, appointment.PatientId);
        var visit = env.Db.Visits.Single(x => x.AppointmentId == env.AppointmentId);
        Assert.Equal(env.Assistant, visit.RecordedBy);
        Assert.Equal(appointment.DentistId, visit.DentistId);
        Assert.Single(env.Db.Set<VisitAudit>().Where(x => x.VisitId == visit.Id));
        Assert.Equal(1, await env.Db.Set<ReceptionReceipt>().CountAsync(x => x.VisitId == visit.Id));
        Assert.Single(env.Db.AppointmentChanges.Where(x => x.AppointmentId == env.AppointmentId && x.Action == "CHECK_IN"));
    }

    [Fact]
    public async Task CheckIn_UT_CHK_34_each_attendance_purpose_is_accepted()
    {
        foreach (var purpose in AttendancePurposes.All)
        {
            using var env = await NewEnv();
            var result = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(purpose: purpose), default);
            Assert.Equal(purpose, result.Purpose);
        }
    }

    [Fact]
    public async Task CheckIn_UT_CHK_35_availability_validation_failure_is_passed_through_and_nothing_is_saved()
    {
        using var env = await NewEnv();
        env.Availability.OnValidate = () => throw new BusinessException(409, SystemMessages.Get("MSG47"));
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default), 409, "MSG47");
        Assert.Equal("Confirmed", env.Appointment.Status);
        Assert.Equal(0, await env.Db.Visits.CountAsync(x => x.AppointmentId == env.AppointmentId));
    }

    // ---------- Retry ----------
    [Fact]
    public async Task CheckIn_UT_CHK_36_retry_with_the_same_key_returns_the_existing_visit_as_replayed()
    {
        using var env = await NewEnv();
        var first = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        var again = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        Assert.True(again.Replayed);
        Assert.Equal(first.VisitId, again.VisitId);
        Assert.Equal(1, await env.Db.Visits.CountAsync(x => x.AppointmentId == env.AppointmentId));
        Assert.Equal(2, env.Appointment.Version);
    }

    [Fact]
    public async Task CheckIn_UT_CHK_37_same_key_with_another_purpose_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(purpose: "PeriodicOrNewProblem"), default), 409, "MSG111");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_38_new_key_on_an_appointment_that_already_has_a_visit_returns_that_visit()
    {
        using var env = await NewEnv();
        var first = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        var again = await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(version: 2, key: Guid.NewGuid()), default);
        Assert.True(again.Replayed);
        Assert.Equal(first.VisitId, again.VisitId);
    }

    [Fact]
    public async Task CheckIn_UT_CHK_39_existing_visit_of_another_patient_is_MSG43()
    {
        using var env = await NewEnv();
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        var appointment = await env.Db.Appointments.SingleAsync(x => x.Id == env.AppointmentId);
        appointment.PatientId = env.OtherPatientId; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(patientId: env.OtherPatientId, key: Guid.NewGuid()), default), 409, "MSG43");
    }

    [Fact]
    public async Task CheckIn_UT_CHK_40_a_key_already_used_for_another_appointment_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.CheckInAsync(env.Assistant, env.AppointmentId, env.Request(), default);
        var first = env.Appointment;
        var second = new Appointment { Code = "LX" + env.Suffix, BranchId = first.BranchId, DentistId = first.DentistId, ChairId = first.ChairId, BookerAccountId = first.BookerAccountId,
            AttendeeName = "Trần Thị Mai", StartAt = Now.AddHours(1), EndAt = Now.AddHours(2), Status = "Confirmed", Version = 1, CreatedAt = Now,
            BookingInfo = new AppointmentBookingInfo { BookerName = "Mai", BookerContact = "0987654321", AttendeeBirthDate = new DateOnly(1985, 5, 5) } };
        env.Db.Appointments.Add(second); await env.Db.SaveChangesAsync();
        await Rejects(() => env.Service.CheckInAsync(env.Assistant, second.Id, env.Request(), default), 409, "MSG111");
    }
}
