namespace DCMS.Application.Features.Treatment;

// Rules: docs/specs/LAB-ORDER.md.
public sealed record LabOrderSummary(Guid ActivityId, string WorkName, DateOnly? DueOn, DateOnly? DeliveredOn);
public sealed record LabOrderDto(Guid Id, Guid ActivityId, string? Code, DateOnly OrderedOn, DateOnly? DueOn, DateOnly? DeliveredOn, int DentistId,
    Guid? LabProviderId, Guid? LabWorkTypeId, string WorkName, string? MarginOuter, string? MarginInner, string? MarginSide,
    IReadOnlyList<string> BridgeShapes, bool IsChild, IReadOnlyList<string> Teeth, IReadOnlyDictionary<string, string> Shade,
    string? Note, string? Material, int Quantity, long TotalFeeVnd, long PaidVnd, string? Warranty, int Version);
public sealed record LabOrderPatient(string Name, string Code, int? Age, string? Gender);
public sealed record LabOrderLab(Guid Id, string Name);
public sealed record LabOrderWorkType(Guid Id, string Name, Guid? ProviderId, long? ReferencePrice, string? Warranty);
public sealed record LabOrderDentist(int Id, string Name);
public sealed record LabOrderOptions(IReadOnlyList<LabOrderLab> Labs, IReadOnlyList<LabOrderWorkType> WorkTypes, IReadOnlyList<LabOrderDentist> Dentists, int? DefaultDentistId);
public sealed record LabOrderContext(LabOrderDto? Order, LabOrderPatient Patient, LabOrderOptions Options);
public sealed record SaveLabOrderRequest(string? Code, DateOnly OrderedOn, DateOnly? DueOn, DateOnly? DeliveredOn, int DentistId, Guid? LabProviderId,
    Guid? LabWorkTypeId, string? WorkName, string? MarginOuter, string? MarginInner, string? MarginSide, string[]? BridgeShapes, bool IsChild,
    string[]? Teeth, Dictionary<string, string>? Shade, string? Note, string? Material, int Quantity, long TotalFeeVnd, long PaidVnd,
    string? Warranty, int? ExpectedVersion);
