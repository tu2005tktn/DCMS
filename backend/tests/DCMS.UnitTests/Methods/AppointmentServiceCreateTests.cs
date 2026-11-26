using DCMS.Application.Features.Appointments;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of AppointmentService.CreateAsync. Every row of <see cref="Rejected"/> is one case:
/// Input = actor + request (the Valid() request with one change), Expected = HTTP status, message code (and text when given).
/// The success and special-flow cases (replay, follow-up, family member, replacement of a no-show) are separate facts.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AppointmentServiceCreateTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Key = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DossierOpen = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid DossierClosed = new("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid DossierOther = new("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid SourceId = new("bbbbbbbb-0000-0000-0000-000000000001");
    private const string BadFormat = "Thông tin không đúng định dạng.";

    /// <summary>A valid booking of a guest: standard first examination, 48 h from now, 30 minutes, branch 1.</summary>
    private static CreateAppointmentRequest Valid() => new(1, new BookerContact("Lê Văn Nam", "0987654321", null),
        new AppointmentAttendee(null, "Lê Văn Nam", new DateOnly(1990, 1, 1)), "Self", "Standard", null, 1, "Đau răng",
        1, null, Now.AddHours(48), 30, null, null, Key, 1, null);

    // ---------- Input: actor + request; Expected: status, code, text ----------
    public static TheoryData<string, string, CreateAppointmentRequest, int, string, string?> Rejected => new()
    {
        // 1. Required numbers / ids
        { "UT-APC-01 RequestKey rỗng", "guest", Valid() with { RequestKey = Guid.Empty }, 400, "MSG04", BadFormat },
        { "UT-APC-02 BranchId = 0", "guest", Valid() with { BranchId = 0 }, 400, "MSG04", null },
        { "UT-APC-03 DentistId = 0", "guest", Valid() with { DentistId = 0 }, 400, "MSG04", null },
        { "UT-APC-04 ScheduleVersion = 0", "guest", Valid() with { ScheduleVersion = 0 }, 400, "MSG04", null },
        { "UT-APC-05 StartAt chưa chọn", "guest", Valid() with { StartAt = default }, 400, "MSG04", null },
        { "UT-APC-06 ReplacesAppointmentId = Guid rỗng", "assistant", Valid() with { ReplacesAppointmentId = Guid.Empty }, 400, "MSG04", null },
        // 2. Account
        { "UT-APC-07 Tài khoản không tồn tại", "ghost", Valid(), 401, "MSG07", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." },
        { "UT-APC-08 Tài khoản bị khóa (IsActive = false)", "inactive", Valid(), 401, "MSG07", null },
        { "UT-APC-09 Khách đặt thay cho lịch vắng mặt (ReplacesAppointmentId)", "customer", Valid() with { ReplacesAppointmentId = SourceId }, 403, "MSG06", null },
        { "UT-APC-10 Bác sĩ đặt lịch tại quầy (không phải lịch tái khám)", "dentist", Valid(), 403, "MSG06", null },
        { "UT-APC-11 Nhân viên không thuộc chi nhánh", "outsider", Valid(), 403, "MSG06", null },
        // 3. Relation / kind / source
        { "UT-APC-12 Có FamilyMemberId nhưng người đặt là khách vãng lai", "guest", Valid() with { FamilyMemberId = Guid.NewGuid(), AttendeeRelation = "Relative" }, 403, "MSG06", null },
        { "UT-APC-13 Có FamilyMemberId nhưng quan hệ là Self", "customer", Valid() with { FamilyMemberId = Guid.NewGuid() }, 403, "MSG06", null },
        { "UT-APC-14 Quan hệ không hợp lệ (Friend)", "guest", Valid() with { AttendeeRelation = "Friend" }, 400, "MSG04", null },
        { "UT-APC-15 Loại lịch không hợp lệ (Other)", "guest", Valid() with { Kind = "Other" }, 400, "MSG04", null },
        { "UT-APC-16 Nguồn của nhân viên không hợp lệ (Fax)", "assistant", Valid() with { Source = "Fax" }, 400, "MSG04", null },
        { "UT-APC-17 Khách đặt loại WalkIn", "guest", Valid() with { Kind = "WalkIn" }, 400, "MSG04", null },
        { "UT-APC-18 Có DossierId nhưng loại không phải FollowUp", "assistant", Valid() with { DossierId = DossierOpen }, 400, "MSG04", null },
        { "UT-APC-19 Loại FollowUp nhưng thiếu DossierId", "assistant", Valid() with { Kind = "FollowUp" }, 400, "MSG04", null },
        { "UT-APC-20 Thay thế lịch vắng mặt nhưng loại không phải WalkIn", "assistant", Valid() with { ReplacesAppointmentId = SourceId, Source = "InPerson" }, 400, "MSG04", null },
        { "UT-APC-21 Thay thế lịch vắng mặt nhưng nguồn không phải InPerson", "assistant", Valid() with { ReplacesAppointmentId = SourceId, Kind = "WalkIn" }, 400, "MSG04", null },
        // 4. Booker contact
        { "UT-APC-22 Người đặt thiếu họ tên (khách vãng lai)", "guest", Valid() with { Booker = new BookerContact(" ", "0987654321", null) }, 400, "MSG04", null },
        { "UT-APC-23 Họ tên người đặt 101 ký tự", "guest", Valid() with { Booker = new BookerContact(new string('a', 101), "0987654321", null) }, 400, "MSG04", null },
        { "UT-APC-24 Không có số điện thoại và email", "guest", Valid() with { Booker = new BookerContact("Nam", null, null) }, 400, "MSG04", null },
        { "UT-APC-25 Số điện thoại sai (9 chữ số)", "guest", Valid() with { Booker = new BookerContact("Nam", "098765432", null) }, 400, "MSG04", null },
        { "UT-APC-26 Số điện thoại không bắt đầu bằng 0", "guest", Valid() with { Booker = new BookerContact("Nam", "1987654321", null) }, 400, "MSG04", null },
        { "UT-APC-27 Email thiếu @", "guest", Valid() with { Booker = new BookerContact("Nam", null, "nam-gmail.com") }, 400, "MSG04", null },
        // 5. Attendee
        { "UT-APC-28 Người khám thiếu họ tên", "guest", Valid() with { Attendee = new AppointmentAttendee(null, " ", null) }, 400, "MSG23", "Vui lòng nhập thông tin người khám thực tế." },
        { "UT-APC-29 Họ tên người khám 101 ký tự", "guest", Valid() with { Attendee = new AppointmentAttendee(null, new string('a', 101), null) }, 400, "MSG23", null },
        { "UT-APC-30 Không có người khám (Attendee = null)", "guest", Valid() with { Attendee = null }, 400, "MSG23", null },
        { "UT-APC-31 Ngày sinh ở tương lai (11/01/2030)", "guest", Valid() with { Attendee = new AppointmentAttendee(null, "Nam", new DateOnly(2030, 1, 11)) }, 400, "MSG04", null },
        { "UT-APC-32 Khách gửi PatientId khi chưa liên kết hồ sơ", "customer", Valid() with { Attendee = new AppointmentAttendee(999, "Nam", null) }, 403, "MSG06", null },
        { "UT-APC-33 Nhân viên gửi PatientId không tồn tại", "assistant", Valid() with { Attendee = new AppointmentAttendee(999999, "Nam", null) }, 404, "MSG06", null },
        // 6. Reason / service / duration
        { "UT-APC-34 Lý do 1.001 ký tự", "guest", Valid() with { Reason = new string('a', 1001) }, 400, "MSG04", null },
        { "UT-APC-35 Lý do ngoại lệ 501 ký tự (nhân viên)", "assistant", Valid() with { ExceptionReason = new string('a', 501) }, 400, "MSG04", null },
        { "UT-APC-36 Nhân viên không chọn dịch vụ và không ghi lý do", "assistant", Valid() with { ServiceId = null, Reason = null }, 400, "MSG04", null },
        { "UT-APC-37 Khách không chọn dịch vụ", "guest", Valid() with { ServiceId = null }, 400, "MSG04", null },
        { "UT-APC-38 Khách gửi lý do ngoại lệ", "guest", Valid() with { ExceptionReason = "Gấp" }, 400, "MSG04", null },
        { "UT-APC-39 Thời lượng 0 phút", "guest", Valid() with { DurationMinutes = 0 }, 400, "MSG04", null },
        { "UT-APC-40 Thời lượng 481 phút", "guest", Valid() with { DurationMinutes = 481 }, 400, "MSG04", null },
        // 7. Service / time rules
        { "UT-APC-41 Dịch vụ không phải khám lần đầu", "guest", Valid() with { ServiceId = 2 }, 400, "MSG110", null },
        { "UT-APC-42 Dịch vụ ngừng hoạt động", "guest", Valid() with { ServiceId = 3 }, 400, "MSG110", null },
        { "UT-APC-43 Dịch vụ thuộc chi nhánh khác", "guest", Valid() with { ServiceId = 4 }, 400, "MSG110", null },
        { "UT-APC-44 Giờ hẹn đã qua", "assistant", Valid() with { StartAt = Now.AddMinutes(-1) }, 409, "MSG25", null },
        { "UT-APC-45 Giờ hẹn đúng bây giờ", "assistant", Valid() with { StartAt = Now }, 409, "MSG25", null },
        { "UT-APC-46 Khách đặt giờ cách 59 phút (dưới 60)", "guest", Valid() with { StartAt = Now.AddMinutes(59) }, 400, "MSG24", null },
        { "UT-APC-47 Nhân viên đặt giờ cách 59 phút, không có lý do ngoại lệ", "assistant", Valid() with { StartAt = Now.AddMinutes(59) }, 400, "MSG24", null },
        // 8. Follow-up dossier
        { "UT-APC-48 Tái khám: hồ sơ điều trị không tồn tại", "dentist", Valid() with { Kind = "FollowUp", DossierId = Guid.NewGuid(), Attendee = new AppointmentAttendee(PatientIdPlaceholder, "A", null) }, 404, "MSG113", null },
        { "UT-APC-49 Tái khám: hồ sơ của bệnh nhân khác", "dentist", Valid() with { Kind = "FollowUp", DossierId = DossierOther, Attendee = new AppointmentAttendee(PatientIdPlaceholder, "A", null) }, 403, "MSG06", null },
        { "UT-APC-50 Tái khám: hồ sơ đã đóng", "dentist", Valid() with { Kind = "FollowUp", DossierId = DossierClosed, Attendee = new AppointmentAttendee(PatientIdPlaceholder, "A", null) }, 409, "MSG230", null },
        { "UT-APC-51 Tái khám: chưa xác nhận sự đồng ý của bệnh nhân", "dentist", Valid() with { Kind = "FollowUp", DossierId = DossierOpen, Attendee = new AppointmentAttendee(PatientIdPlaceholder, "A", null) }, 400, "APPOINTMENT_PATIENT_CONSENT_REQUIRED", "Vui lòng xác nhận đã có sự đồng ý của bệnh nhân trước khi đặt lịch." },
        { "UT-APC-52 Tái khám: không xác định được bệnh nhân", "dentist", Valid() with { Kind = "FollowUp", DossierId = DossierOpen }, 403, "MSG06", null },
    };

    /// <summary>The seeded patient has a database-generated id; cases that need it use this marker and the test swaps in the real id.</summary>
    private const int PatientIdPlaceholder = -1;

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects(string caseName, string actor, CreateAppointmentRequest request, int status, string code, string? message)
    {
        using var env = await NewEnv();
        if (request.Attendee?.PatientId == PatientIdPlaceholder) request = request with { Attendee = request.Attendee with { PatientId = env.PatientId } };
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Actor(actor), request, default));
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
        Assert.Equal(0, await env.Db.Appointments.CountAsync(x => x.BranchId == env.Branch && x.Id != env.SourceId));
    }

    // ---------- test environment (real PostgreSQL: staff bookings read branch membership from JSON collections) ----------
    /// <summary>Wraps the service: the static test data uses fixed ids (branch 1, service 1..4, dentist 1, dossiers, request key) which are mapped to the rows this test created.</summary>
    private sealed class BoundService(AppointmentService inner, Func<CreateAppointmentRequest, CreateAppointmentRequest> bind)
    {
        public Task<CreateAppointmentOutcome> CreateAsync(int? accountId, CreateAppointmentRequest request, CancellationToken ct) => inner.CreateAsync(accountId, bind(request), ct);
    }

    private sealed class Env : IDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public BoundService Service = null!;
        public required TestAvailability Availability;
        public required TestQueries Queries;
        public required int Assistant, Dentist, Outsider, Customer, Inactive, PatientId, Branch, DentistEmployeeId, Suffix;
        public required Guid RequestKey, SourceId;
        public DcmsDbContext Db => Pg.Db;
        public void Dispose() => Pg.Db.Dispose();
        public int? Actor(string name) => name switch
        {
            "guest" => null, "ghost" => 9999, "assistant" => Assistant, "dentist" => Dentist, "outsider" => Outsider,
            "customer" => Customer, "inactive" => Inactive, _ => throw new ArgumentException(name)
        };
        public Guid Map(Guid id) => id == Guid.Empty ? id : new Guid(BitConverter.GetBytes(Suffix).Concat(id.ToByteArray().Skip(4)).ToArray());
        /// <summary>The appointment this test created (the seeded source appointment is excluded).</summary>
        public Appointment Created() => Db.Appointments.Single(x => x.BranchId == Branch && x.Id != SourceId);
        public StaffAppointmentDetail DetailOf(AppointmentListItem item) => new(item with
        {
            Id = SourceId, BranchId = item.BranchId == 1 ? Branch : item.BranchId, DentistId = item.DentistId == 1 ? DentistEmployeeId : item.DentistId
        }, "Khách", null, null, true, null, null, Now);
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
        var otherBranch = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "assistant" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "dentist" + n, AccountRoles.Dentist, n);
        var outsider = await CreateTestKit.SeedStaffAsync(db, "outsider" + n, AccountRoles.ClinicAssistant, n, member: false);
        await CreateTestKit.SeedStaffAsync(db, "other" + n, AccountRoles.ClinicAssistant, otherBranch);
        var customer = new ApplicationUser { UserName = "customer" + n, FullName = "Khách Hàng", PhoneNumber = $"09{n:D8}", Email = "khach" + n + "@example.com", AccountType = "Public", IsActive = true };
        var inactive = new ApplicationUser { UserName = "inactive" + n, FullName = "Đã khóa", AccountType = "Public", IsActive = false };
        db.Users.AddRange(customer, inactive);
        var patient = new DCMS.Domain.Features.Patients.PatientProfile { PatientCode = "BN" + n, FullName = "Trần Thị Mai", NormalizedName = "TRAN THI MAI",
            BirthDate = new DateOnly(1985, 5, 5), CreatedAt = Now, UpdatedAt = Now };
        var other = new DCMS.Domain.Features.Patients.PatientProfile { PatientCode = "BM" + n, FullName = "Người khác", NormalizedName = "NGUOI KHAC", CreatedAt = Now, UpdatedAt = Now };
        db.Patients.AddRange(patient, other);
        await db.SaveChangesAsync();
        var dentistEmployee = await db.Employees.SingleAsync(x => x.AccountId == dentist.Id);
        var services = new[]
        {
            new SchedulingService { BranchId = n, Name = "Khám lần đầu", IsInitialExamination = true },
            new SchedulingService { BranchId = n, Name = "Trám răng", IsInitialExamination = false },
            new SchedulingService { BranchId = n, Name = "Dịch vụ ngừng", IsInitialExamination = false, Active = false },
            new SchedulingService { BranchId = otherBranch, Name = "Khám chi nhánh khác", IsInitialExamination = true }
        };
        db.Set<SchedulingService>().AddRange(services);
        db.PatientAccountLinks.Add(new PatientAccountLink { AccountId = customer.Id, PatientId = patient.Id, Status = PatientLinkStatus.Verified, RequestCode = "LK-" + n });
        var availability = new TestAvailability(); var queries = new TestQueries();
        var env = new Env { Pg = pg, Availability = availability, Queries = queries, Assistant = assistant.Id, Dentist = dentist.Id,
            Outsider = outsider.Id, Customer = customer.Id, Inactive = inactive.Id, PatientId = patient.Id, Branch = n, DentistEmployeeId = dentistEmployee.Id, Suffix = n,
            RequestKey = Guid.NewGuid(), SourceId = Guid.NewGuid() };
        env = new Env { Pg = pg, Availability = availability, Queries = queries, Assistant = assistant.Id, Dentist = dentist.Id,
            Outsider = outsider.Id, Customer = customer.Id, Inactive = inactive.Id, PatientId = patient.Id, Branch = n, DentistEmployeeId = dentistEmployee.Id, Suffix = n,
            RequestKey = env.RequestKey, SourceId = env.Map(SourceId) };
        db.TreatmentDossiers.AddRange(
            new TreatmentDossier { Id = env.Map(DossierOpen), Code = "HS1-" + n, PatientId = patient.Id, BranchId = n, DentistId = dentistEmployee.Id, State = "Open", Diagnosis = "d", CreatedAt = Now, UpdatedAt = Now },
            new TreatmentDossier { Id = env.Map(DossierClosed), Code = "HS2-" + n, PatientId = patient.Id, BranchId = n, DentistId = dentistEmployee.Id, State = "Closed", Diagnosis = "d", CreatedAt = Now, UpdatedAt = Now },
            new TreatmentDossier { Id = env.Map(DossierOther), Code = "HS3-" + n, PatientId = other.Id, BranchId = n, DentistId = dentistEmployee.Id, State = "Open", Diagnosis = "d", CreatedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync();
        db.Appointments.Add(new Appointment { Id = env.SourceId, Code = "SRC" + n, BranchId = n, DentistId = dentistEmployee.Id, ChairId = 1, AttendeeName = "Khách", Status = "NoShow",
            StartAt = Now.AddHours(-1), EndAt = Now.AddHours(2), CreatedAt = Now, BookerAccountId = customer.Id });
        await db.SaveChangesAsync();
        var serviceIds = services.Select(x => x.Id).ToArray();
        var inner = new AppointmentService(db, availability, CreateTestKit.NewRoleUserManager(db), new TestClock(Now), new TestBranchAccess(n), queries);
        var key = env.RequestKey;
        var local = env;
        CreateAppointmentRequest Bind(CreateAppointmentRequest r) => r with
        {
            BranchId = r.BranchId == 1 ? n : r.BranchId,
            DentistId = r.DentistId == 1 ? dentistEmployee.Id : r.DentistId,
            ServiceId = r.ServiceId is >= 1 and <= 4 ? serviceIds[r.ServiceId.Value - 1] : r.ServiceId,
            DossierId = r.DossierId.HasValue ? local.Map(r.DossierId.Value) : null,
            ReplacesAppointmentId = r.ReplacesAppointmentId.HasValue ? local.Map(r.ReplacesAppointmentId.Value) : null,
            RequestKey = r.RequestKey == Key ? key : r.RequestKey
        };
        env.Service = new BoundService(inner, Bind);
        return env;
    }

    // ---------- Success paths ----------
    [Fact]
    public async Task Create_UT_APC_53_guest_booking_is_saved_as_a_confirmed_app_appointment()
    {
        using var env = await NewEnv();
        var outcome = await env.Service.CreateAsync(null, Valid(), default);

        Assert.False(outcome.Replayed);
        Assert.Equal("MSG29", outcome.Result.Notification.Code);
        Assert.Equal("Confirmed", outcome.Result.Status);
        Assert.Equal(1, outcome.Result.Version);
        Assert.StartsWith("AP-", outcome.Result.Code);
        var saved = env.Created();
        Assert.Null(saved.BookerAccountId);
        Assert.Equal("App", saved.Source);
        Assert.Equal("Standard", saved.Kind);
        Assert.Equal("Lê Văn Nam", saved.AttendeeName);
        Assert.Equal("0987654321", saved.BookingInfo.BookerContact);
        Assert.Equal("Self", saved.BookingInfo.AttendeeRelation);
        Assert.Equal(Now.AddHours(48), saved.StartAt);
        Assert.Equal(Now.AddHours(48).AddMinutes(30), saved.EndAt);
        Assert.Equal("AppointmentConfirmed", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.Created().Id).EventType);
        Assert.Equal(1, await env.Db.AppointmentReceipts.CountAsync(x => x.RequestKey == env.RequestKey));
        Assert.True(env.Availability.LastCheck!.Public);
        Assert.Null(env.Availability.LastCheck.ChairId);
    }

    [Fact]
    public async Task Create_UT_APC_54_boundary_guest_booking_exactly_60_minutes_ahead_is_allowed()
    {
        using var env = await NewEnv();
        var outcome = await env.Service.CreateAsync(null, Valid() with { StartAt = Now.AddMinutes(60) }, default);
        Assert.Equal("Confirmed", outcome.Result.Status);
    }

    [Fact]
    public async Task Create_UT_APC_55_email_is_stored_lowercase_when_no_phone_is_given()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(null, Valid() with { Booker = new BookerContact("Nam", null, "Nam@Example.com") }, default);
        Assert.Equal("nam@example.com", env.Created().BookingInfo.BookerContact);
    }

    [Fact]
    public async Task Create_UT_APC_56_signed_in_customer_uses_the_account_contact_when_none_is_given()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Customer, Valid() with { Booker = null, Attendee = new AppointmentAttendee(null, "Khách Hàng", null) }, default);
        var saved = env.Created();
        Assert.Equal(env.Customer, saved.BookerAccountId);
        Assert.Equal("Khách Hàng", saved.BookingInfo.BookerName);
        Assert.Equal($"09{env.Branch:D8}", saved.BookingInfo.BookerContact);
    }

    [Fact]
    public async Task Create_UT_APC_57_customer_with_a_verified_self_link_books_for_the_linked_patient()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Customer, Valid(), default);
        var saved = env.Created();
        Assert.Equal(env.PatientId, saved.PatientId);
        Assert.Equal("Trần Thị Mai", saved.AttendeeName);
        Assert.Equal(new DateOnly(1985, 5, 5), saved.BookingInfo.AttendeeBirthDate);
    }

    [Fact]
    public async Task Create_UT_APC_58_customer_booking_for_a_relative_does_not_use_the_link()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Customer, Valid() with { AttendeeRelation = "Relative", Attendee = new AppointmentAttendee(null, "Con của tôi", null) }, default);
        var saved = env.Created();
        Assert.Null(saved.PatientId);
        Assert.Equal("Con của tôi", saved.AttendeeName);
    }

    [Fact]
    public async Task Create_UT_APC_59_staff_booking_defaults_to_the_phone_source_and_passes_the_chair()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Assistant, Valid() with { ChairId = 3 }, default);
        var saved = env.Created();
        Assert.Equal("Phone", saved.Source);
        Assert.Equal(3, saved.ChairId);
        Assert.False(env.Availability.LastCheck!.Public);
        Assert.Equal(3, env.Availability.LastCheck.ChairId);
    }

    [Fact]
    public async Task Create_UT_APC_60_staff_may_book_inside_60_minutes_with_an_exception_reason()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Assistant, Valid() with { StartAt = Now.AddMinutes(10), ExceptionReason = "Khách đang ở phòng khám" }, default);
        Assert.Equal("Khách đang ở phòng khám", env.Created().BookingInfo.ExceptionReason);
    }

    [Fact]
    public async Task Create_UT_APC_61_staff_without_a_service_may_book_with_a_reason_only()
    {
        using var env = await NewEnv();
        await Assert.ThrowsAsync<DCMS.Application.Common.BusinessException>(() => env.Service.CreateAsync(env.Assistant, Valid() with { ServiceId = null }, default));
    }

    [Fact]
    public async Task Create_UT_APC_62_staff_gives_a_patient_id_and_the_patient_record_overrides_the_typed_name()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(env.Assistant, Valid() with { Attendee = new AppointmentAttendee(env.PatientId, "Tên gõ sai", null) }, default);
        var saved = env.Created();
        Assert.Equal(env.PatientId, saved.PatientId);
        Assert.Equal("Trần Thị Mai", saved.AttendeeName);
    }

    [Fact]
    public async Task Create_UT_APC_63_availability_rejection_is_passed_through_and_nothing_is_saved()
    {
        using var env = await NewEnv();
        env.Availability.OnCheck = _ => throw new DCMS.Application.Common.BusinessException(409, DCMS.Application.Common.SystemMessages.Get("MSG25"));
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(null, Valid(), default));
        Assert.Equal("MSG25", error.Response.Code);
        Assert.Equal(0, await env.Db.Appointments.CountAsync(x => x.BranchId == env.Branch && x.Id != env.SourceId));
        Assert.Equal(0, await env.Db.AppointmentReceipts.CountAsync(x => x.RequestKey == env.RequestKey));
    }

    [Fact]
    public async Task Create_UT_APC_64_the_slot_returned_by_availability_decides_chair_and_time()
    {
        using var env = await NewEnv();
        var start = Now.AddHours(48);
        env.Availability.OnCheck = r => new(start.AddMinutes(15), start.AddMinutes(45), 7, r.DentistId, null, 1, null);
        await env.Service.CreateAsync(null, Valid(), default);
        var saved = env.Created();
        Assert.Equal(7, saved.ChairId);
        Assert.Equal(start.AddMinutes(15), saved.StartAt);
    }

    // ---------- Retry / idempotency ----------
    [Fact]
    public async Task Create_UT_APC_65_retry_with_the_same_key_and_body_replays_the_first_result()
    {
        using var env = await NewEnv();
        var first = await env.Service.CreateAsync(null, Valid(), default);
        var again = await env.Service.CreateAsync(null, Valid(), default);
        Assert.True(again.Replayed);
        Assert.Equal(first.Result.AppointmentId, again.Result.AppointmentId);
        Assert.Equal("MSG30", again.Result.Notification.Code);
        Assert.Equal(1, await env.Db.Appointments.CountAsync(x => x.BranchId == env.Branch && x.Id != env.SourceId));
    }

    [Fact]
    public async Task Create_UT_APC_66_same_key_with_another_start_time_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(null, Valid(), default);
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(null, Valid() with { StartAt = Now.AddHours(50) }, default));
        Assert.Equal((409, "MSG111"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Create_UT_APC_67_same_key_used_by_another_contact_is_MSG111()
    {
        using var env = await NewEnv();
        await env.Service.CreateAsync(null, Valid(), default);
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(null, Valid() with { Booker = new BookerContact("Nam", "0912345678", null) }, default));
        Assert.Equal("MSG111", error.Response.Code);
    }

    // ---------- Follow-up ----------
    [Fact]
    public async Task Create_UT_APC_68_dentist_books_a_follow_up_of_an_open_dossier_with_patient_consent()
    {
        using var env = await NewEnv();
        var request = Valid() with { Kind = "FollowUp", DossierId = DossierOpen, PatientConsentConfirmed = true, Attendee = new AppointmentAttendee(env.PatientId, "A", null) };
        await env.Service.CreateAsync(env.Dentist, request, default);
        var saved = env.Created();
        Assert.Equal(env.Map(DossierOpen), saved.DossierId);
        Assert.Equal("FollowUp", saved.Kind);
        Assert.Equal(env.PatientId, saved.PatientId);
    }

    [Fact]
    public async Task Create_UT_APC_69_follow_up_skips_the_initial_examination_service_check()
    {
        using var env = await NewEnv();
        var request = Valid() with { Kind = "FollowUp", DossierId = DossierOpen, PatientConsentConfirmed = true, ServiceId = 2, Attendee = new AppointmentAttendee(env.PatientId, "A", null) };
        await env.Service.CreateAsync(env.Dentist, request, default);
        Assert.Equal(env.Db.Set<SchedulingService>().Single(x => x.BranchId == env.Branch && x.Name == "Trám răng").Id, env.Created().ServiceId);
    }

    // ---------- Family member ----------
    [Fact]
    public async Task Create_UT_APC_70_family_member_not_in_the_customers_list_is_404()
    {
        using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Customer, Valid() with { FamilyMemberId = Guid.NewGuid(), AttendeeRelation = "Relative" }, default));
        Assert.Equal((404, "FAMILY_MEMBER_NOT_FOUND"), (error.Status, error.Response.Code));
        Assert.Equal("Không tìm thấy người thân trong danh sách của bạn.", error.Message);
    }

    [Fact]
    public async Task Create_UT_APC_71_family_member_belonging_to_another_account_is_404()
    {
        using var env = await NewEnv();
        var member = new DCMS.Domain.Features.FamilyMembers.FamilyMember { OwnerAccountId = env.Assistant, FullName = "Người nhà", Relationship = "Con" };
        env.Db.Add(member); await env.Db.SaveChangesAsync();
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Customer, Valid() with { FamilyMemberId = member.Id, AttendeeRelation = "Relative" }, default));
        Assert.Equal("FAMILY_MEMBER_NOT_FOUND", error.Response.Code);
    }

    [Fact]
    public async Task Create_UT_APC_72_deleted_family_member_is_404()
    {
        using var env = await NewEnv();
        var member = new DCMS.Domain.Features.FamilyMembers.FamilyMember { OwnerAccountId = env.Customer, FullName = "Người nhà", Relationship = "Con", DeletedAt = Now };
        env.Db.Add(member); await env.Db.SaveChangesAsync();
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Customer, Valid() with { FamilyMemberId = member.Id, AttendeeRelation = "Relative" }, default));
        Assert.Equal((404, "Người thân không còn khả dụng."), (error.Status, error.Message));
    }

    [Fact]
    public async Task Create_UT_APC_73_family_member_name_and_birth_date_replace_the_typed_attendee()
    {
        using var env = await NewEnv();
        var member = new DCMS.Domain.Features.FamilyMembers.FamilyMember { OwnerAccountId = env.Customer, FullName = "Bé Na", Relationship = "Con", BirthDate = new DateOnly(2018, 3, 3) };
        env.Db.Add(member); await env.Db.SaveChangesAsync();
        await env.Service.CreateAsync(env.Customer, Valid() with { FamilyMemberId = member.Id, AttendeeRelation = "Relative", Attendee = new AppointmentAttendee(null, "Gõ sai", null) }, default);
        var saved = env.Created();
        Assert.Equal(member.Id, saved.FamilyMemberId);
        Assert.Equal("Bé Na", saved.AttendeeName);
        Assert.Equal(new DateOnly(2018, 3, 3), saved.BookingInfo.AttendeeBirthDate);
    }

    // ---------- Replacement of a no-show ----------
    private static AppointmentListItem Source(string status = "NoShow", int startHours = -1, int endHours = 2, int branch = 1, int dentist = 1) =>
        new(SourceId, "AP-SRC", branch, "CN", Now.AddHours(startHours), Now.AddHours(endHours), 180, dentist, "BS", 1, "Ghế", null, "Khách", "Self", "Standard", "App", status, 2);

    private static CreateAppointmentRequest Replacement() => Valid() with
    {
        Kind = "WalkIn", Source = "InPerson", ReplacesAppointmentId = SourceId, StartAt = Now.AddMinutes(10), ExceptionReason = "Khách tới trễ"
    };


    [Fact]
    public async Task Create_UT_APC_74_replacing_a_no_show_in_its_window_creates_the_walk_in_and_records_the_source()
    {
        using var env = await NewEnv();
        env.Queries.Detail = env.DetailOf(Source());
        await env.Service.CreateAsync(env.Assistant, Replacement(), default);
        var saved = env.Created();
        Assert.Equal("WalkIn", saved.Kind);
        Assert.Equal("InPerson", saved.Source);
        Assert.Contains("replacesAppointmentId", env.Db.AppointmentOutboxEvents.Single(x => x.AggregateId == env.Created().Id).PayloadJson);
    }

    [Fact]
    public async Task Create_UT_APC_75_source_appointment_of_another_branch_than_the_staff_permission_is_MSG06()
    {
        using var env = await NewEnv();
        env.Queries.Detail = env.DetailOf(Source(branch: 9));
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Assistant, Replacement(), default));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Theory]
    [InlineData("UT-APC-76 Lịch gốc chưa ở trạng thái Vắng mặt", "Confirmed", -1, 2, 1, 1)]
    [InlineData("UT-APC-77 Lịch gốc chưa bắt đầu", "NoShow", 1, 2, 1, 1)]
    [InlineData("UT-APC-78 Lịch gốc đã kết thúc (EndAt = hiện tại)", "NoShow", -3, 0, 1, 1)]
    [InlineData("UT-APC-79 Bác sĩ khác lịch gốc", "NoShow", -1, 2, 1, 5)]
    public async Task Create_rejects_a_replacement_that_does_not_fit_the_source(string caseName, string status, int startHours, int endHours, int branch, int dentist)
    {
        using var env = await NewEnv();
        env.Queries.Detail = env.DetailOf(Source(status, startHours, endHours, branch, dentist));
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Assistant, Replacement(), default));
        Assert.Equal((409, "APPOINTMENT_REPLACEMENT_UNAVAILABLE"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Create_UT_APC_80_replacement_that_runs_past_the_source_end_is_rejected()
    {
        using var env = await NewEnv();
        env.Queries.Detail = env.DetailOf(Source(endHours: 0));
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Assistant, Replacement() with { StartAt = Now.AddMinutes(-30) }, default));
        Assert.Equal("APPOINTMENT_REPLACEMENT_UNAVAILABLE", error.Response.Code);
    }

    [Fact]
    public async Task Create_UT_APC_81_source_that_already_has_a_visit_cannot_be_replaced()
    {
        using var env = await NewEnv();
        env.Queries.Detail = env.DetailOf(Source());
        env.Db.Visits.Add(new Visit { AppointmentId = env.SourceId, PatientId = env.PatientId, BranchId = env.Branch, DentistId = env.DentistEmployeeId, ArrivalAt = Now, RecordedBy = env.Assistant });
        await env.Db.SaveChangesAsync();
        var error = await CreateTestKit.ErrorOf(() => env.Service.CreateAsync(env.Assistant, Replacement(), default));
        Assert.Equal("APPOINTMENT_REPLACEMENT_UNAVAILABLE", error.Response.Code);
    }
}
