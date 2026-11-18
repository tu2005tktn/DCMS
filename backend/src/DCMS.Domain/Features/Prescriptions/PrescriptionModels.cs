namespace DCMS.Domain.Features.Prescriptions;

public sealed class PrescriptionAudit : DCMS.Domain.Common.AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ActorId { get; set; }
    public int BranchId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? TemplateId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}

// Patient-profile prescription (S23): saved directly under PRESCRIPTION with Items stored in JSONB.
public sealed class PatientPrescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int PrescriberEmployeeId { get; set; }
    public DateOnly PrescribedOn { get; set; }
    public string Diagnosis { get; set; } = "";
    public string? OtherDiagnosis { get; set; }
    public string? Advice { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public List<PatientPrescriptionItem> Items { get; set; } = [];
}

public sealed class PatientPrescriptionItem
{
    // Retained inside Items JSONB to support data-preserving migration rollback to legacy PATIENT_PRESCRIPTION_ITEM.
    public bool IsExternalPurchase { get; set; } = true;
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

public sealed class PatientPrescriptionReceipt : DCMS.Domain.Common.CommandReceiptRecord
{
    public int ActorId { get; set; }
    public Guid PrescriptionId { get; set; }
}
