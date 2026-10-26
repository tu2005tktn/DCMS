using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentDossierApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Login(string user)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(user, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private async Task<(int PatientId, int BranchId, int DentistId, int ServiceId)> Prepare()
    {
        using var assistant = await Login("assistant.demo");
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(
            null, "SYNTHETIC DOSSIER " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 5, 6),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patient = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentistId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" select employee.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "S21 isolated " + Guid.NewGuid().ToString("N") };
        db.Add(branch);
        await db.SaveChangesAsync();
        var member = new SchedulingMember { BranchId = branch.Id, EmployeeId = dentistId, CanDentist = true };
        var service = new SchedulingService { BranchId = branch.Id, Name = "Trám răng tổng hợp", PublicDurationMinutes = 30 };
        db.AddRange(member, service);
        await db.SaveChangesAsync();
        return (patient.Id, branch.Id, dentistId, service.Id);
    }

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Fact]
    public async Task WarrantyIsItemSpecificAndPrognosisDoesNotExtendCoverage()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC warranty", null,
                null, Guid.NewGuid()));
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = new TreatmentItemInput(null, null, setup.ServiceId, "25", "răng", 1, 1,
            30, null, null, 1000000, "Agreed", "Đã giải thích bảo hành", "Bệnh nhân", null, null);
        var draftResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            new SavePlanDraftRequest(created.Version, "SYNTHETIC warranty plan", [item], [], Guid.NewGuid()));
        var draft = (await draftResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(draft.Version, Guid.NewGuid()));
        var committed = (await committedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var itemId = Assert.Single(committed.Items).Id;
        var activation = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var warranty = new ConfirmWarrantyRequest(committed.Version, "SYNTHETIC policy", "v1",
            "Mão sứ răng 25", activation.AddYears(2), Guid.NewGuid(), "Mão sứ",
            "Không bao gồm tổn thương do chấn thương", activation,
            "Đã giải thích điều kiện và loại trừ", 120);
        var url = $"/api/dossiers/{created.Id}/items/{itemId}/warranty/confirm";
        await AssertMessage(await dentist.PostAsJsonAsync(url, warranty), HttpStatusCode.BadRequest, "MSG244");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var row = await db.TreatmentItems.SingleAsync(x => x.Id == itemId);
            row.Progress = "Completed"; // isolated clinical fixture, not a production actual.
            await db.SaveChangesAsync();
        }
        var confirmedResponse = await dentist.PostAsJsonAsync(url, warranty);
        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);
        var confirmed = (await confirmedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var covered = Assert.Single(confirmed.Items);
        Assert.Equal("Mão sứ", covered.WarrantyComponent);
        Assert.Equal(activation.AddYears(2), covered.WarrantyEndsOn);
        Assert.Equal(120, covered.ExpectedServiceLifeMonths);
        Assert.NotEqual(activation.AddYears(10), covered.WarrantyEndsOn);
    }

    [Fact]
    public async Task V23DurationsReferenceDiscountAndPaymentTermsStayPlanned()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        using var admin = await Login("admin.demo");
        using var assistant = await Login("assistant.demo");
        var priceUrl = $"/api/service-prices/{setup.ServiceId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PutAsJsonAsync(priceUrl,
            new SaveServicePriceRequest(1, 600000, Guid.NewGuid()))).StatusCode);
        var setPrice = await dentist.PutAsJsonAsync(priceUrl,
            new SaveServicePriceRequest(1, 600000, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, setPrice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(priceUrl)).StatusCode);
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC v23 plan", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var first = new TreatmentItemInput(null, null, setup.ServiceId, "46", "răng", 1, 1,
            25, null, null, 600000, "Agreed", "Đã tư vấn", "Bệnh nhân", null, null,
            EstimatedMinMinutes: 20, EstimatedMaxMinutes: 25, ReservedMinutes: 30,
            EstimatedSessions: 2, ReturnEarliestOn: new DateOnly(2027, 1, 1),
            ReturnTargetOn: new DateOnly(2027, 1, 8), ReturnLatestOn: new DateOnly(2027, 1, 15));
        var second = first with { Region = "25", PlannedOrder = 2, AgreedUnitPriceVnd = 1000000,
            EstimatedSessions = 4, Decision = "Deferred", Counseling = "Hoãn sau tư vấn" };
        var draft = new SavePlanDraftRequest(created.Version, "Điều trị nhiều buổi", [first, second],
            [new PaymentTermInput("Đợt một", 200000, new DateOnly(2027, 1, 1), "Thỏa thuận",
                "Sau buổi đầu")], Guid.NewGuid());
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            draft with { Items = [first with { ReservedMinutes = 20 }, second] }), HttpStatusCode.BadRequest, "MSG04");
        var savedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft", draft);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(20, saved.Items[0].EstimatedMinMinutes);
        Assert.Equal(25, saved.Items[0].EstimatedMaxMinutes);
        Assert.Equal(30, saved.Items[0].ReservedMinutes);
        Assert.Equal(600000, saved.Items[0].ReferenceUnitPriceVnd);
        Assert.Equal("Sau buổi đầu", saved.PaymentTerms[0].Milestone);
        Assert.Equal(600000, saved.FinancialSummary!.PlannedVnd); // Deferred item is not agreed cost.
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            draft with { ExpectedVersion = saved.Version, RequestKey = Guid.NewGuid(),
                Items = [first with { Id = saved.Items[0].Id, EstimatedMaxMinutes = 40 },
                    second with { Id = saved.Items[1].Id }] }), HttpStatusCode.BadRequest, "MSG04");

        var proposal = new TreatmentDiscountInput("Fixed", 300000, "Ưu đãi chụp sứ", saved.Items[0].Id);
        var proposedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            draft with { ExpectedVersion = saved.Version, RequestKey = Guid.NewGuid(),
                Items = [first with { Id = saved.Items[0].Id }, second with { Id = saved.Items[1].Id,
                    Decision = "Agreed", Discount = proposal }] });
        Assert.Equal(HttpStatusCode.OK, proposedResponse.StatusCode);
        var proposed = (await proposedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal("Proposed", proposed.Items[1].Discount!.Status);
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(proposed.Version, Guid.NewGuid())), HttpStatusCode.BadRequest, "MSG240");
        var decision = new DecideDiscountRequest(proposed.Version, "Approved", Guid.NewGuid());
        var decisionUrl = $"/api/commercial/dossiers/{created.Id}/items/{proposed.Items[1].Id}/discount/decision";
        Assert.Equal(HttpStatusCode.Forbidden,
            (await assistant.PostAsJsonAsync(decisionUrl, decision)).StatusCode);
        var approvedResponse = await dentist.PostAsJsonAsync(decisionUrl, decision);
        Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);
        var approved = (await approvedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(300000, approved.Items[1].Discount!.AmountVnd);
        var repeatedDecision = await dentist.PostAsJsonAsync(decisionUrl, decision);
        Assert.Equal(HttpStatusCode.OK, repeatedDecision.StatusCode);
        Assert.Equal(approved.Version,
            (await repeatedDecision.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Version);
        await AssertMessage(await dentist.PostAsJsonAsync(decisionUrl,
            decision with { Decision = "Rejected" }), HttpStatusCode.Conflict, "MSG111");
        Assert.Equal(HttpStatusCode.OK, (await dentist.PutAsJsonAsync(priceUrl,
            new SaveServicePriceRequest(2, 700000, Guid.NewGuid()))).StatusCode);
        var afterCatalogChange = await dentist.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{created.Id}");
        Assert.Equal(600000, afterCatalogChange!.Items[0].ReferenceUnitPriceVnd);
        var preview = await dentist.GetFromJsonAsync<PlanPricePreviewDto>($"/api/dossiers/{created.Id}/plan/preview");
        Assert.Equal(1300000, preview!.NetBeforeTaxVnd);
        var depositUrl = $"/api/commercial/dossiers/{created.Id}/deposit-exception";
        Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PostAsJsonAsync(depositUrl,
            new DepositExceptionRequest(approved.Version, "Không cọc", Guid.NewGuid()))).StatusCode);
        var deposit = await dentist.PostAsJsonAsync(depositUrl,
            new DepositExceptionRequest(approved.Version, "Thỏa thuận không cọc", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        var deposited = (await deposit.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(deposited.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        var committed = (await committedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(1300000, committed.FinancialSummary!.PlannedVnd);
        Assert.False(committed.FinancialSummary.Available);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(0, await db.Appointments.CountAsync(x => x.PatientId == setup.PatientId));
        Assert.Equal(0, await db.ClinicalActivities.CountAsync(x => x.PatientId == setup.PatientId));
    }

    [Fact]
    public async Task DraftDecisionSnapshotRetryAndClosureRemainIndependentOfActualAndCharges()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var create = new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC diagnosis", null,
            null, Guid.NewGuid());
        var url = $"/api/patients/{setup.PatientId}/dossiers";
        var createdResponse = await dentist.PostAsJsonAsync(url, create);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var retry = await dentist.PostAsJsonAsync(url, create);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(created.Id, (await retry.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier.Id);
        await AssertMessage(await dentist.PostAsJsonAsync(url, create with { Diagnosis = "Different" }),
            HttpStatusCode.Conflict, "MSG111");

        var filling = new TreatmentItemInput(null, null, setup.ServiceId, "25", "răng", 1, 1,
            30, null, null, 300000, "Agreed", "Lợi ích, rủi ro và vật liệu đã trao đổi", "Bệnh nhân", null, null);
        var deferred = filling with { Region = "46", PlannedOrder = 2, Decision = "Deferred",
            Counseling = "Hoãn chữa tủy sau tư vấn" };
        var draft = new SavePlanDraftRequest(created.Version, "Điều trị răng 25 và cân nhắc răng 46",
            [filling, deferred], [new PaymentTermInput("Theo thỏa thuận", 300000, new DateOnly(2027, 1, 1), null)],
            Guid.NewGuid());
        var draftResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft", draft);
        Assert.True(draftResponse.StatusCode == HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
        var saved = (await draftResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(2, saved.Items.Count);
        Assert.All(saved.Items, x => Assert.Equal("NotStarted", x.Progress));
        Assert.Equal("Draft", saved.PlanState);
        Assert.All(saved.Items, x => Assert.NotEqual(Guid.Empty, x.Id));
        var draftRetry = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft", draft);
        Assert.Equal(HttpStatusCode.OK, draftRetry.StatusCode);
        var replayed = (await draftRetry.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(saved.Version, replayed.Version);
        Assert.Equal(saved.Items.Select(x => x.Id), replayed.Items.Select(x => x.Id));
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            draft with { Goals = "Payload changed" }), HttpStatusCode.Conflict, "MSG111");
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            draft with { RequestKey = Guid.NewGuid() }), HttpStatusCode.Conflict, "MSG111");
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(saved.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        var committed = (await committedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal(1, committed.PlanVersion);
        Assert.Equal("Committed", committed.PlanState);
        Assert.Equal("Deferred", committed.Items.Single(x => x.Region == "46").Decision);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var service = await db.Set<SchedulingService>().SingleAsync(x => x.Id == setup.ServiceId);
            service.Name = "Đổi tên catalog";
            await db.SaveChangesAsync();
        }
        var reopened = (await dentist.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{created.Id}"))!;
        Assert.All(reopened.Items, x => { Assert.Equal("Trám răng tổng hợp", x.ServiceName); Assert.Equal(300000, x.AgreedUnitPriceVnd); });
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/close",
            new CloseDossierRequest(reopened.Version, "CompletedAsAgreed", null, [], Guid.NewGuid())),
            HttpStatusCode.Conflict, "MSG243");
        var agreedItem = reopened.Items.Single(x => x.Region == "25");
        await AssertMessage(await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/close",
            new CloseDossierRequest(reopened.Version, "StoppedIncomplete", "Dừng phần còn lại",
                [new ItemDisposition(agreedItem.Id, "Stopped", "Lý do 1"),
                 new ItemDisposition(agreedItem.Id, "Stopped", "Lý do 2")], Guid.NewGuid())),
            HttpStatusCode.Conflict, "MSG243");
        var stoppedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/close",
            new CloseDossierRequest(reopened.Version, "StoppedIncomplete", "Dừng phần còn lại",
                [new ItemDisposition(agreedItem.Id, "Stopped", "Bệnh nhân dừng")], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, stoppedResponse.StatusCode);
        var stopped = (await stoppedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        Assert.Equal("Stopped", stopped.State);
        Assert.Equal("StoppedIncomplete", stopped.ClosureOutcome);
        Assert.Equal("Stopped", stopped.Items.Single(x => x.Region == "25").Progress);
        Assert.Equal("NotStarted", stopped.Items.Single(x => x.Region == "46").Progress);
        using var verify = fixture.Services.CreateScope();
        var readDb = verify.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(0, await readDb.Appointments.CountAsync(x => x.PatientId == setup.PatientId));
    }

    [Fact]
    public async Task NonDentistCannotCreateAndUnlinkedPublicCannotReadOwnSummary()
    {
        var setup = await Prepare();
        var create = new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC", null, null, Guid.NewGuid());
        using var assistant = await Login("assistant.demo");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await assistant.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers", create)).StatusCode);
        using var publicClient = await Login("guest.demo");
        var summary = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(summary!, x => x.Diagnosis == "SYNTHETIC");
    }

    [Fact]
    public async Task CompetingDraftWritesCannotBothAdvanceOneDossierVersion()
    {
        var setup = await Prepare();
        using var firstClient = await Login("dentist.demo");
        using var secondClient = await Login("dentist.demo");
        var createdResponse = await firstClient.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC competing draft", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = new TreatmentItemInput(null, null, setup.ServiceId, "25", "răng", 1, 1,
            30, null, null, 300000, "Proposed", null, null, null, null);
        var first = firstClient.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            new SavePlanDraftRequest(created.Version, "Phương án A", [item], [], Guid.NewGuid()));
        var second = secondClient.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            new SavePlanDraftRequest(created.Version, "Phương án B", [item], [], Guid.NewGuid()));
        var responses = await Task.WhenAll(first, second);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = (await firstClient.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{created.Id}"))!;
        Assert.Equal(created.Version + 1, current.Version);
        Assert.Single(current.Items);
    }

    [Fact]
    public async Task OwnSummaryExposesOnlyCommittedPlanForVerifiedSelfLink()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var createdResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC own summary", null,
                null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        using var publicClient = await Login("guest.demo");
        var before = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(before!, x => x.DossierId == created.Id);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var accountId = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
            db.PatientAccountLinks.Add(new PatientAccountLink
            {
                AccountId = accountId, PatientId = setup.PatientId, Status = PatientLinkStatus.Verified,
                RequestCode = "SYNTHETIC-" + Guid.NewGuid().ToString("N")[..16],
                DeclaredFullName = "SYNTHETIC self", RequestedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var uncommitted = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        Assert.DoesNotContain(uncommitted!, x => x.DossierId == created.Id);
        var draftResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            new SavePlanDraftRequest(created.Version, "SYNTHETIC official plan",
                [new TreatmentItemInput(null, null, setup.ServiceId, "25", "răng", 1, 1, 30,
                    null, null, 300000, "Agreed", "Tư vấn phương án", "Bệnh nhân", null, null)],
                [], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(draft.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        var official = await publicClient.GetFromJsonAsync<OwnTreatmentSummaryItem[]>("/api/patients/me/treatment-summary");
        var item = Assert.Single(official!, x => x.DossierId == created.Id);
        Assert.Equal(1, item.PlanVersion);
        Assert.Single(item.AgreedItems);
    }
}
