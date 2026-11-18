namespace DCMS.Application.Features.Treatment;

public interface IClinicalChargePort
{
    Task<ClinicalFeeResult> PostInTransactionAsync(ClinicalFeeRequest request, CancellationToken ct);
    Task<bool> PostInTransactionAsync(int patientId,int branchId,Guid activityId,decimal quantity,decimal priceSnapshotVnd,int postingActor,CancellationToken ct) => Task.FromResult(false);
}

public sealed record PostActivityRequest(int ExpectedVersion, Guid RequestKey);
public sealed record ActivityDto(Guid Id, Guid DossierId, Guid? ItemId, int? PlanVersion, int ServiceId,
    string? Region, string Unit, decimal Quantity, DateTimeOffset PerformedAt, int PerformerId, string Outcome,
    decimal PriceSnapshotVnd, int Version, DateTimeOffset CreatedAt, string FeeStatus = "Unavailable",
    long DiscountSnapshotVnd = 0, int? AssistantId = null, string? ServiceNameSnapshot = null,
    string? LabWorkSnapshot = null, bool IsPlanTransfer = false, int SheetVersion = 0, bool SheetDeleted = false,
    string? SheetDiscountType = null, decimal? SheetDiscountValue = null, string? SheetDiscountReason = null)
{
    public static ActivityDto From(DCMS.Domain.Features.Treatment.ClinicalActivity x) => new(x.Id, x.DossierId, x.ItemId,
        x.PlanVersion, x.ServiceId, x.Region, x.Unit, x.Quantity, x.PerformedAt, x.PerformerId, x.Outcome,
        x.PriceSnapshotVnd, x.Version, x.CreatedAt, x.FeeStatus, x.DiscountSnapshotVnd, x.AssistantId,
        x.ServiceNameSnapshot, x.LabWorkSnapshot, x.IsPlanTransfer);
}

// Records the fee of an activity just added to a dossier, inside the caller's transaction.
public interface IClinicalWorkService
{
    Task<ActivityDto> PostWithinTransactionAsync(int actorId, Guid id, PostActivityRequest request, CancellationToken ct, ClinicalFeeRequest? feeSnapshot = null);
}

public sealed record CreateCareTaskRequest(int PatientId, int BranchId,
    Guid? AppointmentId, string Type, string Content,
    int? OwnerAccountId, DateTimeOffset DueAt, Guid RequestKey);
public sealed record UpdateCareTaskRequest(int ExpectedVersion, string Status,
    int? OwnerAccountId, DateTimeOffset DueAt, string Content, Guid RequestKey);
public sealed record ContactAttemptRequest(string Channel, string Result,
    string? NextAction, Guid? RebookedAppointmentId, int ExpectedTaskVersion, Guid RequestKey);
public sealed record CareTaskDto(Guid Id, int PatientId, int BranchId,
    Guid? AppointmentId, string Type, string Content,
    int? OwnerAccountId, DateTimeOffset DueAt, string Status, int Version,
    string[] AllowedActions);
public sealed record ContactAttemptDto(Guid Id, Guid TaskId, string Channel, string Result,
    string? NextAction, Guid? RebookedAppointmentId, int ActorId, DateTimeOffset OccurredAt);
public sealed record CareTaskDetail(CareTaskDto Task, IReadOnlyList<ContactAttemptDto> Contacts);
// S60 staff Web (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md): read-only worklist, pickers and Excel.
public sealed record CareStaffOption(int AccountId, string Name, string[] Roles);
public sealed record CareBranchOption(int Id, string Name, bool CanCreate, IReadOnlyList<CareStaffOption> Staff);
public sealed record CareTypeOption(string Code, string Name, bool System);
// Branches the actor can read: the owner reads every active branch, other staff their member branches.
public sealed record CareTaskOptions(DateOnly ClinicDate, int ActorAccountId, bool AllBranches,
    IReadOnlyList<CareBranchOption> Branches, IReadOnlyList<CareTypeOption> Types);
public sealed record CarePatientOption(int PatientId, string PatientCode, string FullName,
    string? Phone, DateTimeOffset LastVisitAt);
// BranchId null = every branch the actor can read.
public sealed record CareWorklistQuery(int? BranchId, DateOnly? From, DateOnly? To, int? OwnerAccountId,
    string? Type, string? Status, string? Search, int Page = 1, int PageSize = 20);
public sealed record CareWorklistItem(CareTaskDto Task, string BranchName, string PatientCode, string PatientName,
    string? Phone, string TypeName, string? OwnerName, string? LastContactResult,
    DateTimeOffset? LastContactAt);
public sealed record CareWorklistPage(IReadOnlyList<CareWorklistItem> Items, int Total, int Page,
    int PageSize);
public interface ICareTaskService
{
    Task<IReadOnlyList<CareTaskDto>> ListAsync(int actorId, int branchId, DateOnly? from,
        DateOnly? to, CancellationToken ct);
    Task<CareTaskDetail> GetAsync(int actorId, Guid id, CancellationToken ct);
    Task<CareTaskDto> CreateAsync(int actorId, CreateCareTaskRequest request, CancellationToken ct);
    Task<CareTaskDto> UpdateAsync(int actorId, Guid id, UpdateCareTaskRequest request, CancellationToken ct);
    Task<ContactAttemptDto> ContactAsync(int actorId, Guid id, ContactAttemptRequest request, CancellationToken ct);
    Task<CareTaskOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<IReadOnlyList<CarePatientOption>> PatientOptionsAsync(int actorId, int branchId,
        string? search, CancellationToken ct);
    Task<CareWorklistPage> WorklistAsync(int actorId, CareWorklistQuery query, CancellationToken ct);
    Task<byte[]> ExportAsync(int actorId, CareWorklistQuery query, CancellationToken ct);
}
