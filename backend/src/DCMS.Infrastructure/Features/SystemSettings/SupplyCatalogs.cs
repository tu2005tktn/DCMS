using System.IO.Compression;
using System.Net.Mail;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Shared validation, command replay and audit helpers for S58 typed supply catalogs.
internal static partial class SupplyCatalogSupport
{
    public const long MaxPrice = 1_000_000_000_000;
    public static readonly int[] PageSizes = [20, 50, 100];
    public sealed record HistoryChange<TDto>(string? Reason, TDto? Before) where TDto : class;

    public static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null, int max = 0) =>
        new(status, SystemMessages.Get(code, label, max) with { Field = field });

    public static (string Code, string Name, string NormalizedName) ValidateIdentity(string? code, string? name, int nameMax)
    {
        var cleanCode = code?.Trim().ToUpperInvariant() ?? "";
        if (cleanCode.Length == 0) throw Error("MSG02", label: "Mã", field: "code");
        if (cleanCode.Length > 40) throw Error("MSG03", label: "Mã", field: "code", max: 40);
        if (!CodePattern().IsMatch(cleanCode)) throw Error("MSG04", label: "Mã", field: "code");
        var cleanName = name?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (cleanName.Length == 0) throw Error("MSG02", label: "Tên", field: "name");
        if (cleanName.Length > nameMax) throw Error("MSG03", label: "Tên", field: "name", max: nameMax);
        return (cleanCode, cleanName, cleanName.ToUpperInvariant());
    }

    public static string ValidateSearchTerm(string? q, int page, int pageSize)
    {
        if (q?.Length > 100) throw Error("MSG03", label: "Từ khóa", max: 100);
        if (page < 1 || !PageSizes.Contains(pageSize) || page > int.MaxValue / pageSize) throw Error("MSG04", label: "Phân trang");
        return q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
    }

    public static string? Text(string? value, string label, string field, int max)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim().Normalize(NormalizationForm.FormC);
        return text?.Length > max ? throw Error("MSG03", label: label, field: field, max: max) : text;
    }

    public static long? Price(long? value) => value switch
    {
        < 0 => throw new BusinessException(400, SystemMessages.Get("MSG60") with { Field = "referencePrice" }),
        > MaxPrice => throw Error("MSG04", label: "Giá tham khảo", field: "referencePrice"),
        _ => value
    };

    public static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length == 0) throw Error("MSG02", label: "Lý do", field: "reason");
        return reason.Length <= 500 ? reason : throw Error("MSG03", label: "Lý do", field: "reason", max: 500);
    }

    public static string StateImpactMessage(bool active) => active
        ? "Mục sẽ được chọn lại cho giao dịch mới."
        : "Mục sẽ không còn được chọn cho giao dịch mới; dữ liệu đã ghi giữ nguyên.";

    public static async Task<TDto> CommandAsync<TDto>(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access,
        string resource, int actorId, Guid key, string command, object request,
        Func<DateTimeOffset, Task<(Guid Id, string? Reason, TDto? Before, TDto Result)>> apply, CancellationToken ct) where TDto : class
    {
        // Write grants are enforced here too, not only by the route middleware (docs/specs/S58-BRANCHES-AND-CHAIRS.md §2.2).
        var action = command.Split(':')[^1] switch { "create" => "create", "update" => "update", _ => "deactivate" };
        await access.RequireAsync(actorId, $"catalog.{resource}.{action}", ct);
        if (key == Guid.Empty) throw Error("MSG04", label: "Mã yêu cầu", field: "requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362060)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, ClinicSettingsStore.Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG41", 409);
            return JsonSerializer.Deserialize<TDto>(prior.ResultJson, ClinicSettingsStore.Json) ?? throw Error("MSG49", 503);
        }
        var now = clock.GetUtcNow();
        var (id, reason, before, result) = await apply(now);
        db.SystemCatalogChanges.Add(new SystemCatalogChange
        {
            RequestKey = key, ActorId = actorId, Command = command, ResourceId = id, Fingerprint = hash,
            BeforeJson = JsonSerializer.Serialize(new HistoryChange<TDto>(reason, before), ClinicSettingsStore.Json),
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json), OccurredAt = now
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public static async Task SaveWithUniqueCheckAsync(DcmsDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new BusinessException(409, pg.ConstraintName?.Contains("Code") == true ? ReferenceMessages.DuplicateCode : ReferenceMessages.DuplicateName);
        }
    }

    public static async Task<IReadOnlyList<SupplyHistoryItem<TDto>>> HistoryAsync<TDto>(DcmsDbContext db, Guid id, CancellationToken ct) where TDto : class
    {
        var rows = await (from change in db.SystemCatalogChanges.AsNoTracking()
                          join user in db.Users.AsNoTracking() on change.ActorId equals user.Id
                          where change.ResourceId == id
                          orderby change.OccurredAt descending
                          select new { change, user.FullName }).ToListAsync(ct);
        return rows.Select(x =>
        {
            var before = x.change.BeforeJson == null ? null : JsonSerializer.Deserialize<HistoryChange<TDto>>(x.change.BeforeJson, ClinicSettingsStore.Json);
            return new SupplyHistoryItem<TDto>(x.change.OccurredAt, x.FullName, x.change.Command.Split(':')[^1], before?.Reason, before?.Before,
                JsonSerializer.Deserialize<TDto>(x.change.ResultJson, ClinicSettingsStore.Json)!);
        }).ToArray();
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodePattern();
}

    // S58 đợt 4 (docs/specs/S58-SUPPLY-CATALOGS.md): shared command/audit/version flow; each catalog only validates and maps its own fields.
