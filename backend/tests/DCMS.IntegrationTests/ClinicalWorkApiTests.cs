using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ClinicalVisitFixturePort : IClinicalVisitPort
{
    public ClinicalVisitState State { get; set; } = new(false, false, 0, 0, 0);
    public Task<ClinicalVisitState> GetAsync(Guid visitId, CancellationToken ct) => Task.FromResult(State);
}

public sealed class ClinicalConsentFixturePort : IClinicalConsentPort
{
    public Task<bool> ValidAsync(int patientId, Guid? itemId, Guid? stepId, int? planVersion,
        int serviceId, CancellationToken ct) => Task.FromResult(true);
}

public sealed class ClinicalWorkFixture : ApiFixture
{
    public ClinicalVisitFixturePort Visit { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClinicalVisitPort>();
            services.AddSingleton<IClinicalVisitPort>(Visit);
            services.RemoveAll<IClinicalConsentPort>();
            services.AddSingleton<IClinicalConsentPort, ClinicalConsentFixturePort>();
        });
    }
}

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
        db.AddRange(new SchedulingMember { BranchId = branch.Id, EmployeeId = dentistId, CanDentist = true },
            new SchedulingMember { BranchId = branch.Id, EmployeeId = assistantId },
            new SchedulingService { BranchId = branch.Id, Name = "SYNTHETIC trám", PublicDurationMinutes = 30 });
        await db.SaveChangesAsync();
        var serviceId = await db.Set<SchedulingService>().Where(x => x.BranchId == branch.Id)
            .Select(x => x.Id).SingleAsync();
        var visitId = Guid.NewGuid();
        fixture.Visit.State = new ClinicalVisitState(true, true, patientId, branch.Id, 1);
        return (patientId, branch.Id, serviceId, visitId);
    }

    [Fact]
    public async Task ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId()
    {
        using var dentist = await Login("dentist.demo");
        using var assistant = await Login("assistant.demo");
        var setup = await SetupAsync(assistant);
        var input = new ActivityInput(setup.VisitId, null, null, null, null, setup.ServiceId,
            "25", "răng", 1, DateTimeOffset.UtcNow, 30, "Đã trám răng 25", null,
            "Separate", null, 300000, null);
        var draftRequest = new CreateActivityDraftRequest(input, 1, Guid.NewGuid());
        var draftResponse = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/activities/drafts", draftRequest);
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<ActivityDto>())!;
        var retry = (await (await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/activities/drafts",
            draftRequest)).Content.ReadFromJsonAsync<ActivityDto>())!;
        Assert.Equal(draft.Id, retry.Id);
        Assert.Equal("Draft", draft.State);
        Assert.Equal(HttpStatusCode.OK, (await dentist.PostAsJsonAsync(
            $"/api/activities/{draft.Id}/post", new PostActivityRequest(draft.Version, 1,
                null, null, Guid.NewGuid()))).StatusCode);

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Equal("Posted", (await db.ClinicalActivities.SingleAsync(x => x.Id == draft.Id)).State);
            // A prior, already-posted activity is a valid source for a review in the current visit.
            var source = await db.ClinicalActivities.SingleAsync(x => x.Id == draft.Id);
            source.State = "Posted"; source.Version = 2; source.PostedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        var reviewRequest = new CreateReviewRequest(setup.VisitId, draft.Id, null,
            "Ê buốt sau điều trị", "Cần theo dõi", "PendingAssessment",
            "FurtherAssessment", null, null, null, null, null, "Tái khám",
            1, 2, Guid.NewGuid());
        var reviewResponse = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/reviews", reviewRequest);
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        var review = (await reviewResponse.Content.ReadFromJsonAsync<ReviewDto>())!;
        var replayReview = (await (await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/reviews",
            reviewRequest)).Content.ReadFromJsonAsync<ReviewDto>())!;
        Assert.Equal(review.Id, replayReview.Id);
        using var check = fixture.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Single(await context.ClinicalActivities.Where(x => x.VisitId == setup.VisitId).ToListAsync());
    }

    [Fact]
    public async Task CoveredCarePostsOnlyAfterExplicitConfirmationAndCareNoteRemainsSeparate()
    {
        using var dentist = await Login("dentist.demo");
        using var assistant = await Login("assistant.demo");
        var setup = await SetupAsync(assistant);
        var sourceId = Guid.NewGuid();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            db.ClinicalActivities.Add(new DCMS.Domain.Features.Treatment.ClinicalActivity
            {
                Id = sourceId, VisitId = Guid.NewGuid(), PatientId = setup.PatientId,
                BranchId = setup.BranchId, ServiceId = setup.ServiceId, Unit = "răng",
                Quantity = 1, PerformedAt = DateTimeOffset.UtcNow.AddDays(-3),
                PerformerId = 1, Outcome = "Điều trị nguồn", ChargingBasis = "Included",
                State = "Posted", CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                PostedAt = DateTimeOffset.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();
        }
        var reviewResponse = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/reviews",
            new CreateReviewRequest(setup.VisitId, sourceId, null, "Ê buốt", "Cần chăm sóc",
                "Covered", "CorrectiveCareNeeded", "Phần trám", "Sâu răng mới",
                "BH-01", "1", "Trong phạm vi phần trám", "Theo dõi", 1, 1, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        var review = (await reviewResponse.Content.ReadFromJsonAsync<ReviewDto>())!;
        var noteRequest = new CreateCareNoteRequest(setup.VisitId, sourceId, review.Id,
            "Hướng dẫn chăm sóc", "Đã tiếp nhận", 1, Guid.NewGuid());
        var note = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/care-notes", noteRequest);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        var noteId = (await note.Content.ReadFromJsonAsync<CareNoteDto>())!.Id;
        var noteRetry = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/care-notes", noteRequest);
        Assert.Equal(noteId, (await noteRetry.Content.ReadFromJsonAsync<CareNoteDto>())!.Id);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Single(await db.ClinicalActivities.Where(x => x.PatientId == setup.PatientId).ToListAsync());
        }
        var input = new ActivityInput(setup.VisitId, null, null, null, null, setup.ServiceId,
            "25", "răng", 1, DateTimeOffset.UtcNow, 20, "Sửa phần trám", "Theo dõi",
            "Free", "Bảo hành phần trám theo BH-01", 0, review.Id);
        var draftResponse = await dentist.PostAsJsonAsync($"/api/visits/{setup.VisitId}/activities/drafts",
            new CreateActivityDraftRequest(input, 1, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = (await draftResponse.Content.ReadFromJsonAsync<ActivityDto>())!;
        var postRequest = new PostActivityRequest(draft.Version, 1, null, null, Guid.NewGuid());
        var post = await dentist.PostAsJsonAsync($"/api/activities/{draft.Id}/post", postRequest);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var posted = (await post.Content.ReadFromJsonAsync<ActivityDto>())!;
        Assert.Equal("Posted", posted.State);
        var postRetry = await dentist.PostAsJsonAsync($"/api/activities/{draft.Id}/post", postRequest);
        Assert.Equal(posted.Id, (await postRetry.Content.ReadFromJsonAsync<ActivityDto>())!.Id);
        using var check = fixture.Services.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(2, await verify.ClinicalActivities.CountAsync(x => x.PatientId == setup.PatientId));
        Assert.Single(await verify.ClinicalCareNotes.Where(x => x.PatientId == setup.PatientId).ToListAsync());
        Assert.Single(await verify.TreatmentProgressionEvents.Where(x =>
            x.StableKey == $"clinical-activity-posted:{draft.Id}").ToListAsync());
    }

    [Fact]
    public async Task CareContactRetryDoesNotCreateAnotherAttemptOrCompleteInstruction()
    {
        using var dentist = await Login("dentist.demo");
        using var assistant = await Login("assistant.demo");
        var setup = await SetupAsync(assistant);
        var create = new CreateCareTaskRequest(setup.PatientId, setup.BranchId, null,
            null, null, "FollowUp", "Gọi hỏi tình trạng sau điều trị", null,
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
