namespace DCMS.Domain.Features.Finance;

public sealed class Provider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedCode { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Kind { get; set; } = "ClinicalFacility";
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
}
public sealed class FinancialAccount
{
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class FinanceCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid VisitId { get; set; }
    public string SourceKind { get; set; } = "";
    public Guid SourceId { get; set; }
    public Guid? InvestigationId { get; set; }
    public Guid? ActivityId { get; set; }
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public long UnitPriceVnd { get; set; }
    public long AmountVnd { get; set; }
    public int PriceVersion { get; set; }
    public int PostedBy { get; set; }
    public DateTimeOffset PostedAt { get; set; }
}
public sealed class ProviderPayable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChargeId { get; set; }
    public Guid ProviderId { get; set; }
    public int BranchId { get; set; }
    public string ProviderName { get; set; } = "";
    public long AmountVnd { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class PaymentReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public string PatientName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string Method { get; set; } = "Cash";
    public string State { get; set; } = "Pending";
    public long AmountVnd { get; set; }
    public string Payer { get; set; } = "";
    public string? Note { get; set; }
    public string? DeclaredReference { get; set; }
    public string? BankReference { get; set; }
    public string? NormalizedBankReference { get; set; }
    public string? ConfirmationNote { get; set; }
    public string? CancellationReason { get; set; }
    public string? Number { get; set; }
    public int CreatedBy { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public int Version { get; set; } = 1;
    public List<PaymentAllocation> Allocations { get; set; } = [];
}
public sealed class PaymentAllocation
{
    public Guid PaymentId { get; set; }
    public Guid ChargeId { get; set; }
    public long AmountVnd { get; set; }
}
public sealed class BranchReceiptSequence
{
    public int BranchId { get; set; }
    public long NextNumber { get; set; } = 1;
    public int Version { get; set; } = 1;
}
public sealed class ReceiptPrintEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public int ActorId { get; set; }
    public int SettingsVersion { get; set; }
    public Guid RequestKey { get; set; }
    public DateTimeOffset PrintedAt { get; set; }
}
public sealed class FinanceAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ActorId { get; set; }
    public int BranchId { get; set; }
    public Guid? VisitId { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}
public sealed class FinanceCommandReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}
