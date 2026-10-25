using DCMS.Application.Common;

namespace DCMS.Application.Features.Treatment;

public sealed record RevisionProposal(int ServiceId, string? Material, string? Region, string Unit,
    decimal Quantity, int? EstimatedMinutes, long AgreedUnitPriceVnd,
    int? EstimatedMinMinutes = null, int? EstimatedMaxMinutes = null,
    int? ReservedMinutes = null, bool AcknowledgeShortReservation = false,
    int? EstimatedSessions = null, DateOnly? ReturnEarliestOn = null,
    DateOnly? ReturnTargetOn = null, DateOnly? ReturnLatestOn = null);
public sealed record RevisionDisposition(Guid StepId, string Outcome, string Reason);
public sealed record SaveRevisionDraftRequest(int ExpectedDraftVersion, int BasePlanVersion,
    RevisionProposal Proposed, string Reason, string Counseling, string Decision,
    string DecisionMaker, string? RepresentativeBasis, RevisionDisposition[] FutureDispositions,
    Guid RequestKey);
public sealed record RevisionDraftDto(Guid DossierId, Guid ItemId, int ActorId, int BasePlanVersion,
    int Version, RevisionProposal Proposed, string Reason, string Counseling, string Decision,
    string DecisionMaker, string? RepresentativeBasis, RevisionDisposition[] FutureDispositions,
    DateTimeOffset UpdatedAt);
public sealed record RevisionContextDto(TreatmentDossierDto Dossier, TreatmentItemDto Item,
    IReadOnlyList<RevisionStepImpact> Steps, IReadOnlyList<RevisionInstructionImpact> Instructions,
    IReadOnlyList<RevisionBookingImpact> Bookings, IReadOnlyList<RevisionServiceOption> Services);
public sealed record RevisionServiceOption(int Id, string Name);
public sealed record RevisionStepImpact(Guid Id, int Version, int PlanVersion, string Name,
    string Outcome, bool Held);
public sealed record RevisionInstructionImpact(Guid Id, int Version, string Status, Guid? AppointmentId);
public sealed record RevisionBookingImpact(Guid Id, string Code, int Version, string Status,
    DateTimeOffset StartAt);
public sealed record RevisionFinanceSummary(decimal PostedChargeVnd, decimal AllocationVnd,
    decimal CreditVnd, decimal DueVnd, decimal ReferencePriceVnd,
    decimal DiscountVnd, decimal AdjustmentVnd);
public sealed record RevisionSourceState(bool Available, string? VersionToken, string? Reason,
    bool CommitSafe = false, RevisionFinanceSummary? Summary = null);
public sealed record RevisionPreviewDto(Guid DossierId, Guid ItemId, int DraftVersion,
    int DossierVersion, int PlanVersion, TreatmentItemDto Current, RevisionProposal Proposed,
    IReadOnlyList<RevisionStepImpact> Steps, IReadOnlyList<RevisionInstructionImpact> Instructions,
    IReadOnlyList<RevisionBookingImpact> Bookings, RevisionSourceState Actual,
    RevisionSourceState Finance, decimal ProposedDifferenceVnd, bool CanSave,
    string[] Blockers);
public sealed record PreviewRevisionRequest(Guid ItemId, int DraftVersion);
public sealed record ExpectedRevisionVersion(Guid Id, int Version);
public sealed record CommitRevisionRequest(Guid ItemId, int DraftVersion, int ExpectedDossierVersion,
    int ExpectedPlanVersion, ExpectedRevisionVersion[] Steps,
    ExpectedRevisionVersion[] Instructions, ExpectedRevisionVersion[] Bookings,
    string? ActualVersionToken, string? FinanceVersionToken, Guid RequestKey);
public sealed record RevisionChange(TreatmentDossierDto Dossier, Guid SelectedItemId,
    int PlanVersion, SystemMessage Notification);

public interface ITreatmentRevisionActualPort
{
    Task<RevisionSourceState> CheckAsync(int patientId, Guid dossierId, Guid itemId, CancellationToken ct);
}

public interface ITreatmentRevisionFinancePort
{
    Task<RevisionSourceState> CheckAsync(int patientId, int branchId, Guid dossierId, Guid itemId,
        RevisionProposal proposed, CancellationToken ct);
}

public interface ITreatmentRevisionService
{
    Task<RevisionContextDto> ContextAsync(int actorId, Guid dossierId, Guid itemId, CancellationToken ct);
    Task<RevisionDraftDto> DraftAsync(int actorId, Guid dossierId, Guid itemId, CancellationToken ct);
    Task<RevisionDraftDto> SaveDraftAsync(int actorId, Guid dossierId, Guid itemId,
        SaveRevisionDraftRequest request, CancellationToken ct);
    Task DeleteDraftAsync(int actorId, Guid dossierId, Guid itemId, CancellationToken ct);
    Task<RevisionPreviewDto> PreviewAsync(int actorId, Guid dossierId, PreviewRevisionRequest request,
        CancellationToken ct);
    Task<RevisionChange> CommitAsync(int actorId, Guid dossierId, CommitRevisionRequest request,
        CancellationToken ct);
}
