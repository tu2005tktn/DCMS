using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Application.Features.Examinations;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.Examinations;

// Calls the private ai-service (docs/specs/AI-CARIES-DETECTION.vi.md §4). Configuration: DentalAi:BaseUrl, ApiKey, TimeoutSeconds.
public sealed class HttpDentalAiClient(IHttpClientFactory factory, IConfiguration config) : IDentalAiClient
{
    private sealed record Box(double X1, double Y1, double X2, double Y2);
    private sealed record Finding(string FindingId, string Kind, string Label, double Confidence, Box Box, string Jaw, string Side);
    private sealed record Model(string Kind, string Name, string Sha256);
    private sealed record Size(int Width, int Height);
    private sealed record Response(string SchemaVersion, Model[] Models, Size Image, string Device, int ElapsedMs, Finding[] Findings);

    public async Task<DentalAiResult> AnalyzeXRayAsync(byte[] image, string contentType, string fileName, CancellationToken ct)
    {
        var baseUrl = config["DentalAi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root))
            throw new DentalAiUnavailableException("Dịch vụ AI chưa được cấu hình.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("DentalAi:TimeoutSeconds", 120), 5, 600)));
        var client = factory.CreateClient(nameof(HttpDentalAiClient));
        client.Timeout = Timeout.InfiniteTimeSpan;
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image); file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, "v1/xray/analyze")) { Content = form };
        var key = config["DentalAi:ApiKey"];
        if (!string.IsNullOrEmpty(key)) request.Headers.Add("X-Api-Key", key);
        Response? body;
        try {
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new DentalAiUnavailableException($"Dịch vụ AI trả lỗi {(int)response.StatusCode}.");
            body = await response.Content.ReadFromJsonAsync<Response>(new JsonSerializerOptions(JsonSerializerDefaults.Web), timeout.Token);
        } catch (OperationCanceledException e) when (!ct.IsCancellationRequested) {
            throw new DentalAiUnavailableException("Dịch vụ AI phản hồi quá thời gian.", e);
        } catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException) {
            throw new DentalAiUnavailableException("Không kết nối được dịch vụ AI.", e);
        }
        if (body?.Models is not { Length: > 0 } || body.Image is null || body.Findings is null || body.Image.Width <= 0 || body.Image.Height <= 0)
            throw new DentalAiUnavailableException("Dịch vụ AI trả dữ liệu không hợp lệ.");
        var findings = body.Findings.Where(f => f.Box is not null && f.Confidence is >= 0 and <= 1
                && f.Box.X1 >= 0 && f.Box.Y1 >= 0 && f.Box.X2 <= body.Image.Width && f.Box.Y2 <= body.Image.Height && f.Box.X2 > f.Box.X1 && f.Box.Y2 > f.Box.Y1)
            .Select(f => new DentalAiFinding(Clip(f.FindingId, 16), Clip(f.Kind, 32), Clip(f.Label, 64), Math.Round(f.Confidence, 4),
                f.Box.X1, f.Box.Y1, f.Box.X2, f.Box.Y2, Clip(f.Jaw, 8), Clip(f.Side, 8))).ToArray();
        return new(Clip(body.SchemaVersion, 16), Clip(string.Join("+", body.Models.Select(m => m.Name)), 128), Clip(body.Models[0].Sha256, 64),
            body.Image.Width, body.Image.Height, Math.Max(0, body.ElapsedMs), Clip(body.Device, 16), findings);
    }

    private static string Clip(string? value, int max) => value is null ? "" : value.Length <= max ? value : value[..max];
}
