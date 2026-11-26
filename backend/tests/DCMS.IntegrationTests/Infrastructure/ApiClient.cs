using System.Net.Http.Json;
using System.Text.Json;

namespace DCMS.IntegrationTests;

/// <summary>What the API answered: HTTP status plus the raw JSON body.</summary>
public sealed record ApiResponse(int Status, string Body, string? Reason = null)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The HTTP status line as the report shows it, for example "HTTP 409 Conflict".</summary>
    public string StatusLine => $"HTTP {Status} {Reason}".TrimEnd();

    public T Json<T>() => JsonSerializer.Deserialize<T>(Body, Options) ?? throw new InvalidOperationException("Empty body: " + Body);

    /// <summary>The "message" the API sends back (system message), or null when the body has none.</summary>
    public string? Message => Property("message");
    public string? Code => Property("code");

    private string? Property(string name)
    {
        if (string.IsNullOrWhiteSpace(Body)) return null;
        try
        {
            using var document = JsonDocument.Parse(Body);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var value)
                ? value.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>Thin HTTP wrapper: sends the request, records the exchange for the report, returns status + body.</summary>
public sealed class ApiClient(HttpClient http)
{
    public async Task<ApiResponse> GetAsync(string path) => await Send(HttpMethod.Get, path, null);
    public async Task<ApiResponse> PostAsync(string path, object? body) => await Send(HttpMethod.Post, path, body);
    public async Task<ApiResponse> PutAsync(string path, object? body) => await Send(HttpMethod.Put, path, body);

    private async Task<ApiResponse> Send(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase((int)response.StatusCode)
            : response.ReasonPhrase;
        var result = new ApiResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync(), reason);
        Trace.Record(method.Method, path, result);
        return result;
    }
}
