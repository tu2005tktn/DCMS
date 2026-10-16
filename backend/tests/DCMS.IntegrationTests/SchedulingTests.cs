using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ScheduleRulesTests
{
    private static readonly DateOnly Monday = new(2030, 1, 7);
    [Theory]
    [InlineData(480, 720, true)] [InlineData(720, 480, false)] [InlineData(480, 480, false)]
    [InlineData(481, 720, false)] [InlineData(0, 1440, true)] [InlineData(-15, 60, false)] [InlineData(0, 1455, false)]
    public void WorkBoundaries(int start, int end, bool valid) => Assert.Equal(valid, ScheduleRules.ValidWeek(Monday, [new(0, start, end, "Work")]));
    [Theory]
    [InlineData(15, false, true)] [InlineData(480, false, true)] [InlineData(120, false, true)]
    [InlineData(485, false, false)] [InlineData(0, false, false)] [InlineData(30, true, true)]
    [InlineData(60, true, true)] [InlineData(120, true, false)] [InlineData(15, true, false)]
    public void DurationPolicy(int duration, bool isPublic, bool valid) => Assert.Equal(valid, ScheduleRules.ValidDuration(duration, isPublic));
    [Fact]
    public void InvalidWeekOverlapAndBreakAreRejected()
    {
        Assert.False(ScheduleRules.ValidWeek(Monday.AddDays(1), []));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 780, "Work")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 705, 735, "Break", "Nghỉ")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 630, "Break")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 645, "Break", "Nghỉ"), new(0, 630, 660, "Block", "Bận")]));
        Assert.True(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 720, 900, "Work")]));
    }
    [Fact]
    public void FullIntervalMustFitOneWorkPeriodWithoutBreak()
    {
        SchedulePeriod[] periods = [new(0, 480, 720, "Work"), new(0, 720, 900, "Work"), new(0, 600, 615, "Break", "Nghỉ")];
        Assert.True(ScheduleRules.Covers(periods, 0, 480, 540));
        Assert.True(ScheduleRules.Covers(periods, 0, 480, 600));
        Assert.False(ScheduleRules.Covers(periods, 0, 495, 615));
        Assert.False(ScheduleRules.Covers(periods, 0, 660, 780));
        Assert.True(ScheduleRules.Covers(periods, 0, 615, 675));
    }
}

