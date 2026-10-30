using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DCMS.Infrastructure.Features.SystemSettings;

// S58 danh mục tham chiếu (docs/specs/S58-REFERENCE-CATALOGS.md).
public sealed partial class ReferenceCatalogService(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IReferenceCatalogService
{
    private static readonly int[] PageSizes = [20, 50, 100];
    private const string Impact = "Mục sẽ không còn được chọn cho giao dịch mới; dữ liệu đã ghi giữ nguyên.";
    private sealed record Change(string? Reason, ReferenceEntryDto? Before);

    private static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null, int max = 0) =>
        new(status, SystemMessages.Get(code, label, max) with { Field = field });

    private async Task<string[]> RolesAsync(int actorId)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        return [.. await users.GetRolesAsync(user)];
    }

    // Catalog grants from access groups (catalog.{kind}.{action}); the owner holds them all. Customer groups have no grant
    // yet and stay owner-only (docs/specs/S58-SETTINGS-MERGE.vi.md).
    private async Task AuthorizeAsync(int actorId, string kind, string action, CancellationToken ct)
    {
        Kind(kind);
        if (DCMS.Application.Features.Settings.SettingsGrants.CatalogKinds.Contains(kind)) await access.RequireAsync(actorId, $"catalog.{kind}.{action}", ct);
        else if (!(await RolesAsync(actorId)).Contains(AccountRoles.ClinicOwner)) throw Error("MSG06", 403);
    }

    private static string Kind(string kind) => ReferenceKinds.All.Contains(kind) ? kind : throw Error("MSG04", label: "Loại danh mục", field: "kind");

    public async Task<CatalogPage<ReferenceEntryDto>> ListAsync(int actorId, string kind, string? q, string? status, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId, kind, "read", ct);
        if (page < 1 || !PageSizes.Contains(pageSize) || page > int.MaxValue / pageSize) throw Error("MSG04", label: "Phân trang");
        if (q?.Length > 100) throw Error("MSG03", label: "Từ khóa", max: 100);
        var term = q?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        var query = db.ReferenceEntries.AsNoTracking().Where(x => x.Kind == kind);
        query = (status ?? "Active") switch
        {
            "Active" => query.Where(x => x.IsActive), "Inactive" => query.Where(x => !x.IsActive), "All" => query,
            _ => throw Error("MSG04", label: "Trạng thái", field: "status")
        };
        if (term.Length > 0) query = query.Where(x => x.NormalizedName.Contains(term) || (x.Code != null && x.Code.Contains(term)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(await DtosAsync(rows, ct), total, page, pageSize);
    }

    public async Task<ReferenceEntryDto> GetAsync(int actorId, string kind, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actorId, kind, "read", ct);
        return await DtoAsync(await RowAsync(Kind(kind), id, tracked: false, ct), ct);
    }

    public Task<ReferenceEntryDto> CreateAsync(int actorId, string kind, CreateReferenceRequest request, CancellationToken ct) =>
        Command(actorId, kind, "create", request.RequestKey, "ReferenceCreate", new { kind, request }, async now =>
        {
            var (code, name, description) = Validate(Kind(kind), request.Entry);
            await UniqueAsync(kind, code, name, null, ct);
            var row = new ReferenceEntry { Kind = kind, Code = code, Name = name, NormalizedName = name.ToUpperInvariant(), Description = description,
                CreatedAt = now, CreatedBy = actorId, UpdatedAt = now, UpdatedBy = actorId };
            db.ReferenceEntries.Add(row);
            return (row, new Change(null, null));
        }, ct);

    public Task<ReferenceEntryDto> UpdateAsync(int actorId, string kind, Guid id, UpdateReferenceRequest request, CancellationToken ct) =>
        Command(actorId, kind, "update", request.RequestKey, "ReferenceUpdate", new { kind, id, request }, async now =>
        {
            var reason = Reason(request.Reason);
            var row = await RowAsync(Kind(kind), id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion) throw Error("MSG111", 409);
            var (code, name, description) = Validate(kind, request.Entry);
            if (code == row.Code && name == row.Name && description == row.Description) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
            await UniqueAsync(kind, code, name, id, ct);
            var before = await DtoAsync(row, ct);
            row.Code = code; row.Name = name; row.NormalizedName = name.ToUpperInvariant(); row.Description = description;
            Touch(row, actorId, now);
            return (row, new Change(reason, before));
        }, ct);

    public async Task<ReferenceStatePreview> StatePreviewAsync(int actorId, string kind, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId, kind, "deactivate", ct);
        var row = await RowAsync(Kind(kind), id, tracked: false, ct);
        // Reserved expense categories are the only blocker (GBR-182); linked patients never block deactivation (SET-01).
        string[] blockers = !request.Active && row.IsReserved ? [ReferenceMessages.Reserved.Message] : [];
        var dto = await DtoAsync(row, ct);
        var impact = request.Active ? "Mục sẽ được chọn lại cho giao dịch mới." : Impact;
        if (dto.PatientCount > 0) impact += $" Đang gắn với {dto.PatientCount} hồ sơ bệnh nhân; các hồ sơ này giữ nguyên giá trị.";
        return new(dto, blockers.Length == 0 && row.IsActive != request.Active, blockers, impact);
    }

    public Task<ReferenceEntryDto> SetStateAsync(int actorId, string kind, Guid id, ReferenceStateRequest request, CancellationToken ct) =>
        Command(actorId, kind, "deactivate", request.RequestKey, "ReferenceState", new { kind, id, request }, async now =>
        {
            var reason = Reason(request.Reason);
            var row = await RowAsync(Kind(kind), id, tracked: true, ct);
            if (row.Version != request.ExpectedVersion || row.IsActive == request.Active) throw Error("MSG111", 409);
            if (!request.Active && row.IsReserved) throw new BusinessException(409, ReferenceMessages.Reserved);
            var before = await DtoAsync(row, ct);
            row.IsActive = request.Active;
            Touch(row, actorId, now);
            return (row, new Change(reason, before));
        }, ct);

    public async Task<IReadOnlyList<ReferenceHistoryItem>> HistoryAsync(int actorId, string kind, Guid id, CancellationToken ct)
    {
        await AuthorizeAsync(actorId, kind, "read", ct);
        await RowAsync(Kind(kind), id, tracked: false, ct);
        var rows = await (from change in db.SystemCatalogChanges.AsNoTracking()
                          join user in db.Users.AsNoTracking() on change.ActorId equals user.Id
                          where change.ResourceId == id
                          orderby change.OccurredAt descending
                          select new { change, user.FullName }).ToListAsync(ct);
        return rows.Select(x =>
        {
            var before = x.change.BeforeJson == null ? null : JsonSerializer.Deserialize<Change>(x.change.BeforeJson, ClinicSettingsStore.Json);
            return new ReferenceHistoryItem(x.change.OccurredAt, x.FullName, x.change.Command, before?.Reason, before?.Before,
                JsonSerializer.Deserialize<ReferenceEntryDto>(x.change.ResultJson, ClinicSettingsStore.Json)!);
        }).ToArray();
    }

    // UC-51 A5: business consumers pick active entries without settings rights; access follows the consumer's role.
    public async Task<IReadOnlyList<ReferenceOption>> OptionsAsync(int actorId, string? kind, string? search, CancellationToken ct)
    {
        var assigned = await RolesAsync(actorId);
        var effective = AccountRoles.Effective(assigned);
        if (search?.Length > 100) throw Error("MSG03", label: "Từ khóa", max: 100);
        if (kind is "suppliers" or "lab-providers" or "lab-work-types" or "medicines") return await SupplyOptionsAsync(kind, effective, search, ct);
        var allowed = Kind(kind ?? "") switch
        {
            ReferenceKinds.Diagnoses => effective.Contains(AccountRoles.Dentist),
            ReferenceKinds.ExpenseCategories => assigned.Contains(AccountRoles.ClinicOwner),
            _ => effective.Contains(AccountRoles.ClinicAssistant) || effective.Contains(AccountRoles.Dentist)
        };
        if (!allowed) throw Error("MSG06", 403);
        if (search?.Length > 100) throw Error("MSG03", label: "Từ khóa", max: 100);
        var term = search?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        return await db.ReferenceEntries.AsNoTracking()
            .Where(x => x.Kind == kind && x.IsActive && (term == "" || x.NormalizedName.Contains(term) || (x.Code != null && x.Code.Contains(term))))
            .OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Take(kind == ReferenceKinds.Diagnoses ? 500 : 50)
            .Select(x => new ReferenceOption(x.Id, x.Code, x.Name, x.Version)).ToArrayAsync(ct);
    }

    // S58 đợt 4 typed catalogs: same projection shape, never prices or contacts.
    private async Task<IReadOnlyList<ReferenceOption>> SupplyOptionsAsync(string kind, string[] effective, string? search, CancellationToken ct)
    {
        var dentist = effective.Contains(AccountRoles.Dentist);
        if (kind == "medicines" ? !dentist : !(dentist || effective.Contains(AccountRoles.ClinicAssistant))) throw Error("MSG06", 403);
        var term = search?.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant() ?? "";
        IQueryable<SupplyEntity> query = kind switch
        {
            "suppliers" => db.CatalogProviders.Where(x => x.Type != ProviderTypes.Lab),
            "lab-providers" => db.CatalogProviders.Where(x => x.Type != ProviderTypes.Supplier),
            "lab-work-types" => db.LabWorkTypes,
            _ => db.CatalogMedicines.Where(x => x.PrescriptionEligible)
        };
        return await query.AsNoTracking().Where(x => x.IsActive && (term == "" || x.NormalizedName.Contains(term) || x.Code.Contains(term)))
            .OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Take(50).Select(x => new ReferenceOption(x.Id, x.Code, x.Name, x.Version)).ToArrayAsync(ct);
    }

    private async Task<ReferenceEntryDto> Command(int actorId, string kind, string action, Guid key, string command, object request,
        Func<DateTimeOffset, Task<(ReferenceEntry Row, Change Change)>> apply, CancellationToken ct)
    {
        await AuthorizeAsync(actorId, kind, action, ct);
        if (key == Guid.Empty) throw Error("MSG04", label: "Mã yêu cầu", field: "requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362059)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, ClinicSettingsStore.Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<ReferenceEntryDto>(prior.ResultJson, ClinicSettingsStore.Json) ?? throw Error("MSG144", 503);
        }
        var now = clock.GetUtcNow();
        var (row, change) = await apply(now);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new BusinessException(409, pg.ConstraintName?.Contains("Code") == true ? ReferenceMessages.DuplicateCode : ReferenceMessages.DuplicateName);
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

    private static (string? Code, string Name, string? Description) Validate(string kind, ReferenceEntryInput? entry)
    {
        if (entry == null) throw Error("MSG04", label: "Danh mục", field: "entry");
        var name = entry.Name?.Trim().Normalize(NormalizationForm.FormC) ?? "";
        if (name.Length == 0) throw Error("MSG02", label: "Tên", field: "name");
        if (name.Length > 160) throw Error("MSG03", label: "Tên", field: "name", max: 160);
        var code = string.IsNullOrWhiteSpace(entry.Code) ? null : entry.Code.Trim().ToUpperInvariant();
        // Diagnoses keep an optional ICD reference code; every other kind needs a business code (S58 §6).
        if (code == null && kind != ReferenceKinds.Diagnoses) throw Error("MSG02", label: "Mã", field: "code");
        if (code?.Length > 40) throw Error("MSG03", label: "Mã", field: "code", max: 40);
        if (code != null && !CodePattern().IsMatch(code)) throw Error("MSG04", label: "Mã", field: "code");
        var description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description.Trim().Normalize(NormalizationForm.FormC);
        if (description?.Length > 1000) throw Error("MSG03", label: "Mô tả", field: "description", max: 1000);
        return (code, name, description);
    }

    private static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length == 0) throw Error("MSG02", label: "Lý do", field: "reason");
        return reason.Length <= 500 ? reason : throw Error("MSG03", label: "Lý do", field: "reason", max: 500);
    }

    // Uniqueness covers inactive entries too: deactivation never frees a name or code (SET-01/03).
    private async Task UniqueAsync(string kind, string? code, string name, Guid? except, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        if (code != null && await db.ReferenceEntries.AnyAsync(x => x.Kind == kind && x.Id != except && x.Code == code, ct))
            throw new BusinessException(409, ReferenceMessages.DuplicateCode);
        if (await db.ReferenceEntries.AnyAsync(x => x.Kind == kind && x.Id != except && x.NormalizedName == normalized, ct))
            throw new BusinessException(409, ReferenceMessages.DuplicateName);
    }

    private static void Touch(ReferenceEntry row, int actorId, DateTimeOffset now) { row.Version++; row.UpdatedAt = now; row.UpdatedBy = actorId; }

    private async Task<ReferenceEntry> RowAsync(string kind, Guid id, bool tracked, CancellationToken ct) =>
        await (tracked ? db.ReferenceEntries : db.ReferenceEntries.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id && x.Kind == kind, ct)
        ?? throw Error("MSG113", 404);

    private async Task<ReferenceEntryDto> DtoAsync(ReferenceEntry row, CancellationToken ct) => (await DtosAsync([row], ct))[0];

    private async Task<ReferenceEntryDto[]> DtosAsync(IReadOnlyList<ReferenceEntry> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(x => new[] { x.CreatedBy, x.UpdatedBy }).OfType<int>().Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        string Name(int? id) => id is { } value && names.TryGetValue(value, out var name) ? name : "Hệ thống";
        var counts = await PatientCountsAsync(rows, ct);
        return rows.Select(x => new ReferenceEntryDto(x.Id, x.Kind, x.Code, x.Name, x.Description, x.IsReserved, x.IsActive, x.Version,
            x.CreatedAt, Name(x.CreatedBy), x.UpdatedAt, Name(x.UpdatedBy),
            PatientKinds.Contains(x.Kind) ? counts.GetValueOrDefault(x.Id) : null)).ToArray();
    }

    private static readonly string[] PatientKinds = [ReferenceKinds.CustomerGroups, ReferenceKinds.PatientSources, ReferenceKinds.Occupations, ReferenceKinds.IntakeHistory];
    private sealed class UsageRow { public Guid Id { get; set; } public int Count { get; set; } }

    // DCMS-127: membership is the patient's own FK, counted at read time; there is no stored counter to drift.
    private async Task<Dictionary<Guid, int>> PatientCountsAsync(IReadOnlyList<ReferenceEntry> rows, CancellationToken ct)
    {
        var ids = rows.Where(x => PatientKinds.Contains(x.Kind)).Select(x => x.Id).ToArray();
        if (ids.Length == 0) return [];
        var usage = await db.Database.SqlQuery<UsageRow>($"""
            SELECT ref AS "Id", COUNT(*)::int AS "Count" FROM (
                SELECT "SourceId" AS ref FROM "PATIENT_PROFILE" WHERE NOT "IsArchived"
                UNION ALL SELECT "CustomerGroupId" FROM "PATIENT_PROFILE" WHERE NOT "IsArchived"
                UNION ALL SELECT "OccupationId" FROM "PATIENT_PROFILE" WHERE NOT "IsArchived"
                UNION ALL SELECT unnest("IntakeHistoryIds") FROM "PATIENT_PROFILE" WHERE NOT "IsArchived") used
            WHERE ref = ANY({ids}) GROUP BY ref
            """).ToListAsync(ct);
        return usage.ToDictionary(x => x.Id, x => x.Count);
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodePattern();
}
