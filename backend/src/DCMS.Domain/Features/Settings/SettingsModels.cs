namespace DCMS.Domain.Features.Settings;

// Structure and rows of a retired /api/settings table (SETTINGS_VERSION, OPERATIONAL_REFERENCE_ENTRY), kept so a rollback
// can rebuild it (docs/specs/SETTINGS-CATALOG-STORAGE.vi.md). BeforeJson holds { table, columns, constraints, indexes, rows }.
public sealed class SettingsLegacyArchive : DCMS.Domain.Common.AuditRecord
{
    public string BeforeJson { get; set; } = "{}";
}
public sealed class AccessGroup
{
    public string RecordKind { get; set; } = "PermissionGroup";
    public int? IdentityRoleId { get; set; }
    public string? RoleConcurrencyStamp { get; set; }
    public List<DCMS.Domain.Features.Authentication.AccountClaim> RoleClaims { get; set; } = [];
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
public sealed class SettingsAudit : DCMS.Domain.Common.AuditRecord
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
public sealed class SettingsReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
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
    public int OpenMinute { get; set; } = 0;
    public int CloseMinute { get; set; } = 1440;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