public abstract class SupplyCatalog<TEntity, TInput, TDto>(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access)
    : ISupplyCatalog<TInput, TDto> where TEntity : SupplyEntity where TInput : class where TDto : class
{
    protected DcmsDbContext Db => db;
    protected abstract string Resource { get; }
    protected abstract DbSet<TEntity> Set { get; }
    protected abstract int NameMax { get; }
    // Returns a detached entity holding the normalized, validated values.
    protected abstract Task<TEntity> NormalizeAsync(TInput? input, TEntity? current, CancellationToken ct);
    protected abstract bool SameContent(TEntity current, TEntity next);
    protected abstract void Copy(TEntity from, TEntity to);
    protected abstract Task<TDto[]> MapAsync(IReadOnlyList<TEntity> rows, CancellationToken ct);
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, string? type) => type == null ? query : throw Error("MSG04", label: "Loại", field: "type");
    protected virtual Task UpdateGuardAsync(TEntity current, TEntity next, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task<string?> StateImpactAsync(TEntity row, bool active, CancellationToken ct) => Task.FromResult<string?>(null);

    protected static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null, int max = 0) =>
        SupplyCatalogSupport.Error(code, status, label, field, max);

    protected async Task AuthorizeAsync(int actorId) =>
        await access.RequireAsync(actorId, $"catalog.{Resource}.read", CancellationToken.None);

    protected IQueryable<TEntity> Search(string? q, string? status, string? type)
    {
        var term = q?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        var query = Filter(Set.AsNoTracking(), type);
        query = (status ?? "Active") switch
        {
            "Active" => query.Where(x => x.IsActive), "Inactive" => query.Where(x => !x.IsActive), "All" => query,
            _ => throw Error("MSG04", label: "Trạng thái", field: "status")
        };
        if (term.Length > 0)
        {
            var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, like) || EF.Functions.ILike(x.Code, like));
        }
        return query.OrderBy(x => x.Name.ToUpper()).ThenBy(x => x.Id);
    }

    public async Task<CatalogPage<TDto>> ListAsync(int actorId, string? q, string? status, string? type, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        SupplyCatalogSupport.ValidateSearchTerm(q, page, pageSize);
        var query = Search(q, status, type);
        var total = await query.CountAsync(ct);
        return new(await DtosAsync(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), ct), total, page, pageSize);
    }

    public async Task<TDto> GetAsync(int actorId, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        return await DtoAsync(await RowAsync(id, tracked: false, ct), ct);
    }

    public Task<TDto> CreateAsync(int actorId, CreateSupplyRequest<TInput> request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<TDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:create", request, async _ =>
        {
            var next = await NormalizeAsync(request.Entry, null, ct);
            await UniqueAsync(next, null, ct);
            Set.Add(next);
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (next.Id, null, null, await DtoAsync(next, ct));
        }, ct);

    public Task<TDto> UpdateAsync(int actorId, Guid id, UpdateSupplyRequest<TInput> request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<TDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:update", new { id, request }, async _ =>
        {
            var reason = SupplyCatalogSupport.Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion) throw Error("MSG41", 409);
            var next = await NormalizeAsync(request.Entry, row, ct);
            if (SameContent(row, next)) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
            await UniqueAsync(next, id, ct);
            await UpdateGuardAsync(row, next, ct);
            var before = await DtoAsync(row, ct);
            Copy(next, row); row.Code = next.Code; row.Name = next.Name;
            row.Version++;
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (row.Id, reason, before, await DtoAsync(row, ct));
        }, ct);

    public async Task<SupplyStatePreview<TDto>> StatePreviewAsync(int actorId, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var row = await RowAsync(id, tracked: false, ct);
        var impact = SupplyCatalogSupport.StateImpactMessage(request.Active);
        if (await StateImpactAsync(row, request.Active, ct) is { } extra) impact += " " + extra;
        return new(await DtoAsync(row, ct), row.IsActive != request.Active, [], impact);
    }

    public Task<TDto> SetStateAsync(int actorId, Guid id, ReferenceStateRequest request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<TDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:state", new { id, request }, async _ =>
        {
            var reason = SupplyCatalogSupport.Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion || row.IsActive == request.Active) throw Error("MSG41", 409);
            var before = await DtoAsync(row, ct);
            row.IsActive = request.Active;
            row.Version++;
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (row.Id, reason, before, await DtoAsync(row, ct));
        }, ct);

    public async Task<IReadOnlyList<SupplyHistoryItem<TDto>>> HistoryAsync(int actorId, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        await RowAsync(id, tracked: false, ct);
        return await SupplyCatalogSupport.HistoryAsync<TDto>(db, id, ct);
    }

    protected T Identity<T>(T entity, string? code, string? name) where T : TEntity
    {
        (entity.Code, entity.Name, _) = SupplyCatalogSupport.ValidateIdentity(code, name, NameMax);
        return entity;
    }

    protected static string? Text(string? value, string label, string field, int max) => SupplyCatalogSupport.Text(value, label, field, max);
    protected static long? Price(long? value) => SupplyCatalogSupport.Price(value);

    // Uniqueness includes inactive rows: deactivation never frees a code or name (SET-01/03).
    private async Task UniqueAsync(TEntity next, Guid? except, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Id != except && x.Code == next.Code, ct)) throw new BusinessException(409, ReferenceMessages.DuplicateCode);
        var upperName = next.Name.ToUpperInvariant();
        if (await Set.AnyAsync(x => x.Id != except && x.Name.ToUpper() == upperName, ct)) throw new BusinessException(409, ReferenceMessages.DuplicateName);
    }

    protected async Task<TEntity> RowAsync(Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? Set : Set.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG42", 404);

    private async Task<TDto> DtoAsync(TEntity row, CancellationToken ct) => (await DtosAsync([row], ct))[0];

    protected Task<TDto[]> DtosAsync(IReadOnlyList<TEntity> rows, CancellationToken ct) => MapAsync(rows, ct);
}

