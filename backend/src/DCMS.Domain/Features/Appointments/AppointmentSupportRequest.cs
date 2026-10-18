namespace DCMS.Domain.Features.Appointments;

// UC-06 A1: a request for staff help when self-service is outside the 12-hour window. Creating or withdrawing it
// never changes the appointment or holds a slot; TV5 resolves or rejects it.
public sealed class AppointmentSupportRequest
{
    public int Id { get; set; }
    public Guid AppointmentId { get; set; }
    public int AccountId { get; set; }
    public string Kind { get; set; } = SupportRequestKinds.Reschedule;
    public string Reason { get; set; } = "";
    public string Status { get; set; } = SupportRequestStatuses.Pending;
    public string? PublicNote { get; set; }
    public int? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public Guid RequestKey { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class SupportRequestKinds
{
    public const string Reschedule = "RESCHEDULE";
    public const string Cancel = "CANCEL";
    public const string Other = "OTHER";
    public static readonly string[] All = [Reschedule, Cancel, Other];
}

public static class SupportRequestStatuses
{
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
}
