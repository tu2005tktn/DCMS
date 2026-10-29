namespace DCMS.Application.Features.SystemSettings;

// S58 đợt 4. Contract: docs/specs/S58-SUPPLY-CATALOGS.md.
public static class ProviderTypes
{
    public const string Supplier = "Supplier";
    public const string Lab = "Lab";
    public const string Both = "Both";
    public static readonly string[] All = [Supplier, Lab, Both];
}
public static class LabWorkUnits
{
    public static readonly string[] All = ["Jaw", "HalfJaw", "Tooth", "Piece", "Set"];
}

public sealed record ProviderInput(string? Code, string? Name, string? Type, string? Address, string? Phone, string? Email);
public sealed record ProviderDto(Guid Id, string Code, string Name, string Type, string? Address, string? Phone, string? Email, bool Active, int Version,
    DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset UpdatedAt, string UpdatedByName);
public sealed record LabWorkTypeInput(string? Code, string? Name, Guid? ProviderId, string? Unit, long? ReferencePrice, string? Warranty);
public sealed record LabWorkTypeDto(Guid Id, string Code, string Name, Guid? ProviderId, string? ProviderName, bool? ProviderActive, string Unit,
    long? ReferencePrice, string? Warranty, bool Active, int Version, DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset UpdatedAt, string UpdatedByName);
public sealed record MedicineInput(string? Code, string? Name, string? ActiveIngredient, string? Strength, string? DosageForm, string? BaseUnit,
    string? Packaging, string? Instructions, long? ReferencePrice, bool PrescriptionEligible, bool InternalStockTracked);
public sealed record MedicineDto(Guid Id, string Code, string Name, string? ActiveIngredient, string? Strength, string? DosageForm, string BaseUnit,
    string? Packaging, string? Instructions, long? ReferencePrice, bool PrescriptionEligible, bool InternalStockTracked, bool Active, int Version,
    DateTimeOffset CreatedAt, string CreatedByName, DateTimeOffset UpdatedAt, string UpdatedByName);

public sealed record CreateSupplyRequest<T>(Guid RequestKey, T? Entry) where T : class;
public sealed record UpdateSupplyRequest<T>(int ExpectedVersion, Guid RequestKey, string? Reason, T? Entry) where T : class;
public sealed record SupplyStatePreview<T>(T Entry, bool Allowed, IReadOnlyList<string> Blockers, string Impact);
public sealed record SupplyHistoryItem<T>(DateTimeOffset OccurredAt, string ActorName, string Command, string? Reason, T? Before, T After) where T : class;

public interface ISupplyCatalog<TInput, TDto> where TInput : class where TDto : class
{
    Task<CatalogPage<TDto>> ListAsync(int actorId, string? q, string? status, string? type, int page, int pageSize, CancellationToken ct);
    Task<TDto> GetAsync(int actorId, Guid id, CancellationToken ct);
    Task<TDto> CreateAsync(int actorId, CreateSupplyRequest<TInput> request, CancellationToken ct);
    Task<TDto> UpdateAsync(int actorId, Guid id, UpdateSupplyRequest<TInput> request, CancellationToken ct);
    Task<SupplyStatePreview<TDto>> StatePreviewAsync(int actorId, Guid id, ReferenceStatePreviewRequest request, CancellationToken ct);
    Task<TDto> SetStateAsync(int actorId, Guid id, ReferenceStateRequest request, CancellationToken ct);
    Task<IReadOnlyList<SupplyHistoryItem<TDto>>> HistoryAsync(int actorId, Guid id, CancellationToken ct);
}
public interface IMedicineCatalog : ISupplyCatalog<MedicineInput, MedicineDto>
{
    Task<byte[]> ExportAsync(int actorId, string? q, string? status, CancellationToken ct);
}

// Resource-specific authorization: medicines deliberately retain their existing Owner policy.
public static class SupplyCatalogPermissions
{
    public static bool CanManage(string resource, IEnumerable<string> roles)
    {
        var values = roles.ToArray();
        if (values.Contains(DCMS.Domain.Features.Accounts.AccountRoles.ClinicOwner)) return true;
        return resource is "providers" or "lab-work-types" or "materials" or "material-categories" or "units"
            && values.Contains(DCMS.Domain.Features.Accounts.AccountRoles.Dentist);
    }
}
