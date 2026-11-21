namespace DCMS.Application.Features.Treatment;

public sealed record SheetActivityMaterial(Guid MaterialId, decimal Quantity);

public sealed record ImportItemPlanRequest(Guid ItemPlanId, int ExpectedItemPlanVersion, int ExpectedVersion, Guid RequestKey);
public sealed record ImportItemPlanResult(TreatmentDossierDto Dossier, Guid[] CreatedItemIds, Guid ItemPlanId, int ItemPlanVersion);

public sealed record SheetService(int Id, int BranchId, string Name, string Unit, long? ReferenceUnitPriceVnd, int PriceVersion);
public sealed record SheetMember(int Id, string Name, bool CanDentist);
public sealed record SheetVisit(Guid Id, int Version, DateTimeOffset ArrivalAt, int DentistId);
public sealed record TreatmentSheetContext(IReadOnlyList<SheetService> Services, IReadOnlyList<SheetMember> Members,
    IReadOnlyList<SheetVisit> Visits, string[] AllowedActions);
public sealed record EditSheetActivityRequest(int ExpectedVersion, int ExpectedActivityVersion, int ExpectedSheetVersion,
    System.Text.Json.JsonElement Row, Guid RequestKey);
public sealed record SheetActivityActionRequest(int ExpectedVersion, int ExpectedActivityVersion, int ExpectedSheetVersion, Guid RequestKey);
public sealed record PerformSheetItemRequest(int ExpectedVersion, Guid RequestKey, System.Text.Json.JsonElement? Row = null);
public sealed record SheetItemActionRequest(int ExpectedVersion, Guid RequestKey);
public sealed record MarkSheetItemRequest(int ExpectedVersion, bool Completed, Guid RequestKey);
public sealed record EditSheetItemRequest(int ExpectedVersion, System.Text.Json.JsonElement Row, Guid RequestKey);
public sealed record ConfirmSheetPlanRequest(int ExpectedVersion, int ExpectedDraftVersion, Guid RequestKey);
public sealed record ConfirmSheetActualRequest(int ExpectedVersion, int ExpectedDraftVersion, Guid RequestKey);
public sealed record ConfirmSheetActualResult(TreatmentDossierDto Dossier, ActivityDto Activity);
public sealed record ScheduleNoteRequest(int ExpectedVersion, DateOnly? PlannedOn, string? Note, Guid RequestKey);
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
