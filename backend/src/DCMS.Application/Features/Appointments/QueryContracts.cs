namespace DCMS.Application.Features.Appointments;

// Shared read DTOs for S10/S12 (staff) and S14/S16 (patient app).
// Contract: docs/implementation/UC04-05-S10-S11.md §3 and UC05-06-S14-S16.md §4.1. No clinical or financial data.
public sealed record CalendarOption(int Id, string Name);
public sealed record CalendarDentist(int Id, int BranchId, string Name);
public sealed record CalendarChair(int Id, int BranchId, string Name);
public sealed record CalendarService(int Id, int BranchId, string Name, int PublicDurationMinutes);
public sealed record AppointmentOptions(CalendarOption[] Branches, CalendarDentist[] Dentists, CalendarChair[] Chairs,
    CalendarService[] Services, bool OwnScheduleOnly);

public sealed record AppointmentListItem(Guid Id, string Code, int BranchId, string BranchName, DateTimeOffset StartAt,
    DateTimeOffset EndAt, int DurationMinutes, int DentistId, string DentistName, int ChairId, string ChairName,
    string? ServiceName, string AttendeeName, string AttendeeRelation, string Kind, string Source, string Status, int Version);
public sealed record AppointmentListPage(IReadOnlyList<AppointmentListItem> Items, int Total, int Page, int PageSize);
public sealed record StaffAppointmentDetail(AppointmentListItem Appointment, string BookerName, string? BookerPhoneMasked,
    string? BookerEmailMasked, bool BookedFromApp, string? Reason, string? ExceptionReason, DateTimeOffset CreatedAt);

// Published working periods of one dentist on one clinic date (minutes from local midnight).
public sealed record CalendarPeriod(int StartMinute, int EndMinute, string Kind, string? Reason);
public sealed record CalendarDay(int DentistId, DateOnly Date, IReadOnlyList<CalendarPeriod> Periods);
public sealed record CalendarView(int BranchId, DateOnly From, DateOnly To, IReadOnlyList<AppointmentListItem> Appointments,
    IReadOnlyList<CalendarDay> Days, bool OwnScheduleOnly);

public sealed record StaffAppointmentQuery(int BranchId, DateOnly From, DateOnly To, int? DentistId, string? Status, int Page, int PageSize);

// Patient app (S14/S16).
public sealed record MyAppointmentCard(string Code, DateTimeOffset Start, DateTimeOffset End, int DurationMinutes,
    string DentistName, string AttendeeName, string AttendeeKind, string Status, string Access, int Version);
public sealed record MyAppointmentPage(IReadOnlyList<MyAppointmentCard> Items, int Total, int Page, int PageSize);
public sealed record MyEligibility(bool CanSelfService, double HoursRemaining, string? ReasonCode, bool CanRequestSupport);
public sealed record MyClinicContact(string Phone, string Hours);
public sealed record MyAppointmentDetail(string Code, DateTimeOffset Start, DateTimeOffset End, int DurationMinutes,
    string DentistName, string AttendeeName, string AttendeeKind, string Status, string Access, int Version,
    int BranchId, string BranchName, int DentistId, int? ServiceId, string ServiceName, string BookerName,
    string BookerContactMasked, object? FollowUp, MyEligibility Eligibility, IReadOnlyList<object> SupportRequests,
    IReadOnlyList<object> History, MyClinicContact ClinicContact);
public sealed record MyAppointmentQuery(string Scope, string? Status, DateOnly? From, DateOnly? To, int Page, int PageSize);

public interface IAppointmentQueryService
{
    Task<AppointmentOptions> OptionsAsync(int actorId, CancellationToken ct);
    Task<CalendarView> CalendarAsync(int actorId, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status, CancellationToken ct);
    Task<AppointmentListPage> ListAsync(int actorId, StaffAppointmentQuery query, CancellationToken ct);
    Task<StaffAppointmentDetail> DetailAsync(int actorId, Guid id, CancellationToken ct);
    Task<MyAppointmentPage> MineAsync(int accountId, MyAppointmentQuery query, CancellationToken ct);
    Task<MyAppointmentDetail> MineDetailAsync(int accountId, string code, CancellationToken ct);
}
