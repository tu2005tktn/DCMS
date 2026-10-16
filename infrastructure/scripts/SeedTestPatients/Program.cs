// Công cụ seed tài khoản test Patient/Guest. Idempotent — chạy nhiều lần không tạo trùng.
// Dùng: .\infrastructure\scripts\seed-test-patients.ps1
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();
builder.Configuration["ConnectionStrings:DefaultConnection"] =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not set.");
builder.Services.AddDbContext<DcmsDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<ApplicationUser>(o => { })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<DcmsDbContext>();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

foreach (var role in AccountRoles.All)
    if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<int>(role));

const string password = "Patient#Test2026";
(string Name, string FullName, string AccountType, string[] SeedRoles)[] seeds =
[
    ("patient.demo",  "Bệnh nhân demo",   "Public", [AccountRoles.Patient]),
    ("patient2.demo", "Bệnh nhân demo 2", "Public", [AccountRoles.Patient, AccountRoles.Guest])
];
foreach (var (name, fullName, accountType, seedRoles) in seeds)
{
    var existing = await users.FindByNameAsync(name);
    if (existing != null) { Console.WriteLine($"[skip] {name}"); continue; }
    var now = DateTimeOffset.UtcNow;
    var user = new ApplicationUser
    {
        UserName = name, FullName = fullName, AccountType = accountType, CreatedAt = now, UpdatedAt = now
    };
    var createResult = await users.CreateAsync(user, password);
    if (!createResult.Succeeded)
    {
        Console.WriteLine($"[fail] {name}: {string.Join(", ", createResult.Errors.Select(e => e.Code))}");
        continue;
    }
    var roleResult = await users.AddToRolesAsync(user, seedRoles);
    if (!roleResult.Succeeded)
    {
        Console.WriteLine($"[fail] {name} roles: {string.Join(", ", roleResult.Errors.Select(e => e.Code))}");
        continue;
    }
    db.AccountAudits.Add(new AccountAudit
    {
        ActorId = user.Id, TargetId = user.Id, Action = "DevelopmentSeed", Reason = "Test patient seed",
        OccurredAt = now, AfterJson = "{\"testPatient\":true}"
    });
    await db.SaveChangesAsync();
    Console.WriteLine($"[ok]  {name} ({fullName}) AccountType={accountType} roles=[{string.Join(",", seedRoles)}] password='{password}'");
}
Console.WriteLine("\nDONE.");
