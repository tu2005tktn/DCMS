using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.PatientLinks;

// Development-only synthetic data for UC-36. Real Patients are created at check-in (UC-07), never here in production.
public sealed class DevelopmentPatientLinkSeeder(DcmsDbContext db, UserManager<ApplicationUser> users, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var password = configuration["DevSeed:Password"];
        if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Set DevSeed__Password to seed demo accounts.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var (name, fullName, email) in new[]
        {
            ("patient.demo", "Nguyễn Văn An", "patient.demo@example.invalid"),
            ("patient2.demo", "Trần Thị Bình", "patient2.demo@example.invalid")
        })
        {
            // Repeatable seed: existing accounts keep their credentials, states and roles.
            if (await users.FindByNameAsync(name) != null) continue;
            var user = new ApplicationUser
            {
                UserName = name, FullName = fullName, Email = email, EmailConfirmed = true,
                AccountType = "Public", CreatedAt = now, UpdatedAt = now
            };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRolesAsync(user, [AccountRoles.Patient]));
            db.AccountAudits.Add(new AccountAudit
            {
                ActorId = user.Id, TargetId = user.Id, Action = "DevelopmentSeed", Reason = "Dữ liệu giả development",
                OccurredAt = now, AfterJson = "{\"developmentDemo\":true}"
            });
        }
        // BN-DEMO-0001 and BN-DEMO-0002 share a phone on purpose: a shared phone must never establish a link.
        PatientProfile[] patients =
        [
            new() { PatientCode = "BN-DEMO-0001", FullName = "Nguyễn Văn An", BirthDate = new DateOnly(1990, 5, 12), Gender = "MALE", Phone = "0900000001" },
            new() { PatientCode = "BN-DEMO-0002", FullName = "Trần Thị Bình", BirthDate = new DateOnly(1985, 11, 3), Gender = "FEMALE", Phone = "0900000001" },
            new() { PatientCode = "BN-DEMO-0003", FullName = "Lê Minh Châu", Gender = "FEMALE", Phone = "0900000003" }
        ];
        foreach (var patient in patients)
        {
            if (await db.Patients.AnyAsync(x => x.PatientCode == patient.PatientCode, ct)) continue;
            patient.CreatedAt = patient.UpdatedAt = now;
            patient.NormalizedName = PatientLinkSupport.NormalizeName(patient.FullName);
            db.Patients.Add(patient);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo seed failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
