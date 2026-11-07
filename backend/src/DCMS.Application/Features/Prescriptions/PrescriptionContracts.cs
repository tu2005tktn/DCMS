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
    Guid VisitId,
    int PatientId,
    int BranchId,
    int PrescribedByDoctorId,
    string? DoctorName,
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
    string Status,
    DateTimeOffset? FinalizedAt,
    int Version);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionLine(
    Guid RequestKey,
    int ExpectedVersion,
    Guid MedicineId,
    int Quantity,
    string? Route,
    string? MorningDose,
    string? NoonDose,
    string? AfternoonDose,
    string? EveningDose,
    string? MealTiming,
    int? Days,
    string? Instructions);

public sealed record TemplateLineInput(
    Guid MedicineId,
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
    string Unit,
    string Route,
    string DosageInstruction,
    string FullInstruction);

public sealed record PrescriptionPrintDto(
    Guid VisitId,
    string ClinicName,
    string BranchName,
    string BranchAddress,
    string BranchPhone,
    string? LogoUrl,
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
    string VerificationQrData);

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
    string BranchName,
    string DoctorName,
    string Diagnosis,
    DateTimeOffset PrescribedAt,
    string Status,
    string GeneralAdvice,
    PatientPrescriptionMedicineDto[] Medicines);

public interface IPrescriptionService
{
    Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int branchId, string? search, CancellationToken ct);
    Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct);
    Task<ClinicalAllergyWarning[]> CheckSafetyAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine command, CancellationToken ct);
    Task DeleteLineAsync(int actor, Guid visitId, Guid lineId, Guid requestKey, int expectedVersion, CancellationToken ct);
    Task<PrescriptionLineDto[]> ApplyTemplateAsync(int actor, Guid visitId, ApplyPrescriptionTemplateCommand command, CancellationToken ct);
    Task<PrescriptionLineDto[]> FinalizeAsync(int actor, Guid visitId, FinalizePrescriptionCommand command, CancellationToken ct);
    Task<PrescriptionPrintDto> PrintDataAsync(int actor, Guid visitId, CancellationToken ct);

    Task<PrescriptionTemplateDto[]> TemplatesAsync(int actor, string? search, string? diagnosis, int page, int pageSize, CancellationToken ct);
    Task<PrescriptionTemplateDto> TemplateAsync(int actor, Guid id, CancellationToken ct);
    Task<PrescriptionTemplateDto> SaveTemplateAsync(int actor, Guid? id, SavePrescriptionTemplate command, CancellationToken ct);
    Task<PrescriptionTemplateDto> SetTemplateStateAsync(int actor, Guid id, Guid requestKey, int expectedVersion, bool active, string reason, CancellationToken ct);

    Task<PatientPrescriptionDto[]> PatientPrescriptionsAsync(int actor, CancellationToken ct);
    Task<PatientPrescriptionDto> PatientPrescriptionAsync(int actor, Guid visitId, CancellationToken ct);
}
