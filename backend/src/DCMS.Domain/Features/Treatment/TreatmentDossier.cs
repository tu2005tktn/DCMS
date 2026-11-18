namespace DCMS.Domain.Features.Treatment;

// UC-13/39. Clinical course, proposed work and money remain independent sources.
public sealed class TreatmentDossier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SourceExaminationVisitId { get; set; }
    public string Code { get; set; } = "";
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int DentistId { get; set; }
    public DateOnly StartedOn { get; set; }
    public string Diagnosis { get; set; } = "";
    public string? Note { get; set; }
    public string State { get; set; } = "Open";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TreatmentItem> Items { get; set; } = [];
    public List<TreatmentSheetDraft> SheetDrafts { get; set; } = [];

    public bool HasCommittedPlan() => Items.Count > 0;
}

public sealed class TreatmentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public int PlannedOrder { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public string? Region { get; set; }
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateOnly? EstimatedStart { get; set; }
    public int? EstimatedMinutes { get; set; }
    public string? Note { get; set; }
    public int? ResponsibleDentistId { get; set; }
    public int? AssistantId { get; set; }
    public long AgreedUnitPriceVnd { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? DiscountReason { get; set; }
    public bool DisplayCompleted { get; set; }
}

// One append-only source for plan snapshots, clinical state changes and warranty confirmation.
public sealed class TreatmentEvent : DCMS.Domain.Common.AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public int ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int DossierVersion { get; set; }
}

public sealed class TreatmentReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public Guid DossierId { get; set; }
}

// Editable sheet rows are saved independently of agreed plan items and posted clinical activities.
public sealed class TreatmentSheetDraft
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public Guid? VisitId { get; set; }
    public int ActorId { get; set; }
    public string Kind { get; set; } = "";
    public int Version { get; set; } = 1;
    public string PayloadJson { get; set; } = "{}";
    public Guid LastRequestKey { get; set; }
    public string LastFingerprint { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
