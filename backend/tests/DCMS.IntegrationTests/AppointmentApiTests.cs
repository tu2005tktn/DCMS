using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class AppointmentApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(int BranchId, int DentistId, int ServiceId, DateOnly Date);

    private async Task<Setup> PrepareAsync(DateOnly? targetDate = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" select new { employee.Id, employee.AccountId }).SingleAsync();
        var assistant = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "assistant.demo" select employee.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "UC04 fixture " + Guid.NewGuid() };
        db.Add(branch); await db.SaveChangesAsync();
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant });
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", PublicDurationMinutes = 30 };
        db.Add(service);
        db.Add(new SchedulingChair { BranchId = branch.Id, Name = "Ghế 1", OpenMinute = 0, CloseMinute = 1440 });
        var date = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = dentist.Id, WeekStart = monday,
            PublishedBy = dentist.AccountId, PublishedAt = DateTimeOffset.UtcNow };
        for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod
        { WeekId = week.Id, Day = day, StartMinute = 0, EndMinute = 1440, Kind = "Work" });
        db.Add(week); await db.SaveChangesAsync();
        return new(branch.Id, dentist.Id, service.Id, date);
    }

    private static CreateAppointmentRequest Request(Setup setup, AvailabilitySlot slot, Guid? key = null,
        string attendee = "Nguyễn A", string relation = "Self") => new(setup.BranchId,
        new BookerContact("Nguyễn B", "0912345678", null), new AppointmentAttendee(null, attendee, null),
        relation, "Standard", "App", setup.ServiceId, null, setup.DentistId, null, null,
        slot.Start, 30, null, null, key ?? Guid.NewGuid(), slot.ScheduleVersion, null);

    private static async Task<AvailabilitySlot> SlotAsync(HttpClient client, Setup setup)
    {
        var response = await client.GetAsync($"/api/availability?branchId={setup.BranchId}&dentistId={setup.DentistId}&serviceId={setup.ServiceId}&date={setup.Date:yyyy-MM-dd}&durationMinutes=30");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = (await response.Content.ReadFromJsonAsync<AvailabilityResult>())!.Slots;
        var desired = slots.FirstOrDefault(x => x.Start.ToOffset(TimeSpan.FromHours(7)).TimeOfDay >= TimeSpan.FromHours(9));
        Assert.True(desired != null, "No slot available after 09:00 in the isolated test week.");
        return desired!;
    }

    [Fact]
    public async Task GuestCreatesConfirmedAppointmentAndRetryReturnsSameResult()
    {
        var setup = await PrepareAsync(); using var client = fixture.CreateClient();
        var command = Request(setup, await SlotAsync(client, setup));
        var first = await client.PostAsJsonAsync("/api/appointments", command);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = (await first.Content.ReadFromJsonAsync<AppointmentResult>())!;
        Assert.Equal("Confirmed", created.Status);
        Assert.Equal("MSG29", created.Notification.Code);
        var retry = await client.PostAsJsonAsync("/api/appointments", command);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var repeated = (await retry.Content.ReadFromJsonAsync<AppointmentResult>())!;
        Assert.Equal(created.AppointmentId, repeated.AppointmentId);
        Assert.Equal("MSG30", repeated.Notification.Code);
        var changed = await client.PostAsJsonAsync("/api/appointments", command with
        { Attendee = new AppointmentAttendee(null, "Người khác", null) });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Appointments.CountAsync(x => x.BranchId == setup.BranchId));
        Assert.Equal(1, await db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == created.AppointmentId));
        Assert.Equal(1, await db.Set<ResourceReservation>().CountAsync(x => x.BranchId == setup.BranchId));
        Assert.Equal(0, await db.Patients.CountAsync(x => x.FullName == "Nguyễn A"));
    }

    [Fact]
    public async Task CompetingRequestsCannotReserveSameDentistOrChair()
    {
        var setup = await PrepareAsync(); using var firstClient = fixture.CreateClient(); using var secondClient = fixture.CreateClient();
        var slot = await SlotAsync(firstClient, setup);
        var first = firstClient.PostAsJsonAsync("/api/appointments", Request(setup, slot, attendee: "Người 1"));
        var second = secondClient.PostAsJsonAsync("/api/appointments", Request(setup, slot, attendee: "Người 2"));
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Appointments.CountAsync(x => x.BranchId == setup.BranchId));
    }

    [Fact]
    public async Task PublicAccountMayBookRelativeWithoutPatientLink()
    {
        var setup = await PrepareAsync(); using var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("guest.demo", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.PostAsJsonAsync("/api/appointments", Request(setup, await SlotAsync(client, setup), relation: "Relative"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var appointment = await db.Appointments.SingleAsync(x => x.BranchId == setup.BranchId);
        Assert.Equal(session.User.Id, appointment.BookerAccountId);
        Assert.Null(appointment.PatientId);
        Assert.Equal("Relative", appointment.AttendeeRelation);
    }

    [Fact]
    public async Task VerifiedSelfPatientCanBeIdentifiedButRelativeCannotClaimThatPatientId()
    {
        var setup = await PrepareAsync();
        int patientId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var accountId = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
            var patient = new PatientProfile { PatientCode = "UC04-" + Guid.NewGuid().ToString("N"),
                FullName = "Tên hồ sơ đã xác minh", BirthDate = new DateOnly(1990, 5, 6),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.Add(patient); await db.SaveChangesAsync(); patientId = patient.Id;
            db.Add(new PatientAccountLink { RequestCode = "UC04-" + Guid.NewGuid().ToString("N")[..24],
                AccountId = accountId, PatientId = patientId, Status = PatientLinkStatus.Verified,
                RequestedAt = DateTimeOffset.UtcNow, DeclaredFullName = patient.FullName });
            await db.SaveChangesAsync();
        }
        using var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("guest.demo", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        var slot = await SlotAsync(client, setup);
        var other = Request(setup, slot, relation: "Relative") with
        { Attendee = new AppointmentAttendee(patientId, "Tên nhập từ client", null) };
        var denied = await client.PostAsJsonAsync("/api/appointments", other);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var accepted = await client.PostAsJsonAsync("/api/appointments", other with
        { AttendeeRelation = "Self", Attendee = new AppointmentAttendee(null, "Tên nhập từ client", null), RequestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var verify = fixture.Services.CreateScope();
        var appointment = await verify.ServiceProvider.GetRequiredService<DcmsDbContext>().Appointments
            .SingleAsync(x => x.BranchId == setup.BranchId);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal("Tên hồ sơ đã xác minh", appointment.AttendeeName);
        Assert.Equal(new DateOnly(1990, 5, 6), appointment.AttendeeBirthDate);
    }

    [Fact]
    public async Task PublicBookingInsideLeadTimeIsRejected()
    {
        var setup = await PrepareAsync(); using var client = fixture.CreateClient();
        var slot = await SlotAsync(client, setup);
        var command = Request(setup, slot) with { StartAt = DateTimeOffset.UtcNow.AddMinutes(30) };
        var response = await client.PostAsJsonAsync("/api/appointments", command);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG24", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Fact]
    public async Task StaffNearTermWalkInNeedsReasonAndStillReservesResources()
    {
        var local = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).AddMinutes(20);
        var candidate = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0,
            TimeSpan.FromHours(7)).AddMinutes(((local.Minute + 14) / 15) * 15);
        var setup = await PrepareAsync(DateOnly.FromDateTime(candidate.DateTime));
        using var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("assistant.demo", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        var command = new CreateAppointmentRequest(setup.BranchId, new BookerContact("Khách vãng lai", "0912345678", null),
            new AppointmentAttendee(null, "Người khám", null), "Self", "WalkIn", "InPerson", null,
            "Khám khẩn", setup.DentistId, null, null, candidate, 15, null, null, Guid.NewGuid(), 1, null);
        var denied = await client.PostAsJsonAsync("/api/appointments", command);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("MSG24", (await denied.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        var accepted = await client.PostAsJsonAsync("/api/appointments", command with { ExceptionReason = "Ca gấp đã được tiếp nhận" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var conflict = await client.PostAsJsonAsync("/api/appointments", command with
        { ExceptionReason = "Ca gấp đã được tiếp nhận", RequestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task PublicFollowUpWithoutVerifiedPatientIsRejected()
    {
        var setup = await PrepareAsync(); using var client = fixture.CreateClient();
        var command = Request(setup, await SlotAsync(client, setup)) with { Kind = "FollowUp", FollowUpId = Guid.NewGuid() };
        var response = await client.PostAsJsonAsync("/api/appointments", command);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Appointments.CountAsync(x => x.BranchId == setup.BranchId));
    }
}
