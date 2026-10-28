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
