using DCMS.Application.Common;

namespace DCMS.Application.Features.PatientLinks;

// S07 — public account. Never exposes internal notes, reviewer identity or another Patient's data.
public sealed record SelfAccountSummary(int Id, string UserName, string FullName, string? VerifiedEmail, string? VerifiedPhone);
public sealed record SelfLinkRequestDto(int Id, string RequestCode, string Status, DateTimeOffset RequestedAt,
    string DeclaredFullName, DateOnly? DeclaredBirthDate, string? DeclaredPatientCode, string? MatchingInformation,
    DateTimeOffset? DecidedAt, SystemMessage? Result, string? PublicNote, int Version);
public sealed record SelfLinkOverview(SelfAccountSummary Account, string LinkStatus, SelfLinkRequestDto? Current,
    IReadOnlyList<SelfLinkRequestDto> History, bool CanSubmit);
public sealed record SubmitLinkRequest(string FullName, DateOnly? BirthDate, string? PatientCode,
    string? MatchingInformation, bool ConfirmSelf);
public sealed record SelfLinkChange(SelfLinkRequestDto Request, SystemMessage Notification, bool Created);
public sealed record WithdrawLinkRequest(int Version);
public sealed record OwnRecordSummary(string PatientCode, string FullName, DateOnly? BirthDate, DateTimeOffset LinkedAt);

public interface IPatientLinkService
{
    Task<SelfLinkOverview> OverviewAsync(int accountId, CancellationToken ct);
    Task<SelfLinkChange> SubmitAsync(int accountId, SubmitLinkRequest request, CancellationToken ct);
    Task<SelfLinkRequestDto> GetAsync(int accountId, int id, CancellationToken ct);
    Task<SelfLinkRequestDto> WithdrawAsync(int accountId, int id, WithdrawLinkRequest request, CancellationToken ct);
    Task<OwnRecordSummary> OwnRecordAsync(int accountId, CancellationToken ct);
}

// S08 — Clinic Assistant.
public sealed record LinkQueueItem(int Id, string RequestCode, int AccountId, string AccountUserName, string AccountFullName,
    string DeclaredFullName, DateTimeOffset RequestedAt, string Status, string? HandlerName, DateTimeOffset? HandledAt);
public sealed record LinkQueuePage(IReadOnlyList<LinkQueueItem> Items, int Total, int Page, int PageSize);
public sealed record ReviewAccountInfo(int Id, string UserName, string FullName, string? Email, bool EmailConfirmed,
    string? Phone, bool PhoneConfirmed, bool IsActive, DateTimeOffset CreatedAt);
public sealed record PatientSummary(int Id, string PatientCode, string FullName, DateOnly? BirthDate, string? Gender, string? Phone);
public sealed record PatientCandidate(PatientSummary Patient, bool HasActiveLink);
public sealed record LinkConflict(bool AccountHasActiveLink, string? ActiveRequestCode);
public sealed record LinkReviewDetail(int Id, string RequestCode, string Status, DateTimeOffset RequestedAt,
    ReviewAccountInfo Account, string DeclaredFullName, DateOnly? DeclaredBirthDate, string? DeclaredPatientCode,
    string? MatchingInformation, PatientSummary? Patient, string? VerificationNote, string? DecisionReason, string? PublicNote,
    string? ReviewerName, DateTimeOffset? ReviewedAt, string? RevokerName, DateTimeOffset? RevokedAt,
    DateTimeOffset? WithdrawnAt, LinkConflict Conflict, int Version);
public sealed record VerifyLinkRequest(int Version, int PatientId, string VerificationNote);
public sealed record RejectLinkRequest(int Version, string DecisionReason, string? VerificationNote, string? PublicNote);
public sealed record RevokeLinkRequest(int Version, string DecisionReason, string? PublicNote);
public sealed record LinkDecisionResult(LinkReviewDetail Request, SystemMessage Notification);
public sealed record LinkAuditDto(long Id, int ActorAccountId, string ActorName, string Action, string? Reason,
    DateTimeOffset OccurredAt, string BeforeJson, string AfterJson);

public interface IPatientLinkReviewService
{
    Task<LinkQueuePage> QueueAsync(string? status, DateOnly? from, DateOnly? to, string? keyword, int page, int pageSize, CancellationToken ct);
    Task<LinkReviewDetail> DetailAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<PatientCandidate>> PatientsAsync(int id, string? keyword, CancellationToken ct);
    Task<LinkDecisionResult> VerifyAsync(int actorId, int id, VerifyLinkRequest request, CancellationToken ct);
    Task<LinkDecisionResult> RejectAsync(int actorId, int id, RejectLinkRequest request, CancellationToken ct);
    Task<LinkDecisionResult> RevokeAsync(int actorId, int id, RevokeLinkRequest request, CancellationToken ct);
    Task<IReadOnlyList<LinkAuditDto>> AuditAsync(int id, CancellationToken ct);
}
