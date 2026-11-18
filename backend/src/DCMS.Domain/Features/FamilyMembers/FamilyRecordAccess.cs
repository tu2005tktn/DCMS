namespace DCMS.Domain.Features.FamilyMembers;

public static class FamilyRecordScopes
{
    public static readonly string[] All = ["Examinations", "Treatment", "Prescriptions", "Documents"];
}

public sealed class FamilyRecordAccessDetails
{
    public string DeclaredFullName { get; set; } = "";
    public DateOnly? DeclaredBirthDate { get; set; }
    public string Relationship { get; set; } = "";
    public string? DeclaredPatientCode { get; set; }
    public string? MatchingInformation { get; set; }
    public string? SharingBasis { get; set; }
    public string? VerificationNote { get; set; }
    public string? DecisionReason { get; set; }
    public string? PublicNote { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

// Separate from PATIENT_ACCOUNT_LINK: a delegate never becomes the patient's self account.
public sealed class FamilyRecordAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyMemberId { get; set; }
    public int OwnerAccountId { get; set; }
    public string Code { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int? PatientId { get; set; }
    public int? BranchId { get; set; }
    public string[] Scopes { get; set; } = [];
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int? DecidedBy { get; set; }
    public int Version { get; set; } = 1;
    public FamilyRecordAccessDetails Details { get; set; } = new();
    public string DeclaredFullName { get => Details.DeclaredFullName; set => Details.DeclaredFullName = value; }
    public DateOnly? DeclaredBirthDate { get => Details.DeclaredBirthDate; set => Details.DeclaredBirthDate = value; }
    public string Relationship { get => Details.Relationship; set => Details.Relationship = value; }
    public string? DeclaredPatientCode { get => Details.DeclaredPatientCode; set => Details.DeclaredPatientCode = value; }
    public string? MatchingInformation { get => Details.MatchingInformation; set => Details.MatchingInformation = value; }
    public string? SharingBasis { get => Details.SharingBasis; set => Details.SharingBasis = value; }
    public string? VerificationNote { get => Details.VerificationNote; set => Details.VerificationNote = value; }
    public string? DecisionReason { get => Details.DecisionReason; set => Details.DecisionReason = value; }
    public string? PublicNote { get => Details.PublicNote; set => Details.PublicNote = value; }
    public DateTimeOffset? DecidedAt { get => Details.DecidedAt; set => Details.DecidedAt = value; }
}

public sealed class FamilyRecordAudit : DCMS.Domain.Common.AuditRecord
{
    public long Id { get; set; }
    public Guid AccessId { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class FamilyRecordReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorAccountId { get; set; }
    public Guid AccessId { get; set; }
}
