using DCMS.Application.Common;
using DCMS.Domain.Features.Scheduling;

namespace DCMS.Application.Features.Scheduling;

public sealed record ScheduleCommand(int BranchId, int DentistId, DateOnly WeekStart, int ExpectedVersion,
    SchedulePeriod[] Intervals, Guid RequestKey);
public sealed record ScheduleDto(Guid? Id, int BranchId, int DentistId, DateOnly WeekStart, int Version,
    string Status, SchedulePeriod[] Intervals, int? PublishedBy, DateTimeOffset? PublishedAt);
public sealed record PublishResult(ScheduleDto Schedule, SystemMessage Notification);
public sealed record AffectedAppointment(Guid Id, string Code, DateTimeOffset Start, DateTimeOffset End, string Attendee, string Status);
public sealed record SchedulePreview(AffectedAppointment[] AffectedAppointments);
public sealed record BranchOption(int Id, string Name);
public sealed record EmployeeOption(int Id, int BranchId, string Name, bool CanDentist, bool CanEdit, bool Self);
public sealed record ChairOption(int Id, int BranchId, string Name);
public sealed record ServiceOption(int Id, int BranchId, string Name, int PublicDurationMinutes);
public sealed record ScheduleOptions(BranchOption[] Branches, EmployeeOption[] Employees, ChairOption[] Chairs, ServiceOption[] Services);
public sealed record AvailabilityQuery(int BranchId, int DentistId, int? ServiceId, DateOnly Date, int DurationMinutes, int? AssistantId = null);
public sealed record AvailabilitySlot(DateTimeOffset Start, DateTimeOffset End, int ChairId, int DentistId, int? AssistantId,
    int ScheduleVersion, int? AssistantScheduleVersion, int BookingSettingsVersion = 1);
public sealed record AvailabilityResult(string TimeZone, DateOnly Date, AvailabilitySlot[] Slots);
public sealed record PreviewAvailabilityRequest(ScheduleCommand Schedule, AvailabilityQuery Query);
public sealed record ResourceCheck(int BranchId, int DentistId, int? ServiceId, DateTimeOffset Start, int DurationMinutes,
    int? ChairId, int? AssistantId, string? AttendeeKey, int ExpectedScheduleVersion,
    int? ExpectedAssistantScheduleVersion = null, Guid? ExcludeReservationId = null, bool Public = false, int? BookingSettingsVersion = null);
public sealed record BranchPeriod(int BranchId, int Day, int StartMinute, int EndMinute, string Kind, string Reason = "");
public sealed record RecurringCommand(int DentistId, DateOnly Date, string Kind, int ExpectedVersion,
    BranchPeriod[] Intervals, Guid RequestKey, string Reason = "");
public sealed record RevisionDto(Guid Id, int DentistId, DateOnly Date, string Kind, int Version,
    BranchPeriod[] Intervals, string Reason, int PublishedBy, DateTimeOffset PublishedAt);
public sealed record RecurringScheduleDto(int DentistId, int Version, RevisionDto? Template, RevisionDto? Exception,
    RevisionDto[] FutureChanges, ScheduleDto[] LegacyWeek);
public sealed record RecurringPublishResult(RecurringScheduleDto Schedule, SystemMessage Notification);
public sealed record RecurringPreviewRequest(RecurringCommand Schedule, int BranchId, int? ServiceId,
    DateOnly From, DateOnly To, int DurationMinutes, int? AssistantId = null);
public sealed record RecurringAvailabilityResult(AvailabilityResult[] Days);
public interface IWorkingScheduleService
{
    Task<RecurringScheduleDto> GetRecurringAsync(int actorId, int dentistId, DateOnly date, CancellationToken ct);
    Task<SchedulePreview> PreviewRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct);
    Task<RecurringPublishResult> PublishRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct);
    Task<RecurringAvailabilityResult> PreviewRecurringAvailabilityAsync(int actorId, RecurringPreviewRequest request, CancellationToken ct);
    Task<ScheduleOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<ScheduleOptions> PublicOptionsAsync(int branchId, CancellationToken ct);
    Task<ScheduleDto> GetAsync(int actorId, int branchId, int dentistId, DateOnly weekStart, CancellationToken ct);
    Task<SchedulePreview> PreviewAsync(int actorId, ScheduleCommand command, CancellationToken ct);
    Task<PublishResult> PublishAsync(int actorId, ScheduleCommand command, CancellationToken ct);
    Task<AvailabilityResult> PreviewAvailabilityAsync(int actorId, PreviewAvailabilityRequest request, CancellationToken ct);
}
public interface IAvailabilityService
{
    // Reception reuses resource validation for the existing reservation; never books or releases resources.
    Task ValidateReceptionAsync(Guid reservationId, int patientId, CancellationToken ct);
    Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct);
    // Caller must authorize the existing appointment and supply its immutable resource requirements.
    Task<AvailabilityResult> QueryForReservationAsync(AvailabilityQuery query, Guid reservationId, string? attendeeKey, CancellationToken ct);
    // TV2: caller MUST begin a transaction on the same scoped DbContext and persist reservations before commit.
    Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct);
}
