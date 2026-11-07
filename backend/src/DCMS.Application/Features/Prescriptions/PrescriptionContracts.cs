using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Prescriptions;

public sealed record PrescriptionMedicineOption(Guid Id, string Code, string Name, string? ActiveIngredient,
    string? Strength, string? DosageForm, string BaseUnit, string? Packaging, string? DefaultInstructions, bool InternalStockTracked);
public sealed record PrescriptionLineDto(Guid Id, Guid VisitId, Guid MedicineId, string MedicineCode, string MedicineName,
    string? Strength, string? DosageForm, string Unit, string Instructions, int Quantity, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionLine(Guid RequestKey, int ExpectedVersion, Guid MedicineId, int Quantity, string? Instructions);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePatientPrescription(Guid RequestKey, int ExpectedVersion, int BranchId, DateOnly PrescribedOn,
    string Diagnosis, string? OtherDiagnosis, string? Advice, bool RequiresExternalPurchase, SavePatientPrescriptionItem[] Items);
public sealed record SavePatientPrescriptionItem(Guid MedicineId, int Quantity, string? Instructions, bool IsExternalPurchase);
public sealed record PatientPrescriptionItemDto(Guid Id, Guid MedicineId, string MedicineCode, string MedicineName,
    string? Strength, string? DosageForm, string Unit, int Quantity, string Instructions, bool IsExternalPurchase);
public sealed record PatientPrescriptionDto(Guid Id, int PatientId, int BranchId, DateOnly PrescribedOn, string Diagnosis,
    string? OtherDiagnosis, string? Advice, bool RequiresExternalPurchase, int PrescriberEmployeeId, string PrescriberName, int Version,
    PatientPrescriptionItemDto[] Items);
public sealed record PatientPrescriptionContext(int[] BranchIds, string PrescriberName);

public interface IPrescriptionService
{
    Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int branchId, string? search, string? source, CancellationToken ct);
    Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine command, CancellationToken ct);
    Task<PatientPrescriptionDto[]> PatientListAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct);
    Task<PatientPrescriptionDto> PatientDetailAsync(int actor, int patientId, Guid id, CancellationToken ct);
    Task<PatientPrescriptionDto> SavePatientAsync(int actor, int patientId, Guid? id, SavePatientPrescription command, CancellationToken ct);
}
