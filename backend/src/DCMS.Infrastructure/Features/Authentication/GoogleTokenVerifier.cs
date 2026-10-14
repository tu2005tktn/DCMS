using System.Net.Mail;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed class GoogleTokenVerifier(IHttpClientFactory clients, IConfiguration configuration) : IGoogleTokenVerifier
{
    private const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private JsonWebKeySet? keys;
    private DateTimeOffset keysExpireAt;

    public async Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct)
    {
        var clientId = configuration["Google:WebClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new BusinessException(503, new SystemMessage("GOOGLE_NOT_CONFIGURED", "Đăng nhập Google chưa được cấu hình."));
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192)
            throw InvalidToken();

        // Google's public signing keys are fetched only from a fixed HTTPS endpoint and cached.
        var handler = new JsonWebTokenHandler();
        TokenValidationParameters Parameters(JsonWebKeySet keySet) => new()
        {
            ValidateIssuer = true,
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keySet.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        var result = await handler.ValidateTokenAsync(idToken, Parameters(await GetKeysAsync(false, ct)));
        // A key may rotate before its cached TTL ends. Retry once with fresh Google keys.
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
            result = await handler.ValidateTokenAsync(idToken, Parameters(await GetKeysAsync(true, ct)));
        if (!result.IsValid || result.ClaimsIdentity == null) throw InvalidToken();
        var claims = result.ClaimsIdentity.Claims;
        var subject = claims.FirstOrDefault(x => x.Type == "sub")?.Value;
        var email = claims.FirstOrDefault(x => x.Type == "email")?.Value;
        var verified = claims.FirstOrDefault(x => x.Type == "email_verified")?.Value;
        var name = claims.FirstOrDefault(x => x.Type == "name")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 128
            || string.IsNullOrWhiteSpace(email) || email.Length > 254
            || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email
            || !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase)) throw InvalidToken();
        return new GoogleIdentity(subject, email, string.IsNullOrWhiteSpace(name) ? email : name.Trim()[..Math.Min(name.Trim().Length, 160)]);
    }

    private async Task<JsonWebKeySet> GetKeysAsync(bool force, CancellationToken ct)
    {
        if (!force && keys != null && DateTimeOffset.UtcNow < keysExpireAt) return keys;
        await refreshLock.WaitAsync(ct);
        try
        {
            if (!force && keys != null && DateTimeOffset.UtcNow < keysExpireAt) return keys;
            using var request = new HttpRequestMessage(HttpMethod.Get, JwksUrl);
            using var response = await clients.CreateClient().SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsStringAsync(ct);
            var fetched = new JsonWebKeySet(payload);
            if (fetched.Keys.Count == 0) throw new InvalidOperationException("Google signing keys unavailable.");
            var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromMinutes(15);
            keys = fetched;
            keysExpireAt = DateTimeOffset.UtcNow.Add(maxAge > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : maxAge);
            return fetched;
        }
        finally { refreshLock.Release(); }
    }

    private static BusinessException InvalidToken() => new(401,
        new SystemMessage("GOOGLE_INVALID_TOKEN", "Không thể xác thực tài khoản Google. Vui lòng thử lại."));
}
