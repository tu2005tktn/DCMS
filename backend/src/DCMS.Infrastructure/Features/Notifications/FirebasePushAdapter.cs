using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Notifications;

/// <summary>FCM HTTP v1. The service account JSON stays on the server outside Git.</summary>
public sealed class FirebasePushAdapter(
    DcmsDbContext db, IConfiguration configuration, IHttpClientFactory clients,
    ILogger<FirebasePushAdapter> logger) : IExternalNotificationAdapter
{
    public async Task<ExternalDeliveryResult> SendAsync(NotificationChannel channel, NotificationDto notification, CancellationToken ct)
    {
        if (channel != NotificationChannel.Push)
            return new(false, null, $"{channel} adapter is not configured.");
        if (notification.AccountId is not int accountId)
            return new(true, null, null);
        var path = configuration["Firebase:ServiceAccountPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new(false, null, "Firebase service account is not configured.");
        var devices = await db.PushDevices.Where(x => x.AccountId == accountId)
            .OrderBy(x => x.UpdatedAt).Take(20).ToListAsync(ct);
        if (devices.Count == 0) return new(true, null, null);

        try
        {
            using var credentials = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            var root = credentials.RootElement;
            var project = root.GetProperty("project_id").GetString()!;
            var bearer = await GetAccessTokenAsync(root, ct);
            var client = clients.CreateClient();
            var delivered = false;
            var failed = false;
            foreach (var device in devices)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(project)}/messages:send");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                request.Content = JsonContent.Create(new
                {
                    message = new
                    {
                        token = device.Token,
                        notification = new { title = notification.Title, body = notification.Body },
                        android = new { notification = new { channel_id = "appointments" } },
                        data = new { notificationId = notification.Id, deepLink = notification.DeepLink ?? "" }
                    }
                });
                using var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode) { delivered = true; continue; }
                // An unregistered device must never receive notifications for a future account.
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                if ((int)response.StatusCode == 404 && errorBody.Contains("UNREGISTERED", StringComparison.Ordinal))
                    db.PushDevices.Remove(device);
                else
                    failed = true;
                logger.LogWarning("FCM rejected a push with HTTP {StatusCode}", (int)response.StatusCode);
            }
            await db.SaveChangesAsync(ct);
            return failed ? new(false, null, "FCM delivery failed.") : new(true, delivered ? "fcm" : null, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "FCM delivery failed");
            return new(false, null, "FCM delivery failed.");
        }
    }

    private async Task<string> GetAccessTokenAsync(JsonElement credentials, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var email = credentials.GetProperty("client_email").GetString()!;
        var privateKey = credentials.GetProperty("private_key").GetString()!;
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = email,
            ["scope"] = "https://www.googleapis.com/auth/firebase.messaging",
            ["aud"] = "https://oauth2.googleapis.com/token",
            ["iat"] = now,
            ["exp"] = now + 3600
        }));
        var assertion = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var signature = Base64Url(rsa.SignData(Encoding.UTF8.GetBytes(assertion),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var response = await clients.CreateClient().PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = $"{assertion}.{signature}"
            }), ct);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return body.RootElement.GetProperty("access_token").GetString()!;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
