using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class RegistrationApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static string NewPhone() => "09" + RandomNumberGenerator.GetInt32(10_000_000, 100_000_000);
    private static string NewEmail(string phone) => $"guest{phone}@example.com";

    [Fact]
    public async Task SharedPolicyAdvertisesEightCharactersWithoutCompositionRules()
    {
        var policy = await fixture.CreateClient().GetFromJsonAsync<CredentialPolicy>("/api/auth/policy");
        Assert.NotNull(policy);
        Assert.Equal(8, policy.RequiredLength);
        Assert.Equal(256, policy.MaximumLength);
        Assert.False(policy.RequireDigit);
        Assert.False(policy.RequireLowercase);
        Assert.False(policy.RequireUppercase);
        Assert.False(policy.RequireNonAlphanumeric);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("abcdefgh")]
    [InlineData("ABCDEFGH")]
    [InlineData("!!!!!!!!")]
    public async Task RegistrationAcceptsEightCharactersWithoutComposition(string password)
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Password policy test", phone, NewEmail(phone), password, password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(phone, password))).StatusCode);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public async Task RegistrationEnforcesPasswordLengthBoundaries(int length, bool accepted)
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var password = new string('a', length);
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Password length test", phone, NewEmail(phone), password, password));
        if (accepted) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        else await AssertMessage(response, HttpStatusCode.BadRequest, "MSG109");
    }

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(code, message.Code);
    }

    [Fact]
    public async Task RegisterThenSignInCreatesOnlyUnverifiedGuest()
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var email = NewEmail(phone);
        var registration = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("  Nguyễn Văn A  ", phone, $"  {email}  ", fixture.Password, fixture.Password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var result = (await registration.Content.ReadFromJsonAsync<RegisterResponse>())!;
        Assert.Equal("MSG11", result.Notification.Code);
        Assert.Equal(phone, result.PhoneNumber);

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(phone, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!;
        Assert.Equal("Public", session.User.AccountType);
        Assert.Equal("Nguyễn Văn A", session.User.FullName);
        Assert.Equal([AccountRoles.Guest], session.User.Roles);
        Assert.Empty(session.User.Permissions);
        Assert.False(session.User.HasVerifiedPatientLink);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == result.AccountId);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.Equal(email, user.Email);
        Assert.Equal(email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.False(user.EmailConfirmed);
        Assert.False(await db.Employees.AnyAsync(x => x.AccountId == user.Id));
        Assert.True(await db.AccountAudits.AnyAsync(x => x.TargetId == user.Id && x.Action == "SelfRegister"));
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task RepeatedAndConcurrentRequestsCreateOneAccount()
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var request = new RegisterRequest("Khách kiểm thử", phone, NewEmail(phone), fixture.Password, fixture.Password);
        var first = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", request), HttpStatusCode.Conflict, "MSG139");

        var concurrentPhone = NewPhone();
        var concurrentRequest = request with { PhoneNumber = concurrentPhone };
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/register", concurrentRequest),
            client.PostAsJsonAsync("/api/auth/register", concurrentRequest));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.UserName == phone));
        Assert.Equal(1, await db.Users.CountAsync(x => x.UserName == concurrentPhone));
        Assert.Equal(2, await db.Users.CountAsync(x => x.Email == request.Email));
    }

    [Fact]
    public async Task InvalidFieldsAndPasswordAreRejected()
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var valid = new RegisterRequest("Khách kiểm thử", phone, NewEmail(phone), fixture.Password, fixture.Password);
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { FullName = " " }),
            HttpStatusCode.BadRequest, "MSG02");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { FullName = new string('A', 161) }),
            HttpStatusCode.BadRequest, "MSG03");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { PhoneNumber = "+84912345678" }),
            HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { PhoneNumber = "0212345678" }),
            HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { Email = " " }),
            HttpStatusCode.BadRequest, "MSG02");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { Email = new string('a', 250) + "@example.com" }),
            HttpStatusCode.BadRequest, "MSG03");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { Email = "not-an-email" }),
            HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { ConfirmPassword = "different" }),
            HttpStatusCode.BadRequest, "MSG04");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/register", valid with { Password = "weak", ConfirmPassword = "weak" }),
            HttpStatusCode.BadRequest, "MSG109");
    }

    [Fact]
    public async Task ClientSuppliedRolesCannotGrantPrivileges()
    {
        using var client = fixture.CreateClient();
        var phone = NewPhone();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Khách kiểm thử", phoneNumber = phone, email = NewEmail(phone), password = fixture.Password,
            confirmPassword = fixture.Password, accountType = "Staff", roles = new[] { "Admin", "Patient" },
            phoneNumberConfirmed = true, emailConfirmed = true, hasVerifiedPatientLink = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RegisterResponse>())!;
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == result.AccountId);
        Assert.Equal("Public", user.AccountType);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.False(user.EmailConfirmed);
        var assigned = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            where ur.UserId == user.Id select role.Name).ToArrayAsync();
        Assert.Equal([AccountRoles.Guest], assigned);
        Assert.False(await db.Employees.AnyAsync(x => x.AccountId == user.Id));
    }
}
