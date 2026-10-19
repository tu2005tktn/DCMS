using DCMS.Application.Common;

namespace DCMS.Application.Features.Appointments;

public sealed record BookerContact(string? Name, string? Phone, string? Email);
public sealed record AppointmentAttendee(int? PatientId, string? Name, DateOnly? BirthDate);
public sealed record CreateAppointmentRequest(int BranchId, BookerContact? Booker, AppointmentAttendee? Attendee,
    string? AttendeeRelation, string? Kind, string? Source, int? ServiceId, string? Reason,
    int DentistId, int? AssistantId, int? ChairId, DateTimeOffset StartAt, int DurationMinutes,
    Guid? FollowUpId, string? ExceptionReason, Guid RequestKey, int ScheduleVersion,
    int? AssistantScheduleVersion, int? BookingSettingsVersion = null);
public sealed record AppointmentResult(Guid AppointmentId, string Code, int Version, string Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt, SystemMessage Notification);
public sealed record CreateAppointmentOutcome(AppointmentResult Result, bool Replayed);

public interface IAppointmentService
{
    Task<CreateAppointmentOutcome> CreateAsync(int? accountId, CreateAppointmentRequest request, CancellationToken ct);
}

// TV4 can replace this port when the follow-up module arrives.
public interface IFollowUpBookingPort
{
    Task ValidateAsync(Guid followUpId, int patientId, int dentistId, int durationMinutes, CancellationToken ct);
    Task MarkBookedAsync(Guid followUpId, Guid appointmentId, CancellationToken ct);
}
