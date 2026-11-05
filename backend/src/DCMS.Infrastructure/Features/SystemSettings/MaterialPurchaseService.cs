using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Docs: docs/specs/MATERIAL-PURCHASES.md. Same command envelope as SystemCatalogService (advisory lock, request key receipts).
public sealed class MaterialPurchaseService(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IMaterialPurchaseService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const long MaxVnd = 1_000_000_000_000;
    private static BusinessException Error(string code, int status = 400, string? field = null) => new(status, SystemMessages.Get(code) with { Field = field });
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
    private Task Require(int actorId, string permission) => access.RequireAsync(actorId, permission, CancellationToken.None);
    private static void Version(int actual, int expected) { if (actual != expected) throw Error("MSG111", 409); }
    private sealed record Change(Guid Id, object? Before, MaterialPurchaseDto Result);

    private static MaterialPurchaseDto ToDto(MaterialPurchase x) => new(x.Id, x.EnteredOn, x.ReceivedOn, x.ExpiresOn, x.MaterialId, x.MaterialName, x.CategoryName,
        x.Quantity, x.Unit, x.UnitPriceVnd, x.TotalVnd, x.PaidVnd, x.TotalVnd - x.PaidVnd, x.Version);
    private async Task<MaterialPurchase> Row(Guid id, CancellationToken ct) =>
        await db.MaterialPurchases.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw Error("MSG113", 404);

    private sealed record Fields(DateOnly EnteredOn, DateOnly? ReceivedOn, DateOnly? ExpiresOn, MaterialCatalogItem Material, decimal Quantity, string Unit, long UnitPrice, long Total, long Paid);
    private async Task<Fields> Validate(DateOnly enteredOn, DateOnly? receivedOn, DateOnly? expiresOn, Guid materialId, decimal quantity, string? unit, long unitPrice, long paid, CancellationToken ct)
    {
        if (enteredOn > Today) throw new BusinessException(400, MaterialPurchaseMessages.InvalidDate("enteredOn"));
        if (receivedOn.HasValue && (receivedOn < enteredOn || receivedOn > Today)) throw new BusinessException(400, MaterialPurchaseMessages.InvalidDate("receivedOn"));
        if (expiresOn.HasValue && expiresOn < enteredOn) throw new BusinessException(400, MaterialPurchaseMessages.InvalidDate("expiresOn"));
        if (quantity <= 0 || quantity > 1_000_000 || decimal.Round(quantity, 3) != quantity) throw Error("MSG04", field: "quantity");
        var cleanUnit = unit?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (cleanUnit.Length is < 1 or > 40) throw Error("MSG04", field: "unit");
        if (unitPrice < 0 || unitPrice > MaxVnd) throw Error("MSG04", field: "unitPriceVnd");
        var totalDecimal = decimal.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);
        if (totalDecimal > MaxVnd) throw Error("MSG04", field: "quantity");
        var total = (long)totalDecimal;
        if (paid < 0 || paid > MaxVnd) throw Error("MSG04", field: "paidVnd");
        if (paid > total) throw new BusinessException(400, MaterialPurchaseMessages.PaidExceedsTotal);
        var material = await db.MaterialCatalogItems.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == materialId, ct);
        if (material is null || material.IsDeleted || material.Category.IsDeleted) throw new BusinessException(400, MaterialPurchaseMessages.MaterialInactive);
        return new(enteredOn, receivedOn, expiresOn, material, quantity, cleanUnit, unitPrice, total, paid);
    }
    private void Apply(MaterialPurchase row, Fields f, int actorId)
    {
        row.MaterialId = f.Material.Id; row.MaterialName = f.Material.Name; row.CategoryName = f.Material.Category.Name;
        row.EnteredOn = f.EnteredOn; row.ReceivedOn = f.ReceivedOn; row.ExpiresOn = f.ExpiresOn; row.Quantity = f.Quantity; row.Unit = f.Unit;
        row.UnitPriceVnd = f.UnitPrice; row.TotalVnd = f.Total; row.PaidVnd = f.Paid; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = actorId;
    }

    private async Task<MaterialPurchaseDto> Command(int actorId, string permission, Guid key, string command, object request, Func<Task<Change>> apply, CancellationToken ct)
    {
        await Require(actorId, permission);
        if (key == Guid.Empty) throw Error("MSG04", field: "requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<MaterialPurchaseDto>(prior.ResultJson, Json) ?? throw Error("MSG144", 503);
        }
        var change = await apply();
        db.SystemCatalogChanges.Add(new SystemCatalogChange { RequestKey = key, ActorId = actorId, Command = command, ResourceId = change.Id,
            Fingerprint = hash, BeforeJson = change.Before == null ? null : JsonSerializer.Serialize(change.Before, Json),
            ResultJson = JsonSerializer.Serialize(change.Result, Json), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return change.Result;
    }

    public async Task<MaterialPurchasePage> ListAsync(int actorId, string? q, DateOnly? from, DateOnly? to, string? payment, int page, int pageSize, CancellationToken ct)
    {
        await Require(actorId, "catalog.materials.read");
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160) throw Error("MSG04");
        if (from.HasValue && to.HasValue && from > to) throw Error("MSG04");
        if (payment is not (null or "" or "unpaid" or "partial" or "paid")) throw Error("MSG04", field: "payment");
        var search = q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        var query = db.MaterialPurchases.AsNoTracking().Where(x => !x.IsDeleted);
        if (search.Length > 0) query = query.Where(x => x.MaterialName.ToUpper().Contains(search));
        if (from.HasValue) query = query.Where(x => x.EnteredOn >= from);
        if (to.HasValue) query = query.Where(x => x.EnteredOn <= to);
        query = payment switch
        {
            "unpaid" => query.Where(x => x.PaidVnd == 0 && x.TotalVnd > 0),
            "partial" => query.Where(x => x.PaidVnd > 0 && x.PaidVnd < x.TotalVnd),
            "paid" => query.Where(x => x.PaidVnd >= x.TotalVnd),
            _ => query,
        };
        var total = await query.CountAsync(ct);
        var sums = await query.GroupBy(_ => 1).Select(g => new { Total = g.Sum(x => x.TotalVnd), Paid = g.Sum(x => x.PaidVnd) }).SingleOrDefaultAsync(ct);
        var rows = await query.OrderByDescending(x => x.EnteredOn).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var totals = new MaterialPurchaseTotals(sums?.Total ?? 0, sums?.Paid ?? 0, (sums?.Total ?? 0) - (sums?.Paid ?? 0));
        return new(rows.Select(ToDto).ToArray(), total, page, pageSize, totals);
    }
    public async Task<MaterialPurchaseDto> GetAsync(int actorId, Guid id, CancellationToken ct)
    { await Require(actorId, "catalog.materials.read"); return ToDto(await db.MaterialPurchases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw Error("MSG113", 404)); }

    public Task<MaterialPurchaseDto> CreateAsync(int actorId, CreateMaterialPurchaseRequest r, CancellationToken ct) =>
        Command(actorId, "catalog.materials.create", r.RequestKey, "PurchaseCreate", r, async () =>
        {
            var f = await Validate(r.EnteredOn, r.ReceivedOn, r.ExpiresOn, r.MaterialId, r.Quantity, r.Unit, r.UnitPriceVnd, r.PaidVnd, ct);
            var row = new MaterialPurchase { CreatedAt = clock.GetUtcNow(), CreatedBy = actorId };
            Apply(row, f, actorId); db.MaterialPurchases.Add(row); await db.SaveChangesAsync(ct);
            return new Change(row.Id, null, ToDto(row));
        }, ct);
    public Task<MaterialPurchaseDto> UpdateAsync(int actorId, Guid id, UpdateMaterialPurchaseRequest r, CancellationToken ct) =>
        Command(actorId, "catalog.materials.update", r.RequestKey, "PurchaseUpdate", new { id, r }, async () =>
        {
            var row = await Row(id, ct); Version(row.Version, r.ExpectedVersion); var before = ToDto(row);
            var f = await Validate(r.EnteredOn, r.ReceivedOn, r.ExpiresOn, r.MaterialId, r.Quantity, r.Unit, r.UnitPriceVnd, r.PaidVnd, ct);
            Apply(row, f, actorId); row.Version++; await db.SaveChangesAsync(ct);
            return new Change(id, before, ToDto(row));
        }, ct);
    public Task<MaterialPurchaseDto> DeleteAsync(int actorId, Guid id, DeleteCatalogRequest r, CancellationToken ct) =>
        Command(actorId, "catalog.materials.deactivate", r.RequestKey, "PurchaseDelete", new { id, r }, async () =>
        {
            var row = await Row(id, ct); Version(row.Version, r.ExpectedVersion); var before = ToDto(row);
            row.IsDeleted = true; row.Version++; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = actorId; await db.SaveChangesAsync(ct);
            return new Change(id, before, ToDto(row));
        }, ct);
}
