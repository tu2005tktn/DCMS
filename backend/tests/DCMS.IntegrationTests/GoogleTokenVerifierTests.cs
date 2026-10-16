using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using DCMS.Application.Common;
using DCMS.Infrastructure.Features.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class GoogleTokenVerifierTests
{
    [Fact]
    public async Task AcceptsSignedVerifiedEmailAndRejectsWrongAudienceOrUnverifiedEmail()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-google-key" };
        var publicKey = rsa.ExportParameters(false);
        var jwks = $$"""
            {"keys":[{"kty":"RSA","kid":"test-google-key","use":"sig","alg":"RS256",
            "n":"{{Base64UrlEncoder.Encode(publicKey.Modulus)}}","e":"{{Base64UrlEncoder.Encode(publicKey.Exponent)}}"}]}
            """;
        var verifier = new GoogleTokenVerifier(new StaticClientFactory(jwks),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Google:WebClientId"] = "dcms-test-client" }).Build());
        string MakeToken(string audience, bool verified)
        {
            var jwt = new JwtSecurityToken("https://accounts.google.com", audience,
                [new Claim("sub", "google-sub-123"), new Claim("email", "new@example.com"),
                 new Claim("email_verified", verified ? "true" : "false"), new Claim("name", "Google User")],
                notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        var identity = await verifier.VerifyAsync(MakeToken("dcms-test-client", true), CancellationToken.None);
        Assert.Equal("google-sub-123", identity.Subject);
        Assert.Equal("new@example.com", identity.Email);
        await Assert.ThrowsAsync<BusinessException>(() => verifier.VerifyAsync(MakeToken("other-client", true), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessException>(() => verifier.VerifyAsync(MakeToken("dcms-test-client", false), CancellationToken.None));
    }

    private sealed class StaticClientFactory(string jwks) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "") => new(new StaticHandler(jwks));
    }

    private sealed class StaticHandler(string jwks) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jwks) });
    }
}
