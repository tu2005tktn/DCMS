namespace DCMS.Domain.Features.Patients;

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
    public string? GuardianName { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Note { get; set; }
    // S58 đợt 3: administrative fields backed by REFERENCE_ENTRY catalogs (docs/specs/S58-PATIENT-CATALOG-FIELDS.md).
    public DateOnly? ProfileDate { get; set; }
    public Guid? SourceId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? OccupationId { get; set; }
    // Patient-reported intake history; it never replaces dentist-reviewed findings in S19.
    public Guid[] IntakeHistoryIds { get; set; } = [];
    public string? IntakeHistoryOther { get; set; }
    public DateTimeOffset? IntakeRecordedAt { get; set; }
    public int? IntakeRecordedBy { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchiveReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
}

public static class PatientGenders
{
    public const string Male = "MALE";
    public const string Female = "FEMALE";
    public const string Other = "OTHER";
    public static readonly string[] All = [Male, Female, Other];
}

// PATIENT_AUDIT: administrative change history (GBR-081). Never deleted.
public sealed class PatientAudit
{
    public long Id { get; set; }
    public int PatientId { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public string ChangesJson { get; set; } = "[]";
    public DateTimeOffset OccurredAt { get; set; }
}

public static class PatientAuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Archive = "ARCHIVE";
    public const string Restore = "RESTORE";
}
