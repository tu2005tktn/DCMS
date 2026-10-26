using DCMS.Application.Common;
using System.Text.Json;

namespace DCMS.Application.Features.Treatment;

public sealed record CreateDossierRequest(int BranchId, int DentistId, string? Diagnosis, string? Note,
    DateOnly? StartedOn, Guid RequestKey);
public sealed record PaymentTermInput(string Purpose, long AmountVnd, DateOnly DueOn, string? Evidence,
    string? Milestone = null);
public sealed record TreatmentDiscountInput(string Type, decimal Value, string Reason, Guid? QualifyingItemId);
public sealed record TreatmentDiscountDto(string Type, decimal Value, string Reason, Guid? QualifyingItemId,
    string Status, int? DecidedBy, DateTimeOffset? DecidedAt, long AmountVnd);
public sealed record TreatmentItemInput(Guid? Id, int? SourceDiagnosisId, int ServiceId, string? Region,
    string Unit, decimal Quantity, int PlannedOrder, int? EstimatedMinutes, DateOnly? EstimatedStart,
    DateOnly? EstimatedEnd, long AgreedUnitPriceVnd, string Decision, string? Counseling,
    string? DecisionMaker, string? RepresentativeBasis, Guid? ConsentDocumentId,
    int? EstimatedMinMinutes = null, int? EstimatedMaxMinutes = null, int? ReservedMinutes = null,
    bool AcknowledgeShortReservation = false, int? EstimatedSessions = null,
    DateOnly? ReturnEarliestOn = null, DateOnly? ReturnTargetOn = null, DateOnly? ReturnLatestOn = null,
    TreatmentDiscountInput? Discount = null, bool ClearDiscount = false);
public sealed record SavePlanDraftRequest(int ExpectedVersion, string? Goals, TreatmentItemInput[] Items,
    PaymentTermInput[] PaymentTerms, Guid RequestKey);
public sealed record CommitPlanRequest(int ExpectedVersion, Guid RequestKey);
public sealed record ItemDisposition(Guid ItemId, string Outcome, string Reason);
public sealed record CloseDossierRequest(int ExpectedVersion, string Outcome, string? Reason,
    ItemDisposition[] Dispositions, Guid RequestKey);
public sealed record ReopenDossierRequest(int ExpectedVersion, string? Reason, Guid RequestKey);
public sealed record DeleteDossierRequest(int ExpectedVersion, Guid RequestKey);
public sealed record SetDossierDisplayStatusRequest(int ExpectedVersion, bool Completed, Guid RequestKey);
public sealed record UpdateDossierNoteRequest(int ExpectedVersion, string? Note, Guid RequestKey);
public sealed record SaveSheetDraftRequest(string Kind, Guid? VisitId, JsonElement Row,
    int ExpectedVersion, Guid RequestKey);
public sealed record SheetDraftDto(Guid Id, string Kind, Guid? VisitId, JsonElement Row,
    int Version, DateTimeOffset UpdatedAt);
public sealed record ConfirmWarrantyRequest(int ExpectedVersion, string? Policy, string? PolicyVersion,
    string? Scope, DateOnly? EndsOn, Guid RequestKey, string? Component = null,
    string? Exclusions = null, DateOnly? ActivationOn = null, string? Explanation = null,
    int? ExpectedServiceLifeMonths = null);
public sealed record DecideDiscountRequest(int ExpectedVersion, string Decision, Guid RequestKey);
public sealed record DepositExceptionRequest(int ExpectedVersion, string Reason, Guid RequestKey);
public sealed record PlanPricePreviewItem(Guid ItemId, long GrossVnd, long ApprovedDiscountVnd,
    long NetVnd, long? ReferenceUnitPriceVnd, string Decision, bool IncludedInTotal);
public sealed record PlanPricePreviewDto(Guid DossierId, int DossierVersion, int PlanVersion,
    IReadOnlyList<PlanPricePreviewItem> Items, long GrossVnd, long ApprovedDiscountVnd, long NetBeforeTaxVnd);
public sealed record CommercialPlanDto(Guid DossierId, string Code, int BranchId, int Version,
    string PlanState, IReadOnlyList<TreatmentItemDto> Items, string? DepositExceptionReason,
    PlanPricePreviewDto Preview);
public sealed record CommercialDossierListItem(Guid DossierId, string Code, int BranchId,
    int Version, int PendingDiscounts);
public sealed record TreatmentFinancialSummary(bool Available, long PlannedVnd, long? PostedChargeVnd,
    long? ReceivedVnd, long? AllocatedVnd, long? OutstandingVnd, string? SourceVersion);
public interface ITreatmentFinancialSummaryPort
{
    Task<TreatmentFinancialSummary?> ReadAsync(int patientId, int branchId, Guid dossierId,
        CancellationToken ct);
}

