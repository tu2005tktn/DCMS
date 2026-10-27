using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

public sealed class SystemCatalogService(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : ISystemCatalogService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static BusinessException Error(string code, int status = 400, string? field = null) => new(status, SystemMessages.Get(code) with { Field = field });
    private async Task AuthorizeAsync(int actorId)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" } || !AccountRoles.Effective(await users.GetRolesAsync(user)).Contains(AccountRoles.Dentist)) throw Error("MSG06", 403);
    }
    private static string Name(string? value)
    {
        var name = value?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (name.Length is < 1 or > 160) throw Error("MSG04", field: "name");
        return name;
    }
    private static string Search(string? q, int page, int pageSize)
    {
        if (pageSize is not (20 or 50 or 100) || page < 1 || page > int.MaxValue / pageSize || q?.Length > 160) throw Error("MSG04");
        return q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
    }
    private static void Version(int actual, int expected) { if (actual != expected) throw Error("MSG111", 409); }
    private async Task<MaterialCategory> CategoryRow(Guid id, CancellationToken ct) =>
        await db.MaterialCategories.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw Error("MSG113", 404);
    private async Task<MaterialCategoryDto> CategoryResult(Guid id, CancellationToken ct) => await db.MaterialCategories.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new MaterialCategoryDto(x.Id, x.Name, db.MaterialCatalogItems.Count(i => i.CategoryId == x.Id && !i.IsDeleted), x.Version)).SingleAsync(ct);
    private async Task<MaterialDto> MaterialResult(Guid id, CancellationToken ct) => await db.MaterialCatalogItems.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new MaterialDto(x.Id, x.Name, x.CategoryId, x.Category.Name, x.Version)).SingleAsync(ct);
    private async Task<MaterialCatalogItem> MaterialRow(Guid id, CancellationToken ct) => await db.MaterialCatalogItems.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw Error("MSG113", 404);
    private async Task UniqueCategory(string name, Guid? except, CancellationToken ct)
    {
        if (await db.MaterialCategories.AnyAsync(x => !x.IsDeleted && x.Id != except && x.NormalizedName == name.ToUpperInvariant(), ct)) throw new BusinessException(409, CatalogMessages.Duplicate);
    }
    private async Task UniqueMaterial(Guid category, string name, Guid? except, CancellationToken ct)
    {
        if (await db.MaterialCatalogItems.AnyAsync(x => !x.IsDeleted && x.Id != except && x.CategoryId == category && x.NormalizedName == name.ToUpperInvariant(), ct)) throw new BusinessException(409, CatalogMessages.Duplicate);
    }
    private sealed record Change<T>(Guid Id, object? Before, T Result);
    private async Task<T> Command<T>(int actorId, Guid key, string command, object request, Func<Task<Change<T>>> apply, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (key == Guid.Empty) throw Error("MSG04", field: "requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Low-volume global settings share a dedicated lock, including category-parent checks.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362001)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<T>(prior.ResultJson, Json) ?? throw Error("MSG144", 503);
        }
        var change = await apply();
        db.SystemCatalogChanges.Add(new SystemCatalogChange { RequestKey = key, ActorId = actorId, Command = command, ResourceId = change.Id,
            Fingerprint = hash, BeforeJson = change.Before == null ? null : JsonSerializer.Serialize(change.Before, Json),
            ResultJson = JsonSerializer.Serialize(change.Result, Json), OccurredAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return change.Result;
    }
    public async Task<CatalogPage<MaterialCategoryDto>> CategoriesAsync(int actorId, string? q, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId); var search = Search(q, page, pageSize);
        var query = db.MaterialCategories.AsNoTracking().Where(x => !x.IsDeleted && x.NormalizedName.Contains(search));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new MaterialCategoryDto(x.Id, x.Name, db.MaterialCatalogItems.Count(i => i.CategoryId == x.Id && !i.IsDeleted), x.Version)).ToArrayAsync(ct);
        return new(items, total, page, pageSize);
    }
    public async Task<CatalogPage<MaterialDto>> MaterialsAsync(int actorId, string? q, Guid? categoryId, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId); var search = Search(q, page, pageSize);
        if (categoryId.HasValue) await CategoryRow(categoryId.Value, ct);
        var query = db.MaterialCatalogItems.AsNoTracking().Where(x => !x.IsDeleted && !x.Category.IsDeleted && x.NormalizedName.Contains(search) && (!categoryId.HasValue || x.CategoryId == categoryId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new MaterialDto(x.Id, x.Name, x.CategoryId, x.Category.Name, x.Version)).ToArrayAsync(ct);
        return new(items, total, page, pageSize);
    }
    public async Task<MaterialCategoryDto> CategoryAsync(int actorId, Guid id, CancellationToken ct)
    { await AuthorizeAsync(actorId); await CategoryRow(id, ct); return await CategoryResult(id, ct); }
    public async Task<MaterialDto> MaterialAsync(int actorId, Guid id, CancellationToken ct)
    { await AuthorizeAsync(actorId); await MaterialRow(id, ct); return await MaterialResult(id, ct); }
    public Task<MaterialCategoryDto> CreateCategoryAsync(int actorId, CreateCategoryRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "CategoryCreate", request, async () => {
        var name = Name(request.Name); await UniqueCategory(name, null, ct);
        var row = new MaterialCategory { Name = name, NormalizedName = name.ToUpperInvariant(), CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
        db.MaterialCategories.Add(row); await db.SaveChangesAsync(ct); return new Change<MaterialCategoryDto>(row.Id, null, await CategoryResult(row.Id, ct));
    }, ct);
    public Task<MaterialCategoryDto> UpdateCategoryAsync(int actorId, Guid id, UpdateCategoryRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "CategoryUpdate", new { id, request }, async () => {
        var row = await CategoryRow(id, ct); Version(row.Version, request.ExpectedVersion); var before = await CategoryResult(id, ct);
        var name = Name(request.Name); await UniqueCategory(name, id, ct);
        row.Name = name; row.NormalizedName = name.ToUpperInvariant(); row.Version++; row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); return new Change<MaterialCategoryDto>(id, before, await CategoryResult(id, ct));
    }, ct);
    public Task<MaterialCategoryDto> DeleteCategoryAsync(int actorId, Guid id, DeleteCatalogRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "CategoryDelete", new { id, request }, async () => {
        var row = await CategoryRow(id, ct); Version(row.Version, request.ExpectedVersion); var before = await CategoryResult(id, ct);
        if (before.ItemCount > 0) throw new BusinessException(409, CatalogMessages.InUse);
        row.IsDeleted = true; row.Version++; row.UpdatedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct);
        return new Change<MaterialCategoryDto>(id, before, await CategoryResult(id, ct));
    }, ct);
    public Task<MaterialDto> CreateMaterialAsync(int actorId, CreateMaterialRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "MaterialCreate", request, async () => {
        var name = Name(request.Name); await CategoryRow(request.CategoryId, ct); await UniqueMaterial(request.CategoryId, name, null, ct);
        var row = new MaterialCatalogItem { Name = name, NormalizedName = name.ToUpperInvariant(), CategoryId = request.CategoryId, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
        db.MaterialCatalogItems.Add(row); await db.SaveChangesAsync(ct); return new Change<MaterialDto>(row.Id, null, await MaterialResult(row.Id, ct));
    }, ct);
    public Task<MaterialDto> UpdateMaterialAsync(int actorId, Guid id, UpdateMaterialRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "MaterialUpdate", new { id, request }, async () => {
        var row = await MaterialRow(id, ct); Version(row.Version, request.ExpectedVersion); var before = await MaterialResult(id, ct);
        var name = Name(request.Name); await CategoryRow(request.CategoryId, ct); await UniqueMaterial(request.CategoryId, name, id, ct);
        row.Name = name; row.NormalizedName = name.ToUpperInvariant(); row.CategoryId = request.CategoryId; row.Version++; row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); return new Change<MaterialDto>(id, before, await MaterialResult(id, ct));
    }, ct);
    public Task<MaterialDto> DeleteMaterialAsync(int actorId, Guid id, DeleteCatalogRequest request, CancellationToken ct) => Command(actorId, request.RequestKey, "MaterialDelete", new { id, request }, async () => {
        var row = await MaterialRow(id, ct); Version(row.Version, request.ExpectedVersion); var before = await MaterialResult(id, ct);
        row.IsDeleted = true; row.Version++; row.UpdatedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct);
        return new Change<MaterialDto>(id, before, await MaterialResult(id, ct));
    }, ct);
}
