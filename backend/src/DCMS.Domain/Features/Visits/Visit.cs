namespace DCMS.Domain.Features.Visits;

public static class VisitStatuses
{
    public const string Queued = "Queued";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = [Queued, InProgress, Completed, Cancelled];
}

public static class AttendancePurposes
{
    public const string InitialExamination = "InitialExamination";
    public const string TreatmentContinuation = "TreatmentContinuation";
    public const string PostoperativeOrWarrantyReview = "PostoperativeOrWarrantyReview";
    public const string PeriodicOrNewProblem = "PeriodicOrNewProblem";
    public static readonly string[] All = [InitialExamination, TreatmentContinuation, PostoperativeOrWarrantyReview, PeriodicOrNewProblem];
}

// One actual attendance per appointment. The waiting queue is a projection of this record.
public sealed class Visit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public Guid AppointmentId { get; set; }
    public int BranchId { get; set; }
    public int PatientId { get; set; }
    public int DentistId { get; set; }
    public string Status { get; set; } = VisitStatuses.Queued;
    public int Version { get; set; } = 1;
    public string Purpose { get; set; } = AttendancePurposes.InitialExamination;
    public Guid? DossierId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? StepId { get; set; }
    public Guid? ActivityId { get; set; }
    public string? ReviewReason { get; set; }
    public string? AdditionalConcern { get; set; }
    public string? Note { get; set; }
    public string IdentityNote { get; set; } = "";
    public DateTimeOffset ActualArrivalAt { get; set; }
    public DateTimeOffset EnteredAt { get; set; }
    public string? ArrivalReason { get; set; }
    public int EnteredBy { get; set; }
    public long QueueOrder { get; set; }
    public string? ClinicalPriority { get; set; }
    public int? PriorityDentistId { get; set; }
}

public sealed class ReceptionCommandReceipt
{
    public int ActorAccountId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public Guid VisitId { get; set; }
}

public sealed class VisitAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int ActorAccountId { get; set; }
    public int? DecidingDentistId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
}

// Integration read model published by clinical modules; reception has no write endpoint.
// ID is the original clinical ID, never a newly allocated copy of that source.
public sealed class ClinicalSourceReference
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "Dossier";
    public Guid? ParentId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public string Label { get; set; } = "";
    public string? Blocker { get; set; }
    public int Version { get; set; } = 1;
}
