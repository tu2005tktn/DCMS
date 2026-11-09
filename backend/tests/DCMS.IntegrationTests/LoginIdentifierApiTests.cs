using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class LoginIdentifierApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<ApplicationUser> Create(string? username = null, string? email = null)
    {
        using var scope = fixture.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = username ?? "login." + suffix, Email = email ?? suffix + "@example.invalid",
            FullName = "SYNTHETIC login customer", AccountType = "Public", IsActive = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Guest)).Succeeded); return user;
    }

    [Theory]
    [InlineData("username")]
    [InlineData("email")]
    [InlineData("uppercase-username")]
    [InlineData("uppercase-email")]
    [InlineData("long-username")]
    [InlineData("long-email")]
    [InlineData("phone-username")]
    public async Task PublicCanSignInWithUsernameOrEmailAndNormalSession(string mode)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var name = mode == "long-username" ? "patient." + new string('a', 90) + suffix : mode == "phone-username"
            ? "09" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10000000, 100000000) : "patient." + suffix;
        var email = mode == "long-email" ? new string('e', 50) + "@" + suffix + ".example.invalid" : suffix + "@example.invalid";
        var user = await Create(name, email);
        var identifier = mode.Contains("email") ? email : name;
        if (mode.StartsWith("uppercase")) identifier = identifier.ToUpperInvariant();
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("  " + identifier + "  ", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.Equal(user.Id, session.User.Id); Assert.Equal("Public", session.User.AccountType); Assert.False(session.User.HasVerifiedPatientLink);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var current = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"); Assert.Equal(user.Id, current!.Id);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Single(await db.AuthSessions.Where(value => value.AccountId == user.Id).ToArrayAsync());
        Assert.Single(await db.AccountAudits.Where(value => value.TargetId == user.Id && value.Action == "SignIn").ToArrayAsync());
        (await client.PostAsync("/api/auth/sign-out", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-password")]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("blank")]
    [InlineData("long-missing")]
    public async Task InvalidEmailLoginDoesNotCreateSessionOrDiscloseAccountState(string scenario)
    {
        var user = await Create();
        using (var scope = fixture.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var saved = (await users.FindByIdAsync(user.Id.ToString()))!;
            if (scenario == "inactive") { saved.IsActive = false; Assert.True((await users.UpdateAsync(saved)).Succeeded); }
            if (scenario == "locked") { Assert.True((await users.SetLockoutEnabledAsync(saved, true)).Succeeded); Assert.True((await users.SetLockoutEndDateAsync(saved, DateTimeOffset.UtcNow.AddDays(1))).Succeeded); }
        }
        var identifier = scenario switch { "unknown" => Guid.NewGuid() + "@example.invalid", "blank" => "   ", "long-missing" => new string('z', 2048), _ => user.Email! };
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(identifier, scenario == "wrong-password" ? "invalid" : fixture.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal("MSG09", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        using var check = fixture.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Empty(await db.AuthSessions.Where(value => value.AccountId == user.Id).ToArrayAsync());
        Assert.Empty(await db.AccountAudits.Where(value => value.TargetId == user.Id && value.Action == "SignIn").ToArrayAsync());
    }

    [Fact]
    public async Task PublicAccountWithoutPasswordCannotBypassItsNormalSignInMethod()
    {
        var user = await Create();
        using (var scope = fixture.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.Users.FindAsync(user.Id))!.PasswordHash = null; await db.SaveChangesAsync();
        }
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(user.Email!, fixture.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal("MSG09", (await response.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        using var check = fixture.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Empty(await context.AuthSessions.Where(value => value.AccountId == user.Id).ToArrayAsync());
    }

    [Fact]
    public async Task StaffUsernameRemainsValidAndEmailFallbackIsForPublicAccounts()
    {
        using var client = fixture.CreateClient();
        var denied = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("assistant.demo@example.invalid", fixture.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("assistant.demo", fixture.Password))).StatusCode);
    }
}
