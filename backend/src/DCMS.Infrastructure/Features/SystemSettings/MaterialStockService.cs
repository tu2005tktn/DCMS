using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Docs: docs/specs/MATERIAL-STOCK.md. Read-only views; writes happen inside treatment activity commands.
public sealed class MaterialStockService(DcmsDbContext db, DCMS.Application.Features.Settings.ISettingsAccess access) : IMaterialStockService
{
    private static string Search(string? q) => q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
    private static BusinessException Bad() => new(400, SystemMessages.Get("MSG04"));

    public async Task<MaterialStockPage> StockAsync(int actorId, string? q, int page, int pageSize, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "materials.stock.read", ct);
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160) throw Bad();
        var search = Search(q);
        var query = db.MaterialPurchases.AsNoTracking().Where(x => !x.IsDeleted && x.ReceivedOn != null);
        if (search.Length > 0) query = query.Where(x => x.MaterialName.ToUpper().Contains(search));
        var batches = await query.ToListAsync(ct);
        var ids = batches.Select(x => x.MaterialId).Distinct().ToArray();
        var used = await db.MaterialUsages.AsNoTracking().Where(x => ids.Contains(x.MaterialId))
            .GroupBy(x => x.MaterialId).Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Id, x => x.Sum, ct);
        // Usage is not tied to a lot: allocate each material's used total to its batches, earliest expiry first (docs/specs/MATERIAL-STOCK.md).
        var rows = new List<MaterialStockDto>();
        foreach (var group in batches.GroupBy(x => x.MaterialId))
        {
            var left = used.GetValueOrDefault(group.Key);
            foreach (var b in group.OrderBy(x => x.ExpiresOn == null).ThenBy(x => x.ExpiresOn).ThenBy(x => x.ReceivedOn).ThenBy(x => x.CreatedAt))
            {
                var take = Math.Min(left, b.UsageQuantity); left -= take;
                rows.Add(new(b.Id, b.MaterialId, b.MaterialName, b.CategoryName, b.UsageUnit, b.SupplierName, b.LotNumber, b.ReceivedOn!.Value, b.ExpiresOn,
                    b.UsageQuantity, take, b.UsageQuantity - take));
            }
        }
        var ordered = rows.OrderBy(x => x.Name).ThenBy(x => x.MaterialId).ThenBy(x => x.ExpiresOn == null).ThenBy(x => x.ExpiresOn).ThenBy(x => x.ReceivedOn).ThenBy(x => x.PurchaseId).ToArray();
        return new(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), ordered.Length, page, pageSize);
    }

    public async Task<MaterialAvailablePage> AvailableAsync(int actorId, string? q, int page, int pageSize, CancellationToken ct)
    {
        await access.RequireAsync(actorId, "catalog.materials.read", ct);
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160) throw Bad();
        var search = Search(q);
        var items = db.MaterialCatalogItems.AsNoTracking().Where(x => !x.IsDeleted && !x.Category.IsDeleted);
        if (search.Length > 0) items = items.Where(x => x.Name.ToUpper().Contains(search));
        var all = await items.Select(x => new { x.Id, x.Name, Category = x.Category.Name, x.UsageUnit }).ToListAsync(ct);
        var balances = await MaterialStock.BalancesAsync(db, all.Select(x => x.Id).ToArray(), null, ct);
        var rows = all.Where(x => balances[x.Id].Received - balances[x.Id].Used > 0).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new MaterialAvailableDto(x.Id, x.Name, x.Category, x.UsageUnit, balances[x.Id].Received - balances[x.Id].Used)).ToArray();
        return new(rows.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), rows.Length, page, pageSize);
    }

    public async Task<MaterialUsagePage> UsageAsync(int actorId, Guid? materialId, Guid? activityId, string? q, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        var scope = await access.RequireAsync(actorId, "materials.usage.read", ct);
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160 || from > to) throw Bad();
        var search = Search(q);
        var query = db.MaterialUsages.AsNoTracking().Where(x => scope.BranchIds.Contains(x.BranchId));
        if (materialId.HasValue) query = query.Where(x => x.MaterialId == materialId);
        if (activityId.HasValue) query = query.Where(x => x.ActivityId == activityId);
        if (search.Length > 0) query = query.Where(x => x.MaterialName.ToUpper().Contains(search));
        // Calendar days in clinic time (UTC+7), same convention as purchases.
        if (from.HasValue) query = query.Where(x => x.UsedAt >= new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)));
        if (to.HasValue) query = query.Where(x => x.UsedAt < new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UsedAt).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var patientIds = rows.Select(x => x.PatientId).Distinct().ToArray();
        var branchIds = rows.Select(x => x.BranchId).Distinct().ToArray();
        var dossierIds = rows.Where(x => x.DossierId.HasValue).Select(x => x.DossierId!.Value).Distinct().ToArray();
        var accountIds = rows.Where(x => x.RecordedBy.HasValue).Select(x => x.RecordedBy!.Value).Distinct().ToArray();
        var patients = await db.Patients.AsNoTracking().Where(x => patientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var dossiers = await db.TreatmentDossiers.AsNoTracking().Where(x => dossierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var staff = await db.Employees.AsNoTracking().Where(x => accountIds.Contains(x.AccountId)).ToDictionaryAsync(x => x.AccountId, x => x.FullName, ct);
        return new(rows.Select(x => new MaterialUsageDto(x.Id, x.UsedAt, x.MaterialId, x.MaterialName, x.Quantity, x.UsageUnit, x.PatientId,
            patients.GetValueOrDefault(x.PatientId, ""), x.BranchId, branches.GetValueOrDefault(x.BranchId, ""), x.DossierId,
            x.DossierId.HasValue ? dossiers.GetValueOrDefault(x.DossierId.Value) : null,
            x.RecordedBy.HasValue ? staff.GetValueOrDefault(x.RecordedBy.Value) : null)).ToArray(), total, page, pageSize);
    }
}