public sealed partial class ProviderCatalog(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access)
    : SupplyCatalog<CatalogProvider, ProviderInput, ProviderDto>(db, clock, access)
{
    protected override string Resource => "providers";
    protected override DbSet<CatalogProvider> Set => Db.CatalogProviders;
    protected override int NameMax => 160;

    // Tabs: Supplier shows Supplier+Both, Lab shows Lab+Both; one Both company keeps one ID.
    protected override IQueryable<CatalogProvider> Filter(IQueryable<CatalogProvider> query, string? type) => type switch
    {
        null => query,
        ProviderTypes.Supplier => query.Where(x => x.Type != ProviderTypes.Lab),
        ProviderTypes.Lab => query.Where(x => x.Type != ProviderTypes.Supplier),
        _ => throw Error("MSG04", label: "Loại công ty", field: "type")
    };

    protected override Task<CatalogProvider> NormalizeAsync(ProviderInput? input, CatalogProvider? current, CancellationToken ct)
    {
        if (input == null) throw Error("MSG04", label: "Công ty", field: "entry");
        var row = Identity(new CatalogProvider(), input.Code, input.Name);
        row.Type = ProviderTypes.All.Contains(input.Type) ? input.Type! : throw Error("MSG04", label: "Loại công ty", field: "type");
        row.Address = Text(input.Address, "Địa chỉ", "address", 1000);
        row.Phone = Text(input.Phone, "Số điện thoại", "phone", 20);
        if (row.Phone != null && (!PhonePattern().IsMatch(row.Phone) || row.Phone.Count(char.IsAsciiDigit) < 8)) throw Error("MSG04", label: "Số điện thoại", field: "phone");
        row.Email = Text(input.Email, "Email", "email", 254);
        if (row.Email != null && (!MailAddress.TryCreate(row.Email, out var mail) || mail.Address != row.Email)) throw Error("MSG04", label: "Email", field: "email");
        return Task.FromResult(row);
    }

    protected override bool SameContent(CatalogProvider a, CatalogProvider b) => a.Code == b.Code && a.Name == b.Name && a.Type == b.Type
        && a.Address == b.Address && a.Phone == b.Phone && a.Email == b.Email;

    protected override void Copy(CatalogProvider from, CatalogProvider to) { to.Type = from.Type; to.Address = from.Address; to.Phone = from.Phone; to.Email = from.Email; }

    // Dropping the Lab role while lab work types still default to this company would orphan them (SET-AT-15).
    protected override async Task UpdateGuardAsync(CatalogProvider current, CatalogProvider next, CancellationToken ct)
    {
        if (next.Type == ProviderTypes.Supplier && current.Type != ProviderTypes.Supplier && await Db.LabWorkTypes.AnyAsync(x => x.ProviderId == current.Id, ct))
            throw new BusinessException(409, SystemMessages.Get("MSG52") with { Field = "type" });
    }

    protected override async Task<string?> StateImpactAsync(CatalogProvider row, bool active, CancellationToken ct)
    {
        var count = active ? 0 : await Db.LabWorkTypes.CountAsync(x => x.ProviderId == row.Id && x.IsActive, ct);
        return count > 0 ? $"{count} loại công việc labo đang dùng công ty này làm mặc định vẫn giữ liên kết." : null;
    }

    protected override Task<ProviderDto[]> MapAsync(IReadOnlyList<CatalogProvider> rows, CancellationToken ct) =>
        Task.FromResult(rows.Select(x => new ProviderDto(x.Id, x.Code, x.Name, x.Type, x.Address, x.Phone, x.Email, x.IsActive, x.Version)).ToArray());

    [GeneratedRegex(@"^\+?[0-9 ().-]+$")]
    private static partial Regex PhonePattern();
}

