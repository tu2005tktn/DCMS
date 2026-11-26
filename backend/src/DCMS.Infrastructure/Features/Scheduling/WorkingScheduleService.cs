using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed partial class WorkingScheduleService(DcmsDbContext db, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess access) : IWorkingScheduleService, IAvailabilityService
{
    public const long TransactionLock = 90403;
    private static readonly string[] OccupiedStatuses = ["Confirmed", "CheckedIn", "InProgress"];
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    private static DateTimeOffset Local(DateOnly date, int minute = 0) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(minute);
    private static SchedulePeriod[] Periods(WorkingWeek? week) => week?.Periods.Select(p => new SchedulePeriod(p.Day, p.StartMinute, p.EndMinute, p.Kind, p.Reason)).ToArray() ?? [];
    private static ScheduleDto Dto(WorkingWeek? week, int branchId, int employeeId, DateOnly date) => new(week?.Id, branchId, employeeId, date,
        week?.Version ?? 0, week == null ? "Unpublished" : "Published", Periods(week), week?.PublishedBy, week?.PublishedAt);
    private Task<WorkingWeek?> Week(int branchId, int employeeId, DateOnly date, CancellationToken ct) => db.Set<WorkingWeek>().AsNoTracking()
        .SingleOrDefaultAsync(x => x.BranchId == branchId && x.EmployeeId == employeeId && x.WeekStart == date, ct);

    private async Task<SchedulingMember[]> ActiveMembers(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await (
            from member in db.SchedulingMembers.AsNoTracking()
            join employee in db.Employees on member.EmployeeId equals employee.Id
            join account in db.Users on employee.AccountId equals account.Id
            join branch in db.Set<SchedulingBranch>() on member.BranchId equals branch.Id
            where member.Active && branch.Active && account.AccountType == "Staff" && account.IsActive
            select new
            {
                member.BranchId, member.EmployeeId, member.Active, member.CanManageSchedules,
                CanDentist = member.CanDentist && db.UserRoles.Any(ur => ur.UserId == account.Id
                    && db.Roles.Any(role => role.Id == ur.RoleId && (role.Name == "Dentist" || role.Name == "ClinicOwner"))),
                account.AuthData
            }).ToArrayAsync(ct);
        return rows
            .Where(x => !x.AuthData.LockoutEnabled || x.AuthData.LockoutEnd == null || x.AuthData.LockoutEnd <= now)
            .Select(x => new SchedulingMember
            {
                BranchId = x.BranchId, EmployeeId = x.EmployeeId, Active = x.Active,
                CanManageSchedules = x.CanManageSchedules, CanDentist = x.CanDentist
            })
            .ToArray();
    }

    private async Task Authorize(int actorId, int branchId, int? employeeId, CancellationToken ct)
    {
        var scope = await access.ResolveAsync(actorId, ct);
        if (scope.Assigned && !scope.Owner && !scope.BranchIds.Contains(branchId)) throw Error("MSG05", 403);
        if (scope.Owner)
        {
            // Chủ phòng khám làm việc trên mọi chi nhánh đang hoạt động; nhân sự được chọn vẫn phải thuộc chi nhánh đó.
            var active = await ActiveMembers(ct);
            if (!active.Any(x => x.BranchId == branchId) || employeeId.HasValue && !active.Any(x => x.BranchId == branchId && x.EmployeeId == employeeId)) throw Error("MSG05", 403);
            return;
        }
        var own = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        var membership = (await ActiveMembers(ct)).SingleOrDefault(x => x.BranchId == branchId && x.EmployeeId == own?.Id);
        if (membership == null || (employeeId.HasValue && !membership.CanManageSchedules && (employeeId != own!.Id || !membership.CanDentist))) throw Error("MSG05", 403);
        if (employeeId.HasValue && !(await ActiveMembers(ct)).Any(x => x.BranchId == branchId && x.EmployeeId == employeeId)) throw Error("MSG05", 403);
    }
    public async Task<ScheduleOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var scope = await access.ResolveAsync(actorId, ct);
        var members = await ActiveMembers(ct);
        var own = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        var mine = members.Where(x => x.EmployeeId == own?.Id && (x.CanDentist || x.CanManageSchedules) && (!scope.Assigned || scope.Owner || scope.BranchIds.Contains(x.BranchId))).ToArray();
        // Chủ phòng khám quản lý lịch mọi chi nhánh đang hoạt động, không cần là thành viên.
        if (scope.Owner) mine = members.Select(x => x.BranchId).Distinct().Select(b => new SchedulingMember { BranchId = b, EmployeeId = own?.Id ?? 0, Active = true, CanManageSchedules = true, CanDentist = true }).ToArray();
        if (mine.Length == 0) throw Error("MSG05", 403);
        var branches = mine.Select(x => x.BranchId).ToArray();
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return new(await db.Set<SchedulingBranch>().Where(x => branches.Contains(x.Id)).Select(x => new BranchOption(x.Id, x.Name)).ToArrayAsync(ct),
            members.Where(x => branches.Contains(x.BranchId)).Select(x => new EmployeeOption(x.EmployeeId, x.BranchId, employees[x.EmployeeId].FullName,
                x.CanDentist, scope.Permissions.Contains("working-schedules.manage") && (mine.Any(m => m.BranchId == x.BranchId && m.CanManageSchedules) || x.EmployeeId == own!.Id), x.EmployeeId == own!.Id,
                mine.Any(m => m.BranchId == x.BranchId && m.CanManageSchedules) || x.EmployeeId == own!.Id)).ToArray(),
            await db.SchedulingChairs.Where(x => branches.Contains(x.BranchId) && x.Active).Select(x => new ChairOption(x.Id, x.BranchId, x.Name)).ToArrayAsync(ct),
            await db.Set<SchedulingService>().Where(x => branches.Contains(x.BranchId) && x.Active).Select(x => new ServiceOption(x.Id, x.BranchId, x.Name, x.PublicDurationMinutes)).ToArrayAsync(ct));
    }
    public async Task<ScheduleOptions> PublicOptionsAsync(int branchId, CancellationToken ct)
    {
        var members = (await ActiveMembers(ct)).Where(x => x.CanDentist && (branchId == 0 || x.BranchId == branchId)).ToArray();
        var branches = members.Select(x => x.BranchId).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return new(await db.Set<SchedulingBranch>().Where(x => branches.Contains(x.Id)).Select(x => new BranchOption(x.Id, x.Name)).ToArrayAsync(ct),
            members.Select(x => new EmployeeOption(x.EmployeeId, x.BranchId, employees[x.EmployeeId].FullName, true, false, false)).ToArray(), [],
            await db.Set<SchedulingService>().Where(x => branches.Contains(x.BranchId) && x.Active && x.PublicBookable && x.IsInitialExamination).Select(x => new ServiceOption(x.Id, x.BranchId, "Khám", x.PublicDurationMinutes)).ToArrayAsync(ct));
    }
    private async Task Lock(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);

    public async Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct)
    {
        if (staffActorId.HasValue) await Authorize(staffActorId.Value, query.BranchId, null, ct);
        return query.DentistId > 0
            ? await Query(query, !staffActorId.HasValue, ct)
            : await QueryAnyDentist(query, !staffActorId.HasValue, ct);
    }
    // docs/specs/CHAIR-CAPACITY-ANY-DENTIST-BOOKING.vi.md: chairs are branch capacity, not tied to a dentist. Each start time gets
    // at most one slot, assigned to the free on-shift dentist with the fewest active bookings that day (ties: lower Id).
    private async Task<AvailabilityResult> QueryAnyDentist(AvailabilityQuery q, bool isPublic, CancellationToken ct)
    {
        if (q.AssistantId.HasValue) throw Error("MSG40");
        var dentists = (await ActiveMembers(ct)).Where(x => x.BranchId == q.BranchId && x.CanDentist)
            .Select(x => x.EmployeeId).Distinct().ToArray();
        if (dentists.Length == 0) return new("Asia/Ho_Chi_Minh", q.Date, []);
        // Service/duration/date rules do not depend on the dentist, so a bad query still fails loudly.
        await ValidateQuery(q with { DentistId = dentists[0] }, isPublic, ct);
        var from = Local(q.Date).ToUniversalTime(); var to = from.AddDays(1);
        var load = await db.Appointments.AsNoTracking()
            .Where(x => x.BranchId == q.BranchId && OccupiedStatuses.Contains(x.Status) && x.StartAt < to && x.EndAt > from)
            .GroupBy(x => x.DentistId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var chosen = new Dictionary<DateTimeOffset, AvailabilitySlot>();
        foreach (var dentistId in dentists.OrderBy(id => load.GetValueOrDefault(id)).ThenBy(id => id))
        {
            AvailabilityResult result;
            try { result = await Query(q with { DentistId = dentistId }, isPublic, ct); }
            catch (BusinessException) { continue; }
            foreach (var slot in result.Slots) chosen.TryAdd(slot.Start, slot);
        }
        return new("Asia/Ho_Chi_Minh", q.Date, chosen.Values.OrderBy(x => x.Start).ToArray());
    }
    public async Task<AvailabilityResult> QueryForAppointmentAsync(AvailabilityQuery query, Guid appointmentId, int? patientId, CancellationToken ct, bool requireLeadTime = true)
    {
        await ValidateQuery(query, false, ct);
        var data = await Load(query, ct);
        var slots = new List<AvailabilitySlot>();
        foreach (var minute in CandidateMinutes(query, data))
        {
            var start = Local(query.Date, minute);
            if (!requireLeadTime && start <= clock.GetUtcNow()) continue;
            var slot = Evaluate(query, requireLeadTime, start, null, patientId, appointmentId, data);
            if (slot != null) slots.Add(slot);
        }
        return new("Asia/Ho_Chi_Minh", query.Date, slots.ToArray());
    }
    private async Task ValidateQuery(AvailabilityQuery q, bool isPublic, CancellationToken ct)
    {
        if (!ScheduleRules.ValidDuration(q.DurationMinutes, isPublic) || q.Date.Year is < 2000 or > 2100 || q.AssistantId == q.DentistId) throw Error("MSG40");
        var members = await ActiveMembers(ct);
        if (!members.Any(x => x.BranchId == q.BranchId && x.EmployeeId == q.DentistId && x.CanDentist)
            || (q.AssistantId.HasValue && !members.Any(x => x.BranchId == q.BranchId && x.EmployeeId == q.AssistantId))) throw Error("MSG40");
        var service = q.ServiceId.HasValue
            ? await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.ServiceId && x.BranchId == q.BranchId && x.Active, ct)
            : null;
        if ((q.ServiceId.HasValue && service == null) || (isPublic && (service == null || !service.IsInitialExamination || !service.PublicBookable || service.PublicDurationMinutes != q.DurationMinutes))) throw Error("MSG40");
    }
    private async Task<AvailabilityResult> Query(AvailabilityQuery q, bool isPublic, CancellationToken ct, RecurringCommand? recurringDraft = null)
    {
        await ValidateQuery(q, isPublic, ct);
        var slots = new List<AvailabilitySlot>();
        // Load resources once per query; candidate starts all share the same resource snapshot.
        var data = await Load(q, ct, recurringDraft);
        var now = clock.GetUtcNow();
        foreach (var minute in CandidateMinutes(q, data))
        {
            var start = Local(q.Date, minute);
            if (recurringDraft == null && start <= now) continue; // never offer slots that already passed
            var slot = Evaluate(q, isPublic, start, null, null, null, data);
            if (slot != null) slots.Add(slot);
        }
        return new("Asia/Ho_Chi_Minh", q.Date, slots.ToArray());
    }
    private static IEnumerable<int> CandidateMinutes(AvailabilityQuery query, Resources data)
    {
        var day = query.Date.DayNumber - Monday(query.Date).DayNumber;
        return data.MainPeriods.Where(p => p.Day == day && p.Kind == "Work")
            .SelectMany(p => Enumerable.Range(0, (p.EndMinute - p.StartMinute - 1) / data.Branch.SlotIntervalMinutes + 1)
                .Select(index => p.StartMinute + index * data.Branch.SlotIntervalMinutes))
            .Where(minute => minute + query.DurationMinutes <= 1440).Distinct().Order();
    }
    private sealed record Resources(SchedulingBranch Branch, WorkingWeek? Main, WorkingWeek? Assistant, SchedulePeriod[] MainPeriods,
        SchedulePeriod[] AssistantPeriods, SchedulingChair[] Chairs, Appointment[] Occupied);
    private async Task<Resources> Load(AvailabilityQuery q, CancellationToken ct, RecurringCommand? recurringDraft = null)
    {
        var main = await Resolve(q.BranchId, q.DentistId, q.Date, recurringDraft, ct);
        var assistant = q.AssistantId.HasValue ? await Resolve(q.BranchId, q.AssistantId.Value, q.Date, recurringDraft, ct) : null;
        var from = Local(q.Date).ToUniversalTime(); var to = from.AddDays(1);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.BranchId && x.Active, ct)
            ?? throw Error("MSG40");
        return new(branch, main, assistant,
            Periods(main),
            Periods(assistant),
            await db.SchedulingChairs.AsNoTracking().Where(x => x.BranchId == q.BranchId && x.Active).OrderBy(x => x.Id).ToArrayAsync(ct),
            await db.Appointments.AsNoTracking().Where(x => OccupiedStatuses.Contains(x.Status) && x.StartAt < to && x.EndAt > from).ToArrayAsync(ct));
    }
    private AvailabilitySlot? Evaluate(AvailabilityQuery q, bool isPublic, DateTimeOffset start, int? chairId, int? patientId, Guid? exclude, Resources data, bool throwOnConflict = false)
    {
        AvailabilitySlot? Fail(string code)
        {
            if (!throwOnConflict) return null;
            if (code == "MSG21") throw new BusinessException(409, new("MSG21", $"Không còn khoảng thời gian liên tiếp đủ {q.DurationMinutes} phút. Vui lòng chọn giờ khác."));
            throw Error(code, 409);
        }
        var end = start.AddMinutes(q.DurationMinutes); var minute = start.Hour * 60 + start.Minute; var day = q.Date.DayNumber - Monday(q.Date).DayNumber;
        if (start.Second != 0 || start.Ticks % TimeSpan.TicksPerSecond != 0 || minute + q.DurationMinutes > 1440
            || !data.MainPeriods.Any(p => p.Day == day && p.Kind == "Work" && p.StartMinute <= minute && minute < p.EndMinute
                && (minute - p.StartMinute) % data.Branch.SlotIntervalMinutes == 0)
            ) return Fail("MSG40");
        if (isPublic && start < clock.GetUtcNow().AddMinutes(60)) return Fail("MSG19");
        if (!ScheduleRules.Covers(data.MainPeriods, day, minute, minute + q.DurationMinutes)
            || (q.AssistantId.HasValue && !ScheduleRules.Covers(data.AssistantPeriods, day, minute, minute + q.DurationMinutes))) return Fail("MSG21");
        var overlaps = data.Occupied.Where(r => r.Id != exclude && r.StartAt < end && start < r.EndAt).ToArray();
        var staff = new[] { q.DentistId, q.AssistantId ?? q.DentistId };
        if (overlaps.Any(r => staff.Contains(r.DentistId) || (patientId.HasValue && r.PatientId == patientId.Value))) return Fail("MSG22");
        var chair = data.Chairs.FirstOrDefault(c => (!chairId.HasValue || c.Id == chairId) && !overlaps.Any(r => r.ChairId == c.Id));
        return chair == null ? Fail("MSG23") : new(start, end, chair.Id, q.DentistId, q.AssistantId, data.Main?.Version ?? 0, data.Assistant?.Version, data.Branch.Version);
    }
    public async Task ValidateReceptionAsync(Guid appointmentId, int patientId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Reception requires the reservation transaction.");
        await Lock(ct);
        var a = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
            ?? throw Error("MSG36", 409);
        if (a.Status != "Confirmed") throw Error("MSG36", 409);
        var start = a.StartAt.ToOffset(TimeSpan.FromHours(7));
        var q = new AvailabilityQuery(a.BranchId, a.DentistId, a.ServiceId, DateOnly.FromDateTime(start.DateTime),
            (int)(a.EndAt - a.StartAt).TotalMinutes);
        await ValidateQuery(q, false, ct);
        var data = await Load(q, ct);
        _ = Evaluate(q, false, start, a.ChairId, patientId, a.Id, data, true);
    }

    public async Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Resource checks require the caller's reservation transaction.");
        if (db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Resource checks require ReadCommitted isolation.");
        await Lock(ct);
        var start = request.Start.ToOffset(TimeSpan.FromHours(7));
        var q = new AvailabilityQuery(request.BranchId, request.DentistId, request.ServiceId, DateOnly.FromDateTime(start.DateTime), request.DurationMinutes, request.AssistantId);
        if (request.BookingSettingsVersion.HasValue && request.BookingSettingsVersion != await db.Set<SchedulingBranch>()
            .Where(x => x.Id == q.BranchId).Select(x => x.Version).SingleOrDefaultAsync(ct)) throw Error("MSG41", 409);
        await ValidateQuery(q, request.Public, ct);
        var data = await Load(q, ct);
        if (request.BookingSettingsVersion.HasValue && request.BookingSettingsVersion != data.Branch.Version) throw Error("MSG41", 409);
        if ((data.Main?.Version ?? 0) != request.ExpectedScheduleVersion || (q.AssistantId.HasValue && data.Assistant?.Version != request.ExpectedAssistantScheduleVersion)) throw Error("MSG41", 409);
        return Evaluate(q, request.Public, start, request.ChairId, request.PatientId, request.ExcludeAppointmentId, data, true)!;
    }
}
