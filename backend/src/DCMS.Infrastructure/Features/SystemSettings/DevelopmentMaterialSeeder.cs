using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Development-only sample data for the instrument/material screens. Rules: docs/specs/MATERIAL-CATALOG-SEED.md.
// Catalog names and usage units only: no purchases, stock, usage or suppliers.
public sealed class DevelopmentMaterialSeeder(DcmsDbContext db, TimeProvider clock)
{
    // Category, name, usage unit, purchase unit, conversion factor (1 purchase unit = N usage units), purchase quantity, unit price per purchase unit, shelf life in months (null = no expiry).
    private sealed record Item(string Category, string Name, string UsageUnit, string PurchaseUnit, decimal Factor, decimal Quantity, long Price, int? ShelfMonths);

    private const string Hand = "Dụng cụ cầm tay", Endo = "Dụng cụ nội nha và mũi khoan", Fill = "Vật liệu trám", EndoMat = "Vật liệu nội nha",
        Anes = "Thuốc tê và kim", Impr = "Lấy dấu và phục hình", Sterile = "Vô trùng và bảo hộ", Surg = "Chỉ khâu và phẫu thuật", Impl = "Implant", White = "Tẩy trắng";

    private static readonly Item[] Items =
    [
        new(Hand, "Gương khám nha khoa", "cái", "cái", 1, 20, 25_000, null), new(Hand, "Thám trâm", "cái", "cái", 1, 20, 30_000, null),
        new(Hand, "Kẹp gắp nha khoa", "cái", "cái", 1, 20, 35_000, null), new(Hand, "Cây lấy cao răng tay", "cái", "cái", 1, 10, 150_000, null),
        new(Hand, "Bay trám composite", "cái", "cái", 1, 10, 220_000, null), new(Hand, "Kìm nhổ răng", "cái", "cái", 1, 6, 650_000, null),
        new(Hand, "Cây nạy răng", "cái", "cái", 1, 6, 280_000, null),
        new(Endo, "Trâm nội nha tay K-file", "cây", "hộp 6 cây", 6, 10, 90_000, 36), new(Endo, "Trâm máy NiTi", "cây", "hộp 6 cây", 6, 6, 750_000, 36),
        new(Endo, "Mũi khoan kim cương", "cái", "hộp 5 cái", 5, 10, 120_000, null), new(Endo, "Mũi khoan tay chậm", "cái", "hộp 10 cái", 10, 5, 200_000, null),
        new(Endo, "Kim bơm rửa ống tủy", "cái", "hộp 100 cái", 100, 4, 140_000, 36),
        new(Fill, "Composite đặc", "g", "tuýp 4 g", 4, 15, 320_000, 24), new(Fill, "Composite thẩm mỹ", "g", "tuýp 4 g", 4, 10, 480_000, 24),
        new(Fill, "Glass ionomer (GIC) viên nang", "viên", "hộp 50 viên", 50, 4, 1_200_000, 18), new(Fill, "Keo dán composite", "mL", "chai 5 mL", 5, 8, 650_000, 18),
        new(Fill, "Axit etching", "mL", "ống tiêm 3 mL", 3, 12, 60_000, 24), new(Fill, "Giấy cắn", "tờ", "hộp 144 tờ", 144, 4, 85_000, null),
        new(EndoMat, "Gutta-percha", "cây", "hộp 100 cây", 100, 8, 130_000, 36), new(EndoMat, "Sealer nội nha", "bộ", "bộ", 1, 6, 450_000, 24),
        new(EndoMat, "Dung dịch NaOCl 3%", "mL", "chai 500 mL", 500, 6, 55_000, 12), new(EndoMat, "Chốt sợi", "cái", "hộp 10 cái", 10, 5, 750_000, 36),
        new(EndoMat, "Côn giấy", "cây", "hộp 200 cây", 200, 5, 70_000, 36),
        new(Anes, "Thuốc tê Lidocaine 2% (ống 1,8 mL)", "ống", "hộp 50 ống", 50, 8, 380_000, 24), new(Anes, "Kim tê nha khoa", "cái", "hộp 100 cái", 100, 6, 160_000, 36),
        new(Anes, "Gel tê bề mặt", "g", "tuýp 30 g", 30, 6, 120_000, 24),
        new(Impr, "Alginate lấy dấu", "g", "gói 500 g", 500, 8, 90_000, 24), new(Impr, "Silicone lấy dấu", "bộ", "bộ", 1, 4, 850_000, 24),
        new(Impr, "Xi măng gắn mão", "bộ", "bộ", 1, 5, 520_000, 24), new(Impr, "Vật liệu làm răng tạm", "bộ", "bộ", 1, 4, 700_000, 18),
        new(Sterile, "Găng tay y tế", "cái", "hộp 100 cái", 100, 30, 110_000, 36), new(Sterile, "Khẩu trang y tế", "cái", "hộp 50 cái", 50, 30, 45_000, 36),
        new(Sterile, "Túi hấp tiệt trùng", "cái", "hộp 200 cái", 200, 6, 180_000, 36), new(Sterile, "Bông cuộn nha khoa", "cuộn", "gói 300 cuộn", 300, 10, 40_000, 36),
        new(Sterile, "Ống hút nước bọt", "cái", "túi 100 cái", 100, 20, 35_000, 36), new(Sterile, "Dung dịch sát khuẩn dụng cụ", "mL", "can 5 L", 5000, 6, 420_000, 24),
        new(Sterile, "Khăn trải bệnh nhân", "cái", "gói 125 cái", 125, 12, 70_000, null),
        new(Surg, "Chỉ khâu tiêu", "sợi", "hộp 12 sợi", 12, 6, 280_000, 36), new(Surg, "Chỉ khâu không tiêu", "sợi", "hộp 12 sợi", 12, 6, 180_000, 36),
        new(Surg, "Lưỡi dao mổ", "cái", "hộp 100 cái", 100, 3, 150_000, 36), new(Surg, "Xốp cầm máu", "miếng", "hộp 10 miếng", 10, 8, 260_000, 24),
        new(Impl, "Trụ Implant", "cái", "cái", 1, 8, 9_000_000, 30), new(Impl, "Abutment Implant", "cái", "cái", 1, 8, 3_000_000, 30),
        new(Impl, "Trụ lành thương", "cái", "cái", 1, 8, 600_000, 30),
        new(White, "Gel tẩy trắng tại ghế", "bộ", "bộ", 1, 4, 1_800_000, 18), new(White, "Gel tẩy trắng tại nhà", "ống", "hộp 10 ống", 10, 4, 650_000, 18),
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var now = clock.GetUtcNow();

        var categories = await db.MaterialCategories.Where(x => !x.IsDeleted).ToListAsync(ct); var addedCategories = 0;
        foreach (var name in Items.Select(i => i.Category).Distinct().Where(n => categories.All(c => c.NormalizedName != n.ToUpperInvariant())))
        {
            var row = new MaterialCategory { Name = name, NormalizedName = name.ToUpperInvariant(), CreatedAt = now, UpdatedAt = now };
            db.Add(row); categories.Add(row); addedCategories++;
        }
        await db.SaveChangesAsync(ct);

        var materials = await db.MaterialCatalogItems.Where(x => !x.IsDeleted).ToListAsync(ct); var addedItems = 0;
        foreach (var item in Items)
        {
            var category = categories.First(c => c.NormalizedName == item.Category.ToUpperInvariant());
            if (materials.Any(m => m.CategoryId == category.Id && m.NormalizedName == item.Name.ToUpperInvariant())) continue;
            var row = new MaterialCatalogItem { Name = item.Name, NormalizedName = item.Name.ToUpperInvariant(), CategoryId = category.Id, UsageUnit = item.UsageUnit, CreatedAt = now, UpdatedAt = now };
            db.Add(row); materials.Add(row); addedItems++;
        }
        await db.SaveChangesAsync(ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return $"Nhóm vật liệu: thêm {addedCategories}. Dụng cụ/vật liệu: thêm {addedItems}/{Items.Length}.";
    }
}
