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
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Persistence;
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

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
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
            // Generous request limits so feature tests are not throttled by the shared counters.
            ["Recovery:RateLimitPerAccountPerHour"] = "200",
            ["Recovery:RateLimitPerIpPerHour"] = "2000",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DcmsDbContext>();
            services.RemoveAll<DbContextOptions<DcmsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DcmsDbContext>>();
            services.AddDbContext<DcmsDbContext>(options => options.UseNpgsql(Connection));
            // Tests use an in-memory stand-in for the S02 contact adapter so the delivered proof can
            // be read back. It never writes a proof, link or password anywhere.
            services.RemoveAll<IContactDeliveryAdapter>();
            services.AddSingleton<FakeContactDeliveryAdapter>();
            services.AddSingleton<IContactDeliveryAdapter>(provider => provider.GetRequiredService<FakeContactDeliveryAdapter>());
            services.RemoveAll<IEmailVerificationDelivery>();
            services.AddSingleton<FakeEmailVerificationDelivery>();
            services.AddSingleton<IEmailVerificationDelivery>(provider => provider.GetRequiredService<FakeEmailVerificationDelivery>());
        });
    }

    public FakeContactDeliveryAdapter Delivery => Services.GetRequiredService<FakeContactDeliveryAdapter>();
    public FakeEmailVerificationDelivery EmailDelivery => Services.GetRequiredService<FakeEmailVerificationDelivery>();

    // Test-only connection string for the isolated schema this fixture created.
    public string ConnectionString => new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Import development env before running integration tests."))
        { SearchPath = schema }.ConnectionString;

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
        // Clinical sentinel stays test-only until the medical record module is implemented.
        await using var clinicalSentinel = new NpgsqlCommand("""
            CREATE TABLE "MEDICAL_RECORD" ("Id" integer PRIMARY KEY, "Content" text NOT NULL);
            INSERT INTO "MEDICAL_RECORD" VALUES (1, 'SYNTHETIC clinical sentinel');
            """, connection);
        await clinicalSentinel.ExecuteNonQueryAsync();
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
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*), min("Content") FROM "MEDICAL_RECORD"
            """, connection);
        await using var result = await command.ExecuteReaderAsync();
        Assert.True(await result.ReadAsync());
        Assert.Equal(1L, result.GetInt64(0));
        Assert.Equal("SYNTHETIC clinical sentinel", result.GetString(1));
    }

    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); }
    // Test schemas are deliberately retained for inspection; no development data is deleted.
}

/// <summary>
/// In-memory stand-in for the shared S02 contact adapter used only by tests. It keeps the last
/// delivered proof in process memory so a test can complete verification; it never persists a
/// proof, a reset link or a password.
/// </summary>
public sealed class FakeContactDeliveryAdapter : IContactDeliveryAdapter
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, string> lastProof = [];
    public int SendCount { get; private set; }

    public RecoveryChannel DetectChannel(string? contact) =>
        !string.IsNullOrWhiteSpace(contact) && contact.Contains('@') ? RecoveryChannel.Email : RecoveryChannel.Unknown;

    public bool IsValid(string? contact) => DetectChannel(contact) != RecoveryChannel.Unknown;

    public string Normalize(string? contact) => contact?.Trim().ToLowerInvariant() ?? "";

    public Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination,
        string proof, string resetLink, CancellationToken ct)
    {
        lock (gate)
        {
            lastProof[accountId] = proof;
            SendCount++;
        }
        return Task.FromResult(true);
    }

    public string? ProofFor(int accountId)
    {
        lock (gate) { return lastProof.TryGetValue(accountId, out var proof) ? proof : null; }
    }

    public void Reset() { lock (gate) { lastProof.Clear(); SendCount = 0; } }
}

public sealed record DevelopmentProofResponse(int AccountId, string Proof);

public sealed class FakeEmailVerificationDelivery : IEmailVerificationDelivery
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, string> proofs = [];
    public bool Fail { get; set; }
    public Task<bool> SendAsync(string destination, string code, int lifetimeMinutes, CancellationToken ct)
    {
        if (Fail) return Task.FromResult(false);
        lock (gate) proofs[destination.ToLowerInvariant()] = code;
        return Task.FromResult(true);
    }
    public string? ProofFor(string destination)
    {
        lock (gate) return proofs.GetValueOrDefault(destination.ToLowerInvariant());
    }
}

/// <summary>
/// Adapter that records every attempt, used to show the development adapter is not reachable
/// outside Development.
/// </summary>
public sealed class RefusedAdapter : IContactDeliveryAdapter
{
    public static List<string> Attempts { get; } = [];
    public RecoveryChannel DetectChannel(string? contact) => RecoveryChannel.Unknown;
    public bool IsValid(string? contact) => false;
    public string Normalize(string? contact) => "";
    public Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination,
        string proof, string resetLink, CancellationToken ct)
    {
        lock (Attempts) { Attempts.Add($"{accountId}:{destination}"); }
        return Task.FromResult(true);
    }
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
            fixture.Password, "Nhân viên kiểm thử", "TEST-" + suffix, suffix + "@example.invalid", null, roles ?? [AccountRoles.Dentist]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AccountChange>())!;
        Assert.Equal("MSG14", result.Notification.Code);
        return result.Account;
    }

    private static Task<HttpResponseMessage> Update(HttpClient admin, AccountDto account, string[] roles) =>
        admin.PutAsJsonAsync($"/api/accounts/{account.Id}", new UpdateStaffRequest(account.Version, account.UserName,
            account.FullName, account.Email, account.Phone, roles, "Kiểm thử đổi vai trò"));

    private static async Task AssertMessage(HttpResponseMessage response, HttpStatusCode status, string code, string field = "Thông tin")
    {
        Assert.Equal(status, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal(code == "MSG139" && message.Field != null
            ? StaffAccountConflicts.ForField(message.Field).Message : SystemMessages.Get(code, field).Message, message.Message);
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

    [Theory]
    [InlineData("userName")]
    [InlineData("employeeCode")]
    [InlineData("email")]
    [InlineData("phone")]
    public async Task DuplicateStaffFieldIsIdentifiedWithoutPartialWrites(string field)
    {
        var (admin, _) = await Login();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var phone = "09" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10000000, 100000000);
        var original = new CreateStaffRequest("field." + suffix, fixture.Password, "Conflict field test",
            "FIELD-" + suffix, suffix + "@example.invalid", phone, [AccountRoles.Dentist]);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/accounts", original)).StatusCode);
        var next = original with { UserName = "next." + suffix, EmployeeCode = "NEXT-" + suffix,
            Email = "next." + suffix + "@example.invalid", Phone = null };
        next = field switch
        {
            "userName" => next with { UserName = original.UserName.ToUpperInvariant() },
            "employeeCode" => next with { EmployeeCode = original.EmployeeCode.ToLowerInvariant() },
            "email" => next with { Email = original.Email!.ToUpperInvariant() },
            "phone" => next with { Phone = phone.Insert(4, "- ") },
            _ => throw new InvalidOperationException()
        };
        var before = await admin.GetFromJsonAsync<AccountPage>("/api/accounts?pageSize=100");
        var response = await admin.PostAsJsonAsync("/api/accounts", next);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = (await response.Content.ReadFromJsonAsync<SystemMessage>())!;
        Assert.Equal("MSG139", conflict.Code);
        Assert.Equal(field, conflict.Field);
        Assert.Equal(StaffAccountConflicts.ForField(field).Message, conflict.Message);
        Assert.Equal(before!.Total, (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?pageSize=100"))!.Total);
    }

    [Fact]
    public async Task StaffCreationAcceptsEightDigitPassword()
    {
        var (admin, _) = await Login();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var name = "numeric." + suffix;
        var created = await admin.PostAsJsonAsync("/api/accounts", new CreateStaffRequest(name,
            "12345678", "Numeric password test", "NUM-" + suffix,
            suffix + "@example.invalid", null, [AccountRoles.Dentist]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.CreateClient().PostAsJsonAsync("/api/auth/sign-in",
            new SignInRequest(name, "12345678"))).StatusCode);
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
        var duplicate = new CreateStaffRequest(account.UserName.ToUpperInvariant(), fixture.Password, "Duplicate", "OTHER-" + account.Id, "duplicate-" + account.Id + "@example.invalid", null, [AccountRoles.Dentist]);
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
        await AssertMessage(await admin.PutAsJsonAsync($"/api/accounts/{guest.Id}", new UpdateStaffRequest(guest.Version,
            guest.UserName, guest.FullName, "guest-edit@example.invalid", null, [AccountRoles.Admin], "Denied")), HttpStatusCode.Forbidden, "MSG06");
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
        var request = new CreateStaffRequest("not valid", fixture.Password, "Invalid", "INVALID", "invalid@example.invalid", null, [AccountRoles.Dentist]);
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
        var (guest, _) = await Login("guest.demo");
        var (owner, _) = await Login("owner.demo");
        var (assistant, _) = await Login("assistant.demo");
        foreach (var client in new[] { fixture.CreateClient(), dentist, guest, owner, assistant })
        {
            var status = client.DefaultRequestHeaders.Authorization == null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            var code = status == HttpStatusCode.Unauthorized ? "MSG07" : "MSG06";
            await AssertMessage(await client.GetAsync("/api/accounts/roles"), status, code);
            await AssertMessage(await client.GetAsync("/api/accounts/catalog"), status, code);
            await AssertMessage(await client.GetAsync($"/api/accounts/{target.Id}"), status, code);
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

    [Fact]
    public async Task ListSearchesCodeNameEmailPhoneAndFiltersAssignedRoleAndStateWithServerSorting()
    {
        var (admin, _) = await Login();
        var first = await Create(admin, [AccountRoles.ClinicOwner]);
        var phone = "+" + RandomNumberGenerator.GetInt32(100000000, 999999999).ToString() + "77";
        var response = await admin.PutAsJsonAsync($"/api/accounts/{first.Id}", new UpdateStaffRequest(first.Version,
            first.UserName, "S04-list-" + first.Id, first.Email, phone, first.Roles, "Search setup"));
        first = (await response.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        foreach (var term in new[] { first.Id.ToString(), first.EmployeeCode!, first.FullName, first.Email!, phone.Insert(4, "-") })
        {
            var found = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={Uri.EscapeDataString(term)}"))!;
            Assert.Contains(found.Items, account => account.Id == first.Id);
        }
        var owners = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={first.UserName}&role=ClinicOwner&status=Active"))!;
        Assert.Single(owners.Items);
        var dentist = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={first.UserName}&role=Dentist"))!;
        Assert.Empty(dentist.Items); // Role filter means assigned roles, not inherited capabilities.
        var locked = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={first.UserName}&status=Locked"))!;
        Assert.Empty(locked.Items);
        await admin.PutAsJsonAsync($"/api/accounts/{first.Id}/state", new AccountStateRequest(first.Version, true, "Filter test"));
        locked = (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={first.UserName}&status=Locked"))!;
        Assert.Single(locked.Items);
        var page1 = (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?sortBy=fullName&sortDirection=asc&pageSize=2"))!;
        var page2 = (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?sortBy=fullName&sortDirection=asc&pageSize=2&page=2"))!;
        Assert.Equal(page1.Total, page2.Total);
        Assert.Empty(page1.Items.Select(x => x.Id).Intersect(page2.Items.Select(x => x.Id)));
        var descending = (await admin.GetFromJsonAsync<AccountPage>("/api/accounts?sortBy=id&sortDirection=desc&pageSize=2"))!;
        Assert.True(descending.Items[0].Id > descending.Items[1].Id);
        foreach (var field in new[] { "employeeCode", "userName", "email", "phone", "isActive", "updatedAt" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/accounts?sortBy={field}&sortDirection=desc&pageSize=1")).StatusCode);
    }

    [Fact]
    public async Task InvalidListQueriesAndMissingDetailUseSpecifiedMessages()
    {
        var (admin, _) = await Login();
        foreach (var (query, field) in new[] { ("role=Unknown", "Vai trò"), ("status=Deleted", "Trạng thái"),
            ("sortBy=PasswordHash", "Sắp xếp"), ("sortDirection=sideways", "Sắp xếp"), ("page=0", "Phân trang hoặc tìm kiếm"),
            ("pageSize=101", "Phân trang hoặc tìm kiếm"), ("search=" + new string('a', 161), "Phân trang hoặc tìm kiếm") })
            await AssertMessage(await admin.GetAsync("/api/accounts?" + query), HttpStatusCode.BadRequest, "MSG04", field);
        await AssertMessage(await admin.GetAsync("/api/accounts/2147483647"), HttpStatusCode.NotFound, "MSG01");
        await AssertMessage(await admin.GetAsync("/api/accounts/2147483647/audit"), HttpStatusCode.NotFound, "MSG01");
    }

    [Fact]
    public async Task DetailAndAuditHaveActorAndDatesWithoutCredentialsAndNoWriteAuditEndpoint()
    {
        var (admin, _) = await Login();
        var account = await Create(admin);
        var response = await admin.GetAsync($"/api/accounts/{account.Id}");
        var detail = (await response.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal(account.Id, detail.Id); Assert.Equal(account.Version, detail.Version);
        Assert.Equal(account.Email, detail.Email); Assert.Equal(account.Roles, detail.Roles);
        Assert.NotEqual(default, detail.CreatedAt); Assert.Equal(detail.CreatedAt, detail.UpdatedAt);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
        var audit = (await admin.GetFromJsonAsync<AccountAuditDto[]>($"/api/accounts/{account.Id}/audit"))!;
        Assert.Single(audit); Assert.Equal("admin.demo", audit[0].ActorUserName); Assert.NotEmpty(audit[0].ActorFullName);
        Assert.DoesNotContain(fixture.Password, audit[0].AfterJson);
        Assert.DoesNotContain("PasswordHash", audit[0].AfterJson);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PutAsJsonAsync($"/api/accounts/{account.Id}/audit", new { reason = "Forbidden" })).StatusCode);
    }

    [Theory]
    [InlineData(AccountRoles.Admin)]
    [InlineData(AccountRoles.Dentist)]
    [InlineData(AccountRoles.ClinicAssistant)]
    [InlineData(AccountRoles.ClinicOwner)]
    public async Task AdminCanCreateEachStaffRoleWithoutAffectingPatientRecords(string role)
    {
        var (admin, _) = await Login();
        Assert.Equal([role], (await Create(admin, [role])).Roles);
        await fixture.AssertPatientUnchangedAsync();
    }

    [Fact]
    public async Task DuplicateNormalizedContactsAreRejectedOnCreateAndUpdateWithNoPartialAudit()
    {
        var (admin, _) = await Login();
        var original = await Create(admin);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var duplicate = new CreateStaffRequest("contact." + suffix, fixture.Password, "Duplicate contact", "CONTACT-" + suffix,
            " " + original.Email!.ToUpperInvariant() + " ", null, [AccountRoles.Dentist]);
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", duplicate), HttpStatusCode.Conflict, "MSG139");
        var other = await Create(admin);
        await AssertMessage(await admin.PutAsJsonAsync($"/api/accounts/{other.Id}", new UpdateStaffRequest(other.Version,
            other.UserName, other.FullName, original.Email, null, other.Roles, "Duplicate email")), HttpStatusCode.Conflict, "MSG139");
        var phone = "+" + RandomNumberGenerator.GetInt32(100000000, 999999999) + "88";
        var changed = await admin.PutAsJsonAsync($"/api/accounts/{original.Id}", new UpdateStaffRequest(original.Version,
            original.UserName, original.FullName, original.Email, phone, original.Roles, "Phone setup"));
        original = (await changed.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", duplicate with { Email = null, Phone = phone.Insert(4, "- ") }), HttpStatusCode.Conflict, "MSG139");
        await AssertMessage(await admin.PutAsJsonAsync($"/api/accounts/{other.Id}", new UpdateStaffRequest(other.Version,
            other.UserName, other.FullName, null, phone.Insert(3, " (" ) + ")", other.Roles, "Duplicate phone")), HttpStatusCode.Conflict, "MSG139");
        Assert.Equal(HttpStatusCode.OK, (await Update(admin, original, original.Roles)).StatusCode); // Own contact is allowed.
        var audit = (await admin.GetFromJsonAsync<AccountAuditDto[]>($"/api/accounts/{other.Id}/audit"))!;
        Assert.Single(audit);
        Assert.Equal(other.Version, (await admin.GetFromJsonAsync<AccountDto>($"/api/accounts/{other.Id}"))!.Version);
    }

    [Fact]
    public async Task ConcurrentDuplicateContactsCommitOnlyOneAccount()
    {
        var (admin, _) = await Login(); var (second, _) = await Login();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var first = new CreateStaffRequest("race.a." + suffix, fixture.Password, "Race", "RACE-A-" + suffix,
            suffix + "@example.invalid", null, [AccountRoles.Dentist]);
        var results = await Task.WhenAll(admin.PostAsJsonAsync("/api/accounts", first),
            second.PostAsJsonAsync("/api/accounts", first with { UserName = "race.b." + suffix, EmployeeCode = "RACE-B-" + suffix }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        var denied = Assert.Single(results, x => x.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Contains((await denied.Content.ReadFromJsonAsync<SystemMessage>())!.Code, new[] { "MSG139", "MSG111" });
        Assert.Equal(1, (await admin.GetFromJsonAsync<AccountPage>($"/api/accounts?search={suffix}@example.invalid"))!.Total);
    }

    [Fact]
    public async Task ContactRequiredValidationAndConfirmationFlagsRemainUnverified()
    {
        var (admin, _) = await Login();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var request = new CreateStaffRequest("required." + suffix, fixture.Password, "Required", "REQ-" + suffix, null, null, [AccountRoles.Dentist]);
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", request), HttpStatusCode.BadRequest, "MSG02", "Email hoặc số điện thoại");
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", request with { Email = "not-email" }), HttpStatusCode.BadRequest, "MSG04", "Email");
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", request with { Phone = "letters1234" }), HttpStatusCode.BadRequest, "MSG04", "Số điện thoại");
        await AssertMessage(await admin.PostAsJsonAsync("/api/accounts", request with { Phone = "-------" }), HttpStatusCode.BadRequest, "MSG04", "Số điện thoại");
        var response = await admin.PostAsJsonAsync("/api/accounts", request with { Email = suffix + "@example.invalid" });
        var account = (await response.Content.ReadFromJsonAsync<AccountChange>())!.Account;
        using var scope = fixture.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<DcmsDbContext>().Users.SingleAsync(x => x.Id == account.Id);
        Assert.False(user.EmailConfirmed); Assert.False(user.PhoneNumberConfirmed);
        await fixture.AssertPatientUnchangedAsync();
    }
}