public sealed class SchedulingApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly DateOnly Date = new(2030, 1, 7);
    private sealed record Setup(int Branch, int Dentist, int Assistant, int Other, int Service, int Chair, int Actor, int Owner, DateOnly Date);
    private async Task<Setup> Create()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var branch = new SchedulingBranch { Name = "S09 fixture " + Guid.NewGuid() }; db.Add(branch); await db.SaveChangesAsync();
        var employees = await (from e in db.Employees join a in db.Users on e.AccountId equals a.Id select new { e.Id, e.AccountId, a.UserName }).ToArrayAsync();
        var dentist = employees.Single(e => e.UserName == "dentist.demo"); var assistant = employees.Single(e => e.UserName == "assistant.demo");
        var other = employees.Single(e => e.UserName == "dual.demo"); var owner = employees.Single(e => e.UserName == "owner.demo");
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true }, new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = other.Id, CanDentist = true }, new SchedulingMember { BranchId = branch.Id, EmployeeId = owner.Id, CanDentist = true, CanManageSchedules = true });
        var service = new SchedulingService { BranchId = branch.Id, Name = "Test 60", PublicDurationMinutes = 60 };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "Chair", OpenMinute = 480, CloseMinute = 1080 };
        db.AddRange(service, chair); await db.SaveChangesAsync();
        return new(branch.Id, dentist.Id, assistant.Id, other.Id, service.Id, chair.Id, dentist.AccountId, owner.AccountId, Date.AddDays(branch.Id * 7));
    }
    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient(); var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken); return client;
    }
    private static ScheduleCommand Command(Setup s, int version = 0, int? employee = null, SchedulePeriod[]? periods = null) =>
        new(s.Branch, employee ?? s.Dentist, s.Date, version, periods ?? [new(0, 480, 1080, "Work"), new(0, 720, 780, "Break", "Nghỉ trưa")], Guid.NewGuid());
    private static async Task<PublishResult> Publish(HttpClient client, ScheduleCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/working-schedules/publish", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PublishResult>())!;
    }
    private static DateTimeOffset Start(Setup s, int minute) => new DateTimeOffset(s.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(minute);
    private async Task<ResourceReservation> Reserve(Setup s, int start, int end, int? dentist = null, int? assistant = null, string? attendee = null, string status = "Confirmed")
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var reservation = new ResourceReservation { BranchId = s.Branch, DentistId = dentist ?? s.Dentist, AssistantId = assistant, ChairId = s.Chair, ServiceId = s.Service,
            Code = Guid.NewGuid().ToString("N"), Start = Start(s, start).ToUniversalTime(), End = Start(s, end).ToUniversalTime(), AttendeeKey = attendee, AttendeeLabel = "Synthetic attendee", Status = status };
        db.Add(reservation); await db.SaveChangesAsync(); return reservation;
    }
    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code); }

    [Fact]
    public async Task ReportedWorkBreakBlockSamplePublishesAndExcludesBothBreakAndBlock()
    {
        var s = await Create(); using var client = await Login();
        var command = Command(s, periods: [new(0, 480, 1020, "Work"), new(0, 720, 780, "Break", "Nghỉ trưa"), new(0, 900, 930, "Block", "Họp nội bộ")]);
        var preview = await client.PostAsJsonAsync("/api/working-schedules/preview", command);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var published = await Publish(client, command);
        Assert.Equal("Published", published.Schedule.Status); Assert.Equal(3, published.Schedule.Intervals.Length);
        using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var query = new AvailabilityQuery(s.Branch, s.Dentist, s.Service, s.Date, 30);
        var slots = (await service.QueryAsync(query, s.Actor, default)).Slots;
        Assert.Contains(slots, slot => slot.Start == Start(s, 480)); Assert.Contains(slots, slot => slot.Start == Start(s, 990));
        Assert.DoesNotContain(slots, slot => slot.Start < Start(s, 780) && slot.End > Start(s, 720));
        Assert.DoesNotContain(slots, slot => slot.Start < Start(s, 930) && slot.End > Start(s, 900));
        var longer = (await service.QueryAsync(query with { DurationMinutes = 120 }, s.Actor, default)).Slots;
        Assert.Contains(longer, slot => slot.Start == Start(s, 600));
        Assert.DoesNotContain(longer, slot => slot.Start == Start(s, 660) || slot.Start == Start(s, 840));
    }
    [Fact]
    public async Task PublishedWeekReadbackPublicAndStaffDurationsAndBoundary()
    {
        var s = await Create(); using var client = await Login(); var published = await Publish(client, Command(s));
        Assert.Equal(1, published.Schedule.Version); Assert.NotNull(published.Schedule.Id); Assert.Equal("MSG39", published.Notification.Code);
        var read = await client.GetFromJsonAsync<ScheduleDto>($"/api/working-schedules?branchId={s.Branch}&dentistId={s.Dentist}&weekStart={s.Date:yyyy-MM-dd}");
        Assert.Equal(published.Schedule.Id, read!.Id); Assert.Equal("Published", read.Status);
        using var publicClient = fixture.CreateClient();
        var path = $"/api/availability?branchId={s.Branch}&dentistId={s.Dentist}&serviceId={s.Service}&date={s.Date:yyyy-MM-dd}&durationMinutes=";
        var slots = (await publicClient.GetFromJsonAsync<AvailabilityResult>(path + "60"))!;
        Assert.Equal("Asia/Ho_Chi_Minh", slots.TimeZone); Assert.NotEmpty(slots.Slots);
        Assert.All(slots.Slots, slot => { Assert.Equal(TimeSpan.FromHours(7), slot.Start.Offset); Assert.Equal(TimeSpan.FromMinutes(60), slot.End - slot.Start); Assert.Equal(s.Chair, slot.ChairId); });
        Assert.Contains(slots.Slots, slot => slot.Start == Start(s, 660)); Assert.DoesNotContain(slots.Slots, slot => slot.Start == Start(s, 675));
        Assert.DoesNotContain(slots.Slots, slot => slot.Start == Start(s, 735)); Assert.Contains(slots.Slots, slot => slot.Start == Start(s, 1020)); Assert.DoesNotContain(slots.Slots, slot => slot.Start == Start(s, 1035));
        await Message(await publicClient.GetAsync(path + "120"), HttpStatusCode.BadRequest, "MSG110");
        var staff = (await client.GetFromJsonAsync<AvailabilityResult>(path + "120"))!;
        Assert.Contains(staff.Slots, slot => slot.Start == Start(s, 600)); Assert.DoesNotContain(staff.Slots, slot => slot.Start == Start(s, 615));
        Assert.Contains(staff.Slots, slot => slot.Start == Start(s, 960)); Assert.DoesNotContain(staff.Slots, slot => slot.Start == Start(s, 975));
    }
    [Fact]
    public async Task InvalidCommandsAndOwnershipCannotChangePublishedWeek()
    {
        var s = await Create(); using var dentist = await Login(); using var assistant = await Login("assistant.demo");
        using var guest = await Login("guest.demo"); using var anonymous = fixture.CreateClient();
        await Message(await anonymous.PostAsJsonAsync("/api/working-schedules/publish", Command(s)), HttpStatusCode.Unauthorized, "MSG07");
        await Message(await guest.PostAsJsonAsync("/api/working-schedules/publish", Command(s)), HttpStatusCode.Forbidden, "MSG06");
        await Message(await assistant.PostAsJsonAsync("/api/working-schedules/publish", Command(s)), HttpStatusCode.Forbidden, "MSG06");
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/publish", Command(s, employee: s.Other)), HttpStatusCode.Forbidden, "MSG06");
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/publish", Command(s, periods: [new(0, 600, 480, "Work")])), HttpStatusCode.BadRequest, "MSG110");
        await Publish(dentist, Command(s));
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/publish", Command(s)), HttpStatusCode.Conflict, "MSG111");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, (await db.Set<WorkingWeek>().SingleAsync(w => w.BranchId == s.Branch)).Version);
    }
    [Fact]
    public async Task ConfirmedAppointmentBlocksPublicationAndPreviewListsImpact()
    {
        var s = await Create(); using var client = await Login(); await Publish(client, Command(s));
        var booked = await Reserve(s, 480, 600); var command = Command(s, 1, periods: [new(0, 600, 1080, "Work")]);
        var preview = await client.PostAsJsonAsync("/api/working-schedules/preview", command);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); var impact = (await preview.Content.ReadFromJsonAsync<SchedulePreview>())!;
        Assert.Equal(booked.Id, Assert.Single(impact.AffectedAppointments).Id);
        await Message(await client.PostAsJsonAsync("/api/working-schedules/publish", command), HttpStatusCode.Conflict, "MSG38");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, (await db.Set<WorkingWeek>().SingleAsync(w => w.BranchId == s.Branch)).Version);
        Assert.Equal(480, (await db.Set<WorkingPeriod>().Where(p => p.WeekId == db.Set<WorkingWeek>().Where(w => w.BranchId == s.Branch).Select(w => w.Id).Single()).OrderBy(p => p.StartMinute).FirstAsync()).StartMinute);
    }
    [Fact]
    public async Task ChairMainAssistantAndAttendeeConflictsAreCheckedForEntireInterval()
    {
        var s = await Create(); using var owner = await Login("owner.demo");
        await Publish(owner, Command(s)); await Publish(owner, Command(s, employee: s.Assistant)); await Publish(owner, Command(s, employee: s.Other));
        await Reserve(s, 540, 555, dentist: s.Other, attendee: "patient-1");
        using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(s.Branch, s.Dentist, s.Service, Start(s, 480), 120, s.Chair, s.Assistant, null, 1, 1);
        Assert.Equal("MSG28", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default))).Response.Code);
        // Move occupancy to another chair: main's assistant remains unavailable, independent of chair capacity.
        var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.BranchId == s.Branch);
        var extraChair = new SchedulingChair { BranchId = s.Branch, Name = "Other" }; db.Add(extraChair); await db.SaveChangesAsync();
        reservation.ChairId = extraChair.Id; reservation.AssistantId = s.Assistant; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default));
        reservation.AssistantId = null; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { AttendeeKey = "patient-1" }, default));
        var available = await service.CheckInTransactionAsync(request with { DurationMinutes = 60 }, default);
        Assert.Equal(Start(s, 540), available.End); // adjacency at occupancy start is allowed
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { AssistantId = s.Dentist }, default));
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { ExpectedScheduleVersion = 0 }, default));
        var member = await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == s.Branch && x.EmployeeId == s.Assistant); member.Active = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default));
    }
    [Fact]
    public async Task AssistantSchedulePublicationAlsoProtectsConfirmedAppointments()
    {
        var s = await Create(); using var owner = await Login("owner.demo");
        await Publish(owner, Command(s)); await Publish(owner, Command(s, employee: s.Assistant));
        await Reserve(s, 480, 600, assistant: s.Assistant);
        await Message(await owner.PostAsJsonAsync("/api/working-schedules/publish", Command(s, 1, s.Assistant, [])), HttpStatusCode.Conflict, "MSG38");
    }
    [Fact]
    public async Task IdempotentRetryAndConcurrentVersionConflict()
    {
        var s = await Create(); using var client = await Login(); var command = Command(s);
        var first = await Publish(client, command); var replay = await Publish(client, command);
        Assert.Equal(first.Schedule.Id, replay.Schedule.Id); Assert.Equal(first.Schedule.Version, replay.Schedule.Version);
        await Message(await client.PostAsJsonAsync("/api/working-schedules/publish", command with { Intervals = [] }), HttpStatusCode.Conflict, "MSG111");
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/working-schedules/publish", Command(s, 1)), client.PostAsJsonAsync("/api/working-schedules/publish", Command(s, 1)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ResourceGuardSerializesCompetingReservationsAndRechecksCommittedOccupancy()
    {
        var s = await Create(); using var client = await Login(); await Publish(client, Command(s));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Attempt(bool first)
        {
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!first) await ready.Task;
            try
            {
                await service.CheckInTransactionAsync(new(s.Branch, s.Dentist, s.Service, Start(s, 480), 60, s.Chair, null, "concurrent-patient", 1), default);
                if (first) ready.SetResult();
                db.Add(new ResourceReservation { BranchId = s.Branch, DentistId = s.Dentist, ServiceId = s.Service, ChairId = s.Chair,
                    Code = Guid.NewGuid().ToString("N"), Start = Start(s, 480).ToUniversalTime(), End = Start(s, 540).ToUniversalTime(), AttendeeKey = "concurrent-patient" });
                await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
            }
            catch (BusinessException ex) { Assert.Equal("MSG27", ex.Response.Code); return false; }
        }
        var attempts = await Task.WhenAll(Attempt(true), Attempt(false));
        Assert.Single(attempts, success => success);
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    [Fact]
    public async Task PublicLeadTimeServiceDurationAndAssistantVersionAreEnforced()
    {
        var s = await Create(); using var owner = await Login("owner.demo"); await Publish(owner, Command(s)); await Publish(owner, Command(s, employee: s.Assistant));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var service = new DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService(db, new FixedClock(Start(s, 480)));
        var query = new AvailabilityQuery(s.Branch, s.Dentist, s.Service, s.Date, 60, s.Assistant);
        var result = await service.QueryAsync(query, null, default);
        Assert.DoesNotContain(result.Slots, x => x.Start == Start(s, 525)); Assert.Contains(result.Slots, x => x.Start == Start(s, 540));
        await Assert.ThrowsAsync<BusinessException>(() => service.QueryAsync(query with { DurationMinutes = 30 }, null, default));
        await using var tx = await db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(s.Branch, s.Dentist, s.Service, Start(s, 540), 60, s.Chair, s.Assistant, null, 1, 0);
        Assert.Equal("MSG111", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default))).Response.Code);
        Assert.Equal("MSG26", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { ExpectedAssistantScheduleVersion = 1, Start = Start(s, 690) }, default))).Response.Code);
        var member = await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == s.Branch && x.EmployeeId == s.Dentist); member.CanDentist = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.QueryAsync(query, null, default));
    }
    [Fact]
    public async Task TransactionRequiredAndDraftPreviewDoesNotPublishOrHoldSlots()
    {
        var s = await Create(); using var client = await Login();
        using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckInTransactionAsync(new(s.Branch, s.Dentist, s.Service, Start(s, 480), 60, s.Chair, null, null, 0), default));
        var query = new AvailabilityQuery(s.Branch, s.Dentist, s.Service, s.Date, 60);
        var response = await client.PostAsJsonAsync("/api/working-schedules/preview-availability", new PreviewAvailabilityRequest(Command(s), query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.NotEmpty((await response.Content.ReadFromJsonAsync<AvailabilityResult>())!.Slots);
        Assert.Empty((await service.QueryAsync(query, null, default)).Slots);
        Assert.False(await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Set<ResourceReservation>().AnyAsync(x => x.BranchId == s.Branch));
    }
}
