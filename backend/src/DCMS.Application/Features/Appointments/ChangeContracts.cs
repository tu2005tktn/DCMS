using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;

namespace DCMS.Application.Features.Appointments;

public sealed record RescheduleAppointmentRequest(int ExpectedVersion, Guid RequestKey, DateTimeOffset NewStart,
    int ChairId, int ScheduleVersion, int? AssistantScheduleVersion, string? Reason,
    string? RequesterVerification = null, int? DentistId = null, int? BookingSettingsVersion = null);
public sealed record CancelAppointmentRequest(int ExpectedVersion, Guid RequestKey, string? Reason,
    string? RequesterVerification = null);
public sealed record AppointmentChangeResult(MyAppointmentDetail Appointment, SystemMessage Notification);
public sealed record MyAppointmentChange(string Action, string ActorKind, DateTimeOffset OldStart,
    DateTimeOffset? NewStart, string Reason, DateTimeOffset OccurredAt);

public interface IAppointmentChangeService
{
    Task<AvailabilityResult> AvailabilityAsync(int actorId, string code, DateOnly date, int? dentistId, CancellationToken ct);
    Task<AppointmentChangeResult> RescheduleAsync(int actorId, string code, RescheduleAppointmentRequest request, CancellationToken ct);
    Task<AppointmentChangeResult> CancelAsync(int actorId, string code, CancelAppointmentRequest request, CancellationToken ct);
}

// Implementations MUST enlist in the caller's scoped DbContext transaction; no external side effects.
public interface IFollowUpAppointmentChangePort
{
    Task MarkNeedsBookingAsync(Guid followUpId, Guid appointmentId, CancellationToken ct);
}
