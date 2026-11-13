namespace DCMS.Domain.Features.SystemSettings;

public sealed class MaterialCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class MaterialCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public Guid CategoryId { get; set; }
    public MaterialCategory Category { get; set; } = null!;
    public string? UsageUnit { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
// Purchase log row (docs/specs/MATERIAL-PURCHASES.md). Name snapshots keep history stable when the catalog changes.
public sealed class MaterialPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialId { get; set; }
    public MaterialCatalogItem Material { get; set; } = null!;
    public string MaterialName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly EnteredOn { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal ConversionFactor { get; set; } = 1;
    public string? UsageUnit { get; set; }
    public decimal UsageQuantity { get; set; }
    public long UnitPriceVnd { get; set; }
    public long TotalVnd { get; set; }
    public long PaidVnd { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int UpdatedBy { get; set; }
}
// One material line consumed by a treatment activity (docs/specs/MATERIAL-STOCK.md).
public sealed class MaterialUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = "";
    public string? UsageUnit { get; set; }
    public decimal Quantity { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? DossierId { get; set; }
    public DateTimeOffset UsedAt { get; set; }
    public int? RecordedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
// Replay receipt and change history of system catalogs, stored in the shared COMMAND_RECEIPT table
// (docs/specs/SETTINGS-CATALOG-STORAGE.vi.md). RequestKey, Fingerprint and ResultJson come from the base receipt.
public sealed class SystemCatalogChange : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string? BeforeJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
