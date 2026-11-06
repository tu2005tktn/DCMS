namespace DCMS.Domain.Features.SystemSettings;

// S58 Thiết lập chung (UC-51): immutable versions; the current one is the highest Version (SET-06).
public sealed class ClinicSettingsVersion
{
    public int Version { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public int ActorId { get; set; }
    public string Reason { get; set; } = "";
    public int? RestoredFromVersion { get; set; }
    public string ContentJson { get; set; } = "{}";
}

// S58 reference catalog entry (UC-51): one table for the simple allowlisted kinds; never hard-deleted (SET-01).
public sealed class ReferenceEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsReserved { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
