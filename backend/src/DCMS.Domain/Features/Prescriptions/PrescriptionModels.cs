namespace DCMS.Domain.Features.Prescriptions;

public sealed class PatientPrescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public DateOnly PrescribedOn { get; set; }
    public string Diagnosis { get; set; } = "";
    public string? OtherDiagnosis { get; set; }
    public string? Advice { get; set; }
    public int PrescriberEmployeeId { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<PatientPrescriptionItem> Items { get; set; } = [];
}

public sealed class PatientPrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineCode { get; set; } = "";
    public string MedicineName { get; set; } = "";
    public string? Strength { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public string Instructions { get; set; } = "";
    public int Position { get; set; }
}

public sealed class PatientPrescriptionReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public Guid PrescriptionId { get; set; }
}

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
