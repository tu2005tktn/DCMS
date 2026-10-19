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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ProgressionFixture : ApiFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITreatmentActualEvidencePort>();
            services.AddScoped<ITreatmentActualEvidencePort, SyntheticActualEvidencePort>();
        });
    }
}

public sealed class SyntheticActualEvidencePort : ITreatmentActualEvidencePort
{
    public Task<bool> HasDentistConfirmedActualAsync(int patientId, Guid dossierId, Guid stepId,
        IReadOnlyList<Guid> evidenceIds, CancellationToken ct) => Task.FromResult(evidenceIds.Count > 0);
    public Task<bool> HasFollowUpFulfillmentAsync(int patientId, Guid instructionId, CancellationToken ct) =>
        Task.FromResult(false);
}

public sealed class TreatmentProgressionApiTests(ProgressionFixture fixture) : IClassFixture<ProgressionFixture>
{
    private async Task<HttpClient> Login(string name)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }

    private async Task<(int Patient, int Branch, int Dentist, int Service)> Prepare()
    {
        using var assistant = await Login("assistant.demo");
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(null,
            "SYNTHETIC PROGRESSION " + Guid.NewGuid().ToString("N"), new DateOnly(1991, 4, 3),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patient = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "dentist.demo" select e.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "PROGRESSION " + Guid.NewGuid().ToString("N") };
        db.Add(branch); await db.SaveChangesAsync();
        var member = new SchedulingMember { BranchId = branch.Id, EmployeeId = dentist, CanDentist = true };
        var service = new SchedulingService { BranchId = branch.Id, Name = "Điều trị thử", PublicDurationMinutes = 30 };
        db.AddRange(member, service); await db.SaveChangesAsync();
        return (patient.Id, branch.Id, dentist, service.Id);
    }

    private static StepDefinition Definition(Guid id, Guid? itemId = null, Guid[]? parents = null) =>
        new(id, itemId, "Bước điều trị", "25", 20, 30, parents ?? [], null, null,
            1, 3, 5, "IncludedInItem");

    [Fact]
    public async Task TemplateSnapshotAndProspectiveInstructionStayIndependentOfBooking()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        Assert.Empty((await dentist.GetFromJsonAsync<TemplateListItem[]>(
            $"/api/treatment-templates?branchId={setup.Branch}"))!);
        var logical = Guid.NewGuid();
        var nextLogical = Guid.NewGuid();
        var createdResponse = await dentist.PostAsJsonAsync("/api/treatment-templates",
            new CreateTemplateRequest(setup.Branch, "Trám nhiều buổi", [Definition(logical),
                Definition(nextLogical, parents: [logical]) with { Name = "Kiểm tra lần hai" }], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var template = (await createdResponse.Content.ReadFromJsonAsync<TreatmentTemplateDto>())!;
        var activated = await dentist.PostAsJsonAsync($"/api/treatment-templates/{template.Id}/versions/1/activate",
            new TemplateStateRequest(template.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        template = (await activated.Content.ReadFromJsonAsync<TreatmentTemplateDto>())!;
        var listed = (await dentist.GetFromJsonAsync<TemplateListItem[]>(
            $"/api/treatment-templates?branchId={setup.Branch}"))!;
        Assert.Single(listed);
        Assert.Equal(template.Id, listed[0].Id);
        Assert.Equal(1, listed[0].ActiveVersion);

        var dossierResponse = await dentist.PostAsJsonAsync($"/api/patients/{setup.Patient}/dossiers",
            new CreateDossierRequest(setup.Branch, setup.Dentist, "Điều trị 25", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, dossierResponse.StatusCode);
        var dossier = (await dossierResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var draftResponse = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/plan/draft",
            new SavePlanDraftRequest(dossier.Version, "Điều trị nhiều buổi",
                [new TreatmentItemInput(null, null, setup.Service, "25", "răng", 1, 1,
                    30, null, null, 300000, "Agreed", "Đã tư vấn", "Bệnh nhân", null, null)],
                [], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
        dossier = (await draftResponse.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var itemId = dossier.Items.Single().Id;
        var apply = new ApplyTemplateRequest(dossier.Version, template.Id, 1,
            [Definition(logical, itemId), Definition(nextLogical, itemId, [logical]) with { Name = "Kiểm tra lần hai" }], Guid.NewGuid());
        var applied = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/plan/from-template", apply);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var appliedChange = (await applied.Content.ReadFromJsonAsync<StepChange>())!;
        var step = appliedChange.Steps.Single(x => x.Name == "Bước điều trị");
        Assert.NotEqual(logical, step.Id);
        Assert.Equal("Ready", step.Readiness);
        Assert.Equal("Blocked", appliedChange.Steps
            .Single(x => x.Name == "Kiểm tra lần hai").Readiness);
        var replay = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/plan/from-template", apply);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var afterApply = (await dentist.GetFromJsonAsync<TreatmentDossierDto>($"/api/dossiers/{dossier.Id}"))!;
        var commit = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/plan/commit",
            new CommitPlanRequest(afterApply.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var committed = (await commit.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var instruction = await dentist.PostAsJsonAsync("/api/follow-up-instructions", new CreateFollowUpRequest(
            setup.Patient, setup.Branch, new FollowUpSource(dossier.Id, itemId, step.Id, null, null),
            "TreatmentContinuation", "Quay lại để thực hiện bước đầu", null,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 5),
            30, setup.Dentist, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, instruction.StatusCode);
        Assert.Equal("NeedsBooking", (await instruction.Content.ReadFromJsonAsync<FollowUpChange>())!.Instruction.Status);
        var completeCommand = new CompleteStepRequest(committed.Version, step.Version,
            [Guid.NewGuid()], Guid.NewGuid());
        var complete = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/steps/{step.Id}/complete", completeCommand);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = (await complete.Content.ReadFromJsonAsync<StepChange>())!;
        Assert.Single(completed.InstructionIds);
        var completedRetry = await dentist.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/steps/{step.Id}/complete", completeCommand);
        Assert.Equal(HttpStatusCode.OK, completedRetry.StatusCode);
        Assert.Equal(completed.InstructionIds, (await completedRetry.Content.ReadFromJsonAsync<StepChange>())!.InstructionIds);
        Assert.Equal("Ready", completed.Steps.Single(x => x.Name == "Kiểm tra lần hai").Readiness);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.False(await db.Appointments.AnyAsync(x => x.PatientId == setup.Patient));
        Assert.Equal("Completed", (await db.FollowUpInstructions.SingleAsync(x => x.StepId == step.Id)).Status);

        var currentTemplate = (await dentist.GetFromJsonAsync<TreatmentTemplateDto>(
            $"/api/treatment-templates/{template.Id}"))!;
        Assert.Equal(template.Version, currentTemplate.Version);
        var next = await dentist.PostAsJsonAsync($"/api/treatment-templates/{template.Id}/versions",
            new NewTemplateVersionRequest(template.Version,
                [Definition(logical) with { Name = "Tên mới" },
                    Definition(nextLogical, parents: [logical]) with { Name = "Kiểm tra lần hai" }], Guid.NewGuid()));
        Assert.True(next.StatusCode == HttpStatusCode.OK, await next.Content.ReadAsStringAsync());
        Assert.Equal("Bước điều trị", (await dentist.GetFromJsonAsync<StepDto[]>($"/api/dossiers/{dossier.Id}/steps"))!
            .Single(x => x.Id == step.Id).Name);
    }

    [Fact]
    public async Task InvalidGraphAndDurationAreRejectedBeforePersistence()
    {
        var setup = await Prepare();
        using var dentist = await Login("dentist.demo");
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var response = await dentist.PostAsJsonAsync("/api/treatment-templates",
            new CreateTemplateRequest(setup.Branch, "Lỗi chu trình",
                [Definition(a, parents: [b]), Definition(b, parents: [a])], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG229", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        response = await dentist.PostAsJsonAsync("/api/treatment-templates",
            new CreateTemplateRequest(setup.Branch, "Lỗi thời lượng",
                [Definition(a) with { ReservedMinutes = 20 }], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG229", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }
}
