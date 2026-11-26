using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Appointments;
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
    [InlineData(60, true, true)] [InlineData(120, true, true)] [InlineData(15, true, true)] [InlineData(35, true, true)] [InlineData(1, false, true)]
    public void DurationPolicy(int duration, bool isPublic, bool valid) => Assert.Equal(valid, ScheduleRules.ValidDuration(duration, isPublic));
    [Fact]
    public void InvalidWeekOverlapAndBreakAreRejected()
    {
        Assert.False(ScheduleRules.ValidWeek(Monday.AddDays(1), []));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 780, "Work")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 705, 735, "Break", "Nghỉ")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 630, "Break")]));
        Assert.False(ScheduleRules.ValidWeek(Monday, [new(0, 480, 720, "Work"), new(0, 600, 645, "Break", "Nghỉ"), new(0, 630, 660, "Break", "Bận")]));
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
        // Recurring schedule versions are per employee across all branches, and the demo staff are shared by every test in this
        // class: start each test from an unpublished schedule so expected versions (0, 1, ...) belong to this test only.
        int[] staff = [dentist.Id, assistant.Id, other.Id, owner.Id];
        await db.Set<WorkingScheduleRevision>().Where(x => staff.Contains(x.EmployeeId)).ExecuteDeleteAsync();
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true }, new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = other.Id, CanDentist = true }, new SchedulingMember { BranchId = branch.Id, EmployeeId = owner.Id, CanDentist = true, CanManageSchedules = true });
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", PublicDurationMinutes = 60, IsInitialExamination = true };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "Chair", OpenMinute = 480, CloseMinute = 1080 };
        await db.AddSchedulingDataAsync(service, chair); await db.SaveChangesAsync();
        return new(branch.Id, dentist.Id, assistant.Id, other.Id, service.Id, chair.Id, dentist.AccountId, owner.AccountId, Date.AddDays(branch.Id * 7));
    }
    [Fact]
    public async Task DentistConfiguresFlexibleServicesAndSlotsWithReplayAndScopeProtection()
    {
        var s = await Create(); using var dentist = await Login();
        var path = $"/api/booking-settings/{s.Branch}";
        var original = (await dentist.GetFromJsonAsync<BookingSettings>(path))!;
        Assert.Equal(15, original.SlotIntervalMinutes);
        var command = new SaveBookingSettings(original.Version, Guid.NewGuid(), 20,
            [new(s.Service, "Exam 35", 35, true), new(0, "Treatment 95", 95, true, true, true)]);
        var savedResponse = await dentist.PutAsJsonAsync(path, command);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<BookingSettings>())!;
        Assert.Equal(original.Version + 1, saved.Version); Assert.Equal(20, saved.SlotIntervalMinutes);
        Assert.Equal(95, saved.Services.Single(x => x.Name == "Treatment 95").DurationMinutes);
        Assert.True(saved.Services.Single(x => x.Id == s.Service).IsInitialExamination);
        Assert.Equal("Khám", saved.Services.Single(x => x.Id == s.Service).Name);
        var treatment = saved.Services.Single(x => x.Name == "Treatment 95");
        Assert.False(treatment.IsInitialExamination); Assert.False(treatment.PublicBookable);
        using var anonymous = fixture.CreateClient();
        var publicOptions = (await anonymous.GetFromJsonAsync<ScheduleOptions>($"/api/availability/options?branchId={s.Branch}"))!;
        Assert.Equal(s.Service, Assert.Single(publicOptions.Services).Id);
        await Message(await anonymous.GetAsync($"/api/availability?branchId={s.Branch}&dentistId={s.Dentist}&serviceId={treatment.Id}&date={s.Date:yyyy-MM-dd}&durationMinutes=95"), HttpStatusCode.BadRequest, "MSG40");
        var forged = new CreateAppointmentRequest(s.Branch, new("Synthetic booker", "0987654321", null), new(null, "Synthetic attendee", null),
            "Self", "Standard", "App", treatment.Id, null, s.Dentist, null, Start(s, 480), 95,
            null, null, Guid.NewGuid(), 1, null);
        await Message(await anonymous.PostAsJsonAsync("/api/appointments", forged), HttpStatusCode.BadRequest, "MSG40");
        var replay = (await (await dentist.PutAsJsonAsync(path, command)).Content.ReadFromJsonAsync<BookingSettings>())!;
        Assert.Equal(saved.Version, replay.Version); Assert.Equal(2, replay.Services.Length);
        await Message(await dentist.PutAsJsonAsync(path, command with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG41");
        await Message(await dentist.PutAsJsonAsync(path, command with { SlotIntervalMinutes = 30 }), HttpStatusCode.Conflict, "MSG41");
        await Message(await dentist.PutAsJsonAsync(path, new SaveBookingSettings(saved.Version, Guid.NewGuid(), 0, saved.Services)), HttpStatusCode.BadRequest, "MSG04");
        await Message(await dentist.PutAsJsonAsync(path, new SaveBookingSettings(saved.Version, Guid.NewGuid(), 20,
            [saved.Services[0] with { Id = int.MaxValue }, saved.Services[1]])), HttpStatusCode.BadRequest, "MSG04");
        using var assistant = await Login("assistant.demo");
        await Message(await assistant.PostAsJsonAsync("/api/appointments", forged with { Source = "InPerson", RequestKey = Guid.NewGuid() }), HttpStatusCode.BadRequest, "MSG40");
        await Message(await assistant.GetAsync(path), HttpStatusCode.Forbidden, "MSG05");
        using var guest = await Login("guest.demo");
        await Message(await guest.GetAsync(path), HttpStatusCode.Forbidden, "MSG05");
        await Message(await dentist.GetAsync("/api/booking-settings/2147483647"), HttpStatusCode.Forbidden, "MSG05");
    }

    [Fact]
    public async Task ConfiguredGridAndServiceDurationApplyToQueryBookingStaleSettingsAndReschedule()
    {
        var s = await Create(); using var dentist = await Login();
        await Publish(dentist, Command(s));
        var path = $"/api/booking-settings/{s.Branch}";
        var settings = (await dentist.GetFromJsonAsync<BookingSettings>(path))!;
        var saved = (await (await dentist.PutAsJsonAsync(path, new SaveBookingSettings(settings.Version, Guid.NewGuid(), 20,
            [new(s.Service, "Exam 35", 35, true)]))).Content.ReadFromJsonAsync<BookingSettings>())!;
        using var anonymous = fixture.CreateClient();
        var query = $"/api/availability?branchId={s.Branch}&dentistId={s.Dentist}&serviceId={s.Service}&date={s.Date:yyyy-MM-dd}&durationMinutes=35";
        var slots = (await anonymous.GetFromJsonAsync<AvailabilityResult>(query))!.Slots;
        Assert.Contains(slots, x => x.Start == Start(s, 500));
        Assert.DoesNotContain(slots, x => x.Start == Start(s, 495));
        Assert.All(slots, x => { Assert.Equal(35, (x.End - x.Start).TotalMinutes); Assert.Equal(saved.Version, x.BookingSettingsVersion); });
        Assert.DoesNotContain(slots, x => x.Start == Start(s, 700) || x.Start == Start(s, 1060));
        await Message(await anonymous.GetAsync(query.Replace("durationMinutes=35", "durationMinutes=30")), HttpStatusCode.BadRequest, "MSG40");
        var slot = slots.First();
        CreateAppointmentRequest Booking(AvailabilitySlot value) => new(s.Branch, new("Synthetic booker", "0987654321", null),
            new(null, "Synthetic attendee", null), "Self", "Standard", "App", s.Service, null, s.Dentist, null,
            value.Start, 35, null, null, Guid.NewGuid(), value.ScheduleVersion, null, value.BookingSettingsVersion);
        await Message(await anonymous.PostAsJsonAsync("/api/appointments", Booking(slot) with { StartAt = slot.Start.AddMinutes(1) }), HttpStatusCode.Conflict, "MSG40");
        var bookedResponse = await anonymous.PostAsJsonAsync("/api/appointments", Booking(slot));
        Assert.Equal(HttpStatusCode.Created, bookedResponse.StatusCode);
        var booked = (await bookedResponse.Content.ReadFromJsonAsync<AppointmentResult>())!;
        var remaining = (await anonymous.GetFromJsonAsync<AvailabilityResult>(query))!.Slots;
        Assert.DoesNotContain(remaining, x => x.Start < booked.EndAt && x.End > booked.StartAt);
        var changed = (await (await dentist.PutAsJsonAsync(path, new SaveBookingSettings(saved.Version, Guid.NewGuid(), 20,
            [new(s.Service, "Exam 55", 55, true)]))).Content.ReadFromJsonAsync<BookingSettings>())!;
        await Message(await anonymous.PostAsJsonAsync("/api/appointments", Booking(remaining.Last())), HttpStatusCode.Conflict, "MSG41");
        var detail = (await dentist.GetFromJsonAsync<StaffAppointmentDetail>($"/api/appointments/{booked.AppointmentId}"))!;
        Assert.Equal(35, detail.Appointment.DurationMinutes);
        using var assistant = await Login("assistant.demo");
        var moves = (await assistant.GetFromJsonAsync<AvailabilityResult>($"/api/appointments/{booked.Code}/availability?date={s.Date:yyyy-MM-dd}"))!.Slots;
        Assert.All(moves, x => Assert.Equal(35, (x.End - x.Start).TotalMinutes));
        var target = moves.Last();
        var moved = await assistant.PostAsJsonAsync($"/api/appointments/{booked.Code}/reschedule", new RescheduleAppointmentRequest(
            booked.Version, Guid.NewGuid(), target.Start, target.ChairId, target.ScheduleVersion, null, "Synthetic move", "Verified requester",
            BookingSettingsVersion: changed.Version));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var disabled = await dentist.PutAsJsonAsync(path, new SaveBookingSettings(changed.Version, Guid.NewGuid(), 20,
            [new(s.Service, "Exam 55", 55, false, PublicBookable: false)]));
        Assert.True(disabled.IsSuccessStatusCode, await disabled.Content.ReadAsStringAsync());
        Assert.False((await disabled.Content.ReadFromJsonAsync<BookingSettings>())!.Services.Single().Active);
        var publicOptions = (await anonymous.GetFromJsonAsync<ScheduleOptions>($"/api/availability/options?branchId={s.Branch}"))!;
        Assert.DoesNotContain(publicOptions.Services, x => x.Id == s.Service);
        await Message(await anonymous.GetAsync(query.Replace("durationMinutes=35", "durationMinutes=55")), HttpStatusCode.BadRequest, "MSG40");
    }

    // docs/specs/CHAIR-CAPACITY-ANY-DENTIST-BOOKING.vi.md: chairs are branch capacity; "any dentist" assigns a free on-shift
    // dentist and a free chair, prefers the dentist with the fewest bookings that day and stops at N chairs per slot.
    [Fact]
    public async Task AnyDentistBookingFillsFreeChairsAcrossDentistsSharingAShift()
    {
        var s = await Create(); using var owner = await Login("owner.demo");
        await Publish(owner, Command(s)); await Publish(owner, Command(s, employee: s.Other));
        await Publish(owner, Command(s, employee: (await OwnerEmployee())));
        int secondChair;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var chair = new SchedulingChair { BranchId = s.Branch, Name = "Chair 2" };
            await db.AddSchedulingDataAsync(chair); await db.SaveChangesAsync(); secondChair = chair.Id;
        }
        using var anonymous = fixture.CreateClient();
        var any = $"/api/availability?branchId={s.Branch}&serviceId={s.Service}&date={s.Date:yyyy-MM-dd}&durationMinutes=60";
        async Task<AvailabilitySlot[]> Slots(string query) => (await anonymous.GetFromJsonAsync<AvailabilityResult>(query))!.Slots;
        CreateAppointmentRequest Booking(AvailabilitySlot value, string attendee) => new(s.Branch, new("Synthetic booker", "0987654321", null),
            new(null, attendee, null), "Self", "Standard", "App", s.Service, null, value.DentistId, null,
            value.Start, 60, null, null, Guid.NewGuid(), value.ScheduleVersion, null, value.BookingSettingsVersion);

        // Omitted dentistId and dentistId=0 are the same query; one slot per start, matching a single dentist's grid.
        var initial = await Slots(any);
        Assert.Equal(initial.Select(x => (x.Start, x.DentistId, x.ChairId)), (await Slots(any + "&dentistId=0")).Select(x => (x.Start, x.DentistId, x.ChairId)));
        Assert.Equal(initial.Length, initial.Select(x => x.Start).Distinct().Count());
        Assert.Equal((await Slots(any + $"&dentistId={s.Dentist}")).Select(x => x.Start), initial.Select(x => x.Start));
        await Message(await anonymous.GetAsync(any + $"&assistantId={s.Assistant}"), HttpStatusCode.BadRequest, "MSG40");

        var nine = Start(s, 540);
        var first = initial.Single(x => x.Start == nine);
        Assert.Equal(s.Chair, first.ChairId);
        Assert.Equal(HttpStatusCode.Created, (await anonymous.PostAsJsonAsync("/api/appointments", Booking(first, "Attendee one"))).StatusCode);

        // Same time: next free chair, a different dentist. Another time: least-loaded dentist is preferred.
        var afterFirst = await Slots(any);
        var second = afterFirst.Single(x => x.Start == nine);
        Assert.NotEqual(first.DentistId, second.DentistId); Assert.Equal(secondChair, second.ChairId);
        Assert.NotEqual(first.DentistId, afterFirst.Single(x => x.Start == Start(s, 600)).DentistId);
        Assert.Equal(HttpStatusCode.Created, (await anonymous.PostAsJsonAsync("/api/appointments", Booking(second, "Attendee two"))).StatusCode);

        // Two chairs are full at 09:00 although a third dentist is on shift: no slot, for any dentist or that dentist.
        Assert.DoesNotContain(await Slots(any), x => x.Start < nine.AddMinutes(60) && x.End > nine);
        var third = new[] { s.Dentist, s.Other, await OwnerEmployee() }.Single(x => x != first.DentistId && x != second.DentistId);
        Assert.DoesNotContain(await Slots(any + $"&dentistId={third}"), x => x.Start == nine);
        await Message(await anonymous.PostAsJsonAsync("/api/appointments", Booking(first with { DentistId = third,
            ScheduleVersion = 1 }, "Attendee three")), HttpStatusCode.Conflict, "MSG23");

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var booked = await db.Appointments.AsNoTracking().Where(x => x.BranchId == s.Branch && x.StartAt == nine.ToUniversalTime()).ToArrayAsync();
            Assert.Equal(2, booked.Length);
            Assert.Equal(2, booked.Select(x => x.ChairId).Distinct().Count());
            Assert.Equal(2, booked.Select(x => x.DentistId).Distinct().Count());
        }
    }
    private async Task<int> OwnerEmployee()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        return await (from e in db.Employees join a in db.Users on e.AccountId equals a.Id where a.UserName == "owner.demo" select e.Id).SingleAsync();
    }

    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient(); var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken); return client;
    }
    // Weekly template published through the recurring schedule API (docs/specs/UNUSED-API-REMOVAL.vi.md).
    private static RecurringCommand Command(Setup s, int version = 0, int? employee = null, SchedulePeriod[]? periods = null) =>
        new(employee ?? s.Dentist, s.Date, "Template", version,
            (periods ?? [new(0, 480, 1080, "Work"), new(0, 720, 780, "Break", "Nghỉ trưa")])
                .Select(p => new BranchPeriod(s.Branch, p.Day, p.StartMinute, p.EndMinute, p.Kind, p.Reason)).ToArray(), Guid.NewGuid());
    private static async Task<RecurringPublishResult> Publish(HttpClient client, RecurringCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/working-schedules/recurring/publish", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RecurringPublishResult>())!;
    }
    private static DateTimeOffset Start(Setup s, int minute) => new DateTimeOffset(s.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(minute);
    private async Task<DCMS.Domain.Features.Appointments.Appointment> Reserve(Setup s, int start, int end, int? dentist = null, int? patientId = null, string status = "Confirmed")
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var appointment = new DCMS.Domain.Features.Appointments.Appointment
        {
            BranchId = s.Branch, DentistId = dentist ?? s.Dentist, ChairId = s.Chair, ServiceId = s.Service,
            Code = "AP-" + Guid.NewGuid().ToString("N"), StartAt = Start(s, start).ToUniversalTime(), EndAt = Start(s, end).ToUniversalTime(),
            PatientId = patientId, AttendeeName = "Synthetic attendee", Status = status, CreatedAt = DateTimeOffset.UtcNow,
            BookingInfo = new() { BookerName = "Synthetic booker" }
        };
        db.Add(appointment); await db.SaveChangesAsync(); return appointment;
    }
    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code); }

    [Fact]
    public async Task InvalidCommandsAndOwnershipCannotChangePublishedWeek()
    {
        var s = await Create(); using var dentist = await Login(); using var assistant = await Login("assistant.demo");
        using var guest = await Login("guest.demo"); using var anonymous = fixture.CreateClient();
        await Message(await anonymous.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s)), HttpStatusCode.Unauthorized, "MSG06");
        await Message(await guest.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s)), HttpStatusCode.Forbidden, "MSG05");
        await Message(await assistant.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s)), HttpStatusCode.Forbidden, "MSG05");
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, employee: s.Other)), HttpStatusCode.Forbidden, "MSG05");
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, periods: [new(0, 600, 480, "Work")])), HttpStatusCode.BadRequest, "MSG40");
        await Publish(dentist, Command(s));
        await Message(await dentist.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s)), HttpStatusCode.Conflict, "MSG41");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, (await db.Set<WorkingScheduleRevision>().SingleAsync(w => w.EmployeeId == s.Dentist)).Version);
    }
    [Fact]
    public async Task ConfirmedAppointmentBlocksPublicationAndPreviewListsImpact()
    {
        var s = await Create(); using var client = await Login(); await Publish(client, Command(s));
        var booked = await Reserve(s, 480, 600); var command = Command(s, 1, periods: [new(0, 600, 1080, "Work")]);
        var preview = await client.PostAsJsonAsync("/api/working-schedules/recurring/preview", command);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode); var impact = (await preview.Content.ReadFromJsonAsync<SchedulePreview>())!;
        Assert.Equal(booked.Id, Assert.Single(impact.AffectedAppointments).Id);
        await Message(await client.PostAsJsonAsync("/api/working-schedules/recurring/publish", command), HttpStatusCode.Conflict, "MSG32");
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        // The blocked publication leaves the first revision as the only one, with its original 08:00 start.
        var head = await db.Set<WorkingScheduleRevision>().AsNoTracking().SingleAsync(w => w.EmployeeId == s.Dentist);
        Assert.Equal(1, head.Version); Assert.Equal(480, head.Periods.Min(p => p.StartMinute));
    }
    [Fact]
    public async Task ChairMainAssistantAndAttendeeConflictsAreCheckedForEntireInterval()
    {
        var s = await Create(); using var owner = await Login("owner.demo");
        await Publish(owner, Command(s)); await Publish(owner, Command(s, employee: s.Assistant)); await Publish(owner, Command(s, employee: s.Other));
        int patientId;
        using (var pScope = fixture.Services.CreateScope())
        {
            var pDb = pScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var patient = new DCMS.Domain.Features.Patients.PatientProfile
            {
                PatientCode = "SCH-" + Guid.NewGuid().ToString("N"), FullName = "Synthetic patient",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };
            pDb.Add(patient); await pDb.SaveChangesAsync(); patientId = patient.Id;
        }
        await Reserve(s, 540, 555, dentist: s.Other, patientId: patientId);
        using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(s.Branch, s.Dentist, s.Service, Start(s, 480), 120, s.Chair, s.Assistant, null, 1, 1);
        Assert.Equal("MSG23", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default))).Response.Code);
        // Move occupancy to another chair and reassign to assistant: main's assistant remains unavailable, independent of chair capacity.
        var occupancy = await db.Appointments.SingleAsync(x => x.BranchId == s.Branch);
        var extraChair = new SchedulingChair { BranchId = s.Branch, Name = "Other" }; await db.AddSchedulingDataAsync(extraChair); await db.SaveChangesAsync();
        occupancy.ChairId = extraChair.Id; occupancy.DentistId = s.Assistant; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default));
        occupancy.DentistId = s.Other; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { PatientId = patientId }, default));
        var available = await service.CheckInTransactionAsync(request with { DurationMinutes = 60 }, default);
        Assert.Equal(Start(s, 540), available.End); // adjacency at occupancy start is allowed
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { AssistantId = s.Dentist }, default));
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { ExpectedScheduleVersion = 0 }, default));
        var member = await db.LoadMemberAsync(s.Branch, s.Assistant); member.Active = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default));
    }
    [Fact]
    public async Task SchedulePublicationProtectsConfirmedAppointments()
    {
        var s = await Create(); using var owner = await Login("owner.demo");
        await Publish(owner, Command(s));
        await Reserve(s, 480, 600, dentist: s.Dentist);
        await Message(await owner.PostAsJsonAsync("/api/working-schedules/recurring/publish", Command(s, 1, s.Dentist, [])), HttpStatusCode.Conflict, "MSG32");
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
            int patientId;
            if (first)
            {
                var p = new DCMS.Domain.Features.Patients.PatientProfile { PatientCode = "CON-" + Guid.NewGuid().ToString("N"), FullName = "Concurrent patient", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                db.Add(p); await db.SaveChangesAsync(); patientId = p.Id;
            }
            else patientId = 0;
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!first) await ready.Task;
            try
            {
                await service.CheckInTransactionAsync(new(s.Branch, s.Dentist, s.Service, Start(s, 480), 60, s.Chair, null, null, 1), default);
                if (first) ready.SetResult();
                db.Add(new DCMS.Domain.Features.Appointments.Appointment { BranchId = s.Branch, DentistId = s.Dentist, ServiceId = s.Service, ChairId = s.Chair,
                    Code = "AP-" + Guid.NewGuid().ToString("N"), StartAt = Start(s, 480).ToUniversalTime(), EndAt = Start(s, 540).ToUniversalTime(),
                    PatientId = first ? patientId : null, AttendeeName = "Concurrent patient", CreatedAt = DateTimeOffset.UtcNow, BookingInfo = new() { BookerName = "Concurrent" } });
                await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
            }
            catch (BusinessException ex) { Assert.Equal("MSG22", ex.Response.Code); return false; }
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
        var service = new DCMS.Infrastructure.Features.Scheduling.WorkingScheduleService(db, new FixedClock(Start(s, 480)), scope.ServiceProvider.GetRequiredService<DCMS.Application.Features.Settings.ISettingsAccess>());
        var query = new AvailabilityQuery(s.Branch, s.Dentist, s.Service, s.Date, 60, s.Assistant);
        var result = await service.QueryAsync(query, null, default);
        Assert.DoesNotContain(result.Slots, x => x.Start == Start(s, 525)); Assert.Contains(result.Slots, x => x.Start == Start(s, 540));
        await Assert.ThrowsAsync<BusinessException>(() => service.QueryAsync(query with { DurationMinutes = 30 }, null, default));
        await using var tx = await db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(s.Branch, s.Dentist, s.Service, Start(s, 540), 60, s.Chair, s.Assistant, null, 1, 0);
        Assert.Equal("MSG41", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request, default))).Response.Code);
        Assert.Equal("MSG21", (await Assert.ThrowsAsync<BusinessException>(() => service.CheckInTransactionAsync(request with { ExpectedAssistantScheduleVersion = 1, Start = Start(s, 690) }, default))).Response.Code);
        var member = await db.LoadMemberAsync(s.Branch, s.Dentist); member.CanDentist = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(() => service.QueryAsync(query, null, default));
    }
    [Fact]
    public async Task TransactionRequiredAndDraftPreviewDoesNotPublishOrHoldSlots()
    {
        var s = await Create(); using var client = await Login();
        using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckInTransactionAsync(new(s.Branch, s.Dentist, s.Service, Start(s, 480), 60, s.Chair, null, null, 0), default));
        var query = new AvailabilityQuery(s.Branch, s.Dentist, s.Service, s.Date, 60);
        var response = await client.PostAsJsonAsync("/api/working-schedules/recurring/preview-availability",
            new RecurringPreviewRequest(Command(s), s.Branch, s.Service, s.Date, s.Date, 60));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(Assert.Single((await response.Content.ReadFromJsonAsync<RecurringAvailabilityResult>())!.Days).Slots);
        Assert.Empty((await service.QueryAsync(query, null, default)).Slots);
        Assert.False(await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Appointments.AnyAsync(x => x.BranchId == s.Branch));
    }
}
