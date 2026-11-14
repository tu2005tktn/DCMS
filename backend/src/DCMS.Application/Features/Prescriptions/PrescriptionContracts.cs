using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Prescriptions;

public sealed record PrescriptionMedicineOption(
    Guid Id,
    string Code,
    string Name,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    string BaseUnit,
    string Route,
    string? Packaging,
    string? Contraindications,
    string? AllergyWarnings,
    string? DefaultInstructions,
    long? ReferencePrice);

public sealed record PrescriptionLineDto(
    Guid Id,
    Guid PrescriptionId,
    Guid VisitId,
    int PatientId,
    int BranchId,
    int PrescribedByDoctorId,
    string? DoctorName,
    Guid? MedicineId,
    string MedicineCode,
    string MedicineName,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    string Unit,
    string Route,
    string MorningDose,
    string NoonDose,
    string AfternoonDose,
    string EveningDose,
    string MealTiming,
    int Days,
    int Quantity,
    string Instructions,
    string Status,
    DateTimeOffset? FinalizedAt,
    int Version);

public sealed record PrescriptionDto(
    Guid Id,
    Guid VisitId,
    int PatientId,
    int BranchId,
    int PrescribedByDoctorId,
    string? DoctorName,
    int Revision,
    string Status,
    string GeneralAdvice,
    string? Diagnosis,
    string? CorrectionReason,
    Guid? SupersedesId,
    DateTimeOffset? FinalizedAt,
    string? FinalizedByName,
    string? VerificationCode,
    int Version,
    PrescriptionLineDto[] Lines);

public sealed record PrescriptionSummaryDto(
    Guid Id,
    int Revision,
    string Status,
    string? DoctorName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinalizedAt,
    string? CorrectionReason,
    int LineCount);

public sealed record VisitPrescriptionDto(PrescriptionDto? Current, PrescriptionSummaryDto[] History);

