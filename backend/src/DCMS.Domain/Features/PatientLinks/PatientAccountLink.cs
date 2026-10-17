namespace DCMS.Domain.Features.PatientLinks;

public static class PatientLinkStatus
{
    public const string Pending = "PENDING";
    public const string Verified = "VERIFIED";
    public const string Rejected = "REJECTED";
    public const string Revoked = "REVOKED";
    public const string Withdrawn = "WITHDRAWN";
    public const string NotLinked = "NOT_LINKED";
    public static readonly string[] All = [Pending, Verified, Rejected, Revoked, Withdrawn];
}

// SELF record link request (UC-36). Only VERIFIED grants own-record access.
public sealed class PatientAccountLink
{
    public int Id { get; set; }
    public string RequestCode { get; set; } = "";
    public int AccountId { get; set; }
    public int? PatientId { get; set; }
    public string Status { get; set; } = PatientLinkStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; }
    public string DeclaredFullName { get; set; } = "";
    public DateOnly? DeclaredBirthDate { get; set; }
    public string? DeclaredPatientCode { get; set; }
    public string? RequestNote { get; set; }
    public int? ReviewingEmployeeId { get; set; }
    public string? VerificationNote { get; set; }
    public string? DecisionReason { get; set; }
    public string? PublicNote { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public int? RevokingEmployeeId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class PatientLinkAudit
{
    public long Id { get; set; }
    public int LinkId { get; set; }
    public PatientAccountLink? Link { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string BeforeJson { get; set; } = "null";
    public string AfterJson { get; set; } = "null";
}
