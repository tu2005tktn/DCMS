using DCMS.Application.Features.Patients;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>Patient records: POST /api/patients (screen "Thêm bệnh nhân").</summary>
[Collection(ApiCollection.Name)]
public sealed class PatientTests(ApiHost host)
{
    private const string Assistant = "A clinic assistant (ClinicAssistant) is signed in.";

    private static CreatePatientRequest Body(string? name = null, DateOnly? birth = null, string? gender = "FEMALE", string? phone = null, Guid? key = null) =>
        new(null, name ?? "Trần Thị Mai " + Guid.NewGuid().ToString("N")[..6], birth ?? new DateOnly(1990, 5, 20), gender,
            phone ?? "09" + Random.Shared.Next(10000000, 99999999), "12 Lê Lợi, Quận 1", null, null, null, null, true, RequestKey: key ?? Guid.NewGuid());

    [Fact, Case("PAT-01", "A clinic assistant creates a patient record", Assistant,
        "Step 1: Sign in as assistant.demo.",
        "Step 2: Call POST /api/patients with full name, birth date, gender, phone and address.",
        "Step 3: Read the patient back from the database.")]
    public async Task Assistant_creates_a_patient()
    {
        var body = Body();
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/patients", body);

        Assert.Equal(201, response.Status);
        var id = response.Json<PatientChange>().Patient.Id;
        var stored = await host.QueryAsync(db => db.Patients.AsNoTracking().SingleAsync(x => x.Id == id));
        Assert.Equal(body.FullName, stored.FullName);
        Assert.StartsWith("BN", stored.PatientCode);
    }

    [Fact, Case("PAT-02", "Sending the same request twice creates one patient only", Assistant,
        "Step 1: Call POST /api/patients with a request key.",
        "Step 2: Call POST /api/patients again with the same request key and the same data.")]
    public async Task Same_request_key_creates_one_patient()
    {
        var body = Body();
        var client = await host.ClientAsync("assistant.demo");
        var first = await client.PostAsync("/api/patients", body);

        var second = await client.PostAsync("/api/patients", body);

        Assert.Equal(201, first.Status);
        Assert.Equal(first.Json<PatientChange>().Patient.Id, second.Json<PatientChange>().Patient.Id);
        Assert.Equal(1, await host.QueryAsync(db => db.Patients.CountAsync(x => x.FullName == body.FullName)));
    }

    [Fact, Case("PAT-03", "A full name left empty is rejected", Assistant,
        "Step 1: Call POST /api/patients with FullName = \"\".")]
    public async Task Empty_name_is_rejected()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/patients", Body(name: ""));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("PAT-04", "A birth date in the future is rejected", Assistant,
        "Step 1: Call POST /api/patients with BirthDate = 2099-01-01.")]
    public async Task Future_birth_date_is_rejected()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/patients", Body(birth: new DateOnly(2099, 1, 1)));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("PAT-05", "A phone number with 5 digits is rejected", Assistant,
        "Step 1: Call POST /api/patients with Phone = \"12345\".")]
    public async Task Short_phone_is_rejected()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/patients", Body(phone: "12345"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("PAT-06", "A gender outside the allowed list is rejected", Assistant,
        "Step 1: Call POST /api/patients with Gender = \"ROBOT\".")]
    public async Task Unknown_gender_is_rejected()
    {
        var client = await host.ClientAsync("assistant.demo");

        var response = await client.PostAsync("/api/patients", Body(gender: "ROBOT"));

        Assert.Equal(400, response.Status);
    }

    [Fact, Case("PAT-07", "A suspected duplicate patient must be reviewed before it is saved", Assistant + " A patient with the same data exists.",
        "Step 1: Create a patient.",
        "Step 2: Call POST /api/patients with the same full name, birth date and phone, DuplicatesReviewed = false.")]
    public async Task Suspected_duplicate_must_be_reviewed()
    {
        var first = Body();
        var client = await host.ClientAsync("assistant.demo");
        Assert.Equal(201, (await client.PostAsync("/api/patients", first)).Status);

        var response = await client.PostAsync("/api/patients", first with { DuplicatesReviewed = false, RequestKey = Guid.NewGuid() });

        Assert.Equal(400, response.Status);
        Assert.Equal("MSG02", response.Code);
    }

    [Fact, Case("PAT-08", "A customer (public) account cannot create patients", "Customer account guest.demo is signed in.",
        "Step 1: Sign in as guest.demo.",
        "Step 2: Call POST /api/patients with valid data.")]
    public async Task Customer_cannot_create_patients()
    {
        var client = await host.ClientAsync("guest.demo");

        var response = await client.PostAsync("/api/patients", Body());

        Assert.Equal(403, response.Status);
    }

    [Fact, Case("PAT-09", "A request without a token is rejected", "No user is signed in.",
        "Step 1: Call POST /api/patients without an Authorization header.")]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await host.ClientAsync();

        var response = await client.PostAsync("/api/patients", Body());

        Assert.Equal(401, response.Status);
    }
}
