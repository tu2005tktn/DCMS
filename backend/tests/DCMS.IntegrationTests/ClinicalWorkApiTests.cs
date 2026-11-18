using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Care task backend (S25/S60); the visit-based treatment flow (S22) was removed.
public sealed class ClinicalWorkFixture : ApiFixture;

public sealed class ClinicalWorkApiTests(ClinicalWorkFixture fixture) : IClassFixture<ClinicalWorkFixture>
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

    private async Task<(int PatientId, int BranchId, int ServiceId, Guid VisitId)> SetupAsync(
        HttpClient assistant)
    {
        var patientResponse = await assistant.PostAsJsonAsync("/api/patients", new CreatePatientRequest(
            null, "SYNTHETIC clinical " + Guid.NewGuid().ToString("N"), new DateOnly(1990, 5, 6),
            "OTHER", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.Created, patientResponse.StatusCode);
        var patientId = (await patientResponse.Content.ReadFromJsonAsync<PatientChange>())!.Patient.Id;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var dentistId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "dentist.demo" select employee.Id).SingleAsync();
        var assistantId = await (from employee in db.Employees join user in db.Users on employee.AccountId equals user.Id
            where user.UserName == "assistant.demo" select employee.Id).SingleAsync();
        var branch = new SchedulingBranch { Name = "SYNTHETIC clinical " + Guid.NewGuid().ToString("N") };
        db.Add(branch); await db.SaveChangesAsync();
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentistId, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = assistantId },
            new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC trám", PublicDurationMinutes = 30 });
        await db.SaveChangesAsync();
        var serviceId = await db.Set<SchedulingService>().Where(x => x.BranchId == branch.Id)
            .Select(x => x.Id).SingleAsync();
        return (patientId, branch.Id, serviceId, Guid.NewGuid());
    }

    [Fact]
    public async Task CareContactRetryDoesNotCreateAnotherAttemptOrCompleteInstruction()
    {
        using var dentist = await Login("dentist.demo");
        using var assistant = await Login("assistant.demo");
        var setup = await SetupAsync(assistant);
        var create = new CreateCareTaskRequest(setup.PatientId, setup.BranchId,
            null, "FollowUp", "Gọi hỏi tình trạng sau điều trị", null,
            DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());
        var created = await assistant.PostAsJsonAsync("/api/care-tasks", create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<CareTaskDto>())!;
        var again = (await (await assistant.PostAsJsonAsync("/api/care-tasks", create))
            .Content.ReadFromJsonAsync<CareTaskDto>())!;
        Assert.Equal(task.Id, again.Id);
        var contact = new ContactAttemptRequest("Phone", "Reached", "Gọi lại tuần sau",
            null, task.Version, Guid.NewGuid());
        var first = await assistant.PostAsJsonAsync($"/api/care-tasks/{task.Id}/contact-attempts", contact);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var attempt = (await first.Content.ReadFromJsonAsync<ContactAttemptDto>())!;
        var retry = (await (await assistant.PostAsJsonAsync($"/api/care-tasks/{task.Id}/contact-attempts",
            contact)).Content.ReadFromJsonAsync<ContactAttemptDto>())!;
        Assert.Equal(attempt.Id, retry.Id);
        var detail = await assistant.GetFromJsonAsync<CareTaskDetail>($"/api/care-tasks/{task.Id}");
        Assert.Single(detail!.Contacts);
        Assert.Equal("Open", detail.Task.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await dentist.PostAsJsonAsync(
            $"/api/care-tasks/{task.Id}/escalations", new EscalateCareRequest(-123,
                "Nhờ bác sĩ đánh giá", detail.Task.Version, Guid.NewGuid()))).StatusCode);
    }
}
