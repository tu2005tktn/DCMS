namespace DCMS.Application.Features.Treatment;

public sealed record ClinicalVisitState(bool Available, bool InProgress, int PatientId, int BranchId,
    int Version);
public interface IClinicalVisitPort
{
    Task<ClinicalVisitState> GetAsync(Guid visitId, CancellationToken ct);
}

public interface IClinicalConsentPort
{
    Task<bool> ValidAsync(int patientId, Guid? itemId, Guid? stepId, int? planVersion,
        int serviceId, CancellationToken ct);
}

public interface IClinicalChargePort
{
    Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request, CancellationToken ct);
    Task<bool> PostInTransactionAsync(int patientId,int branchId,Guid activityId,decimal quantity,decimal priceSnapshotVnd,int postingActor,CancellationToken ct) => Task.FromResult(false);
}

public sealed record ActivityInput(Guid VisitId, Guid? DossierId, Guid? ItemId, Guid? StepId,
    int? PlanVersion, int ServiceId, string? Region, string Unit, decimal Quantity,
    DateTimeOffset PerformedAt, int? ElapsedMinutes, string Outcome, string? NextSteps,
    string ChargingBasis, string? ChargingBasisEvidence, decimal PriceSnapshotVnd, Guid? ReviewId);
public sealed record CreateActivityDraftRequest(ActivityInput Activity, int ExpectedVisitVersion,
    Guid RequestKey);
public sealed record UpdateActivityDraftRequest(ActivityInput Activity, int ExpectedVersion,
    int ExpectedVisitVersion, Guid RequestKey);
public sealed record PostActivityRequest(int ExpectedVersion, int ExpectedVisitVersion,
    int? ExpectedStepVersion, string? StepOutcome, Guid RequestKey);
public sealed record ActivityDto(Guid Id, Guid? VisitId, int PatientId, int BranchId, Guid? DossierId,
    Guid? ItemId, Guid? StepId, int? PlanVersion, int ServiceId, string? Region, string Unit,
    decimal Quantity, DateTimeOffset PerformedAt, int? ElapsedMinutes, int PerformerId,
    string Outcome, string? NextSteps, string ChargingBasis, string? ChargingBasisEvidence,
    decimal PriceSnapshotVnd,
    Guid? ReviewId, string State, int Version, DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt, string FeeStatus = "Unavailable", long DiscountSnapshotVnd = 0,
    int? AssistantId = null, int? RecordedBy = null, string SourceKind = "Visit", string? ServiceNameSnapshot = null, string? LabWorkSnapshot = null, int SheetVersion = 0, bool SheetDeleted = false,
    string? SheetDiscountType = null, decimal? SheetDiscountValue = null, string? SheetDiscountReason = null);
public sealed record TreatmentContextDto(ClinicalVisitState Visit,
    IReadOnlyList<StepDto> Steps, IReadOnlyList<ActivityDto> Activities);
public sealed record ClinicalServiceOption(int Id, string Name);

public sealed record CreateReviewRequest(Guid VisitId, Guid? SourceActivityId,
    Guid? SourceItemId, string Complaint, string Findings, string Coverage,
    string Outcome, string? CoveredComponent, string? Exclusions, string? Policy,
    string? PolicyVersion, string? Basis, string? NextAction,
    int ExpectedVisitVersion, int ExpectedSourceVersion, Guid RequestKey);
public sealed record ReviewDto(Guid Id, Guid VisitId, int PatientId, int BranchId,
    Guid? SourceActivityId, Guid? SourceItemId, string Complaint, string Findings,
    string Coverage, string Outcome, string? CoveredComponent, string? Exclusions,
    string? Policy, string? PolicyVersion, string? Basis, string? NextAction,
    int DentistAccountId, DateTimeOffset RecordedAt, int Version);
public sealed record CreateCareNoteRequest(Guid VisitId, Guid? SourceActivityId,
    Guid? ReviewId, string Content, string? Response, int ExpectedVisitVersion,
    Guid RequestKey);
public sealed record CareNoteDto(Guid Id, Guid VisitId, int PatientId, int BranchId,
    Guid? SourceActivityId, Guid? ReviewId, string Content, string? Response,
    int ActorId, DateTimeOffset RecordedAt);

