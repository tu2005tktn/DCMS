using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class BookingPrefillApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task SignInAndMeReturnCurrentAccountsOwnContactDetails()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("guest.demo", fixture.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var me = (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;
        using var scope = fixture.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Users.AsNoTracking()
            .SingleAsync(x => x.UserName == "guest.demo");
        Assert.Equal(account.Id, me.Id);
        Assert.Equal(account.FullName, me.FullName);
        Assert.Equal(account.PhoneNumber, me.PhoneNumber);
        Assert.Equal(account.Email, me.Email);
        Assert.Equal(me.PhoneNumber, session.User.PhoneNumber);
        Assert.Equal(me.Email, session.User.Email);
    }
}
