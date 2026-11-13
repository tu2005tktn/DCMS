using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class AccountSessionStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, SignInResponse Session)> Login()
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("admin.demo", fixture.Password, "SYNTHETIC"));
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session);
    }

    [Fact]
    public async Task ConcurrentRefreshRotatesOnceAndReuseRevokesAllAccountSessions()
    {
        var (first, one) = await Login(); var (second, two) = await Login();
        using (first) using (second) using (var anonymous = fixture.CreateClient())
        {
            var sessions = await first.GetFromJsonAsync<SessionInfo[]>("/api/auth/sessions");
            Assert.Contains(sessions!, x => x.SessionId == one.SessionId);
            Assert.Contains(sessions!, x => x.SessionId == two.SessionId);
            var replies = await Task.WhenAll(
                anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(one.RefreshToken)),
                anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(one.RefreshToken)));
            Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Unauthorized);
            Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var owner = await db.Users.SingleAsync(x => x.Id == one.User.Id);
            Assert.All(owner.Sessions, x => Assert.NotNull(x.RevokedAt));
            Assert.DoesNotContain(owner.Sessions.SelectMany(x => x.RefreshTokens), x => x.Status == "ACTIVE");
        }
    }

    [Fact]
    public async Task InteractiveActivityAndIdleExpiryPersistInsideTheAccount()
    {
        var (client, session) = await Login();
        using (client)
        {
            var status = (await client.GetFromJsonAsync<StaffIdleStatusDto>("/api/auth/session/idle"))!;
            var key = Guid.NewGuid();
            var command = new StaffIdleActivityRequest(status.ActivityVersion, key);
            var response = await client.PostAsJsonAsync("/api/auth/session/activity", command);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/session/activity", command)).StatusCode);
            using (var scope = fixture.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
                var user = await db.Users.SingleAsync(x => x.Id == session.User.Id);
                user.Sessions.Single(x => x.Id == session.SessionId).LastInteractiveActivityAt = DateTimeOffset.UtcNow.AddMinutes(-121);
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            using var check = fixture.Services.CreateScope();
            var persisted = (await check.ServiceProvider.GetRequiredService<DcmsDbContext>().Users.SingleAsync(x => x.Id == session.User.Id))
                .Sessions.Single(x => x.Id == session.SessionId);
            Assert.NotNull(persisted.RevokedAt);
            Assert.All(persisted.RefreshTokens, x => Assert.Equal("REVOKED", x.Status));
        }
    }
}
