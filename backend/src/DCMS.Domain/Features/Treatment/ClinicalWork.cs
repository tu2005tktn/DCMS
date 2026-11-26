namespace DCMS.Domain.Features.Treatment;

// One treatment actually performed, always recorded on a treatment dossier item; patient and branch come from
// the dossier. See docs/specs/CLINICAL-ACTIVITY-TABLE-SIMPLIFICATION.vi.md (phase 4, 20 columns).
public sealed class ClinicalActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Guid? ItemId { get; set; }
    public int? PlanVersion { get; set; }
    public bool IsPlanTransfer { get; set; }
    public int ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? Region { get; set; }
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public string? LabWorkSnapshot { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public int PerformerId { get; set; }
    public int? AssistantId { get; set; }
    public string Outcome { get; set; } = "";
    public decimal PriceSnapshotVnd { get; set; }
    public long DiscountSnapshotVnd { get; set; }
    public string FeeStatus { get; set; } = "Unavailable";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ClinicalCommandReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public Guid ResultId { get; set; }
}

public sealed class CareTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // Empty only for the callback of a missed booking that was never linked to a patient record.
    public int? PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string Type { get; set; } = "FollowUp";
    public string Content { get; set; } = "";
    public int? OwnerAccountId { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public string Status { get; set; } = "Open";
    public int Version { get; set; } = 1;
    // Contact attempts are stored on the task row; Version is the single concurrency token.
    public List<CareContactAttempt> ContactAttempts { get; set; } = [];
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
