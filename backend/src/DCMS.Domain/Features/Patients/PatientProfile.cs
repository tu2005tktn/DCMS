namespace DCMS.Domain.Features.Patients;

public sealed class PatientProfileDetails
{
    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Note { get; set; }
    public DateOnly? ProfileDate { get; set; }
    public Guid[] IntakeHistoryIds { get; set; } = [];
    public string? IntakeHistoryOther { get; set; }
    public DateTimeOffset? IntakeRecordedAt { get; set; }
    public int? IntakeRecordedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchiveReason { get; set; }
}

// PATIENT_PROFILE (UC-38 patient master). Creating or archiving never creates a visit or grants account access (GBR-016/084).
public sealed class PatientProfile
{
    public int Id { get; set; }
    public string PatientCode { get; set; } = "";
    public string FullName { get; set; } = "";
    // Upper-case, accent-free copy of FullName used for search and duplicate hints.
    public string NormalizedName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Guid? SourceId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? OccupationId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
    public PatientProfileDetails Details { get; set; } = new();
    public string? GuardianName { get => Details.GuardianName; set => Details.GuardianName = value; }
    public string? GuardianRelationship { get => Details.GuardianRelationship; set => Details.GuardianRelationship = value; }
    public string? GuardianPhone { get => Details.GuardianPhone; set => Details.GuardianPhone = value; }
    public string? Note { get => Details.Note; set => Details.Note = value; }
    public DateOnly? ProfileDate { get => Details.ProfileDate; set => Details.ProfileDate = value; }
    public Guid[] IntakeHistoryIds { get => Details.IntakeHistoryIds; set => Details.IntakeHistoryIds = value ?? []; }
    public string? IntakeHistoryOther { get => Details.IntakeHistoryOther; set => Details.IntakeHistoryOther = value; }
    public DateTimeOffset? IntakeRecordedAt { get => Details.IntakeRecordedAt; set => Details.IntakeRecordedAt = value; }
    public int? IntakeRecordedBy { get => Details.IntakeRecordedBy; set => Details.IntakeRecordedBy = value; }
    public DateTimeOffset? ArchivedAt { get => Details.ArchivedAt; set => Details.ArchivedAt = value; }
    public string? ArchiveReason { get => Details.ArchiveReason; set => Details.ArchiveReason = value; }
}

public static class PatientGenders
{
    public const string Male = "MALE";
    public const string Female = "FEMALE";
    public const string Other = "OTHER";
    public static readonly string[] All = [Male, Female, Other];
}

// PATIENT_AUDIT: administrative change history (GBR-081). Never deleted.
public sealed class PatientAudit : DCMS.Domain.Common.AuditRecord
{
    public long Id { get; set; }
    public int PatientId { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public string ChangesJson { get; set; } = "[]";
    public DateTimeOffset OccurredAt { get; set; }
}

// DCMS-122: a create retried with the same actor + requestKey returns this patient instead of issuing a second code.
public sealed class PatientCreateReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public int PatientId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class PatientAuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Archive = "ARCHIVE";
    public const string Restore = "RESTORE";
}
