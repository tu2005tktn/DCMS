using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ReceptionClock : TimeProvider
{
    // Freeze within the valid JWT lifetime and leave enough same-day time to test quick booking at midnight.
    public DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds());
    public override DateTimeOffset GetUtcNow() => Now;
}
public class ReceptionFixture : ApiFixture
{
    public ReceptionClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:AccessTokenMinutes"] = "60" }));
        builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock); });
    }
}

public sealed class ReceptionApiTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    internal sealed record Setup(int BranchId, int DentistId, int AssistantId, int ChairId, int ServiceId, int PatientId, Guid AppointmentId);
    private DateOnly Today => DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime);
    internal async Task<HttpClient> Login(string name = "assistant.demo", ApiFixture? targetFixture = null)
    {
        var activeFixture = targetFixture ?? fixture;
        var client = activeFixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, activeFixture.Password, "Reception API test"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    internal async Task<Setup> Prepare(bool linked = true, int dayOffset = 0, ReceptionFixture? targetFixture = null)
    {
        var activeFixture = targetFixture ?? fixture;
        using var scope = activeFixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        // Cases share demo employees in this fixture's isolated schema. Retire only previous case reservations
        // before arranging a new case so cross-branch staff occupancy remains a real global check.
        await db.Set<ResourceReservation>().ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
        var assistant = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "assistant.demo" select e).SingleAsync();
        var dual = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dual.demo" select e).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC reception " + Guid.NewGuid().ToString("N") }; db.Add(branch); await db.SaveChangesAsync();
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id }, new SchedulingMember { BranchId = branch.Id, EmployeeId = dual.Id, CanDentist = true });
        var chairs = Enumerable.Range(1, 3).Select(n => new SchedulingChair { BranchId = branch.Id, Name = $"Ghế {n}", OpenMinute = 0, CloseMinute = 1440 }).ToArray(); db.AddRange(chairs);
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", IsInitialExamination = true }; db.Add(service);
        var patient = new PatientProfile { PatientCode = "RX-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC người đến", Phone = "0912345678", CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now };
        db.Add(patient); await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(activeFixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(dayOffset); var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        foreach (var employee in new[] { dentist, assistant, dual })
        {
            var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = employee.Id, WeekStart = monday, PublishedBy = dentist.AccountId, PublishedAt = fixture.Clock.Now };
            for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 0, EndMinute = 1440, Kind = "Work" }); db.Add(week);
        }
        var minute = (activeFixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).Hour * 60 + activeFixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).Minute) / 15 * 15;
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(minute).ToUniversalTime();
        var a = new Appointment { Code = "AP-" + Guid.NewGuid().ToString("N"), BranchId = branch.Id, PatientId = linked ? patient.Id : null,
            DentistId = dentist.Id, AssistantId = assistant.Id, ChairId = chairs[0].Id, ServiceId = service.Id, BookerName = "SYNTHETIC người đặt khác",
            BookerPhone = patient.Phone, AttendeeName = patient.FullName, AttendeeRelation = "Relative", StartAt = start, EndAt = start.AddMinutes(15), CreatedAt = fixture.Clock.Now };
        a.ReservationId = a.Id;
        db.Add(new ResourceReservation { Id = a.Id, Code = a.Code, BranchId = branch.Id, DentistId = dentist.Id, AssistantId = assistant.Id,
            ChairId = a.ChairId, ServiceId = service.Id, Start = a.StartAt, End = a.EndAt, AttendeeKey = linked ? $"patient:{patient.Id}" : null, AttendeeLabel = patient.FullName });
        db.Add(a); await db.SaveChangesAsync(); return new(branch.Id, dentist.Id, assistant.Id, a.ChairId, service.Id, patient.Id, a.Id);
    }
    private static CheckInRequest Command(Setup s) => new(s.PatientId, 1, Guid.NewGuid(), true, null, null, "InitialExamination", null, null);
    private static string Path(Setup s) => $"/api/reception/appointments/{s.AppointmentId}/check-in";
    internal async Task<VisitResult> Check(HttpClient client, Setup s, CheckInRequest? request = null)
    {
        var response = await client.PostAsJsonAsync(Path(s), request ?? Command(s));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<VisitResult>())!;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        if (code != null) Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Fact] public async Task CheckInLinksActualAttendeeAndExposesSameVisitToAssignedDentistWithoutSideEffects()
    {
        var s = await Prepare(false); using var client = await Login();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var patientsBefore = await db.Patients.CountAsync(); var linksBefore = await db.PatientAccountLinks.CountAsync();
        var v = await Check(client, s); Assert.Equal("Queued", v.Status); Assert.Equal("CheckedIn", v.AppointmentStatus); Assert.Equal(s.PatientId, v.PatientId);
        Assert.Equal(fixture.Clock.Now, v.ArrivalAt); Assert.Equal(v.ArrivalAt, v.RecordedAt);
        using var dentist = await Login("dentist.demo"); var read = await dentist.GetFromJsonAsync<VisitResult>($"/api/visits/{v.VisitId}");
        Assert.Equal(v.VisitId, read!.VisitId); Assert.Equal(s.AppointmentId, read.AppointmentId);
        var queue = await dentist.GetFromJsonAsync<ReceptionPage<VisitResult>>($"/api/reception/queue?branchId={s.BranchId}&date={Today:yyyy-MM-dd}");
        Assert.Single(queue!.Items); Assert.Equal(v.VisitId, queue.Items[0].VisitId);
        Assert.Equal(patientsBefore, await db.Patients.CountAsync()); Assert.Equal(linksBefore, await db.PatientAccountLinks.CountAsync());
        Assert.Single(await db.Set<VisitAudit>().Where(x => x.VisitId == v.VisitId).ToArrayAsync());
        Assert.Equal("Confirmed", (await db.Set<ResourceReservation>().AsNoTracking().SingleAsync(x => x.Id == s.AppointmentId)).Status);
        Assert.Equal(0, await db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == s.AppointmentId));
    }
    [Fact] public async Task ConcurrentSameAndDifferentKeysAndTwoStaffCreateOneVisitAndOneAudit()
    {
        var s = await Prepare(); using var first = await Login(); using var second = await Login("dual.demo"); var command = Command(s);
        var responses = await Task.WhenAll(first.PostAsJsonAsync(Path(s), command), first.PostAsJsonAsync(Path(s), command), second.PostAsJsonAsync(Path(s), command with { RequestKey = Guid.NewGuid() }));
        var visits = new List<VisitResult>(); foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); visits.Add((await response.Content.ReadFromJsonAsync<VisitResult>())!); }
        Assert.Single(visits.Select(x => x.VisitId).Distinct()); Assert.Single(visits, x => !x.Replayed);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Visits.CountAsync(x => x.AppointmentId == s.AppointmentId)); Assert.Equal(1, await db.Set<VisitAudit>().CountAsync(x => x.VisitId == visits[0].VisitId));
    }
    [Theory] [InlineData("InProgress")] [InlineData("Completed")]
    public async Task ReplayReturnsCurrentVisitWithoutResettingClinicalState(string status)
    {
        var s = await Prepare(); using var client = await Login(); var command = Command(s); var first = await Check(client, s, command);
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var v = await db.Visits.SingleAsync(x => x.Id == first.VisitId); v.Status = status; v.Version++; await db.SaveChangesAsync(); }
        var replay = await Check(client, s, command); Assert.Equal(first.VisitId, replay.VisitId); Assert.Equal(status, replay.Status); Assert.Equal(2, replay.VisitVersion); Assert.True(replay.Replayed);
        await Error(await client.PostAsJsonAsync(Path(s), command with { Note = "changed" }), HttpStatusCode.Conflict, "MSG111");
    }
    [Theory] [InlineData("Cancelled")] [InlineData("NoShow")] [InlineData("Completed")]
    public async Task IneligibleAppointmentNeverCreatesVisit(string status)
    {
        var s = await Prepare(); using var client = await Login();
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); (await db.Appointments.SingleAsync(x => x.Id == s.AppointmentId)).Status = status; await db.SaveChangesAsync(); }
        await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.Conflict, status == "NoShow" ? "MSG47" : "MSG42");
    }
    [Theory] [InlineData(-1)] [InlineData(1)]
    public async Task AppointmentOnAnotherClinicDayIsExplainedBeforeSubmitAndRejected(int day)
    {
        var s = await Prepare(dayOffset: day); using var client = await Login(); var detail = await client.GetFromJsonAsync<ReceptionDetail>($"/api/reception/appointments/{s.AppointmentId}");
        Assert.False(detail!.Appointment.CanCheckIn); Assert.NotNull(detail.Appointment.IneligibleReason);
        await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.Conflict, "MSG42");
    }
    [Fact] public async Task SamePhoneDoesNotMatchAnotherPatientAndIdentityConfirmationIsRequired()
    {
        var s = await Prepare(false); using var client = await Login(); int other;
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var p = new PatientProfile { PatientCode = Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC khác người", Phone = "0912345678", CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now }; db.Add(p); await db.SaveChangesAsync(); other = p.Id; }
        await Error(await client.PostAsJsonAsync(Path(s), Command(s) with { PatientId = other }), HttpStatusCode.Conflict, "MSG43");
        await Error(await client.PostAsJsonAsync(Path(s), Command(s) with { IdentityConfirmed = false }), HttpStatusCode.BadRequest);
        await Check(client, s);
    }
    [Fact] public async Task ArchivedPatientAndStaleVersionAreRejectedWithoutPartialWrite()
    {
        var s = await Prepare(); using var client = await Login(); await Error(await client.PostAsJsonAsync(Path(s), Command(s) with { ExpectedVersion = 2 }), HttpStatusCode.Conflict, "MSG111");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        (await db.Patients.SingleAsync(x => x.Id == s.PatientId)).IsArchived = true; await db.SaveChangesAsync();
        await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.Conflict, "MSG202"); Assert.Equal(0, await db.Visits.CountAsync(x => x.AppointmentId == s.AppointmentId));
    }
    [Fact] public async Task OptionalSourceDoesNotBlockArrivalButForgedCourseIsNotAccepted()
    {
        var s = await Prepare(); using var client = await Login(); var command = Command(s) with { Purpose = "TreatmentContinuation" };
        var courses = await client.GetFromJsonAsync<ReceptionCourse[]>($"/api/reception/appointments/{s.AppointmentId}/courses?patientId={s.PatientId}"); Assert.Empty(courses!);
        await Error(await client.PostAsJsonAsync(Path(s), command with { PriorCourseId = Guid.NewGuid() }), HttpStatusCode.NotFound, "MSG113");
        await Error(await client.PostAsJsonAsync(Path(s), command with { PriorCourseId = Guid.NewGuid() }), HttpStatusCode.ServiceUnavailable, "MSG144");
        var v = await Check(client, s, command); Assert.True(v.RequiresClinicalSourceReview); Assert.Null(v.Source);
    }
    [Fact] public async Task AppointmentFollowUpSourceIsCarriedByServer()
    {
        var s = await Prepare(); var source = Guid.NewGuid(); using var client = await Login();
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); (await db.Appointments.SingleAsync(x => x.Id == s.AppointmentId)).FollowUpId = source; await db.SaveChangesAsync(); }
        var v = await Check(client, s, Command(s) with { Purpose = "TreatmentContinuation" }); Assert.Equal(source, v.Source!.FollowUpId);
    }
    [Fact] public async Task BackdatingRequiresExplicitCurrentGrantAndReason()
    {
        var s = await Prepare(); using var client = await Login(); var arrival = fixture.Clock.Now.AddMinutes(-1);
        var command = Command(s) with { ArrivalAt = arrival, ArrivalReason = "Đã đến trước khi nhập lịch" };
        await Error(await client.PostAsJsonAsync(Path(s), command), HttpStatusCode.Forbidden);
        using (var scope = fixture.Services.CreateScope()) { var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await users.FindByNameAsync("assistant.demo"))!; Assert.True((await users.AddClaimAsync(user, new Claim("permission", "reception.backdate"))).Succeeded); }
        try {
            await Error(await client.PostAsJsonAsync(Path(s), command with { ArrivalReason = null }), HttpStatusCode.BadRequest);
            await Error(await client.PostAsJsonAsync(Path(s), command with { ArrivalAt = fixture.Clock.Now.AddSeconds(1) }), HttpStatusCode.BadRequest);
            var v = await Check(client, s, command); Assert.Equal(arrival, v.ArrivalAt); Assert.Equal(fixture.Clock.Now, v.RecordedAt);
        } finally { using var scope = fixture.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); await users.RemoveClaimAsync((await users.FindByNameAsync("assistant.demo"))!, new Claim("permission", "reception.backdate")); }
    }
    [Theory] [InlineData("guest.demo")] [InlineData("admin.demo")] [InlineData("dentist.demo")] [InlineData("owner.demo")]
    public async Task NonReceptionActorsCannotCheckIn(string name)
    {
        var s = await Prepare(); using var client = await Login(name); await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.Forbidden);
    }
    [Fact] public async Task DentistOwnScopeAndRevokedBranchApplyToReadAndReplay()
    {
        var s = await Prepare(); using var client = await Login(); var command = Command(s); var v = await Check(client, s, command);
        using var owner = await Login("owner.demo"); await Error(await owner.GetAsync($"/api/visits/{v.VisitId}"), HttpStatusCode.Forbidden);
        using var dual = await Login("dual.demo"); using var dentist = await Login("dentist.demo");
        var queue = await dentist.GetFromJsonAsync<ReceptionPage<VisitResult>>($"/api/reception/queue?branchId={s.BranchId}&date={Today:yyyy-MM-dd}&dentistId={s.AssistantId}"); Assert.Single(queue!.Items);
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); (await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == s.BranchId && x.EmployeeId == s.AssistantId)).Active = false; await db.SaveChangesAsync(); }
        await Error(await client.GetAsync($"/api/visits/{v.VisitId}"), HttpStatusCode.Forbidden); await Error(await client.PostAsJsonAsync(Path(s), command), HttpStatusCode.Forbidden);
    }
    [Theory] [InlineData("chair")] [InlineData("dentist")] [InlineData("assistant")] [InlineData("patient")]
    public async Task RecheckBlocksOverlappingResourcesAndLeavesNoPartialReception(string kind)
    {
        var s = await Prepare(); using var client = await Login();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var a = await db.Appointments.SingleAsync(x => x.Id == s.AppointmentId);
        var dual = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dual.demo" select e.Id).SingleAsync();
        var otherChair = await db.Set<SchedulingChair>().Where(x => x.BranchId == s.BranchId && x.Id != s.ChairId).Select(x => x.Id).FirstAsync();
        db.Add(new ResourceReservation { Code = "RX-" + Guid.NewGuid().ToString("N"), BranchId = s.BranchId, DentistId = kind == "dentist" ? s.DentistId : dual,
            AssistantId = kind == "assistant" ? s.AssistantId : null, ChairId = kind == "chair" ? s.ChairId : otherChair,
            Start = a.StartAt, End = a.EndAt, AttendeeKey = kind == "patient" ? $"patient:{s.PatientId}" : null }); await db.SaveChangesAsync();
        await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.Conflict, kind == "chair" ? "MSG28" : "MSG27");
        Assert.Equal(0, await db.Visits.CountAsync(x => x.AppointmentId == s.AppointmentId)); Assert.Equal("Confirmed", (await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == s.AppointmentId)).Status);
    }
    [Fact] public async Task QuickBookingUsesExistingCommandThenCheckInAndResourceConflictsAreRejected()
    {
        var s = await Prepare(); using var client = await Login();
        var available = (await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={Today:yyyy-MM-dd}&durationMinutes=15"))!;
        var slot = available.Slots.First(x => x.Start > fixture.Clock.Now);
        var booking = new CreateAppointmentRequest(s.BranchId, new BookerContact("SYNTHETIC người đặt", "0912345678", null), new AppointmentAttendee(s.PatientId, "SYNTHETIC người đến", null),
            "Relative", "WalkIn", "InPerson", s.ServiceId, null, s.DentistId, null, slot.ChairId, slot.Start, 15, null, "Vãng lai còn nguồn lực", Guid.NewGuid(), slot.ScheduleVersion, null, slot.BookingSettingsVersion);
        var response = await client.PostAsJsonAsync("/api/appointments", booking); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var created = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        await Error(await client.PostAsJsonAsync("/api/appointments", booking with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG27");
        var v = await Check(client, s with { AppointmentId = created.AppointmentId }); Assert.Equal(created.AppointmentId, v.AppointmentId);
    }
    [Fact] public async Task CancelRaceNeverLeavesCancelledAppointmentWithAQueuedVisit()
    {
        var s = await Prepare(); using var client = await Login();
        using (var arrangeScope = fixture.Services.CreateScope())
        {
            var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var a = await arrangeDb.Appointments.SingleAsync(x => x.Id == s.AppointmentId);
            var r = await arrangeDb.Set<ResourceReservation>().SingleAsync(x => x.Id == a.ReservationId);
            a.StartAt = r.Start = a.StartAt.AddMinutes(15); a.EndAt = r.End = a.EndAt.AddMinutes(15); await arrangeDb.SaveChangesAsync();
        }
        var cancel = client.PostAsJsonAsync($"/api/appointments/{(await client.GetFromJsonAsync<ReceptionDetail>($"/api/reception/appointments/{s.AppointmentId}"))!.Appointment.Code}/cancel",
            new { expectedVersion = 1, requestKey = Guid.NewGuid(), reason = "SYNTHETIC cancel race", requesterVerification = "Đã đối chiếu" });
        var check = client.PostAsJsonAsync(Path(s), Command(s)); var responses = await Task.WhenAll(cancel, check);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var status = (await db.Appointments.SingleAsync(x => x.Id == s.AppointmentId)).Status;
        Assert.Contains(status, new[] { "Cancelled", "CheckedIn" });
        Assert.Equal(status == "Cancelled" ? 0 : 1, await db.Visits.CountAsync(x => x.AppointmentId == s.AppointmentId));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact] public async Task SaveFailureRollsBackVisitReceiptAuditAndAppointmentTogether()
    {
        var s = await Prepare(); using var client = await Login();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reception_fail_audit() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN RAISE EXCEPTION 'SYNTHETIC audit failure'; END $fn$;
            CREATE TRIGGER reception_fail BEFORE INSERT ON "VISIT_AUDIT" FOR EACH ROW EXECUTE FUNCTION reception_fail_audit();
            """);
        try
        {
            await Error(await client.PostAsJsonAsync(Path(s), Command(s)), HttpStatusCode.ServiceUnavailable, "MSG144");
            Assert.Equal(0, await db.Visits.CountAsync(x => x.AppointmentId == s.AppointmentId));
            Assert.Equal("Confirmed", (await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == s.AppointmentId)).Status);
            Assert.Equal(0, await db.AppointmentChanges.CountAsync(x => x.AppointmentId == s.AppointmentId));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reception_fail ON \"VISIT_AUDIT\"; DROP FUNCTION reception_fail_audit();"); }
        await Check(client, s);
    }

    [Fact] public async Task CourseLookupRejectsAnotherPatientBeforeCallingAnUnavailableAdapter()
    {
        var s = await Prepare(false); using var client = await Login(); int other;
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); other = await db.Patients.Where(x => x.Id != s.PatientId).Select(x => x.Id).FirstAsync(); }
        await Error(await client.GetAsync($"/api/reception/appointments/{s.AppointmentId}/courses?patientId={other}"), HttpStatusCode.Conflict, "MSG43");
    }

    [Fact] public async Task LegacyMigrationAdoptsExistingVisitAndHistoryAndSupportsNewCheckInAndRollback()
    {
        await using var legacy = new ReceptionFixture(); await legacy.InitializeAsync();
        var s = await Prepare(targetFixture: legacy); using var client = await Login(targetFixture: legacy); var command = Command(s);
        var original = await Check(client, s, command);
        using var scope = legacy.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var source = Guid.NewGuid();
        // Arrange the exact old column/constraint names in a private schema, with an existing attendance.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "VISIT" RENAME COLUMN "ArrivalAt" TO "ActualArrivalAt";
            ALTER TABLE "VISIT" RENAME COLUMN "RecordedAt" TO "EnteredAt";
            ALTER TABLE "VISIT" RENAME COLUMN "RecordedBy" TO "EnteredBy";
            ALTER TABLE "VISIT" RENAME CONSTRAINT "FK_VISIT_USER_ACCOUNT_RecordedBy" TO "FK_VISIT_USER_ACCOUNT_EnteredBy";
            ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Arrival" TO "CK_VISIT_ARRIVAL";
            ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Status" TO "CK_VISIT_STATUS";
            ALTER TABLE "VISIT" RENAME CONSTRAINT "CK_VISIT_Purpose" TO "CK_VISIT_PURPOSE";
            ALTER INDEX "IX_VISIT_RecordedBy" RENAME TO "IX_VISIT_EnteredBy";
            ALTER TABLE "VISIT" ADD COLUMN "Code" varchar(40) NOT NULL DEFAULT 'legacy-visit';
            ALTER TABLE "VISIT" ADD COLUMN "IdentityNote" varchar(500) NOT NULL DEFAULT 'Legacy verified identity';
            ALTER TABLE "VISIT" ADD COLUMN "QueueOrder" bigint NOT NULL DEFAULT 1;
            ALTER TABLE "VISIT" ADD COLUMN "DossierId" uuid, ADD COLUMN "ItemId" uuid, ADD COLUMN "StepId" uuid, ADD COLUMN "ActivityId" uuid, ADD COLUMN "AdditionalConcern" varchar(1000);
            ALTER TABLE "VISIT" DROP COLUMN "SourceJson", DROP COLUMN "RequiresClinicalSourceReview";
            ALTER TABLE "VISIT_AUDIT" ADD COLUMN "Reason" varchar(500) NOT NULL DEFAULT 'Legacy audit';
            ALTER TABLE "RECEPTION_RECEIPT" RENAME TO "RECEPTION_COMMAND_RECEIPT";
            ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "PK_RECEPTION_RECEIPT" TO "PK_RECEPTION_COMMAND_RECEIPT";
            ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_RECEIPT_USER_ACCOUNT_ActorAccountId" TO "FK_RECEPTION_COMMAND_RECEIPT_USER_ACCOUNT_ActorAccountId";
            ALTER TABLE "RECEPTION_COMMAND_RECEIPT" RENAME CONSTRAINT "FK_RECEPTION_RECEIPT_VISIT_VisitId" TO "FK_RECEPTION_COMMAND_RECEIPT_VISIT_VisitId";
            ALTER INDEX "IX_RECEPTION_RECEIPT_VisitId" RENAME TO "IX_RECEPTION_COMMAND_RECEIPT_VisitId";
            CREATE TABLE "CLINICAL_SOURCE_REFERENCE" ("Id" uuid PRIMARY KEY, "PatientId" integer, "BranchId" integer, "Label" text, "Blocker" text);
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId"='20261025163452_ReceptionVisits';
            INSERT INTO "__EFMigrationsHistory" VALUES ('20261025075305_ReceptionVisits','10.0.0');
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"VISIT\" SET \"Purpose\"='TreatmentContinuation', \"DossierId\"={source}, \"AdditionalConcern\"='Legacy concern' WHERE \"Id\"={original.VisitId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CLINICAL_SOURCE_REFERENCE\" VALUES ({source},{s.PatientId},{s.BranchId},'Legacy course','Needs clinical review')");
        await db.Database.MigrateAsync();
        var adopted = await client.GetFromJsonAsync<VisitResult>($"/api/visits/{original.VisitId}");
        Assert.Equal(original.VisitId, adopted!.VisitId); Assert.Equal(original.ArrivalAt, adopted.ArrivalAt); Assert.Equal(original.RecordedAt, adopted.RecordedAt);
        Assert.Equal(source, adopted.Source!.PriorCourseId); Assert.Equal("Needs clinical review", adopted.Source.Blocker); Assert.Equal("Legacy concern", adopted.Note);
        Assert.Equal(original.VisitId, (await Check(client, s, command)).VisitId);
        Assert.Equal(1, await db.Set<VisitAudit>().CountAsync(x => x.VisitId == original.VisitId));
        var a = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == s.AppointmentId);
        a.Id = a.ReservationId = Guid.NewGuid(); a.Code = "AP-" + a.Id.ToString("N"); a.Status = "Confirmed"; a.Version = 1; a.StartAt = a.StartAt.AddMinutes(15); a.EndAt = a.EndAt.AddMinutes(15);
        db.Add(new ResourceReservation { Id = a.Id, Code = a.Code, BranchId = a.BranchId, DentistId = a.DentistId, AssistantId = a.AssistantId,
            ChairId = a.ChairId, ServiceId = a.ServiceId, Start = a.StartAt, End = a.EndAt, AttendeeKey = $"patient:{s.PatientId}" }); db.Add(a); await db.SaveChangesAsync();
        await Check(client, s with { AppointmentId = a.Id });
        Assert.Equal(2, await db.Visits.CountAsync());
        var previous = db.Database.GetMigrations().Last(x => x != "20261025163452_ReceptionVisits");
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.MigrateAsync(); // Restore the current model after a data-preserving legacy rollback.
        Assert.Equal(2, await db.Visits.CountAsync());
        Assert.Equal(original.VisitId, (await client.GetFromJsonAsync<VisitResult>($"/api/visits/{original.VisitId}"))!.VisitId);
    }
}
