using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Infrastructure.Features.Treatment;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DCMS.UnitTests;

public sealed class PayOsGatewayTests
{
    private const string Key = "test-checksum-key";
    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }
    private static PayOsGateway Gateway(HttpMessageHandler handler) => new(new StubFactory(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PayOs:ClientId"] = "test-client", ["PayOs:ApiKey"] = "test-api",
            ["PayOs:ChecksumKey"] = Key, ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
        }).Build());
    private static string Hmac(string source) => Convert.ToHexStringLower(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(source)));

    [Fact]
    public async Task CreateSignsExactAmountAndReturnsQrPayload()
    {
        var gateway = Gateway(new StubHandler(async request =>
        {
            Assert.Equal("api-merchant.payos.vn", request.RequestUri!.Host);
            Assert.Equal("test-client", request.Headers.GetValues("x-client-id").Single());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var data = body.RootElement;
            Assert.Equal(120000, data.GetProperty("amount").GetInt64());
            var callback = "http://localhost:5173/patients/12/dossiers/00000000-0000-0000-0000-000000000001";
            Assert.Equal(Hmac($"amount=120000&cancelUrl={callback}&description=D12345678&orderCode=12345678&returnUrl={callback}"),
                data.GetProperty("signature").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                "{\"code\":\"00\",\"data\":{\"orderCode\":12345678,\"amount\":120000,\"paymentLinkId\":\"link-1\",\"qrCode\":\"qr-payload\",\"bin\":\"970415\",\"accountNumber\":\"123456789\",\"accountName\":\"CLINIC\"}}") };
        }));
        var link = await gateway.CreateAsync(12345678, 120000, "D12345678", 12,
            Guid.Parse("00000000-0000-0000-0000-000000000001"), DateTimeOffset.UtcNow.AddMinutes(30), default);
        Assert.Equal("qr-payload", link.QrCode);
        Assert.Equal("123456789", link.AccountNumber);
    }

    [Fact]
    public void WebhookSignatureChecksEveryFieldAndRejectsChanges()
    {
        var gateway = Gateway(new StubHandler(_ => throw new InvalidOperationException()));
        using var payload = JsonDocument.Parse("{\"orderCode\":12345678,\"amount\":120000,\"currency\":\"VND\",\"reference\":\"BANK-1\"}");
        var signature = Hmac("amount=120000&currency=VND&orderCode=12345678&reference=BANK-1");
        Assert.True(gateway.VerifyWebhook(payload.RootElement, signature));
        using var changed = JsonDocument.Parse("{\"orderCode\":12345678,\"amount\":120001,\"currency\":\"VND\",\"reference\":\"BANK-1\"}");
        Assert.False(gateway.VerifyWebhook(changed.RootElement, signature));
        Assert.False(gateway.VerifyWebhook(payload.RootElement, "invalid"));
    }
}
