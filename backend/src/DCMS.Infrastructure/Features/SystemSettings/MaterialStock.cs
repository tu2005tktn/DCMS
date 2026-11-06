using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Docs: docs/specs/MATERIAL-STOCK.md. Stock = received purchases (usage units) - usage lines; global across branches.
public static class MaterialStock
{
    public const long Lock = 6462362002;
    public const int MaxLines = 30;
    private static BusinessException Fail(SystemMessage m, int status = 409) => new(status, m);

    public static async Task<Dictionary<Guid, (decimal Received, decimal Used)>> BalancesAsync(DcmsDbContext db, ICollection<Guid> ids, Guid? exceptActivity, CancellationToken ct)
    {
        var received = await db.MaterialPurchases.AsNoTracking().Where(x => !x.IsDeleted && x.ReceivedOn != null && ids.Contains(x.MaterialId))
            .GroupBy(x => x.MaterialId).Select(g => new { Id = g.Key, Sum = g.Sum(x => x.UsageQuantity) }).ToListAsync(ct);
        var used = await db.MaterialUsages.AsNoTracking().Where(x => ids.Contains(x.MaterialId) && (exceptActivity == null || x.ActivityId != exceptActivity))
            .GroupBy(x => x.MaterialId).Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        return ids.ToDictionary(id => id, id => (received.FirstOrDefault(x => x.Id == id)?.Sum ?? 0, used.FirstOrDefault(x => x.Id == id)?.Sum ?? 0));
    }

    /// <summary>Replaces the activity's usage lines. null = keep as is. Throws 409 MATERIAL_STOCK_INSUFFICIENT when stock is short.</summary>
    public static async Task ReplaceAsync(DcmsDbContext db, TimeProvider clock, Guid activityId, int patientId, int branchId, Guid? dossierId,
        DateTimeOffset usedAt, int? actorId, IReadOnlyList<MaterialUseLine>? lines, CancellationToken ct)
    {
        if (lines is null) return;
        if (lines.Count > MaxLines || lines.Select(x => x.MaterialId).Distinct().Count() != lines.Count
            || lines.Any(x => x.Quantity <= 0 || x.Quantity > 1_000_000 || decimal.Round(x.Quantity, 3) != x.Quantity))
            throw new BusinessException(400, MaterialStockMessages.InvalidLines);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({Lock})", ct);
        var old = await db.MaterialUsages.Where(x => x.ActivityId == activityId).ToListAsync(ct);
        var ids = lines.Select(x => x.MaterialId).ToArray();
        var items = await db.MaterialCatalogItems.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        // A material kept from the previous save stays valid even if it was deactivated since.
        foreach (var line in lines)
            if (!items.TryGetValue(line.MaterialId, out var item) || item.IsDeleted && old.All(x => x.MaterialId != line.MaterialId))
                throw new BusinessException(400, MaterialStockMessages.InvalidLines);
        var balances = await BalancesAsync(db, ids, activityId, ct);
        var short_ = lines.Where(l => balances[l.MaterialId].Received - balances[l.MaterialId].Used < l.Quantity).ToArray();
        if (short_.Length > 0)
        {
            var text = string.Join("; ", short_.Select(l => $"{items[l.MaterialId].Name}: tồn {balances[l.MaterialId].Received - balances[l.MaterialId].Used:0.###}, cần {l.Quantity:0.###}"));
            throw Fail(MaterialStockMessages.Insufficient(text));
        }
        db.MaterialUsages.RemoveRange(old);
        var now = clock.GetUtcNow();
        foreach (var l in lines)
            db.MaterialUsages.Add(new MaterialUsage { ActivityId = activityId, MaterialId = l.MaterialId, MaterialName = items[l.MaterialId].Name,
                UsageUnit = items[l.MaterialId].UsageUnit, Quantity = l.Quantity, PatientId = patientId, BranchId = branchId, DossierId = dossierId,
                UsedAt = usedAt, RecordedBy = actorId, CreatedAt = now });
    }

    public static async Task ReleaseAsync(DcmsDbContext db, Guid activityId, CancellationToken ct)
    {
        var old = await db.MaterialUsages.Where(x => x.ActivityId == activityId).ToListAsync(ct);
        if (old.Count > 0) db.MaterialUsages.RemoveRange(old);
    }

    /// <summary>After a purchase change is saved inside the transaction: refuse if any affected material would go below zero.</summary>
    public static async Task EnsureNotNegativeAsync(DcmsDbContext db, IEnumerable<Guid> materialIds, CancellationToken ct)
    {
        var ids = materialIds.Distinct().ToArray();
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({Lock})", ct);
        var balances = await BalancesAsync(db, ids, null, ct);
        if (balances.Any(x => x.Value.Received < x.Value.Used)) throw Fail(MaterialStockMessages.Negative);
    }
}

public static class MaterialStockMessages
{
    public static SystemMessage InvalidLines => new("MATERIAL_USAGE_INVALID", "Danh sách vật liệu dùng không hợp lệ. Vui lòng kiểm tra vật liệu và số lượng.") { Field = "materials" };
    public static SystemMessage Insufficient(string detail) => new("MATERIAL_STOCK_INSUFFICIENT", $"Tồn kho không đủ. {detail}.") { Field = "materials" };
    public static SystemMessage Negative => new("MATERIAL_STOCK_NEGATIVE", "Thay đổi này làm tồn kho vật liệu bị âm vì đã xuất dùng. Vui lòng kiểm tra lại.");
}
