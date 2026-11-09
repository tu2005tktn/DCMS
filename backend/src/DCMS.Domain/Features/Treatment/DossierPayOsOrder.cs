namespace DCMS.Domain.Features.Treatment;

public sealed class DossierPayOsOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public string Purpose { get; set; } = "Treatment";
    public Guid? AdvanceId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public long OrderCode { get; set; }
    public long AmountVnd { get; set; }
    public DateOnly PaidOn { get; set; }
    public int CollectorEmployeeId { get; set; }
    public int CreatedBy { get; set; }
    public string? Note { get; set; }
    public string State { get; set; } = "Creating";
    public string? PaymentLinkId { get; set; }
    public string? QrCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? BankBin { get; set; }
    public string? BankReference { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public List<DossierPayOsOrderLine> Lines { get; set; } = [];
}

public sealed class DossierPayOsOrderLine
{
    public Guid OrderId { get; set; }
    public Guid ActivityId { get; set; }
    public long AmountVnd { get; set; }
}
