using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.Appointments;

// Read side of UC-05 for S10/S12 (staff) and S14/S16 (patient app). Every request recomputes the actor's scope;
// filters and IDs from the client never widen it. Contract: docs/implementation/UC04-05-S10-S11.md.
public sealed class AppointmentQueryService(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock,
    IConfiguration configuration) : IAppointmentQueryService
{
    private static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(7);
    private static readonly int[] PageSizes = [20, 50, 100];
    private static readonly string[] Statuses = ["Confirmed", "CheckedIn", "InProgress", "Completed", "Cancelled", "NoShow"];
    private static readonly string[] ActiveStatuses = ["Confirmed", "CheckedIn", "InProgress"];
    private const double SelfServiceHours = 12;

    private static BusinessException Error(string code, int status = 400, string field = "Thông tin") => new(status, SystemMessages.Get(code, field));
    // Npgsql only accepts UTC offsets for timestamptz parameters.
    private static DateTimeOffset LocalMidnight(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();
    private static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(ClinicOffset).DateTime);
    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // Staff scope in one branch: an active branch member; assistants, owners and schedule managers see the branch,
    // a dentist sees only their own appointments (GBR-026, GBR-075).
    private sealed record Scope(int BranchId, int EmployeeId, int? OwnDentistId);

    private async Task<(int EmployeeId, bool Broad)> ActorAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff") throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        return (employee.Id, roles.Contains(AccountRoles.ClinicAssistant) || roles.Contains(AccountRoles.ClinicOwner));
    }

    private async Task<Scope> ScopeAsync(int actorId, int branchId, CancellationToken ct)
    {
        var (employeeId, broad) = await ActorAsync(actorId, ct);
        var membership = await db.Set<SchedulingMember>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.BranchId == branchId && x.EmployeeId == employeeId && x.Active, ct) ?? throw Error("MSG06", 403);
        if (broad || membership.CanManageSchedules) return new(branchId, employeeId, null);
        if (!membership.CanDentist) throw Error("MSG06", 403);
        return new(branchId, employeeId, employeeId);
    }

    public async Task<AppointmentOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var (employeeId, broad) = await ActorAsync(actorId, ct);
        var mine = await db.Set<SchedulingMember>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Active).ToListAsync(ct);
        mine = mine.Where(x => broad || x.CanManageSchedules || x.CanDentist).ToList();
        if (mine.Count == 0) throw Error("MSG06", 403);
        var branchIds = mine.Select(x => x.BranchId).ToArray();
        var ownOnly = mine.Where(x => !broad && !x.CanManageSchedules).Select(x => x.BranchId).ToHashSet();
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id) && x.Active)
            .OrderBy(x => x.Name).Select(x => new CalendarOption(x.Id, x.Name)).ToArrayAsync(ct);
        var members = await db.Set<SchedulingMember>().AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.Active && x.CanDentist).ToListAsync(ct);
        var names = await EmployeeNamesAsync(members.Select(x => x.EmployeeId), ct);
        var dentists = members.Where(x => !ownOnly.Contains(x.BranchId) || x.EmployeeId == employeeId)
            .Select(x => new CalendarDentist(x.EmployeeId, x.BranchId, names.GetValueOrDefault(x.EmployeeId, $"#{x.EmployeeId}")))
            .OrderBy(x => x.Name).ToArray();
        var chairs = await db.Set<SchedulingChair>().AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.Active)
            .OrderBy(x => x.Name).Select(x => new CalendarChair(x.Id, x.BranchId, x.Name)).ToArrayAsync(ct);
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => branchIds.Contains(x.BranchId) && x.Active && x.IsInitialExamination)
            .OrderBy(x => x.Name).Select(x => new CalendarService(x.Id, x.BranchId, x.Name, x.PublicDurationMinutes)).ToArrayAsync(ct);
        return new(branches, dentists, chairs, services, ownOnly.Count == branchIds.Length);
    }

    public async Task<CalendarView> CalendarAsync(int actorId, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > 6) throw Error("MSG04", field: "Khoảng ngày");
        var scope = await ScopeAsync(actorId, branchId, ct);
        var filterStatus = Status(status);
        var dentist = scope.OwnDentistId ?? dentistId;
        var rows = await Range(scope.BranchId, from, to, dentist, filterStatus).OrderBy(x => x.StartAt).ToListAsync(ct);

        int[] dentistIds = dentist.HasValue ? [dentist.Value] : await db.Set<SchedulingMember>().AsNoTracking()
            .Where(x => x.BranchId == branchId && x.Active && x.CanDentist).Select(x => x.EmployeeId).ToArrayAsync(ct);
        var weekStarts = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => Monday(from.AddDays(i))).Distinct().ToArray();
        var weeks = await db.Set<WorkingWeek>().AsNoTracking().Include(x => x.Periods)
            .Where(x => x.BranchId == branchId && dentistIds.Contains(x.EmployeeId) && weekStarts.Contains(x.WeekStart)).ToListAsync(ct);
        var days = new List<CalendarDay>();
        foreach (var id in dentistIds)
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var week = weeks.Where(x => x.EmployeeId == id && x.WeekStart == Monday(date)).MaxBy(x => x.Version);
                var index = date.DayNumber - Monday(date).DayNumber;
                days.Add(new(id, date, week?.Periods.Where(p => p.Day == index).OrderBy(p => p.StartMinute)
                    .Select(p => new CalendarPeriod(p.StartMinute, p.EndMinute, p.Kind, p.Kind == "Work" ? null : p.Reason)).ToArray() ?? []));
            }
        return new(branchId, from, to, await ItemsAsync(rows, ct), days, scope.OwnDentistId.HasValue);
    }

    public async Task<AppointmentListPage> ListAsync(int actorId, StaffAppointmentQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || !PageSizes.Contains(query.PageSize)) throw Error("MSG04", field: "Phân trang");
        if (query.To < query.From || query.To.DayNumber - query.From.DayNumber > 61) throw Error("MSG04", field: "Khoảng ngày");
        var scope = await ScopeAsync(actorId, query.BranchId, ct);
        var source = Range(scope.BranchId, query.From, query.To, scope.OwnDentistId ?? query.DentistId, Status(query.Status));
        var total = await source.CountAsync(ct);
        var rows = await source.OrderBy(x => x.StartAt).ThenBy(x => x.Code)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(await ItemsAsync(rows, ct), total, query.Page, query.PageSize);
    }

    public async Task<AppointmentListPage> ForPatientAsync(int actorId, int patientId, string scope, int page, int pageSize, CancellationToken ct)
    {
        if (scope is not ("ALL" or "UPCOMING" or "HISTORY") || page < 1 || !PageSizes.Contains(pageSize))
            throw Error("MSG04");
        var (employeeId, broad) = await ActorAsync(actorId, ct);
        if (!await db.Patients.AsNoTracking().AnyAsync(x => x.Id == patientId, ct)) throw Error("MSG113", 404);
        var memberships = db.Set<SchedulingMember>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Active);
        var activeBranches = db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Active);
        var source = db.Appointments.AsNoTracking().Where(a => a.PatientId == patientId
            && activeBranches.Any(b => b.Id == a.BranchId)
            && memberships.Any(m => m.BranchId == a.BranchId
                && (broad || m.CanManageSchedules || (m.CanDentist && a.DentistId == employeeId))));
        var now = clock.GetUtcNow();
        if (scope == "UPCOMING") source = source.Where(a => a.EndAt > now && ActiveStatuses.Contains(a.Status));
        if (scope == "HISTORY") source = source.Where(a => a.EndAt <= now || !ActiveStatuses.Contains(a.Status));
        var total = await source.CountAsync(ct);
        var ordered = scope == "UPCOMING" ? source.OrderBy(a => a.StartAt).ThenBy(a => a.Id)
            : source.OrderByDescending(a => a.StartAt).ThenBy(a => a.Id);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(await ItemsAsync(rows, ct), total, page, pageSize);
    }

    public async Task<StaffAppointmentDetail> DetailAsync(int actorId, Guid id, CancellationToken ct)
    {
        var row = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        var scope = await ScopeAsync(actorId, row.BranchId, ct);
        if (scope.OwnDentistId.HasValue && row.DentistId != scope.OwnDentistId) throw Error("MSG06", 403);
        var item = (await ItemsAsync([row], ct))[0];
        return new(item, row.BookerName, MaskPhone(row.BookerPhone), MaskEmail(row.BookerEmail), row.BookerAccountId.HasValue,
            row.Reason, row.ExceptionReason, row.CreatedAt);
    }

    // Patient app: own bookings plus the account's appointments through a current VERIFIED self link (GBR-031, GBR-073).
    private async Task<(IQueryable<Appointment> Query, int? PatientId)> MineQueryAsync(int accountId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(accountId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Public") throw Error("MSG06", 403);
        var patientId = await db.PatientAccountLinks.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Status == PatientLinkStatus.Verified).Select(x => x.PatientId).SingleOrDefaultAsync(ct);
        var query = db.Appointments.AsNoTracking()
            .Where(x => x.BookerAccountId == accountId || (patientId != null && x.PatientId == patientId));
        return (query, patientId);
    }

    public async Task<MyAppointmentPage> MineAsync(int accountId, MyAppointmentQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw Error("MSG04", field: "Phân trang");
        if (query.Scope is not ("UPCOMING" or "HISTORY")) throw Error("MSG04", field: "Nhóm thời gian");
        if (query.From > query.To) throw Error("MSG04", field: "Khoảng ngày");
        var (source, patientId) = await MineQueryAsync(accountId, ct);
        var now = clock.GetUtcNow();
        source = query.Scope == "UPCOMING"
            ? source.Where(x => x.EndAt > now && ActiveStatuses.Contains(x.Status))
            : source.Where(x => x.EndAt <= now || !ActiveStatuses.Contains(x.Status));
        if (Status(query.Status) is { } status) source = source.Where(x => x.Status == status);
        if (query.From is { } from) { var since = LocalMidnight(from); source = source.Where(x => x.StartAt >= since); }
        if (query.To is { } to) { var until = LocalMidnight(to.AddDays(1)); source = source.Where(x => x.StartAt < until); }
        var total = await source.CountAsync(ct);
        var ordered = query.Scope == "UPCOMING" ? source.OrderBy(x => x.StartAt) : source.OrderByDescending(x => x.StartAt);
        var rows = await ordered.ThenBy(x => x.Code).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        var names = await EmployeeNamesAsync(rows.Select(x => x.DentistId), ct);
        return new(rows.Select(x => Card(x, names, accountId, patientId)).ToArray(), total, query.Page, query.PageSize);
    }

    public async Task<MyAppointmentDetail> MineDetailAsync(int accountId, string code, CancellationToken ct)
    {
        var (source, patientId) = await MineQueryAsync(accountId, ct);
        // Out of scope and missing are indistinguishable so a code cannot be used to probe other bookings.
        var row = await source.SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw Error("MSG102", 404);
        return await DetailOfAsync(row, accountId, patientId, false, ct);
    }

    public async Task<MyAppointmentDetail> ChangeDetailAsync(int accountId, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(accountId.ToString());
        if (user?.AccountType != "Staff") return await MineDetailAsync(accountId, code, ct);
        var row = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct) ?? throw Error("MSG102", 404);
        var scope = await ScopeAsync(accountId, row.BranchId, ct);
        if (scope.OwnDentistId.HasValue && row.DentistId != scope.OwnDentistId) throw Error("MSG102", 404);
        return await DetailOfAsync(row, accountId, null, true, ct);
    }

    private async Task<MyAppointmentDetail> DetailOfAsync(Appointment row, int accountId, int? patientId, bool staff, CancellationToken ct)
    {
        var names = await EmployeeNamesAsync([row.DentistId], ct);
        var card = Card(row, names, accountId, patientId);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => x.Id == row.BranchId).Select(x => x.Name).SingleOrDefaultAsync(ct);
        var service = row.ServiceId.HasValue
            ? await db.Set<SchedulingService>().AsNoTracking().Where(x => x.Id == row.ServiceId).Select(x => x.Name).SingleOrDefaultAsync(ct) : null;
        var hours = (row.StartAt - clock.GetUtcNow()).TotalHours;
        var reason = row.Status is "CheckedIn" or "InProgress" || hours <= 0 ? "STARTED"
            : row.Status != "Confirmed" ? "NOT_CONFIRMED" : hours < SelfServiceHours ? "WITHIN_12H" : null;
        var history = await db.AppointmentChanges.AsNoTracking().Where(x => x.AppointmentId == row.Id)
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id)
            .Select(x => new MyAppointmentChange(x.Action, x.ActorKind, x.OldStart, x.NewStart, x.Reason, x.OccurredAt)).ToArrayAsync(ct);
        var supportRequests = await db.AppointmentSupportRequests.AsNoTracking()
            .Where(x => x.AppointmentId == row.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Code,
                appointmentCode = row.Code,
                kind = x.Kind == "Change" ? "RESCHEDULE" : x.Kind == "Cancel" ? "CANCEL" : x.Kind,
                reason = x.Reason ?? "",
                status = x.Status,
                createdAt = x.CreatedAt,
                decidedAt = x.DecidedAt,
                publicNote = x.PublicNote,
                version = x.Version,
                proposedStartAt = x.ProposedStartAt
            }).ToListAsync(ct);
        return new(card.Code, card.Start, card.End, card.DurationMinutes, card.DentistName, card.AttendeeName, card.AttendeeKind,
            card.Status, staff ? "STAFF" : card.Access, card.Version, row.BranchId, branch ?? "", row.DentistId, row.ServiceId, service ?? "Khám và tư vấn",
            row.BookerName, MaskPhone(row.BookerPhone) ?? MaskEmail(row.BookerEmail) ?? "",
            row.FollowUpId.HasValue ? new { id = row.FollowUpId, status = row.Status == "Cancelled" ? "NeedsBooking" : "Booked" } : null,
            new(reason == null, Math.Round(hours, 2), reason, reason == "WITHIN_12H"), supportRequests, history,
            new(configuration["Clinic:Phone"] ?? "0234 3 888 999", configuration["Clinic:Hours"] ?? "Thứ 2 – Thứ 7, 08:00 – 17:00"), row.AssistantId);
    }

    private static MyAppointmentCard Card(Appointment x, IReadOnlyDictionary<int, string> names, int accountId, int? patientId)
    {
        var booked = x.BookerAccountId == accountId;
        var own = patientId.HasValue && x.PatientId == patientId;
        return new(x.Code, x.StartAt, x.EndAt, (int)(x.EndAt - x.StartAt).TotalMinutes, names.GetValueOrDefault(x.DentistId, ""),
            x.AttendeeName, x.AttendeeRelation == "Self" ? "SELF" : "RELATIVE", x.Status,
            booked && own ? "BOTH" : own ? "MY_OWN" : "BOOKED_BY_ME", x.Version);
    }

    private static string? Status(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("ALL", StringComparison.OrdinalIgnoreCase)) return null;
        return Statuses.FirstOrDefault(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw Error("MSG04", field: "Trạng thái");
    }

    private IQueryable<Appointment> Range(int branchId, DateOnly from, DateOnly to, int? dentistId, string? status)
    {
        var since = LocalMidnight(from);
        var until = LocalMidnight(to.AddDays(1));
        var query = db.Appointments.AsNoTracking().Where(x => x.BranchId == branchId && x.StartAt < until && x.EndAt > since);
        if (dentistId.HasValue) query = query.Where(x => x.DentistId == dentistId);
        if (status != null) query = query.Where(x => x.Status == status);
        return query;
    }

    private async Task<Dictionary<int, string>> EmployeeNamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var set = ids.Distinct().ToArray();
        return await db.Employees.AsNoTracking().Where(x => set.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
    }

    private async Task<AppointmentListItem[]> ItemsAsync(IReadOnlyList<Appointment> rows, CancellationToken ct)
    {
        var names = await EmployeeNamesAsync(rows.Select(x => x.DentistId), ct);
        var branchIds = rows.Select(x => x.BranchId).Distinct().ToArray();
        var chairIds = rows.Select(x => x.ChairId).Distinct().ToArray();
        var serviceIds = rows.Where(x => x.ServiceId.HasValue).Select(x => x.ServiceId!.Value).Distinct().ToArray();
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var chairs = await db.Set<SchedulingChair>().AsNoTracking().Where(x => chairIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var services = await db.Set<SchedulingService>().AsNoTracking().Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return rows.Select(x => new AppointmentListItem(x.Id, x.Code, x.BranchId, branches.GetValueOrDefault(x.BranchId, ""),
            x.StartAt, x.EndAt, (int)(x.EndAt - x.StartAt).TotalMinutes, x.DentistId, names.GetValueOrDefault(x.DentistId, ""),
            x.ChairId, chairs.GetValueOrDefault(x.ChairId, ""), x.ServiceId.HasValue ? services.GetValueOrDefault(x.ServiceId.Value) : null,
            x.AttendeeName, x.AttendeeRelation, x.Kind, x.Source, x.Status, x.Version)).ToArray();
    }

    private static string? MaskPhone(string? phone) => phone is { Length: >= 4 } ? phone[..2] + new string('*', phone.Length - 4) + phone[^2..] : null;
    private static string? MaskEmail(string? email)
    {
        if (email == null) return null;
        var at = email.IndexOf('@');
        return at <= 1 ? "***" + email[Math.Max(at, 0)..] : email[0] + new string('*', at - 1) + email[at..];
    }
}
