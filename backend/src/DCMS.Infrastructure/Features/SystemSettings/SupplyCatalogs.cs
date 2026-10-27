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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DCMS.Infrastructure.Features.SystemSettings;

// S58 đợt 4 (docs/specs/S58-SUPPLY-CATALOGS.md): shared command/audit/version flow; each catalog only validates and maps its own fields.
public abstract partial class SupplyCatalog<TEntity, TInput, TDto>(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
    : ISupplyCatalog<TInput, TDto> where TEntity : SupplyEntity where TInput : class where TDto : class
{
    private const long MaxPrice = 1_000_000_000_000;
    private static readonly int[] PageSizes = [20, 50, 100];
    private sealed record Change(string? Reason, TDto? Before);
    protected DcmsDbContext Db => db;
    protected abstract string Resource { get; }
    protected abstract DbSet<TEntity> Set { get; }
    protected abstract int NameMax { get; }
    // Returns a detached entity holding the normalized, validated values.
    protected abstract Task<TEntity> NormalizeAsync(TInput? input, TEntity? current, CancellationToken ct);
    protected abstract bool SameContent(TEntity current, TEntity next);
    protected abstract void Copy(TEntity from, TEntity to);
    protected abstract Task<TDto[]> MapAsync(IReadOnlyList<TEntity> rows, Func<int, string> actorName, CancellationToken ct);
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, string? type) => type == null ? query : throw Error("MSG04", label: "Loại", field: "type");
    protected virtual Task UpdateGuardAsync(TEntity current, TEntity next, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task<string?> StateImpactAsync(TEntity row, bool active, CancellationToken ct) => Task.FromResult<string?>(null);

    protected static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null, int max = 0) =>
        new(status, SystemMessages.Get(code, label, max) with { Field = field });

    // Same rule as the other S58 sections: an assigned ClinicOwner until an explicit grant store exists.
    protected async Task AuthorizeAsync(int actorId)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" } || !(await users.GetRolesAsync(user)).Contains(Domain.Features.Accounts.AccountRoles.ClinicOwner))
            throw Error("MSG06", 403);
    }

    protected IQueryable<TEntity> Search(string? q, string? status, string? type)
    {
        if (q?.Length > 100) throw Error("MSG03", label: "Từ khóa", max: 100);
        var term = q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        var query = Filter(Set.AsNoTracking(), type);
        query = (status ?? "Active") switch
        {
            "Active" => query.Where(x => x.IsActive), "Inactive" => query.Where(x => !x.IsActive), "All" => query,
            _ => throw Error("MSG04", label: "Trạng thái", field: "status")
        };
        if (term.Length > 0) query = query.Where(x => x.NormalizedName.Contains(term) || x.Code.Contains(term));
        return query.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id);
    }

    public async Task<CatalogPage<TDto>> ListAsync(int actorId, string? q, string? status, string? type, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (page < 1 || !PageSizes.Contains(pageSize) || page > int.MaxValue / pageSize) throw Error("MSG04", label: "Phân trang");
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
        Command(actorId, request.RequestKey, $"{Resource}:create", request, async now =>
        {
            var next = await NormalizeAsync(request.Entry, null, ct);
            await UniqueAsync(next, null, ct);
            next.CreatedAt = next.UpdatedAt = now; next.CreatedBy = next.UpdatedBy = actorId;
            Set.Add(next);
            return (next, new Change(null, null));
        }, ct);

    public Task<TDto> UpdateAsync(int actorId, Guid id, UpdateSupplyRequest<TInput> request, CancellationToken ct) =>
        Command(actorId, request.RequestKey, $"{Resource}:update", new { id, request }, async now =>
        {
            var reason = Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            var next = await NormalizeAsync(request.Entry, row, ct);
            if (SameContent(row, next)) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
            await UniqueAsync(next, id, ct);
            await UpdateGuardAsync(row, next, ct);
            var before = await DtoAsync(row, ct);
            Copy(next, row); row.Code = next.Code; row.Name = next.Name; row.NormalizedName = next.NormalizedName;
            Touch(row, actorId, now);
            return (row, new Change(reason, before));
        }, ct);

    public async Task<SupplyStatePreview<TDto>> StatePreviewAsync(int actorId, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var row = await RowAsync(id, tracked: false, ct);
        var impact = request.Active ? "Mục sẽ được chọn lại cho giao dịch mới." : "Mục sẽ không còn được chọn cho giao dịch mới; dữ liệu đã ghi giữ nguyên.";
        if (await StateImpactAsync(row, request.Active, ct) is { } extra) impact += " " + extra;
        return new(await DtoAsync(row, ct), row.IsActive != request.Active, [], impact);
    }

    public Task<TDto> SetStateAsync(int actorId, Guid id, ReferenceStateRequest request, CancellationToken ct) =>
        Command(actorId, request.RequestKey, $"{Resource}:state", new { id, request }, async now =>
        {
            var reason = Reason(request.Reason);
            var row = await RowAsync(id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion || row.IsActive == request.Active) throw Error("MSG111", 409);
            var before = await DtoAsync(row, ct);
            row.IsActive = request.Active;
            Touch(row, actorId, now);
            return (row, new Change(reason, before));
        }, ct);

    public async Task<IReadOnlyList<SupplyHistoryItem<TDto>>> HistoryAsync(int actorId, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        await RowAsync(id, tracked: false, ct);
        var rows = await (from change in db.SystemCatalogChanges.AsNoTracking()
                          join user in db.Users.AsNoTracking() on change.ActorId equals user.Id
                          where change.ResourceId == id
                          orderby change.OccurredAt descending
                          select new { change, user.FullName }).ToListAsync(ct);
        return rows.Select(x =>
        {
            var before = x.change.BeforeJson == null ? null : JsonSerializer.Deserialize<Change>(x.change.BeforeJson, ClinicSettingsStore.Json);
            return new SupplyHistoryItem<TDto>(x.change.OccurredAt, x.FullName, x.change.Command.Split(':')[^1], before?.Reason, before?.Before,
                JsonSerializer.Deserialize<TDto>(x.change.ResultJson, ClinicSettingsStore.Json)!);
        }).ToArray();
    }

    private async Task<TDto> Command(int actorId, Guid key, string command, object request,
        Func<DateTimeOffset, Task<(TEntity Row, Change Change)>> apply, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (key == Guid.Empty) throw Error("MSG04", label: "Mã yêu cầu", field: "requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362060)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, ClinicSettingsStore.Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<TDto>(prior.ResultJson, ClinicSettingsStore.Json) ?? throw Error("MSG144", 503);
        }
        var now = clock.GetUtcNow();
        var (row, change) = await apply(now);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new BusinessException(409, pg.ConstraintName?.EndsWith("_Code") == true ? ReferenceMessages.DuplicateCode : ReferenceMessages.DuplicateName);
        }
        var result = await DtoAsync(row, ct);
        db.SystemCatalogChanges.Add(new SystemCatalogChange
        {
            RequestKey = key, ActorId = actorId, Command = command, ResourceId = row.Id, Fingerprint = hash,
            BeforeJson = JsonSerializer.Serialize(change, ClinicSettingsStore.Json), ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json), OccurredAt = now
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // Fills Code/Name/NormalizedName on a new detached entity (SET-03).
    protected T Identity<T>(T entity, string? code, string? name) where T : TEntity
    {
        var value = code?.Trim().ToUpperInvariant() ?? "";
        if (value.Length == 0) throw Error("MSG02", label: "Mã", field: "code");
        if (value.Length > 40) throw Error("MSG03", label: "Mã", field: "code", max: 40);
        if (!CodePattern().IsMatch(value)) throw Error("MSG04", label: "Mã", field: "code");
        var label = name?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (label.Length == 0) throw Error("MSG02", label: "Tên", field: "name");
        if (label.Length > NameMax) throw Error("MSG03", label: "Tên", field: "name", max: NameMax);
        entity.Code = value; entity.Name = label; entity.NormalizedName = label.ToUpperInvariant();
        return entity;
    }

    protected static string? Text(string? value, string label, string field, int max)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim().Normalize(NormalizationForm.FormC);
        return text?.Length > max ? throw Error("MSG03", label: label, field: field, max: max) : text;
    }

    protected static long? Price(long? value) => value switch
    {
        < 0 => throw new BusinessException(400, SystemMessages.Get("MSG257") with { Field = "referencePrice" }),
        > MaxPrice => throw Error("MSG04", label: "Giá tham khảo", field: "referencePrice"),
        _ => value
    };

    private static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length == 0) throw Error("MSG02", label: "Lý do", field: "reason");
        return reason.Length <= 500 ? reason : throw Error("MSG03", label: "Lý do", field: "reason", max: 500);
    }

    // Uniqueness includes inactive rows: deactivation never frees a code or name (SET-01/03).
    private async Task UniqueAsync(TEntity next, Guid? except, CancellationToken ct)
    {
        if (await Set.AnyAsync(x => x.Id != except && x.Code == next.Code, ct)) throw new BusinessException(409, ReferenceMessages.DuplicateCode);
        if (await Set.AnyAsync(x => x.Id != except && x.NormalizedName == next.NormalizedName, ct)) throw new BusinessException(409, ReferenceMessages.DuplicateName);
    }

    private static void Touch(TEntity row, int actorId, DateTimeOffset now) { row.Version++; row.UpdatedAt = now; row.UpdatedBy = actorId; }

    protected async Task<TEntity> RowAsync(Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? Set : Set.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);

    private async Task<TDto> DtoAsync(TEntity row, CancellationToken ct) => (await DtosAsync([row], ct))[0];

    protected async Task<TDto[]> DtosAsync(IReadOnlyList<TEntity> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(x => new[] { x.CreatedBy, x.UpdatedBy }).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return await MapAsync(rows, id => names.GetValueOrDefault(id, "—"), ct);
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodePattern();
}

