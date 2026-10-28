using System.Text.Json.Serialization;
using DCMS.Application.Features.Settings;

namespace DCMS.Application.Features.Finance;

public sealed record FinanceCapabilities(string[] Permissions, ScopeBranch[] Branches);
public sealed record InvestigationServiceDto(int Id, int BranchId, string Name, string Kind, string Unit, long? PriceVnd, int Version, bool Active);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveInvestigationService(Guid RequestKey, int BranchId, string Name, string Kind, string Unit, long? PriceVnd, int ExpectedVersion = 0, bool Active = true);
public sealed record ProviderDto(Guid Id, string Code, string Name, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProvider(Guid RequestKey, string Code, string Name);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QuoteInvestigation(int PatientId, int ExpectedVisitVersion, int ExpectedVersion, Guid RequestKey,
    string Mode, int? ServiceId = null, int? ExpectedPriceVersion = null, int Quantity = 1, long? AgreedUnitPriceVnd = null,
    string? Evidence = null, Guid? ProviderId = null, int? ProviderVersion = null, long? ProviderUnitCostVnd = null);
public sealed record PerformInvestigation(int PatientId, int ExpectedVisitVersion, int ExpectedVersion, Guid RequestKey);
public sealed record InvestigationBillingDto(Guid InvestigationId, Guid VisitId, int VisitVersion, int OrderVersion,
    string? Mode, int? ServiceId, string? ServiceName, string? Unit, int Quantity, long? UnitPriceVnd, long? AmountVnd,
    string? Evidence, DateTimeOffset? PerformedAt, Guid? ChargeId, Guid? ProviderId, string? ProviderName, long? ProviderCostVnd, string[] AllowedActions);
public sealed record ChargeDto(Guid Id, Guid VisitId, string SourceKind, Guid SourceId, string Name, string Unit,
    decimal Quantity, long UnitPriceVnd, long AmountVnd, long PaidVnd, long RemainingVnd, DateTimeOffset PostedAt);
public sealed record AllocationInput(Guid ChargeId, long AmountVnd);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectPayment(int PatientId, int ExpectedAccountVersion, Guid RequestKey, string Method, string Payer,
    IReadOnlyList<AllocationInput> Allocations, string? Note = null, string? DeclaredReference = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmPayment(int ExpectedVersion, int ExpectedAccountVersion, Guid RequestKey, string BankReference, string Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelPayment(int ExpectedVersion, Guid RequestKey, string Reason);
public sealed record PaymentDto(Guid Id, string? Number, string Method, string State, long AmountVnd, string Payer, string? Note,
    string? BankReference, int Version, int CreatedBy, DateTimeOffset CreatedAt, int? ConfirmedBy, DateTimeOffset? ConfirmedAt,
    IReadOnlyList<AllocationInput> Allocations,string CreatedByName,string? ConfirmedByName,string? DeclaredReference,string? ConfirmationNote,string? CancellationReason);
public sealed record PayableDto(Guid Id, Guid ChargeId, string ProviderName, long AmountVnd);
public sealed record BillInvestigation(Guid Id, string Name, string Kind, string State, string? BillingMode, DateTimeOffset? PerformedAt);
public sealed record BillDto(Guid VisitId, int PatientId, string PatientName, int BranchId, string BranchName, int AccountVersion,
    long TotalVnd, long PaidVnd, long RemainingVnd, IReadOnlyList<ChargeDto> Charges, IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<PayableDto>? ProviderPayables, string? SuggestedPayer, string[] AllowedActions, IReadOnlyList<BillInvestigation> Investigations);
public sealed record QueueBill(Guid VisitId, int PatientId, string PatientName, int BranchId, long TotalVnd, long RemainingVnd, int PendingPayments, int PendingServices,int PendingExaminationFees=0);

public interface IFinanceService
{
    Task<FinanceCapabilities> CapabilitiesAsync(int actor, CancellationToken ct);
    Task<InvestigationServiceDto[]> ServicesAsync(int actor, int branchId, bool includeInactive, CancellationToken ct);
    Task<InvestigationServiceDto> SaveServiceAsync(int actor, int? id, SaveInvestigationService command, CancellationToken ct);
    Task<ProviderDto[]> ProvidersAsync(int actor, CancellationToken ct);
    Task<ProviderDto> CreateProviderAsync(int actor, CreateProvider command, CancellationToken ct);
    Task<InvestigationBillingDto> BillingAsync(int actor, Guid visitId, Guid id, CancellationToken ct);
    Task<InvestigationBillingDto> QuoteAsync(int actor, Guid visitId, Guid id, QuoteInvestigation command, CancellationToken ct);
    Task<InvestigationBillingDto> PerformAsync(int actor, Guid visitId, Guid id, PerformInvestigation command, CancellationToken ct);
    Task<QueueBill[]> QueueAsync(int actor, int branchId, CancellationToken ct);
    Task<BillDto> BillAsync(int actor, Guid visitId, CancellationToken ct);
    Task<PaymentDto> CollectAsync(int actor, Guid visitId, CollectPayment command, CancellationToken ct);
    Task<PaymentDto> ConfirmAsync(int actor, Guid id, ConfirmPayment command, CancellationToken ct);
    Task<PaymentDto> CancelAsync(int actor, Guid id, CancelPayment command, CancellationToken ct);
}
