using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Development-only starter medicine catalog for UI demos.
public sealed class DevelopmentMedicineSeeder(DcmsDbContext db)
{
    private sealed record Item(string Code, string Name, string ActiveIngredient, string Strength, string DosageForm, string Unit, string? Instructions = null);

    private static readonly Item[] Items =
    [
        new("MED-STARTER-001", "Paracetamol 500 mg", "Paracetamol", "500 mg", "Viên nén", "viên"),
        new("MED-STARTER-002", "Ibuprofen 400 mg", "Ibuprofen", "400 mg", "Viên nén", "viên"),
        new("MED-STARTER-003", "Amoxicillin 500 mg", "Amoxicillin", "500 mg", "Viên nang", "viên"),
        new("MED-STARTER-004", "Metronidazole 250 mg", "Metronidazole", "250 mg", "Viên nén", "viên"),
        new("MED-STARTER-005", "Chlorhexidine 0,12%", "Chlorhexidine", "0,12%", "Dung dịch súc miệng", "ml"),
        new("MED-STARTER-006", "Spiramycin + Metronidazole", "Spiramycin; Metronidazole", "750.000 IU + 125 mg", "Viên nén bao phim", "viên", "Uống sau ăn."),
        new("MED-STARTER-007", "Alpha chymotrypsin 4,2 mg", "Alpha chymotrypsin", "4,2 mg", "Viên nén", "viên", "Ngậm dưới lưỡi cho tan dần."),
        new("MED-STARTER-008", "Clindamycin 300 mg", "Clindamycin", "300 mg", "Viên nang", "viên", "Uống với nhiều nước."),
        new("MED-STARTER-009", "Amoxicillin + Acid clavulanic 625 mg", "Amoxicillin; Acid clavulanic", "500 mg + 125 mg", "Viên nén bao phim", "viên", "Uống ngay đầu bữa ăn."),
        new("MED-STARTER-010", "Gel nha chu Metronidazole + Chlorhexidine", "Metronidazole; Chlorhexidine", "10 mg/g + 0,25%", "Gel bôi", "tuýp", "Bôi lên vùng nướu sau khi chải răng."),
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var hasOwner = await (from e in db.Employees
                              join u in db.Users on e.AccountId equals u.Id
                              where u.UserName == "owner.demo"
                              select u.Id).AnyAsync(ct);
        if (!hasOwner) throw new InvalidOperationException("Chưa có tài khoản owner.demo: chạy --seed-demo trước.");

        var existing = await db.CatalogMedicines.ToListAsync(ct);
        var added = 0;
        foreach (var item in Items)
        {
            var upperName = item.Name.ToUpperInvariant();
            if (existing.Any(m => m.Code == item.Code || m.Name.ToUpperInvariant() == upperName)) continue;
            var medicine = new CatalogMedicine
            {
                Code = item.Code,
                Name = item.Name,
                BaseUnit = item.Unit,
                PrescriptionEligible = true,
                InternalStockTracked = false,
                Spec = new CatalogMedicineSpec
                {
                    ActiveIngredient = item.ActiveIngredient,
                    Strength = item.Strength,
                    DosageForm = item.DosageForm,
                    Instructions = item.Instructions
                }
            };
            db.CatalogMedicines.Add(medicine);
            existing.Add(medicine);
            added++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"Danh mục thuốc: thêm {added}/{Items.Length} thuốc khởi tạo.";
    }
}
