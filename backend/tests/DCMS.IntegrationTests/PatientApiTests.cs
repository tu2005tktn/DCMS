using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class PatientApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Login(string name = "assistant.demo")
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private static CreatePatientRequest Request() => new(null, "SYNTHETIC " + Guid.NewGuid().ToString("N"),
        new DateOnly(1990, 5, 6), "OTHER", null, null, null, null, null, null, false);

    private static UpdatePatientRequest Update(PatientDetail patient) => new(patient.Version, patient.FullName,
        patient.BirthDate, patient.Gender, patient.Phone, patient.Address, patient.GuardianName,
        patient.GuardianRelationship, patient.GuardianPhone, patient.Note, true);

    private static async Task<PatientDetail> Create(HttpClient client, CreatePatientRequest? request = null)
    {
        var response = await client.PostAsJsonAsync("/api/patients", request ?? Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
    }

    private static async Task Message(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
    }

    [Theory]
    [InlineData("admin.demo", false, false)]
    [InlineData("guest.demo", false, false)]
    [InlineData("dentist.demo", true, false)]
    [InlineData("owner.demo", true, false)]
    [InlineData("assistant.demo", true, true)]
    public async Task ServerEnforcesReadAndWriteRoles(string user, bool read, bool manage)
    {
        using var client = await Login(user);
        Assert.Equal(read ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/patients")).StatusCode);
        Assert.Equal(manage ? HttpStatusCode.Created : HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/patients", Request())).StatusCode);
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;
        Assert.Equal(read, me.Permissions.Contains("patients.read"));
        Assert.Equal(manage, me.Permissions.Contains("patients.manage"));
    }

    [Fact]
    public async Task AnonymousCannotReadPatients()
    {
        using var client = fixture.CreateClient();
        await Message(await client.GetAsync("/api/patients"), HttpStatusCode.Unauthorized, "MSG07");
    }

    [Fact]
    public async Task CreateSearchEditArchiveRestoreRetainsAuditAndStaleProtection()
    {
        using var client = await Login();
        var patient = await Create(client);
        Assert.Matches("^BN[0-9]{6,}$", patient.PatientCode);
        var editedResponse = await client.PutAsJsonAsync($"/api/patients/{patient.Id}", Update(patient) with { Address = "SYNTHETIC updated address" });
        Assert.Equal(HttpStatusCode.OK, editedResponse.StatusCode);
        var edited = (await editedResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        Assert.Equal(patient.Version + 1, edited.Version);
        Assert.Equal(patient.PatientCode, edited.PatientCode);
        await Message(await client.PutAsJsonAsync($"/api/patients/{patient.Id}", Update(patient)), HttpStatusCode.Conflict, "MSG111");
        await Message(await client.PostAsJsonAsync($"/api/patients/{patient.Id}/archive", new ArchivePatientRequest(edited.Version, " ")),
            HttpStatusCode.BadRequest, "MSG02");
        var archivedResponse = await client.PostAsJsonAsync($"/api/patients/{patient.Id}/archive", new ArchivePatientRequest(edited.Version, "SYNTHETIC archive"));
        Assert.Equal(HttpStatusCode.OK, archivedResponse.StatusCode);
        var archived = (await archivedResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient;
        Assert.True(archived.IsArchived);
        Assert.DoesNotContain((await client.GetFromJsonAsync<PatientPage>($"/api/patients?keyword={patient.PatientCode}"))!.Items, x => x.Id == patient.Id);
        Assert.Contains((await client.GetFromJsonAsync<PatientPage>($"/api/patients?keyword={patient.PatientCode}&includeArchived=true"))!.Items, x => x.Id == patient.Id);
        await Message(await client.PutAsJsonAsync($"/api/patients/{patient.Id}", Update(archived)), HttpStatusCode.Conflict, "MSG202");
        var restoredResponse = await client.PostAsJsonAsync($"/api/patients/{patient.Id}/restore", new ArchivePatientRequest(archived.Version, "SYNTHETIC restore"));
        Assert.Equal(HttpStatusCode.OK, restoredResponse.StatusCode);
        Assert.False((await restoredResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient.IsArchived);
        var audit = (await client.GetFromJsonAsync<PatientAuditEntry[]>($"/api/patients/{patient.Id}/audit"))!;
        Assert.Equal(new[] { "RESTORE", "ARCHIVE", "UPDATE", "CREATE" }, audit.Select(x => x.Action));
        Assert.Contains(audit.Single(x => x.Action == "UPDATE").Changes, x => x.After == "SYNTHETIC updated address");
    }

    [Fact]
    public async Task SearchNormalizesVietnameseNamesPhoneAndBirthDateWithoutWriting()
    {
        using var client = await Login();
        var suffix = Guid.NewGuid().ToString("N");
        var patient = await Create(client, Request() with { FullName = "Đặng Thị Ánh " + suffix, Phone = "0912 345 678" });
        foreach (var term in new[] { "dang thi anh " + suffix, patient.PatientCode.ToLowerInvariant(), "0912345678" })
            Assert.Contains((await client.GetFromJsonAsync<PatientPage>($"/api/patients?keyword={Uri.EscapeDataString(term)}"))!.Items, x => x.Id == patient.Id);
        Assert.Single((await client.GetFromJsonAsync<PatientPage>($"/api/patients?keyword={suffix}&birthDate=1990-05-06"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<PatientPage>($"/api/patients?keyword={suffix}&birthDate=1991-05-06"))!.Items);
        Assert.Single((await client.GetFromJsonAsync<PatientAuditEntry[]>($"/api/patients/{patient.Id}/audit"))!);
    }

    [Fact]
    public async Task DuplicateReviewIsRequiredButSharedPhoneNeverMergesRecords()
    {
        using var client = await Login();
        var request = Request() with { Phone = "0988776655" };
        var first = await Create(client, request with { DuplicatesReviewed = true });
        var candidates = await client.PostAsJsonAsync("/api/patients/duplicates",
            new DuplicateCheckRequest(request.FullName, request.BirthDate, request.Phone, null));
        Assert.Contains((await candidates.Content.ReadFromJsonAsync<DuplicateCandidate[]>())!, x => x.Patient.Id == first.Id);
        await Message(await client.PostAsJsonAsync("/api/patients", request), HttpStatusCode.BadRequest, "MSG02");
        var second = await Create(client, request with { DuplicatesReviewed = true });
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.PatientCode, second.PatientCode);
    }

    [Fact]
    public async Task ManualCodeNormalizesAndRejectsDuplicate()
    {
        using var client = await Login();
        var code = "test-" + Guid.NewGuid().ToString("N");
        var first = await Create(client, Request() with { PatientCode = " " + code + " " });
        Assert.Equal(code.ToUpperInvariant(), first.PatientCode);
        await Message(await client.PostAsJsonAsync("/api/patients", Request() with { PatientCode = code }), HttpStatusCode.Conflict, "MSG201");
    }

    [Theory]
    [InlineData("name", "MSG02")]
    [InlineData("longName", "MSG03")]
    [InlineData("futureBirth", "MSG04")]
    [InlineData("phone", "MSG04")]
    [InlineData("guardian", "MSG02")]
    public async Task InvalidDataIsRejected(string field, string message)
    {
        using var client = await Login();
        var request = field switch
        {
            "name" => Request() with { FullName = " " },
            "longName" => Request() with { FullName = new string('A', 101) },
            "futureBirth" => Request() with { BirthDate = new DateOnly(2099, 1, 1) },
            "phone" => Request() with { Phone = "abc" },
            _ => Request() with { GuardianPhone = "0912345678" }
        };
        await Message(await client.PostAsJsonAsync("/api/patients", request), HttpStatusCode.BadRequest, message);
    }

    [Fact]
    public async Task AdministrativeEditPreservesVerifiedAccountLink()
    {
        using var client = await Login();
        var patient = await Create(client);
        int linkId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var accountId = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
            var link = new PatientAccountLink { AccountId = accountId, PatientId = patient.Id, Status = PatientLinkStatus.Verified,
                RequestCode = "PT-" + Guid.NewGuid().ToString("N")[..24], DeclaredFullName = patient.FullName, RequestedAt = DateTimeOffset.UtcNow };
            db.PatientAccountLinks.Add(link); await db.SaveChangesAsync(); linkId = link.Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/patients/{patient.Id}", Update(patient) with { Note = "SYNTHETIC administrative edit" })).StatusCode);
        using var verify = fixture.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<DcmsDbContext>().PatientAccountLinks.AsNoTracking().SingleAsync(x => x.Id == linkId);
        Assert.Equal(patient.Id, saved.PatientId);
        Assert.Equal(PatientLinkStatus.Verified, saved.Status);
    }

    [Fact]
    public async Task InvalidPaginationAndMissingRecordReturnBusinessErrors()
    {
        using var client = await Login();
        await Message(await client.GetAsync("/api/patients?pageSize=10"), HttpStatusCode.BadRequest, "MSG04");
        await Message(await client.GetAsync("/api/patients/2147483647"), HttpStatusCode.NotFound, "MSG113");
    }
}
