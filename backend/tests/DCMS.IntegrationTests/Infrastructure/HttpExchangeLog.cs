using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DCMS.IntegrationTests;

/// <summary>
/// Server-side log of every HTTP exchange the tests make, written as JSON lines to the file in DCMS_IT_HTTP_LOG
/// (docs/testing/INTEGRATION-TEST-COVERAGE.vi.md). The report generator matches each line to the test that was
/// running at that time (from the .trx start/end times), so every case in the report shows its real requests.
/// Does nothing when the variable is not set.
/// </summary>
public sealed class HttpExchangeLog : IStartupFilter
{
    private const int MaxRequest = 4000;
    private const int MaxResponse = 3000;
    private static readonly Lock Gate = new();
    private static readonly string? Path = Environment.GetEnvironmentVariable("DCMS_IT_HTTP_LOG");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        if (!string.IsNullOrWhiteSpace(Path)) app.Use(RecordAsync);
        next(app);
    };

    private static async Task RecordAsync(HttpContext context, Func<Task> next)
    {
        var started = DateTimeOffset.UtcNow;
        string? requestBody = null;
        if (context.Request.ContentLength is > 0 && context.Request.ContentType?.Contains("json") == true)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next();
        }
        finally
        {
            buffer.Position = 0;
            var isText = context.Response.ContentType is { } type && (type.Contains("json") || type.StartsWith("text/"));
            var responseBody = isText ? Encoding.UTF8.GetString(buffer.ToArray()) : null;
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
            context.Response.Body = original;
            Write(new
            {
                at = started.ToString("O"),
                method = context.Request.Method,
                path = context.Request.Path.Value + context.Request.QueryString.Value,
                status = context.Response.StatusCode,
                // Account id of an authenticated caller; the report maps it to the user name from the sign-in response.
                user = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                request = Trim(requestBody, MaxRequest),
                response = Trim(responseBody, MaxResponse)
            });
        }
    }

    // Test passwords and tokens are random per run, but they never belong in a report.
    private static readonly System.Text.RegularExpressions.Regex Secret = new(
        "(\"(?:password|confirmPassword|newPassword|currentPassword|accessToken|refreshToken|token|proof|idToken|resetGrant|otp)\"\\s*:\\s*)\"[^\"]*\"",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string? Trim(string? text, int max)
    {
        if (text == null) return null;
        text = Secret.Replace(text, "$1\"***\"");
        return text.Length > max ? text[..max] + "…" : text;
    }

    private static void Write(object line)
    {
        var json = JsonSerializer.Serialize(line, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        lock (Gate) File.AppendAllText(Path!, json + Environment.NewLine);
    }
}
