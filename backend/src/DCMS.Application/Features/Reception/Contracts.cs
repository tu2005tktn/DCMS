using DCMS.Application.Features.Appointments;

namespace DCMS.Application.Features.Reception;

public sealed record CheckInRequest(int PatientId, int ExpectedVersion, Guid RequestKey, bool IdentityConfirmed,
    DateTimeOffset? ArrivalAt, string? ArrivalReason, string Purpose, Guid? PriorCourseId, string? Note);
public sealed record ReceptionQuery(int BranchId, DateOnly Date, int? DentistId = null, string? Status = null,
    string? Keyword = null, int Page = 1, int PageSize = 20);
public sealed record ReceptionPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize,
    DateOnly ClinicDate, DateTimeOffset ServerNow);
public sealed record ReceptionOptions(AppointmentOptions Calendar, bool CanCheckIn, DateOnly ClinicDate, DateTimeOffset ServerNow);
public sealed record ReceptionAppointment(Guid AppointmentId, string Code, int BranchId, string BranchName,
    int DentistId, string DentistName, int ChairId, string ChairName, int? AssistantId,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string Status, int Version, int? PatientId,
    string? PatientCode, string? PatientName, string BookerName, string? BookerPhone, string? BookerEmail,
    string AttendeeName, DateOnly? AttendeeBirthDate, string AttendeeRelation, Guid? VisitId,
    bool CanCheckIn, string? IneligibleReason);
public sealed record ReceptionDetail(ReceptionAppointment Appointment, VisitResult? Visit,
    DateOnly ClinicDate, DateTimeOffset ServerNow, bool CanBackdate);
public sealed record ReceptionCourse(Guid Id, string Label, int Version);
public sealed record VisitSource(Guid? FollowUpId = null, Guid? PriorCourseId = null, int? CourseVersion = null,
    Guid? ItemId = null, Guid? StepId = null, Guid? ActivityId = null, string? Label = null, string? Blocker = null);
public sealed record VisitResult(Guid VisitId, int PatientId, string PatientCode, string PatientName,
    Guid AppointmentId, string AppointmentCode, int BranchId, string BranchName, int DentistId, string DentistName,
    int ChairId, int? AssistantId, DateTimeOffset AppointmentAt, DateTimeOffset ArrivalAt, DateTimeOffset RecordedAt,
    string Status, string AppointmentStatus, int VisitVersion, int AppointmentVersion, string Purpose,
    string? Note, string? ArrivalReason, VisitSource? Source, bool RequiresClinicalSourceReview,
    int WaitingMinutes, bool Replayed, string OpenVisitPath);

// Implemented by the clinical module against its authoritative data; no reception-owned clinical catalog.
public interface IReceptionSourceReader
{
    Task<IReadOnlyList<ReceptionCourse>> CoursesAsync(int branchId, int patientId, CancellationToken ct);
    Task<VisitSource> ResolveAsync(int branchId, int patientId, Guid priorCourseId, CancellationToken ct);
}

public interface IReceptionService
{
    Task<ReceptionOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<ReceptionPage<ReceptionAppointment>> AppointmentsAsync(int actorId, ReceptionQuery query, CancellationToken ct);
    Task<ReceptionDetail> DetailAsync(int actorId, Guid appointmentId, CancellationToken ct);
    Task<IReadOnlyList<ReceptionCourse>> CoursesAsync(int actorId, Guid appointmentId, int patientId, CancellationToken ct);
    Task<ReceptionPage<VisitResult>> QueueAsync(int actorId, ReceptionQuery query, CancellationToken ct);
    Task<VisitResult> VisitAsync(int actorId, Guid visitId, CancellationToken ct);
    Task<VisitResult> CheckInAsync(int actorId, Guid appointmentId, CheckInRequest request, CancellationToken ct);
}
