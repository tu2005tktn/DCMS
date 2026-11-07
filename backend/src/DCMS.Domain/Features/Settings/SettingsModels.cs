namespace DCMS.Domain.Features.Settings;

public sealed class SettingsVersion
{
    public int Version { get; set; }
    public string ContentJson { get; set; } = "{}";
    public int ActorId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; }
}
public sealed class ReferenceEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public string? Code { get; set; }
    public string? NormalizedCode { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Description { get; set; }
    public bool Reserved { get; set; }
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class AccessGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedCode { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Description { get; set; }
    public string BaseRole { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
    public List<AccessGroupGrant> Grants { get; set; } = [];
}
public sealed class AccessGroupGrant
{
    public Guid GroupId { get; set; }
    public string Permission { get; set; } = "";
}
public sealed class StaffAccessAssignment
{
    public int AccountId { get; set; }
    public Guid GroupId { get; set; }
    public int[] BranchIds { get; set; } = [];
    public int Version { get; set; } = 1;
}
public sealed class SettingsAudit
{
    public int[] BranchIds { get; set; } = [];
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ActorId { get; set; }
    public string Section { get; set; } = "";
    public string Target { get; set; } = "";
    public int Version { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}
public sealed class SettingsReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}

public sealed class BranchProfile
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public Guid? LogoAssetId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? PayeeName { get; set; }
    public int SlotIntervalMinutes { get; set; } = 15;
    public int BookingSettingsVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}

// Logo image stored outside Git and the web root; only verified PNG/JPEG/WebP bytes with metadata removed.
public sealed class BranchAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int BranchId { get; set; }
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public int UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public sealed class BranchChair
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int OpenMinute { get; set; } = 480;
    public int CloseMinute { get; set; } = 1080;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
