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
        db.AddRange(service, chair, new SchedulingMember { BranchId = branch.Id, EmployeeId = staff.Id, CanDentist = true });
        await db.SaveChangesAsync();
        var visits = new List<Guid>();
        for (var i = 0; i < (createVisits ? 2 : 0); i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(-i).AddMinutes(-30);
            var id = Guid.NewGuid();
            db.Add(new ResourceReservation { Id = id, Code = "RV-" + id.ToString("N"), BranchId = branch.Id, DentistId = staff.Id,
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
        db.Add(step); await db.SaveChangesAsync();
        var deferred = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sheet-drafts/{b.Id}/confirm-plan",
            new ConfirmSheetPlanRequest(saved.Version, b.Version, new("Deferred", "SYNTHETIC hoãn", "SYNTHETIC"), Guid.NewGuid()));
        Assert.True(deferred.IsSuccessStatusCode, await deferred.Content.ReadAsStringAsync());
        var next = (await deferred.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        await db.Entry(step).ReloadAsync(); Assert.Equal(next.PlanVersion, step.PlanVersion); Assert.Equal(2, step.Version);
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
            Assert.Equal("Pending", result.Activity.FeeStatus); Assert.Equal(50000, result.Activity.DiscountSnapshotVnd);
            var retry = await c.PostAsJsonAsync(url, request);
            Assert.Equal(result.Activity.Id, (await retry.Content.ReadFromJsonAsync<ConfirmSheetActualResult>())!.Activity.Id);
            dossier = result.Dossier;
        }
        var page = await c.GetFromJsonAsync<TreatmentActivityPage>($"/api/dossiers/{dossier.Id}/activities");
        Assert.Equal(2, page!.Total); Assert.Equal(2, page.Items.Select(x => x.VisitId).Distinct().Count());
        var close = await c.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/close", new CloseDossierRequest(dossier.Version, "CompletedAsAgreed", null, [], Guid.NewGuid()));
        Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync());
        Assert.False((await close.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.FinancialSummaryAvailable);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var ids = page.Items.Select(x => x.Id).ToArray();
        Assert.Equal(2, await db.TreatmentProgressionEvents.CountAsync(x => ids.Contains(x.AggregateId) && x.Kind == "clinical.fee.requested"));
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
        db.Add(new SchedulingMember { BranchId = s.Branch, EmployeeId = employee.Id }); await db.SaveChangesAsync();
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
            db.Add(step); await db.SaveChangesAsync(); stepId = step.Id;
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
        Assert.Equal("Pending", result.Activity.FeeStatus); Assert.True(result.Dossier.Items[0].DisplayCompleted);
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
        Assert.Equal(1, await db.TreatmentProgressionEvents.CountAsync(e => e.AggregateId == actual.Id && e.Kind == "clinical.fee.requested"));
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
        var fee = Assert.Single(await db.TreatmentProgressionEvents.Where(e => e.AggregateId == result.Activity.Id && e.Kind == "clinical.fee.requested").ToListAsync());
        Assert.Contains("800000", fee.PayloadJson); Assert.Contains("100000", fee.PayloadJson);
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
