namespace DCMS.Domain.Features.Treatment;

public sealed class DossierPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Method { get; set; } = "Cash";
    public int CollectorEmployeeId { get; set; }
    public string? Note { get; set; }
    public long TotalVnd { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<DossierPaymentLine> Lines { get; set; } = [];
}

public sealed class DossierPaymentLine
{
    public Guid PaymentId { get; set; }
    public Guid ActivityId { get; set; }
    public long AmountVnd { get; set; }
}
