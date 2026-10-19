using DCMS.Application.Common;

namespace DCMS.Application.Features.Treatment;

public sealed record StepDefinition(Guid LogicalStepId, Guid? ItemId, string Name, string? Region,
    int EstimatedMinutes, int ReservedMinutes, Guid[] PredecessorIds, string? Capability,
    string? LabRequirement, int? EarliestOffsetDays, int? TargetOffsetDays, int? LatestOffsetDays,
    string ChargingCoverage);
public sealed record CreateTemplateRequest(int BranchId, string Name, StepDefinition[] Steps, Guid RequestKey);
public sealed record NewTemplateVersionRequest(int ExpectedVersion, StepDefinition[] Steps, Guid RequestKey);
public sealed record UpdateTemplateDraftRequest(int ExpectedVersion, StepDefinition[] Steps, Guid RequestKey);
public sealed record TemplateStateRequest(int ExpectedVersion, Guid RequestKey);
public sealed record TreatmentTemplateVersionDto(int Number, string State, StepDefinition[] Steps,
    DateTimeOffset CreatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? RetiredAt);
public sealed record TreatmentTemplateDto(Guid Id, int BranchId, string Name, int Version,
    IReadOnlyList<TreatmentTemplateVersionDto> Versions);
public sealed record TemplateListItem(Guid Id, int BranchId, string Name, int Version,
    int? ActiveVersion, DateTimeOffset CreatedAt);
public sealed record ApplyTemplateRequest(int ExpectedVersion, Guid TemplateId, int TemplateVersion,
    StepDefinition[] Steps, Guid RequestKey);
public sealed record StepProgressRequest(int ExpectedVersion, int ExpectedStepVersion, string Outcome,
    string Reason, Guid RequestKey);
public sealed record HoldStepRequest(int ExpectedVersion, int ExpectedStepVersion, string Reason,
    DateTimeOffset EffectiveAt, string ReviewCondition, Guid RequestKey);
public sealed record CompleteStepRequest(int ExpectedVersion, int ExpectedStepVersion,
    Guid[] ActualEvidenceIds, Guid RequestKey);
public sealed record StepDto(Guid Id, Guid ItemId, string Name, string? Region, int PlanVersion,
    int EstimatedMinutes, int ReservedMinutes, Guid[] PredecessorIds, string? Capability,
    string? LabRequirement, int? EarliestOffsetDays, int? TargetOffsetDays, int? LatestOffsetDays,
    string ChargingCoverage, string Outcome, string Readiness, string[] Blockers,
    string? HoldReason, string? ReviewCondition, int Version);
public sealed record StepChange(IReadOnlyList<StepDto> Steps, Guid[] InstructionIds,
    SystemMessage Notification);

public sealed record FollowUpSource(Guid? DossierId, Guid? ItemId, Guid? StepId,
    Guid? VisitId, Guid? ActivityId);
public sealed record CreateFollowUpRequest(int PatientId, int BranchId, FollowUpSource Source,
    string Purpose, string Reason, string? Guidance, DateOnly EarliestOn, DateOnly TargetOn,
    DateOnly LatestOn, int DurationMinutes, int? DentistId, string? Capability, Guid RequestKey);
public sealed record ReviseFollowUpRequest(int ExpectedVersion, DateOnly EarliestOn,
    DateOnly TargetOn, DateOnly LatestOn, string Reason, Guid RequestKey);
public sealed record FollowUpActionRequest(int ExpectedVersion, string Reason, Guid RequestKey);
public sealed record FollowUpDto(Guid Id, int PatientId, int BranchId, FollowUpSource Source,
    string Purpose, string Reason, string? Guidance, DateOnly EarliestOn, DateOnly TargetOn,
    DateOnly LatestOn, int DurationMinutes, int? DentistId, string? Capability, string Status,
    bool Overdue, Guid? ActiveAppointmentId, int Version, string[] AllowedActions);
public sealed record FollowUpChange(FollowUpDto Instruction, SystemMessage Notification);

public interface ITreatmentProgressionService
{
    Task<IReadOnlyList<TemplateListItem>> ListTemplatesAsync(int actorId, int branchId, CancellationToken ct);
    Task<TreatmentTemplateDto> GetTemplateAsync(int actorId, Guid id, CancellationToken ct);
    Task<TreatmentTemplateDto> CreateTemplateAsync(int actorId, CreateTemplateRequest request, CancellationToken ct);
    Task<TreatmentTemplateDto> NewVersionAsync(int actorId, Guid id, NewTemplateVersionRequest request, CancellationToken ct);
    Task<TreatmentTemplateDto> UpdateDraftAsync(int actorId, Guid id, int number,
        UpdateTemplateDraftRequest request, CancellationToken ct);
    Task<TreatmentTemplateDto> TemplateStateAsync(int actorId, Guid id, int number, string state,
        TemplateStateRequest request, CancellationToken ct);
    Task<StepChange> ApplyTemplateAsync(int actorId, Guid dossierId, ApplyTemplateRequest request, CancellationToken ct);
    Task<IReadOnlyList<StepDto>> StepsAsync(int actorId, Guid dossierId, CancellationToken ct);
    Task<StepChange> ProgressAsync(int actorId, Guid dossierId, Guid stepId, StepProgressRequest request, CancellationToken ct);
    Task<StepChange> HoldAsync(int actorId, Guid dossierId, Guid stepId, HoldStepRequest request, CancellationToken ct);
    Task<StepChange> ReleaseHoldAsync(int actorId, Guid dossierId, Guid stepId, HoldStepRequest request, CancellationToken ct);
    Task<StepChange> CompleteStepAsync(int actorId, Guid dossierId, Guid stepId, CompleteStepRequest request, CancellationToken ct);
    Task<IReadOnlyList<FollowUpDto>> ListFollowUpsAsync(int actorId, int patientId, CancellationToken ct);
    Task<FollowUpDto> GetFollowUpAsync(int actorId, Guid id, CancellationToken ct);
    Task<FollowUpChange> CreateFollowUpAsync(int actorId, CreateFollowUpRequest request, CancellationToken ct);
    Task<FollowUpChange> ReviseFollowUpAsync(int actorId, Guid id, ReviseFollowUpRequest request, CancellationToken ct);
    Task<FollowUpChange> ChangeFollowUpAsync(int actorId, Guid id, string action,
        FollowUpActionRequest request, CancellationToken ct);
}

// Other owners supply these implementations when their source modules are available.
public interface ITreatmentActualEvidencePort
{
    Task<bool> HasDentistConfirmedActualAsync(int patientId, Guid dossierId, Guid stepId,
        IReadOnlyList<Guid> evidenceIds, CancellationToken ct);
    Task<bool> HasFollowUpFulfillmentAsync(int patientId, Guid instructionId, CancellationToken ct);
}
public interface ITreatmentLabReadinessPort
{
    Task<LabReadinessResult> CheckAsync(int patientId, Guid stepId, string specification,
        int planVersion, CancellationToken ct);
}
public sealed record LabReadinessResult(bool Available, bool IsAccepted, string? BlockerEpisodeKey);
