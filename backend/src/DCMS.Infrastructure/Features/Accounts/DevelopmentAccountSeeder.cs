using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed class DevelopmentAccountSeeder(DcmsDbContext db, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<int>> roles, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var password = configuration["DevSeed:Password"];
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Set DevSeed__Password to seed demo accounts.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var role in AccountRoles.All)
        {
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole<int>(role)));
        }
        (string Name, string FullName, string[] Roles)[] demo =
        [
            ("admin.demo", "Quản trị viên demo", [AccountRoles.Admin]),
            ("dentist.demo", "Nha sĩ demo", [AccountRoles.Dentist]),
            ("owner.demo", "Chủ phòng khám demo", [AccountRoles.ClinicOwner]),
            ("assistant.demo", "Trợ lý demo", [AccountRoles.ClinicAssistant]),
            ("dual.demo", "Nhân viên đa vai trò demo", [AccountRoles.Dentist, AccountRoles.ClinicAssistant]),
            ("guest.demo", "Tài khoản công khai demo", [AccountRoles.Guest])
        ];
        foreach (var entry in demo)
        {
            // Repeatable seed: never alter credentials, states or roles of an existing account.
            if (await users.FindByNameAsync(entry.Name) != null) continue;
            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                UserName = entry.Name, FullName = entry.FullName,
                AccountType = entry.Name == "guest.demo" ? "Public" : "Staff", CreatedAt = now, UpdatedAt = now,
                // Synthetic Development-only contact so UC-02 recovery can be exercised locally.
                // This is not real contact verification and never applies outside Development.
                Email = entry.Name + "@example.invalid", EmailConfirmed = true
            };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRolesAsync(user, entry.Roles));
            if (user.AccountType == "Staff") db.Employees.Add(new EmployeeProfile
            {
                AccountId = user.Id, EmployeeCode = entry.Name.Replace(".demo", "").ToUpperInvariant() + "-DEMO",
                FullName = user.FullName
            });
            db.AccountAudits.Add(new AccountAudit
            {
                ActorId = user.Id, TargetId = user.Id, Action = "DevelopmentSeed", Reason = "Dữ liệu giả development",
                OccurredAt = now, AfterJson = "{\"developmentDemo\":true}"
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo seed failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
