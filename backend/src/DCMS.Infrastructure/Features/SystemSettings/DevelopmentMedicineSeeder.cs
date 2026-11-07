using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Development-only starter catalog. Intentionally has no doses, default directions, prices, stock or patient prescriptions.
public sealed class DevelopmentMedicineSeeder(DcmsDbContext db, TimeProvider clock)
{
    private sealed record Item(string Code, string Name, string ActiveIngredient, string Strength, string DosageForm, string Unit, string Route);

    private static readonly Item[] Items =
    [
        new("MED-STARTER-001", "Paracetamol 500 mg", "Paracetamol", "500 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-002", "Ibuprofen 400 mg", "Ibuprofen", "400 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-003", "Amoxicillin 500 mg", "Amoxicillin", "500 mg", "Viên nang", "viên", "Uống"),
        new("MED-STARTER-004", "Metronidazole 250 mg", "Metronidazole", "250 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-005", "Chlorhexidine 0,12%", "Chlorhexidine", "0,12%", "Dung dịch súc miệng", "ml", "Súc miệng"),
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var actor = await (from e in db.Employees
                           join u in db.Users on e.AccountId equals u.Id
                           where u.UserName == "owner.demo"
                           select u.Id).SingleOrDefaultAsync(ct);
        if (actor == 0) throw new InvalidOperationException("Chưa có tài khoản owner.demo: chạy --seed-demo trước.");

        var now = clock.GetUtcNow();
        var existing = await db.Set<CatalogMedicine>().ToListAsync(ct);
        var added = 0;
        foreach (var item in Items)
        {
            var normalizedName = item.Name.ToUpperInvariant();
            if (existing.Any(m => m.Code == item.Code || m.NormalizedName == normalizedName)) continue;
            var medicine = new CatalogMedicine
            {
                Code = item.Code,
                Name = item.Name,
                NormalizedName = normalizedName,
                ActiveIngredient = item.ActiveIngredient,
                Strength = item.Strength,
                DosageForm = item.DosageForm,
                BaseUnit = item.Unit,
                Route = item.Route,
                PrescriptionEligible = true,
                InternalStockTracked = false,
                CreatedAt = now,
                CreatedBy = actor,
                UpdatedAt = now,
                UpdatedBy = actor,
            };
            db.Add(medicine);
            existing.Add(medicine);
            added++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"Danh mục thuốc: thêm {added}/{Items.Length} thuốc khởi tạo (không có liều hoặc hướng dẫn mặc định).";
    }
}
