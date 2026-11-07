namespace DCMS.Domain.Features.Prescriptions;

public static class PrescriptionStatuses
{
    public const string Draft = "Draft";
    public const string Finalized = "Finalized";
    public const string Superseded = "Superseded";
}

public static class PrescriptionRules
{
    public static readonly string[] MealTimings = ["BeforeMeal", "AfterMeal", "WithMeal"];
    public const int MaxQuantity = 10_000;
    public const int MaxDays = 365;
    public const int MaxDoseLength = 20;
    public const int MaxTemplateLines = 30;
}

// One signed (or draft) revision of the external prescription of a visit; finalized revisions are immutable.
public sealed class ExternalPrescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int PrescribedByDoctorId { get; set; }
    public int Revision { get; set; } = 1;
    public string Status { get; set; } = PrescriptionStatuses.Draft;
    public string GeneralAdvice { get; set; } = "";
    public string? Diagnosis { get; set; }
    public string? CorrectionReason { get; set; }
    public Guid? SupersedesId { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public int? FinalizedBy { get; set; }
    public string? VerificationCode { get; set; }
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExternalPrescriptionLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid VisitId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public int PrescribedByDoctorId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineCode { get; set; } = "";
    public string MedicineName { get; set; } = "";
    public string? ActiveIngredient { get; set; }
    public string? Strength { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "";
    public string Route { get; set; } = "Uống";
    public string MorningDose { get; set; } = "0";
    public string NoonDose { get; set; } = "0";
    public string AfternoonDose { get; set; } = "0";
    public string EveningDose { get; set; } = "0";
    public string MealTiming { get; set; } = "AfterMeal"; // BeforeMeal, AfterMeal, WithMeal
    public int Days { get; set; } = 1;
    public string Instructions { get; set; } = "";
    public int Quantity { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Finalized
    public DateTimeOffset? FinalizedAt { get; set; }
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PrescriptionTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? DiagnosisKind { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<PrescriptionTemplateLine> Lines { get; set; } = [];
}

public sealed class PrescriptionTemplateLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid MedicineId { get; set; }
    public string MedicineCode { get; set; } = "";
    public string MedicineName { get; set; } = "";
    public string? ActiveIngredient { get; set; }
    public string? Strength { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "";
    public string Route { get; set; } = "Uống";
    public string MorningDose { get; set; } = "0";
    public string NoonDose { get; set; } = "0";
    public string AfternoonDose { get; set; } = "0";
    public string EveningDose { get; set; } = "0";
    public string MealTiming { get; set; } = "AfterMeal";
    public int Days { get; set; } = 1;
    public int Quantity { get; set; } = 1;
    public string Instructions { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class PrescriptionAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ActorId { get; set; }
    public int BranchId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? TemplateId { get; set; }
    public string Action { get; set; } = ""; // LineCreate, LineUpdate, LineDelete, TemplateApply, AdviceUpdate, Finalize, CorrectionCreate, TemplateCreate, TemplateUpdate, TemplateState
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}

public sealed class PrescriptionCommandReceipt
{
    public int ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}
