namespace DCMS.Domain.Features.Examinations;

public sealed class InvestigationOrder
{
    public string? BillingMode { get; set; }
    public int? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? UnitSnapshot { get; set; }
    public int Quantity { get; set; } = 1;
    public long? UnitPriceSnapshotVnd { get; set; }
    public int? PriceVersionSnapshot { get; set; }
    public string? QuoteEvidence { get; set; }
    public int? QuotedBy { get; set; }
    public DateTimeOffset? QuotedAt { get; set; }
    public Guid? ProviderId { get; set; }
    public string? ProviderNameSnapshot { get; set; }
    public long? ProviderUnitCostSnapshotVnd { get; set; }
    public int? PerformedBy { get; set; }
    public DateTimeOffset? PerformedAt { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public string Kind { get; set; } = "XRay";
    public string Name { get; set; } = "";
    public string? Region { get; set; }
    public string Reason { get; set; } = "";
    public bool RequiredForDiagnosis { get; set; } = true;
    public string State { get; set; } = "Ordered";
    public string? ResultText { get; set; }
    public string ResultDocumentIdsJson { get; set; } = "[]";
    public string? Interpretation { get; set; }
    public string? CancellationReason { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? ResultBy { get; set; }
    public DateTimeOffset? ResultAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public int? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid CreateRequestKey { get; set; }
    public int Version { get; set; } = 1;
}
