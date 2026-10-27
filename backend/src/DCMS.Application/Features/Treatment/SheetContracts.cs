namespace DCMS.Application.Features.Treatment;

public sealed record SheetService(int Id, int BranchId, string Name, string Unit, long? ReferenceUnitPriceVnd, int PriceVersion);
public sealed record SheetMember(int Id, string Name, bool CanDentist);
public sealed record SheetVisit(Guid Id, int Version, DateTimeOffset ArrivalAt, int DentistId);
public sealed record SheetStep(Guid Id, Guid ItemId, string Name, int Version, string Outcome, bool Held);
public sealed record SheetConsentDocument(Guid Id, string FileName, int Version);
public sealed record TreatmentSheetContext(IReadOnlyList<SheetService> Services, IReadOnlyList<SheetMember> Members,
    IReadOnlyList<SheetVisit> Visits, string[] AllowedActions, IReadOnlyList<SheetStep> Steps,
    IReadOnlyList<SheetConsentDocument>? ConsentDocuments = null);
public sealed record EditSheetActivityRequest(int ExpectedVersion, int ExpectedActivityVersion, int ExpectedSheetVersion,
    System.Text.Json.JsonElement Row, Guid RequestKey);
public sealed record SheetActivityActionRequest(int ExpectedVersion, int ExpectedActivityVersion, int ExpectedSheetVersion, Guid RequestKey);
public sealed record PerformSheetItemRequest(int ExpectedVersion, Guid RequestKey, System.Text.Json.JsonElement? Row = null);
public sealed record SheetItemActionRequest(int ExpectedVersion, Guid RequestKey);
public sealed record MarkSheetItemRequest(int ExpectedVersion, bool Completed, Guid RequestKey);
public sealed record EditSheetItemRequest(int ExpectedVersion, System.Text.Json.JsonElement Row, Guid RequestKey);
public sealed record SheetDecision(string Decision, string? Counseling, string? DecisionMaker,
    string? RepresentativeBasis = null, Guid? ConsentDocumentId = null);
public sealed record SheetEstimate(int? MinMinutes, int? SelectedMinutes, int? MaxMinutes,
    int? Sessions, DateOnly? EarliestOn, DateOnly? TargetOn, DateOnly? LatestOn);
public sealed record ConfirmSheetPlanRequest(int ExpectedVersion, int ExpectedDraftVersion, SheetDecision? Decision, Guid RequestKey,
    SheetEstimate? Estimate = null, Guid? QualifyingItemId = null);
public sealed record ConfirmSheetActualRequest(int ExpectedVersion, int ExpectedDraftVersion, Guid? VisitId,
    int ExpectedVisitVersion, string Outcome, string ChargingBasis, string? ChargingBasisEvidence,
    bool CompleteItem, SheetDecision? DirectDecision, Guid RequestKey, Guid? StepId = null,
    int? ExpectedStepVersion = null);
public sealed record ConfirmSheetActualResult(TreatmentDossierDto Dossier, ActivityDto Activity);
public sealed record ScheduleNoteRequest(int ExpectedVersion, DateOnly? PlannedOn, string? Note, Guid RequestKey);
public sealed record DecideSheetItemRequest(int ExpectedVersion, SheetDecision Decision, Guid RequestKey);
public sealed record TreatmentActivityPage(IReadOnlyList<ActivityDto> Items, int Total, int Page, int PageSize);
public sealed record DossierTotals(int DossierCount, long PlannedGrossVnd, long PlannedDiscountVnd,
    long PlannedNetVnd, bool FinancialAvailable, long? PostedNetVnd, long? ConfirmedAllocatedVnd,
    long? OutstandingVnd, long? UnusedAdvanceVnd, int PendingFeeCount);

// Handoff to DCMS-101. Unique sourceKey is the consumer's idempotency key.
public sealed record ClinicalFeeRequest(string SourceKey, Guid ActivityId, int ActivityVersion,
    int PatientId, int BranchId, Guid? DossierId, Guid? ItemId, int? PlanVersion,
    string ChargingBasis, decimal Quantity, string Unit, long AgreedUnitPriceVnd,
    long GrossVnd, long ApprovedDiscountVnd, string? DiscountType, decimal? DiscountValue,
    string? DiscountReason, Guid? QualifyingItemId, int? DiscountApprovedBy = null,
    DateTimeOffset? DiscountApprovedAt = null, int? RecorderAccountId = null);
public sealed record ClinicalFeeResult(string Status, Guid? ChargeId = null, string? SourceVersion = null);

// S38/S39 wire contract only. No collection implementation or mock runtime fallback.
public sealed record FinancialCharge(Guid Id, int Version, string SourceCode, DateTimeOffset PostedAt,
    string Service, string Dentist, long GrossVnd, long DiscountVnd, long TaxVnd, long NetVnd,
    long ConfirmedAllocatedVnd, long DueVnd);
public sealed record CollectionAllocation(Guid ChargeId, int ExpectedChargeVersion, long AmountVnd);
public sealed record ConfirmCollectionRequest(Guid DossierId, DateTimeOffset ReceivedAt, string Method,
    string? Payer, string? Note, long ReceivedVnd, long AcknowledgedAdvanceVnd,
    CollectionAllocation[] Allocations, string ExpectedSourceVersion, Guid RequestKey);
public sealed record FinancialDossier(Guid Id, string Code, string ClinicalState, TreatmentFinancialSummary? Summary);
public interface ITreatmentFinancialWorkspace
{
    Task<IReadOnlyList<FinancialDossier>> ListAsync(int actorId, int patientId, CancellationToken ct);
}
