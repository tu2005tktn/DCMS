namespace DCMS.Domain.Features.Treatment;

public sealed class ClinicalActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? DossierId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? StepId { get; set; }
    public int? PlanVersion { get; set; }
    public int ServiceId { get; set; }
    public string? Region { get; set; }
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public int? ElapsedMinutes { get; set; }
    public int PerformerId { get; set; }
    public string Outcome { get; set; } = "";
    public string? NextSteps { get; set; }
    public string ChargingBasis { get; set; } = "Included";
    public string? ChargingBasisEvidence { get; set; }
    public decimal PriceSnapshotVnd { get; set; }
    public Guid? ReviewId { get; set; }
    public string State { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
}

public sealed class ClinicalReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? SourceActivityId { get; set; }
    public Guid? SourceItemId { get; set; }
    public string Complaint { get; set; } = "";
    public string Findings { get; set; } = "";
    public string Coverage { get; set; } = "PendingAssessment";
    public string Outcome { get; set; } = "AdviceOnly";
    public string? CoveredComponent { get; set; }
    public string? Exclusions { get; set; }
    public string? Policy { get; set; }
    public string? PolicyVersion { get; set; }
    public string? Basis { get; set; }
    public string? NextAction { get; set; }
    public int DentistAccountId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class ClinicalCareNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? SourceActivityId { get; set; }
    public Guid? ReviewId { get; set; }
    public string Content { get; set; } = "";
    public string? Response { get; set; }
    public int ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class ClinicalCommandReceipt
{
    public Guid RequestKey { get; set; }
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}

public sealed class CareTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? InstructionId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? ReviewId { get; set; }
    public string? EpisodeKey { get; set; }
    public string Type { get; set; } = "FollowUp";
    public string Content { get; set; } = "";
    public int? OwnerAccountId { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public string Status { get; set; } = "Open";
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CareContactAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string Channel { get; set; } = "";
    public string Result { get; set; } = "";
    public string? NextAction { get; set; }
    public Guid? RebookedAppointmentId { get; set; }
    public int ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class CareEscalation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public int TargetClinicianAccountId { get; set; }
    public string Reason { get; set; } = "";
    public string? Resolution { get; set; }
    public string Status { get; set; } = "Pending";
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
