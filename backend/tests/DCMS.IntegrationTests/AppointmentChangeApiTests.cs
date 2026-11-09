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
using DCMS.Domain.Features.Settings;
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

public sealed class AppointmentChangeApiTests(AppointmentChangeFixture fixture) : IClassFixture<AppointmentChangeFixture>, IAsyncLifetime
{
    private readonly List<int> dueBranches = [];
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync()
    {
        if (dueBranches.Count == 0) return;
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        // Staff share a dentist across synthetic branches; release each test's reservations after assertions.
        await db.Set<ResourceReservation>().Where(x => dueBranches.Contains(x.BranchId)).ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Cancelled"));
    }
    private sealed record Setup(int BranchId, int DentistId, int SecondDentistId, int ServiceId, int ChairId, DateOnly Date);
    private async Task<Setup> Prepare(bool assistantMember = true, bool today = false)
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
        var service = new SchedulingService { BranchId = branch.Id, Name = "UC06 exam", PublicDurationMinutes = 30, IsInitialExamination = true };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "UC06 chair", OpenMinute = 0, CloseMinute = 1440 };
        db.AddRange(service, chair);
        var date = today ? DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime)
            : DateOnly.FromDateTime(fixture.Clock.Now.UtcDateTime).AddDays(14 + fixture.NextWeek() * 7);
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

    private async Task<(Setup Setup, AppointmentResult Source)> DueAppointment()
    {
        var s = await Prepare(today: true);
        dueBranches.Add(s.BranchId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var id = Guid.NewGuid(); var start = fixture.Clock.Now.AddMinutes(-5); var end = fixture.Clock.Now.AddMinutes(75);
        var patient = new PatientProfile { PatientCode = "NOSHOW-" + id.ToString("N"), FullName = "SYNTHETIC original patient", CreatedAt = start, UpdatedAt = start };
        db.Add(patient); await db.SaveChangesAsync();
        var source = new Appointment { Id = id, ReservationId = id, Code = "AP-" + id.ToString("N").ToUpperInvariant(),
            BranchId = s.BranchId, DentistId = s.DentistId, ChairId = s.ChairId, ServiceId = s.ServiceId, PatientId = patient.Id,
            BookerName = "SYNTHETIC original booker", BookerPhone = "0912345678", AttendeeName = patient.FullName,
            StartAt = start, EndAt = end, CreatedAt = start.AddDays(-1) };
        db.AddRange(source, new ResourceReservation { Id = id, Code = source.Code, BranchId = s.BranchId, DentistId = s.DentistId,
            ChairId = s.ChairId, ServiceId = s.ServiceId, Start = start, End = end, AttendeeKey = $"patient:{patient.Id}", AttendeeLabel = patient.FullName });
        await db.SaveChangesAsync();
        return (s, new(id, source.Code, 1, "Confirmed", start, end, SystemMessages.Get("MSG29")));
    }
    private async Task<CreateAppointmentRequest> Replacement(HttpClient staff, Setup s, AppointmentResult source)
    {
        var slots = (await staff.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.DentistId}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=5"))!.Slots;
        var slot = slots.First(value => value.Start > fixture.Clock.Now && value.End <= source.EndAt);
        return new(s.BranchId, new("SYNTHETIC new booker", "0987654321", null), new(null, "SYNTHETIC new attendee", null), "Self",
            "WalkIn", "InPerson", s.ServiceId, null, s.DentistId, null, slot.ChairId, slot.Start, 5, null,
            "SYNTHETIC replacement of no-show", Guid.NewGuid(), slot.ScheduleVersion, slot.AssistantScheduleVersion,
            slot.BookingSettingsVersion, ReplacesAppointmentId: source.AppointmentId);
    }

    [Theory]
    [InlineData("assistant.demo")]
    [InlineData("dentist.demo")]
    [InlineData("owner.demo")]
    public async Task NoShowReleasesResourcesAndBooksDifferentPatientWithoutOverwritingHistory(string username)
    {
        var (s, source) = await DueAppointment(); using var staff = await Login(username);
        var command = new NoShowAppointmentRequest(1, Guid.NewGuid(), "SYNTHETIC patient did not arrive");
        var marked = await staff.PostAsJsonAsync(Path(source, "no-show"), command);
        Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync());
        Assert.Equal("NoShow", (await marked.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.Status);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(Path(source, "no-show"), command)).StatusCode);
        var booking = await Replacement(staff, s, source);
        var response = await staff.PostAsJsonAsync("/api/appointments", booking);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        Assert.NotEqual(source.AppointmentId, created.AppointmentId); Assert.True(created.StartAt > fixture.Clock.Now); Assert.True(created.EndAt <= source.EndAt);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync("/api/appointments", booking)).StatusCode);
        await Message(await staff.PostAsJsonAsync("/api/appointments", booking with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG27");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var original = await db.Appointments.SingleAsync(x => x.Id == source.AppointmentId);
        Assert.Equal("NoShow", original.Status); Assert.Equal("SYNTHETIC original patient", original.AttendeeName); Assert.NotNull(original.PatientId); Assert.Equal(2, original.Version);
        Assert.Equal("NoShow", (await db.Set<ResourceReservation>().FindAsync(original.ReservationId))!.Status);
        var replacement = await db.Appointments.SingleAsync(x => x.Id == created.AppointmentId);
        Assert.Equal("SYNTHETIC new attendee", replacement.AttendeeName); Assert.Null(replacement.PatientId); Assert.Equal("Confirmed", replacement.Status);
        Assert.Single(await db.AppointmentChanges.Where(x => x.AppointmentId == original.Id).ToArrayAsync());
        Assert.Single(await db.AppointmentOutboxEvents.Where(x => x.AggregateId == original.Id && x.EventType == "appointment.no_show").ToArrayAsync());
        var payload = await db.AppointmentOutboxEvents.Where(x => x.AggregateId == created.AppointmentId).Select(x => x.PayloadJson).SingleAsync();
        Assert.Contains(source.AppointmentId.ToString(), payload);
        // Exact replay remains valid after the source window expires; it never creates another booking.
        original.EndAt = fixture.Clock.Now.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync("/api/appointments", booking)).StatusCode);
        Assert.Equal(2, await db.Appointments.CountAsync(x => x.BranchId == s.BranchId));
    }

    [Fact]
    public async Task NoShowAndReplacementRecheckDynamicGrantsEvenOnReplayAndDirectCalls()
    {
        var (s, source) = await DueAppointment(); var username = "noshow." + Guid.NewGuid().ToString("N")[..12]; int actor; Guid groupId;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = username, FullName = "SYNTHETIC no-show scheduler", AccountType = "Staff", IsActive = true };
            Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, "ClinicAssistant")).Succeeded); actor = user.Id;
            var employee = new EmployeeProfile { AccountId = actor, FullName = user.FullName, EmployeeCode = username };
            db.Add(employee); await db.SaveChangesAsync();
            var group = new AccessGroup { Code = username, Name = username, NormalizedCode = username.ToUpperInvariant(), NormalizedName = username.ToUpperInvariant(), BaseRole = "ClinicAssistant" };
            foreach (var permission in new[] { "appointments.read", "appointments.no-show", "appointments.create" }) group.Grants.Add(new AccessGroupGrant { GroupId = group.Id, Permission = permission });
            groupId = group.Id; db.AddRange(group, new SchedulingMember { BranchId = s.BranchId, EmployeeId = employee.Id },
                new StaffAccessAssignment { AccountId = actor, GroupId = group.Id, BranchIds = [s.BranchId] }); await db.SaveChangesAsync();
        }
        using var client = await Login(username); var mark = new NoShowAppointmentRequest(1, Guid.NewGuid(), "SYNTHETIC no-show");
        (await client.PostAsJsonAsync(Path(source, "no-show"), mark)).EnsureSuccessStatusCode();
        var booking = await Replacement(client, s, source); (await client.PostAsJsonAsync("/api/appointments", booking)).EnsureSuccessStatusCode();
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.RemoveRange(await db.Set<AccessGroupGrant>().Where(x => x.GroupId == groupId && x.Permission != "appointments.read").ToArrayAsync()); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Path(source, "no-show"), mark)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/appointments", booking)).StatusCode);
        using var direct = fixture.Services.CreateScope();
        var deniedMark = await Assert.ThrowsAsync<BusinessException>(() => direct.ServiceProvider.GetRequiredService<IAppointmentChangeService>().MarkNoShowAsync(actor, source.Code, mark, default));
        Assert.Equal(403, deniedMark.Status);
        var deniedBook = await Assert.ThrowsAsync<BusinessException>(() => direct.ServiceProvider.GetRequiredService<IAppointmentService>().CreateAsync(actor, booking, default));
        Assert.Equal(403, deniedBook.Status);
    }

    [Theory]
    [InlineData("future")]
    [InlineData("checked-in")]
    [InlineData("visit-recorded")]
    [InlineData("other-dentist")]
    [InlineData("customer")]
    public async Task NoShowRejectsIneligibleAppointmentOrActor(string scenario)
    {
        var (_, source) = await DueAppointment();
        if (scenario == "future") await SetState(source, start: fixture.Clock.Now.AddMinutes(1));
        if (scenario == "checked-in") await SetState(source, "CheckedIn");
        if (scenario == "visit-recorded") {
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var row = await db.Appointments.FindAsync(source.AppointmentId);
            var recorder = await db.Users.Where(x => x.UserName == "assistant.demo").Select(x => x.Id).SingleAsync();
            db.Visits.Add(new DCMS.Domain.Features.Visits.Visit { AppointmentId = row!.Id, PatientId = row.PatientId!.Value,
                BranchId = row.BranchId, DentistId = row.DentistId, ArrivalAt = fixture.Clock.Now, RecordedAt = fixture.Clock.Now, RecordedBy = recorder });
            await db.SaveChangesAsync();
        }
        using var client = await Login(scenario == "customer" ? "guest.demo" : scenario == "other-dentist" ? "owner.demo" : "assistant.demo");
        if (scenario == "other-dentist") {
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var other = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id where user.UserName == "owner.demo" select employee).SingleAsync();
            var row = await db.Appointments.FindAsync(source.AppointmentId); row!.DentistId = other.Id;
            await db.SaveChangesAsync();
        }
        // A dentist cannot mark another doctor's appointment, even with the same branch membership.
        using var dentist = scenario == "other-dentist" ? await Login("dentist.demo") : null;
        var response = await (dentist ?? client).PostAsJsonAsync(Path(source, "no-show"), new NoShowAppointmentRequest(1, Guid.NewGuid(), "SYNTHETIC no show"));
        Assert.False(response.IsSuccessStatusCode);
        using var check = fixture.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Empty(await context.AppointmentChanges.Where(x => x.AppointmentId == source.AppointmentId).ToArrayAsync());
    }

    [Theory]
    [InlineData("confirmed")]
    [InlineData("expired")]
    [InlineData("overrun")]
    [InlineData("other-dentist")]
    [InlineData("customer")]
    public async Task ReplacementRejectsInvalidContextWithoutCreatingAnything(string scenario)
    {
        var (s, source) = await DueAppointment(); using var staff = await Login("assistant.demo");
        await SetState(source, "NoShow"); var command = await Replacement(staff, s, source);
        if (scenario == "confirmed") await SetState(source);
        if (scenario == "expired") await SetState(source, "NoShow", fixture.Clock.Now.AddHours(-2));
        if (scenario == "overrun") command = command with { DurationMinutes = 480 };
        if (scenario == "other-dentist") command = command with { DentistId = s.SecondDentistId };
        using var customer = scenario == "customer" ? await Login("guest.demo") : null;
        var response = await (customer ?? staff).PostAsJsonAsync("/api/appointments", command);
        Assert.False(response.IsSuccessStatusCode);
        if (scenario != "customer") Assert.Equal("APPOINTMENT_REPLACEMENT_UNAVAILABLE", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Appointments.CountAsync(x => x.BranchId == s.BranchId));
        Assert.Empty(await db.AppointmentReceipts.Where(x => x.RequestKey == command.RequestKey).ToArrayAsync());
    }

    [Theory]
    [InlineData("assistant.demo")]
    [InlineData("dentist.demo")]
    [InlineData("owner.demo")]
    public async Task ClinicCanProactivelyRescheduleAndCancelWithoutARequester(string staffName)
    {
        var s = await Prepare(); var (booker, _) = await Public();
        var a = await Book(booker, s, (await Slots(booker, s))[36]); using var staff = await Login(staffName);
        var slots = (await staff.GetFromJsonAsync<AvailabilityResult>(Path(a, "availability") + $"?date={s.Date:yyyy-MM-dd}"))!.Slots;
        var move = Move(a, slots.First(x => x.Start != a.StartAt)) with { ClinicInitiated = true };
        var movedResponse = await staff.PostAsJsonAsync(Path(a, "reschedule"), move);
        Assert.True(movedResponse.IsSuccessStatusCode, await movedResponse.Content.ReadAsStringAsync());
        var moved = (await movedResponse.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment;
        Assert.Equal(move.NewStart, moved.Start); Assert.Equal(30, moved.DurationMinutes);
        var cancel = new CancelAppointmentRequest(moved.Version, Guid.NewGuid(), "SYNTHETIC clinic cancellation", ClinicInitiated: true);
        var response = await staff.PostAsJsonAsync(Path(a, "cancel"), cancel);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment.Status);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(Path(a, "cancel"), cancel)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == a.AppointmentId);
        Assert.Equal("Cancelled", (await db.Set<ResourceReservation>().AsNoTracking().SingleAsync(x => x.Id == saved.ReservationId)).Status);
        var history = await db.AppointmentChanges.Where(x => x.AppointmentId == a.AppointmentId).ToArrayAsync(); Assert.Equal(2, history.Length);
        Assert.All(history, row => { Assert.Equal("STAFF", row.ActorKind); Assert.Contains("Phòng khám chủ động", row.RequesterVerification); });
        Assert.Equal(2, await db.AppointmentOutboxEvents.CountAsync(x => x.AggregateId == a.AppointmentId && (x.EventType == "appointment.rescheduled" || x.EventType == "appointment.cancelled")));
    }

    [Fact]
    public async Task ProactiveModeDoesNotExpandCustomerOrOtherDoctorsAccess()
    {
        var s = await Prepare(); var (customer, _) = await Public(); var a = await Book(customer, s, (await Slots(customer, s))[36]);
        await Message(await customer.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { ClinicInitiated = true }), HttpStatusCode.Forbidden, "MSG06");
        using var dentist = await Login("dentist.demo");
        var availability = (await dentist.GetFromJsonAsync<AvailabilityResult>(Path(a, "availability") + $"?date={s.Date:yyyy-MM-dd}"))!;
        var transfer = Move(a, availability.Slots.First()) with { ClinicInitiated = true, DentistId = s.SecondDentistId };
        await Message(await dentist.PostAsJsonAsync(Path(a, "reschedule"), transfer), HttpStatusCode.Forbidden, "MSG06");
        var other = s with { DentistId = s.SecondDentistId }; var b = await Book(customer, other, (await Slots(customer, other))[40]);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentist.PostAsJsonAsync(Path(b, "cancel"), Cancel(b) with { ClinicInitiated = true })).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Empty(await db.AppointmentChanges.Where(x => x.AppointmentId == a.AppointmentId || x.AppointmentId == b.AppointmentId).ToArrayAsync());
    }

    [Fact]
    public async Task ProactiveCommandsRecheckDynamicGrantsBeforeReplayAndInDirectServiceCalls()
    {
        var s = await Prepare(); var (booker, _) = await Public(); var a = await Book(booker, s, (await Slots(booker, s))[36]);
        var username = "proactive." + Guid.NewGuid().ToString("N")[..12]; int actor; Guid groupId;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = username, FullName = "SYNTHETIC scheduler", AccountType = "Staff", IsActive = true };
            Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, "ClinicAssistant")).Succeeded); actor = user.Id;
            var employee = new EmployeeProfile { AccountId = actor, FullName = user.FullName, EmployeeCode = username };
            db.Add(employee); await db.SaveChangesAsync();
            var group = new AccessGroup { Code = username, Name = username, NormalizedCode = username.ToUpperInvariant(), NormalizedName = username.ToUpperInvariant(), BaseRole = "ClinicAssistant" };
            foreach (var permission in new[] { "appointments.read", "appointments.reschedule", "appointments.cancel" }) group.Grants.Add(new AccessGroupGrant { GroupId = group.Id, Permission = permission });
            groupId = group.Id;
            db.AddRange(group, new SchedulingMember { BranchId = s.BranchId, EmployeeId = employee.Id }, new StaffAccessAssignment { AccountId = actor, GroupId = group.Id, BranchIds = [s.BranchId] }); await db.SaveChangesAsync();
        }
        using var staff = await Login(username); var slots = (await staff.GetFromJsonAsync<AvailabilityResult>(Path(a, "availability") + $"?date={s.Date:yyyy-MM-dd}"))!.Slots;
        var move = Move(a, slots.First()) with { ClinicInitiated = true };
        var response = await staff.PostAsJsonAsync(Path(a, "reschedule"), move); response.EnsureSuccessStatusCode();
        var moved = (await response.Content.ReadFromJsonAsync<AppointmentChangeResult>())!.Appointment;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.RemoveRange(await db.Set<AccessGroupGrant>().Where(x => x.GroupId == groupId && x.Permission != "appointments.read").ToArrayAsync()); await db.SaveChangesAsync();
        }
        var cancel = new CancelAppointmentRequest(moved.Version, Guid.NewGuid(), "SYNTHETIC revoked grant", ClinicInitiated: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(Path(a, "cancel"), cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync(Path(a, "reschedule"), move)).StatusCode);
        using var direct = fixture.Services.CreateScope();
        var denied = await Assert.ThrowsAsync<BusinessException>(() => direct.ServiceProvider.GetRequiredService<IAppointmentChangeService>().CancelAsync(actor, a.Code, cancel, CancellationToken.None));
        Assert.Equal(403, denied.Status);
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
    public async Task ValidationAndUnknownFollowUpLeaveNoPartialWrites()
    {
        var s = await Prepare(); var (client, _) = await Public(); var slots = await Slots(client, s); var a = await Book(client, s, slots[36]);
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a, "  ")), HttpStatusCode.BadRequest, "MSG02");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a, new string('a', 501))), HttpStatusCode.BadRequest, "MSG03");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { RequestKey = Guid.Empty }), HttpStatusCode.BadRequest, "MSG04");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a) with { ExpectedVersion = 0 }), HttpStatusCode.BadRequest, "MSG04");
        await SetState(a, followUp: Guid.NewGuid());
        await Message(await client.PostAsJsonAsync(Path(a, "reschedule"), Move(a, slots[40])),
            HttpStatusCode.NotFound, "MSG113");
        await Message(await client.PostAsJsonAsync(Path(a, "cancel"), Cancel(a)),
            HttpStatusCode.NotFound, "MSG113");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.FindAsync(a.AppointmentId);
        Assert.Equal("Confirmed", saved!.Status); Assert.Equal(1, saved.Version); Assert.Null(saved.CancelledAt);
        Assert.Empty(await db.AppointmentChanges.Where(x => x.AppointmentId == a.AppointmentId).ToListAsync());
        Assert.Single(await db.AppointmentOutboxEvents.Where(x => x.AggregateId == a.AppointmentId).ToListAsync());
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