public interface IClinicalWorkService
{
    Task<ActivityDto> PostWithinTransactionAsync(int actorId, Guid id, PostActivityRequest request, CancellationToken ct, ClinicalFeeRequest? feeSnapshot = null);
    Task<IReadOnlyList<ClinicalServiceOption>> ServicesAsync(int actorId, int branchId, CancellationToken ct);
    Task<TreatmentContextDto> ContextAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<ActivityDto> CreateDraftAsync(int actorId, CreateActivityDraftRequest request, CancellationToken ct);
    Task<ActivityDto> UpdateDraftAsync(int actorId, Guid id, UpdateActivityDraftRequest request, CancellationToken ct);
    Task<ActivityDto> PostAsync(int actorId, Guid id, PostActivityRequest request, CancellationToken ct);
    Task<ReviewDto> CreateReviewAsync(int actorId, CreateReviewRequest request, CancellationToken ct);
    Task<IReadOnlyList<ReviewDto>> ReviewsAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<ReviewDto> ReviewAsync(int actorId, Guid id, CancellationToken ct);
    Task<CareNoteDto> AddCareNoteAsync(int actorId, CreateCareNoteRequest request, CancellationToken ct);
}

public sealed record CreateCareTaskRequest(int PatientId, int BranchId, Guid? InstructionId,
    Guid? AppointmentId, Guid? ReviewId, string Type, string Content,
    int? OwnerAccountId, DateTimeOffset DueAt, Guid RequestKey);
public sealed record UpdateCareTaskRequest(int ExpectedVersion, string Status,
    int? OwnerAccountId, DateTimeOffset DueAt, string Content, Guid RequestKey);
public sealed record ContactAttemptRequest(string Channel, string Result,
    string? NextAction, Guid? RebookedAppointmentId, int ExpectedTaskVersion, Guid RequestKey);
public sealed record EscalateCareRequest(int TargetClinicianAccountId, string Reason,
    int ExpectedTaskVersion, Guid RequestKey);
public sealed record ResolveEscalationRequest(int ExpectedVersion, string? Resolution,
    Guid RequestKey);
public sealed record CareTaskDto(Guid Id, int PatientId, int BranchId, Guid? InstructionId,
    Guid? AppointmentId, Guid? ReviewId, string Type, string Content,
    int? OwnerAccountId, DateTimeOffset DueAt, string Status, int Version,
    string[] AllowedActions);
public sealed record ContactAttemptDto(Guid Id, Guid TaskId, string Channel, string Result,
    string? NextAction, Guid? RebookedAppointmentId, int ActorId, DateTimeOffset OccurredAt);
public sealed record EscalationDto(Guid Id, Guid TaskId, int TargetClinicianAccountId,
    string Reason, string? Resolution, string Status, int CreatedBy,
    DateTimeOffset CreatedAt, int Version, DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt);
public sealed record CareTaskDetail(CareTaskDto Task, IReadOnlyList<ContactAttemptDto> Contacts,
    IReadOnlyList<EscalationDto> Escalations);
public interface ICareTaskService
{
    Task<IReadOnlyList<CareTaskDto>> ListAsync(int actorId, int branchId, DateOnly? from,
        DateOnly? to, CancellationToken ct);
    Task<CareTaskDetail> GetAsync(int actorId, Guid id, CancellationToken ct);
    Task<CareTaskDto> CreateAsync(int actorId, CreateCareTaskRequest request, CancellationToken ct);
    Task<CareTaskDto> UpdateAsync(int actorId, Guid id, UpdateCareTaskRequest request, CancellationToken ct);
    Task<ContactAttemptDto> ContactAsync(int actorId, Guid id, ContactAttemptRequest request, CancellationToken ct);
    Task<EscalationDto> EscalateAsync(int actorId, Guid id, EscalateCareRequest request, CancellationToken ct);
    Task<EscalationDto> ChangeEscalationAsync(int actorId, Guid id, bool resolve,
        ResolveEscalationRequest request, CancellationToken ct);
}
