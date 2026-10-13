using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string Password { get; } = "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
    private readonly string schema = "dcms_test_" + Guid.NewGuid().ToString("N");
    private string Connection => new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Import development env before running integration tests."))
        { SearchPath = schema }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["DevSeed:Password"] = Password,
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DcmsDbContext>();
            services.RemoveAll<DbContextOptions<DcmsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DcmsDbContext>>();
            services.AddDbContext<DcmsDbContext>(options => options.UseNpgsql(Connection));
        });
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection)) await create.ExecuteNonQueryAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Database.MigrateAsync();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Roles
            .AnyAsync(x => x.NormalizedName == "GUEST"));
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentPatientLinkSeeder>().SeedAsync();
        // Sentinel Patient and REVOKED link: account administration must never change them.
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var guestId = await db.Users.Where(x => x.UserName == "guest.demo").Select(x => x.Id).SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var patient = new PatientProfile { PatientCode = "SENTINEL-1", FullName = "SYNTHETIC patient sentinel", CreatedAt = now, UpdatedAt = now };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var link = new PatientAccountLink
        {
            RequestCode = "LK-SENTINEL", AccountId = guestId, PatientId = patient.Id, Status = PatientLinkStatus.Revoked,
            RequestedAt = now, DeclaredFullName = patient.FullName, RevokedAt = now, DecisionReason = "Sentinel"
        };
        db.PatientAccountLinks.Add(link);
        await db.SaveChangesAsync();
        sentinel = (patient.Id, link.Id, await db.Patients.CountAsync(), await db.PatientAccountLinks.CountAsync());
    }

    private (int PatientId, int LinkId, int Patients, int Links) sentinel;

    public async Task AssertPatientUnchangedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        Assert.Equal(sentinel.Patients, await db.Patients.CountAsync());
        Assert.Equal("SYNTHETIC patient sentinel", (await db.Patients.AsNoTracking().SingleAsync(x => x.Id == sentinel.PatientId)).FullName);
        Assert.Equal(sentinel.Links, await db.PatientAccountLinks.CountAsync());
        Assert.Equal(PatientLinkStatus.Revoked, (await db.PatientAccountLinks.AsNoTracking().SingleAsync(x => x.Id == sentinel.LinkId)).Status);
    }

    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); }
    // Test schemas are deliberately retained for inspection; no development data is deleted.
}

