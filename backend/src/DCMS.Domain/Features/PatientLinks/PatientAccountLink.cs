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

public sealed class PatientAccountLinkDetails
{
    public DateOnly? DeclaredBirthDate { get; set; }
    public string? DeclaredPatientCode { get; set; }
    public string? RequestNote { get; set; }
    public string? VerificationNote { get; set; }
    public string? DecisionReason { get; set; }
    public string? PublicNote { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
}

// SELF record link request (UC-36). Only VERIFIED grants own-record access.
public sealed class PatientAccountLink
{
    public int Id { get; set; }
    public string RequestCode { get; set; } = "";
    public int AccountId { get; set; }
    public int? PatientId { get; set; }
    public string DeclaredFullName { get; set; } = "";
    public string Status { get; set; } = PatientLinkStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; }
    public int? ReviewingEmployeeId { get; set; }
    public int? RevokingEmployeeId { get; set; }
    public int Version { get; set; } = 1;
    public PatientAccountLinkDetails Details { get; set; } = new();
    public DateOnly? DeclaredBirthDate { get => Details.DeclaredBirthDate; set => Details.DeclaredBirthDate = value; }
    public string? DeclaredPatientCode { get => Details.DeclaredPatientCode; set => Details.DeclaredPatientCode = value; }
    public string? RequestNote { get => Details.RequestNote; set => Details.RequestNote = value; }
    public string? VerificationNote { get => Details.VerificationNote; set => Details.VerificationNote = value; }
    public string? DecisionReason { get => Details.DecisionReason; set => Details.DecisionReason = value; }
    public string? PublicNote { get => Details.PublicNote; set => Details.PublicNote = value; }
    public DateTimeOffset? ReviewedAt { get => Details.ReviewedAt; set => Details.ReviewedAt = value; }
    public DateTimeOffset? RevokedAt { get => Details.RevokedAt; set => Details.RevokedAt = value; }
    public DateTimeOffset? WithdrawnAt { get => Details.WithdrawnAt; set => Details.WithdrawnAt = value; }
}

public sealed class PatientLinkAudit : DCMS.Domain.Common.AuditRecord
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
