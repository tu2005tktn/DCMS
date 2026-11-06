using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Development-only sample labs and lab work types. Rules: docs/specs/LAB-CATALOG-SEED.md. No prices or warranty.
public sealed class DevelopmentLabSeeder(DcmsDbContext db, TimeProvider clock)
{
    private static readonly (string Code, string Name)[] Labs = [("LAB-01", "Xưởng labo mẫu 1"), ("LAB-02", "Xưởng labo mẫu 2")];
    private static readonly (string Name, string Unit)[] WorkTypes =
    [
        ("Mão sứ kim loại", "Tooth"), ("Mão toàn sứ Zirconia", "Tooth"), ("Mão trên Implant", "Tooth"), ("Cầu răng sứ kim loại", "Tooth"),
        ("Hàm tháo lắp nhựa", "Jaw"), ("Mặt dán sứ Veneer", "Tooth"), ("Hàm duy trì sau niềng", "Jaw"),
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var actor = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id where u.UserName == "dentist.demo" select u.Id).SingleOrDefaultAsync(ct);
        if (actor == 0) throw new InvalidOperationException("Chưa có tài khoản dentist.demo: chạy --seed-demo trước.");
        var now = clock.GetUtcNow();
        var providers = await db.Set<CatalogProvider>().ToListAsync(ct); var addedLabs = 0;
        foreach (var (code, name) in Labs.Where(l => providers.All(p => p.Code != l.Code)))
        {
            var row = new CatalogProvider { Code = code, Name = name, NormalizedName = name.ToUpperInvariant(), Type = "Lab", CreatedAt = now, CreatedBy = actor, UpdatedAt = now, UpdatedBy = actor };
            db.Add(row); providers.Add(row); addedLabs++;
        }
        var labRows = Labs.Select(l => providers.First(p => p.Code == l.Code)).ToArray();
        var types = await db.LabWorkTypes.ToListAsync(ct); var addedTypes = 0; var index = 0;
        foreach (var (name, unit) in WorkTypes)
        {
            index++;
            if (types.Any(t => t.NormalizedName == name.ToUpperInvariant())) continue;
            db.Add(new LabWorkType { Code = $"LW-{index:00}", Name = name, NormalizedName = name.ToUpperInvariant(), ProviderId = labRows[(index - 1) % labRows.Length].Id, Unit = unit,
                CreatedAt = now, CreatedBy = actor, UpdatedAt = now, UpdatedBy = actor });
            addedTypes++;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return $"Công ty labo: thêm {addedLabs}. Loại công việc labo: thêm {addedTypes}/{WorkTypes.Length}.";
    }
}
