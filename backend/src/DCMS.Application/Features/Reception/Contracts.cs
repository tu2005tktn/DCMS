namespace DCMS.Application.Features.Reception;

public sealed record VisitSource(Guid? DossierId = null, Guid? ItemId = null, Guid? StepId = null, Guid? ActivityId = null);
public sealed record CheckInRequest(string Appointment, int BranchId, int PatientId, bool IdentityVerified,
    string? IdentityNote, string Purpose, VisitSource? Source, string? ReviewReason, string? AdditionalConcern,
    string? Note, DateTimeOffset? ActualArrivalAt, string? ArrivalReason, int ExpectedVersion, Guid RequestKey);
public sealed record ReorderVisitRequest(Guid? BeforeVisitId, string? Reason, int ExpectedVersion, Guid RequestKey);
public sealed record VisitPriorityRequest(string Priority, string? Reason, int ExpectedVersion, Guid RequestKey);
public sealed record ReceptionQuery(int BranchId, DateOnly Date, int? DentistId, string? AppointmentStatus,
    string? VisitStatus, string? Keyword);
public sealed record QueueRow(int Position, long Order, int WaitingMinutes);
public sealed record VisitResult(Guid AppointmentId, string AppointmentCode, string AppointmentStatus, int AppointmentVersion,
    Guid VisitId, string VisitCode, string VisitStatus, int Version, int BranchId, int PatientId, string PatientCode,
    string PatientName, int DentistId, string DentistName, DateTimeOffset AppointmentAt, DateTimeOffset ActualArrivalAt,
    DateTimeOffset EnteredAt, string Purpose, VisitSource Source, bool SourceReviewPending, IReadOnlyList<string> Blockers,
    QueueRow Queue, string? ClinicalPriority, int? PriorityDentistId, string? AdditionalConcern, string? ReviewReason,
    string? Note, string? ArrivalReason, IReadOnlyList<ReceptionSource> SourceLinks);
public sealed record ReceptionAppointment(Guid AppointmentId, string Code, int BranchId, DateTimeOffset StartAt,
    DateTimeOffset EndAt, string Status, int Version, int DentistId, string DentistName, int? PatientId,
    string? PatientCode, string BookerName, string? BookerPhone, string? BookerEmail,
    string AttendeeName, DateOnly? AttendeeBirthDate, string AttendeeRelation, bool NoShowEligible);
public sealed record ReceptionView(DateOnly Date, string TimeZone, DateTimeOffset ServerNow,
    IReadOnlyList<ReceptionAppointment> NotArrived, IReadOnlyList<VisitResult> Queue);
public sealed record ReceptionDetail(ReceptionAppointment Appointment, VisitResult? Visit);
public sealed record ReceptionOptions(DCMS.Application.Features.Appointments.AppointmentOptions Calendar, bool CanCheckIn,
    bool CanReorder, bool CanDecidePriority);
public sealed record ReceptionSource(Guid Id, string Kind, Guid? ParentId, string Label, string? Blocker, int Version);
public sealed record VisitHistory(Guid Id, string Action, int ActorAccountId, string ActorName, int? DecidingDentistId,
    string Reason, DateTimeOffset OccurredAt, string BeforeJson, string AfterJson);

public interface IReceptionSourceReader
{
    Task<IReadOnlyList<ReceptionSource>> ReadAsync(int branchId, int patientId, CancellationToken ct);
}

public interface IReceptionService
{
    Task<ReceptionOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<ReceptionView> ReadAsync(int actorId, ReceptionQuery query, CancellationToken ct);
    Task<ReceptionDetail> DetailAsync(int actorId, string appointment, CancellationToken ct);
    Task<IReadOnlyList<ReceptionSource>> SourcesAsync(int actorId, int branchId, int patientId, CancellationToken ct);
    Task<VisitResult> VisitAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<VisitResult> CheckInAsync(int actorId, CheckInRequest request, CancellationToken ct);
    Task<VisitResult> ReorderAsync(int actorId, Guid visitId, ReorderVisitRequest request, CancellationToken ct);
    Task<VisitResult> PriorityAsync(int actorId, Guid visitId, VisitPriorityRequest request, CancellationToken ct);
    Task<IReadOnlyList<VisitHistory>> HistoryAsync(int actorId, Guid visitId, CancellationToken ct);
}
