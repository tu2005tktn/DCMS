using System.Text.RegularExpressions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

var schema = Environment.GetEnvironmentVariable("DCMS_RECEPTION_E2E_SCHEMA") ?? "";
if (!Regex.IsMatch(schema, "^dcms_reception_e2e_[a-z0-9]+$")) throw new InvalidOperationException("Use prepare-reception-e2e.ps1.");
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "backend/src/DCMS.Api/appsettings.json"))
    .AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Jwt:Issuer"] = "DCMS", ["Jwt:Audience"] = "DCMS", ["DevSeed:Enabled"] = "true"
}).AddEnvironmentVariables().Build();
var connectionString = config.GetConnectionString("DefaultConnection")!;
if (new NpgsqlConnectionStringBuilder(connectionString).SearchPath != schema) throw new InvalidOperationException($"Isolated schema required; expected {schema}; search path {new NpgsqlConnectionStringBuilder(connectionString).SearchPath ?? "(null)"}.");
await using (var connection = new NpgsqlConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
    await command.ExecuteNonQueryAsync();
}
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddLogging(); services.AddDataProtection(); services.AddInfrastructure(config, new DevelopmentEnvironment());
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
await db.Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<DevelopmentAccountSeeder>().SeedAsync();
await scope.ServiceProvider.GetRequiredService<DevelopmentSchedulingSeeder>().SeedAsync();
var branch = await db.Set<SchedulingBranch>().SingleAsync();
if (await db.Set<SchedulingChair>().CountAsync() != 3) throw new InvalidOperationException("Exactly three physical fixture chairs required.");
var now = DateTimeOffset.UtcNow;
var date = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
foreach (var member in await db.Set<SchedulingMember>().ToArrayAsync())
{
    var week = new WorkingWeek { BranchId = branch.Id, EmployeeId = member.EmployeeId, WeekStart = monday,
        PublishedBy = (await db.Employees.SingleAsync(x => x.Id == member.EmployeeId)).AccountId, PublishedAt = now };
    for (var day = 0; day < 7; day++) week.Periods.Add(new WorkingPeriod { WeekId = week.Id, Day = day, StartMinute = 0, EndMinute = 1440 });
    db.Add(week);
}
foreach (var chair in await db.Set<SchedulingChair>().ToArrayAsync()) { chair.OpenMinute = 0; chair.CloseMinute = 1440; }
await db.SaveChangesAsync();
Console.WriteLine($"Prepared synthetic schema {schema}; branch {branch.Id}; chairs 3; clinic date {date:yyyy-MM-dd}");

sealed class DevelopmentEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "DCMS.ReceptionE2E";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
