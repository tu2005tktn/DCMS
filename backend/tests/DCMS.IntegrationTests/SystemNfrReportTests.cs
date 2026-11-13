using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Treatment;
using DCMS.Application.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace DCMS.IntegrationTests;

// Real antivirus and a 60-second concurrent workload are explicit, optional NFR checks.
public sealed class SystemNfrFactAttribute : FactAttribute
{
    public SystemNfrFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DCMS_RUN_SYSTEM_NFR") != "1")
            Skip = "Set DCMS_RUN_SYSTEM_NFR=1 and start ClamAV to run local NFR checks.";
    }
}

public sealed class SystemNfrFixture : ReceptionFixture
{
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "dcms-nfr-" + Guid.NewGuid().ToString("N"));
    public SystemNfrFixture() => UseKestrel(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DCMS_NFR_BROWSER_SCRIPT")) ? 0 : 5080);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ClinicalDocuments:StoragePath"] = StoragePath,
            ["ClinicalDocuments:ClamAvHost"] = "127.0.0.1",
            ["ClinicalDocuments:ClamAvPort"] = "3310"
        }));
    }
}

public sealed class SystemNfrReportTests(SystemNfrFixture fixture, ITestOutputHelper output) : IClassFixture<SystemNfrFixture>
{
    [SystemNfrFact]
    public async Task MissingOrUnavailableScannerKeepsFilesPending()
    {
        var missing = new DCMS.Infrastructure.Features.Examinations.ClamAvClinicalFileScanner(new ConfigurationBuilder().Build());
        Assert.Equal("PendingScan", await missing.ScanAsync("%PDF-1.4"u8.ToArray(), default));
        var unavailable = new DCMS.Infrastructure.Features.Examinations.ClamAvClinicalFileScanner(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ClinicalDocuments:ClamAvHost"] = "127.0.0.1", ["ClinicalDocuments:ClamAvPort"] = "1" }).Build());
        Assert.Equal("PendingScan", await unavailable.ScanAsync("%PDF-1.4"u8.ToArray(), default));
    }

    [SystemNfrFact]
    public async Task RealHttpFiveMbUploadAndDocumentAccessIsolation()
    {
        var reception = new ReceptionApiTests(fixture);
        var setup = await reception.Prepare();
        using var dentist = await reception.Login("dentist.demo");
        var created = await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers",
            new CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC NFR", null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dossier = (await created.Content.ReadFromJsonAsync<TreatmentChange>())!.Dossier;
        var bytes = MakePdf(5_000_000);
        var measurements = new List<object>();
        var checks = new List<object>();
        DocumentDto? document = null;
        for (var sample = 1; sample <= 3; sample++)
        {
            var metadata = new DocumentMetadata(setup.PatientId, null, dossier.Id, null, null, null, "Other", "Clinic",
                DateOnly.FromDateTime(fixture.Clock.Now.ToOffset(TimeSpan.FromHours(7)).DateTime), "SYNTHETIC NFR 5 MB", Guid.NewGuid());
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "metadata");
            var part = new ByteArrayContent(bytes); part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(part, "file", "synthetic-nfr-5mb.pdf");
            var timer = Stopwatch.StartNew();
            using var response = await dentist.PostAsync("/api/clinical-documents", form);
            var body = await response.Content.ReadAsStringAsync(); timer.Stop();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            document = JsonSerializer.Deserialize<UploadBatchResult>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Document!;
            Assert.NotNull(document);
            Assert.Equal("Ready", document.FileState);
            using var content = await dentist.GetAsync($"/api/clinical-documents/{document.DocumentId}/content");
            Assert.Equal(HttpStatusCode.OK, content.StatusCode);
            Assert.Equal(bytes, await content.Content.ReadAsByteArrayAsync());
            Assert.Equal("no-store", content.Headers.CacheControl!.ToString());
            Assert.Equal("nosniff", Assert.Single(content.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal(bytes.Length, document.SizeBytes);
            measurements.Add(new { sample, milliseconds = timer.Elapsed.TotalMilliseconds, httpStatus = (int)response.StatusCode, document.FileState, sizeBytes = bytes.Length });
            if (sample == 3)
            {
                using var replayResponse = await dentist.PostAsync("/api/clinical-documents", form);
                Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
                var replay = (await replayResponse.Content.ReadFromJsonAsync<UploadBatchResult>())!.Document!;
                Assert.Equal(document.DocumentId, replay.DocumentId);
                checks.Add(new { id = "TC-REL-001", result = "Pass", sameDocumentId = true });
            }
        }
        var paths = new[] { $"/api/clinical-documents/{document!.DocumentId}/content", $"/clinical-documents/{document.DocumentId:N}", $"/uploads/{document.DocumentId:N}" };
        var probes = new List<object>();
        foreach (var name in new[] { "guest.demo", "patient.demo", "admin.demo" })
        {
            using var actor = await reception.Login(name);
            foreach (var path in paths)
            {
                using var denied = await actor.GetAsync(path);
                probes.Add(new { actor = name, path, status = (int)denied.StatusCode });
                Assert.True(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
            }
        }
        using var anonymous = fixture.CreateClient();
        using var anon = await anonymous.GetAsync(paths[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
        probes.Add(new { actor = "anonymous", path = paths[0], status = (int)anon.StatusCode });
        using var anonymousMe = await anonymous.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMe.StatusCode);
        checks.Add(new { id = "TC-SEC-003", status = (int)anonymousMe.StatusCode });
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.nfr.token");
        using var invalid = await anonymous.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        checks.Add(new { id = "TC-SEC-004", status = (int)invalid.StatusCode });
        using var cors = new HttpRequestMessage(HttpMethod.Options, "/api/auth/me");
        cors.Headers.Add("Origin", "https://untrusted.example.invalid");
        cors.Headers.Add("Access-Control-Request-Method", "GET");
        using var corsResult = await anonymous.SendAsync(cors);
        Assert.False(corsResult.Headers.Contains("Access-Control-Allow-Origin"));
        checks.Add(new { id = "TC-SEC-008", status = (int)corsResult.StatusCode, allowedOrigin = false });
        using var guest = await reception.Login("guest.demo");
        var script = Environment.GetEnvironmentVariable("DCMS_NFR_BROWSER_SCRIPT");
        if (!string.IsNullOrWhiteSpace(script))
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DCMS_NFR_BROWSER_NODE") ?? "node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(script);
            start.Environment["DCMS_NFR_TOKEN"] = guest.DefaultRequestHeaders.Authorization!.Parameter!;
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            output.WriteLine(await stdout); output.WriteLine(await stderr);
            Assert.Equal(0, process.ExitCode);
        }
        var single = new List<double>();
        for (var i = 0; i < 30; i++) { var t = Stopwatch.StartNew(); using var r = await guest.GetAsync("/api/auth/me"); var user = await r.Content.ReadFromJsonAsync<JsonElement>(); t.Stop(); Assert.Equal(HttpStatusCode.OK, r.StatusCode); Assert.True(user.TryGetProperty("userName", out _)); single.Add(t.Elapsed.TotalMilliseconds); }
        var clients = new List<HttpClient>();
        for (var i = 0; i < 20; i++)
        {
            using var register = fixture.CreateClient();
            var phone = "09" + RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
            using var registered = await register.PostAsJsonAsync("/api/auth/register", new RegisterRequest("SYNTHETIC NFR " + i, phone, $"nfr{phone}@example.invalid", fixture.Password, fixture.Password));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            clients.Add(await reception.Login(phone));
        }
        var loadSamples = new System.Collections.Concurrent.ConcurrentBag<(double Ms, int Status, bool Valid)>();
        var loadTimer = Stopwatch.StartNew();
        await Task.WhenAll(clients.Select(async client =>
        {
            var index = 0;
            while (loadTimer.Elapsed < TimeSpan.FromSeconds(60))
            {
                var path = index++ % 2 == 0 ? "/api/auth/me" : "/api/me/appointments?scope=UPCOMING&status=ALL&page=1&pageSize=20";
                var t = Stopwatch.StartNew();
                try { using var r = await client.GetAsync(path); var jsonBody = await r.Content.ReadFromJsonAsync<JsonElement>(); t.Stop(); var valid = jsonBody.TryGetProperty(path.Contains("appointments") ? "items" : "userName", out _); loadSamples.Add((t.Elapsed.TotalMilliseconds, (int)r.StatusCode, valid)); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { t.Stop(); loadSamples.Add((t.Elapsed.TotalMilliseconds, 0, false)); }
                await Task.Delay(250);
            }
        }));
        loadTimer.Stop(); foreach (var client in clients) client.Dispose();
        static double P95(IEnumerable<double> values) { var a = values.Order().ToArray(); return a[(int)Math.Ceiling(a.Length * .95) - 1]; }
        var load = new { users = 20, requestedDurationSeconds = 60, elapsedSeconds = loadTimer.Elapsed.TotalSeconds, thinkTimeMs = 250, requests = loadSamples.Count, errors = loadSamples.Count(x => x.Status != 200 || !x.Valid), p95Ms = P95(loadSamples.Select(x => x.Ms)), requestsPerSecond = loadSamples.Count / loadTimer.Elapsed.TotalSeconds, statuses = loadSamples.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count()) };
        using var health = await anonymous.GetAsync("/health"); Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var evidence = new { atUtc = DateTimeOffset.UtcNow, server = "Kestrel TCP loopback", scanner = "Real ClamAV INSTREAM", sizeBytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)), measurements, probes, checks, singleUser = new { samples = single, p95Ms = P95(single) }, load, healthAfterLoad = (int)health.StatusCode };
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(json);
        var evidencePath = Environment.GetEnvironmentVariable("DCMS_NFR_EVIDENCE_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath)) await File.WriteAllTextAsync(evidencePath, json);
    }

    // A syntactically valid one-page PDF padded by a comment before xref; exact decimal MB size.
    private static byte[] MakePdf(int size)
    {
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string value) { offsets.Add(text.Length); text.Append($"{offsets.Count} 0 obj\n{value}\nendobj\n"); }
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        string Tail(int position) => $"xref\n0 4\n0000000000 65535 f \n{string.Join("", offsets.Select(x => $"{x:0000000000} 00000 n \n"))}trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{position}\n%%EOF\n";
        var tailLength = Tail(size).Length;
        var position = size - tailLength;
        while (Tail(position).Length != tailLength) { tailLength = Tail(position).Length; position = size - tailLength; }
        var padding = position - text.Length - 2;
        text.Append('%').Append('x', padding).Append('\n');
        text.Append(Tail(position));
        var bytes = Encoding.ASCII.GetBytes(text.ToString()); Assert.Equal(size, bytes.Length); return bytes;
    }
}
