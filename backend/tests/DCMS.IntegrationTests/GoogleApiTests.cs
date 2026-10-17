using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class GoogleApiFixture : ApiFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier, StubGoogleVerifier>();
        });
    }
}

internal sealed class StubGoogleVerifier : IGoogleTokenVerifier
{
    public Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct)
    {
        var fields = idToken.Split('|');
        if (fields.Length != 3 || fields.Any(string.IsNullOrWhiteSpace))
            throw new DCMS.Application.Common.BusinessException(401,
                new DCMS.Application.Common.SystemMessage("GOOGLE_INVALID_TOKEN", "Invalid Google token"));
        return Task.FromResult(new GoogleIdentity(fields[0], fields[1], fields[2]));
    }
}

public sealed class GoogleApiTests(GoogleApiFixture fixture) : IClassFixture<GoogleApiFixture>
{
    private static string Email() => $"google{Guid.NewGuid():N}@example.com";
    private static string Token(string subject, string email) => $"{subject}|{email}|Người dùng Google";
    private static string Phone() => "09" + RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);

    [Fact]
    public async Task NewGoogleIdentityCanSignInAgainAfterSignOutWithoutCreatingAnotherAccount()
    {
        using var client = fixture.CreateClient();
        var email = Email();
        var token = Token(Guid.NewGuid().ToString("N"), email);
        var first = await client.PostAsJsonAsync("/api/auth/google/sign-in", new GoogleSignInRequest(token));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var signedIn = (await first.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.Equal("Public", signedIn.User.AccountType);
        Assert.Equal([AccountRoles.Guest], signedIn.User.Roles);
        Assert.False(signedIn.User.HasVerifiedPatientLink);
        Assert.Empty(signedIn.User.Permissions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedIn.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/sign-out", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        var second = await client.PostAsJsonAsync("/api/auth/google/sign-in", new GoogleSignInRequest(token));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var signedInAgain = (await second.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.Equal(signedIn.User.Id, signedInAgain.User.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signedInAgain.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var account = await db.Users.SingleAsync(x => x.Id == signedIn.User.Id);
        Assert.Null(account.PhoneNumber);
        Assert.Null(account.PasswordHash);
        Assert.True(account.EmailConfirmed);
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
        Assert.False(await db.Employees.AnyAsync(x => x.AccountId == account.Id));
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task ExistingLocalEmailCreatesSeparateGoogleAccount()
    {
        using var client = fixture.CreateClient();
        var email = Email();
        var phone = Phone();
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Tài khoản cũ", phone, email, fixture.Password, fixture.Password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var token = Token(Guid.NewGuid().ToString("N"), email);
        var passwordSignIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(phone, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, passwordSignIn.StatusCode);
        var local = (await passwordSignIn.Content.ReadFromJsonAsync<SignInResponse>())!;

        var googleSignIn = await client.PostAsJsonAsync("/api/auth/google/sign-in", new GoogleSignInRequest(token));
        Assert.Equal(HttpStatusCode.OK, googleSignIn.StatusCode);
        var google = (await googleSignIn.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.NotEqual(local.User.Id, google.User.Id);
        Assert.Equal("Public", google.User.AccountType);
        Assert.Equal([AccountRoles.Guest], google.User.Roles);
        Assert.False(google.User.HasVerifiedPatientLink);
        Assert.Empty(google.User.Permissions);

        var repeat = await client.PostAsJsonAsync("/api/auth/google/sign-in", new GoogleSignInRequest(token));
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(google.User.Id, (await repeat.Content.ReadFromJsonAsync<SignInResponse>())!.User.Id);

        var localAgain = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(phone, fixture.Password));
        Assert.Equal(local.User.Id, (await localAgain.Content.ReadFromJsonAsync<SignInResponse>())!.User.Id);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(2, await db.Users.CountAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
        Assert.False((await db.Users.SingleAsync(x => x.Id == local.User.Id)).EmailConfirmed);
        Assert.True((await db.Users.SingleAsync(x => x.Id == google.User.Id)).EmailConfirmed);
        Assert.False(await db.PatientAccountLinks.AnyAsync(x => x.AccountId == google.User.Id));
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task GoogleLinkEndpointIsNotAvailable()
    {
        using var client = fixture.CreateClient();
        var token = Token(Guid.NewGuid().ToString("N"), Email());
        var signIn = await client.PostAsJsonAsync("/api/auth/google/sign-in", new GoogleSignInRequest(token));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        var response = await client.PostAsJsonAsync("/api/auth/google/link", new { idToken = token });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
