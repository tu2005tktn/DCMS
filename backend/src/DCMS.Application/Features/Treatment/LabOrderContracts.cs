namespace DCMS.Application.Features.Treatment;

// Rules: docs/specs/LAB-ORDER.md.
public sealed record LabOrderSummary(Guid ActivityId, string WorkName, DateOnly? DueOn, DateOnly? DeliveredOn);
public sealed record LabOrderDto(Guid Id, Guid ActivityId, string? Code, DateOnly OrderedOn, DateOnly? DueOn, DateOnly? DeliveredOn, int DentistId,
    Guid? LabProviderId, Guid? LabWorkTypeId, string WorkName, string? MarginOuter, string? MarginInner, string? MarginSide,
    IReadOnlyList<string> BridgeShapes, bool IsChild, IReadOnlyList<string> Teeth, IReadOnlyDictionary<string, string> Shade,
    string? Note, string? Material, int Quantity, long TotalFeeVnd, long PaidVnd, string? Warranty, int Version, string Status = "NotSent", DateOnly? SentOn = null, DateOnly? ReceivedOn = null);
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

// Lab work management. Rules: docs/specs/LAB-ORDER-MANAGEMENT.md.
public sealed record LabWorkItem(Guid Id, string? Code, Guid DossierId, string DossierCode, Guid ActivityId, int PatientId, string PatientCode,
    string PatientName, string WorkName, Guid? LabProviderId, string? LabName, int DentistId, string DentistName, DateOnly OrderedOn, DateOnly? DueOn,
    DateOnly? SentOn, DateOnly? ReceivedOn, DateOnly? DeliveredOn, int Quantity, string? Material, IReadOnlyList<string> Teeth, string? Note,
    long TotalFeeVnd, long PaidVnd, string Status, bool Overdue, int Version);
public sealed record LabWorkCounts(int NotSent, int Sent, int Received, int Delivered, int Rework, int Overdue);
public sealed record LabWorkPage(IReadOnlyList<LabWorkItem> Items, int Total, int Page, int PageSize, LabWorkCounts Counts,
    IReadOnlyList<LabOrderLab> Labs, IReadOnlyList<LabOrderDentist> Dentists);
public sealed record LabStatusLogItem(string? FromStatus, string ToStatus, DateTimeOffset ChangedAt, string ChangedByName, string? Note);
public sealed record ChangeLabStatusRequest(string Status, int ExpectedVersion, string? Note);
