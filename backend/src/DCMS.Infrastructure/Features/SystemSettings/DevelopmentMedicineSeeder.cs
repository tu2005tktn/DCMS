using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Development-only starter catalog and synthetic prescription templates for UI demos.
public sealed class DevelopmentMedicineSeeder(DcmsDbContext db, TimeProvider clock)
{
    private sealed record Item(string Code, string Name, string ActiveIngredient, string Strength, string DosageForm, string Unit, string Route);
    private sealed record DemoTemplate(string Code, string Name, string Diagnosis, string MedicineCode, string Route,
        string Morning, string Noon, string Afternoon, string Evening, string MealTiming, int Days, int Quantity, string Instructions);

    private static readonly Item[] Items =
    [
        new("MED-STARTER-001", "Paracetamol 500 mg", "Paracetamol", "500 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-002", "Ibuprofen 400 mg", "Ibuprofen", "400 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-003", "Amoxicillin 500 mg", "Amoxicillin", "500 mg", "Viên nang", "viên", "Uống"),
        new("MED-STARTER-004", "Metronidazole 250 mg", "Metronidazole", "250 mg", "Viên nén", "viên", "Uống"),
        new("MED-STARTER-005", "Chlorhexidine 0,12%", "Chlorhexidine", "0,12%", "Dung dịch súc miệng", "ml", "Súc miệng"),
    ];

    private static readonly DemoTemplate[] Templates =
    [
        new("DEMO-POST-TREATMENT", "DEMO — Sau điều trị (minh họa)", "Demo: chăm sóc sau điều trị", "MED-STARTER-001", "Uống", "1", "0", "0", "1", "AfterMeal", 2, 4, "Dữ liệu minh họa; bác sĩ phải đánh giá và chỉnh sửa trước khi sử dụng."),
        new("DEMO-ORAL-CARE", "DEMO — Vệ sinh răng miệng (minh họa)", "Demo: chăm sóc răng miệng", "MED-STARTER-005", "Súc miệng", "0", "0", "0", "0", "AfterMeal", 1, 1, "Dữ liệu minh họa; xem hướng dẫn sản phẩm và đánh giá của bác sĩ trước khi sử dụng."),
        new("DEMO-PAIN-RELIEF", "DEMO — Giảm đau (minh họa)", "Demo: đau răng", "MED-STARTER-002", "Uống", "1", "0", "0", "1", "AfterMeal", 2, 4, "Dữ liệu minh họa; bác sĩ phải đánh giá chống chỉ định và chỉnh sửa trước khi sử dụng."),
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

        var medicines = await db.CatalogMedicines
            .Where(m => Templates.Select(t => t.MedicineCode).Contains(m.Code) && m.IsActive && m.PrescriptionEligible)
            .ToDictionaryAsync(m => m.Code, ct);
        var existingTemplateCodes = await db.PrescriptionTemplates.Select(t => t.Code).ToListAsync(ct);
        var templatesAdded = 0;
        foreach (var item in Templates)
        {
            if (existingTemplateCodes.Contains(item.Code) || !medicines.TryGetValue(item.MedicineCode, out var medicine)) continue;
            var template = new PrescriptionTemplate
            {
                Code = item.Code,
                Name = item.Name,
                DiagnosisKind = item.Diagnosis,
                Description = "CHỈ DÙNG DEMO — không phải hướng dẫn điều trị. Bác sĩ phải đánh giá, xác minh và chỉnh sửa trước khi kê.",
                IsActive = true,
                Version = 1,
                CreatedBy = actor,
                CreatedAt = now,
                UpdatedBy = actor,
                UpdatedAt = now,
                Lines =
                [
                    new PrescriptionTemplateLine
                    {
                        MedicineId = medicine.Id,
                        MedicineCode = medicine.Code,
                        MedicineName = medicine.Name,
                        ActiveIngredient = medicine.ActiveIngredient,
                        Strength = medicine.Strength,
                        DosageForm = medicine.DosageForm,
                        Unit = medicine.BaseUnit,
                        Route = item.Route,
                        MorningDose = item.Morning,
                        NoonDose = item.Noon,
                        AfternoonDose = item.Afternoon,
                        EveningDose = item.Evening,
                        MealTiming = item.MealTiming,
                        Days = item.Days,
                        Quantity = item.Quantity,
                        Instructions = item.Instructions,
                        SortOrder = 0,
                    },
                ],
            };
            db.PrescriptionTemplates.Add(template);
            existingTemplateCodes.Add(item.Code);
            templatesAdded++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"Danh mục thuốc: thêm {added}/{Items.Length} thuốc khởi tạo; mẫu đơn demo: thêm {templatesAdded}/{Templates.Length} mẫu.";
    }
}
