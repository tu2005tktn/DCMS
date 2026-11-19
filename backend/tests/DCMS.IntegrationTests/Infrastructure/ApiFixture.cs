using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Accounts;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Features.Notifications;
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
            services.RemoveAll<IRealtimeNotifier>();
            services.AddSingleton<FakeRealtimeNotifier>();
            services.AddSingleton<IRealtimeNotifier>(provider => provider.GetRequiredService<FakeRealtimeNotifier>());
        });
    }

    public FakeContactDeliveryAdapter Delivery => Services.GetRequiredService<FakeContactDeliveryAdapter>();
    public FakeEmailVerificationDelivery EmailDelivery => Services.GetRequiredService<FakeEmailVerificationDelivery>();
    public FakeRealtimeNotifier Realtime => Services.GetRequiredService<FakeRealtimeNotifier>();

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

public sealed record RealtimeCall(int? AccountId, int? BranchId, string Method, JsonElement Payload);

public sealed class FakeRealtimeNotifier : IRealtimeNotifier
{
    private readonly ConcurrentQueue<RealtimeCall> calls = new();
    public IReadOnlyList<RealtimeCall> Calls => calls.ToArray();

    public Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default)
    {
        calls.Enqueue(new RealtimeCall(accountId, null, method, JsonSerializer.SerializeToElement(payload)));
        return Task.CompletedTask;
    }

    public Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default)
    {
        calls.Enqueue(new RealtimeCall(null, branchId, method, JsonSerializer.SerializeToElement(payload)));
        return Task.CompletedTask;
    }

    public Task SendToAllAsync(string method, object payload, CancellationToken ct = default)
    {
        calls.Enqueue(new RealtimeCall(null, null, method, JsonSerializer.SerializeToElement(payload)));
        return Task.CompletedTask;
    }
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
