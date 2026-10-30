using DCMS.Application.Common;
namespace DCMS.Application.Features.SystemSettings;

// S58 danh mục tham chiếu. Contract: docs/specs/S58-REFERENCE-CATALOGS.md.
public static class ReferenceKinds
{
    public const string Diagnoses = "diagnoses";
    public const string PatientSources = "patient-sources";
    public const string Occupations = "occupations";
    public const string IntakeHistory = "intake-history";
    public const string CareTypes = "care-types";
    public const string ExpenseCategories = "expense-categories";
    public const string CustomerGroups = "customer-groups";
    public static readonly string[] All = [Diagnoses, PatientSources, Occupations, IntakeHistory, CareTypes, ExpenseCategories, CustomerGroups];
}

// PatientCount (DCMS-127): live count of non-archived patients pointing at the entry; null for kinds patients do not reference.
public sealed record ReferenceEntryDto(Guid Id, string Kind, string? Code, string Name, string? Description, bool Reserved, bool Active, int Version,
    DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset UpdatedAt, string UpdatedByName, int? PatientCount = null);
public sealed record ReferenceEntryInput(string? Code, string? Name, string? Description);
public sealed record CreateReferenceRequest(Guid RequestKey, ReferenceEntryInput? Entry);
public sealed record UpdateReferenceRequest(int ExpectedVersion, Guid RequestKey, string? Reason, ReferenceEntryInput? Entry);
public sealed record ReferenceStatePreviewRequest(bool Active);
public sealed record ReferenceStatePreview(ReferenceEntryDto Entry, bool Allowed, IReadOnlyList<string> Blockers, string Impact);
public sealed record ReferenceStateRequest(int ExpectedVersion, Guid RequestKey, bool Active, string? Reason);
public sealed record ReferenceHistoryItem(DateTimeOffset OccurredAt, string ActorName, string Command, string? Reason, ReferenceEntryDto? Before, ReferenceEntryDto After);
// Version lets consumers snapshot the exact entry (examination diagnoses, docs/specs/S58-SETTINGS-MERGE.vi.md).
public sealed record ReferenceOption(Guid Id, string? Code, string Name, int Version = 0);

public static class ReferenceMessages
{
    public static SystemMessage DuplicateCode => new("MSG201", "Mã danh mục đã tồn tại. Vui lòng chọn mã khác.") { Field = "code" };
    public static SystemMessage DuplicateName => new("MSG201", "Tên đã tồn tại trong danh mục. Vui lòng nhập tên khác.") { Field = "name" };
    public static SystemMessage Reserved => new("SETTINGS_RESERVED_ENTRY", "Mục này được hệ thống dùng cho chứng từ liên kết nên không thể ngừng dùng.");
}

public interface IReferenceCatalogService
{
    Task<CatalogPage<ReferenceEntryDto>> ListAsync(int actorId, string kind, string? q, string? status, int page, int pageSize, CancellationToken ct);
    Task<ReferenceEntryDto> GetAsync(int actorId, string kind, Guid id, CancellationToken ct);
    Task<ReferenceEntryDto> CreateAsync(int actorId, string kind, CreateReferenceRequest request, CancellationToken ct);
    Task<ReferenceEntryDto> UpdateAsync(int actorId, string kind, Guid id, UpdateReferenceRequest request, CancellationToken ct);
    Task<ReferenceStatePreview> StatePreviewAsync(int actorId, string kind, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct);
    Task<ReferenceEntryDto> SetStateAsync(int actorId, string kind, Guid id, ReferenceStateRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReferenceHistoryItem>> HistoryAsync(int actorId, string kind, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ReferenceOption>> OptionsAsync(int actorId, string? kind, string? search, CancellationToken ct);
}
