using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Reception;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Stand-in for ai-service; HTTP endpoints, auth, PostgreSQL JSONB and the private file store are real.
public sealed class ExaminationTestDentalAi : IDentalAiClient
{
    public bool Available { get; set; } = true;
    public int Calls { get; private set; }
    public Task<DentalAiResult> AnalyzeXRayAsync(byte[] image, string contentType, string fileName, CancellationToken ct)
    {
        Calls++;
        if (!Available) throw new DentalAiUnavailableException("Không kết nối được dịch vụ AI.");
        return Task.FromResult(new DentalAiResult("1.0", "recommended_caries_best", new string('a', 64), 1, 1, 12, "cpu",
            [new DentalAiFinding("F01", "caries", "caries_lesion", 0.81, 0, 0, 1, 1, "upper", "right")]));
    }
}

// docs/specs/AI-CARIES-DETECTION.vi.md §5 and §9.
public sealed class ClinicalAiAnalysisTests(ExaminationFixture fixture) : IClassFixture<ExaminationFixture>
{
    private readonly ReceptionApiTests reception = new(fixture);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ9sAAAAASUVORK5CYII=");
    private static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private async Task<DocumentDto> Upload(HttpClient c, VisitResult v, string type, byte[] bytes, string name, string mime)
    {
        var m = new DocumentMetadata(v.PatientId, v.VisitId, null, null, null, type, "Clinic",
            DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime), "SYNTHETIC x-ray", Guid.NewGuid());
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(JsonSerializer.Serialize(m, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "metadata");
        var part = new ByteArrayContent(bytes); part.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(part, "file", name);
        var response = await c.PostAsync("/api/clinical-documents", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<UploadBatchResult>())!.Document!;
    }
    private static Task<HttpResponseMessage> Analyze(HttpClient c, Guid id, Guid key) =>
        c.PostAsJsonAsync($"/api/clinical-documents/{id}/ai-analyses", new RunAiAnalysis(key));

    [Fact] public async Task DentistRunsStoredIdempotentAnalysisOnReadyXRayOnly()
    {
        var ai = fixture.Services.GetRequiredService<ExaminationTestDentalAi>(); ai.Available = true;
        fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "Ready";
        var setup = await reception.Prepare(); using var assistant = await reception.Login(); var visit = await reception.Check(assistant, setup);
        using var dentist = await reception.Login("dentist.demo");
        var xray = await Upload(dentist, visit, "XRay", Png, "pano.png", "image/png");

        ai.Available = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Analyze(dentist, xray.DocumentId, Guid.NewGuid())).StatusCode);
        ai.Available = true;

        var key = Guid.NewGuid(); var before = ai.Calls;
        var response = await Analyze(dentist, xray.DocumentId, key);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var analysis = (await response.Content.ReadFromJsonAsync<AiAnalysisDto>())!;
        Assert.Equal("Caries", analysis.Kind); Assert.Single(analysis.Findings); Assert.Equal(0.81, analysis.Findings[0].Confidence);
        Assert.Equal(64, analysis.InputSha256.Length); Assert.False(string.IsNullOrEmpty(analysis.RequestedByName));

        var replay = (await (await Analyze(dentist, xray.DocumentId, key)).Content.ReadFromJsonAsync<AiAnalysisDto>())!;
        Assert.Equal(analysis.AnalysisId, replay.AnalysisId); Assert.Equal(before + 1, ai.Calls);

        var detail = (await dentist.GetFromJsonAsync<DocumentDto>($"/api/clinical-documents/{xray.DocumentId}"))!;
        Assert.Single(detail.AiAnalyses!); Assert.Equal(xray.Version + 1, detail.Version);
        var history = (await dentist.GetFromJsonAsync<DocumentHistoryDto[]>($"/api/clinical-documents/{xray.DocumentId}/history"))!;
        Assert.Contains(history, h => h.Action == "AiAnalysisFailed"); Assert.Contains(history, h => h.Action == "AiAnalyzed");

        Assert.Equal(HttpStatusCode.Forbidden, (await Analyze(assistant, xray.DocumentId, Guid.NewGuid())).StatusCode);
        var pdf = await Upload(dentist, visit, "XRay", Pdf, "film.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, (await Analyze(dentist, pdf.DocumentId, Guid.NewGuid())).StatusCode);
        var photo = await Upload(dentist, visit, "IntraoralPhoto", Png, "photo.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, (await Analyze(dentist, photo.DocumentId, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Analyze(dentist, photo.DocumentId, key)).StatusCode);

        fixture.Services.GetRequiredService<ExaminationTestScanner>().State = "PendingScan";
        var pending = await Upload(dentist, visit, "XRay", Png, "pending.png", "image/png");
        Assert.Equal(HttpStatusCode.Conflict, (await Analyze(dentist, pending.DocumentId, Guid.NewGuid())).StatusCode);
    }
}

// Opt-in contract check against a running ai-service: set DENTAL_AI_LIVE_URL and DENTAL_AI_LIVE_IMAGE (a panoramic JPEG/PNG).
public sealed class DentalAiLiveContractTests
{
    [Fact] public async Task RealClientParsesLiveServiceResponse()
    {
        var url = Environment.GetEnvironmentVariable("DENTAL_AI_LIVE_URL"); var image = Environment.GetEnvironmentVariable("DENTAL_AI_LIVE_IMAGE");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(image)) return;
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["DentalAi:BaseUrl"] = url, ["DentalAi:ApiKey"] = Environment.GetEnvironmentVariable("DENTAL_AI_LIVE_KEY") }).Build();
        var services = new ServiceCollection(); services.AddHttpClient();
        var client = new DCMS.Infrastructure.Features.Examinations.HttpDentalAiClient(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>(), config);
        var mime = image.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        var result = await client.AnalyzeXRayAsync(await File.ReadAllBytesAsync(image), mime, Path.GetFileName(image), CancellationToken.None);
        Assert.Equal("1.0", result.SchemaVersion); Assert.Equal(64, result.ModelSha256.Length); Assert.True(result.ImageWidth > 0);
        Assert.All(result.Findings, f => { Assert.Equal("caries", f.Kind); Assert.InRange(f.Confidence, 0, 1); Assert.True(f.X2 > f.X1 && f.X2 <= result.ImageWidth); });
        Console.WriteLine($"live findings: {result.Findings.Count}");
    }
}
