namespace DCMS.Application.Features.SupportRequests;

public sealed record StaffPermission(bool CanResolve, bool CanEdit, bool BranchMatch, bool OwnershipMatch);
public sealed record SupportRequester(int? AccountId, string Name, string? Phone, bool IsAnonymous);
public sealed record SupportProposedChange(DateTimeOffset? NewStartAt, int? NewDentistId, int? NewServiceId, string Reason);
public sealed record SupportAppointmentPerson(int? PatientId, string Name, DateOnly? BirthDate, string Relation);
public sealed record SupportBooker(int? AccountId, string Name, string? Phone, bool IsAnonymous);
public sealed record SupportAppointmentSummary(Guid Id, string Code, int Version, string Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt, int BranchId, string BranchName, int? ChairId,
    int DentistId, string DentistName, int? ServiceId, string ServiceName, int DurationMinutes,
    SupportAppointmentPerson Attendee, SupportBooker Booker, string ConfirmedToAttend);
public sealed record SupportHistoryEntry(DateTimeOffset At, string Actor, int? ActorUserId, string Action,
    object? Before, object? After, string? Notes);
public sealed record SupportRequestDto(string Id, Guid AppointmentId, string AppointmentCode, string Kind,
    string Status, int ExpectedVersion, Guid RequestKey, string? Reason, SupportProposedChange? ProposedChange,
    SupportRequester Requester, StaffPermission Permission, bool AppointmentActionCommitted,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SupportRequestDetail(string Id, Guid AppointmentId, string AppointmentCode, string Kind,
    string Status, int ExpectedVersion, Guid RequestKey, string? Reason, SupportProposedChange? ProposedChange,
    SupportRequester Requester, StaffPermission Permission, bool AppointmentActionCommitted,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    SupportAppointmentSummary Appointment, IReadOnlyList<SupportHistoryEntry> History, string? DecisionReason,
    DateTimeOffset? DecidedAt, string? DeciderName, DateTimeOffset? RequesterVerifiedAt,
    string? RequesterVerification);
public sealed record SupportRequestPage(IReadOnlyList<SupportRequestDto> Items, int Total, int Page, int PageSize);

public sealed record VerifyRequesterCommand(Guid RequestKey, int ExpectedVersion, string? Notes);
public sealed record StaffSupportChangeCommand(Guid RequestKey, int ExpectedVersion, string Reason,
    string? Notes, DateTimeOffset? NewStartAt);
public sealed record StaffSupportChangeResult(bool Ok, SupportAppointmentSummary? Appointment,
    string? NotificationCode, string? ErrorCode = null, string? ErrorMessage = null,
    SupportRequestDetail? Request = null);
public sealed record ResolveSupportCommand(Guid RequestKey, int ExpectedVersion, string Decision,
    string? Notes, string? DecisionReason);
public sealed record ResolveSupportResult(bool Ok, SupportRequestDetail? Request, string? NotificationCode,
    string? ErrorCode = null, string? ErrorMessage = null);
public sealed record AttendanceConfirmationCommand(Guid RequestKey, int ExpectedVersion, bool Confirmed);
public sealed record AttendanceConfirmationResult(Guid AppointmentId, int Version, string Status,
    string ConfirmedToAttend);

public sealed record CustomerSupportRequestCommand(Guid RequestKey, string Kind, string Reason,
    DateTimeOffset? NewStartAt = null, int? DentistId = null, int? ChairId = null);
public sealed record SupportCustomerRequestDto(string Id, string AppointmentCode, string Kind, string Reason,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, string? PublicNote, int Version,
    DateTimeOffset? ProposedStartAt = null);
public sealed record SupportCustomerResult(SupportCustomerRequestDto Request, DCMS.Application.Common.SystemMessage Notification);
public sealed record WithdrawSupportCommand(int ExpectedVersion);

public interface ISupportRequestService
{
    Task<SupportRequestPage> ListAsync(int actorId, string? statuses, int? branchId, int page, int pageSize, CancellationToken ct, Guid? appointmentId = null);
    Task<SupportRequestDetail> DetailAsync(int actorId, string id, CancellationToken ct);
    Task<SupportRequestDetail> VerifyRequesterAsync(int actorId, string id, VerifyRequesterCommand command, CancellationToken ct);
    Task<StaffSupportChangeResult> ChangeAppointmentAsync(int actorId, string id, StaffSupportChangeCommand command, CancellationToken ct);
    Task<ResolveSupportResult> ResolveAsync(int actorId, string id, ResolveSupportCommand command, CancellationToken ct);
    Task<AttendanceConfirmationResult> SetAttendanceAsync(int actorId, Guid appointmentId,
        AttendanceConfirmationCommand command, CancellationToken ct);
    Task<SupportCustomerResult> CreateCustomerRequestAsync(int actorId, string code, CustomerSupportRequestCommand command, CancellationToken ct);
    Task<SupportCustomerRequestDto> WithdrawCustomerRequestAsync(int actorId, string id, WithdrawSupportCommand command, CancellationToken ct);
}
