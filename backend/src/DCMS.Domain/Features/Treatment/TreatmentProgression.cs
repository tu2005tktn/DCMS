namespace DCMS.Domain.Features.Treatment;

public sealed class TreatmentTemplate
{
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public int BranchId { get; set; }
    public string Name { get; set; } = "";
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<TreatmentTemplateItem> Items { get; set; } = [];
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
