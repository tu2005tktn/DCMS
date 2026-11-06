using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Prescriptions;

public sealed record PrescriptionMedicineOption(Guid Id, string Code, string Name, string? ActiveIngredient,
    string? Strength, string? DosageForm, string BaseUnit, string? Packaging, string? DefaultInstructions);
public sealed record PrescriptionLineDto(Guid Id, Guid VisitId, Guid MedicineId, string MedicineCode, string MedicineName,
    string? Strength, string? DosageForm, string Unit, string Instructions, int Quantity, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePrescriptionLine(Guid RequestKey, int ExpectedVersion, Guid MedicineId, int Quantity, string? Instructions);

public interface IPrescriptionService
{
    Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int branchId, string? search, CancellationToken ct);
    Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine command, CancellationToken ct);
}
