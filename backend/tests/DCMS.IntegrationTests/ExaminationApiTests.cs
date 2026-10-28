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
        complete ? [new(16, null, "SYNTHETIC tình trạng")] : [], complete ? "SYNTHETIC chẩn đoán" : null, "SYNTHETIC ghi chú",
        Conclusion: complete ? "SYNTHETIC kết luận" : null, Advice: complete ? "SYNTHETIC dặn dò" : null, Disposition: complete ? "ConsultationOnly" : null);
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

    private static string Inv(Guid visitId, Guid? orderId = null, string? action = null) => $"/api/visits/{visitId}/investigations" + (orderId.HasValue ? $"/{orderId}/{action}" : "");
    private static CreateInvestigation Order(VisitResult v, bool required = true) => new(v.PatientId,v.VisitVersion,Guid.NewGuid(),"XRay","SYNTHETIC phim cận chóp","31–33","SYNTHETIC đánh giá xương",required);
    [Fact] public async Task InvestigationWaitReceiveReadAndConfirmAreSeparateIdempotentActions()
    {
        var (_,v,c)=await Prepare();using(c) {
            var create=Order(v);var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),create));var order=Assert.Single(created.Items);
            Assert.Equal("Ordered",order.State);Assert.Equal(v.VisitVersion+1,created.VisitVersion);
            var draft=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,created.VisitVersion,Guid.NewGuid(),Content(),false)));
            Assert.Single(draft.Investigations!);
            var replay=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),create));Assert.Single(replay.Items);Assert.Equal(draft.Visit.VisitVersion,replay.VisitVersion);
            var blocked=await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,replay.VisitVersion,Guid.NewGuid(),Content(true),true));Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
            Assert.Equal("investigations",(await blocked.Content.ReadFromJsonAsync<DCMS.Application.Common.SystemMessage>())!.Field);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,replay.VisitVersion,order.Version,Guid.NewGuid()))).StatusCode);
            using var assistant=await reception.Login();
            var receive=new ReceiveInvestigationResult(v.PatientId,replay.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC kết quả văn bản");
            Assert.Equal(HttpStatusCode.Conflict,(await assistant.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),receive with {ExpectedVisitVersion=v.VisitVersion})).StatusCode);
            var result=await Ok<InvestigationPage>(await assistant.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),receive));order=Assert.Single(result.Items);Assert.Equal("ResultAvailable",order.State);
            Assert.Equal(result.VisitVersion,(await Ok<InvestigationPage>(await assistant.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),receive))).VisitVersion);
            Assert.Equal(HttpStatusCode.Forbidden,(await assistant.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,result.VisitVersion,order.Version,Guid.NewGuid()))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,result.VisitVersion,Guid.NewGuid(),Content(true),true))).StatusCode);
            var review=new ReviewInvestigation(v.PatientId,result.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC nhận định");
            var read=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),review));Assert.Equal("Reviewed",Assert.Single(read.Items).State);
            Assert.Equal(read.VisitVersion,(await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),review))).VisitVersion);
            var final=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,read.VisitVersion,Guid.NewGuid(),Content(true),true)));Assert.True(final.Examination!.Confirmed);
            Assert.False((await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!.CanOrder);
            using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Equal(1,await db.Set<DCMS.Domain.Features.Visits.VisitAudit>().CountAsync(x=>x.VisitId==v.VisitId && x.Action=="CreateInvestigation"));
            Assert.Equal(1,await db.Set<DCMS.Domain.Features.Visits.VisitAudit>().CountAsync(x=>x.VisitId==v.VisitId && x.Action=="ReviewInvestigation"));
        }
    }
    [Fact] public async Task ConfirmedOpenExaminationAcceptsAnAuditedSupplementalInvestigationAndBlocksPendingHandoff()
    {
        var (_,v,c)=await Prepare();using(c){
            var confirmed=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true),true)));
            var request=Order(confirmed.Visit) with {ExpectedExaminationVersion=confirmed.Examination!.Version,AmendmentReason="SYNTHETIC cần đánh giá bổ sung"};
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Inv(v.VisitId),request with {AmendmentReason=" "})).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Inv(v.VisitId),request with {ExpectedExaminationVersion=request.ExpectedExaminationVersion-1})).StatusCode);
            using var assistant=await reception.Login();Assert.Equal(HttpStatusCode.Forbidden,(await assistant.PostAsJsonAsync(Inv(v.VisitId),request)).StatusCode);
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),request));var order=Assert.Single(created.Items);
            Assert.Single((await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),request))).Items);
            var unchanged=(await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId)))!;Assert.True(unchanged.Examination!.Confirmed);Assert.Equal(confirmed.Examination.Version,unchanged.Examination.Version);Assert.Equal(confirmed.Examination.Content.Diagnosis,unchanged.Examination.Content.Diagnosis);Assert.DoesNotContain("handoff",unchanged.AllowedActions);
            Assert.Equal(HttpStatusCode.Conflict,(await c.GetAsync($"/api/visits/{v.VisitId}/clinical-handoff")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,created.VisitVersion,Guid.NewGuid(),Content(true),request.AmendmentReason!))).StatusCode);
            var received=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),new ReceiveInvestigationResult(v.PatientId,created.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC báo cáo bổ sung")));order=Assert.Single(received.Items);
            var reviewed=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,received.VisitVersion,order.Version,Guid.NewGuid())));
            var amended=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,reviewed.VisitVersion,Guid.NewGuid(),Content(true) with {Notes="SYNTHETIC cập nhật"},request.AmendmentReason!)));
            Assert.Equal(confirmed.Examination.Version+1,amended.Examination!.Version);
            using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();var audit=await db.Set<DCMS.Domain.Features.Visits.VisitAudit>().SingleAsync(x=>x.VisitId==v.VisitId&&x.Action=="CreateInvestigation");
            using var json=JsonDocument.Parse(audit.AfterJson);Assert.Equal(request.AmendmentReason,json.RootElement.GetProperty("amendmentReason").GetString());Assert.Equal(confirmed.Examination.Version,json.RootElement.GetProperty("sourceExaminationVersion").GetInt32());
        }
    }

    [Fact] public async Task OptionalInvestigationCanWaitAfterConfirmationButNotAfterClosingVisit()
    {
        var (_,v,c)=await Prepare();using(c) {
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),Order(v,false)));var order=Assert.Single(created.Items);
            var final=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,created.VisitVersion,Guid.NewGuid(),Content(true),true)));
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,final.Visit.VisitVersion,Guid.NewGuid(),true))).StatusCode);
            var cancel=new CancelInvestigation(v.PatientId,final.Visit.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC không còn cần");
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"cancel"),cancel with {Reason=" "})).StatusCode);
            var cancelled=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"cancel"),cancel));Assert.Equal("Cancelled",Assert.Single(cancelled.Items).State);
            Assert.Equal(cancelled.VisitVersion,(await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"cancel"),cancel))).VisitVersion);
            var closed=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,cancelled.VisitVersion,Guid.NewGuid(),true)));
            Assert.Equal("Completed",closed.Visit.Status);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Inv(v.VisitId),Order(closed.Visit))).StatusCode);
        }
    }
    [Theory] [InlineData("Ready")] [InlineData("PendingScan")] [InlineData("Rejected")]
    public async Task InvestigationFilesUseOnlyReadyEvidenceAndKeepUploadReplayStable(string state)
    {
        var (_,v,c)=await Prepare();using(c) {
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),Order(v)));var order=Assert.Single(created.Items);
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State=state;
            var metadata=Metadata(v) with {InvestigationId=order.Id};var uploaded=await Ok<UploadBatchResult>(await Upload(c,metadata));var file=uploaded.Document!;
            Assert.Equal(order.Id,file.Context.InvestigationId);
            var latest=(await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!;Assert.Equal(created.VisitVersion+1,latest.VisitVersion);order=Assert.Single(latest.Items);
            Assert.Equal(file.DocumentId,(await Ok<UploadBatchResult>(await Upload(c,metadata))).Document!.DocumentId);
            Assert.Equal(latest.VisitVersion,(await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!.VisitVersion);
            var received=await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),new ReceiveInvestigationResult(v.PatientId,latest.VisitVersion,order.Version,Guid.NewGuid(),null));
            if(state!="Ready") {
                Assert.Equal(HttpStatusCode.BadRequest,received.StatusCode);Assert.Empty(order.ResultDocumentIds);
                var textResult=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),new ReceiveInvestigationResult(v.PatientId,latest.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC báo cáo văn bản")));
                var textOrder=Assert.Single(textResult.Items);Assert.Empty(textOrder.ResultDocumentIds);
                var readText=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,textResult.VisitVersion,textOrder.Version,Guid.NewGuid())));
                Assert.Empty(Assert.Single(readText.Items).Documents[0].Reviews!);return;
            }
            var result=await Ok<InvestigationPage>(received);order=Assert.Single(result.Items);Assert.Equal(new[]{file.DocumentId},order.ResultDocumentIds);
            Assert.Equal(HttpStatusCode.Conflict,(await Upload(c,metadata with {RequestKey=Guid.NewGuid()})).StatusCode);
            var reviewed=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,result.VisitVersion,order.Version,Guid.NewGuid())));
            Assert.Equal("Reviewed",Assert.Single(reviewed.Items).State);Assert.Single(Assert.Single(reviewed.Items).Documents[0].Reviews!);
        }
    }
    [Theory] [InlineData("Imaging", "MRI khớp thái dương hàm")] [InlineData("Pathology", "Mô bệnh học mẫu sinh thiết")]
    public async Task InvestigationSupportsAdditionalTechniques(string kind, string name)
    {
        var (_,v,c)=await Prepare();using(c){
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),Order(v) with {Kind=kind,Name=name}));
            var item=Assert.Single(created.Items);Assert.Equal(kind,item.Kind);Assert.Equal(name,item.Name);
            var stored=(await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!;
            Assert.Equal(kind,Assert.Single(stored.Items).Kind);Assert.Equal(name,Assert.Single(stored.Items).Name);
        }
    }

    [Fact] public async Task InvestigationReviewRechecksSnapshotFileState()
    {
        var (_,v,c)=await Prepare();using(c) {
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),Order(v)));var order=Assert.Single(created.Items);
            fixture.Services.GetRequiredService<ExaminationTestScanner>().State="Ready";
            var file=(await Ok<UploadBatchResult>(await Upload(c,Metadata(v) with {InvestigationId=order.Id}))).Document!;
            var latest=(await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!;order=Assert.Single(latest.Items);
            var result=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),new ReceiveInvestigationResult(v.PatientId,latest.VisitVersion,order.Version,Guid.NewGuid(),null)));order=Assert.Single(result.Items);
            using(var scope=fixture.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();var row=await db.Set<DCMS.Domain.Features.Examinations.ClinicalDocument>().SingleAsync(x=>x.Id==file.DocumentId);row.FileState="Rejected";await db.SaveChangesAsync();}
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"review"),new ReviewInvestigation(v.PatientId,result.VisitVersion,order.Version,Guid.NewGuid()))).StatusCode);
        }
    }
    [Theory] [InlineData("assistant.demo")] [InlineData("admin.demo")] [InlineData("dual.demo")]
    public async Task OnlyAssignedDentistCreatesInvestigations(string actor)
    {
        var (_,v,c)=await Prepare();c.Dispose();using var other=await reception.Login(actor);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync(Inv(v.VisitId),Order(v))).StatusCode);
    }
    [Fact] public async Task InvestigationCannotLinkAnotherVisitOrPatientAndCannotReuseAKeyForDifferentContent()
    {
        var (_,v,c)=await Prepare();using(c) {
            var create=Order(v);var page=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),create));var order=Assert.Single(page.Items);
            Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Inv(v.VisitId),create with {Name="SYNTHETIC khác"})).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync(Inv(v.VisitId),create with {PatientId=v.PatientId+10000,RequestKey=Guid.NewGuid()})).StatusCode);
            var (_,other,otherClient)=await Prepare();using(otherClient) {
                Assert.Equal(HttpStatusCode.NotFound,(await Upload(otherClient,Metadata(other) with {InvestigationId=order.Id})).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,(await otherClient.PostAsJsonAsync(Inv(other.VisitId,order.Id,"result"),new ReceiveInvestigationResult(other.PatientId,other.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC"))).StatusCode);
            }
        }
    }
    [Fact] public async Task ConcurrentReceiveAndCancelCannotBothCommit()
    {
        var (_,v,c)=await Prepare();using(c) {
            var created=await Ok<InvestigationPage>(await c.PostAsJsonAsync(Inv(v.VisitId),Order(v)));var order=Assert.Single(created.Items);
            var responses=await Task.WhenAll(c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"result"),new ReceiveInvestigationResult(v.PatientId,created.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC kết quả")),
                c.PostAsJsonAsync(Inv(v.VisitId,order.Id,"cancel"),new CancelInvestigation(v.PatientId,created.VisitVersion,order.Version,Guid.NewGuid(),"SYNTHETIC hủy")));
            Assert.Single(responses,x=>x.IsSuccessStatusCode);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
            var current=(await c.GetFromJsonAsync<InvestigationPage>(Inv(v.VisitId)))!;Assert.Equal(created.VisitVersion+1,current.VisitVersion);Assert.Equal(order.Version+1,Assert.Single(current.Items).Version);
        }
    }

    [Fact] public async Task MultipleDiagnosesAreTheFinalConclusionAcrossDraftConfirmationAndAmendment()
    {
        var (_,v,c)=await Prepare(); using(c) {
            var content=Content(true) with { Diagnoses=[" Sâu răng ","Viêm lợi, nướu"],Diagnosis="SYNTHETIC chuỗi cũ",Conclusion="SYNTHETIC kết luận cũ" };
            var draft=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false)));
            var reopened=await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(new[]{"Sâu răng","Viêm lợi, nướu"},reopened!.Examination!.Content.Diagnoses);
            Assert.Equal("Sâu răng; Viêm lợi, nướu",reopened.Examination.Content.Diagnosis); Assert.Equal(reopened.Examination.Content.Diagnosis,reopened.Examination.Content.Conclusion);
            var confirmed=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,draft.Visit.VisitVersion,Guid.NewGuid(),content,true)));
            var amended=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,confirmed.Visit.VisitVersion,Guid.NewGuid(),content with {Diagnoses=["Sâu răng","Viêm nha chu"]},"SYNTHETIC sửa chẩn đoán")));
            var versions=await c.GetFromJsonAsync<ExaminationVersion[]>(Exam(v.VisitId)+"/versions");
            Assert.Equal(new[]{"Sâu răng","Viêm nha chu"},versions![0].Content.Diagnoses);
            Assert.Equal(new[]{"Sâu răng","Viêm lợi, nướu"},versions[1].Content.Diagnoses);
            var closed=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,amended.Visit.VisitVersion,Guid.NewGuid(),true)));
            Assert.Equal("Completed",closed.Visit.Status);
        }
    }
    [Fact] public async Task ClearingDiagnosesDoesNotRestoreLegacyTextAndBlankConfirmationDoesNotWrite()
    {
        var (_,v,c)=await Prepare(); using(c) {
            var content=Content(true) with { Diagnoses=[] };
            var draft=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false)));
            Assert.Empty(draft.Examination!.Content.Diagnoses!);Assert.Equal("",draft.Examination.Content.Diagnosis);Assert.Equal("",draft.Examination.Content.Conclusion);
            var response=await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,draft.Visit.VisitVersion,Guid.NewGuid(),content,true));
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            Assert.Equal("diagnoses",(await response.Content.ReadFromJsonAsync<DCMS.Application.Common.SystemMessage>())!.Field);
            var reopened=await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));Assert.Equal(draft.Visit.VisitVersion,reopened!.Visit.VisitVersion);
        }
    }
    [Fact] public async Task ScopedDiagnosesAggregateOnceAndKeepLocationsThroughAmendmentAndHandoff()
    {
        var (_,v,c)=await Prepare();using(c){
            var input=Content(true) with {Diagnosis=null,Conclusion=null,Diagnoses=[],UnscopedDiagnoses=[],Findings=[new(16,null,"",["O"],[" Sâu răng "]),new(26,null,"",null,["sâu răng"]),new(null,"Toàn hàm","",null,["Viêm lợi, nướu"])]};
            var saved=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),input,true)));
            var content=saved.Examination!.Content;Assert.Equal(new[]{"Sâu răng","Viêm lợi, nướu"},content.Diagnoses);Assert.Empty(content.UnscopedDiagnoses!);Assert.Equal("Sâu răng; Viêm lợi, nướu",content.Diagnosis);Assert.Equal(content.Diagnosis,content.Conclusion);
            var handoff=await Ok<ClinicalHandoff>(await c.GetAsync($"/api/visits/{v.VisitId}/clinical-handoff"));Assert.Equal(new[]{16,26},handoff.Teeth.Order().ToArray());
            var edited=content with {Findings=[content.Findings[2]],UnscopedDiagnoses=[]};
            var amended=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),edited,"SYNTHETIC sửa nguồn")));
            Assert.Equal(new[]{"Viêm lợi, nướu"},amended.Examination!.Content.Diagnoses);
            var versions=await c.GetFromJsonAsync<ExaminationVersion[]>(Exam(v.VisitId)+"/versions");Assert.Contains(versions!,x=>x.Content.Findings.Any(f=>f.Tooth==16 && f.Diagnoses!.Contains("Sâu răng")));
        }
    }
    [Fact] public async Task RemovingScopedDiagnosisDoesNotReviveAggregateAliases()
    {
        var (_,v,c)=await Prepare();using(c){
            var input=Content(true) with {Diagnoses=[],UnscopedDiagnoses=[],Findings=[new(16,null,"",null,["Sâu răng"])]};
            var saved=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),input,false)));
            var removed=saved.Examination!.Content with {Findings=[new(16,null,"SYNTHETIC chỉ còn phát hiện")]};
            var draft=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),removed,false)));
            Assert.Empty(draft.Examination!.Content.Diagnoses!);Assert.Equal("",draft.Examination.Content.Diagnosis);
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,draft.Visit.VisitVersion,Guid.NewGuid(),draft.Examination.Content,true))).StatusCode);
        }
    }
    [Fact] public async Task ExplicitNormalExaminationNeedsNoArtificialFindingOrTooth()
    {
        var (_,v,c)=await Prepare();using(c){
            var input=Content(true) with {Findings=[],Diagnoses=[],UnscopedDiagnoses=["Chưa phát hiện bất thường"]};
            var saved=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),input,true)));
            Assert.Empty(saved.Examination!.Content.Findings);Assert.Equal(new[]{"Chưa phát hiện bất thường"},saved.Examination.Content.Diagnoses);
            await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),true)));
        }
    }
    [Theory] [InlineData("null")] [InlineData("duplicate")] [InlineData("many")] [InlineData("combined")]
    public async Task InvalidScopedDiagnosisSourcesDoNotWrite(string scenario)
    {
        var (_,v,c)=await Prepare();using(c){
            string[] values=scenario switch {"null"=>[null!],"duplicate"=>["Sâu răng"," sâu răng "],"many"=>Enumerable.Range(0,31).Select(i=>"SYNTHETIC "+i).ToArray(),_=>Enumerable.Range(0,30).Select(i=>"SYNTHETIC "+i).ToArray()};
            var input=Content(true) with {Diagnoses=[],UnscopedDiagnoses=scenario=="combined"?["SYNTHETIC thêm"]:[],Findings=[new(16,null,"",null,values)]};
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),input,false))).StatusCode);
            Assert.Null((await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId)))!.Examination);
        }
    }

    [Fact] public async Task LegacyDiagnosisCanConcludeWithoutASecondRequiredConclusion()
    {
        var (_,v,c)=await Prepare();using(c) {
            var content=Content(true) with {Conclusion=null};
            var confirmed=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,true)));
            Assert.Null(confirmed.Examination!.Content.Diagnoses);Assert.Equal(content.Diagnosis,confirmed.Examination.Content.Conclusion);
        }
    }
    [Theory] [InlineData("blank")] [InlineData("null")] [InlineData("duplicate")] [InlineData("many")] [InlineData("long")] [InlineData("aggregate")]
    public async Task InvalidDiagnosisListsDoNotWrite(string scenario)
    {
        var (_,v,c)=await Prepare();using(c) {
            string[] diagnoses=scenario switch {
                "blank"=>[" "],"null"=>[null!],"duplicate"=>[" Sâu răng ","sâu răng"],
                "many"=>Enumerable.Range(0,31).Select(i=>"SYNTHETIC "+i).ToArray(),"long"=>[new string('x',4001)],
                _=>Enumerable.Range(0,30).Select(i=>new string('x',145)+i).ToArray()
            };
            var response=await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true) with {Diagnoses=diagnoses},false));
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            var reopened=await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));Assert.Null(reopened!.Examination);Assert.Equal(v.VisitVersion,reopened.Visit.VisitVersion);
        }
    }

    [Fact] public async Task ToothSurfacesPersistInDraftConfirmedAndAmendedSnapshots()
    {
        var (_,v,c) = await Prepare(); using(c) {
            var content = Content(true) with { Findings = [new(16,null,"SYNTHETIC sâu răng",["O","M"]),new(null,"Toàn hàm","SYNTHETIC kết quả tổng quát")] };
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false)));
            var reopened = await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(new[]{"O","M"},reopened!.Examination!.Content.Findings[0].Surfaces);
            var confirmed = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),content,true)));
            var amendedContent = content with { Findings = [new(16,null,"SYNTHETIC bổ sung",["B"])] };
            await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,confirmed.Visit.VisitVersion,Guid.NewGuid(),amendedContent,"SYNTHETIC bổ sung bề mặt")));
            var versions = await c.GetFromJsonAsync<ExaminationVersion[]>(Exam(v.VisitId)+"/versions");
            Assert.Equal(new[]{"B"},versions![0].Content.Findings[0].Surfaces);
            Assert.All(versions.Skip(1),version => Assert.Equal(new[]{"O","M"},version.Content.Findings[0].Surfaces));
        }
    }
    [Theory] [InlineData("unknown")] [InlineData("duplicate")] [InlineData("region")] [InlineData("tooMany")]
    public async Task InvalidToothSurfacesDoNotMutateExamination(string scenario)
    {
        var (_,v,c) = await Prepare(); using(c) {
            string[] surfaces = scenario switch { "unknown" => ["X"], "duplicate" => ["O","O"], "tooMany" => ["O","B","L","M","D","O"], _ => ["O"] };
            var content = Content(true) with { Findings = [new(scenario == "region" ? null : 16,"Toàn hàm","SYNTHETIC kết quả",surfaces)] };
            var response = await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false));
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            var reopened = await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(v.VisitVersion,reopened!.Visit.VisitVersion); Assert.Null(reopened.Examination);
        }
    }

    [Theory] [InlineData(-1, 26, "MALE")] [InlineData(0, 26, "FEMALE")] [InlineData(1, 25, "OTHER")]
    public async Task HeaderUsesVisitPatientProfileAndCompletedYearsAtClinicDate(int birthdayOffset, int expectedAge, string gender)
    {
        var (_,v,c) = await Prepare(false); using(c) {
            var today = DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime);
            var birth = today.AddYears(-26).AddDays(birthdayOffset);
            using(var scope = fixture.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                var p = await db.Patients.SingleAsync(x => x.Id == v.PatientId); p.BirthDate = birth; p.Gender = gender;
                var a = await db.Appointments.SingleAsync(x => x.Id == v.AppointmentId); a.AttendeeBirthDate = today.AddYears(-10);
                await db.SaveChangesAsync();
            }
            var w = await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(v.PatientId, w!.Patient.Id); Assert.Equal(v.PatientName, w.Patient.FullName);
            Assert.Equal(v.PatientCode, w.Patient.PatientCode); Assert.Equal(birth, w.Patient.BirthDate);
            Assert.Equal(gender, w.Patient.Gender); Assert.Equal(expectedAge, w.Patient.AgeYears);
            var started = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", new StartExamination(v.PatientId,v.VisitVersion,Guid.NewGuid())));
            Assert.Equal(w.Patient, started.Patient);
        }
    }
    [Theory] [InlineData("missing", null)] [InlineData("future", null)] [InlineData("infant", 0)]
    public async Task MissingDemographicsAndInfantAgeRemainExplicitAndDoNotBlockExamination(string scenario, int? expectedAge)
    {
        var (_,v,c) = await Prepare(false); using(c) {
            var today = DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime);
            using(var scope = fixture.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); var p = await db.Patients.SingleAsync(x => x.Id == v.PatientId);
                p.BirthDate = scenario switch { "infant" => today.AddMonths(-2), "future" => today.AddDays(1), _ => null }; p.Gender = null; await db.SaveChangesAsync();
            }
            var w = await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(expectedAge, w!.Patient.AgeYears); Assert.Null(w.Patient.Gender);
            var started = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/start", new StartExamination(v.PatientId,v.VisitVersion,Guid.NewGuid())));
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId,started.Visit.VisitVersion,Guid.NewGuid(),Content(),false)));
            Assert.Equal(expectedAge, saved.Patient.AgeYears); Assert.Equal(v.PatientId, saved.Patient.Id);
        }
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
            // Native Vinh context is readable for an active Visit; reading it creates no treatment data.
            var treatment = await c.GetAsync($"/api/visits/{v.VisitId}/treatment-context"); Assert.Equal(HttpStatusCode.OK, treatment.StatusCode);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.Equal(0, await db.ClinicalActivities.CountAsync(x => x.VisitId == v.VisitId)); Assert.Equal(0, await db.TreatmentDossiers.CountAsync(x => x.PatientId == s.PatientId));
            Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync(Exam(v.VisitId), draft with { ExpectedVersion = confirmed.Visit.VisitVersion, RequestKey = Guid.NewGuid() })).StatusCode);
        }
    }
    [Fact] public async Task CompletedExaminationKeepsVisitOpenUntilExplicitCompletionAndAmendmentsPreserveVersions()
    {
        var (_,v,c) = await Prepare(); using(c) {
            var close = new CompleteExaminationVisit(v.PatientId, v.VisitVersion, Guid.NewGuid(), true);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", close)).StatusCode);
            var confirmed = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true),true)));
            Assert.Equal("InProgress", confirmed.Visit.Status); Assert.Equal("InProgress", confirmed.Visit.AppointmentStatus);
            Assert.Equal(1, confirmed.Examination!.Version); Assert.Equal(v.VisitId, confirmed.Examination.Id);
            var list = await c.GetFromJsonAsync<PatientExamination[]>($"/api/patients/{v.PatientId}/examinations");
            Assert.Contains(list!, x => x.VisitId == v.VisitId && x.Examination!.Version == 1);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", close)).StatusCode);
            close = close with { ExpectedVersion = confirmed.Visit.VisitVersion };
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", close with { WorkReviewed = false })).StatusCode);
            var closed = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete", close));
            Assert.Equal("Completed", closed.Visit.Status); Assert.Equal("Completed", closed.Visit.AppointmentStatus);
            Assert.True((await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",close))).Replayed);
            Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"/api/visits/{v.VisitId}/clinical-handoff")).StatusCode);
            var amendment = new AmendExamination(v.PatientId,closed.Visit.VisitVersion,Guid.NewGuid(),Content(true) with { Conclusion = "SYNTHETIC đính chính" },"SYNTHETIC bổ sung kết luận");
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",amendment with { Reason = " " })).StatusCode);
            var amended = await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",amendment));
            Assert.Equal("Completed", amended.Visit.Status); Assert.Equal(2,amended.Examination!.Version);
            Assert.True((await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",amendment))).Replayed);
            var versions = await c.GetFromJsonAsync<ExaminationVersion[]>(Exam(v.VisitId)+"/versions");
            Assert.Equal(2,versions!.Length); Assert.Equal("SYNTHETIC đính chính",versions[0].Content.Conclusion);
            Assert.Equal(Content(true).Conclusion,versions[1].Content.Conclusion); Assert.Equal(amendment.Reason,versions[0].Reason);
            using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.False(await db.ClinicalActivities.AnyAsync(x => x.VisitId == v.VisitId));
            Assert.False(await db.TreatmentDossiers.AnyAsync(x => x.PatientId == v.PatientId));
            Assert.Equal(1,await db.Set<DCMS.Domain.Features.Visits.VisitAudit>().CountAsync(x=>x.VisitId==v.VisitId && x.Action=="CompleteVisit"));
        }
    }
    [Theory] [InlineData("FollowUp")] [InlineData("Referral")]
    public async Task CompletionRequiresDispositionDetails(string disposition)
    {
        var (_,v,c) = await Prepare(); using(c) {
            var command = new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true) with { Disposition = disposition },true);
            Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Exam(v.VisitId),command)).StatusCode);
            command = command with { Content = command.Content with { ReturnNeed="SYNTHETIC tái khám",ReferralDestination="SYNTHETIC chuyển chuyên khoa" } };
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),command));
            Assert.True(saved.Examination!.Confirmed);
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task VisitClosingBlocksDraftWorkButDoesNotRequirePromisedTreatmentToBePerformed(bool draftWork)
    {
        var (_,v,c) = await Prepare(); using(c) {
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),
                Content(true) with { Disposition = draftWork ? "ConsultationOnly" : "ProceedToTreatment" },true)));
            if(draftWork) { using var scope=fixture.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                db.ClinicalActivities.Add(new ClinicalActivity { VisitId=v.VisitId,PatientId=v.PatientId,BranchId=v.BranchId,State="Draft",Quantity=1,PerformedAt=fixture.Clock.Now,CreatedAt=fixture.Clock.Now });
                await db.SaveChangesAsync(); }
            var complete=new CompleteExaminationVisit(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),true);
            var response=await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",complete);
            if(draftWork){Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.Equal("InProgress",(await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId)))!.Visit.Status);}
            else {
                var closed=await Ok<ExaminationWorkspace>(response);Assert.Equal("Completed",closed.Visit.Status);Assert.Contains("handoff",closed.AllowedActions);
                var handoff=await Ok<ClinicalHandoff>(await c.GetAsync($"/api/visits/{v.VisitId}/clinical-handoff"));Assert.Equal("Completed",handoff.VisitStatus);Assert.Equal(v.VisitId,handoff.VisitId);
                var replay=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",complete));Assert.True(replay.Replayed);
                using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();Assert.False(await db.ClinicalActivities.AnyAsync(x=>x.VisitId==v.VisitId));Assert.Equal("Completed",(await db.Appointments.SingleAsync(x=>x.Id==v.AppointmentId)).Status);
            }
        }
    }
    [Fact] public async Task OptionalHealthNotesAllowBlankFinalizationAndPreserveEnteredAndClearedText()
    {
        var (_,v,c) = await Prepare(); using(c) {
            var content=Content(true) with { MedicalHistory=new(Detail:"SYNTHETIC ghi chú"),Allergies=new(),Medications=new() };
            var draft=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,false)));
            var read=await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Equal(content.MedicalHistory.Detail,read!.Examination!.Content.MedicalHistory.Detail);
            Assert.False(read.Examination.Content.MedicalHistory.Verified);
            var cleared=content with { MedicalHistory=new(),Medications=new(Detail:"SYNTHETIC thuốc") };
            var final=await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,draft.Visit.VisitVersion,Guid.NewGuid(),cleared,true)));
            Assert.True(final.Examination!.Confirmed); Assert.Null(final.Examination.Content.MedicalHistory.Detail);
            Assert.Null(final.Examination.Content.Allergies.Detail); Assert.Equal("SYNTHETIC thuốc",final.Examination.Content.Medications.Detail);
            Assert.False(final.Examination.Content.Medications.Verified);
            var amended=await Ok<ExaminationWorkspace>(await c.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,final.Visit.VisitVersion,Guid.NewGuid(),cleared with {Medications=new()},"SYNTHETIC xóa ghi chú")));
            Assert.Null(amended.Examination!.Content.Medications.Detail);
            var versions=await c.GetFromJsonAsync<ExaminationVersion[]>(Exam(v.VisitId)+"/versions");
            Assert.Contains(versions!,x=>x.Content.MedicalHistory.Detail==content.MedicalHistory.Detail);
        }
    }
    [Theory]
    [InlineData("symptoms","lý do khám / triệu chứng")]
    [InlineData("diagnosis","chẩn đoán")]
    [InlineData("findings","ít nhất một kết quả khám")]
    [InlineData("findings.0.region","vùng khám")]
    [InlineData("findings.0.condition","tình trạng / kết quả khám")]
    [InlineData("advice","dặn dò / lời khuyên")]
    [InlineData("disposition","hướng xử trí")]
    [InlineData("returnNeed","nhu cầu quay lại")]
    [InlineData("referralDestination","nơi / lý do chuyển khám")]
    public async Task FinalizationIdentifiesMissingFieldWithoutWriting(string field,string label)
    {
        var (_,v,c)=await Prepare(); using(c) {
            var content=Content(true);
            content=field switch {
                "symptoms"=>content with {Symptoms=" "},"diagnosis"=>content with {Diagnosis=null},
                "findings"=>content with {Findings=[]},"findings.0.region"=>content with {Findings=[new(null," ","SYNTHETIC kết quả")]},
                "findings.0.condition"=>content with {Findings=[new(16,null," ")]},
                "conclusion"=>content with {Conclusion=null},"advice"=>content with {Advice=" "},
                "disposition"=>content with {Disposition=null},"returnNeed"=>content with {Disposition="FollowUp",ReturnNeed=null},
                _=>content with {Disposition="Referral",ReferralDestination=null}
            };
            var response=await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),content,true));
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            var error=await response.Content.ReadFromJsonAsync<DCMS.Application.Common.SystemMessage>();
            Assert.Equal("MSG02",error!.Code); Assert.Equal(field,error.Field);Assert.Contains(label,error.Message);
            var workspace=await c.GetFromJsonAsync<ExaminationWorkspace>(Exam(v.VisitId));
            Assert.Null(workspace!.Examination);Assert.Equal(v.VisitVersion,workspace.Visit.VisitVersion);
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
    [Fact] public async Task ConcurrentDraftCreationAndVisitCompletionCannotLeaveDraftOnCompletedVisit()
    {
        var (s,v,c) = await Prepare(); using(c) {
            var saved = await Ok<ExaminationWorkspace>(await c.PutAsJsonAsync(Exam(v.VisitId),new SaveExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true),true)));
            var activity = new DCMS.Application.Features.Treatment.ActivityInput(v.VisitId,null,null,null,null,s.ServiceId,"Toàn hàm","lần",1,fixture.Clock.Now,10,"SYNTHETIC ghi nhận",null,"Separate",null,100000,null);
            var results = await Task.WhenAll(
                c.PostAsJsonAsync($"/api/visits/{v.VisitId}/activities/drafts",new DCMS.Application.Features.Treatment.CreateActivityDraftRequest(activity,saved.Visit.VisitVersion,Guid.NewGuid())),
                c.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),true)));
            Assert.Single(results,x=>x.IsSuccessStatusCode); Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
            using var scope=fixture.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var visit=await db.Visits.SingleAsync(x=>x.Id==v.VisitId);
            Assert.False(visit.Status=="Completed" && await db.ClinicalActivities.AnyAsync(x=>x.VisitId==v.VisitId && x.State=="Draft"));
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
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.PostAsJsonAsync(Exam(v.VisitId)+"/amend",new AmendExamination(v.PatientId,v.VisitVersion,Guid.NewGuid(),Content(true),"SYNTHETIC lý do"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.PostAsJsonAsync($"/api/visits/{v.VisitId}/complete",new CompleteExaminationVisit(v.PatientId,v.VisitVersion,Guid.NewGuid(),true))).StatusCode);
    }
    [Fact] public async Task WrongPatientAndForgedDocumentSourcesAreRejected()
    {
        var (_,v,c) = await Prepare(); using(c) {
            Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(Exam(v.VisitId), new SaveExamination(v.PatientId + 1000, v.VisitVersion, Guid.NewGuid(), Content(), false))).StatusCode);
            foreach (var m in new[] { Metadata(v) with { PatientId = v.PatientId + 1000 }, Metadata(v) with { DossierId = Guid.NewGuid() }, Metadata(v) with { PlanId = Guid.NewGuid() } })
                Assert.Equal(HttpStatusCode.NotFound, (await Upload(c,m)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Upload(c, Metadata(v) with { InvestigationId = Guid.NewGuid() })).StatusCode);
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
