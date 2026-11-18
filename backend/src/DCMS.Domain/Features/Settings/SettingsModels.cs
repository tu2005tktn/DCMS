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
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
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
    public int? ClaimId { get; set; }
    public string? ClaimValue { get; set; }
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
