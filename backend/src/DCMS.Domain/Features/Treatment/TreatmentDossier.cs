namespace DCMS.Domain.Features.Treatment;

// UC-13/39. Clinical course, proposed work and money remain independent sources.
public sealed class TreatmentDossier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SourceExaminationVisitId { get; set; }
    public string? SourceExaminationJson { get; set; }
    public string Code { get; set; } = "";
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int DentistId { get; set; }
    public DateOnly StartedOn { get; set; }
    public string Diagnosis { get; set; } = "";
    public string? Note { get; set; }
    public string State { get; set; } = "Open";
    public bool DisplayCompleted { get; set; }
    public string? ClosureOutcome { get; set; }
    public string? ClosureReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public int? ClosedBy { get; set; }
    public int Version { get; set; } = 1;
    public Guid PlanId { get; set; } = Guid.NewGuid();
    public int PlanVersion { get; set; }
    public string PlanState { get; set; } = "None";
    public string? Goals { get; set; }
    public Guid? SourceExaminationId { get; set; }
    public int? SourceExaminationVersion { get; set; }
    public Guid? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string PaymentTermsJson { get; set; } = "[]";
    public string? DepositExceptionReason { get; set; }
    public int? DepositExceptionBy { get; set; }
    public DateTimeOffset? DepositExceptionAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TreatmentItem> Items { get; set; } = [];
}

public sealed class TreatmentItem
{
    public string? EntrySource { get; set; }
    public int? EntryRecordedBy { get; set; }
    public DateTimeOffset? EntryRecordedAt { get; set; }
    public bool DisplayCompleted { get; set; }
    public bool SheetDeleted { get; set; }
    public bool HiddenFromSheet { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public int? SourceDiagnosisId { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public string? Material { get; set; }
    public string? Note { get; set; }
    public int? ResponsibleDentistId { get; set; }
    public int? AssistantId { get; set; }
    public string? Region { get; set; }
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public int PlannedOrder { get; set; }
    public int? EstimatedMinutes { get; set; }
    public int? EstimatedMinMinutes { get; set; }
    public int? EstimatedMaxMinutes { get; set; }
    public int? ReservedMinutes { get; set; }
    public int? ShortReservationAcknowledgedBy { get; set; }
    public DateTimeOffset? ShortReservationAcknowledgedAt { get; set; }
    public int? EstimatedSessions { get; set; }
    public DateOnly? ReturnEarliestOn { get; set; }
    public DateOnly? ReturnTargetOn { get; set; }
    public DateOnly? ReturnLatestOn { get; set; }
    public DateOnly? EstimatedStart { get; set; }
    public DateOnly? EstimatedEnd { get; set; }
    public long AgreedUnitPriceVnd { get; set; }
    public long? ReferenceUnitPriceVnd { get; set; }
    public int? ReferencePriceVersion { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? DiscountReason { get; set; }
    public Guid? DiscountQualifyingItemId { get; set; }
    public string? DiscountStatus { get; set; }
    public string? DiscountBasisHash { get; set; }
    public int? DiscountDecidedBy { get; set; }
    public DateTimeOffset? DiscountDecidedAt { get; set; }
    public string Decision { get; set; } = "Proposed";
    public string Progress { get; set; } = "NotStarted";
    public string? Counseling { get; set; }
    public string? DecisionMaker { get; set; }
    public string? RepresentativeBasis { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int? DecidedBy { get; set; }
    public int? DecisionPlanVersion { get; set; }
    public Guid? ConsentDocumentId { get; set; }
    public int? ConsentPlanVersion { get; set; }
    public string? WarrantyPolicy { get; set; }
    public string? WarrantyPolicyVersion { get; set; }
    public string? WarrantyScope { get; set; }
    public DateTimeOffset? WarrantyConfirmedAt { get; set; }
    public DateOnly? WarrantyEndsOn { get; set; }
    public int? WarrantyConfirmedBy { get; set; }
    public string? WarrantyComponent { get; set; }
    public string? WarrantyExclusions { get; set; }
    public DateOnly? WarrantyActivatedOn { get; set; }
    public string? WarrantyExplanation { get; set; }
    public int? ExpectedServiceLifeMonths { get; set; }
}

// One append-only source for plan snapshots, clinical state changes and warranty confirmation.
public sealed class TreatmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public int ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int DossierVersion { get; set; }
}

public sealed class TreatmentReceipt
{
    public Guid RequestKey { get; set; }
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid DossierId { get; set; }
    public string ResultJson { get; set; } = "{}";
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

public sealed class TreatmentRevisionDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Guid ItemId { get; set; }
    public int ActorId { get; set; }
    public int BasePlanVersion { get; set; }
    public int Version { get; set; } = 1;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
