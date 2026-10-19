namespace DCMS.Domain.Features.Appointments;

public static class AppointmentRules
{
    public static bool PublicLeadTimeValid(DateTimeOffset now, DateTimeOffset start) => start >= now.AddMinutes(60);
    public static bool CanSelfService(string status, DateTimeOffset start, DateTimeOffset now) =>
        status == "Confirmed" && start >= now.AddHours(12);
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
    // S12: attendance intent is not an Appointment/Visit lifecycle state.
    public string ConfirmedToAttend { get; set; } = "Pending";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public int? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

public sealed class AppointmentSupportRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public Guid AppointmentId { get; set; }
    public string Kind { get; set; } = "General";
    public string Status { get; set; } = "Pending";
    public int Version { get; set; } = 1;
    public Guid RequestKey { get; set; }
    public int? RequesterAccountId { get; set; }
    public string RequesterName { get; set; } = "";
    public string? RequesterPhone { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? ProposedStartAt { get; set; }
    public string? ProposedReason { get; set; }
    public Guid? AppointmentActionRequestKey { get; set; }
    public DateTimeOffset? RequesterVerifiedAt { get; set; }
    public int? RequesterVerifiedBy { get; set; }
    public string? RequesterVerification { get; set; }
    public string? DecisionReason { get; set; }
    public string? PublicNote { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int? DecidedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AppointmentSupportHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid AppointmentId { get; set; }
    public int? ActorAccountId { get; set; }
    public string ActorKind { get; set; } = "System";
    public string Action { get; set; } = "RequestSubmitted";
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class SupportRequestReceipt
{
    public int ActorAccountId { get; set; }
    public Guid RequestKey { get; set; }
    public string Fingerprint { get; set; } = "";
    public Guid SupportRequestId { get; set; }
    public string ResultJson { get; set; } = "";
}

public sealed class InAppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicId { get; set; } = "";
    public int? AccountId { get; set; }
    public int? BranchId { get; set; }
    public string Kind { get; set; } = "AppointmentBooked";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? RequestId { get; set; }
    public string? DeepLink { get; set; }
    public bool HasExternalFailure { get; set; }
    public string DedupeKey { get; set; } = "";
    public List<NotificationDelivery> Deliveries { get; set; } = [];
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = "InApp";
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? LastError { get; set; }
}

public sealed class AppointmentReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public int AppointmentVersion { get; set; }
    public int AccountId { get; set; }
    public int LeadMinutes { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class StaffHandlingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "ExternalDeliveryFailed";
    public int? BranchId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? NotificationId { get; set; }
    public int AttemptCount { get; set; }
    public string Error { get; set; } = "";
    public string Status { get; set; } = "Open";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppointmentChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public int ActorAccountId { get; set; }
    public string ActorKind { get; set; } = "SELF";
    public string Action { get; set; } = "";
    public DateTimeOffset OldStart { get; set; }
    public DateTimeOffset OldEnd { get; set; }
    public int OldDentistId { get; set; }
    public int OldChairId { get; set; }
    public DateTimeOffset? NewStart { get; set; }
    public DateTimeOffset? NewEnd { get; set; }
    public int? NewDentistId { get; set; }
    public int? NewChairId { get; set; }
    public string Reason { get; set; } = "";
    public string? RequesterVerification { get; set; }
    public Guid RequestKey { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class AppointmentChangeReceipt
{
    public int ActorAccountId { get; set; }
    public Guid RequestKey { get; set; }
    public Guid AppointmentId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "";
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
