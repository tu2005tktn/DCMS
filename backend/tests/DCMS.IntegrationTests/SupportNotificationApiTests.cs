using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Notifications;
using DCMS.Application.Features.SupportRequests;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class SupportNotificationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Seed(Guid AppointmentId, string AppointmentCode, string RequestCode,
        int GuestId, int BranchId);

    private async Task<HttpClient> Login(string userName)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(userName, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private async Task<Seed> SeedAsync(string kind)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var guest = await db.Users.SingleAsync(x => x.UserName == "guest.demo");
        var assistant = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "assistant.demo" select e).SingleAsync();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "dentist.demo" select e).SingleAsync();
        var branch = new SchedulingBranch { Name = "S12 fixture " + Guid.NewGuid() };
        db.Add(branch); await db.SaveChangesAsync();
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true });
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "Ghế S12", OpenMinute = 0, CloseMinute = 1440 };
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám S12", PublicDurationMinutes = 30 };
        db.AddRange(chair, service); await db.SaveChangesAsync();
        var id = Guid.NewGuid(); var start = DateTimeOffset.UtcNow.AddDays(3);
        db.Set<ResourceReservation>().Add(new ResourceReservation { Id = id, Code = "RS-" + id.ToString("N"),
            BranchId = branch.Id, DentistId = dentist.Id, ChairId = chair.Id, ServiceId = service.Id,
            Start = start, End = start.AddMinutes(30), AttendeeLabel = "Synthetic attendee", Status = "Confirmed" });
        var appointment = new Appointment { Id = id, ReservationId = id, Code = "AP-" + id.ToString("N"),
            BranchId = branch.Id, BookerAccountId = guest.Id, BookerName = guest.FullName,
            BookerPhone = guest.PhoneNumber, AttendeeName = "Synthetic attendee", DentistId = dentist.Id,
            ChairId = chair.Id, ServiceId = service.Id, StartAt = start, EndAt = start.AddMinutes(30),
            Status = "Confirmed", CreatedAt = DateTimeOffset.UtcNow };
        db.Appointments.Add(appointment);
        var request = new AppointmentSupportRequest { Code = "SR-" + Guid.NewGuid().ToString("N")[..12],
            AppointmentId = id, Kind = kind, Status = "Pending", RequestKey = Guid.NewGuid(),
            RequesterAccountId = guest.Id, RequesterName = guest.FullName, RequesterPhone = guest.PhoneNumber,
            Reason = "Synthetic request", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.AppointmentSupportRequests.Add(request);
        await db.SaveChangesAsync();
        return new(id, appointment.Code, request.Code, guest.Id, branch.Id);
    }

    [Fact]
    public async Task AppointmentFilterScopesRequestsBeforePaginationAndPreservesBranchAuthorization()
    {
        var target = await SeedAsync("Change");
        var other = await SeedAsync("Cancel");
        using var staff = await Login("assistant.demo");
        var filtered = (await staff.GetFromJsonAsync<SupportRequestPage>(
            $"/api/support-requests?appointmentId={target.AppointmentId}&statuses=Pending&branchId={target.BranchId}&pageSize=1"))!;
        Assert.Equal(1, filtered.Total);
        Assert.Equal(target.RequestCode, Assert.Single(filtered.Items).Id);
        var second = (await staff.GetFromJsonAsync<SupportRequestPage>(
            $"/api/support-requests?appointmentId={target.AppointmentId}&statuses=Pending&pageSize=1&page=2"))!;
        Assert.Equal(1, second.Total);
        Assert.Empty(second.Items);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var employee = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
                where u.UserName == "assistant.demo" select e).SingleAsync();
            var member = await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == other.BranchId && x.EmployeeId == employee.Id);
            member.Active = false;
            await db.SaveChangesAsync();
        }
        var deniedScope = (await staff.GetFromJsonAsync<SupportRequestPage>(
            $"/api/support-requests?appointmentId={other.AppointmentId}&statuses=Pending"))!;
        Assert.Equal(0, deniedScope.Total);
        Assert.Empty(deniedScope.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(
            $"/api/support-requests?appointmentId={other.AppointmentId}&branchId={other.BranchId}")).StatusCode);
        using var patient = await Login("guest.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await patient.GetAsync(
            $"/api/support-requests?appointmentId={target.AppointmentId}")).StatusCode);
    }

    [Fact]
    public async Task ResolveConflictKeepsRequestPendingAndAttendanceDoesNotChangeLifecycle()
    {
        var change = await SeedAsync("Change");
        using var staff = await Login("assistant.demo");
        var conflict = await staff.PostAsJsonAsync($"/api/support-requests/{change.RequestCode}/resolve",
            new ResolveSupportCommand(Guid.NewGuid(), 1, "Resolve", null, null));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (var scope = fixture.Services.CreateScope())
            Assert.Equal("Pending", (await scope.ServiceProvider.GetRequiredService<DcmsDbContext>()
                .AppointmentSupportRequests.SingleAsync(x => x.Code == change.RequestCode)).Status);

        var attendance = await SeedAsync("ConfirmToAttend");
        var resolved = await staff.PostAsJsonAsync($"/api/support-requests/{attendance.RequestCode}/resolve",
            new ResolveSupportCommand(Guid.NewGuid(), 1, "Resolve", "Đã xác nhận", null));
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using var verifyScope = fixture.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var appointment = await db.Appointments.SingleAsync(x => x.Id == attendance.AppointmentId);
        Assert.Equal("Confirmed", appointment.Status);
        Assert.Equal("Yes", appointment.ConfirmedToAttend);
        Assert.Equal("Resolved", (await db.AppointmentSupportRequests.SingleAsync(x => x.Code == attendance.RequestCode)).Status);
    }

    [Fact]
    public async Task VerifyCancelAndResolveReturnsFreshRequestVersionWithoutExtraRead()
    {
        var seed = await SeedAsync("Cancel");
        using var staff = await Login("assistant.demo");

        var before = (await staff.GetFromJsonAsync<SupportRequestDetail>(
            $"/api/support-requests/{seed.RequestCode}"))!;
        Assert.False(before.AppointmentActionCommitted);

        var verifiedResponse = await staff.PostAsJsonAsync($"/api/support-requests/{seed.RequestCode}/verify-requester",
            new VerifyRequesterCommand(Guid.NewGuid(), before.ExpectedVersion, "Đã xác minh qua số liên hệ gốc."));
        Assert.Equal(HttpStatusCode.OK, verifiedResponse.StatusCode);
        var verified = (await verifiedResponse.Content.ReadFromJsonAsync<SupportRequestDetail>())!;

        var changeKey = Guid.NewGuid();
        var changedResponse = await staff.PostAsJsonAsync($"/api/support-requests/{seed.RequestCode}/change-appointment",
            new StaffSupportChangeCommand(changeKey, verified.ExpectedVersion, "PatientRequest", "Bệnh nhân yêu cầu hủy.", null));
        Assert.Equal(HttpStatusCode.OK, changedResponse.StatusCode);
        var changed = (await changedResponse.Content.ReadFromJsonAsync<StaffSupportChangeResult>())!;
        Assert.True(changed.Ok);
        Assert.NotNull(changed.Request);
        Assert.True(changed.Request!.AppointmentActionCommitted);
        Assert.Equal(verified.ExpectedVersion + 1, changed.Request.ExpectedVersion);
        Assert.Equal("Cancelled", changed.Appointment!.Status);

        var replay = (await (await staff.PostAsJsonAsync(
            $"/api/support-requests/{seed.RequestCode}/change-appointment",
            new StaffSupportChangeCommand(changeKey, verified.ExpectedVersion, "PatientRequest", "Bệnh nhân yêu cầu hủy.", null)))
            .Content.ReadFromJsonAsync<StaffSupportChangeResult>())!;
        Assert.Equal(changed.Request.ExpectedVersion, replay.Request!.ExpectedVersion);

        var resolvedResponse = await staff.PostAsJsonAsync($"/api/support-requests/{seed.RequestCode}/resolve",
            new ResolveSupportCommand(Guid.NewGuid(), changed.Request.ExpectedVersion, "Resolve", "Đã hủy theo yêu cầu.", null));
        Assert.Equal(HttpStatusCode.OK, resolvedResponse.StatusCode);
        var resolved = (await resolvedResponse.Content.ReadFromJsonAsync<ResolveSupportResult>())!;
        Assert.True(resolved.Ok);
        Assert.Equal("Resolved", resolved.Request!.Status);
        Assert.Equal(changed.Request.ExpectedVersion + 1, resolved.Request.ExpectedVersion);

        var realtime = fixture.Realtime.Calls.Where(x => x.Method == "supportRequestUpdated"
            && x.Payload.GetProperty("requestId").GetString() == seed.RequestCode).ToArray();
        Assert.Equal(["RequesterVerified", "AppointmentCancelled", "Resolved"],
            realtime.Select(x => x.Payload.GetProperty("action").GetString()!).ToArray());
        Assert.All(realtime, call => Assert.Equal(seed.BranchId, call.BranchId));
        Assert.Equal([verified.ExpectedVersion, changed.Request.ExpectedVersion, resolved.Request.ExpectedVersion],
            realtime.Select(x => x.Payload.GetProperty("version").GetInt32()).ToArray());
    }

    [Fact]
    public async Task AppointmentOutboxCreatesOneNotificationAndCurrentVersionReminders()
    {
        var seed = await SeedAsync("General");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.AppointmentOutboxEvents.Add(new AppointmentOutboxEvent { AggregateId = seed.AppointmentId,
                EventType = "appointment.confirmed", PayloadJson = "{}", OccurredAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IAppointmentNotificationDispatcher>();
            await dispatcher.ProcessPendingAsync(default);
            await dispatcher.ProcessPendingAsync(default);
        }
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Single(await db.InAppNotifications.Where(x => x.AppointmentId == seed.AppointmentId).ToListAsync());
            Assert.Equal(2, await db.AppointmentReminders.CountAsync(x => x.AppointmentId == seed.AppointmentId && x.Status == "Pending"));
        }
        using var patient = await Login("guest.demo");
        var page = await patient.GetFromJsonAsync<ListNotificationsResult>("/api/notifications?limit=20");
        Assert.Contains(page!.Items, x => x.AppointmentId == seed.AppointmentId.ToString());
    }
}