public sealed class AccountApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, SignInResponse Session)> Login(string name = "admin.demo")
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SignInResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session);
    }

    private async Task<AccountDto> Create(HttpClient admin, string[]? roles = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var response = await admin.PostAsJsonAsync("/api/accounts", new CreateStaffRequest("test." + suffix,
            fixture.Password, "Nhân viên kiểm thử", "TEST-" + suffix, null, null, roles ?? [AccountRoles.Dentist]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AccountChange>())!;
        Assert.Equal("MSG14", result.Notification.Code);
        return result.Account;
    }

    private static Task<HttpResponseMessage> Update(HttpClient admin, AccountDto account, string[] roles) =>
        admin.PutAsJsonAsync($"/api/accounts/{account.Id}", new UpdateStaffRequest(account.Version, account.UserName,
            account.FullName, account.Email, account.Phone, roles, "Kiểm thử đổi vai trò"));

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(SystemMessages.Get(code).Message, message.Message);
        Assert.Equal(code, message.Code);
    }

    [Fact]
    public async Task ValidInvalidSignInAndCurrentUser()
    {
        var (client, session) = await Login();
        Assert.Contains(AccountRoles.Admin, session.User.Roles);
        Assert.NotEmpty(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        foreach (var credentials in new[] { new SignInRequest("admin.demo", "wrong"), new SignInRequest("missing-user", fixture.Password) })
            await AssertMessage(await client.PostAsJsonAsync("/api/auth/sign-in", credentials), HttpStatusCode.Unauthorized, "MSG09");
    }

    [Fact]
    public async Task SignOutInvalidatesOnlyThatSession()
    {
        var (client, _) = await Login("dentist.demo");
        var (other, _) = await Login("dentist.demo");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/sign-out", null)).StatusCode);
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndNonAdminCannotManageAccounts()
    {
        var anonymous = fixture.CreateClient();
        await AssertMessage(await anonymous.GetAsync("/api/accounts"), HttpStatusCode.Unauthorized, "MSG07");
        foreach (var name in new[] { "dentist.demo", "owner.demo", "assistant.demo", "guest.demo" })
        {
            var (client, _) = await Login(name);
            await AssertMessage(await client.GetAsync("/api/accounts"), HttpStatusCode.Forbidden, "MSG06");
            await AssertMessage(await client.PutAsJsonAsync("/api/accounts/1/state", new AccountStateRequest(1, true, "Denied")), HttpStatusCode.Forbidden, "MSG06");
        }
    }

    [Fact]
    public async Task OwnerInheritanceMultipleRolesAndPublicGuestAreDistinct()
    {
        var (_, owner) = await Login("owner.demo");
        Assert.Contains(AccountRoles.Dentist, owner.User.EffectiveRoles);
        Assert.DoesNotContain(AccountRoles.Admin, owner.User.EffectiveRoles);
        Assert.DoesNotContain(AccountRoles.ClinicAssistant, owner.User.EffectiveRoles);
        var (_, dual) = await Login("dual.demo");
        Assert.Contains(AccountRoles.Dentist, dual.User.Roles); Assert.Contains(AccountRoles.ClinicAssistant, dual.User.Roles);
        var (_, guest) = await Login("guest.demo");
        Assert.Equal("Public", guest.User.AccountType);
        Assert.Equal([AccountRoles.Guest], guest.User.Roles);
        Assert.False(guest.User.HasVerifiedPatientLink); Assert.DoesNotContain(AccountRoles.Patient, guest.User.EffectiveRoles);
        Assert.Equal([AccountRoles.Guest, AccountRoles.Patient], AccountRoles.Effective([AccountRoles.Patient]));
    }

    [Fact]
    public async Task LockImmediatelyDeniesExistingTokenAndUnlockDoesNotReviveIt()
    {
        var (admin, _) = await Login();
        var account = await Create(admin, [AccountRoles.Admin]);
        var (client, _) = await Login(account.UserName);
        var lockResponse = await admin.PutAsJsonAsync($"/api/accounts/{account.Id}/state", new AccountStateRequest(account.Version, true, "Test lock"));
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        var locked = (await lockResponse.Content.ReadFromJsonAsync<AccountChange>())!;
        Assert.Equal("MSG15", locked.Notification.Code);
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        await AssertMessage(await client.GetAsync("/api/accounts"), HttpStatusCode.Unauthorized, "MSG07");
        await AssertMessage(await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(account.UserName, fixture.Password)), HttpStatusCode.Unauthorized, "MSG09");
        var unlock = await admin.PutAsJsonAsync($"/api/accounts/{account.Id}/state", new AccountStateRequest(locked.Account.Version, false, "Test unlock"));
        Assert.Equal("MSG16", (await unlock.Content.ReadFromJsonAsync<AccountChange>())!.Notification.Code);
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        await Login(account.UserName);
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task RevokeAdminRoleAffectsExistingTokenAndCanGrantAgain()
    {
        var (admin, _) = await Login();
        var account = await Create(admin, [AccountRoles.Admin, AccountRoles.Dentist]);
        var (client, _) = await Login(account.UserName);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/accounts")).StatusCode);
        var response = await Update(admin, account, [AccountRoles.Dentist]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var revised = (await response.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        await AssertMessage(await client.GetAsync("/api/accounts"), HttpStatusCode.Forbidden, "MSG06");
        var current = (await client.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;
        Assert.DoesNotContain(AccountRoles.Admin, current.Roles);
        Assert.Equal(HttpStatusCode.OK, (await Update(admin, revised, [AccountRoles.Admin, AccountRoles.Dentist])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/accounts")).StatusCode);
    }

    [Fact]
    public async Task DuplicateUsernameAndEmployeeCodeRollbackCompletely()
    {
        var (admin, _) = await Login();
        var account = await Create(admin);
        var duplicate = new CreateStaffRequest(account.UserName.ToUpperInvariant(), fixture.Password, "Duplicate", "OTHER-" + account.Id, null, null, [AccountRoles.Dentist]);
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", duplicate), HttpStatusCode.Conflict, "MSG139");
        var uniqueName = "test." + Guid.NewGuid().ToString("N")[..12];
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", duplicate with { UserName = uniqueName, EmployeeCode = account.EmployeeCode! }), HttpStatusCode.Conflict, "MSG139");
        var search = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={uniqueName}"))!;
        Assert.Equal(0, search.Total);
    }

    [Fact]
    public async Task StaleUpdateAndStaleLockReturnConflictWithNoAuditOrDataChange()
    {
        var (admin, _) = await Login();
        var account = await Create(admin);
        Assert.Equal(HttpStatusCode.OK, (await Update(admin, account, [AccountRoles.Dentist, AccountRoles.ClinicAssistant])).StatusCode);
        await AssertMessage(await Update(admin, account, [AccountRoles.Admin]), HttpStatusCode.Conflict, "MSG111");
        await AssertMessage(await admin.PutAsJsonAsync($"/api/accounts/{account.Id}/state", new AccountStateRequest(account.Version, true, "Stale")), HttpStatusCode.Conflict, "MSG111");
        var audit = (await admin.GetFromJsonAsync<AccountAuditDto[]>($"/api/accounts/{account.Id}/audit"))!;
        Assert.Equal(2, audit.Length);
        Assert.All(audit, entry => { Assert.True(entry.ActorId > 0); Assert.Equal(account.Id, entry.TargetId); Assert.NotEqual(default, entry.OccurredAt); Assert.DoesNotContain("Password", entry.AfterJson); });
    }

    [Fact]
    public async Task ConcurrentWritersCannotBothCommitTheSameVersion()
    {
        var (first, _) = await Login(); var (second, _) = await Login();
        var account = await Create(first);
        var writes = await Task.WhenAll(Update(first, account, [AccountRoles.Dentist, AccountRoles.Admin]),
            Update(second, account, [AccountRoles.Dentist, AccountRoles.ClinicAssistant]));
        Assert.Single(writes, response => response.StatusCode == HttpStatusCode.OK);
        await AssertMessage(Assert.Single(writes, response => response.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "MSG111");
    }

    [Fact]
    public async Task InvalidRolesAndPublicEditAreDeniedAndPatientDataRemainsIntact()
    {
        var (admin, _) = await Login();
        var account = await Create(admin);
        foreach (var roles in new[] { new[] { AccountRoles.Patient }, new[] { AccountRoles.Guest }, new[] { "Unknown" }, Array.Empty<string>() })
            await AssertMessage(await Update(admin, account, roles), HttpStatusCode.Forbidden, "MSG06");
        var guest = (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?search=guest.demo"))!.Items.Single();
        await AssertMessage(await Update(admin, guest, [AccountRoles.Admin]), HttpStatusCode.Forbidden, "MSG06");
        var locked = await admin.PutAsJsonAsync($"/api/accounts/{guest.Id}/state", new AccountStateRequest(guest.Version, true, "Public lock"));
        var revised = (await locked.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/accounts/{guest.Id}/state", new AccountStateRequest(revised.Version, false, "Public unlock"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Update(admin, account, [AccountRoles.ClinicOwner])).StatusCode);
        await fixture.AssertPatientUnchangedAsync();
        var page = (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?page=1&pageSize=2"))!;
        Assert.Equal(2, page.Items.Count); Assert.True(page.Total > page.Items.Count);
    }

    [Fact]
    public async Task MalformedInputAndWeakPasswordUseSpecifiedMessages()
    {
        var (admin, _) = await Login();
        var request = new CreateStaffRequest("not valid", fixture.Password, "Invalid", "INVALID", null, null, [AccountRoles.Dentist]);
        var malformed = await admin.PostAsJsonAsync("/api/accounts", request);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("MSG04", (await malformed.Content.ReadFromJsonAsync<SystemMessage>())!.Code);
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", request with { UserName = "valid." + Guid.NewGuid().ToString("N")[..12], Password = "weak" }), HttpStatusCode.BadRequest, "MSG109");
    }

    [Fact]
    public async Task TamperedTokenAndExpiredSessionAreRejected()
    {
        var (client, session) = await Login("dentist.demo");
        var token = session.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^8] + "AAAAAAAA");
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await db.AuthSessions.Where(x => x.AccountId == session.User.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
    }

    [Fact]
    public async Task SignedExpiredTokenAndWrongIssuerAreRejectedAndTokenRoleIsIgnored()
    {
        var (client, session) = await Login("dentist.demo");
        using var scope = fixture.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var original = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        var claims = original.Claims.Where(x => x.Type is "sub" or "sid" or "jti").Append(new Claim(ClaimTypes.Role, AccountRoles.Admin));
        string Sign(string issuer, DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, settings.Audience, claims, DateTime.UtcNow.AddMinutes(-2), expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Sign(settings.Issuer, DateTime.UtcNow.AddMinutes(1)));
        await AssertMessage(await client.GetAsync("/api/accounts"), HttpStatusCode.Forbidden, "MSG06");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Sign(settings.Issuer, DateTime.UtcNow.AddMinutes(-1)));
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Sign("wrong-issuer", DateTime.UtcNow.AddMinutes(1)));
        await AssertMessage(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized, "MSG07");
    }

    [Fact]
    public async Task EveryManagementEndpointRequiresAdmin()
    {
        var (admin, _) = await Login();
        var target = await Create(admin);
        var (dentist, _) = await Login("dentist.demo");
        foreach (var client in new[] { fixture.CreateClient(), dentist })
        {
            var status = client.DefaultRequestHeaders.Authorization == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            var code = status == HttpStatusCode.Unauthorized ? "MSG07" : "MSG06";
            await AssertMessage(await client.GetAsync("/api/accounts/roles"), status, code);
            await AssertMessage(await client.GetAsync($"/api/accounts/{target.Id}/audit"), status, code);
            await AssertMessage(await Update(client, target, [AccountRoles.Admin]), status, code);
            await AssertMessage(await client.PostAsJsonAsync("/api/accounts", new CreateStaffRequest("denied.account", fixture.Password,
                "Denied", "DENIED", null, null, [AccountRoles.Admin])), status, code);
        }
    }

    [Fact]
    public async Task SeedRepeatPreservesExistingAccountsAndAudits()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var before = await db.Users.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PasswordHash, x.Version, x.IsActive }).ToArrayAsync();
        var auditCount = await db.AccountAudits.CountAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
        var after = await db.Users.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PasswordHash, x.Version, x.IsActive }).ToArrayAsync();
        Assert.Equal(before, after);
        Assert.Equal(auditCount, await db.AccountAudits.CountAsync());
    }
}
