using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DCMS.Api.Features.Authentication;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class PublicEmailRecoveryTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task PublicCanRecoverUsingRegisteredEmailWithoutPriorConfirmation()
    {
        var phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999);
        var email = $"public-{Guid.NewGuid():N}@example.test";
        var password = fixture.Password;
        var client = fixture.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Email Recovery Test", phone, email, password, password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var accountId = (await registration.Content.ReadFromJsonAsync<RegisterResponse>())!.AccountId;

        var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(phone, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var oldSession = (await login.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldSession.AccessToken);

        var initial = await client.GetFromJsonAsync<EmailVerificationStatus>("/api/me/email-verification");
        Assert.False(initial!.Confirmed);
        Assert.Equal(email, initial.Email);
        var request = await fixture.CreateClient().PostAsJsonAsync(
            "/api/customer/recovery/request", new RecoveryRequest(email));
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var proof = fixture.Delivery.ProofFor(accountId);
        Assert.NotNull(proof);
        var verify = await fixture.CreateClient().PostAsJsonAsync("/api/customer/recovery/verify",
            new RecoveryVerifyRequest(email, proof!));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var grant = (await verify.Content.ReadFromJsonAsync<RecoveryGrantResponse>())!.ResetGrant;
        var newPassword = "New8" + Guid.NewGuid().ToString("N");
        var reset = await fixture.CreateClient().PostAsJsonAsync("/api/customer/recovery/reset",
            new RecoveryResetRequest(email, grant, newPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().PostAsJsonAsync(
            "/api/auth/sign-in", new SignInRequest(phone, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync(
            "/api/auth/sign-in", new SignInRequest(phone, newPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.CreateClient().PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(oldSession.RefreshToken))).StatusCode);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            Assert.False((await db.Users.SingleAsync(x => x.Id == accountId)).EmailConfirmed);
        }
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task UnknownAndRegisteredPublicEmailHaveSameRecoveryAnswer()
    {
        var phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999);
        var email = $"unconfirmed-{Guid.NewGuid():N}@example.test";
        var registration = await fixture.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Unconfirmed Test", phone, email, fixture.Password, fixture.Password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var accountId = (await registration.Content.ReadFromJsonAsync<RegisterResponse>())!.AccountId;
        var existing = await fixture.CreateClient().PostAsJsonAsync("/api/customer/recovery/request", new RecoveryRequest(email));
        var missing = await fixture.CreateClient().PostAsJsonAsync("/api/customer/recovery/request",
            new RecoveryRequest($"missing-{Guid.NewGuid():N}@example.test"));
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal((await existing.Content.ReadFromJsonAsync<SystemMessage>())!.Message,
            (await missing.Content.ReadFromJsonAsync<SystemMessage>())!.Message);
        Assert.NotNull(fixture.Delivery.ProofFor(accountId));
    }

    [Fact]
    public async Task EmailConfirmationRejectsExpiredAndExhaustedCodes()
    {
        async Task<(HttpClient Client, int AccountId, string Email, string Code)> RegisterAndRequestAsync()
        {
            var phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999);
            var email = $"proof-{Guid.NewGuid():N}@example.test";
            var client = fixture.CreateClient();
            var registration = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest("Proof Test", phone, email, fixture.Password, fixture.Password));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            var accountId = (await registration.Content.ReadFromJsonAsync<RegisterResponse>())!.AccountId;
            var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(phone, fixture.Password));
            var session = (await login.Content.ReadFromJsonAsync<SignInResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
                "/api/me/email-verification/request", null)).StatusCode);
            return (client, accountId, email, fixture.EmailDelivery.ProofFor(email)!);
        }

        var expired = await RegisterAndRequestAsync();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var token = await db.EmailVerificationTokens.SingleAsync(x => x.AccountId == expired.AccountId);
            token.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await expired.Client.PostAsJsonAsync(
            "/api/me/email-verification/confirm", new { code = expired.Code })).StatusCode);

        var exhausted = await RegisterAndRequestAsync();
        var wrongCode = exhausted.Code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await exhausted.Client.PostAsJsonAsync(
                "/api/me/email-verification/confirm", new { code = wrongCode })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await exhausted.Client.PostAsJsonAsync(
            "/api/me/email-verification/confirm", new { code = exhausted.Code })).StatusCode);
    }
}
