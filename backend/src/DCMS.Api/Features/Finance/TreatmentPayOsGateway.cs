using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DCMS.Api.Features.Finance;

public sealed record TreatmentPayOsLink(long OrderCode, string PaymentLinkId, string QrCode,
    string BankBin, string AccountNumber, string AccountName);
public sealed record TreatmentPayOsStatus(long OrderCode, string PaymentLinkId, long Amount,
    long AmountPaid, string Status);

public sealed class TreatmentPayOsGateway(IHttpClientFactory clients, IConfiguration configuration)
{
    private string ClientId => configuration["PayOs:ClientId"] ?? "";
    private string ApiKey => configuration["PayOs:ApiKey"] ?? "";
    private string ChecksumKey => configuration["PayOs:ChecksumKey"] ?? "";
    public bool Configured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ChecksumKey);

    public bool VerifyWebhook(JsonElement data, string? signature)
    {
        if (!Configured || data.ValueKind != JsonValueKind.Object || signature is not { Length: 64 }) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); }
        catch (FormatException) { return false; }
        var source = string.Join("&", data.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}={x.Value.ValueKind switch {
                JsonValueKind.Null => "", JsonValueKind.String => x.Value.GetString(),
                JsonValueKind.Number => x.Value.GetRawText(), JsonValueKind.True => "true",
                JsonValueKind.False => "false", _ => x.Value.GetRawText() }}"));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(ChecksumKey), Encoding.UTF8.GetBytes(source));
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private HttpClient Client()
    {
        if (!Configured) throw new InvalidOperationException("payOS chưa được cấu hình trên máy chủ.");
        var client = clients.CreateClient("treatment-payos");
        client.BaseAddress = new Uri("https://api-merchant.payos.vn/");
        client.DefaultRequestHeaders.Add("x-client-id", ClientId);
        client.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        return client;
    }

    public async Task<TreatmentPayOsLink> CreateAsync(long orderCode, long amount, CancellationToken ct)
    {
        // payOS requires return/cancel URLs even though DCMS renders the QR inside its own popup.
        // Reuse the configured web origin; no PAYOS_WEB_BASE_URL is required.
        var callback = (configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:5173").TrimEnd('/') + "/patients";
        var description = $"DCMS {orderCode}";
        var source = $"amount={amount}&cancelUrl={callback}&description={description}&orderCode={orderCode}&returnUrl={callback}";
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ChecksumKey), Encoding.UTF8.GetBytes(source)));
        using var client = Client();
        using var response = await client.PostAsJsonAsync("v2/payment-requests", new {
            orderCode, amount, description, cancelUrl = callback, returnUrl = callback,
            expiredAt = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds(), signature
        }, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("payOS không tạo được mã QR.");
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new JsonException("payOS không trả dữ liệu.");
        var root = json.RootElement;
        if (root.GetProperty("code").GetString() != "00") throw new HttpRequestException("payOS từ chối tạo mã QR.");
        var data = root.GetProperty("data");
        if (data.GetProperty("orderCode").GetInt64() != orderCode || data.GetProperty("amount").GetInt64() != amount)
            throw new JsonException("Thông tin đơn payOS không khớp.");
        return new(orderCode, data.GetProperty("paymentLinkId").GetString()!, data.GetProperty("qrCode").GetString()!,
            data.GetProperty("bin").GetString() ?? "", data.GetProperty("accountNumber").GetString() ?? "",
            data.GetProperty("accountName").GetString() ?? "");
    }

    public async Task<TreatmentPayOsStatus> GetAsync(long orderCode, CancellationToken ct)
    {
        using var client = Client();
        using var response = await client.GetAsync($"v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}", ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Không đọc được trạng thái payOS.");
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            ?? throw new JsonException("payOS không trả trạng thái.");
        var root = json.RootElement;
        if (root.GetProperty("code").GetString() != "00") throw new HttpRequestException("payOS từ chối tra cứu.");
        var data = root.GetProperty("data");
        return new(data.GetProperty("orderCode").GetInt64(), data.GetProperty("id").GetString() ?? "",
            data.GetProperty("amount").GetInt64(), data.GetProperty("amountPaid").GetInt64(),
            data.GetProperty("status").GetString() ?? "");
    }
}
