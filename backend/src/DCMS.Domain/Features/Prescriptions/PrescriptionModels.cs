namespace DCMS.Domain.Features.Prescriptions;

public sealed class ExternalPrescriptionLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineCode { get; set; } = "";
    public string MedicineName { get; set; } = "";
    public string? Strength { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "";
    public string Instructions { get; set; } = "";
    public int Quantity { get; set; }
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PrescriptionCommandReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}