public sealed partial class ProviderCatalog(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
    : SupplyCatalog<CatalogProvider, ProviderInput, ProviderDto>(db, users, clock)
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
            throw new BusinessException(409, SystemMessages.Get("MSG205") with { Field = "type" });
    }

    protected override async Task<string?> StateImpactAsync(CatalogProvider row, bool active, CancellationToken ct)
    {
        var count = active ? 0 : await Db.LabWorkTypes.CountAsync(x => x.ProviderId == row.Id && x.IsActive, ct);
        return count > 0 ? $"{count} loại công việc labo đang dùng công ty này làm mặc định vẫn giữ liên kết." : null;
    }

    protected override Task<ProviderDto[]> MapAsync(IReadOnlyList<CatalogProvider> rows, Func<int, string> actor, CancellationToken ct) =>
        Task.FromResult(rows.Select(x => new ProviderDto(x.Id, x.Code, x.Name, x.Type, x.Address, x.Phone, x.Email, x.IsActive, x.Version,
            x.CreatedAt, actor(x.CreatedBy), x.UpdatedAt, actor(x.UpdatedBy))).ToArray());

    [GeneratedRegex(@"^\+?[0-9 ().-]+$")]
    private static partial Regex PhonePattern();
}

public sealed class LabWorkTypeCatalog(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
    : SupplyCatalog<LabWorkType, LabWorkTypeInput, LabWorkTypeDto>(db, users, clock)
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

    protected override async Task<LabWorkTypeDto[]> MapAsync(IReadOnlyList<LabWorkType> rows, Func<int, string> actor, CancellationToken ct)
    {
        var ids = rows.Select(x => x.ProviderId).OfType<Guid>().Distinct().ToArray();
        var providers = await Db.CatalogProviders.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(x =>
        {
            var provider = x.ProviderId is { } id ? providers.GetValueOrDefault(id) : null;
            return new LabWorkTypeDto(x.Id, x.Code, x.Name, x.ProviderId, provider?.Name, provider?.IsActive, x.Unit, x.ReferencePrice, x.Warranty,
                x.IsActive, x.Version, x.CreatedAt, actor(x.CreatedBy), x.UpdatedAt, actor(x.UpdatedBy));
        }).ToArray();
    }
}