public sealed record TreatmentItemDto(Guid Id, int? SourceDiagnosisId, int ServiceId, string ServiceName,
    string? Region, string Unit, decimal Quantity, int PlannedOrder, int? EstimatedMinutes,
    DateOnly? EstimatedStart, DateOnly? EstimatedEnd, long AgreedUnitPriceVnd, string Decision,
    string Progress, string? Counseling, string? DecisionMaker, string? RepresentativeBasis,
    DateTimeOffset? DecidedAt, Guid? ConsentDocumentId, int? ConsentPlanVersion,
    string? WarrantyPolicy, string? WarrantyPolicyVersion, string? WarrantyScope,
    DateTimeOffset? WarrantyConfirmedAt, DateOnly? WarrantyEndsOn, string? Material = null,
    int? DecisionPlanVersion = null, int? EstimatedMinMinutes = null, int? EstimatedMaxMinutes = null,
    int? ReservedMinutes = null, int? EstimatedSessions = null, DateOnly? ReturnEarliestOn = null,
    DateOnly? ReturnTargetOn = null, DateOnly? ReturnLatestOn = null,
    int? ShortReservationAcknowledgedBy = null, DateTimeOffset? ShortReservationAcknowledgedAt = null,
    long? ReferenceUnitPriceVnd = null, int? ReferencePriceVersion = null,
    TreatmentDiscountDto? Discount = null, string? WarrantyComponent = null,
    string? WarrantyExclusions = null, DateOnly? WarrantyActivatedOn = null,
    string? WarrantyExplanation = null, int? ExpectedServiceLifeMonths = null);
public sealed record PaymentTermDto(string Purpose, long AmountVnd, DateOnly DueOn, string? Evidence,
    string? Milestone = null, int? PlanVersion = null);
public sealed record TreatmentEventDto(Guid Id, string Kind, int DossierVersion, int ActorId,
    DateTimeOffset OccurredAt, string PayloadJson);
public sealed record TreatmentDossierDto(Guid Id, string Code, int PatientId, int BranchId, int DentistId,
    DateOnly StartedOn, string Diagnosis, string? Note, string State, string? ClosureOutcome,
    string? ClosureReason, int Version, Guid PlanId, int PlanVersion, string PlanState, string? Goals,
    IReadOnlyList<TreatmentItemDto> Items, IReadOnlyList<PaymentTermDto> PaymentTerms,
    IReadOnlyList<TreatmentEventDto> Timeline, bool FinancialSummaryAvailable,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    TreatmentFinancialSummary? FinancialSummary = null, string? DepositExceptionReason = null,
    int? DepositExceptionBy = null, DateTimeOffset? DepositExceptionAt = null,
    bool DisplayCompleted = false);
public sealed record DossierListItem(Guid Id, string Code, int PatientId, int BranchId, int DentistId,
    string Diagnosis, string State, string? ClosureOutcome, int Version, int PlanVersion, DateTimeOffset UpdatedAt,
    bool DisplayCompleted);
public sealed record DossierPage(IReadOnlyList<DossierListItem> Items, int Total, int Page, int PageSize);
public sealed record TreatmentChange(TreatmentDossierDto Dossier, SystemMessage Notification);
public sealed record TreatmentHistoryItem(int Version, DateTimeOffset CommittedAt, int ActorId, string SnapshotJson);
public sealed record OwnTreatmentSummaryItem(Guid DossierId, string Code, string State, DateOnly StartedOn,
    string Diagnosis, int PlanVersion, IReadOnlyList<TreatmentItemDto> AgreedItems);

public interface ITreatmentDossierService
{
    Task<DossierPage> ListAsync(int actorId, int patientId, int? branchId, int page, int pageSize, CancellationToken ct);
    Task<TreatmentDossierDto> GetAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<TreatmentChange> CreateAsync(int actorId, int patientId, CreateDossierRequest request, CancellationToken ct);
    Task<TreatmentChange> SaveDraftAsync(int actorId, Guid dossierId, SavePlanDraftRequest request, CancellationToken ct);
    Task<TreatmentChange> CommitAsync(int actorId, Guid dossierId, CommitPlanRequest request, CancellationToken ct);
    Task<IReadOnlyList<TreatmentHistoryItem>> HistoryAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<TreatmentChange> CloseAsync(int actorId, Guid dossierId, CloseDossierRequest request, CancellationToken ct);
    Task<TreatmentChange> ReopenAsync(int actorId, Guid dossierId, ReopenDossierRequest request, CancellationToken ct);
    Task<TreatmentChange> DeleteAsync(int actorId, Guid dossierId, DeleteDossierRequest request, CancellationToken ct);
    Task<TreatmentChange> SetDisplayStatusAsync(int actorId, Guid dossierId,
        SetDossierDisplayStatusRequest request, CancellationToken ct);
    Task<TreatmentChange> UpdateNoteAsync(int actorId, Guid dossierId,
        UpdateDossierNoteRequest request, CancellationToken ct);
    Task<IReadOnlyList<SheetDraftDto>> SheetDraftsAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<SheetDraftDto> SaveSheetDraftAsync(int actorId, Guid dossierId, Guid rowId,
        SaveSheetDraftRequest request, CancellationToken ct);
    Task DeleteSheetDraftAsync(int actorId, Guid dossierId, Guid rowId, int expectedVersion, CancellationToken ct);
    Task<TreatmentChange> ConfirmWarrantyAsync(int actorId, Guid dossierId, Guid itemId, ConfirmWarrantyRequest request, CancellationToken ct);
    Task<TreatmentChange> DecideDiscountAsync(int actorId, Guid dossierId, Guid itemId, DecideDiscountRequest request, CancellationToken ct);
    Task<TreatmentChange> SetDepositExceptionAsync(int actorId, Guid dossierId, DepositExceptionRequest request, CancellationToken ct);
    Task<PlanPricePreviewDto> PricePreviewAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<CommercialPlanDto> CommercialPlanAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<IReadOnlyList<CommercialDossierListItem>> CommercialDossiersAsync(int actorId, CancellationToken ct);
    Task<IReadOnlyList<OwnTreatmentSummaryItem>> OwnSummaryAsync(int accountId, CancellationToken ct);
}
