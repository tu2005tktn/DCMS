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

// The queue is a projection of Visit, never a second attendance record.
public sealed class Visit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int DentistId { get; set; }
    public string Status { get; set; } = VisitStatuses.Queued;
    public int Version { get; set; } = 1;
    public DateTimeOffset ArrivalAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int RecordedBy { get; set; }
    public string? ArrivalReason { get; set; }
    public string Purpose { get; set; } = AttendancePurposes.InitialExamination;
    public string? Note { get; set; }
    public string? SourceJson { get; set; }
    public bool RequiresClinicalSourceReview { get; set; }
}

public sealed class ReceptionReceipt
{
    public int ActorAccountId { get; set; }
    public Guid RequestKey { get; set; }
    public Guid VisitId { get; set; }
    public string Fingerprint { get; set; } = "";
}

public sealed class VisitAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int ActorAccountId { get; set; }
    public string Action { get; set; } = "CheckIn";
    public DateTimeOffset OccurredAt { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
}
