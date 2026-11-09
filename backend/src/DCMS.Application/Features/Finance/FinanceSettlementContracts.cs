using System.Text.Json.Serialization;

namespace DCMS.Application.Features.Finance;

public sealed record PaymentMethodDto(int BranchId, string Code, string Name, bool Active, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SavePaymentMethod(Guid RequestKey, int BranchId, string Code, string Name,
    bool Active, int ExpectedVersion);
public sealed record InvoiceLineDto(Guid Id, Guid ChargeId, string ServiceName, DateOnly ServiceDate,
    long GrossVnd, long DiscountVnd, long NetVnd, long PaidVnd);
public sealed record InvoicePaymentDto(Guid Id, string Source, string Method, string State, long AmountVnd,
    DateTimeOffset ReceivedAt, Guid ChargeId, int PaymentVersion);
public sealed record InvoiceDto(Guid Id, string Number, int PatientId, string PatientName, int BranchId,
    Guid? VisitId, Guid? DossierId, DateOnly InvoiceDate, DateOnly DueDate, DateTimeOffset CreatedAt,
    long GrossVnd, long DiscountVnd, long NetVnd, long PaidVnd, long OutstandingVnd, string State,
    int Version, int AccountVersion, IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<InvoicePaymentDto> Payments);
public sealed record InvoicePage(int Total, IReadOnlyList<InvoiceDto> Items);
public sealed record UnbilledChargeDto(Guid Id, int PatientId, string PatientName, int BranchId,
    Guid? VisitId, Guid? DossierId, string ServiceName, DateOnly ServiceDate, long AmountVnd);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IssueInvoice(Guid RequestKey, int PatientId, int BranchId, DateOnly InvoiceDate,
    DateOnly DueDate, IReadOnlyList<Guid> ChargeIds);
public sealed record AdvanceDto(Guid Id, int PatientId, int BranchId, Guid? DossierId, string Kind,
    Guid PaymentId, string PaymentState, string Method, long AmountVnd, long AllocatedVnd,
    long RefundedVnd, long AvailableVnd, DateTimeOffset CreatedAt, int Version,
    int PaymentVersion, int AccountVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAdvance(Guid RequestKey, int PatientId, int BranchId, Guid? DossierId,
    string Kind, string Method, long AmountVnd, string Payer, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplyAdvance(Guid RequestKey, Guid InvoiceLineId, long AmountVnd, int ExpectedVersion);
public sealed record ReceivableDto(int PatientId, string PatientName, int BranchId, long OutstandingVnd,
    DateOnly OldestInvoiceDate, DateOnly? EarliestDueDate, int InvoiceCount, long UnbilledVnd);
public sealed record DebtHistoryEntryDto(DateTimeOffset At, string Kind, Guid SourceId,
    string Description, long ChangeVnd, long BalanceVnd);
public sealed record PlanStageSettlementDto(Guid ItemId, string Name, int PlannedOrder, long PlannedGrossVnd,
    long PlannedDiscountVnd, long PlannedNetVnd, long PerformedNetVnd, long UnperformedNetVnd);
public sealed record PlanSettlementDto(Guid DossierId, int PatientId, int BranchId, int PlanVersion,
    long PlannedNetVnd, long PerformedNetVnd, long UnperformedNetVnd, long LinkedAdvanceAvailableVnd,
    long AdvanceForUnperformedServicesVnd, IReadOnlyList<PlanStageSettlementDto> Stages);
public sealed record RefundDto(Guid Id, string Number, int PatientId, int BranchId, Guid? InvoiceId, Guid? ChargeId,
    Guid? AdvanceId, Guid? PaymentId, Guid? DossierPaymentId, long AmountVnd, string Method,
    string Reason, string State, int RequestedBy, int? ApprovedBy, int? CompletedBy,
    DateTimeOffset RequestedAt, DateTimeOffset? ApprovedAt, DateTimeOffset? CompletedAt, int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestRefund(Guid RequestKey, Guid? InvoiceId, Guid? ChargeId, Guid? AdvanceId, Guid? PaymentId,
    Guid? DossierPaymentId, long AmountVnd, string Method, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideRefund(Guid RequestKey, int ExpectedVersion, string? Reason);
public sealed record SettlementTotals(long GrossVnd, long DiscountVnd, long RecognizedRevenueVnd,
    long CollectedVnd, long RefundedVnd, long NetCollectedVnd, long OutstandingVnd,
    long UnallocatedAdvanceVnd, long? AdvanceForUnperformedServicesVnd);
public sealed record SettlementOverview(DateOnly From, DateOnly To, DateTimeOffset AsOf,
    SettlementTotals Totals, int ReconciliationIssueCount);

public interface IFinanceSettlementService
{
    Task<PaymentMethodDto[]> PaymentMethodsAsync(int actor, int branchId, CancellationToken ct);
    Task<PaymentMethodDto> SavePaymentMethodAsync(int actor, SavePaymentMethod command, CancellationToken ct);
    Task<InvoicePage> InvoicesAsync(int actor, int? branchId, int? patientId, int page, int pageSize, CancellationToken ct);
    Task<InvoiceDto> InvoiceAsync(int actor, Guid id, CancellationToken ct);
    Task<UnbilledChargeDto[]> UnbilledChargesAsync(int actor, int branchId, CancellationToken ct);
    Task<InvoiceDto> IssueInvoiceAsync(int actor, IssueInvoice command, CancellationToken ct);
    Task<PaymentDto> CollectInvoiceAsync(int actor, Guid id, CollectPayment command, CancellationToken ct);
    Task<AdvanceDto[]> AdvancesAsync(int actor, int? branchId, int? patientId, CancellationToken ct);
    Task<AdvanceDto> CreateAdvanceAsync(int actor, CreateAdvance command, CancellationToken ct);
    Task<AdvanceDto> ApplyAdvanceAsync(int actor, Guid id, ApplyAdvance command, CancellationToken ct);
    Task<ReceivableDto[]> ReceivablesAsync(int actor, int? branchId, CancellationToken ct);
    Task<DebtHistoryEntryDto[]> DebtHistoryAsync(int actor, int patientId, int branchId, CancellationToken ct);
    Task<PlanSettlementDto> PlanSettlementAsync(int actor, Guid dossierId, CancellationToken ct);
    Task<RefundDto[]> RefundsAsync(int actor, int? branchId, CancellationToken ct);
    Task<RefundDto> RequestRefundAsync(int actor, RequestRefund command, CancellationToken ct);
    Task<RefundDto> ApproveRefundAsync(int actor, Guid id, DecideRefund command, CancellationToken ct);
    Task<RefundDto> CompleteRefundAsync(int actor, Guid id, DecideRefund command, CancellationToken ct);
    Task<RefundDto> RejectRefundAsync(int actor, Guid id, DecideRefund command, CancellationToken ct);
    Task<SettlementOverview> OverviewAsync(int actor, DateOnly? from, DateOnly? to, int? branchId, CancellationToken ct);
}
