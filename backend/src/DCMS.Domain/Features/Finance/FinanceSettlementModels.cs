namespace DCMS.Domain.Features.Finance;

public sealed class FinancePaymentMethod
{
    public int BranchId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
}

public sealed class FinanceInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? DossierId { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int Version { get; set; } = 1;
    public List<FinanceInvoiceLine> Lines { get; set; } = [];
}

public sealed class FinanceInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid ChargeId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public long GrossVnd { get; set; }
    public long DiscountVnd { get; set; }
    public long NetVnd { get; set; }
}

public sealed class FinanceAdvance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? DossierId { get; set; }
    public Guid PaymentId { get; set; }
    public string Kind { get; set; } = "ServiceAdvance";
    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class FinanceAdvanceAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdvanceId { get; set; }
    public Guid InvoiceLineId { get; set; }
    public long AmountVnd { get; set; }
    public DateTimeOffset AllocatedAt { get; set; }
    public int AllocatedBy { get; set; }
}

public sealed class FinanceRefund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? ChargeId { get; set; }
    public Guid? AdvanceId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? DossierPaymentId { get; set; }
    public long AmountVnd { get; set; }
    public string Method { get; set; } = "Cash";
    public string Reason { get; set; } = "";
    public string State { get; set; } = "Requested";
    public int RequestedBy { get; set; }
    public int? ApprovedBy { get; set; }
    public int? CompletedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; } = 1;
}
