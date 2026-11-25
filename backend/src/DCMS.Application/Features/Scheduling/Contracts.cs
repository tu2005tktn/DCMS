using DCMS.Application.Common;
using DCMS.Domain.Features.Scheduling;

namespace DCMS.Application.Features.Scheduling;

public sealed record ScheduleDto(Guid? Id, int BranchId, int DentistId, DateOnly WeekStart, int Version,
    string Status, SchedulePeriod[] Intervals, int? PublishedBy, DateTimeOffset? PublishedAt);
public sealed record AffectedAppointment(Guid Id, string Code, DateTimeOffset Start, DateTimeOffset End, string Attendee, string Status);
public sealed record SchedulePreview(AffectedAppointment[] AffectedAppointments);
public sealed record ScheduleAppointment(Guid Id, string Code, int BranchId, string BranchName,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string PatientName, string ServiceName, string Status,
    bool IsTreatmentPlan);
public sealed record ScheduleAppointments(IReadOnlyList<ScheduleAppointment> Items);
public sealed record BranchOption(int Id, string Name);
public sealed record EmployeeOption(int Id, int BranchId, string Name, bool CanDentist, bool CanEdit, bool Self, bool? CanView = null);
public sealed record ChairOption(int Id, int BranchId, string Name);
public sealed record ServiceOption(int Id, int BranchId, string Name, int PublicDurationMinutes);
public sealed record ScheduleOptions(BranchOption[] Branches, EmployeeOption[] Employees, ChairOption[] Chairs, ServiceOption[] Services);
public sealed record AvailabilityQuery(int BranchId, int DentistId, int? ServiceId, DateOnly Date, int DurationMinutes, int? AssistantId = null);
public sealed record AvailabilitySlot(DateTimeOffset Start, DateTimeOffset End, int ChairId, int DentistId, int? AssistantId,
    int ScheduleVersion, int? AssistantScheduleVersion, int BookingSettingsVersion = 1);
public sealed record AvailabilityResult(string TimeZone, DateOnly Date, AvailabilitySlot[] Slots);
public sealed record ResourceCheck(int BranchId, int DentistId, int? ServiceId, DateTimeOffset Start, int DurationMinutes,
    int? ChairId, int? AssistantId, int? PatientId, int ExpectedScheduleVersion,
    int? ExpectedAssistantScheduleVersion = null, Guid? ExcludeAppointmentId = null, bool Public = false, int? BookingSettingsVersion = null);
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
    Task<ScheduleAppointments> AppointmentsAsync(int actorId, int dentistId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<RecurringScheduleDto> GetRecurringAsync(int actorId, int dentistId, DateOnly date, CancellationToken ct);
    Task<SchedulePreview> PreviewRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct);
    Task<RecurringPublishResult> PublishRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct);
    Task<RecurringAvailabilityResult> PreviewRecurringAvailabilityAsync(int actorId, RecurringPreviewRequest request, CancellationToken ct);
    Task<ScheduleOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<ScheduleOptions> PublicOptionsAsync(int branchId, CancellationToken ct);
}
public interface IAvailabilityService
{
    Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct);
    // Caller must authorize the existing appointment and supply its immutable resource requirements.
    Task<AvailabilityResult> QueryForAppointmentAsync(AvailabilityQuery query, Guid appointmentId, int? patientId, CancellationToken ct);
    // TV2: caller MUST begin a transaction on the same scoped DbContext and persist the appointment before commit.
    Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct);
    // Reception rechecks the existing appointment without replacing or releasing it.
    Task ValidateReceptionAsync(Guid appointmentId, int patientId, CancellationToken ct);
}
