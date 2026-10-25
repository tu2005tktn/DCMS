using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.SupportRequests;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ReceptionFixture : ApiFixture
{
    public ChangeClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(Clock); });
    }
}

public sealed class ReceptionApiTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    private sealed record Setup(int BranchId, int ServiceId, int[] Dentists, int[] Chairs, int AssistantId, string DentistLogin, DateOnly Date);
    private async Task<HttpClient> Login(string name = "assistant.demo")
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private async Task<Setup> Prepare()
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var branch = new SchedulingBranch { Name = "SYNTHETIC reception " + Guid.NewGuid() }; db.Add(branch); await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var employees = new List<int>(); var login = "";
        for (var i = 0; i < 4; i++)
        {
            var user = new ApplicationUser { UserName = "rx-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC clinician " + i, AccountType = "Staff", IsActive = true };
            Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, i < 3 ? AccountRoles.Dentist : AccountRoles.ClinicAssistant)).Succeeded);
            if (i == 0) login = user.UserName;
            var employee = new EmployeeProfile { AccountId = user.Id, EmployeeCode = "RX-" + Guid.NewGuid().ToString("N"), FullName = user.FullName };
            db.Add(employee); await db.SaveChangesAsync(); employees.Add(employee.Id);
            db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = employee.Id, CanDentist = i < 3 });
            var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = employee.Id, WeekStart = monday, PublishedBy = user.Id, PublishedAt = fixture.Clock.Now };
            for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 0, EndMinute = 1440 });
            db.Add(week);
        }
        var receptionActors = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "assistant.demo" || u.UserName == "dual.demo" || u.UserName == "dentist.demo" || u.UserName == "owner.demo"
            select new { e.Id, u.UserName }).ToArrayAsync();
        foreach (var e in receptionActors) db.Add(new SchedulingMember { BranchId = branch.Id, EmployeeId = e.Id, CanDentist = e.UserName != "assistant.demo" });
        var service = new SchedulingService { BranchId = branch.Id, Name = "Khám", IsInitialExamination = true };
        var chairs = Enumerable.Range(1, 3).Select(i => new SchedulingChair { BranchId = branch.Id, Name = "Physical chair " + i, OpenMinute = 0, CloseMinute = 1440 }).ToArray();
        db.Add(service); db.AddRange(chairs); await db.SaveChangesAsync();
        return new(branch.Id, service.Id, employees.Take(3).ToArray(), chairs.Select(x => x.Id).ToArray(), employees[3], login, date);
    }
    private async Task<PatientDetail> Patient(HttpClient client, string? phone = null)
    {
        var response = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest(null, "SYNTHETIC attendee " + Guid.NewGuid(), null, null, phone, null, null, null, null, null, true));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
    }
    private async Task<CreateAppointmentRequest> BookingRequest(HttpClient client, Setup s, int dentist = 0, int chair = 0, int? assistantId = null)
    {
        var result = await client.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.Dentists[dentist]}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=30" + (assistantId.HasValue ? $"&assistantId={assistantId}" : ""));
        var slot = result!.Slots.First(x => x.Start >= fixture.Clock.Now.AddMinutes(65));
        return new(s.BranchId, new("SYNTHETIC booker", "0912345678", null), new(null, "SYNTHETIC expected attendee", null), "Relative", "WalkIn", "InPerson", s.ServiceId, null,
            s.Dentists[dentist], assistantId, s.Chairs[chair], slot.Start, 30, null, null, Guid.NewGuid(), slot.ScheduleVersion, slot.AssistantScheduleVersion, slot.BookingSettingsVersion);
    }
    private async Task<AppointmentResult> Book(HttpClient client, Setup s, int dentist = 0, int chair = 0)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", await BookingRequest(client, s, dentist, chair));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AppointmentResult>())!;
    }
    private static CheckInRequest Command(Setup s, AppointmentResult a, PatientDetail p) => new(a.AppointmentId.ToString(), s.BranchId, p.Id, true,
        "SYNTHETIC verified directly; corrected expected attendee after comparison", "InitialExamination", new(), null, null, null, null, null, a.Version, Guid.NewGuid());
    private static async Task<VisitResult> CheckIn(HttpClient client, CheckInRequest command)
    {
        var response = await client.PostAsJsonAsync("/api/reception/check-ins", command);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<VisitResult>())!;
    }
    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Fact]
    public async Task ValidReceptionPersistsCorrectPatientQueueAndResourcesWithoutAppLinkOrPayment()
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s);
        var before = (await c.GetFromJsonAsync<ReceptionDetail>($"/api/reception/appointments/{a.Code}"))!;
        Assert.NotEqual(before.Appointment.BookerName, before.Appointment.AttendeeName); Assert.Null(before.Visit);
        var v = await CheckIn(c, Command(s, a, p));
        Assert.Equal("CheckedIn", v.AppointmentStatus); Assert.Equal("Queued", v.VisitStatus); Assert.Equal(2, v.AppointmentVersion);
        Assert.Equal(p.Id, v.PatientId); Assert.Equal(fixture.Clock.Now, v.ActualArrivalAt); Assert.False(v.SourceReviewPending);
        var view = (await c.GetFromJsonAsync<ReceptionView>($"/api/reception?branchId={s.BranchId}&date={s.Date:yyyy-MM-dd}&keyword={p.PatientCode}"))!;
        Assert.Empty(view.NotArrived); Assert.Equal(v.VisitId, Assert.Single(view.Queue).VisitId); Assert.Equal("Asia/Ho_Chi_Minh", view.TimeZone);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.PatientAccountLinks.AnyAsync(x => x.PatientId == p.Id));
        var ap = await db.Appointments.SingleAsync(x => x.Id == a.AppointmentId); Assert.Equal(a.StartAt, ap.StartAt);
        var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.Id == ap.ReservationId);
        Assert.Equal("Checked-in", reservation.Status); Assert.Equal($"patient:{p.Id}", reservation.AttendeeKey);
        var occupied = await c.GetFromJsonAsync<AvailabilityResult>($"/api/availability?branchId={s.BranchId}&dentistId={s.Dentists[0]}&serviceId={s.ServiceId}&date={s.Date:yyyy-MM-dd}&durationMinutes=30");
        Assert.DoesNotContain(occupied!.Slots, x => x.Start == a.StartAt);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ConcurrentStaffOrSameKeyRetryCreatesExactlyOneVisitAndReopensIt(bool differentKey, bool differentActor)
    {
        var s = await Prepare(); using var c = await Login(); using var other = await Login(differentActor ? "dual.demo" : "assistant.demo");
        var p = await Patient(c); var a = await Book(c, s); var cmd = Command(s, a, p);
        var second = differentKey ? cmd with { RequestKey = Guid.NewGuid() } : cmd;
        var results = await Task.WhenAll(CheckIn(c, cmd), CheckIn(other, second));
        Assert.Equal(results[0].VisitId, results[1].VisitId);
        Assert.Equal(results[0].VisitId, (await CheckIn(c, cmd)).VisitId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Set<Visit>().CountAsync(x => x.AppointmentId == a.AppointmentId));
        Assert.Equal(1, await db.Set<VisitAudit>().CountAsync(x => x.VisitId == results[0].VisitId && x.Action == "CheckIn"));
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", cmd with { Note = "different payload" }), HttpStatusCode.Conflict, "MSG111");
    }

    [Theory]
    [InlineData("Cancelled", "MSG42")] [InlineData("NoShow", "MSG47")] [InlineData("tomorrow", "MSG42")]
    [InlineData("yesterday", "MSG42")] [InlineData("version", "MSG111")] [InlineData("archived", "MSG202")]
    [InlineData("patient", "MSG43")]
    public async Task InvalidReceptionRollsBackEverything(string scenario, string message)
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var other = await Patient(c); var a = await Book(c, s);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var ap = await db.Appointments.SingleAsync(x => x.Id == a.AppointmentId);
            if (scenario is "Cancelled" or "NoShow") ap.Status = scenario;
            if (scenario is "tomorrow" or "yesterday") { ap.StartAt = ap.StartAt.AddDays(scenario == "tomorrow" ? 1 : -1); ap.EndAt = ap.EndAt.AddDays(scenario == "tomorrow" ? 1 : -1); }
            if (scenario == "version") ap.Version++;
            if (scenario == "archived") (await db.Patients.SingleAsync(x => x.Id == p.Id)).IsArchived = true;
            if (scenario == "patient") ap.PatientId = other.Id;
            await db.SaveChangesAsync();
        }
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", Command(s, a, p)), HttpStatusCode.Conflict, message);
        using var verify = fixture.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await context.Set<Visit>().AnyAsync(x => x.AppointmentId == a.AppointmentId));
        Assert.Equal("Confirmed", (await context.Set<ResourceReservation>().SingleAsync(x => x.Id == a.AppointmentId)).Status);
        Assert.False(await context.Set<ReceptionCommandReceipt>().AnyAsync(x => x.RequestKey == Guid.Empty));
    }

    [Theory]
    [InlineData("admin.demo")] [InlineData("guest.demo")] [InlineData("dentist.demo")] [InlineData("owner.demo")]
    public async Task OnlyAssistantCanCheckIn(string user)
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s); using var other = await Login(user);
        await Message(await other.PostAsJsonAsync("/api/reception/check-ins", Command(s, a, p)), HttpStatusCode.Forbidden, "MSG06");
    }

    [Fact]
    public async Task BranchAndAssignedDentistScopeAreRecheckedAndAnonymousIsRejected()
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s); var v = await CheckIn(c, Command(s, a, p));
        using var assigned = await Login(s.DentistLogin); using var unassigned = await Login("dentist.demo"); using var anonymous = fixture.CreateClient();
        Assert.Equal(v.VisitId, (await assigned.GetFromJsonAsync<VisitResult>($"/api/reception/visits/{v.VisitId}"))!.VisitId);
        var mine = await unassigned.GetFromJsonAsync<ReceptionView>($"/api/reception?branchId={s.BranchId}&date={s.Date:yyyy-MM-dd}"); Assert.Empty(mine!.Queue);
        await Message(await unassigned.GetAsync($"/api/reception/visits/{v.VisitId}"), HttpStatusCode.Forbidden, "MSG06");
        await Message(await anonymous.GetAsync("/api/reception/options"), HttpStatusCode.Unauthorized, "MSG07");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var id = await db.Employees.Where(e => db.Users.Any(u => u.Id == e.AccountId && u.UserName == "assistant.demo")).Select(x => x.Id).SingleAsync();
            (await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == s.BranchId && x.EmployeeId == id)).Active = false; await db.SaveChangesAsync();
        }
        await Message(await c.GetAsync($"/api/reception/visits/{v.VisitId}"), HttpStatusCode.Forbidden, "MSG06");
    }

    [Theory]
    [InlineData("InitialExamination")] [InlineData("TreatmentContinuation")]
    [InlineData("PostoperativeOrWarrantyReview")] [InlineData("PeriodicOrNewProblem")]
    public async Task AllPurposesPreserveClinicalHoldsAndMissingSourceIsExplicit(string purpose)
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s);
        var context = new VisitSource();
        if (purpose is "TreatmentContinuation" or "PostoperativeOrWarrantyReview")
        {
            var dossier = Guid.NewGuid(); var item = Guid.NewGuid(); var step = Guid.NewGuid(); var activity = Guid.NewGuid();
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.AddRange(new ClinicalSourceReference { Id = dossier, PatientId = p.Id, BranchId = s.BranchId, Kind = "Dossier", Label = "Existing course" },
                new ClinicalSourceReference { Id = item, ParentId = dossier, PatientId = p.Id, BranchId = s.BranchId, Kind = "Item", Label = "Existing item", Blocker = "Clinical hold / lab rework" },
                new ClinicalSourceReference { Id = step, ParentId = item, PatientId = p.Id, BranchId = s.BranchId, Kind = "Step", Label = "Existing step" },
                new ClinicalSourceReference { Id = activity, ParentId = step, PatientId = p.Id, BranchId = s.BranchId, Kind = "Activity", Label = "Prior activity" });
            await db.SaveChangesAsync(); context = new(dossier, item, step, purpose == "PostoperativeOrWarrantyReview" ? activity : null);
        }
        var v = await CheckIn(c, Command(s, a, p) with { Purpose = purpose, Source = context, ReviewReason = purpose == "PostoperativeOrWarrantyReview" ? "Warranty" : null });
        Assert.Equal(purpose, v.Purpose); Assert.Equal(context, v.Source); Assert.False(v.SourceReviewPending);
        if (context.ItemId.HasValue)
        {
            Assert.Contains("Clinical hold / lab rework", v.Blockers);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.Set<ClinicalSourceReference>().SingleAsync(x => x.Id == context.ItemId)).Blocker = "Updated hold"; await db.SaveChangesAsync();
            Assert.Contains("Updated hold", (await c.GetFromJsonAsync<VisitResult>($"/api/reception/visits/{v.VisitId}"))!.Blockers);
        }
        var next = await Book(c, s, 1, 1); var pending = await CheckIn(c, Command(s, next, await Patient(c)) with { Purpose = "TreatmentContinuation" });
        Assert.True(pending.SourceReviewPending); Assert.NotEmpty(pending.Blockers);
    }

    [Fact]
    public async Task SourceBelongingToAnotherPatientOrBranchAndInconsistentHierarchyAreRejected()
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var other = await Patient(c); var a = await Book(c, s);
        var foreign = Guid.NewGuid(); var foreignBranch = Guid.NewGuid(); var d1 = Guid.NewGuid(); var d2 = Guid.NewGuid(); var item = Guid.NewGuid();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var otherBranch = new SchedulingBranch { Name = "SYNTHETIC source in other branch" }; db.Add(otherBranch); await db.SaveChangesAsync();
            db.AddRange(new ClinicalSourceReference { Id = foreign, Kind = "Dossier", PatientId = other.Id, BranchId = s.BranchId },
                new ClinicalSourceReference { Id = foreignBranch, Kind = "Dossier", PatientId = p.Id, BranchId = otherBranch.Id },
                new ClinicalSourceReference { Id = d1, Kind = "Dossier", PatientId = p.Id, BranchId = s.BranchId },
                new ClinicalSourceReference { Id = d2, Kind = "Dossier", PatientId = p.Id, BranchId = s.BranchId },
                new ClinicalSourceReference { Id = item, Kind = "Item", PatientId = p.Id, BranchId = s.BranchId, ParentId = d2 }); await db.SaveChangesAsync();
        }
        foreach (var source in new[] { new VisitSource(foreign), new VisitSource(foreignBranch), new VisitSource(Guid.NewGuid()), new VisitSource(d1, item) })
            await Message(await c.PostAsJsonAsync("/api/reception/check-ins", Command(s, a, p) with { Purpose = "TreatmentContinuation", Source = source }), HttpStatusCode.BadRequest, "MSG237");
        var v = await CheckIn(c, Command(s, a, p) with { Purpose = "PostoperativeOrWarrantyReview" });
        Assert.True(v.SourceReviewPending);
    }

    [Fact]
    public async Task SharedPhoneNeverChoosesPatientAndUnknownFieldsRemainNull()
    {
        var s = await Prepare(); using var c = await Login(); var p1 = await Patient(c, "0999888777"); var p2 = await Patient(c, "0999888777"); var a = await Book(c, s);
        Assert.NotEqual(p1.Id, p2.Id); Assert.Null(p2.BirthDate); Assert.Null(p2.Gender);
        var v = await CheckIn(c, Command(s, a, p2)); Assert.Equal(p2.Id, v.PatientId);
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", Command(s, a, p1)), HttpStatusCode.Conflict, "MSG43");
    }

    [Fact]
    public async Task RetrospectiveArrivalRequiresReasonAndKeepsActualEarlierThanSlot()
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s);
        var midnight = new DateTimeOffset(s.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).ToUniversalTime();
        var actual = fixture.Clock.Now.AddMinutes(-1) < midnight ? midnight : fixture.Clock.Now.AddMinutes(-1);
        var cmd = Command(s, a, p) with { ActualArrivalAt = actual };
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", cmd), HttpStatusCode.BadRequest, "MSG02");
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", cmd with { ActualArrivalAt = fixture.Clock.Now.AddMinutes(1) }), HttpStatusCode.BadRequest, "MSG04");
        var v = await CheckIn(c, cmd with { ArrivalReason = "Walk-in arrived before booking" });
        Assert.Equal(actual, v.ActualArrivalAt); Assert.Equal(fixture.Clock.Now, v.EnteredAt); Assert.True(v.ActualArrivalAt < v.AppointmentAt);
    }

    [Fact]
    public async Task ReorderAndDentistPriorityHaveAuditVersionAndIdempotencyWithoutMovingAppointment()
    {
        var s = await Prepare(); using var c = await Login(); var p1 = await Patient(c); var p2 = await Patient(c); var a1 = await Book(c, s); var a2 = await Book(c, s, 1, 1);
        var v1 = await CheckIn(c, Command(s, a1, p1)); var v2 = await CheckIn(c, Command(s, a2, p2));
        await Message(await c.PostAsJsonAsync($"/api/reception/visits/{v2.VisitId}/reorder", new ReorderVisitRequest(v1.VisitId, " ", v2.Version, Guid.NewGuid())), HttpStatusCode.BadRequest, "MSG02");
        var cmd = new ReorderVisitRequest(v1.VisitId, "SYNTHETIC operational reason", v2.Version, Guid.NewGuid());
        var response = await c.PostAsJsonAsync($"/api/reception/visits/{v2.VisitId}/reorder", cmd); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reordered = (await response.Content.ReadFromJsonAsync<VisitResult>())!; Assert.Equal(1, reordered.Queue.Position);
        Assert.Equal(reordered.Version, (await (await c.PostAsJsonAsync($"/api/reception/visits/{v2.VisitId}/reorder", cmd)).Content.ReadFromJsonAsync<VisitResult>())!.Version);
        await Message(await c.PostAsJsonAsync($"/api/reception/visits/{v2.VisitId}/reorder", cmd with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG111");
        await Message(await c.PostAsJsonAsync($"/api/reception/visits/{v1.VisitId}/priority", new VisitPriorityRequest("Urgent", "pain", 1, Guid.NewGuid())), HttpStatusCode.Forbidden, "MSG06");
        using var dentist = await Login(s.DentistLogin); var current = (await dentist.GetFromJsonAsync<VisitResult>($"/api/reception/visits/{v1.VisitId}"))!;
        var priority = await dentist.PostAsJsonAsync($"/api/reception/visits/{v1.VisitId}/priority", new VisitPriorityRequest("Urgent", "SYNTHETIC dentist assessment", current.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, priority.StatusCode); var decided = (await priority.Content.ReadFromJsonAsync<VisitResult>())!; Assert.Equal(s.Dentists[0], decided.PriorityDentistId);
        var history = (await c.GetFromJsonAsync<VisitHistory[]>($"/api/reception/visits/{v1.VisitId}/history"))!;
        Assert.Contains(history, h => h.Action == "ClinicalPriority" && h.DecidingDentistId == s.Dentists[0]);
        Assert.Contains((await c.GetFromJsonAsync<VisitHistory[]>($"/api/reception/visits/{v2.VisitId}/history"))!, h => h.Action == "Reorder" && h.Reason == cmd.Reason);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(a1.StartAt, (await db.Appointments.SingleAsync(x => x.Id == a1.AppointmentId)).StartAt);
        Assert.Equal(a2.StartAt, (await db.Appointments.SingleAsync(x => x.Id == a2.AppointmentId)).StartAt);
    }

    [Fact]
    public async Task ThreePhysicalChairsRequireIndependentStaffAndRespectAssignedAssistantConflict()
    {
        var s = await Prepare(); using var c = await Login(); Assert.Equal(3, s.Chairs.Distinct().Count());
        var one = await BookingRequest(c, s, assistantId: s.AssistantId);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/appointments", one)).StatusCode);
        await Message(await c.PostAsJsonAsync("/api/appointments", one with { RequestKey = Guid.NewGuid(), DentistId = s.Dentists[1], AssistantId = null, AssistantScheduleVersion = null }), HttpStatusCode.Conflict, "MSG28");
        await Message(await c.PostAsJsonAsync("/api/appointments", one with { RequestKey = Guid.NewGuid(), ChairId = s.Chairs[1], AssistantId = null, AssistantScheduleVersion = null }), HttpStatusCode.Conflict, "MSG27");
        await Message(await c.PostAsJsonAsync("/api/appointments", one with { RequestKey = Guid.NewGuid(), DentistId = s.Dentists[1], ChairId = s.Chairs[1] }), HttpStatusCode.Conflict, "MSG27");
        var independent = one with { RequestKey = Guid.NewGuid(), DentistId = s.Dentists[1], ChairId = s.Chairs[1], AssistantId = null, AssistantScheduleVersion = null };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/appointments", independent)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/appointments", independent with { RequestKey = Guid.NewGuid(), DentistId = s.Dentists[2], ChairId = s.Chairs[2] })).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(3, await db.Appointments.CountAsync(x => x.BranchId == s.BranchId)); Assert.False(await db.Set<Visit>().AnyAsync(x => x.BranchId == s.BranchId));
    }

    [Fact]
    public async Task QueryLimitsAndIdentityAndConcernValidationAreEnforced()
    {
        var s = await Prepare(); using var c = await Login(); var p = await Patient(c); var a = await Book(c, s); var cmd = Command(s, a, p);
        await Message(await c.GetAsync($"/api/reception?branchId={s.BranchId}&date={s.Date:yyyy-MM-dd}&keyword={new string('x', 101)}"), HttpStatusCode.BadRequest, "MSG04");
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", cmd with { IdentityVerified = false }), HttpStatusCode.BadRequest, "MSG04");
        await Message(await c.PostAsJsonAsync("/api/reception/check-ins", cmd with { AdditionalConcern = new string('x', 1001) }), HttpStatusCode.BadRequest, "MSG04");
        var v = await CheckIn(c, cmd with { AdditionalConcern = new string('x', 1000) }); Assert.Equal(1000, v.AdditionalConcern!.Length);
    }

    [Fact]
    public async Task ConfirmingAttendanceDoesNotCreateVisitOrQueue()
    {
        var s = await Prepare(); using var c = await Login(); var a = await Book(c, s);
        var code = "SR-" + Guid.NewGuid().ToString("N")[..24];
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.Add(new AppointmentSupportRequest { AppointmentId = a.AppointmentId, Code = code, Kind = "ConfirmToAttend", RequestKey = Guid.NewGuid(),
                RequesterName = "SYNTHETIC booker", CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now }); await db.SaveChangesAsync();
        }
        var response = await c.PostAsJsonAsync($"/api/support-requests/{code}/resolve", new ResolveSupportCommand(Guid.NewGuid(), 1, "Resolve", "SYNTHETIC intent confirmed", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = (await c.GetFromJsonAsync<ReceptionView>($"/api/reception?branchId={s.BranchId}&date={s.Date:yyyy-MM-dd}"))!;
        Assert.Empty(view.Queue); Assert.Equal(a.AppointmentId, Assert.Single(view.NotArrived).AppointmentId);
        using var verify = fixture.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var appointment = await context.Appointments.SingleAsync(x => x.Id == a.AppointmentId);
        Assert.Equal("Confirmed", appointment.Status); Assert.Equal("Yes", appointment.ConfirmedToAttend);
        Assert.False(await context.Set<Visit>().AnyAsync(x => x.AppointmentId == a.AppointmentId));
    }
}
