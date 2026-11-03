namespace DCMS.Application.Features.Treatment;

public sealed record DossierPaymentLineInput(Guid ActivityId, long AmountVnd);
public sealed record CreateDossierPaymentRequest(DateOnly PaidOn, string Method, int CollectorId, string? Note,
    IReadOnlyList<DossierPaymentLineInput> Lines, Guid RequestKey);
public sealed record DossierPaymentContextLine(Guid ActivityId, DateTimeOffset PerformedAt, string DentistName,
    string? ServiceName, long CostVnd, long DiscountVnd, long PaidVnd, long DueVnd);
public sealed record DossierPaymentCollector(int Id, string Name);
public sealed record DossierPaymentContext(long PaidVnd, IReadOnlyList<DossierPaymentContextLine> Lines,
    IReadOnlyList<DossierPaymentCollector> Collectors, int? DefaultCollectorId);
public sealed record DossierPaymentLineDto(Guid ActivityId, string? ServiceName, long AmountVnd);
public sealed record DossierPaymentDto(Guid Id, DateOnly PaidOn, string Method, int CollectorId, string CollectorName,
    string? Note, long TotalVnd, DateTimeOffset CreatedAt, IReadOnlyList<DossierPaymentLineDto> Lines);
