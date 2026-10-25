using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class TreatmentRevisionApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
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

    private async Task<(TreatmentDossierDto Dossier, int ServiceId)> Prepare(HttpClient dentist)
    {
        using var assistant = await Login("assistant.demo");
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(
            null, "SYNTHETIC S62 " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 5, 6),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patientId = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient.Id;
        int branchId, dentistId, serviceId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            dentistId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
                where user.UserName == "dentist.demo" select employee.Id).SingleAsync();
            var branch = new SchedulingBranch { Name = "S62 isolated " + Guid.NewGuid().ToString("N") };
            db.Add(branch);
            await db.SaveChangesAsync();
            branchId = branch.Id;
            var member = new SchedulingMember { BranchId = branchId, EmployeeId = dentistId, CanDentist = true };
            var service = new SchedulingService { BranchId = branchId, Name = "Trám răng S62", PublicDurationMinutes = 30 };
            db.AddRange(member, service);
            await db.SaveChangesAsync();
            serviceId = service.Id;
        }
        var created = (await (await dentist.PostAsJsonAsync($"/api/patients/{patientId}/dossiers",
            new CreateDossierRequest(branchId, dentistId, "SYNTHETIC revision", null, null,
                Guid.NewGuid()))).Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var item = new TreatmentItemInput(null, null, serviceId, "25", "răng", 1, 1,
            30, null, null, 300000, "Agreed", "Đã tư vấn phương án ban đầu", "Bệnh nhân", null, null);
        var draftResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/draft",
            new SavePlanDraftRequest(created.Version, "Điều trị răng 25", [item], [], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        var saved = (await draftResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var committedResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{created.Id}/plan/commit",
            new CommitPlanRequest(saved.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        return ((await committedResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier, serviceId);
    }

    [Fact]
    public async Task RevisionDraftPreviewCommitRetryAndHistoryKeepOriginalSnapshot()
    {
        using var dentist = await Login("dentist.demo");
        var setup = await Prepare(dentist);
        var original = setup.Dossier;
        var item = Assert.Single(original.Items);
        var baseUrl = $"/api/dossiers/{original.Id}/plan";
        var draftUrl = $"{baseUrl}/revision-drafts/{item.Id}";
        using var assistant = await Login("assistant.demo");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await assistant.GetAsync($"{baseUrl}/revisions/context?itemId={item.Id}")).StatusCode);
        var context = (await dentist.GetFromJsonAsync<RevisionContextDto>(
            $"{baseUrl}/revisions/context?itemId={item.Id}"))!;
        Assert.Contains(context.Services, x => x.Id == setup.ServiceId);

        var priceChange = new SaveRevisionDraftRequest(0, original.PlanVersion,
            new RevisionProposal(setup.ServiceId, "Sứ", "25", "răng", 1, 60, 400000),
            "Đổi vật liệu sau khi tư vấn", "Đã trao đổi phương án, lợi ích và rủi ro",
            "Agreed", "Bệnh nhân", null, [], Guid.NewGuid());
        var priceDraftResponse = await dentist.PutAsJsonAsync(draftUrl, priceChange);
        Assert.Equal(HttpStatusCode.OK, priceDraftResponse.StatusCode);
        var priceDraft = (await priceDraftResponse.Content.ReadFromJsonAsync<RevisionDraftDto>())!;
        var pricePreview = (await (await dentist.PostAsJsonAsync($"{baseUrl}/revisions/preview",
            new PreviewRevisionRequest(item.Id, priceDraft.Version)))
            .Content.ReadFromJsonAsync<RevisionPreviewDto>())!;
        Assert.False(pricePreview.CanSave);
        Assert.Contains("MSG245", pricePreview.Blockers);
        Assert.Equal(original.Version, (await dentist.GetFromJsonAsync<TreatmentDossierDto>(
            $"/api/dossiers/{original.Id}"))!.Version);

        var materialDraftResponse = await dentist.PutAsJsonAsync(draftUrl, priceChange with
        {
            ExpectedDraftVersion = priceDraft.Version,
            Proposed = priceChange.Proposed with { AgreedUnitPriceVnd = item.AgreedUnitPriceVnd },
            RequestKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, materialDraftResponse.StatusCode);
        var materialDraft = (await materialDraftResponse.Content.ReadFromJsonAsync<RevisionDraftDto>())!;
        var materialPreview = (await (await dentist.PostAsJsonAsync($"{baseUrl}/revisions/preview",
            new PreviewRevisionRequest(item.Id, materialDraft.Version)))
            .Content.ReadFromJsonAsync<RevisionPreviewDto>())!;
        Assert.Contains("MSG245", materialPreview.Blockers);

        var safeDraftRequest = priceChange with
        {
            ExpectedDraftVersion = materialDraft.Version,
            Proposed = priceChange.Proposed with { Material = item.Material,
                AgreedUnitPriceVnd = item.AgreedUnitPriceVnd },
            RequestKey = Guid.NewGuid()
        };
        var safeDraftResponse = await dentist.PutAsJsonAsync(draftUrl, safeDraftRequest);
        Assert.Equal(HttpStatusCode.OK, safeDraftResponse.StatusCode);
        var safeDraft = (await safeDraftResponse.Content.ReadFromJsonAsync<RevisionDraftDto>())!;
        Assert.Equal(safeDraft.Version, (await dentist.GetFromJsonAsync<RevisionDraftDto>(draftUrl))!.Version);
        var preview = (await (await dentist.PostAsJsonAsync($"{baseUrl}/revisions/preview",
            new PreviewRevisionRequest(item.Id, safeDraft.Version)))
            .Content.ReadFromJsonAsync<RevisionPreviewDto>())!;
        Assert.True(preview.CanSave, string.Join(",", preview.Blockers));
        var command = new CommitRevisionRequest(item.Id, safeDraft.Version, preview.DossierVersion,
            preview.PlanVersion, [], [], [], preview.Actual.VersionToken,
            preview.Finance.VersionToken, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await dentist.PostAsJsonAsync(
            $"{baseUrl}/revisions", command with
            { ExpectedDossierVersion = command.ExpectedDossierVersion + 1,
                RequestKey = Guid.NewGuid() })).StatusCode);
        var committedResponse = await dentist.PostAsJsonAsync($"{baseUrl}/revisions", command);
        Assert.Equal(HttpStatusCode.OK, committedResponse.StatusCode);
        var committed = (await committedResponse.Content.ReadFromJsonAsync<RevisionChange>())!;
        Assert.Equal(original.PlanVersion + 1, committed.PlanVersion);
        Assert.Equal(item.Id, committed.SelectedItemId);
        Assert.Equal(60, Assert.Single(committed.Dossier.Items).EstimatedMinutes);
        Assert.Equal(committed.PlanVersion, Assert.Single(committed.Dossier.Items).DecisionPlanVersion);
        Assert.Null(Assert.Single(committed.Dossier.Items).ConsentDocumentId);
        Assert.Equal(HttpStatusCode.OK, (await dentist.PostAsJsonAsync($"{baseUrl}/revisions", command)).StatusCode);
        var reused = await dentist.PostAsJsonAsync($"{baseUrl}/revisions", command with
        { ExpectedPlanVersion = original.PlanVersion + 10 });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("MSG111", (await reused.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        var history = (await dentist.GetFromJsonAsync<TreatmentHistoryItem[]>($"{baseUrl}/history"))!;
        Assert.Equal(2, history.Length);
        Assert.Contains("300000", history.Single(x => x.Version == 1).SnapshotJson);
        Assert.Contains("60", history.Single(x => x.Version == 2).SnapshotJson);
        Assert.Equal(HttpStatusCode.NotFound, (await dentist.GetAsync(draftUrl)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRevisionCommandsDoNotCreateTwoPlanVersions()
    {
        using var dentist = await Login("dentist.demo");

        async Task<(string Url, CommitRevisionRequest Command, Guid DossierId)> Ready()
        {
            var (dossier, _) = await Prepare(dentist);
            var item = Assert.Single(dossier.Items);
            var url = $"/api/dossiers/{dossier.Id}/plan";
            var response = await dentist.PutAsJsonAsync($"{url}/revision-drafts/{item.Id}",
                new SaveRevisionDraftRequest(0, dossier.PlanVersion,
                    new RevisionProposal(item.ServiceId, item.Material, item.Region, item.Unit,
                        item.Quantity, 60, item.AgreedUnitPriceVnd),
                    "Điều chỉnh thời gian", "Đã tư vấn thời gian, lợi ích và rủi ro",
                    "Agreed", "Bệnh nhân", null, [], Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var draft = (await response.Content.ReadFromJsonAsync<RevisionDraftDto>())!;
            var preview = (await (await dentist.PostAsJsonAsync($"{url}/revisions/preview",
                new PreviewRevisionRequest(item.Id, draft.Version)))
                .Content.ReadFromJsonAsync<RevisionPreviewDto>())!;
            Assert.True(preview.CanSave);
            return (url, new CommitRevisionRequest(item.Id, draft.Version,
                preview.DossierVersion, preview.PlanVersion, [], [], [],
                preview.Actual.VersionToken, preview.Finance.VersionToken, Guid.NewGuid()), dossier.Id);
        }

        var same = await Ready();
        var sameResults = await Task.WhenAll(
            dentist.PostAsJsonAsync($"{same.Url}/revisions", same.Command),
            dentist.PostAsJsonAsync($"{same.Url}/revisions", same.Command));
        Assert.True(sameResults.All(x => x.StatusCode == HttpStatusCode.OK),
            string.Join(" | ", await Task.WhenAll(sameResults.Select(async x =>
                $"{x.StatusCode}: {await x.Content.ReadAsStringAsync()}"))));
        Assert.Equal(Assert.Single((await sameResults[0].Content.ReadFromJsonAsync<RevisionChange>())!
            .Dossier.Items).DecisionPlanVersion,
            Assert.Single((await sameResults[1].Content.ReadFromJsonAsync<RevisionChange>())!
            .Dossier.Items).DecisionPlanVersion);

        var distinct = await Ready();
        var distinctResults = await Task.WhenAll(
            dentist.PostAsJsonAsync($"{distinct.Url}/revisions", distinct.Command),
            dentist.PostAsJsonAsync($"{distinct.Url}/revisions", distinct.Command with
            { RequestKey = Guid.NewGuid() }));
        Assert.Contains(distinctResults, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Contains(distinctResults, x => x.StatusCode == HttpStatusCode.Conflict);
        var latest = (await dentist.GetFromJsonAsync<TreatmentDossierDto>(
            $"/api/dossiers/{distinct.DossierId}"))!;
        Assert.Equal(2, latest.PlanVersion);
    }
}
