using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ChangeClock : TimeProvider
{
    public DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class AppointmentChangeFixture : ApiFixture
{
    public ChangeClock Clock { get; } = new();
    private int sequence;
    public int NextWeek() => Interlocked.Increment(ref sequence);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock); });
    }
}

public sealed class AppointmentChangeApiTests(AppointmentChangeFixture fixture) : IClassFixture<AppointmentChangeFixture>
{
    private sealed record Setup(int BranchId, int DentistId, int SecondDentistId, int ServiceId, int ChairId, DateOnly Date);
    private async Task<Setup> Prepare(bool assistantMember = true)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "dentist.demo" select e).SingleAsync();
        var secondDentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "owner.demo" select e).SingleAsync();
        var assistant = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "assistant.demo" select e).SingleAsync();
        var branch = new SchedulingBranch { Name = "UC06 synthetic " + Guid.NewGuid() };
        db.Add(branch); await db.SaveChangesAsync();
        db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true });
        db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = secondDentist.Id, CanDentist = true });
        if (assistantMember) db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id });
        var service = new SchedulingService { BranchId = branch.Id, Name = "UC06 exam", PublicDurationMinutes = 30 };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "UC06 chair", OpenMinute = 0, CloseMinute = 1440 };
        db.AddRange(service, chair);
        var date = DateOnly.FromDateTime(fixture.Clock.Now.UtcDateTime).AddDays(14 + fixture.NextWeek() * 7);
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = dentist.Id, WeekStart = monday,
            PublishedBy = dentist.AccountId, PublishedAt = fixture.Clock.Now };
        for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day,
            StartMinute = 0, EndMinute = 1440, Kind = "Work" });
        var secondWeek = new WorkingWeek { BranchId = branch.Id, EmployeeId = secondDentist.Id, WeekStart = monday,
            PublishedBy = secondDentist.AccountId, PublishedAt = fixture.Clock.Now };
        for (var day = 0; day < 7; day++) secondWeek.Periods.Add(new WorkingPeriod { WeekId = secondWeek.Id, Day = day,
            StartMinute = 0, EndMinute = 1440, Kind = "Work" });
        db.AddRange(week, secondWeek); await db.SaveChangesAsync();
        return new(branch.Id, dentist.Id, secondDentist.Id, service.Id, chair.Id, date);
    }
    private async Task<HttpClient> Login(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(username, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }
    private async Task<(HttpClient Client, int Id)> Public()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "uc06." + Guid.NewGuid().ToString("N")[..12], FullName = "Synthetic booker",
            Email = Guid.NewGuid() + "@example.invalid", AccountType = "Public", CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Guest)).Succeeded);
        return (await Login(user.UserName), user.Id);
    }
    private static string Path(AppointmentResult a, string action) => $"/api/appointments/{a.Code}/{action}";
    private static CancelAppointmentRequest Cancel(AppointmentResult a, string? reason = "Synthetic cancellation") => new(a.Version, Guid.NewGuid(), reason);
    private async Task<AvailabilitySlot[]> Slots(HttpClient client, Setup s) =>
        (await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=30"))!.Slots;
    private async Task<AppointmentResult> Book(HttpClient client, Setup s, AvailabilitySlot slot, string relation = "Self")
    {
        var request = new CreateAppointmentRequest(s.BranchId, new("Synthetic booker", "0912345678", null),
            new(null, "Synthetic attendee", null), relation, "Standard", "App", s.ServiceId, null, s.DentistId,
            null, null, slot.Start, 30, null, null, Guid.NewGuid(), slot.ScheduleVersion, null);
        var response = await client.PostAsJsonAsync("/api/appointments", request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
    }
    private static RescheduleAppointmentRequest Move(AppointmentResult a, AvailabilitySlot slot) => new(a.Version,
        Guid.NewGuid(), slot.Start, slot.ChairId, slot.ScheduleVersion, slot.AssistantScheduleVersion, "Synthetic reschedule",
        DentistId: slot.DentistId);
    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }
    private async Task SetState(AppointmentResult a, string status = "Confirmed", DateTimeOffset? start = null, Guid? followUp = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var row = await db.Appointments.FindAsync(a.AppointmentId);
        var reservation = await db.Set<ResourceReservation>().FindAsync(row!.ReservationId);
        row.Status = reservation!.Status = status;
        if (start.HasValue) { row.StartAt = reservation.Start = start.Value; row.EndAt = reservation.End = start.Value.AddMinutes(30); }
        row.FollowUpId = followUp;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RescheduleThenCancelPersistsHistoryOutboxAndReleasesIntervals()
    {
        var s = await Prepare(); var (client, actor) = await Public();
        var slots = await Slots(client, s); var a = await Book(client, s, slots[36], "Relative");
        var command = Move(a, slots[40]);
        var response = await client.PostAsJsonAsync(Path(a, "reschedule"), command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal("MSG35", moved.Notification.Code); Assert.Equal(command.NewStart, moved.Appointment.Start);
        Assert.Equal(30, moved.Appointment.DurationMinutes); Assert.Equal("RELATIVE", moved.Appointment.AttendeeKind);
        Assert.Equal(2, moved.Appointment.Version); Assert.Single(moved.Appointment.History);
        var available = await Slots(client, s);
        Assert.Contains(available, x => x.Start == a.StartAt); Assert.DoesNotContain(available, x => x.Start == command.NewStart);
        var cancelCommand = new CancelAppointmentRequest(2, Guid.NewGuid(), "  Synthetic cancel  ");
        var cancelled = await client.PostAsJsonAsync(Path(a, "cancel"), cancelCommand);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var result = (await cancelled.Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal("Cancelled", result.Appointment.Status); Assert.Equal("MSG37", result.Notification.Code);
        Assert.Equal(3, result.Appointment.Version); Assert.Equal(2, result.Appointment.History.Count);
        Assert.Contains(await Slots(client, s), x => x.Start == command.NewStart);
        Assert.DoesNotContain((await client.GetFromJsonAsync<MyAppointmentPage>("/api/me/appointments"))!.Items, x => x.Code == a.Code);
        Assert.Contains((await client.GetFromJsonAsync<MyAppointmentPage>("/api/me/appointments?scope=HISTORY"))!.Items, x => x.Code == a.Code);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.FindAsync(a.AppointmentId);
        Assert.Equal(actor, saved!.CancelledBy); Assert.Equal("Synthetic cancel", saved.CancelReason); Assert.NotNull(saved.CancelledAt);
        Assert.Equal("Cancelled", (await db.Set<ResourceReservation>().FindAsync(saved.ReservationId))!.Status);
        Assert.Equal(2, await db.AppointmentChanges.CountAsync(x => x.AppointmentId == a.AppointmentId));
        Assert.Equal(3, await db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == a.AppointmentId));
        var evt = await db.AppointmentOutboxEvents.SingleAsync(x => x.AggregateId == a.AppointmentId && x.EventType == "appointment.cancelled");
        Assert.DoesNotContain("Synthetic cancel", evt.PayloadJson); Assert.DoesNotContain("0912345678", evt.PayloadJson);
    }

    [Fact]
    public async Task RescheduleCanSelectAnotherActiveDentistInTheSameBranch()
    {
        var s = await Prepare(); var (client, _) = await Public();
        var originalSlots = await Slots(client, s); var a = await Book(client, s, originalSlots[36]);
        var available = (await client.GetFromJsonAsync<AvailabilityResult>(
            $"/api/appointments/{a.Code}/availability?date={s.Date:yyyy-MM-dd}&dentistId={s.SecondDentistId}"))!;
        var target = available.Slots.First(x => x.Start >= originalSlots[40].Start);

        var response = await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, target));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal(s.SecondDentistId, moved.Appointment.DentistId);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var appointment = await db.Appointments.SingleAsync(x => x.Id == a.AppointmentId);
        var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.Id == appointment.ReservationId);
        var history = await db.AppointmentChanges.SingleAsync(x => x.AppointmentId == appointment.Id);
        Assert.Equal(s.SecondDentistId, appointment.DentistId);
        Assert.Equal(s.SecondDentistId, reservation.DentistId);
        Assert.Equal(s.DentistId, history.OldDentistId);
        Assert.Equal(s.SecondDentistId, history.NewDentistId);
    }

    [Fact]
    public async Task ReplayIsExactAndKeyReuseOrStaleVersionCannotMutate()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s);
        var a = await Book(client, s, slots[36]); var move = Move(a, slots[40]);
        var first = (await (await client.PostAsJsonAsync(Path(a, "reschedule"), move)).Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        var repeated = (await (await client.PostAsJsonAsync(Path(a, "reschedule"), move)).Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal(first.Appointment.Version, repeated.Appointment.Version); Assert.Single(repeated.Appointment.History);
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), move with { Reason = "changed" }), HttpStatusCode.Conflict, "MSG111");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), new CancelAppointmentRequest(2, move.RequestKey, move.Reason)), HttpStatusCode.Conflict, "MSG111");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a)), HttpStatusCode.Conflict, "MSG111");
        var cancel = new CancelAppointmentRequest(2, Guid.NewGuid(), "cancel");
        var one = await client.PostAsJsonAsync(Path(a, "cancel"), cancel);
        var two = await client.PostAsJsonAsync(Path(a, "cancel"), cancel);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode); Assert.Equal(HttpStatusCode.OK, two.StatusCode);
        var replay = (await two.Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal(3, replay.Appointment.Version); Assert.Equal(2, replay.Appointment.History.Count);
        // A delayed retry still returns its original receipt after a subsequent successful command.
        var delayed = (await (await client.PostAsJsonAsync(Path(a, "reschedule"), move)).Content.ReadFromJsonAsync<AppointmentChangeResult>())!;
        Assert.Equal(2, delayed.Appointment.Version);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(2, await db.AppointmentChanges.CountAsync(x => x.AppointmentId == a.AppointmentId));
        Assert.Equal(2, await db.AppointmentChangeReceipts.CountAsync(x => x.AppointmentId == a.AppointmentId));
    }

    [Fact]
    public async Task SlotConflictScheduleVersionAndLeadTimePreserveOriginal()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s);
        var a = await Book(client, s, slots[36]); await Book(client, s, slots[40]);
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[40])), HttpStatusCode.Conflict, "MSG34");
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[44]) with { ScheduleVersion = 999 }), HttpStatusCode.Conflict, "MSG111");
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[44]) with { NewStart = fixture.Clock.Now.AddMinutes(59) }), HttpStatusCode.Conflict, "MSG34");
        var unchanged = (await client.GetFromJsonAsync<MyAppointmentDetail>($"/api/me/appointments/{a.Code}"))!;
        Assert.Equal(a.StartAt, unchanged.Start); Assert.Equal(1, unchanged.Version); Assert.Empty(unchanged.History);
        // The check must exclude the old reservation, even if the new interval overlaps it.
        var overlap = Move(a, slots[36]) with { NewStart = a.StartAt.AddMinutes(15) };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Path(a, "reschedule"), overlap)).StatusCode);
    }

    [Theory]
    [InlineData(720, true)]
    [InlineData(719, false)]
    [InlineData(0, false)]
    public async Task TwelveHourBoundaryIsCheckedOnServer(int minutes, bool allowed)
    {
        var s = await Prepare(); var (client, _) = await Public(); var a = await Book(client, s, (await Slots(client, s))[36]);
        await SetState(a, start: fixture.Clock.Now.AddMinutes(minutes));
        var response = await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a));
        if (allowed) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        else await Message(response, HttpStatusCode.Conflict, "MSG31");
    }

    [Theory]
    [InlineData("CheckedIn")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("NoShow")]
    public async Task NonConfirmedAppointmentsCannotChange(string status)
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s); var a = await Book(client, s, slots[36]);
        await SetState(a, status);
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a)), HttpStatusCode.Conflict, "MSG31");
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[40])), HttpStatusCode.Conflict, "MSG31");
    }

    [Fact]
    public async Task AnonymousStrangerAndRevokedSelfLinkCannotChangeOrReplay()
    {
        var s = await Prepare(); var (booker, _) = await Public(); var (other, otherId) = await Public();
        var slots = await Slots(booker, s); var a = await Book(booker, s, slots[36]);
        var command = Cancel(a);
        using var anonymous = fixture.CreateClient();
        await Message(await anonymous.PostAsJsonAsync(Path(a, "cancel"), command), HttpStatusCode.Unauthorized, "MSG32");
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        await Message(await anonymous.PostAsJsonAsync(Path(a, "cancel"), command), HttpStatusCode.Unauthorized, "MSG07");
        await Message(await other.PostAsJsonAsync(Path(a, "cancel"), command), HttpStatusCode.NotFound, "MSG102");
        int linkId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var patient = new PatientProfile { PatientCode = "UC06-" + Guid.NewGuid().ToString("N")[..12], FullName = "Synthetic patient",
                CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now };
            db.Add(patient); await db.SaveChangesAsync();
            var row = await db.Appointments.FindAsync(a.AppointmentId); row!.PatientId = patient.Id;
            var link = new PatientAccountLink { RequestCode = "UC06-" + Guid.NewGuid().ToString("N")[..12], AccountId = otherId,
                PatientId = patient.Id, Status = PatientLinkStatus.Verified, DeclaredFullName = patient.FullName, RequestedAt = fixture.Clock.Now };
            db.Add(link); await db.SaveChangesAsync(); linkId = link.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync(Path(a, "cancel"), command)).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.PatientAccountLinks.FindAsync(linkId))!.Status = PatientLinkStatus.Revoked;
            await db.SaveChangesAsync();
        }
        await Message(await other.PostAsJsonAsync(Path(a, "cancel"), command), HttpStatusCode.NotFound, "MSG102");
    }

    [Fact]
    public async Task StaffMustBeAssistantInBranchAndVerifyRequester()
    {
        var s = await Prepare(); var (owner, _) = await Public(); var a = await Book(owner, s, (await Slots(owner, s))[36]);
        await SetState(a, start: fixture.Clock.Now.AddHours(2));
        using var assistant = await Login("assistant.demo"); using var dentist = await Login("dentist.demo");
        await Message(await dentist.PostAsJsonAsync(Path(a, "cancel"), Cancel(a)), HttpStatusCode.Forbidden, "MSG06");
        await Message(await assistant.PostAsJsonAsync(Path(a, "cancel"), Cancel(a)), HttpStatusCode.BadRequest, "MSG02");
        var command = Cancel(a) with { RequesterVerification = "Original contact and code verified" };
        var response = await assistant.PostAsJsonAsync(Path(a, "cancel"), command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("STAFF", (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.Access);
        var otherBranch = await Prepare(false); var b = await Book(owner, otherBranch, (await Slots(owner, otherBranch))[36]);
        await Message(await assistant.PostAsJsonAsync(Path(b, "cancel"), Cancel(b) with { RequesterVerification = "verified" }), HttpStatusCode.NotFound, "MSG102");
    }

    [Fact]
    public async Task ValidationAndUnavailableFollowUpLeaveNoPartialWrites()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s); var a = await Book(client, s, slots[36]);
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a, "  ")), HttpStatusCode.BadRequest, "MSG02");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a, new string('a', 501))), HttpStatusCode.BadRequest, "MSG03");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { RequestKey = Guid.Empty }), HttpStatusCode.BadRequest, "MSG04");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { ExpectedVersion = 0 }), HttpStatusCode.BadRequest, "MSG04");
        await SetState(a, followUp: Guid.NewGuid());
        var moved = await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[40]));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.NotNull((await moved.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.FollowUp);
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { ExpectedVersion = 2 }), HttpStatusCode.ServiceUnavailable, "MSG144");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.FindAsync(a.AppointmentId);
        Assert.Equal("Confirmed", saved!.Status); Assert.Equal(2, saved.Version); Assert.Null(saved.CancelledAt);
        Assert.Single(await db.AppointmentChanges.Where(x => x.AppointmentId == a.AppointmentId).ToListAsync());
        Assert.Equal(2, await db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == a.AppointmentId));
    }

    [Fact]
    public async Task CompetingReschedulesAndCancellationsCommitOnlyOnce()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s);
        var a = await Book(client, s, slots[36]); var b = await Book(client, s, slots[40]);
        var results = await Task.WhenAll(client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[44])),
            client.PostAsJsonAsync(Path(b, "reschedule"), Move(b, slots[44])));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        await Message(Assert.Single(results, x => x.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "MSG34");
        var winner = results[0].IsSuccessStatusCode ? a : b;
        var cancel = Cancel(winner) with { ExpectedVersion = 2 };
        var competing = await Task.WhenAll(client.PostAsJsonAsync(Path(winner, "cancel"), cancel),
            client.PostAsJsonAsync(Path(winner, "cancel"), cancel with { RequestKey = Guid.NewGuid() }));
        Assert.Single(competing, x => x.StatusCode == HttpStatusCode.OK);
        await Message(Assert.Single(competing, x => x.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "MSG111");
    }

    [Fact]
    public async Task ReschedulingKeepsAssistantAndRechecksAssistantAvailability()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s);
        var a = await Book(client, s, slots[36]); int assistantId; Guid blockerId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var assistant = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
                where u.UserName == "assistant.demo" select e).SingleAsync();
            assistantId = assistant.Id;
            var monday = s.Date.AddDays(-(((int)s.Date.DayOfWeek + 6) % 7));
            var week = new WorkingWeek { BranchId = s.BranchId, EmployeeId = assistant.Id, WeekStart = monday,
                PublishedBy = assistant.AccountId, PublishedAt = fixture.Clock.Now };
            for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day,
                StartMinute = 0, EndMinute = 1440, Kind = "Work" });
            db.Add(week);
            (await db.Appointments.FindAsync(a.AppointmentId))!.AssistantId = assistant.Id;
            (await db.Set<ResourceReservation>().FindAsync(a.AppointmentId))!.AssistantId = assistant.Id;
            var blocker = new ResourceReservation { Code = "UC06 blocker", BranchId = s.BranchId, DentistId = assistant.Id,
                ChairId = s.ChairId, Start = slots[40].Start.ToUniversalTime(), End = slots[40].End.ToUniversalTime() };
            blockerId = blocker.Id; db.Add(blocker); await db.SaveChangesAsync();
        }
        var detail = (await client.GetFromJsonAsync<MyAppointmentDetail>($"/api/me/appointments/{a.Code}"))!;
        Assert.Equal(assistantId, detail.AssistantId);
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[40]) with { AssistantScheduleVersion = 1 }),
            HttpStatusCode.Conflict, "MSG34");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.Remove((await db.Set<ResourceReservation>().FindAsync(blockerId))!); await db.SaveChangesAsync();
        }
        var assisted = (await client.GetFromJsonAsync<AvailabilityResult>($"/api/appointments/{a.Code}/availability?date={s.Date:yyyy-MM-dd}"))!;
        var target = assisted.Slots.Single(x => x.Start == slots[40].Start);
        Assert.NotNull(target.AssistantScheduleVersion);
        var response = await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, target));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(assistantId, (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.AssistantId);
    }

    [Fact]
    public async Task OwnedAvailabilityUsesOriginalDurationAndNeverWidensPublicCatalog()
    {
        var s = await Prepare(); var (client, _) = await Public(); var (stranger, _) = await Public();
        var a = await Book(client, s, (await Slots(client, s))[36]);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.Appointments.FindAsync(a.AppointmentId))!.EndAt = a.StartAt.AddMinutes(45);
            (await db.Set<ResourceReservation>().FindAsync(a.AppointmentId))!.End = a.StartAt.AddMinutes(45);
            (await db.Set<SchedulingService>().FindAsync(s.ServiceId))!.PublicDurationMinutes = 60;
            await db.SaveChangesAsync();
        }
        var path = $"/api/appointments/{a.Code}/availability?date={s.Date:yyyy-MM-dd}";
        using var anonymous = fixture.CreateClient();
        await Message(await anonymous.GetAsync(path), HttpStatusCode.Unauthorized, "MSG32");
        await Message(await stranger.GetAsync(path), HttpStatusCode.NotFound, "MSG102");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=45")).StatusCode);
        var available = (await client.GetFromJsonAsync<AvailabilityResult>(path))!;
        Assert.NotEmpty(available.Slots);
        Assert.All(available.Slots, slot => Assert.Equal(45, (slot.End - slot.Start).TotalMinutes));
        Assert.DoesNotContain(available.Slots, slot => slot.Start == a.StartAt);
        var overlapping = available.Slots.Single(slot => slot.Start == a.StartAt.AddMinutes(15));
        var response = await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, overlapping));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(45, (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.DurationMinutes);
    }
}
