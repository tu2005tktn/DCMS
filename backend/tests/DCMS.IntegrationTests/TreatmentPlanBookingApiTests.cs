using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Treatment;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Booking the next visit of a treatment dossier (docs/specs/FOLLOW-UP-INSTRUCTION-REMOVAL.vi.md), using an isolated PostgreSQL schema.
public sealed class TreatmentPlanBookingApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(int Patient, int Branch, int Dentist, int Service, DateOnly Date, Guid Dossier, Guid Item);
    private CreateAppointmentRequest? lastCommand;
    private int outboxBefore;

    private async Task AssertRejectedWithoutWrites(Setup s, HttpResponseMessage response,
        HttpStatusCode expectedStatus = HttpStatusCode.Conflict, string expectedCode = "MSG54")
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedCode, (await response.Content.ReadFromJsonAsync<DCMS.Application.Common.SystemMessage>())!.Code);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.Appointments.AnyAsync(x => x.PatientId == s.Patient));
        Assert.False(await db.AppointmentReceipts.AnyAsync(x => x.RequestKey == lastCommand!.RequestKey));
        Assert.Equal(outboxBefore, await db.AppointmentOutboxEvents.CountAsync());
    }

    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }

    private async Task<Setup> Prepare(int dayOffset = 7)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
        var assistant = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "assistant.demo" select e).SingleAsync();
        var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(dayOffset);
        var branch = new SchedulingBranch { Name = "SYNTHETIC booking audit " + Guid.NewGuid() };
        var patient = new PatientProfile { PatientCode = "AUDIT-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC audit patient", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.AddRange(branch, patient); await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC planned service", PublicBookable = false, Unit = "răng" };
        await db.AddSchedulingDataAsync(service, new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC chair", OpenMinute = 480, CloseMinute = 1080 },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist.Id, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = assistant.Id });
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = dentist.Id, WeekStart = monday, PublishedBy = dentist.AccountId, PublishedAt = DateTimeOffset.UtcNow };
        for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 480, EndMinute = 1080, Kind = "Work" });
        db.Add(week); await db.SaveChangesAsync();
        var dossier = new TreatmentDossier { PatientId = patient.Id, BranchId = branch.Id, DentistId = dentist.Id, Code = "AUD-" + Guid.NewGuid().ToString("N"), StartedOn = date.AddDays(-7), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var item = new TreatmentItem { DossierId = dossier.Id, ServiceId = service.Id, ServiceName = service.Name, Unit = "răng", Quantity = 1, PlannedOrder = 1, EstimatedMinutes = 30, EstimatedStart = date, AgreedUnitPriceVnd = 100000, ResponsibleDentistId = dentist.Id };
        dossier.Items.Add(item); db.Add(dossier); await db.SaveChangesAsync();
        return new(patient.Id, branch.Id, dentist.Id, service.Id, date, dossier.Id, item.Id);
    }

    private async Task<NextVisitCandidate[]> Candidates(HttpClient client, Setup s) =>
        (await client.GetFromJsonAsync<NextVisitCandidates>($"/api/patients/{s.Patient}/next-visit-candidates"))!.Items.ToArray();

    private async Task<HttpResponseMessage> Book(HttpClient client, Setup s, Guid? dossier, bool patientConsent = true, int durationMinutes = 30)
    {
        var queryDuration = durationMinutes is >= 1 and <= 480 ? durationMinutes : 30;
        var availability = await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.Branch}&dentistId={s.Dentist}&serviceId={s.Service}&date={s.Date:yyyy-MM-dd}&durationMinutes={queryDuration}");
        Assert.NotEmpty(availability!.Slots);
        var slot = availability.Slots[0];
        using (var scope = fixture.Services.CreateScope()) outboxBefore = await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().AppointmentOutboxEvents.CountAsync();
        lastCommand = new CreateAppointmentRequest(s.Branch,
            new BookerContact("SYNTHETIC audit", "0912345678", null), new AppointmentAttendee(s.Patient, "SYNTHETIC audit patient", null),
            "Self", dossier.HasValue ? "FollowUp" : "Standard", "InPerson", s.Service, "SYNTHETIC audit", s.Dentist, slot.ChairId, slot.Start, durationMinutes,
            dossier, null, Guid.NewGuid(), slot.ScheduleVersion, slot.AssistantScheduleVersion, slot.BookingSettingsVersion, PatientConsentConfirmed: patientConsent);
        return await client.PostAsJsonAsync("/api/appointments", lastCommand);
    }

    [Fact]
    public async Task MissingPatientConsentDoesNotCreateAnyBookingWrites()
    {
        var s = await Prepare(); using var client = await Login();
        await AssertRejectedWithoutWrites(s, await Book(client, s, s.Dossier, false),
            HttpStatusCode.BadRequest, "MSG65");
        Assert.Equal(HttpStatusCode.Created, (await Book(client, s, s.Dossier)).StatusCode);
    }

    [Fact]
    public async Task StandardBookingDoesNotRequireTreatmentPatientConsent()
    {
        var s = await Prepare();
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.Set<SchedulingService>().SingleAsync(x => x.Id == s.Service)).IsInitialExamination = true;
            await db.SaveChangesAsync();
        }
        using var assistant = await Login("assistant.demo");
        var response = await Book(assistant, s, null, false); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        using var verification = fixture.Services.CreateScope();
        var row = await verification.ServiceProvider.GetRequiredService<DcmsDbContext>().Appointments.AsNoTracking().SingleAsync(x => x.Id == appointment.AppointmentId);
        Assert.Null(row.DossierId);
    }

    [Fact]
    public async Task WorkingScheduleShowsSavedConsentAndCurrentAppointmentChanges()
    {
        var s = await Prepare(); using var client = await Login();
        var response = await Book(client, s, s.Dossier); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        var path = $"/api/working-schedules/appointments?dentistId={s.Dentist}&from={s.Date:yyyy-MM-dd}&to={s.Date:yyyy-MM-dd}";
        var items = (await client.GetFromJsonAsync<ScheduleAppointments>(path))!.Items;
        var row = Assert.Single(items, item => item.Id == appointment.AppointmentId);
        Assert.True(row.IsTreatmentPlan); Assert.Equal("Confirmed", row.Status);
        using var assistant = await Login("assistant.demo");
        var cancelled = await assistant.PostAsJsonAsync($"/api/appointments/{appointment.Code}/cancel",
            new CancelAppointmentRequest(appointment.Version, Guid.NewGuid(), "SYNTHETIC cancelled", "SYNTHETIC verified patient"));
        Assert.True(cancelled.IsSuccessStatusCode, await cancelled.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", (await client.GetFromJsonAsync<ScheduleAppointments>(path))!.Items.Single(item => item.Id == appointment.AppointmentId).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/working-schedules/appointments?dentistId={s.Dentist}&from={s.Date:yyyy-MM-dd}&to={s.Date.AddDays(31):yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task DoctorScheduleRejectsOtherDoctorsAndFiltersAssignmentBranches()
    {
        var first = await Prepare(); var second = await Prepare(); using var client = await Login();
        foreach (var setup in new[] { first, second })
            Assert.Equal(HttpStatusCode.Created, (await Book(client, setup, setup.Dossier)).StatusCode);
        int otherDentist;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            otherDentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "owner.demo" select e.Id).SingleAsync();
            await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = first.Branch, EmployeeId = otherDentist, CanDentist = true }); await db.SaveChangesAsync();
        }
        var range = $"from={first.Date:yyyy-MM-dd}&to={first.Date:yyyy-MM-dd}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/working-schedules/appointments?dentistId={otherDentist}&{range}")).StatusCode);
        using var owner = await Login("owner.demo");
        var groupResponse = await owner.PostAsJsonAsync("/api/system-settings/access-groups/", new GroupCommand(Guid.NewGuid(),
            new AccessGroupValue("CAL-" + Guid.NewGuid().ToString("N"), "SYNTHETIC schedule reader", "Dentist", ["working-schedules.read"])));
        Assert.True(groupResponse.IsSuccessStatusCode, await groupResponse.Content.ReadAsStringAsync());
        var group = (await groupResponse.Content.ReadFromJsonAsync<GroupDto>())!;
        var staff = (await owner.GetFromJsonAsync<StaffAccessDto[]>("/api/system-settings/access-groups/staff"))!.Single(x => x.EmployeeId == first.Dentist);
        var assignment = await owner.PutAsJsonAsync($"/api/system-settings/staff/{staff.EmployeeId}/access-group",
            new AssignGroup(group.Id, staff.AccountVersion, group.Version, Guid.NewGuid(), [first.Branch], "SYNTHETIC branch scope"));
        Assert.True(assignment.IsSuccessStatusCode, await assignment.Content.ReadAsStringAsync());
        try {
            var report = (await client.GetFromJsonAsync<ScheduleAppointments>($"/api/working-schedules/appointments?dentistId={first.Dentist}&{range}"))!;
            Assert.Equal(first.Branch, Assert.Single(report.Items).BranchId);
        } finally {
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var assigned = await db.Set<DCMS.Domain.Features.Settings.StaffAccessAssignment>().SingleAsync(x => x.AccountId == staff.AccountId);
            db.Remove(assigned); await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task FollowUpBookingStoresTheDossierAndMarksItsSuggestionBooked()
    {
        var s = await Prepare(); using var client = await Login();
        var ready = Assert.Single(await Candidates(client, s));
        Assert.Equal(s.Item, ready.ItemId); Assert.Equal("Ready", ready.Status); Assert.Null(ready.Appointment);
        var response = await Book(client, s, s.Dossier);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var row = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == created.AppointmentId);
            Assert.Equal(s.Dossier, row.DossierId); Assert.Equal("FollowUp", row.Kind);
        }
        var booked = Assert.Single(await Candidates(client, s));
        Assert.Equal("Booked", booked.Status); Assert.Equal(created.AppointmentId, booked.Appointment!.Id);
    }

    [Fact]
    public async Task EachDossierSuggestsOnlyItsNearestPendingItem()
    {
        var s = await Prepare(); using var client = await Login(); Guid later;
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var item = new TreatmentItem { DossierId = s.Dossier, ServiceId = s.Service, ServiceName = "SYNTHETIC later service", Unit = "răng", Quantity = 1,
                PlannedOrder = 2, EstimatedMinutes = 45, EstimatedStart = s.Date.AddDays(14), AgreedUnitPriceVnd = 100000,
                ResponsibleDentistId = s.Dentist };
            db.Add(item); later = item.Id; await db.SaveChangesAsync();
        }
        Assert.Equal(s.Item, Assert.Single(await Candidates(client, s)).ItemId);
        var marked = await client.PatchAsJsonAsync($"/api/dossiers/{s.Dossier}/sheet-items/{s.Item}/display-completion", new MarkSheetItemRequest(1, true, Guid.NewGuid()));
        Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync());
        Assert.Equal(later, Assert.Single(await Candidates(client, s)).ItemId);
    }

    [Fact]
    public async Task CancelledFollowUpLeavesTheDossierReadyToBookAgain()
    {
        var s = await Prepare(); using var dentist = await Login();
        var booked = await Book(dentist, s, s.Dossier); booked.EnsureSuccessStatusCode();
        var appointment = (await booked.Content.ReadFromJsonAsync<AppointmentResult>())!;
        var response = await dentist.PostAsJsonAsync($"/api/appointments/{appointment.Code}/cancel",
            new CancelAppointmentRequest(appointment.Version, Guid.NewGuid(), "SYNTHETIC clinic changes plan", ClinicInitiated: true));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var next = Assert.Single(await Candidates(dentist, s));
        Assert.Equal(s.Item, next.ItemId); Assert.Equal("Ready", next.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(480)]
    public async Task FollowUpBookingUsesEditedDurationWithoutChangingThePlan(int durationMinutes)
    {
        // These cases share the demo dentist: put long reservations on separate days from the regression fixtures.
        var s = await Prepare(durationMinutes == 1 ? 10 : durationMinutes == 37 ? 11 : 12);
        using var client = await Login();
        var response = await Book(client, s, s.Dossier, durationMinutes: durationMinutes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booked = (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
        Assert.Equal(TimeSpan.FromMinutes(durationMinutes), booked.EndAt - booked.StartAt);
        var replay = await client.PostAsJsonAsync("/api/appointments", lastCommand);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(booked.AppointmentId, (await replay.Content.ReadFromJsonAsync<AppointmentResult>())!.AppointmentId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == booked.AppointmentId);
        Assert.Equal(TimeSpan.FromMinutes(durationMinutes), saved.EndAt - saved.StartAt);
        Assert.Equal(30, (await db.TreatmentDossiers.AsNoTracking().Where(x => x.Id == s.Dossier).SelectMany(x => x.Items).SingleAsync(x => x.Id == s.Item)).EstimatedMinutes);
        var changedKeyContent = await client.PostAsJsonAsync("/api/appointments", lastCommand! with { DurationMinutes = durationMinutes == 1 ? 2 : 1 });
        Assert.Equal(HttpStatusCode.Conflict, changedKeyContent.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(481)]
    public async Task EditedDurationOutsideBoundsDoesNotCreateBooking(int durationMinutes)
    {
        var s = await Prepare(); using var client = await Login();
        await AssertRejectedWithoutWrites(s, await Book(client, s, s.Dossier, durationMinutes: durationMinutes), HttpStatusCode.BadRequest, "MSG04");
    }

    [Fact]
    public async Task ClosedDossierCannotBeBooked()
    {
        var s = await Prepare(); using var client = await Login();
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.TreatmentDossiers.SingleAsync(x => x.Id == s.Dossier)).State = "Closed";
            await db.SaveChangesAsync();
        }
        Assert.Empty(await Candidates(client, s));
        await AssertRejectedWithoutWrites(s, await Book(client, s, s.Dossier));
    }

    [Fact]
    public async Task AnotherPatientsDossierCannotBeBooked()
    {
        var s = await Prepare(); var other = await Prepare(); using var client = await Login();
        await AssertRejectedWithoutWrites(s, await Book(client, s, other.Dossier), HttpStatusCode.Forbidden, "MSG05");
    }

    [Fact]
    public async Task FollowUpKindRequiresADossier()
    {
        var s = await Prepare(); using var client = await Login();
        Assert.Equal(HttpStatusCode.Created, (await Book(client, s, s.Dossier)).StatusCode);
        using var assistant = await Login("assistant.demo");
        var withoutDossier = await assistant.PostAsJsonAsync("/api/appointments", lastCommand! with { DossierId = null, RequestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, withoutDossier.StatusCode);
    }

    [Fact]
    public async Task CompletedOrDeletedItemsDisappearFromSuggestions()
    {
        var s = await Prepare(); using var client = await Login();
        var marked = await client.PatchAsJsonAsync($"/api/dossiers/{s.Dossier}/sheet-items/{s.Item}/display-completion", new MarkSheetItemRequest(1, true, Guid.NewGuid()));
        Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync());
        Assert.Empty(await Candidates(client, s));
        var other = await Prepare();
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, $"/api/dossiers/{other.Dossier}/sheet-items/{other.Item}") { Content = JsonContent.Create(new SheetItemActionRequest(1, Guid.NewGuid())) };
        var deleted = await client.SendAsync(deletion);
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        Assert.Empty(await Candidates(client, other));
    }

    [Fact]
    public async Task PostedActivityHidesTheItemUntilItIsDeletedFromTheSheet()
    {
        var s = await Prepare(); using var client = await Login();
        var performed = await client.PostAsJsonAsync($"/api/dossiers/{s.Dossier}/sheet-items/{s.Item}/perform", new SheetItemActionRequest(1, Guid.NewGuid()));
        Assert.True(performed.IsSuccessStatusCode, await performed.Content.ReadAsStringAsync());
        var result = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        // Clear the independent display flag so the posted activity is the only reason the item is not suggested.
        var unmarked = await client.PatchAsJsonAsync($"/api/dossiers/{s.Dossier}/sheet-items/{s.Item}/display-completion",
            new MarkSheetItemRequest(result.Dossier.Version, false, Guid.NewGuid()));
        Assert.True(unmarked.IsSuccessStatusCode, await unmarked.Content.ReadAsStringAsync());
        var current = (await unmarked.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Empty(await Candidates(client, s));
        var activity = result.Activity;
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, $"/api/dossiers/{s.Dossier}/sheet-activities/{activity.Id}") {
            Content = JsonContent.Create(new SheetActivityActionRequest(current.Version, activity.Version, activity.SheetVersion, Guid.NewGuid()))
        };
        var deleted = await client.SendAsync(deletion);
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        Assert.Equal(s.Item, Assert.Single(await Candidates(client, s)).ItemId);
        Assert.Equal(HttpStatusCode.Created, (await Book(client, s, s.Dossier)).StatusCode);
    }

    [Fact]
    public async Task SuccessfulRequestReplayRemainsIdempotentAfterTheItemIsCompleted()
    {
        var s = await Prepare(); using var client = await Login();
        var booked = await Book(client, s, s.Dossier); Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var original = (await booked.Content.ReadFromJsonAsync<AppointmentResult>())!;
        var marked = await client.PatchAsJsonAsync($"/api/dossiers/{s.Dossier}/sheet-items/{s.Item}/display-completion", new MarkSheetItemRequest(1, true, Guid.NewGuid()));
        Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync());
        var replay = await client.PostAsJsonAsync("/api/appointments", lastCommand);
        Assert.True(replay.IsSuccessStatusCode, await replay.Content.ReadAsStringAsync());
        Assert.Equal(original.AppointmentId, (await replay.Content.ReadFromJsonAsync<AppointmentResult>())!.AppointmentId);
        var changedConsent = await client.PostAsJsonAsync("/api/appointments", lastCommand! with { PatientConsentConfirmed = false });
        Assert.Equal(HttpStatusCode.Conflict, changedConsent.StatusCode);
        Assert.Equal("MSG41", (await changedConsent.Content.ReadFromJsonAsync<DCMS.Application.Common.SystemMessage>())!.Code);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = Assert.Single(await db.Appointments.AsNoTracking().Where(x => x.PatientId == s.Patient).ToListAsync());
        Assert.Equal("Confirmed", saved.Status);
    }

    [Fact]
    public async Task FollowUpAppointmentCanBeRescheduled()
    {
        var s = await Prepare(); using var client = await Login();
        var booked = await Book(client, s, s.Dossier); Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var original = (await booked.Content.ReadFromJsonAsync<AppointmentResult>())!;
        var available = (await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.Branch}&dentistId={s.Dentist}&serviceId={s.Service}&date={s.Date:yyyy-MM-dd}&durationMinutes=30"))!;
        var slot = available.Slots.First(x => x.Start != original.StartAt);
        var command = new RescheduleAppointmentRequest(original.Version, Guid.NewGuid(), slot.Start, slot.ChairId,
            slot.ScheduleVersion, slot.AssistantScheduleVersion, "SYNTHETIC valid reschedule", RequesterVerification: "SYNTHETIC verified requester", DentistId: s.Dentist, BookingSettingsVersion: slot.BookingSettingsVersion);
        using var scheduler = await Login("assistant.demo");
        var response = await scheduler.PostAsJsonAsync($"/api/appointments/{original.Code}/reschedule", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var saved = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == original.AppointmentId);
        Assert.Equal(slot.Start, saved.StartAt); Assert.Equal(s.Dossier, saved.DossierId);
        var report = (await client.GetFromJsonAsync<ScheduleAppointments>($"/api/working-schedules/appointments?dentistId={s.Dentist}&from={s.Date:yyyy-MM-dd}&to={s.Date:yyyy-MM-dd}"))!;
        var row = Assert.Single(report.Items, item => item.Id == original.AppointmentId);
        Assert.Equal(slot.Start, row.StartAt); Assert.True(row.IsTreatmentPlan);
    }
}