public sealed record ApplyTemplateResult(PrescriptionDto Prescription, int SkippedLines);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionLine(
    Guid RequestKey,
    int ExpectedVersion,
    Guid? MedicineId,
    int Quantity,
    string? Route,
    string? MorningDose,
    string? NoonDose,
    string? AfternoonDose,
    string? EveningDose,
    string? MealTiming,
    int? Days,
    string? Instructions,
    string? CustomMedicineName = null,
    string? CustomUnit = null,
    string? Unit = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionAdvice(Guid RequestKey, int ExpectedVersion, string? GeneralAdvice);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePrescriptionCorrection(Guid RequestKey, int ExpectedVersion, string Reason);

public sealed record TemplateLineInput(
    Guid MedicineId,
    string? Route,
    string? MorningDose,
    string? NoonDose,
    string? AfternoonDose,
    string? EveningDose,
    string? MealTiming,
    int Days,
    int Quantity,
    string? Instructions,
    int SortOrder);

public sealed record PrescriptionTemplateLineDto(
    Guid Id,
    Guid TemplateId,
    Guid MedicineId,
    string MedicineCode,
    string MedicineName,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    string Unit,
    string Route,
    string MorningDose,
    string NoonDose,
    string AfternoonDose,
    string EveningDose,
    string MealTiming,
    int Days,
    int Quantity,
    string Instructions,
    int SortOrder);

public sealed record PrescriptionTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? DiagnosisKind,
    string? Description,
    bool IsActive,
    int Version,
    int CreatedBy,
    DateTimeOffset CreatedAt,
    int UpdatedBy,
    DateTimeOffset UpdatedAt,
    PrescriptionTemplateLineDto[] Lines);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionTemplate(
    Guid RequestKey,
    int ExpectedVersion,
    string Code,
    string Name,
    string? DiagnosisKind,
    string? Description,
    TemplateLineInput[] Lines);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TemplateStateCommand(Guid RequestKey, int ExpectedVersion, bool Active, string? Reason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplyPrescriptionTemplateCommand(
    Guid RequestKey,
    int ExpectedVersion,
    Guid TemplateId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FinalizePrescriptionCommand(
    Guid RequestKey,
    int ExpectedVersion,
    string? ClinicalNote);

public sealed record ClinicalAllergyWarning(
    string MedicineName,
    string? ActiveIngredient,
    string WarningType, // Allergy, DuplicateIngredient, Contraindication
    string Message);

public sealed record PrescriptionPrintItem(
    int Index,
    string MedicineName,
    string? ActiveIngredient,
    string? Strength,
    string? DosageForm,
    int Quantity,
    string QuantityInWords,
    string Unit,
    string Route,
    string DosageInstruction,
    string Instructions);

public sealed record PrescriptionPrintDto(
    Guid PrescriptionId,
    Guid VisitId,
    int Revision,
    bool IsDraft,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    string? LogoDataUrl,
    string DoctorName,
    string? DoctorLicenseNumber,
    string PatientCode,
    string PatientName,
    int? BirthYear,
    string? Gender,
    string? Address,
    string Diagnosis,
    DateTimeOffset PrescribedAt,
    PrescriptionPrintItem[] Items,
    string GeneralAdvice,
    string? VerificationCode,
    string? VerificationQrData);

public sealed record PatientPrescriptionMedicineDto(
    string MedicineName,
    string? Strength,
    string? DosageForm,
    int Quantity,
    string Unit,
    string Route,
    string MorningDose,
    string NoonDose,
    string AfternoonDose,
    string EveningDose,
    string MealTiming,
    int Days,
    string Instructions);

public sealed record PatientPrescriptionDto(
    Guid VisitId,
    Guid PrescriptionId,
    int Revision,
    bool IsCorrection,
    string BranchName,
    string DoctorName,
    string Diagnosis,
    DateTimeOffset PrescribedAt,
    string GeneralAdvice,
    string ExternalPurchaseNotice,
    PatientPrescriptionMedicineDto[] Medicines);

public sealed record PatientPrescriptionDetailDto(PatientPrescriptionDto Prescription, PrescriptionPrintDto Print);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePatientPrescription(Guid RequestKey, int ExpectedVersion, int BranchId, DateOnly PrescribedOn,
    string Diagnosis, string? OtherDiagnosis, string? Advice, SavePatientPrescriptionItem[] Items, int? PrescriberEmployeeId = null);
public sealed record SavePatientPrescriptionItem(Guid MedicineId, int Quantity, string? Instructions, string? Unit = null);
public sealed record PrescriptionBranchOption(int Id, string Name, string? Address, string? Phone, string? Email);
public sealed record PrescriptionDoctorOption(int EmployeeId, string Name, int BranchId);
public sealed record PatientPrescriptionContext(int[] BranchIds, string PrescriberName, int PrescriberEmployeeId,
    PrescriptionBranchOption[] Branches, PrescriptionDoctorOption[] Doctors);

public sealed record PatientProfilePrescriptionItemDto(Guid Id, Guid MedicineId, string MedicineName, string MedicineCode,
    string? Strength, string? DosageForm, string Unit, int Quantity, string Instructions);
public sealed record PatientProfilePrescriptionDto(Guid Id, int PatientId, int BranchId, string BranchName,
    DateOnly PrescribedOn, string Diagnosis, string? OtherDiagnosis, string? Advice, string PrescriberName, int Version,
    PatientProfilePrescriptionItemDto[] Items, int PrescriberEmployeeId, string? BranchAddress, string? BranchPhone,
    string? BranchEmail, string PatientCode, string PatientName, DateOnly? PatientBirthDate, string? PatientGender);

public interface IPrescriptionService
{
    Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int? branchId, string? search, CancellationToken ct);
    Task<VisitPrescriptionDto> PrescriptionAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct);
    Task<ClinicalAllergyWarning[]> CheckSafetyAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine command, CancellationToken ct);
    Task DeleteLineAsync(int actor, Guid visitId, Guid lineId, Guid requestKey, int expectedVersion, CancellationToken ct);
    Task<PrescriptionDto> SaveAdviceAsync(int actor, Guid visitId, SavePrescriptionAdvice command, CancellationToken ct);
    Task<ApplyTemplateResult> ApplyTemplateAsync(int actor, Guid visitId, ApplyPrescriptionTemplateCommand command, CancellationToken ct);
    Task<PrescriptionDto> FinalizeAsync(int actor, Guid visitId, FinalizePrescriptionCommand command, CancellationToken ct);
    Task<PrescriptionDto> CreateCorrectionAsync(int actor, Guid visitId, CreatePrescriptionCorrection command, CancellationToken ct);
    Task<PrescriptionPrintDto> PrintDataAsync(int actor, Guid visitId, CancellationToken ct);

    Task<PrescriptionTemplateDto[]> TemplatesAsync(int actor, string? search, string? diagnosis, string? status, int page, int pageSize, CancellationToken ct);
    Task<PrescriptionTemplateDto> TemplateAsync(int actor, Guid id, CancellationToken ct);
    Task<PrescriptionTemplateDto> SaveTemplateAsync(int actor, Guid? id, SavePrescriptionTemplate command, CancellationToken ct);
    Task<PrescriptionTemplateDto> SetTemplateStateAsync(int actor, Guid id, TemplateStateCommand command, CancellationToken ct);

    Task<PatientPrescriptionDto[]> PatientPrescriptionsAsync(int actor, CancellationToken ct);
    Task<PatientPrescriptionDetailDto> PatientPrescriptionAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientProfilePrescriptionDto[]> PatientProfilePrescriptionsAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> PatientProfilePrescriptionAsync(int actor, int patientId, Guid id, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> SavePatientProfilePrescriptionAsync(int actor, int patientId, Guid? id, SavePatientPrescription command, CancellationToken ct);
    Task<PatientProfilePrescriptionDto[]> OwnPatientPrescriptionsAsync(int actor, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> OwnPatientPrescriptionAsync(int actor, Guid id, CancellationToken ct);
}
