using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Reception;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

// Only antivirus is a stand-in. HTTP endpoints, auth, EF migrations, private filesystem and PostgreSQL are real.
public sealed class ExaminationTestScanner : IClinicalFileScanner
{
    public string State { get; set; } = "PendingScan";
    public Task<string> ScanAsync(byte[] content, CancellationToken ct) => Task.FromResult(State);
}
public sealed class ExaminationFixture : ReceptionFixture
{
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "dcms112-tests-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> { ["ClinicalDocuments:StoragePath"] = StoragePath }));
        builder.ConfigureServices(s => { s.RemoveAll<IClinicalFileScanner>(); s.AddSingleton<ExaminationTestScanner>(); s.AddSingleton<IClinicalFileScanner>(p => p.GetRequiredService<ExaminationTestScanner>()); });
    }
}

public sealed class ExaminationApiTests(ExaminationFixture fixture) : IClassFixture<ExaminationFixture>
{
    private readonly ReceptionApiTests reception = new(fixture);
    private static string Exam(Guid id) => $"/api/visits/{id}/examination";
    private static ExaminationContent Content(bool complete = false) => new("SYNTHETIC đau răng",
        new("Unknown", Verified: complete), new("None", Verified: complete), new("Present", "SYNTHETIC thuốc", complete),
        complete ? [new(16, null, "SYNTHETIC tình trạng")] : [], complete ? "SYNTHETIC chẩn đoán" : null, "SYNTHETIC ghi chú");
    private async Task<(ReceptionApiTests.Setup Setup, VisitResult Visit, HttpClient Dentist)> Prepare(bool start = true)
    {
        fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "PendingScan";
        var setup = await reception.Prepare(); using var assistant = await reception.Login(); var visit = await reception.Check(assistant, setup);
        var dentist = await reception.Login("dentist.demo");
        if (start) { var result = await dentist.PostAsJsonAsync($"/api/visits/{visit.VisitId}/start", new StartExamination(visit.PatientId, visit.VisitVersion, Guid.NewGuid()));
            Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync()); visit = (await result.Content.ReadFromJsonAsync<ExaminationWorkspace>())!.Visit; }
        return (setup, visit, dentist);
    }
    private static async Task<T> Ok<T>(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<T>())!; }
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");
    private DocumentMetadata Metadata(VisitResult v) => new(v.PatientId, v.VisitId, null, null, null, null, "Other", "Clinic",
        DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime), "SYNTHETIC file", Guid.NewGuid());
    private static async Task<HttpResponseMessage> Upload(HttpClient c, DocumentMetadata m, byte[]? bytes = null, string name = "test.pdf", string type = "application/pdf", int count = 1)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(JsonSerializer.Serialize(m, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "metadata");
        for (var i = 0; i < count; i++) { var part = new ByteArrayContent(bytes ?? Pdf); part.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(part, "file", name); }
        return await c.PostAsync("/api/clinical-documents", form);
    }

    [Fact] public async Task QueueStartDraftReopenConfirmAndHandoffUseTheSameVisit()
    {
        var (s,v,c) = await Prepare(false); using (c) {
            var start = new StartExamination(v.PatientId, v.VisitVersion, Guid.NewGuid());
            var started = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", start));
            var replay = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", start)); Assert.True(replay.Replayed);
            Assert.Equal(v.VisitId, started.Visit.VisitId); Assert.Equal("InProgress", started.Visit.Status); Assert.Equal("InProgress", started.Visit.AppointmentStatus);
            var draft = new SaveExamination(v.PatientId, started.Visit.VisitVersion, Guid.NewGuid(), Content(), false);
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), draft));
            var reopened = await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId)); Assert.Equal(Content().Notes, reopened!.Examination!.Content.Notes); Assert.False(reopened.Examination.Confirmed);
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync(Exam(v.VisitId), draft with { ExpectedVersion = saved.Visit.VisitVersion, RequestKey = Guid.NewGuid(), Confirm = true })).StatusCode);
            var confirmed = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), draft with { ExpectedVersion = saved.Visit.VisitVersion, RequestKey = Guid.NewGuid(), Content = Content(true), Confirm = true }));
            var handoff = await c.GetFromJsonAsync<ClinicalHandoff>($"/api/visits/{v.VisitId}/clinical-handoff"); Assert.Equal(v.VisitId, handoff!.VisitId); Assert.Contains(16, handoff.Teeth); Assert.Empty(handoff.Documents);
            var treatment = await c.GetAsync($"/api/visits/{v.VisitId}/treatment-context"); Assert.Equal(HttpStatusCode.OK, treatment.StatusCode);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Equal(0, await db.ClinicalActivities.CountAsync()); Assert.Equal(0, await db.TreatmentDossiers.CountAsync(x => x.PatientId == s.PatientId));
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Exam(v.VisitId), draft with { ExpectedVersion = confirmed.Visit.VisitVersion, RequestKey = Guid.NewGuid() })).StatusCode);
        }
    }
    [Fact] public async Task ConcurrentRetryAndStaleWritesDoNotDuplicateOrOverwrite()
    {
        var (_,v,c) = await Prepare(); using (c) {
            var command = new SaveExamination(v.PatientId, v.VisitVersion, Guid.NewGuid(), Content(), false);
            var results = await Task.WhenAll(c.PutAsJsonAsync(Exam(v.VisitId), command), c.PutAsJsonAsync(Exam(v.VisitId), command));
            foreach (var r in results) await Ok<ExaminationWorkspace>(r);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Exam(v.VisitId), command with { Content = Content(true) })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Exam(v.VisitId), command with { RequestKey = Guid.NewGuid() })).StatusCode);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Equal(1, await db.Set<DCMS.Domain.Features.Visits.VisitAudit>().CountAsync(x => x.VisitId == v.VisitId && x.Action == "SaveExamination"));
        }
    }
    [Fact] public async Task AnotherVisitKeepsPreviousResultsAndOpensExistingDossier()
    {
        var (s,v,c) = await Prepare(); using (c) {
            await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId, v.VisitVersion, Guid.NewGuid(), Content(true), true)));
            var other = await reception.Prepare();
            Guid dossierId;
            using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                var a = await db.Appointments.SingleAsync(x => x.Id == other.AppointmentId); a.PatientId = s.PatientId; a.BranchId = s.BranchId;
                var reservation = await db.Set<ResourceReservation>().SingleAsync(x => x.Id == a.ReservationId); reservation.AttendeeKey = $"patient:{s.PatientId}";
                // Attach a second already-checked-in Visit to the same Patient. Check-in itself is covered through real API in the other cases.
                a.Status = "CheckedIn"; var visit = new DCMS.Domain.Features.Visits.Visit { AppointmentId = a.Id, PatientId = s.PatientId, BranchId = s.BranchId, DentistId = s.DentistId,
                    ArrivalAt = fixture.Clock.Now, RecordedAt = fixture.Clock.Now, RecordedBy = (await db.Employees.SingleAsync(x => x.Id == s.AssistantId)).AccountId };
                db.Add(visit); var dossier = new TreatmentDossier { PatientId = s.PatientId, BranchId = s.BranchId, DentistId = s.DentistId, Code = "SYNTHETIC-OLD", Diagnosis = "SYNTHETIC cũ", StartedOn = DateOnly.FromDateTime(fixture.Clock.Now.DateTime), CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now }; db.Add(dossier);
                await db.SaveChangesAsync(); dossierId = dossier.Id; other = other with { AppointmentId = a.Id };
            }
            using var scope2 = fixture.Services.CreateScope(); var database = scope2.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var second = await database.Visits.AsNoTracking().SingleAsync(x => x.AppointmentId == other.AppointmentId);
            var started = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{second.Id}/start", new StartExamination(v.PatientId, 1, Guid.NewGuid())));
            Assert.Equal("None", started.InitialContent.Allergies.State); Assert.False(started.InitialContent.Allergies.Verified);
            await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(second.Id), new SaveExamination(v.PatientId, started.Visit.VisitVersion, Guid.NewGuid(), Content() with { Notes = "SYNTHETIC second" }, false)));
            var history = await c.GetFromJsonAsync<ExaminationHistory[]>($"/api/visits/{second.Id}/clinical-history"); Assert.Contains(history!, x => x.VisitId == v.VisitId && x.Examination.Content.Notes == Content().Notes);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/dossiers/{dossierId}")).StatusCode);
            Assert.Equal(1, await database.TreatmentDossiers.CountAsync(x => x.PatientId == v.PatientId));
        }
    }
    [Theory] [InlineData("assistant.demo")] [InlineData("admin.demo")] [InlineData("dual.demo")]
    public async Task UnauthorizedActorsCannotStartOrSave(string name)
    {
        var (_,v,c) = await Prepare(false); c.Dispose(); using var actor = await reception.Login(name);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", new StartExamination(v.PatientId, v.VisitVersion, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId, v.VisitVersion, Guid.NewGuid(), Content(), false))).StatusCode);
    }
    [Fact] public async Task WrongPatientAndForgedDocumentSourcesAreRejected()
    {
        var (_,v,c) = await Prepare(); using(c) {
            Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId + 1000, v.VisitVersion, Guid.NewGuid(), Content(), false))).StatusCode);
            foreach (var m in new[] { Metadata(v) with { PatientId = v.PatientId + 1000 }, Metadata(v) with { DossierId = Guid.NewGuid() }, Metadata(v) with { PlanId = Guid.NewGuid() } })
                Assert.Equal(HttpStatusCode.NotFound, (await Upload(c,m)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Metadata(v) with { InvestigationId = Guid.NewGuid() })).StatusCode);
        }
    }
    [Theory] [InlineData("PendingScan")] [InlineData("Rejected")]
    public async Task NonReadyFilesCannotServeContentOrBeReviewed(string state)
    {
        var (_,v,c) = await Prepare(); using(c) {
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State = state;
            var result = await Ok<UploadBatchResult>(await Upload(c,Metadata(v))); var d = result.Document!; Assert.Equal(state,d.FileState); Assert.Empty(d.Reviews); Assert.Empty(d.AllowedActions);
            Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"/api/clinical-documents/{d.DocumentId}/content")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/clinical-documents/{d.DocumentId}/review",new ReviewDocument(v.PatientId,v.VisitId,d.Version,Guid.NewGuid()))).StatusCode);
        }
    }
    [Fact] public async Task ReadyFileRetryDownloadAndClinicalReviewAreSeparateAndRevocationApplies()
    {
        var (s,v,c) = await Prepare(); using(c) {
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "Ready"; var m = Metadata(v);
            var d = (await Ok<UploadBatchResult>(await Upload(c,m))).Document!; var again = (await Ok<UploadBatchResult>(await Upload(c,m))).Document!;
            Assert.Equal(d.DocumentId, again.DocumentId); Assert.Empty(d.Reviews);
            Assert.Equal(Pdf, await c.GetByteArrayAsync($"/api/clinical-documents/{d.DocumentId}/content"));
            Assert.Empty((await c.GetFromJsonAsync<DocumentDto>($"/api/clinical-documents/{d.DocumentId}"))!.Reviews);
            using var assistant = await reception.Login(); Assert.Equal(HttpStatusCode.Forbidden, (await assistant.PostAsJsonAsync($"/api/clinical-documents/{d.DocumentId}/review", new ReviewDocument(v.PatientId,v.VisitId,d.Version,Guid.NewGuid()))).StatusCode);
            var review = new ReviewDocument(v.PatientId,v.VisitId,d.Version,Guid.NewGuid());
            var reviewed = await Ok<DocumentDto>(await c.PostAsJsonAsync($"/api/clinical-documents/{d.DocumentId}/review",review)); Assert.Single(reviewed.Reviews);
            Assert.Single((await Ok<DocumentDto>(await c.PostAsJsonAsync($"/api/clinical-documents/{d.DocumentId}/review",review))).Reviews);
            Assert.Equal(HttpStatusCode.Conflict, (await Upload(c,m with { Description = "changed" })).StatusCode);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.Set<SchedulingMember>().SingleAsync(x => x.BranchId == s.BranchId && x.EmployeeId == s.DentistId)).Active = false; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/clinical-documents/{d.DocumentId}/content")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Upload(c,m)).StatusCode);
        }
    }
    [Fact] public async Task FileSignatureSizeBatchLimitAndMixedBatchAreValidatedOnServer()
    {
        var (_,v,c) = await Prepare(); using(c) {
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c,Metadata(v),Encoding.ASCII.GetBytes("fake jpeg"),"fake.jpg","image/jpeg")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c,Metadata(v),count:11)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c,Metadata(v),new byte[20*1024*1024+1])).StatusCode);
            Assert.Equal(10,(await Ok<UploadBatchResult>(await Upload(c,Metadata(v),count:10))).Documents.Count);
            using var scope = fixture.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IClinicalDocumentService>();
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var actor = await db.Employees.Where(x => x.Id == v.DentistId).Select(x => x.AccountId).SingleAsync();
            var mixed = await service.UploadAsync(actor,Metadata(v),[new("good.pdf","application/pdf",Pdf),new("fake.png","image/png",[1,2,3])],default);
            Assert.NotNull(mixed.Documents[0].Document); Assert.NotNull(mixed.Documents[1].Error);
        }
    }
    [Fact] public async Task DraftAllowsIncompleteToothAndMedicalDetailsButConfirmationAndInvalidFdiAreRejected()
    {
        var (_,v,c) = await Prepare(); using(c) {
            var content = Content() with { Medications = new("Present"), Findings = [new(55,null,"")] };
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false)));
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),content,true))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),content with { Findings = [new(59,null,"invalid")] },false))).StatusCode);
        }
    }
    [Fact] public async Task ScanWorkerRechecksHashAndNeverMarksUploadAsClinicalReview()
    {
        var (_,v,c) = await Prepare(); using(c) {
            var d = (await Ok<UploadBatchResult>(await Upload(c,Metadata(v)))).Document!;
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "Ready";
            using(var scope = fixture.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IClinicalDocumentService>().ScanPendingAsync(default);
            var ready = await c.GetFromJsonAsync<DocumentDto>($"/api/clinical-documents/{d.DocumentId}"); Assert.Equal("Ready",ready!.FileState); Assert.Empty(ready.Reviews);
            await File.WriteAllTextAsync(Path.Combine(fixture.StoragePath,d.DocumentId.ToString("N")),"SYNTHETIC tampered");
            Assert.Equal(HttpStatusCode.Conflict,(await c.GetAsync($"/api/clinical-documents/{d.DocumentId}/content")).StatusCode);
        }
    }
    [Fact] public async Task DirectDocumentRequestsRequireCurrentBranchAndRoleEvenWithKnownIds()
    {
        var (_,v,c) = await Prepare(); using(c) {
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "Ready";
            var d = (await Ok<UploadBatchResult>(await Upload(c,Metadata(v)))).Document!;
            foreach (var actor in new[] {"admin.demo","guest.demo","owner.demo"}) {
                using var denied = await reception.Login(actor);
                foreach(var path in new[] {$"/api/clinical-documents/{d.DocumentId}",$"/api/clinical-documents/{d.DocumentId}/content",$"/api/clinical-documents/{d.DocumentId}/history",$"/api/clinical-documents?patientId={v.PatientId}&visitId={v.VisitId}"})
                    Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync(path)).StatusCode);
            }
        }
    }
    [Fact] public async Task CancelledVisitAndMissingStartCannotBeWrittenAndUnauthenticatedCallsAreDenied()
    {
        var (_,v,c) = await Prepare(false); using(c) {
            Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(),false))).StatusCode);
            using(var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                (await db.Visits.SingleAsync(x=>x.Id==v.VisitId)).Status="Cancelled"; await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/start",new StartExamination(v.PatientId,v.VisitVersion,Guid.NewGuid()))).StatusCode);
            using var anonymous=fixture.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(Exam(v.VisitId))).StatusCode);
        }
    }
}
