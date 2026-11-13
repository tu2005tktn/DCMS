using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Finance;
using DCMS.Application.Features.Finance;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentSheetV25Tests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record Setup(int Patient, int Branch, int Dentist, int Service, Guid[] Visits);
    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private async Task<Setup> Prepare(bool createVisits = true)
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var staff = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select e).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC v25 " + Guid.NewGuid().ToString("N") };
        var patient = new PatientProfile { PatientCode = "V25-" + Guid.NewGuid().ToString("N"), FullName = "SYNTHETIC v25", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.AddRange(branch, patient); await db.SaveChangesAsync();
        var service = new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC Trám", Unit = "răng", ReferenceUnitPriceVnd = 350000, PublicBookable = false };
        var chair = new SchedulingChair { BranchId = branch.Id, Name = "SYNTHETIC chair" };
        await db.AddSchedulingDataAsync(service, chair, new SchedulingMember { BranchId = branch.Id, EmployeeId = staff.Id, CanDentist = true });
        await db.SaveChangesAsync();
        var visits = new List<Guid>();
        for (var i = 0; i < (createVisits ? 2 : 0); i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(-i).AddMinutes(-30);
            var id = Guid.NewGuid();
            await db.AddSchedulingDataAsync(new ResourceReservation { Id = id, Code = "RV-" + id.ToString("N"), BranchId = branch.Id, DentistId = staff.Id,
                ChairId = chair.Id, ServiceId = service.Id, Start = start, End = start.AddHours(1), AttendeeKey = $"patient:{patient.Id}", AttendeeLabel = patient.FullName });
            var appointment = new Appointment { Id = id, ReservationId = id, Code = "AP-" + id.ToString("N"), BranchId = branch.Id,
                PatientId = patient.Id, DentistId = staff.Id, ChairId = chair.Id, ServiceId = service.Id, BookerName = "SYNTHETIC", BookerPhone = "0900000000",
                AttendeeName = patient.FullName, StartAt = start, EndAt = start.AddHours(1), CreatedAt = start };
            db.Add(appointment);
            var visit = new Visit { AppointmentId = id, PatientId = patient.Id, BranchId = branch.Id, DentistId = staff.Id,
                Status = "InProgress", Purpose = AttendancePurposes.TreatmentContinuation, ArrivalAt = start, RecordedAt = start, RecordedBy = staff.AccountId };
            db.Add(visit); visits.Add(visit.Id);
        }
        await db.SaveChangesAsync(); return new(patient.Id, branch.Id, staff.Id, service.Id, visits.ToArray());
    }
    private async Task<TreatmentDossierDto> Create(HttpClient client, Setup s)
    {
        var response = await client.PostAsJsonAsync($"/api/patients/{s.Patient}/dossiers", new CreateDossierRequest(s.Branch, s.Dentist, "SYNTHETIC", null, null, Guid.NewGuid()));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
    }
    private async Task<SheetDraftDto> Draft(HttpClient c, Setup s, Guid dossier, string kind, Guid? source = null, decimal qty = 1, long unitPrice = 300000, decimal discount = 0)
    {
        var id = Guid.NewGuid();
        var row = JsonSerializer.SerializeToElement(new { serviceId = s.Service, dentistId = s.Dentist, regions = new[] { "25", "26" },
            quantity = qty, unitPriceVnd = unitPrice, costVnd = (long)(qty * unitPrice), note = "SYNTHETIC ghi chú", sourceItemId = source,
            discountType = "VND", discountValue = discount, discountNote = discount > 0 ? "SYNTHETIC case reduction" : null });
        var response = await c.PutAsJsonAsync($"/api/dossiers/{dossier}/sheet-drafts/{id}", new SaveSheetDraftRequest(kind, null, row, 0, Guid.NewGuid()));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SheetDraftDto>())!;
    }
    private static SheetDecision Agreed => new("Agreed", "SYNTHETIC tư vấn và giá", "SYNTHETIC người khám");

    [Fact]
    public async Task QuickCreateIsBlankAuthorizedAndConcurrentRetrySafe()
    {
        using var c = await Login(); var s = await Prepare(false);
        var context = (await c.GetFromJsonAsync<DossierCreateContext>($"/api/patients/{s.Patient}/dossiers/create-context"))!;
        Assert.Equal(s.Dentist, context.DentistId); Assert.Contains(context.Branches,x=>x.Id==s.Branch);
        var url=$"/api/patients/{s.Patient}/dossiers/quick"; var command=new QuickDossierRequest(s.Branch,Guid.NewGuid());
        var responses=await Task.WhenAll(c.PostAsJsonAsync(url,command),c.PostAsJsonAsync(url,command));
        foreach(var response in responses) Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        var a=(await responses[0].Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;var b=(await responses[1].Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(a.Id,b.Id);Assert.Equal("",a.Diagnosis);Assert.Empty(a.Items);Assert.Equal(s.Dentist,a.DentistId);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(url,command with{SourceVisitId=Guid.NewGuid()})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(url,command with{BranchId=int.MaxValue,RequestKey=Guid.NewGuid()})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync($"/api/patients/{s.Patient}/dossiers",new CreateDossierRequest(s.Branch,s.Dentist,null,null,null,Guid.NewGuid()))).StatusCode);
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1,await db.TreatmentDossiers.CountAsync(x=>x.PatientId==s.Patient));
        Assert.False(await db.Appointments.AnyAsync(x=>x.PatientId==s.Patient));Assert.False(await db.ClinicalActivities.AnyAsync(x=>x.PatientId==s.Patient));
        var patient=await db.Patients.FindAsync(s.Patient);patient!.IsArchived=true;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync(url,command)).StatusCode);
    }

    [Fact]
    public async Task ImportReusablePlanIsAtomicSnapshotAndIdempotent()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        const string root = "/api/treatment-templates/item-plans";
        var response = await c.PostAsJsonAsync(root, new ItemPlanCreate(s.Branch, "SYNTHETIC reusable", Guid.NewGuid())); response.EnsureSuccessStatusCode();
        var plan = (await response.Content.ReadFromJsonAsync<ItemPlanDto>())!;
        for (var i = 0; i < 2; i++) {
            response = await c.PostAsJsonAsync($"{root}/{plan.Id}/items", new ItemPlanRowInput(null, ["25", "26"], s.Service, 25, null, null, null, 600000, plan.Version, Guid.NewGuid()));
            response.EnsureSuccessStatusCode(); plan = (await response.Content.ReadFromJsonAsync<ItemPlanDto>())!;
        }
        var command = new ImportItemPlanRequest(plan.Id, plan.Version, dossier.Version, Guid.NewGuid());
        var url = $"/api/dossiers/{dossier.Id}/sheet-items/from-item-plan";
        var results = await Task.WhenAll(c.PostAsJsonAsync(url, command), c.PostAsJsonAsync(url, command));
        foreach (var result in results) Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        var imported = (await results[0].Content.ReadFromJsonAsync<ImportItemPlanResult>())!;
        Assert.Equal(2, imported.CreatedItemIds.Length); Assert.Equal(dossier.Version + 1, imported.Dossier.Version); Assert.Equal(dossier.PlanVersion + 1, imported.Dossier.PlanVersion);
        Assert.All(imported.Dossier.Items, x => { Assert.Equal(1, x.Quantity); Assert.Equal(600000, x.AgreedUnitPriceVnd); Assert.Equal(s.Dentist, x.ResponsibleDentistId); Assert.Equal("ClinicianEntry", x.EntrySource); Assert.Equal("Proposed", x.Decision); });
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(url, command with { ExpectedVersion = imported.Dossier.Version })).StatusCode);
        // A stale source and an invalid second row must leave all existing patient items unchanged.
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(url, command with { ExpectedItemPlanVersion = plan.Version - 1, ExpectedVersion = imported.Dossier.Version, RequestKey = Guid.NewGuid() })).StatusCode);
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var row = await db.LoadTemplateItemAsync(plan.Items[1].Id); row!.TeethJson = "[\"99\"]"; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(url, command with { ExpectedVersion = imported.Dossier.Version, RequestKey = Guid.NewGuid() })).StatusCode);
        var after = (await c.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!; Assert.Equal(2, after.Items.Count); Assert.Equal(imported.Dossier.Version, after.Version);
        using var check = fixture.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await context.ClinicalActivities.AnyAsync(x => x.DossierId == dossier.Id));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task ConfirmPlanHasStableIdsAndRejectsCompetingVersionWithoutCreatingActualOrMoney()
    {
        using var c = await Login(); var s = await Prepare(); var dossier = await Create(c, s);
        var a = await Draft(c, s, dossier.Id, "Plan"); var b = await Draft(c, s, dossier.Id, "Plan");
        var request = new ConfirmSheetPlanRequest(dossier.Version, a.Version, Agreed, Guid.NewGuid(),
            new SheetEstimate(20, 25, 25, 2, null, null, null));
        var url = $"/api/dossiers/{dossier.Id}/sheet-drafts/{a.Id}/confirm-plan";
        var response = await c.PostAsJsonAsync(url, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var saved = (await response.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(a.Id, Assert.Single(saved.Items).Id);
        Assert.Equal(25, saved.Items[0].EstimatedMinutes); Assert.Null(saved.Items[0].ReservedMinutes);
        var replay = await c.PostAsJsonAsync(url, request);
        Assert.Equal(saved.PlanVersion, (await replay.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.PlanVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(url, request with { Decision = new("Deferred", "Hoãn", "SYNTHETIC") })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{b.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, b.Version, Agreed, Guid.NewGuid()))).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.ClinicalActivities.AnyAsync(x => x.DossierId == dossier.Id));
        Assert.DoesNotContain(await db.TreatmentProgressionEvents.Where(x => x.Kind == "clinical.fee.requested").Select(x => x.PayloadJson).ToListAsync(), json => json.Contains(dossier.Id.ToString()));
        var service = await db.Set<SchedulingService>().SingleAsync(x => x.Id == s.Service); service.ReferenceUnitPriceVnd = 999999; await db.SaveChangesAsync();
        var read = await c.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}");
        Assert.Equal(350000, read!.Items[0].ReferenceUnitPriceVnd);
        var step = new DCMS.Domain.Features.Treatment.TreatmentStep { DossierId = dossier.Id, ItemId = a.Id,
            PlanId = saved.PlanId, PlanVersion = saved.PlanVersion, Name = "SYNTHETIC unchanged", EstimatedMinutes = 25, ReservedMinutes = 30 };
        await db.AddTreatmentDataAsync(step); await db.SaveChangesAsync();
        var deferred = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{b.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(saved.Version, b.Version, new("Deferred", "SYNTHETIC hoãn", "SYNTHETIC"), Guid.NewGuid()));
        Assert.True(deferred.IsSuccessStatusCode, await deferred.Content.ReadAsStringAsync());
        var next = (await deferred.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        db.ChangeTracker.Clear(); step = await db.TreatmentSteps.SingleAsync(x => x.Id == step.Id); Assert.Equal(next.PlanVersion, step.PlanVersion); Assert.Equal(2, step.Version);
        var unagreed = await Draft(c, s, dossier.Id, "Actual", b.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{unagreed.Id}/confirm-actual",
            new ConfirmSheetActualRequest(next.Version, unagreed.Version, s.Visits[0], 1, "SYNTHETIC", "Separate", null, false, null, Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task TwoAttendancesPostOneFeeRequestPerActualAndClinicalClosureDoesNotSettleMoney()
    {
        using var c = await Login(); var s = await Prepare(); var dossier = await Create(c, s);
        var plan = await Draft(c, s, dossier.Id, "Plan", qty: 2, discount: 100000);
        var agreed = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{plan.Id}/confirm-plan", new ConfirmSheetPlanRequest(dossier.Version, 1, Agreed, Guid.NewGuid()));
        Assert.True(agreed.IsSuccessStatusCode, await agreed.Content.ReadAsStringAsync());
        dossier = (await agreed.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/close",
            new CloseDossierRequest(dossier.Version, "CompletedAsAgreed", null, [], Guid.NewGuid()))).StatusCode);
        for (var i = 0; i < 2; i++)
        {
            var row = await Draft(c, s, dossier.Id, "Actual", plan.Id);
            var request = new ConfirmSheetActualRequest(dossier.Version, row.Version, s.Visits[i], 1,
                "SYNTHETIC actual", "Separate", null, i == 1, null, Guid.NewGuid());
            var url = $"/api/dossiers/{dossier.Id}/sheet-drafts/{row.Id}/confirm-actual";
            var posted = await c.PostAsJsonAsync(url, request);
            Assert.True(posted.IsSuccessStatusCode, await posted.Content.ReadAsStringAsync());
            var result = (await posted.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
            Assert.Equal("Posted", result.Activity.FeeStatus); Assert.Equal(50000, result.Activity.DiscountSnapshotVnd);
            var retry = await c.PostAsJsonAsync(url, request);
            Assert.Equal(result.Activity.Id, (await retry.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.Id);
            dossier = result.Dossier;
        }
        var page = await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities");
        Assert.Equal(2, page!.Total); Assert.Equal(2, page.Items.Select(x => x.VisitId).Distinct().Count());
        var close = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/close", new CloseDossierRequest(dossier.Version, "CompletedAsAgreed", null, [], Guid.NewGuid()));
        Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync());
        Assert.True((await close.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.FinancialSummaryAvailable);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var ids = page.Items.Select(x => x.Id).ToArray();
        Assert.Equal(2, await db.Set<FinanceCharge>().CountAsync(x => x.ActivityId.HasValue && ids.Contains(x.ActivityId.Value)));
        Assert.All(await db.Visits.Where(x => s.Visits.Contains(x.Id)).ToListAsync(), v => Assert.Equal("InProgress", v.Status));
    }

    [Fact]
    public async Task DirectAgreementCreatesOneSourceAndWrongVisitRollsBackAndAssistantNeedsExplicitFinanceGrant()
    {
        using var c = await Login(); var s = await Prepare(); var other = await Prepare(); var dossier = await Create(c, s);
        var row = await Draft(c, s, dossier.Id, "Actual", unitPrice: 0);
        var request = new ConfirmSheetActualRequest(dossier.Version, row.Version, other.Visits[0], 1, "SYNTHETIC chăm sóc", "Free", "Đã thỏa thuận miễn phí", false, Agreed, Guid.NewGuid());
        var url = $"/api/dossiers/{dossier.Id}/sheet-drafts/{row.Id}/confirm-actual";
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(url, request)).StatusCode);
        var posted = await c.PostAsJsonAsync(url, request with { VisitId = s.Visits[0], RequestKey = Guid.NewGuid() });
        Assert.True(posted.IsSuccessStatusCode, await posted.Content.ReadAsStringAsync());
        var result = (await posted.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        Assert.Equal(row.Id, Assert.Single(result.Dossier.Items).Id); Assert.Equal("Free", result.Activity.FeeStatus);
        using var assistant = await Login("assistant.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.GetAsync($"/api/patients/{s.Patient}/dossiers/financial-context")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.GetAsync($"/api/dossiers/{dossier.Id}")).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await users.FindByNameAsync("assistant.demo"))!;
        await users.AddClaimAsync(user, new Claim("permission", "finance.read"));
        var employee = await db.Employees.SingleAsync(x => x.AccountId == user.Id);
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = s.Branch, EmployeeId = employee.Id }); await db.SaveChangesAsync();
        var projection = await assistant.GetAsync($"/api/patients/{s.Patient}/dossiers/financial-context");
        Assert.Equal(HttpStatusCode.OK, projection.StatusCode);
        var json = await projection.Content.ReadAsStringAsync(); Assert.DoesNotContain("counseling", json); Assert.DoesNotContain("diagnosis", json);
    }

    [Fact]
    public async Task ConcurrentDecisionsHaveOneWinnerAndHeldStepCannotPost()
    {
        using var c = await Login(); var s = await Prepare(); var dossier = await Create(c, s);
        var a = await Draft(c, s, dossier.Id, "Plan"); var b = await Draft(c, s, dossier.Id, "Plan");
        var results = await Task.WhenAll(new[] { a, b }.Select(row => c.PostAsJsonAsync(
            $"/api/dossiers/{dossier.Id}/sheet-drafts/{row.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, row.Version, Agreed, Guid.NewGuid()))));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        dossier = (await results.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = Assert.Single(dossier.Items);
        Guid stepId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var step = new DCMS.Domain.Features.Treatment.TreatmentStep { DossierId = dossier.Id, ItemId = item.Id,
                PlanId = dossier.PlanId, PlanVersion = dossier.PlanVersion, 
                Name = "SYNTHETIC held", EstimatedMinutes = 30, ReservedMinutes = 30, HoldReason = "SYNTHETIC hold" };
            await db.AddTreatmentDataAsync(step); await db.SaveChangesAsync(); stepId = step.Id;
        }
        var actual = await Draft(c, s, dossier.Id, "Actual", item.Id);
        var request = new ConfirmSheetActualRequest(dossier.Version, actual.Version, s.Visits[0], 1,
            "SYNTHETIC", "Separate", null, false, null, Guid.NewGuid(), stepId, 1);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(
            $"/api/dossiers/{dossier.Id}/sheet-drafts/{actual.Id}/confirm-actual", request)).StatusCode);
        var activities = await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities");
        Assert.Empty(activities!.Items);
    }

    [Fact]
    public async Task ExaminationHandoffKeepsTheExistingAuthoritativeVersionAndImmutableSourceSnapshot()
    {
        using var c = await Login(); var s = await Prepare();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var actorId = await db.Employees.Where(e => e.Id == s.Dentist).Select(e => e.AccountId).SingleAsync();
        var examination = new DCMS.Domain.Features.Examinations.VisitExamination { VisitId = s.Visits[0], Confirmed = true,
            UpdatedBy = actorId, UpdatedAt = DateTimeOffset.UtcNow,
            ContentJson = "{\"diagnosis\":\"SYNTHETIC original\"}" };
        db.Add(examination); await db.SaveChangesAsync();
        var response = await c.PostAsJsonAsync($"/api/patients/{s.Patient}/dossiers",
            new CreateDossierRequest(s.Branch, s.Dentist, "SYNTHETIC", null, null, Guid.NewGuid(), s.Visits[0], 1));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var dossier = (await response.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(s.Visits[0], dossier.SourceExaminationId); Assert.Equal(1, dossier.SourceExaminationVersion);
        examination.ContentJson = "{\"diagnosis\":\"SYNTHETIC changed\"}"; await db.SaveChangesAsync();
        var read = (await c.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!;
        Assert.Contains("SYNTHETIC original", Assert.Single(read.Timeline, e => e.Kind == "ExaminationSourceCaptured").PayloadJson);
        Assert.Equal(2, await db.Visits.CountAsync(v => s.Visits.Contains(v.Id)));
    }
    [Fact]
    public async Task DirectSheetTransferIsAtomicAndIdempotentWithoutVisitOrConsentAndKeepsSnapshots()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        var draft = await Draft(c, s, dossier.Id, "Plan", qty: 2, discount: 100000);
        var confirmed = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid(), new SheetEstimate(null, 25, null, null, null, null, null)));
        Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        dossier = (await confirmed.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = Assert.Single(dossier.Items);
        Assert.Equal("ClinicianEntry", item.EntrySource); Assert.Equal("Proposed", item.Decision); Assert.Null(item.Counseling); Assert.Null(item.DecidedAt);
        Assert.Equal(25, item.EstimatedMinutes);
        var preview = await c.GetFromJsonAsync<PlanPricePreviewDto>($"/api/dossiers/{dossier.Id}/plan/preview");
        Assert.Equal(500000, preview!.NetBeforeTaxVnd);
        var url = $"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/perform";
        var request = new SheetItemActionRequest(dossier.Version, Guid.NewGuid());
        var responses = await Task.WhenAll(c.PostAsJsonAsync(url, request), c.PostAsJsonAsync(url, request));
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.Content.ReadAsStringAsync().Result));
        var result = (await responses[0].Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        Assert.Equal(result.Activity.Id, (await responses[1].Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.Id);
        Assert.Null(result.Activity.VisitId); Assert.Equal("Dossier", result.Activity.SourceKind);
        Assert.Equal(item.ServiceName, result.Activity.ServiceNameSnapshot); Assert.Equal(100000, result.Activity.DiscountSnapshotVnd);
        Assert.Equal("Posted", result.Activity.FeeStatus); Assert.True(result.Dossier.Items[0].DisplayCompleted);
        Assert.Equal("NotStarted", result.Dossier.Items[0].Progress); Assert.Equal("Open", result.Dossier.State);
        dossier = result.Dossier;
        var again = await c.PostAsJsonAsync(url, new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        Assert.Equal(result.Activity.Id, (await again.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.Id);
        dossier = (await c.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!;
        using (var legacyScope = fixture.Services.CreateScope())
        {
            var legacyDb = legacyScope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await legacyDb.ClinicalActivities.SingleAsync(a => a.Id == result.Activity.Id)).ServiceNameSnapshot = null;
            (await legacyDb.Set<SchedulingService>().SingleAsync(service => service.Id == s.Service)).Name = "SYNTHETIC renamed catalog";
            await legacyDb.SaveChangesAsync();
        }
        var newRow = JsonSerializer.SerializeToElement(new { serviceId = s.Service, dentistId = s.Dentist, regions = new[] { "25", "26" }, quantity = 2, unitPriceVnd = 400000, costVnd = 800000,
            note = "SYNTHETIC sửa tương lai", estimatedMinutes = 45, discountType = "VND", discountValue = 0 });
        var edited = await c.PutAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}", new EditSheetItemRequest(dossier.Version, newRow, Guid.NewGuid()));
        Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        dossier = (await edited.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(2, dossier.Items[0].Quantity); Assert.Equal(45, dossier.Items[0].EstimatedMinutes);
        var history = await c.GetFromJsonAsync<IReadOnlyList<TreatmentHistoryItem>>($"/api/dossiers/{dossier.Id}/plan/history");
        Assert.Contains(history!, h => h.Version == dossier.PlanVersion && h.SnapshotJson.Contains("400000"));
        var read = await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities");
        var actual = Assert.Single(read!.Items); Assert.Equal(item.ServiceName, actual.ServiceNameSnapshot); Assert.Equal(300000, actual.PriceSnapshotVnd); Assert.Equal(100000, actual.DiscountSnapshotVnd);
        using var deletion = new HttpRequestMessage(HttpMethod.Delete, $"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}") { Content = JsonContent.Create(new SheetItemActionRequest(dossier.Version, Guid.NewGuid())) };
        var deleted = await c.SendAsync(deletion); Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        Assert.True((await deleted.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Items[0].SheetDeleted);
        Assert.Single((await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities"))!.Items);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.Visits.AnyAsync(v => v.PatientId == s.Patient));
        Assert.Equal(1, await db.Set<FinanceCharge>().CountAsync(e => e.ActivityId == actual.Id && e.DossierId == dossier.Id));
    }

    [Fact]
    public async Task WorkStatisticsListsPerformedSheetActivitiesWithCostPaidAndDue()
    {
        using var dentist = await Login(); using var cashier = await Login("owner.demo");
        var setup = await Prepare(false); var dossier = await Create(dentist, setup);
        var draft = await Draft(dentist, setup, dossier.Id, "Plan");
        var plan = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = Assert.Single(dossier.Items);
        var performed = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        Assert.True(performed.IsSuccessStatusCode, await performed.Content.ReadAsStringAsync());
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        // The sheet's own Trả tiền button records dossier payments (needs finance.payments.create, so seed one directly); the statistics must follow them.
        var context = (await dentist.GetFromJsonAsync<DossierPaymentContext>($"/api/dossiers/{dossier.Id}/payments/context"))!;
        var source = Assert.Single(context.Lines); var part = source.DueVnd / 2; Assert.True(part > 0);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var payment = new DCMS.Domain.Features.Treatment.DossierPayment { DossierId = dossier.Id, PatientId = dossier.PatientId, BranchId = dossier.BranchId,
                PaidOn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), Method = "Cash", CollectorEmployeeId = context.DefaultCollectorId ?? context.Collectors[0].Id, TotalVnd = part, CreatedBy = 1, CreatedAt = DateTimeOffset.UtcNow };
            payment.Lines = [new DCMS.Domain.Features.Treatment.DossierPaymentLine { PaymentId = payment.Id, ActivityId = activity.Id, AmountVnd = part }];
            db.Add(payment); await db.SaveChangesAsync();
        }

        var page = (await cashier.GetFromJsonAsync<WorkStatisticsPage>($"/api/work-statistics?q={Uri.EscapeDataString(item.ServiceName)}&pageSize=100"))!;
        var group = Assert.Single(page.Items, x => x.DossierId == dossier.Id);
        var row = Assert.Single(group.Lines); Assert.Equal(activity.Id, row.ActivityId); Assert.False(string.IsNullOrEmpty(row.ServiceName));
        Assert.Equal(dossier.Code, group.DossierCode); Assert.Equal(source.CostVnd - source.DiscountVnd, row.CostVnd); Assert.Equal(part, row.PaidVnd); Assert.Equal(source.DueVnd - part, row.DueVnd);
        Assert.Equal(page.Items.Sum(x => x.Lines.Count), page.LineCount); Assert.Equal(row.CostVnd, group.CostVnd); Assert.Equal(row.DueVnd, group.DueVnd);
        Assert.Equal(setup.Patient, group.PatientId);
        Assert.False(string.IsNullOrEmpty(group.PatientCode)); Assert.False(string.IsNullOrEmpty(group.DentistName));
        Assert.Equal(page.Items.Sum(x => x.CostVnd), page.Totals.CostVnd);
        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.GetAsync("/api/work-statistics?from=2026-12-01&to=2026-01-01")).StatusCode);
        Assert.Empty((await cashier.GetFromJsonAsync<WorkStatisticsPage>("/api/work-statistics?from=2000-01-01&to=2000-01-02"))!.Items);
        using var guest = await Login("guest.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/work-statistics")).StatusCode);
    }

    [Fact]
    public async Task LabOrderCanBeSavedOverwrittenListedAndDeletedWithValidation()
    {
        using var dentist = await Login();
        var setup = await Prepare(false); var dossier = await Create(dentist, setup);
        var draft = await Draft(dentist, setup, dossier.Id, "Plan");
        var plan = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var performed = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{Assert.Single(dossier.Items).Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        var url = $"/api/dossiers/{dossier.Id}/sheet-activities/{activity.Id}/lab-order";
        Guid labId, supplierId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var now = DateTimeOffset.UtcNow;
            var lab = new DCMS.Domain.Features.SystemSettings.CatalogProvider { Code = "LAB-T-" + Guid.NewGuid().ToString("N")[..6], Name = "Xưởng thử", NormalizedName = "XƯỞNG THỬ", Type = "Lab", CreatedAt = now, UpdatedAt = now };
            var supplier = new DCMS.Domain.Features.SystemSettings.CatalogProvider { Code = "SUP-T-" + Guid.NewGuid().ToString("N")[..6], Name = "Cung cấp thử", NormalizedName = "CUNG CẤP THỬ", Type = "Supplier", CreatedAt = now, UpdatedAt = now };
            db.AddRange(lab, supplier); await db.SaveChangesAsync(); labId = lab.Id; supplierId = supplier.Id;
        }
        var context = (await dentist.GetFromJsonAsync<LabOrderContext>(url))!;
        Assert.Null(context.Order); Assert.False(string.IsNullOrEmpty(context.Patient.Name)); Assert.Contains(context.Options.Labs, l => l.Id == labId);
        Assert.DoesNotContain(context.Options.Labs, l => l.Id == supplierId); Assert.NotEmpty(context.Options.Dentists);
        var dentistId = context.Options.DefaultDentistId ?? context.Options.Dentists[0].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        SaveLabOrderRequest Request(Action<Dictionary<string, object?>>? tweak = null, int? version = null) => new(null, today, today.AddDays(7), null, dentistId, labId, null, "Mão sứ", "Vai", null, null,
            ["ovoid"], false, ["36", "37"], new() { ["1"] = "A2" }, "Gửi gấp", "Zirconia", 2, 3_000_000, 1_000_000, "7 năm", version);

        var saved = await dentist.PutAsJsonAsync(url, Request()); Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        var order = (await saved.Content.ReadFromJsonAsync<LabOrderDto>())!; Assert.Equal(1, order.Version); Assert.Equal(["36", "37"], order.Teeth); Assert.Equal("A2", order.Shade["1"]);
        var list = (await dentist.GetFromJsonAsync<LabOrderSummary[]>($"/api/dossiers/{dossier.Id}/lab-orders"))!;
        Assert.Equal("Mão sứ", Assert.Single(list).WorkName);
        Assert.Equal(HttpStatusCode.Conflict, (await dentist.PutAsJsonAsync(url, Request(version: 9))).StatusCode);
        var again = await dentist.PutAsJsonAsync(url, Request(version: 1) with { Quantity = 3 });
        Assert.Equal(2, (await again.Content.ReadFromJsonAsync<LabOrderDto>())!.Version);

        foreach (var bad in new[] { Request(version: 2) with { Teeth = ["99"] }, Request(version: 2) with { Teeth = ["36", "36"] }, Request(version: 2) with { Teeth = ["55"] },
            Request(version: 2) with { DeliveredOn = today.AddDays(-1) }, Request(version: 2) with { PaidVnd = 4_000_000 }, Request(version: 2) with { WorkName = " " },
            Request(version: 2) with { DentistId = -5 }, Request(version: 2) with { LabProviderId = supplierId }, Request(version: 2) with { MarginInner = "Lạ" },
            Request(version: 2) with { Shade = new() { ["9"] = "A1" } }, Request(version: 2) with { Quantity = 0 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await dentist.PutAsJsonAsync(url, bad)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await dentist.PutAsJsonAsync(url, Request(version: 2) with { IsChild = true, Teeth = ["55"] })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dentist.GetAsync($"/api/dossiers/{dossier.Id}/sheet-activities/{Guid.NewGuid()}/lab-order")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await dentist.DeleteAsync(url)).StatusCode);
        Assert.Empty((await dentist.GetFromJsonAsync<LabOrderSummary[]>($"/api/dossiers/{dossier.Id}/lab-orders"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await dentist.DeleteAsync(url)).StatusCode);
    }

    [Fact]
    public async Task LabWorkListFiltersTracksStatusAndKeepsHistory()
    {
        using var dentist = await Login();
        var setup = await Prepare(false); var dossier = await Create(dentist, setup);
        var draft = await Draft(dentist, setup, dossier.Id, "Plan");
        var plan = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var performed = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{Assert.Single(dossier.Items).Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        var url = $"/api/dossiers/{dossier.Id}/sheet-activities/{activity.Id}/lab-order";
        var context = (await dentist.GetFromJsonAsync<LabOrderContext>(url))!;
        var dentistId = context.Options.DefaultDentistId ?? context.Options.Dentists[0].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var work = "Mão sứ " + Guid.NewGuid().ToString("N")[..8];
        var save = await dentist.PutAsJsonAsync(url, new SaveLabOrderRequest(null, today.AddDays(-5), today.AddDays(-1), null, dentistId, null, null, work, null, null, null,
            [], false, ["36"], new(), null, "Zirconia", 1, 2_000_000, 0, null, null));
        Assert.True(save.IsSuccessStatusCode, await save.Content.ReadAsStringAsync());
        var order = (await save.Content.ReadFromJsonAsync<LabOrderDto>())!; Assert.Equal("NotSent", order.Status);

        var page = (await dentist.GetFromJsonAsync<LabWorkPage>($"/api/lab-orders?q={Uri.EscapeDataString(work)}"))!;
        var item = Assert.Single(page.Items); Assert.True(item.Overdue); Assert.Equal(1, page.Counts.NotSent); Assert.Equal(1, page.Counts.Overdue);
        Assert.Equal(item.PatientName, context.Patient.Name);
        Assert.Single((await dentist.GetFromJsonAsync<LabWorkPage>($"/api/lab-orders?q={Uri.EscapeDataString(work)}&status=Overdue"))!.Items);
        Assert.Empty((await dentist.GetFromJsonAsync<LabWorkPage>($"/api/lab-orders?q={Uri.EscapeDataString(work)}&status=Sent"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await dentist.GetAsync("/api/lab-orders?status=Bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await dentist.GetAsync("/api/lab-orders?pageSize=7")).StatusCode);

        var statusUrl = $"/api/lab-orders/{order.Id}/status";
        Assert.Equal(HttpStatusCode.BadRequest, (await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Delivered", 1, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Sent", 9, null))).StatusCode);
        var sent = await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Sent", 1, "Gửi xưởng"));
        Assert.True(sent.IsSuccessStatusCode, await sent.Content.ReadAsStringAsync());
        var sentItem = (await sent.Content.ReadFromJsonAsync<LabWorkItem>())!; Assert.Equal("Sent", sentItem.Status); Assert.Equal(today, sentItem.SentOn); Assert.Equal(2, sentItem.Version);
        var received = (await (await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Received", 2, null))).Content.ReadFromJsonAsync<LabWorkItem>())!;
        Assert.Equal(today, received.ReceivedOn); Assert.False(received.Overdue);
        var delivered = (await (await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Delivered", 3, null))).Content.ReadFromJsonAsync<LabWorkItem>())!;
        Assert.Equal(today, delivered.DeliveredOn);
        var rework = (await (await dentist.PutAsJsonAsync(statusUrl, new ChangeLabStatusRequest("Rework", 4, "Răng lệch màu"))).Content.ReadFromJsonAsync<LabWorkItem>())!;
        Assert.Null(rework.DeliveredOn); Assert.Null(rework.ReceivedOn); Assert.True(rework.Overdue);

        var history = (await dentist.GetFromJsonAsync<LabStatusLogItem[]>($"/api/lab-orders/{order.Id}/history"))!;
        Assert.Equal(["Rework", "Delivered", "Received", "Sent", "NotSent"], history.Select(x => x.ToStatus));
        Assert.Equal("Răng lệch màu", history[0].Note);
        Assert.Equal(HttpStatusCode.NotFound, (await dentist.GetAsync($"/api/lab-orders/{Guid.NewGuid()}/history")).StatusCode);
        using var guest = await Login("guest.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/lab-orders")).StatusCode);
    }

    [Fact]
    public async Task BranchDeactivationIsBlockedByOpenDossierAndUndeliveredLabOrder()
    {
        using var dentist = await Login(); using var owner = await Login("owner.demo");
        var setup = await Prepare(false); var dossier = await Create(dentist, setup);
        var draft = await Draft(dentist, setup, dossier.Id, "Plan");
        var plan = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var performed = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{Assert.Single(dossier.Items).Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        var url = $"/api/dossiers/{dossier.Id}/sheet-activities/{activity.Id}/lab-order";
        var context = (await dentist.GetFromJsonAsync<LabOrderContext>(url))!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var save = await dentist.PutAsJsonAsync(url, new SaveLabOrderRequest(null, today, today.AddDays(5), null, context.Options.DefaultDentistId ?? context.Options.Dentists[0].Id,
            null, null, "Mão sứ", null, null, null, [], false, ["36"], new(), null, null, 1, 1_000_000, 0, null, null));
        Assert.True(save.IsSuccessStatusCode, await save.Content.ReadAsStringAsync());

        var command = new DCMS.Application.Features.Settings.BranchStateCommand(Guid.NewGuid(), 1, false, "Thử ngừng");
        var preview = await owner.PostAsJsonAsync($"/api/settings/branches/{setup.Branch}/state-preview", command);
        Assert.True(preview.IsSuccessStatusCode, await preview.Content.ReadAsStringAsync());
        var result = (await preview.Content.ReadFromJsonAsync<DCMS.Application.Features.Settings.ImpactPreviewResult>())!;
        Assert.False(result.Allowed);
        Assert.Contains(result.Blockers, b => b.Kind == "TreatmentDossiers" && b.Count == 1);
        Assert.Contains(result.Blockers, b => b.Kind == "LabOrders" && b.Count == 1 && b.NavigationUrl == "/lab-orders");
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/settings/branches/{setup.Branch}/state", command)).StatusCode);

        var order = (await save.Content.ReadFromJsonAsync<LabOrderDto>())!;
        foreach (var (target, version) in new[] { ("Sent", 1), ("Received", 2), ("Delivered", 3) })
            Assert.True((await dentist.PutAsJsonAsync($"/api/lab-orders/{order.Id}/status", new ChangeLabStatusRequest(target, version, null))).IsSuccessStatusCode);
        var after = (await (await owner.PostAsJsonAsync($"/api/settings/branches/{setup.Branch}/state-preview", command with { RequestKey = Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<DCMS.Application.Features.Settings.ImpactPreviewResult>())!;
        Assert.DoesNotContain(after.Blockers, b => b.Kind == "LabOrders");
        Assert.Contains(after.Blockers, b => b.Kind == "TreatmentDossiers");
    }

    [Fact]
    public async Task DirectTreatmentChargeCanBeCollectedInPartsAndSummaryUsesConfirmedAllocations()
    {
        using var dentist = await Login(); using var cashier = await Login("owner.demo");
        var setup = await Prepare(false); var dossier = await Create(dentist, setup);
        var draft = await Draft(dentist, setup, dossier.Id, "Plan");
        var plan = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        Assert.True(plan.IsSuccessStatusCode, await plan.Content.ReadAsStringAsync());
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = Assert.Single(dossier.Items);
        var performed = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/perform",
            new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        Assert.True(performed.IsSuccessStatusCode, await performed.Content.ReadAsStringAsync());
        var activity = (await performed.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity;
        Assert.Equal("Posted", activity.FeeStatus);
        var url = $"/api/finance/dossiers/{dossier.Id}";
        var bill = (await cashier.GetFromJsonAsync<DossierBillDto>(url))!;
        var charge = Assert.Single(bill.Charges);
        Assert.Equal(activity.Id, charge.ActivityId);
        Assert.Equal(charge.AmountVnd, bill.RemainingVnd);
        var snapshot = (await cashier.GetFromJsonAsync<TreatmentFinanceSnapshot>($"/api/dossiers/{dossier.Id}/finance"))!;
        Assert.True(snapshot.Available); Assert.Single(snapshot.Charges);
        var firstAmount = charge.AmountVnd / 2;
        var cash = new ConfirmCollectionRequest(dossier.Id, DateTimeOffset.UtcNow, "Cash", "SYNTHETIC payer",
            null, firstAmount, 0, [new CollectionAllocation(charge.Id, 1, firstAmount)], snapshot.SourceVersion, Guid.NewGuid());
        var cashResponse = await cashier.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/collections", cash);
        Assert.True(cashResponse.IsSuccessStatusCode, await cashResponse.Content.ReadAsStringAsync());
        var receipt = (await cashResponse.Content.ReadFromJsonAsync<TreatmentFinanceReceipt>())!;
        Assert.Equal("Confirmed", receipt.Status);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await cashier.PostAsJsonAsync(url + "/payments",
            new CollectPayment(setup.Patient, bill.AccountVersion, Guid.NewGuid(), "Cash", "SYNTHETIC payer",
                [new AllocationInput(Guid.NewGuid(), 1)]))).StatusCode);
        var print = await cashier.GetAsync($"/api/finance/payments/{receipt.Id}/print");
        Assert.True(print.IsSuccessStatusCode, await print.Content.ReadAsStringAsync());
        Assert.Equal(receipt.Id, (await (await cashier.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/collections", cash)).Content.ReadFromJsonAsync<TreatmentFinanceReceipt>())!.Id);
        bill = (await cashier.GetFromJsonAsync<DossierBillDto>(url))!;
        Assert.Equal(firstAmount, bill.PaidVnd);
        var transfer = new CollectPayment(setup.Patient, bill.AccountVersion, Guid.NewGuid(), "Transfer", "SYNTHETIC payer",
            [new AllocationInput(charge.Id, bill.RemainingVnd)], DeclaredReference: "TREATMENT-TEST");
        var transferResponse = await cashier.PostAsJsonAsync(url + "/payments", transfer);
        Assert.True(transferResponse.IsSuccessStatusCode, await transferResponse.Content.ReadAsStringAsync());
        var pending = (await transferResponse.Content.ReadFromJsonAsync<PaymentDto>())!;
        Assert.Equal("Pending", pending.State);
        bill = (await cashier.GetFromJsonAsync<DossierBillDto>(url))!;
        Assert.Equal(firstAmount, bill.PaidVnd);
        var confirmedResponse = await cashier.PostAsJsonAsync($"/api/finance/payments/{pending.Id}/confirm",
            new ConfirmPayment(pending.Version, bill.AccountVersion, Guid.NewGuid(), "TREATMENT-TEST", "SYNTHETIC reconciled"));
        Assert.True(confirmedResponse.IsSuccessStatusCode, await confirmedResponse.Content.ReadAsStringAsync());
        bill = (await cashier.GetFromJsonAsync<DossierBillDto>(url))!;
        Assert.Equal(0, bill.RemainingVnd);
        var detail = (await dentist.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!;
        Assert.True(detail.FinancialSummary!.Available);
        Assert.Equal(charge.AmountVnd, detail.FinancialSummary.AllocatedVnd);
        Assert.Equal(0, detail.FinancialSummary.OutstandingVnd);
    }

    [Fact]
    public async Task DirectActualNeedsNoPlanOrVisitAndDisplayFlagHasNoClinicalEffect()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        var row = await Draft(c, s, dossier.Id, "Actual", unitPrice: 0);
        var request = new ConfirmSheetActualRequest(dossier.Version, row.Version, null, 0, "", "Separate", null, false, null, Guid.NewGuid());
        var url = $"/api/dossiers/{dossier.Id}/sheet-drafts/{row.Id}/confirm-actual";
        var response = await c.PostAsJsonAsync(url, request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        Assert.Null(result.Activity.VisitId); Assert.Equal("NotRequested", result.Activity.FeeStatus);
        var item = Assert.Single(result.Dossier.Items); Assert.True(item.HiddenFromSheet); Assert.Null(item.DecidedAt);
        var preview = await c.GetFromJsonAsync<PlanPricePreviewDto>($"/api/dossiers/{dossier.Id}/plan/preview"); Assert.Equal(0, preview!.NetBeforeTaxVnd);
        var marked = await c.PatchAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/display-completion", new MarkSheetItemRequest(result.Dossier.Version, true, Guid.NewGuid()));
        Assert.True(marked.IsSuccessStatusCode, await marked.Content.ReadAsStringAsync());
        var after = (await marked.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.True(after.Items[0].DisplayCompleted); Assert.Equal(item.Progress, after.Items[0].Progress); Assert.Equal("Open", after.State);
        var stale = await c.PatchAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/display-completion", new MarkSheetItemRequest(result.Dossier.Version, false, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var assistant = await Login("assistant.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{item.Id}/perform", new SheetItemActionRequest(after.Version, Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task DirectSourceRejectsOtherDossierAndInvalidDurationWithoutConsumingDraft()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s); var other = await Create(c, s);
        var row = await Draft(c, s, dossier.Id, "Plan");
        var plan = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{row.Id}/confirm-plan", new ConfirmSheetPlanRequest(dossier.Version, row.Version, null, Guid.NewGuid()));
        Assert.True(plan.IsSuccessStatusCode, await plan.Content.ReadAsStringAsync());
        var actual = await Draft(c, s, other.Id, "Actual", row.Id);
        var response = await c.PostAsJsonAsync($"/api/dossiers/{other.Id}/sheet-drafts/{actual.Id}/confirm-actual", new ConfirmSheetActualRequest(other.Version, actual.Version, null, 0, "", "Separate", null, false, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single((await c.GetFromJsonAsync<IReadOnlyList<SheetDraftDto>>($"/api/dossiers/{other.Id}/sheet-drafts"))!);
        var invalid = JsonSerializer.SerializeToElement(new { serviceId = s.Service, dentistId = s.Dentist, regions = new[] { "25" }, quantity = 1, costVnd = 300000, unitPriceVnd = 300000, estimatedMinutes = 481 });
        var bad = await c.PutAsJsonAsync($"/api/dossiers/{other.Id}/sheet-drafts/{Guid.NewGuid()}", new SaveSheetDraftRequest("Plan", null, invalid, 0, Guid.NewGuid()));
        // Drafts can retain unfinished input; promotion must reject invalid duration atomically.
        if (bad.IsSuccessStatusCode)
        {
            var draft = (await bad.Content.ReadFromJsonAsync<SheetDraftDto>())!;
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/dossiers/{other.Id}/sheet-drafts/{draft.Id}/confirm-plan", new ConfirmSheetPlanRequest(other.Version, draft.Version, null, Guid.NewGuid()))).StatusCode);
        }
        else Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task FeeAdapterFailureRollsBackTransferAndDisplayFlag()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        var draft = await Draft(c, s, dossier.Id, "Plan");
        var promote = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan", new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        Assert.True(promote.IsSuccessStatusCode, await promote.Content.ReadAsStringAsync());
        dossier = (await promote.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        using var failedApp = fixture.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IClinicalChargePort>(); services.AddSingleton<IClinicalChargePort, RejectedSheetChargePort>();
        }));
        using var failingClient = failedApp.CreateClient();
        var login = await failingClient.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("dentist.demo", fixture.Password));
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        var request = new SheetItemActionRequest(dossier.Version, Guid.NewGuid());
        var result = await failingClient.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{draft.Id}/perform", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        var read = (await c.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!;
        Assert.Equal(dossier.Version, read.Version); Assert.False(read.Items[0].DisplayCompleted);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.ClinicalActivities.AnyAsync(a => a.DossierId == dossier.Id));
        Assert.False(await db.ClinicalCommandReceipts.AnyAsync(a => a.RequestKey == request.RequestKey));
        Assert.False(await db.TreatmentProgressionReceipts.AnyAsync(a => a.RequestKey == request.RequestKey));
    }

    [Fact]
    public async Task TransferSavesEditedRowAndMarksPlanAtomicallyOnlyOnExplicitCommand()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        var draft = await Draft(c, s, dossier.Id, "Plan", qty: 2);
        var plan = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        Assert.True(plan.IsSuccessStatusCode, await plan.Content.ReadAsStringAsync());
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.False(dossier.Items[0].DisplayCompleted);
        var row = JsonSerializer.SerializeToElement(new { serviceId = s.Service, dentistId = s.Dentist, regions = new[] { "25", "46" }, quantity = 2,
            unitPriceVnd = 400000, costVnd = 800000, date = "2026-10-27", note = "SYNTHETIC edited transfer", discountType = "VND", discountValue = 100000, discountNote = "SYNTHETIC actual reduction", labWork = "SYNTHETIC lab note" });
        var request = new PerformSheetItemRequest(dossier.Version, Guid.NewGuid(), row);
        var url = $"/api/dossiers/{dossier.Id}/sheet-items/{draft.Id}/perform";
        var posted = await c.PostAsJsonAsync(url, request); Assert.True(posted.IsSuccessStatusCode, await posted.Content.ReadAsStringAsync());
        var result = (await posted.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        Assert.Equal(400000, result.Activity.PriceSnapshotVnd); Assert.Equal(100000, result.Activity.DiscountSnapshotVnd);
        Assert.Equal("25, 46", result.Activity.Region); Assert.Equal("SYNTHETIC edited transfer", result.Activity.Outcome);
        Assert.Equal("SYNTHETIC lab note", result.Activity.LabWorkSnapshot); Assert.Null(result.Activity.VisitId);
        Assert.True(result.Dossier.Items[0].DisplayCompleted); Assert.Equal(300000, result.Dossier.Items[0].AgreedUnitPriceVnd);
        Assert.Equal("SYNTHETIC ghi chú", result.Dossier.Items[0].Note);
        var replay = await c.PostAsJsonAsync(url, request);
        Assert.Equal(result.Activity.Id, (await replay.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.Id);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Single(await db.ClinicalActivities.Where(a => a.DossierId == dossier.Id).ToListAsync());
        Assert.Empty(await db.TreatmentSheetDrafts.Where(a => a.DossierId == dossier.Id).ToListAsync());
        var fee = Assert.Single(await db.Set<FinanceCharge>().Where(e => e.ActivityId == result.Activity.Id).ToListAsync());
        Assert.Equal(dossier.Id, fee.DossierId); Assert.Equal(700000, fee.AmountVnd);
    }

    [Fact]
    public async Task SheetActivityEditDeleteIsVersionedAndPreservesPostedSourceAndPlan()
    {
        using var c = await Login(); var s = await Prepare(false); var dossier = await Create(c, s);
        var draft = await Draft(c, s, dossier.Id, "Plan", qty: 2);
        var plan = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{draft.Id}/confirm-plan", new ConfirmSheetPlanRequest(dossier.Version, draft.Version, null, Guid.NewGuid()));
        dossier = (await plan.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var transfer = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-items/{draft.Id}/perform", new SheetItemActionRequest(dossier.Version, Guid.NewGuid()));
        var original = (await transfer.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        var row = JsonSerializer.SerializeToElement(new { serviceId = s.Service, dentistId = s.Dentist, regions = new[] { "46" }, quantity = 2,
            unitPriceVnd = 350000, costVnd = 700000, note = "SYNTHETIC corrected note", discountType = "VND", discountValue = 50000, discountNote = "SYNTHETIC correction", labWork = "SYNTHETIC lab" });
        var request = new EditSheetActivityRequest(original.Dossier.Version, original.Activity.Version, 0, row, Guid.NewGuid());
        var url = $"/api/dossiers/{dossier.Id}/sheet-activities/{original.Activity.Id}";
        var response = await c.PutAsJsonAsync(url, request); Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var edited = (await response.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!;
        Assert.Equal(1, edited.Activity.SheetVersion); Assert.Equal("NeedsAdjustment", edited.Activity.FeeStatus);
        Assert.Equal(350000, edited.Activity.PriceSnapshotVnd); Assert.Equal(300000, edited.Dossier.Items[0].AgreedUnitPriceVnd);
        var replay = await c.PutAsJsonAsync(url, request);
        Assert.Equal(1, (await replay.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.SheetVersion);
        var stale = await c.PutAsJsonAsync(url, request with { ExpectedVersion = edited.Dossier.Version, RequestKey = Guid.NewGuid() }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var read = await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities");
        Assert.Equal("SYNTHETIC corrected note", Assert.Single(read!.Items).Outcome); Assert.Equal("46", read.Items[0].Region);
        using var assistant = await Login("assistant.demo"); Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PutAsJsonAsync(url, request)).StatusCode);
        var other = await Create(c, s);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/dossiers/{other.Id}/sheet-activities/{original.Activity.Id}", request with { ExpectedVersion = other.Version, RequestKey = Guid.NewGuid() })).StatusCode);
        var delete = new SheetActivityActionRequest(edited.Dossier.Version, edited.Activity.Version, edited.Activity.SheetVersion, Guid.NewGuid());
        using var message = new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(delete) };
        var deleted = await c.SendAsync(message); Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
        Assert.True((await deleted.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.SheetDeleted);
        Assert.Empty((await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities"))!.Items);
        using var repeat = new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(delete) };
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(repeat)).StatusCode);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var raw = await db.ClinicalActivities.SingleAsync(a => a.Id == original.Activity.Id);
        Assert.Equal(original.Activity.Outcome, raw.Outcome); Assert.Equal(original.Activity.PriceSnapshotVnd, raw.PriceSnapshotVnd); Assert.Equal(original.Activity.Version, raw.Version);
        Assert.Equal("Posted", raw.State); Assert.Equal(2, await db.TreatmentProgressionEvents.CountAsync(e => e.AggregateId == raw.Id && e.Kind == "clinical.fee.correction-requested"));
    }

}


public sealed class RejectedSheetChargePort : IClinicalChargePort
{
    public Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request, CancellationToken ct)
        => Task.FromResult(new ClinicalFeeResult("Unavailable"));
}
