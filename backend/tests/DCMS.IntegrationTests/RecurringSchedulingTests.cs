using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class RecurringSchedulingTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly Monday = new(2032, 1, 5);
    private sealed record Setup(HttpClient Client, int Employee, int Actor, int Branch, int OtherBranch, int Service, int OtherService, int Chair);
    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }
    private async Task<Setup> Create()
    {
        using var admin = await Login("admin.demo");
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var name = "recurring." + suffix;
        var created = await admin.PostAsJsonAsync("/api/accounts", new CreateStaffRequest(name, fixture.Password,
            "Recurring fixture", "REC-" + suffix, suffix + "@example.invalid", null, ["Dentist"]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var employee = await db.Employees.SingleAsync(e => e.AccountId == account.Id);
        var branch = new SchedulingBranch { Name = "Recurring A " + suffix };
        var other = new SchedulingBranch { Name = "Recurring B " + suffix }; db.AddRange(branch, other); await db.SaveChangesAsync();
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = employee.Id, CanDentist = true }, new SchedulingMember { BranchId = other.Id, EmployeeId = employee.Id, CanDentist = true });
        var service = new SchedulingService { BranchId = branch.Id, Name = "30", PublicDurationMinutes = 30 };
        var otherService = new SchedulingService { BranchId = other.Id, Name = "30", PublicDurationMinutes = 30 };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "Chair A" };
        db.AddRange(service, otherService, chair, new SchedulingChair { BranchId = other.Id, Name = "Chair B" });
        await db.SaveChangesAsync();
        return new(await Login(name), employee.Id, account.Id, branch.Id, other.Id, service.Id, otherService.Id, chair.Id);
    }
    private static RecurringCommand Command(Setup s, int version = 0) => new(s.Employee, Monday, "Template", version,
        [new(s.Branch, 0, 480, 1020, "Work"), new(s.Branch, 0, 720, 780, "Break", "Lunch"), new(s.OtherBranch, 1, 540, 1020, "Work")], Guid.NewGuid());
    private static async Task<RecurringPublishResult> Publish(Setup s, RecurringCommand c)
    {
        var response = await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", c);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RecurringPublishResult>())!;
    }
    private async Task<AvailabilityResult> Slots(Setup s, DateOnly date, bool other = false)
    {
        using var client = fixture.CreateClient();
        return (await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={(other ? s.OtherBranch : s.Branch)}&dentistId={s.Employee}&serviceId={(other ? s.OtherService : s.Service)}&date={date:yyyy-MM-dd}&durationMinutes=30"))!;
    }
    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }
    [Fact]
    public async Task TemplateRepeatsWithWeekdayBranchesBreaksAndNotWorking()
    {
        var s = await Create(); using var client = s.Client;
        var published = await Publish(s, Command(s)); Assert.Equal(1, published.Schedule.Version);
        var later = Monday.AddDays(70);
        var slots = (await Slots(s, later)).Slots;
        Assert.NotEmpty(slots); Assert.All(slots, slot => Assert.Equal(1, slot.ScheduleVersion));
        Assert.DoesNotContain(slots, x => x.Start.Hour < 13 && x.End.Hour > 12);
        Assert.Empty((await Slots(s, later.AddDays(1))).Slots);
        Assert.NotEmpty((await Slots(s, later.AddDays(1), true)).Slots);
        Assert.Empty((await Slots(s, later.AddDays(2), true)).Slots);
        Assert.Empty((await Slots(s, Monday.AddDays(-7))).Slots);
    }
    [Fact]
    public async Task DatedAbsenceAndExtraSessionOverrideOnlyOneDateAndChangeBookingVersion()
    {
        var s = await Create(); using var client = s.Client;
        await Publish(s, Command(s));
        var date = Monday.AddDays(7);
        await Publish(s, new(s.Employee, date, "Exception", 1, [], Guid.NewGuid(), "Leave"));
        Assert.Empty((await Slots(s, date)).Slots);
        Assert.NotEmpty((await Slots(s, date.AddDays(7))).Slots);
        var extraDate = date.AddDays(2);
        await Publish(s, new(s.Employee, extraDate, "Exception", 2, [new(s.OtherBranch, 2, 480, 720, "Work")], Guid.NewGuid(), "Extra session"));
        Assert.NotEmpty((await Slots(s, extraDate, true)).Slots);
        Assert.Empty((await Slots(s, extraDate.AddDays(7), true)).Slots);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var start = new DateTimeOffset(Monday.AddDays(14).ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(7));
        var stale = await Assert.ThrowsAsync<BusinessException>(() => availability.CheckInTransactionAsync(new(s.Branch, s.Employee, s.Service, start, 30, s.Chair, null, null, 1), default));
        Assert.Equal("MSG111", stale.Response.Code);
    }
    [Fact]
    public async Task FutureBookingsBeyondPreviewRangeBlockBranchRemovalButLaterTemplatesStillApply()
    {
        var s = await Create(); using var client = s.Client;
        await Publish(s, Command(s));
        var later = Monday.AddDays(140);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var start = new DateTimeOffset(later.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(7));
            db.Add(new ResourceReservation { BranchId = s.Branch, DentistId = s.Employee, ChairId = s.Chair, ServiceId = s.Service,
                Code = Guid.NewGuid().ToString("N"), Start = start.ToUniversalTime(), End = start.AddMinutes(60).ToUniversalTime(), AttendeeLabel = "Synthetic", Status = "Confirmed" });
            await db.SaveChangesAsync();
        }
        var removal = Command(s, 1) with { Intervals = [new(s.OtherBranch, 0, 480, 1020, "Work")] };
        var preview = await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/preview", removal);
        Assert.Single((await preview.Content.ReadFromJsonAsync<SchedulePreview>())!.AffectedAppointments);
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", removal), HttpStatusCode.Conflict, "MSG38");
        Assert.NotEmpty((await Slots(s, later)).Slots);
        await Publish(s, Command(s, 1) with { Date = later });
        await Publish(s, removal with { ExpectedVersion = 2 });
        Assert.NotEmpty((await Slots(s, later)).Slots); // The later template protects the later booking.
        Assert.Empty((await Slots(s, Monday)).Slots);
    }
    [Fact]
    public async Task PublicationIsIdempotentVersionedAuthorizedAndRejectsInvalidPeriods()
    {
        var s = await Create(); using var client = s.Client; var c = Command(s);
        await Publish(s, c); Assert.Equal(1, (await Publish(s, c)).Schedule.Version);
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", c with { Reason = "changed" }), HttpStatusCode.Conflict, "MSG111");
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", c with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG111");
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", c with { ExpectedVersion = 1, RequestKey = Guid.NewGuid(), Intervals = [new(s.Branch, 0, 495, 720, "Work")] }), HttpStatusCode.BadRequest, "MSG110");
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", c with { ExpectedVersion = 1, RequestKey = Guid.NewGuid(), Kind = "Exception", Intervals = [] }), HttpStatusCode.BadRequest, "MSG110");
        using var other = await Login("dentist.demo");
        await Message(await other.PostAsJsonAsync("/api/working-schedules/recurring/publish", c), HttpStatusCode.Forbidden, "MSG06");
        using var admin = await Login("admin.demo");
        await Message(await admin.GetAsync($"/api/working-schedules/recurring?dentistId={s.Employee}&date=2032-01-05"), HttpStatusCode.Forbidden, "MSG06");
        var race = await Task.WhenAll(s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, 1)),
            s.Client.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, 1)));
        Assert.Single(race, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(race, r => r.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ManagerNeedsExplicitRightsAcrossEveryBranchAndCannotReadOrEraseOtherBranchHours()
    {
        var s = await Create(); using var client = s.Client;
        await Publish(s, Command(s));
        using var manager = await Login("owner.demo");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var owner = await (from e in db.Employees join a in db.Users on e.AccountId equals a.Id where a.UserName == "owner.demo" select e).SingleAsync();
            db.Add(new SchedulingMember { EmployeeId = owner.Id, BranchId = s.Branch, CanDentist = true, CanManageSchedules = true }); await db.SaveChangesAsync();
            await Message(await manager.GetAsync($"/api/working-schedules/recurring?dentistId={s.Employee}&date=2032-01-05"), HttpStatusCode.Forbidden, "MSG06");
            await Message(await manager.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, 1) with { Intervals = [] }), HttpStatusCode.Forbidden, "MSG06");
            db.Add(new SchedulingMember { EmployeeId = owner.Id, BranchId = s.OtherBranch, CanDentist = true, CanManageSchedules = true }); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/working-schedules/recurring?dentistId={s.Employee}&date=2032-01-05")).StatusCode);
    }
    [Fact]
    public async Task LegacyWeekIsPreservedAndNewTokensCannotCollideWithLegacyVersions()
    {
        var s = await Create(); using var client = s.Client;
        var legacy = new ScheduleCommand(s.Branch, s.Employee, Monday, 0, [new(0, 480, 720, "Work")], Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync("/api/working-schedules/publish", legacy)).StatusCode);
        Assert.NotEmpty((await Slots(s, Monday)).Slots); Assert.Empty((await Slots(s, Monday.AddDays(7))).Slots);
        var result = await Publish(s, Command(s, 1)); Assert.Equal(2, result.Schedule.Version);
        Assert.Single(result.Schedule.LegacyWeek);
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/publish", legacy with { ExpectedVersion = 1, RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG111");
        Assert.All((await Slots(s, Monday.AddDays(7))).Slots, slot => Assert.Equal(2, slot.ScheduleVersion));
        using var scope = fixture.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Set<WorkingWeek>().AnyAsync(x => x.EmployeeId == s.Employee));
    }
    [Fact]
    public async Task RangePreviewUsesProposedRecurringTemplateWithoutPublishingAndEnforcesLimit()
    {
        var s = await Create(); using var client = s.Client;
        var request = new RecurringPreviewRequest(Command(s), s.Branch, s.Service, Monday, Monday.AddDays(14), 75);
        var response = await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/preview-availability", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = (await response.Content.ReadFromJsonAsync<RecurringAvailabilityResult>())!.Days;
        Assert.Equal(15, days.Length); Assert.NotEmpty(days[14].Slots); Assert.Empty(days[1].Slots);
        Assert.Empty((await Slots(s, Monday)).Slots);
        await Message(await s.Client.PostAsJsonAsync("/api/working-schedules/recurring/preview-availability", request with { To = Monday.AddDays(31) }), HttpStatusCode.BadRequest, "MSG110");
    }
}
