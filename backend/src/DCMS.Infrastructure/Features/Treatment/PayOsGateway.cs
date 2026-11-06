using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.Treatment;

public sealed record PayOsLink(string PaymentLinkId, string QrCode, string BankBin,
    string AccountNumber, string AccountName);
public sealed record PayOsRemoteStatus(string Status, long Amount, long AmountPaid, long OrderCode, string PaymentLinkId);

public sealed class PayOsGateway(IHttpClientFactory clients, IConfiguration configuration)
{
    private string ClientId => configuration["PayOs:ClientId"] ?? "";
    private string ApiKey => configuration["PayOs:ApiKey"] ?? "";
    private string ChecksumKey => configuration["PayOs:ChecksumKey"] ?? "";
    private string WebBaseUrl => configuration["PayOs:WebBaseUrl"]
        ?? configuration["Cors:AllowedOrigins:0"] ?? "";
    private static readonly Uri Origin = new("https://api-merchant.payos.vn/");
    private static BusinessException Unavailable() => new(503, new SystemMessage("PAYOS_UNAVAILABLE", "Không kết nối được payOS. Vui lòng thử lại."));

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ApiKey)
            || string.IsNullOrWhiteSpace(ChecksumKey) || !Uri.TryCreate(WebBaseUrl, UriKind.Absolute, out var web)
            || web.Scheme is not ("https" or "http"))
            throw new BusinessException(503, new SystemMessage("PAYOS_NOT_CONFIGURED", "payOS chưa được cấu hình trên máy chủ."));
    }

    private string Sign(string value) => Convert.ToHexStringLower(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(ChecksumKey), Encoding.UTF8.GetBytes(value)));

    public bool VerifyWebhook(JsonElement data, string? signature)
    {
        EnsureConfigured();
        if (data.ValueKind != JsonValueKind.Object || signature is not { Length: 64 }) return false;
        byte[] provided;
        try { provided = Convert.FromHexString(signature); }
        catch (FormatException) { return false; }
        var source = string.Join("&", data.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}={Value(x.Value)}"));
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(source)), provided);
    }

    private static string Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };

    private HttpClient Client()
    {
        EnsureConfigured();
        var client = clients.CreateClient("payos");
        client.BaseAddress = Origin;
        client.DefaultRequestHeaders.Add("x-client-id", ClientId);
        client.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        return client;
    }

    public async Task<PayOsLink> CreateAsync(long orderCode, long amountVnd, string description,
        int patientId, Guid dossierId, DateTimeOffset expiresAt, CancellationToken ct)
    {
        using var client = Client();
        var callback = $"{WebBaseUrl.TrimEnd('/')}/patients/{patientId}/dossiers/{dossierId}";
        var signed = $"amount={amountVnd}&cancelUrl={callback}&description={description}&orderCode={orderCode}&returnUrl={callback}";
        var body = new { orderCode, amount = amountVnd, description, cancelUrl = callback, returnUrl = callback,
            expiredAt = expiresAt.ToUnixTimeSeconds(), signature = Sign(signed) };
        try
        {
            using var response = await client.PostAsJsonAsync("v2/payment-requests", body, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct) ?? throw Unavailable();
            var root = json.RootElement;
            if (root.GetProperty("code").GetString() != "00") throw Unavailable();
            var data = root.GetProperty("data");
            if (data.GetProperty("orderCode").GetInt64() != orderCode || data.GetProperty("amount").GetInt64() != amountVnd)
                throw Unavailable();
            return new(data.GetProperty("paymentLinkId").GetString() ?? throw Unavailable(),
                data.GetProperty("qrCode").GetString() ?? throw Unavailable(),
                data.GetProperty("bin").GetString() ?? "", data.GetProperty("accountNumber").GetString() ?? "",
                data.GetProperty("accountName").GetString() ?? "");
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (KeyNotFoundException) { throw Unavailable(); }
    }

    public async Task<PayOsRemoteStatus> StatusAsync(long orderCode, CancellationToken ct)
    {
        using var client = Client();
        try
        {
            using var response = await client.GetAsync($"v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}", ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct) ?? throw Unavailable();
            var root = json.RootElement;
            if (root.GetProperty("code").GetString() != "00") throw Unavailable();
            var data = root.GetProperty("data");
            return new(data.GetProperty("status").GetString() ?? "", data.GetProperty("amount").GetInt64(),
                data.GetProperty("amountPaid").GetInt64(), data.GetProperty("orderCode").GetInt64(),
                data.GetProperty("id").GetString() ?? "");
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (KeyNotFoundException) { throw Unavailable(); }
    }
}
