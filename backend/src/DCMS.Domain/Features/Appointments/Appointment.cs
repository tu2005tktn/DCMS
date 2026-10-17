namespace DCMS.Domain.Features.Appointments;

public static class AppointmentRules
{
    public static bool PublicLeadTimeValid(DateTimeOffset now, DateTimeOffset start) => start >= now.AddMinutes(60);
}

public sealed class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public string Code { get; set; } = "";
    public int BranchId { get; set; }
    public int? BookerAccountId { get; set; }
    public string BookerName { get; set; } = "";
    public string? BookerPhone { get; set; }
    public string? BookerEmail { get; set; }
    public int? PatientId { get; set; }
    public string AttendeeName { get; set; } = "";
    public DateOnly? AttendeeBirthDate { get; set; }
    public string AttendeeRelation { get; set; } = "Self";
    public string Kind { get; set; } = "Standard";
    public string Source { get; set; } = "App";
    public int? ServiceId { get; set; }
    public string? Reason { get; set; }
    public int DentistId { get; set; }
    public int? AssistantId { get; set; }
    public int ChairId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public Guid? FollowUpId { get; set; }
    public string? ExceptionReason { get; set; }
    public string Status { get; set; } = "Confirmed";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppointmentReceipt
{
    public Guid RequestKey { get; set; }
    public string ActorScope { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid AppointmentId { get; set; }
    public string ResultJson { get; set; } = "";
}

// Written with the booking transaction; the dispatcher is owned by TV5.
public sealed class AppointmentOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "AppointmentConfirmed";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}
