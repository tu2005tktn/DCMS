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
    string? Packaging,
    string? DefaultInstructions,
    long? ReferencePrice);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePatientPrescription(
    Guid RequestKey,
    int ExpectedVersion,
    int BranchId,
    DateOnly PrescribedOn,
    string Diagnosis,
    string? OtherDiagnosis,
    string? Advice,
    SavePatientPrescriptionItem[] Items,
    int? PrescriberEmployeeId = null);

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
    Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientProfilePrescriptionDto[]> PatientProfilePrescriptionsAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> PatientProfilePrescriptionAsync(int actor, int patientId, Guid id, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> SavePatientProfilePrescriptionAsync(int actor, int patientId, Guid? id, SavePatientPrescription command, CancellationToken ct);
    Task<PatientProfilePrescriptionDto[]> OwnPatientPrescriptionsAsync(int actor, CancellationToken ct);
    Task<PatientProfilePrescriptionDto> OwnPatientPrescriptionAsync(int actor, Guid id, CancellationToken ct);
}
