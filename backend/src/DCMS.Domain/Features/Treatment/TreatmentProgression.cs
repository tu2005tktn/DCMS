namespace DCMS.Domain.Features.Treatment;

public sealed class TreatmentTemplate
{
    public string DefinitionKind { get; set; } = "LegacySteps";
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public int BranchId { get; set; }
    public string Name { get; set; } = "";
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<TreatmentTemplateVersion> Versions { get; set; } = [];
    public List<TreatmentTemplateItem> Items { get; set; } = [];
    public Guid StorageStamp { get; set; } = Guid.NewGuid();
}

public sealed class TreatmentTemplateItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public int Position { get; set; }
    public DateOnly? Date { get; set; }
    public string TeethJson { get; set; } = "[]";
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = "";
    public int? EstimatedMinutes { get; set; }
    public string? Note { get; set; }
    public int? DentistId { get; set; }
    public string? DentistName { get; set; }
    public int? AssistantId { get; set; }
    public string? AssistantName { get; set; }
    public long CostVnd { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class TreatmentTemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public int Number { get; set; }
    public string State { get; set; } = "Draft";
    public string StepsJson { get; set; } = "[]";
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}

public sealed class TreatmentStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Guid PlanId { get; set; }
    public int PlanVersion { get; set; }
    public Guid ItemId { get; set; }
    public string Name { get; set; } = "";
    public string? Region { get; set; }
    public int EstimatedMinutes { get; set; }
    public int? EstimatedMinMinutes { get; set; }
    public int? EstimatedMaxMinutes { get; set; }
    public int ReservedMinutes { get; set; }
    public int? ShortReservationAcknowledgedBy { get; set; }
    public DateTimeOffset? ShortReservationAcknowledgedAt { get; set; }
    public string PredecessorsJson { get; set; } = "[]";
    public string? Capability { get; set; }
    public string? LabRequirement { get; set; }
    public int? EarliestOffsetDays { get; set; }
    public int? TargetOffsetDays { get; set; }
    public int? LatestOffsetDays { get; set; }
    public string ChargingCoverage { get; set; } = "IncludedInItem";
    public string Outcome { get; set; } = "NotStarted";
    public string? HoldReason { get; set; }
    public string? ReviewCondition { get; set; }
    public DateTimeOffset? HoldEffectiveAt { get; set; }
    public Guid? CompletionEventId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class TreatmentProgressionReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public string Command { get; set; } = "";
}

public sealed class TreatmentProgressionEvent : DCMS.Domain.Common.AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string? StableKey { get; set; }
    public int ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
