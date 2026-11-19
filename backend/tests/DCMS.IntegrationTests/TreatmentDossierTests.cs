using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Treatment dossier: POST /api/patients/{id}/dossiers/quick (button "Tạo hồ sơ điều trị" on a patient).</summary>
[Collection(ApiCollection.Name)]
public sealed class TreatmentDossierTests(ApiHost host)
{
    private const string Dentist = "Nha sĩ (dentist.demo) đã đăng nhập, làm việc tại chi nhánh A; bệnh nhân đã tồn tại.";

    private async Task<int> NewPatientAsync()
    {
        var assistant = await host.ClientAsync("assistant.demo");
        var response = await assistant.PostAsync("/api/patients", new CreatePatientRequest(null, "Phạm Quốc Huy " + Guid.NewGuid().ToString("N")[..6],
            new DateOnly(1988, 8, 8), "MALE", "09" + Random.Shared.Next(10000000, 99999999), null, null, null, null, null, true, RequestKey: Guid.NewGuid()));
        Assert.Equal(201, response.Status);
        return response.Json<PatientChange>().Patient.Id;
    }

    [Fact, Case("DOS-01", "A dentist opens a treatment dossier for a patient", Dentist,
        "Step 1: Sign in as dentist.demo.",
        "Step 2: Call POST /api/patients/{id}/dossiers/quick with the branch.",
        "Step 3: Read the dossier back from the database.")]
    public async Task Dentist_opens_a_dossier()
    {
        var patient = await NewPatientAsync();
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync($"/api/patients/{patient}/dossiers/quick", new QuickDossierRequest(host.BranchA, Guid.NewGuid()));

        Assert.Equal(200, response.Status);
        var stored = await host.QueryAsync(db => db.TreatmentDossiers.AsNoTracking().SingleAsync(x => x.PatientId == patient));
        Assert.Equal(host.BranchA, stored.BranchId);
        Assert.Equal(host.DentistEmployeeId, stored.DentistId);
    }

    [Fact, Case("DOS-02", "Sending the same request twice opens one dossier only", Dentist,
        "Step 1: Call POST /api/patients/{id}/dossiers/quick with a request key.",
        "Step 2: Call it again with the same request key.")]
    public async Task Same_request_key_opens_one_dossier()
    {
        var patient = await NewPatientAsync();
        var request = new QuickDossierRequest(host.BranchA, Guid.NewGuid());
        var client = await host.ClientAsync("dentist.demo");
        Assert.Equal(200, (await client.PostAsync($"/api/patients/{patient}/dossiers/quick", request)).Status);

        var second = await client.PostAsync($"/api/patients/{patient}/dossiers/quick", request);

        Assert.Equal(200, second.Status);
        Assert.Equal(1, await host.QueryAsync(db => db.TreatmentDossiers.CountAsync(x => x.PatientId == patient)));
    }

    [Fact, Case("DOS-03", "A patient that does not exist is rejected", "Nha sĩ (dentist.demo) đã đăng nhập; patient 999999 does not exist.",
        "Step 1: Call POST /api/patients/999999/dossiers/quick.")]
    public async Task Unknown_patient_is_rejected()
    {
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync("/api/patients/999999/dossiers/quick", new QuickDossierRequest(host.BranchA, Guid.NewGuid()));

        Assert.Equal(404, response.Status);
        Assert.Equal("MSG113", response.Code);
    }

    [Fact, Case("DOS-04", "A branch the dentist does not work in is rejected", "Nha sĩ (dentist.demo) đã đăng nhập, chỉ làm việc tại chi nhánh A; bệnh nhân đã tồn tại.",
        "Step 1: Call POST /api/patients/{id}/dossiers/quick with the id of branch B.")]
    public async Task Other_branch_is_rejected()
    {
        var patient = await NewPatientAsync();
        var client = await host.ClientAsync("dentist.demo");

        var response = await client.PostAsync($"/api/patients/{patient}/dossiers/quick", new QuickDossierRequest(host.BranchB, Guid.NewGuid()));

        Assert.Equal(403, response.Status);
        Assert.Equal("MSG06", response.Code);
    }

    [Fact, Case("DOS-05", "A clinic assistant cannot open a dossier", "Trợ lý (assistant.demo) đã đăng nhập; bệnh nhân đã tồn tại.",
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST /api/patients/{id}/dossiers/quick.")]
    public async Task Assistant_is_rejected()
    {
        var patient = await NewPatientAsync();
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync($"/api/patients/{patient}/dossiers/quick", new QuickDossierRequest(host.BranchA, Guid.NewGuid()));

        Assert.Equal(403, response.Status);
    }

    [Fact, Case("DOS-06", "A request without a token is rejected", "No user is signed in.",
        "Step 1: Call POST /api/patients/1/dossiers/quick without an Authorization header.")]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/patients/1/dossiers/quick", new QuickDossierRequest(host.BranchA, Guid.NewGuid()));

        Assert.Equal(401, response.Status);
    }
}
