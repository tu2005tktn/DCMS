using DCMS.Application.Common;

namespace DCMS.Application.Features.Treatment;

public sealed record CreateDossierRequest(int BranchId, int DentistId, string? Diagnosis, string? Note,
    DateOnly? StartedOn, Guid RequestKey);
public sealed record PaymentTermInput(string Purpose, long AmountVnd, DateOnly DueOn, string? Evidence);
public sealed record TreatmentItemInput(Guid? Id, int? SourceDiagnosisId, int ServiceId, string? Region,
    string Unit, decimal Quantity, int PlannedOrder, int? EstimatedMinutes, DateOnly? EstimatedStart,
    DateOnly? EstimatedEnd, long AgreedUnitPriceVnd, string Decision, string? Counseling,
    string? DecisionMaker, string? RepresentativeBasis, Guid? ConsentDocumentId);
public sealed record SavePlanDraftRequest(int ExpectedVersion, string? Goals, TreatmentItemInput[] Items,
    PaymentTermInput[] PaymentTerms, Guid RequestKey);
public sealed record CommitPlanRequest(int ExpectedVersion, Guid RequestKey);
public sealed record ItemDisposition(Guid ItemId, string Outcome, string Reason);
public sealed record CloseDossierRequest(int ExpectedVersion, string Outcome, string? Reason,
    ItemDisposition[] Dispositions, Guid RequestKey);
public sealed record ReopenDossierRequest(int ExpectedVersion, string? Reason, Guid RequestKey);
public sealed record ConfirmWarrantyRequest(int ExpectedVersion, string? Policy, string? PolicyVersion,
    string? Scope, DateOnly? EndsOn, Guid RequestKey);

public sealed record TreatmentItemDto(Guid Id, int? SourceDiagnosisId, int ServiceId, string ServiceName,
    string? Region, string Unit, decimal Quantity, int PlannedOrder, int? EstimatedMinutes,
    DateOnly? EstimatedStart, DateOnly? EstimatedEnd, long AgreedUnitPriceVnd, string Decision,
    string Progress, string? Counseling, string? DecisionMaker, string? RepresentativeBasis,
    DateTimeOffset? DecidedAt, Guid? ConsentDocumentId, int? ConsentPlanVersion,
    string? WarrantyPolicy, string? WarrantyPolicyVersion, string? WarrantyScope,
    DateTimeOffset? WarrantyConfirmedAt, DateOnly? WarrantyEndsOn);
public sealed record PaymentTermDto(string Purpose, long AmountVnd, DateOnly DueOn, string? Evidence);
public sealed record TreatmentEventDto(Guid Id, string Kind, int DossierVersion, int ActorId,
    DateTimeOffset OccurredAt, string PayloadJson);
public sealed record TreatmentDossierDto(Guid Id, string Code, int PatientId, int BranchId, int DentistId,
    DateOnly StartedOn, string Diagnosis, string? Note, string State, string? ClosureOutcome,
    string? ClosureReason, int Version, Guid PlanId, int PlanVersion, string PlanState, string? Goals,
    IReadOnlyList<TreatmentItemDto> Items, IReadOnlyList<PaymentTermDto> PaymentTerms,
    IReadOnlyList<TreatmentEventDto> Timeline, bool FinancialSummaryAvailable,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DossierListItem(Guid Id, string Code, int PatientId, int BranchId, int DentistId,
    string Diagnosis, string State, string? ClosureOutcome, int Version, int PlanVersion, DateTimeOffset UpdatedAt);
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
    Task<TreatmentChange> ConfirmWarrantyAsync(int actorId, Guid dossierId, Guid itemId, ConfirmWarrantyRequest request, CancellationToken ct);
    Task<IReadOnlyList<OwnTreatmentSummaryItem>> OwnSummaryAsync(int accountId, CancellationToken ct);
}