public sealed class LabWorkTypeCatalog(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access)
    : SupplyCatalog<LabWorkType, LabWorkTypeInput, LabWorkTypeDto>(db, clock, access)
{
    protected override string Resource => "lab-work-types";
    protected override DbSet<LabWorkType> Set => Db.LabWorkTypes;
    protected override int NameMax => 160;

    protected override async Task<LabWorkType> NormalizeAsync(LabWorkTypeInput? input, LabWorkType? current, CancellationToken ct)
    {
        if (input == null) throw Error("MSG04", label: "Loại công việc labo", field: "entry");
        var row = Identity(new LabWorkType(), input.Code, input.Name);
        row.Unit = LabWorkUnits.All.Contains(input.Unit) ? input.Unit! : throw Error("MSG04", label: "Đơn vị", field: "unit");
        row.ReferencePrice = Price(input.ReferencePrice);
        row.Warranty = Text(input.Warranty, "Bảo hành", "warranty", 1000);
        if (input.ProviderId is { } providerId)
        {
            // A new default must be an active Lab/Both company; the one already attached stays valid after deactivation (SET-01).
            var provider = await Db.CatalogProviders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == providerId, ct);
            var kept = providerId == current?.ProviderId;
            if (provider == null || provider.Type == ProviderTypes.Supplier || (!provider.IsActive && !kept)) throw Error("MSG04", label: "Công ty labo", field: "providerId");
            row.ProviderId = providerId;
        }
        return row;
    }

    protected override bool SameContent(LabWorkType a, LabWorkType b) => a.Code == b.Code && a.Name == b.Name && a.ProviderId == b.ProviderId
        && a.Unit == b.Unit && a.ReferencePrice == b.ReferencePrice && a.Warranty == b.Warranty;

    protected override void Copy(LabWorkType from, LabWorkType to) { to.ProviderId = from.ProviderId; to.Unit = from.Unit; to.ReferencePrice = from.ReferencePrice; to.Warranty = from.Warranty; }

    protected override async Task<LabWorkTypeDto[]> MapAsync(IReadOnlyList<LabWorkType> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.ProviderId).OfType<Guid>().Distinct().ToArray();
        var providers = await Db.CatalogProviders.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(x =>
        {
            var provider = x.ProviderId is { } id ? providers.GetValueOrDefault(id) : null;
            return new LabWorkTypeDto(x.Id, x.Code, x.Name, x.ProviderId, provider?.Name, provider?.IsActive, x.Unit, x.ReferencePrice, x.Warranty,
                x.IsActive, x.Version);
        }).ToArray();
    }
}

