using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace DCMS.IntegrationTests;

/// <summary>
/// The real DCMS API (full ASP.NET pipeline) running in the test process on the development PostgreSQL server,
/// in one throw-away schema that is migrated once, seeded with the demo accounts and dropped at the end.
/// </summary>
public sealed class ApiHost : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string schema = "dcms_it_" + Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, string> tokens = new();

    public string Password { get; } = "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    public int BranchA { get; private set; }
    public int BranchB { get; private set; }
    public int DentistEmployeeId { get; private set; }
    public int ExamServiceA { get; private set; }

    private string Admin => Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Run infrastructure/scripts/import-development-env.ps1 first: these tests need the PostgreSQL server.");
    private string Scoped => new NpgsqlConnectionStringBuilder(Admin) { SearchPath = schema }.ConnectionString;

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
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, HttpExchangeLog>();
            services.RemoveAll<DcmsDbContext>();
            services.RemoveAll<DbContextOptions<DcmsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DcmsDbContext>>();
            services.AddDbContext<DcmsDbContext>(options => options.UseNpgsql(Scoped));
            // No e-mail, SMS or push traffic from tests.
            services.RemoveAll<IContactDeliveryAdapter>();
            services.AddSingleton<IContactDeliveryAdapter, SilentContactDelivery>();
            services.RemoveAll<IRealtimeNotifier>();
            services.AddSingleton<IRealtimeNotifier, SilentRealtime>();
        });
    }

    public async Task InitializeAsync()
    {
        await using (var connection = new NpgsqlConnection(Admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
            await create.ExecuteNonQueryAsync();
        }
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSchedulingSeeder>().SeedAsync();
        BranchA = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh thử nghiệm").Select(x => x.Id).SingleAsync();
        BranchB = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh B").Select(x => x.Id).SingleAsync();
        DentistEmployeeId = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
            where u.UserName == "dentist.demo" select e.Id).SingleAsync();
        ExamServiceA = await db.Set<SchedulingService>().Where(x => x.BranchId == BranchA && x.IsInitialExamination).Select(x => x.Id).SingleAsync();
    }

    public new async Task DisposeAsync()
    {
        Trace.Flush();
        await base.DisposeAsync();
        await using var connection = new NpgsqlConnection(Admin);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>A client for the API, signed in as the demo account (or anonymous when no name is given).</summary>
    public async Task<ApiClient> ClientAsync(string? userName = null)
    {
        var http = CreateClient();
        if (userName != null)
        {
            string? token;
            lock (tokens) tokens.TryGetValue(userName, out token);
            if (token == null)
            {
                var response = await http.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(userName, Password));
                response.EnsureSuccessStatusCode();
                token = (await response.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken;
                lock (tokens) tokens[userName] = token;
            }
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return new ApiClient(http);
    }

    /// <summary>Runs code directly against the test schema (used only to read back what the API stored).</summary>
    public async Task<T> QueryAsync<T>(Func<DcmsDbContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<DcmsDbContext>());
    }

    private sealed class SilentContactDelivery : IContactDeliveryAdapter
    {
        public RecoveryChannel DetectChannel(string contact) => contact.Contains('@') ? RecoveryChannel.Email : RecoveryChannel.Phone;
        public bool IsValid(string contact) => true;
        public string Normalize(string contact) => contact.Trim();
        public Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination, string proof, string resetLink, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class SilentRealtime : IRealtimeNotifier
    {
        public Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToAllAsync(string method, object payload, CancellationToken ct = default) => Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiHost>
{
    public const string Name = "Api";
}
