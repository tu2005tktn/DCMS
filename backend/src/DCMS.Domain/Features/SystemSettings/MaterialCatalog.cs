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
public sealed class SystemCatalogChange
{
    public Guid RequestKey { get; set; }
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string ResultJson { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