public sealed class MedicineCatalog(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
    : SupplyCatalog<CatalogMedicine, MedicineInput, MedicineDto>(db, users, clock), IMedicineCatalog
{
    protected override string Resource => "medicines";
    protected override DbSet<CatalogMedicine> Set => Db.CatalogMedicines;
    protected override int NameMax => 200;

    protected override Task<CatalogMedicine> NormalizeAsync(MedicineInput? input, CatalogMedicine? current, CancellationToken ct)
    {
        if (input == null) throw Error("MSG04", label: "Thuốc", field: "entry");
        var row = Identity(new CatalogMedicine(), input.Code, input.Name);
        row.ActiveIngredient = Text(input.ActiveIngredient, "Hoạt chất", "activeIngredient", 200);
        row.Strength = Text(input.Strength, "Hàm lượng", "strength", 200);
        row.DosageForm = Text(input.DosageForm, "Dạng bào chế", "dosageForm", 200);
        row.BaseUnit = Text(input.BaseUnit, "Đơn vị gốc", "baseUnit", 40) ?? throw Error("MSG02", label: "Đơn vị gốc", field: "baseUnit");
        row.Packaging = Text(input.Packaging, "Quy cách đóng gói", "packaging", 160);
        row.Instructions = Text(input.Instructions, "Hướng dẫn sử dụng", "instructions", 1000);
        row.ReferencePrice = Price(input.ReferencePrice);
        row.PrescriptionEligible = input.PrescriptionEligible;
        row.InternalStockTracked = input.InternalStockTracked;
        return Task.FromResult(row);
    }

    protected override bool SameContent(CatalogMedicine a, CatalogMedicine b) => a.Code == b.Code && a.Name == b.Name && a.ActiveIngredient == b.ActiveIngredient
        && a.Strength == b.Strength && a.DosageForm == b.DosageForm && a.BaseUnit == b.BaseUnit && a.Packaging == b.Packaging
        && a.Instructions == b.Instructions && a.ReferencePrice == b.ReferencePrice && a.PrescriptionEligible == b.PrescriptionEligible
        && a.InternalStockTracked == b.InternalStockTracked;

    protected override void Copy(CatalogMedicine from, CatalogMedicine to)
    {
        to.ActiveIngredient = from.ActiveIngredient; to.Strength = from.Strength; to.DosageForm = from.DosageForm; to.BaseUnit = from.BaseUnit;
        to.Packaging = from.Packaging; to.Instructions = from.Instructions; to.ReferencePrice = from.ReferencePrice;
        to.PrescriptionEligible = from.PrescriptionEligible; to.InternalStockTracked = from.InternalStockTracked;
    }

    protected override Task<MedicineDto[]> MapAsync(IReadOnlyList<CatalogMedicine> rows, Func<int, string> actor, CancellationToken ct) =>
        Task.FromResult(rows.Select(x => new MedicineDto(x.Id, x.Code, x.Name, x.ActiveIngredient, x.Strength, x.DosageForm, x.BaseUnit, x.Packaging,
            x.Instructions, x.ReferencePrice, x.PrescriptionEligible, x.InternalStockTracked, x.IsActive, x.Version,
            x.CreatedAt, actor(x.CreatedBy), x.UpdatedAt, actor(x.UpdatedBy))).ToArray());

    // Same filter as the list, read-only; at most 10 000 rows per file.
    public async Task<byte[]> ExportAsync(int actorId, string? q, string? status, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var rows = await Search(q, status, null).Take(10_000).ToListAsync(ct);
        string[] header = ["Mã", "Tên thuốc", "Hoạt chất", "Hàm lượng", "Dạng bào chế", "Đơn vị gốc", "Quy cách đóng gói", "Hướng dẫn sử dụng",
            "Giá tham khảo (VND)", "Dùng kê đơn", "Theo dõi tồn nội bộ", "Trạng thái"];
        return Xlsx.Write("Thuốc", [header, .. rows.Select(x => new[] { x.Code, x.Name, x.ActiveIngredient, x.Strength, x.DosageForm, x.BaseUnit,
            x.Packaging, x.Instructions, x.ReferencePrice?.ToString(), x.PrescriptionEligible ? "Có" : "Không", x.InternalStockTracked ? "Có" : "Không",
            x.IsActive ? "Đang dùng" : "Ngừng dùng" })]);
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