public sealed class MedicineCatalog(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IMedicineCatalog
{
    private const string Resource = "medicines";
    private const int NameMax = 200;

    private async Task AuthorizeReadAsync(int actorId) =>
        await access.RequireAsync(actorId, $"catalog.{Resource}.read", CancellationToken.None);

    private static MedicineDto ToDto(CatalogMedicine x) => new(
        x.Id, x.Code, x.Name, x.Spec.ActiveIngredient, x.Spec.Strength, x.Spec.DosageForm, x.BaseUnit,
        x.Spec.Packaging, x.Spec.Instructions, x.ReferencePrice, x.PrescriptionEligible, x.InternalStockTracked, x.IsActive, x.Version);

    private IQueryable<CatalogMedicine> SearchQuery(string? q, string? status, string? type)
    {
        var term = q?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        IQueryable<CatalogMedicine> query = type switch
        {
            null => db.CatalogMedicines.AsNoTracking(),
            "Clinic" => db.CatalogMedicines.AsNoTracking().Where(x => x.InternalStockTracked),
            "External" => db.CatalogMedicines.AsNoTracking().Where(x => !x.InternalStockTracked),
            _ => throw SupplyCatalogSupport.Error("MSG04", label: "Loại thuốc", field: "type")
        };
        query = (status ?? "Active") switch
        {
            "Active" => query.Where(x => x.IsActive), "Inactive" => query.Where(x => !x.IsActive), "All" => query,
            _ => throw SupplyCatalogSupport.Error("MSG04", label: "Trạng thái", field: "status")
        };
        if (term.Length > 0)
        {
            var like = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, like) || EF.Functions.ILike(x.Code, like));
        }
        return query.OrderBy(x => x.Name.ToUpper()).ThenBy(x => x.Id);
    }

    private static CatalogMedicine ValidateAndBuild(MedicineInput? input)
    {
        if (input == null) throw SupplyCatalogSupport.Error("MSG04", label: "Thuốc", field: "entry");
        var (code, name, _) = SupplyCatalogSupport.ValidateIdentity(input.Code, input.Name, NameMax);
        return new CatalogMedicine
        {
            Code = code,
            Name = name,
            BaseUnit = SupplyCatalogSupport.Text(input.BaseUnit, "Đơn vị gốc", "baseUnit", 40)
                ?? throw SupplyCatalogSupport.Error("MSG02", label: "Đơn vị gốc", field: "baseUnit"),
            ReferencePrice = SupplyCatalogSupport.Price(input.ReferencePrice),
            PrescriptionEligible = input.PrescriptionEligible,
            InternalStockTracked = input.InternalStockTracked,
            Spec = new CatalogMedicineSpec
            {
                ActiveIngredient = SupplyCatalogSupport.Text(input.ActiveIngredient, "Hoạt chất", "activeIngredient", 200),
                Strength = SupplyCatalogSupport.Text(input.Strength, "Hàm lượng", "strength", 200),
                DosageForm = SupplyCatalogSupport.Text(input.DosageForm, "Dạng bào chế", "dosageForm", 200),
                Packaging = SupplyCatalogSupport.Text(input.Packaging, "Quy cách đóng gói", "packaging", 160),
                Instructions = SupplyCatalogSupport.Text(input.Instructions, "Hướng dẫn sử dụng", "instructions", 1000)
            }
        };
    }

    private async Task EnsureUniqueAsync(string code, string name, Guid? except, CancellationToken ct)
    {
        if (await db.CatalogMedicines.AnyAsync(x => x.Id != except && x.Code == code, ct))
            throw new BusinessException(409, ReferenceMessages.DuplicateCode);
        var upperName = name.ToUpperInvariant();
        if (await db.CatalogMedicines.AnyAsync(x => x.Id != except && x.Name.ToUpper() == upperName, ct))
            throw new BusinessException(409, ReferenceMessages.DuplicateName);
    }

    private static bool SameContent(CatalogMedicine a, CatalogMedicine b) =>
        a.Code == b.Code && a.Name == b.Name && a.BaseUnit == b.BaseUnit && a.ReferencePrice == b.ReferencePrice
        && a.PrescriptionEligible == b.PrescriptionEligible && a.InternalStockTracked == b.InternalStockTracked
        && a.Spec.ActiveIngredient == b.Spec.ActiveIngredient && a.Spec.Strength == b.Spec.Strength
        && a.Spec.DosageForm == b.Spec.DosageForm && a.Spec.Packaging == b.Spec.Packaging && a.Spec.Instructions == b.Spec.Instructions;

    private async Task<CatalogMedicine> RowAsync(Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? db.CatalogMedicines : db.CatalogMedicines.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw SupplyCatalogSupport.Error("MSG42", 404);

    public async Task<CatalogPage<MedicineDto>> ListAsync(int actorId, string? q, string? status, string? type, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeReadAsync(actorId);
        SupplyCatalogSupport.ValidateSearchTerm(q, page, pageSize);
        var query = SearchQuery(q, status, type);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(ToDto).ToArray(), total, page, pageSize);
    }

    public async Task<MedicineDto> GetAsync(int actorId, Guid id, CancellationToken ct)
    {
        await AuthorizeReadAsync(actorId);
        return ToDto(await RowAsync(id, tracked: false, ct));
    }

    public Task<MedicineDto> CreateAsync(int actorId, CreateSupplyRequest<MedicineInput> request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<MedicineDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:create", request, async _ =>
        {
            var next = ValidateAndBuild(request.Entry);
            await EnsureUniqueAsync(next.Code, next.Name, null, ct);
            db.CatalogMedicines.Add(next);
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (next.Id, null, null, ToDto(next));
        }, ct);

    public Task<MedicineDto> UpdateAsync(int actorId, Guid id, UpdateSupplyRequest<MedicineInput> request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<MedicineDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:update", new { id, request }, async _ =>
        {
            var reason = SupplyCatalogSupport.Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion) throw SupplyCatalogSupport.Error("MSG41", 409);
            var next = ValidateAndBuild(request.Entry);
            if (SameContent(row, next)) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
            await EnsureUniqueAsync(next.Code, next.Name, id, ct);
            var before = ToDto(row);
            row.Code = next.Code; row.Name = next.Name; row.BaseUnit = next.BaseUnit; row.ReferencePrice = next.ReferencePrice;
            row.PrescriptionEligible = next.PrescriptionEligible; row.InternalStockTracked = next.InternalStockTracked;
            row.Spec = next.Spec; row.Version++;
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (row.Id, reason, before, ToDto(row));
        }, ct);

    public async Task<SupplyStatePreview<MedicineDto>> StatePreviewAsync(int actorId, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeReadAsync(actorId);
        var row = await RowAsync(id, tracked: false, ct);
        return new(ToDto(row), row.IsActive != request.Active, [], SupplyCatalogSupport.StateImpactMessage(request.Active));
    }

    public Task<MedicineDto> SetStateAsync(int actorId, Guid id, ReferenceStateRequest request, CancellationToken ct) =>
        SupplyCatalogSupport.CommandAsync<MedicineDto>(db, clock, access, Resource, actorId, request.RequestKey, $"{Resource}:state", new { id, request }, async _ =>
        {
            var reason = SupplyCatalogSupport.Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion || row.IsActive == request.Active) throw SupplyCatalogSupport.Error("MSG41", 409);
            var before = ToDto(row);
            row.IsActive = request.Active; row.Version++;
            await SupplyCatalogSupport.SaveWithUniqueCheckAsync(db, ct);
            return (row.Id, reason, before, ToDto(row));
        }, ct);

    public async Task<IReadOnlyList<SupplyHistoryItem<MedicineDto>>> HistoryAsync(int actorId, Guid id, CancellationToken ct)
    {
        await AuthorizeReadAsync(actorId);
        await RowAsync(id, tracked: false, ct);
        return await SupplyCatalogSupport.HistoryAsync<MedicineDto>(db, id, ct);
    }

    // Same filter as the list, read-only; at most 10 000 rows per file.
    public async Task<byte[]> ExportAsync(int actorId, string? q, string? status, CancellationToken ct)
    {
        await AuthorizeReadAsync(actorId);
        var rows = await SearchQuery(q, status, null).Take(10_000).ToListAsync(ct);
        string[] header = ["Mã", "Tên thuốc", "Hoạt chất", "Hàm lượng", "Dạng bào chế", "Đơn vị gốc", "Quy cách đóng gói", "Hướng dẫn sử dụng",
            "Giá tham khảo (VND)", "Dùng kê đơn", "Theo dõi tồn nội bộ", "Trạng thái"];
        return Xlsx.Write("Thuốc", [header, .. rows.Select(x => new[] { x.Code, x.Name, x.Spec.ActiveIngredient, x.Spec.Strength, x.Spec.DosageForm,
            x.BaseUnit, x.Spec.Packaging, x.Spec.Instructions, x.ReferencePrice?.ToString(), x.PrescriptionEligible ? "Có" : "Không",
            x.InternalStockTracked ? "Có" : "Không", x.IsActive ? "Đang dùng" : "Ngừng dùng" })]);
    }
}

// Minimal single-sheet xlsx writer. Every cell is an inline string, so text starting with =, +, - or @ can never become a formula (S58 §2).
internal static class Xlsx
{
    public static byte[] Write(string sheetName, IEnumerable<string?[]> rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Entry(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Entry("[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Entry("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Entry("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Entry("xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{SecurityElement.Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            var sheet = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
            foreach (var row in rows)
            {
                sheet.Append("<row>");
                foreach (var cell in row)
                    sheet.Append("<c t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(SecurityElement.Escape(Clean(cell ?? ""))).Append("</t></is></c>");
                sheet.Append("</row>");
            }
            Entry("xl/worksheets/sheet1.xml", sheet.Append("</sheetData></worksheet>").ToString());
        }
        return stream.ToArray();
    }

    // XML 1.0 forbids most control characters; drop them instead of producing a corrupt file.
    private static string Clean(string value) => new(value.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray());
}
