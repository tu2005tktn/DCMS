namespace DCMS.Domain.Features.SystemSettings;

// S58 đợt 4 typed catalogs (docs/specs/S58-SUPPLY-CATALOGS.md). Never hard-deleted; deactivation hides an entry from new choices only.
public abstract class SupplyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int UpdatedBy { get; set; }
}

// One company row serves both the material-supplier and lab tabs (Type Both is never duplicated).
public sealed class CatalogProvider : SupplyEntity
{
    public string Type { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class LabWorkType : SupplyEntity
{
    public Guid? ProviderId { get; set; }
    public string Unit { get; set; } = "";
    // Whole VND; null means not yet known, 0 is a real reference price of zero (SET-04).
    public long? ReferencePrice { get; set; }
    public string? Warranty { get; set; }
}

public sealed class CatalogMedicine : SupplyEntity
{
    public string? ActiveIngredient { get; set; }
    public string? Strength { get; set; }
    public string? DosageForm { get; set; }
    public string BaseUnit { get; set; } = "";
    public string? Packaging { get; set; }
    public string? Instructions { get; set; }
    public long? ReferencePrice { get; set; }
    public bool PrescriptionEligible { get; set; }
    public bool InternalStockTracked { get; set; }
}
