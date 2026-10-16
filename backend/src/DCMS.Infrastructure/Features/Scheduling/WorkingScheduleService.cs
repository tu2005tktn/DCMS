using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed class WorkingScheduleService(DcmsDbContext db, TimeProvider clock) : IWorkingScheduleService, IAvailabilityService
{
    public const long TransactionLock = 90403;
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    private static DateTimeOffset Local(DateOnly date, int minute = 0) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(minute);
    private static SchedulePeriod[] Periods(WorkingWeek? week) => week?.Periods.Select(p => new SchedulePeriod(p.Day, p.StartMinute, p.EndMinute, p.Kind, p.Reason)).ToArray() ?? [];
    private static ScheduleDto Dto(WorkingWeek? week, int branchId, int employeeId, DateOnly date) => new(week?.Id, branchId, employeeId, date,
        week?.Version ?? 0, week == null ? "Unpublished" : "Published", Periods(week), week?.PublishedBy, week?.PublishedAt);
    private Task<WorkingWeek?> Week(int branchId, int employeeId, DateOnly date, CancellationToken ct) => db.Set<WorkingWeek>().AsNoTracking()
        .Include(x => x.Periods).SingleOrDefaultAsync(x => x.BranchId == branchId && x.EmployeeId == employeeId && x.WeekStart == date, ct);

    private async Task<SchedulingMember[]> ActiveMembers(CancellationToken ct) => await (
        from member in db.Set<SchedulingMember>().AsNoTracking()
        join employee in db.Employees on member.EmployeeId equals employee.Id
        join account in db.Users on employee.AccountId equals account.Id
        join branch in db.Set<SchedulingBranch>() on member.BranchId equals branch.Id
        where member.Active && branch.Active && account.AccountType == "Staff" && account.IsActive
            && (!account.LockoutEnabled || account.LockoutEnd == null || account.LockoutEnd <= DateTimeOffset.UtcNow)
        select new SchedulingMember
        {
            BranchId = member.BranchId, EmployeeId = member.EmployeeId, Active = member.Active,
            CanManageSchedules = member.CanManageSchedules,
            CanDentist = member.CanDentist && db.UserRoles.Any(ur => ur.UserId == account.Id
                && db.Roles.Any(role => role.Id == ur.RoleId && (role.Name == "Dentist" || role.Name == "ClinicOwner")))
        }).ToArrayAsync(ct);

    private async Task Authorize(int actorId, int branchId, int? employeeId, CancellationToken ct)
    {
        var own = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        var membership = (await ActiveMembers(ct)).SingleOrDefault(x => x.BranchId == branchId && x.EmployeeId == own?.Id);
        if (membership == null || (employeeId.HasValue && !membership.CanManageSchedules && (employeeId != own!.Id || !membership.CanDentist))) throw Error("MSG06", 403);
        if (employeeId.HasValue && !(await ActiveMembers(ct)).Any(x => x.BranchId == branchId && x.EmployeeId == employeeId)) throw Error("MSG06", 403);
    }
    public async Task<ScheduleOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var members = await ActiveMembers(ct);
        var own = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        var mine = members.Where(x => x.EmployeeId == own?.Id && (x.CanDentist || x.CanManageSchedules)).ToArray();
        if (mine.Length == 0) throw Error("MSG06", 403);
        var branches = mine.Select(x => x.BranchId).ToArray();
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return new(await db.Set<SchedulingBranch>().Where(x => branches.Contains(x.Id)).Select(x => new BranchOption(x.Id, x.Name)).ToArrayAsync(ct),
            members.Where(x => branches.Contains(x.BranchId)).Select(x => new EmployeeOption(x.EmployeeId, x.BranchId, employees[x.EmployeeId].FullName,
                x.CanDentist, mine.Any(m => m.BranchId == x.BranchId && m.CanManageSchedules) || x.EmployeeId == own!.Id, x.EmployeeId == own!.Id)).ToArray(),
            await db.Set<SchedulingChair>().Where(x => branches.Contains(x.BranchId) && x.Active).Select(x => new ChairOption(x.Id, x.BranchId, x.Name)).ToArrayAsync(ct),
            await db.Set<SchedulingService>().Where(x => branches.Contains(x.BranchId) && x.Active).Select(x => new ServiceOption(x.Id, x.BranchId, x.Name, x.PublicDurationMinutes)).ToArrayAsync(ct));
    }
    public async Task<ScheduleOptions> PublicOptionsAsync(int branchId, CancellationToken ct)
    {
        var members = (await ActiveMembers(ct)).Where(x => x.CanDentist && (branchId == 0 || x.BranchId == branchId)).ToArray();
        var branches = members.Select(x => x.BranchId).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        return new(await db.Set<SchedulingBranch>().Where(x => branches.Contains(x.Id)).Select(x => new BranchOption(x.Id, x.Name)).ToArrayAsync(ct),
            members.Select(x => new EmployeeOption(x.EmployeeId, x.BranchId, employees[x.EmployeeId].FullName, true, false, false)).ToArray(), [],
            await db.Set<SchedulingService>().Where(x => branches.Contains(x.BranchId) && x.Active).Select(x => new ServiceOption(x.Id, x.BranchId, x.Name, x.PublicDurationMinutes)).ToArrayAsync(ct));
    }
    public async Task<ScheduleDto> GetAsync(int actorId, int branchId, int dentistId, DateOnly weekStart, CancellationToken ct)
    {
        await Authorize(actorId, branchId, dentistId, ct);
        if (weekStart.DayOfWeek != DayOfWeek.Monday) throw Error("MSG110");
        return Dto(await Week(branchId, dentistId, weekStart, ct), branchId, dentistId, weekStart);
    }
    private async Task<WorkingWeek?> Validate(int actorId, ScheduleCommand command, CancellationToken ct)
    {
        await Authorize(actorId, command.BranchId, command.DentistId, ct);
        if (command.RequestKey == Guid.Empty || command.Intervals == null || !ScheduleRules.ValidWeek(command.WeekStart, command.Intervals)) throw Error("MSG110");
        var previous = await Week(command.BranchId, command.DentistId, command.WeekStart, ct);
        if (command.ExpectedVersion != (previous?.Version ?? 0)) throw Error("MSG111", 409);
        return previous;
    }
    private async Task<AffectedAppointment[]> Affected(ScheduleCommand command, CancellationToken ct)
    {
        var start = Local(command.WeekStart).ToUniversalTime(); var end = start.AddDays(7);
        var reservations = await db.Set<ResourceReservation>().AsNoTracking().Where(x => x.BranchId == command.BranchId
            && (x.DentistId == command.DentistId || x.AssistantId == command.DentistId) && x.Status == "Confirmed" && x.Start < end && x.End > start).ToArrayAsync(ct);
        var affected = new List<AffectedAppointment>();
        foreach (var r in reservations)
        {
            var local = r.Start.ToOffset(TimeSpan.FromHours(7)); var date = DateOnly.FromDateTime(local.DateTime);
            var q = new AvailabilityQuery(r.BranchId, r.DentistId, r.ServiceId, date, (int)(r.End - r.Start).TotalMinutes, r.AssistantId);
            var slot = await Find(q, false, local, r.ChairId, r.AttendeeKey, r.Id, command, ct);
            if (slot == null) affected.Add(new(r.Id, r.Code, r.Start, r.End, r.AttendeeLabel, r.Status));
        }
        return affected.ToArray();
    }
    public async Task<SchedulePreview> PreviewAsync(int actorId, ScheduleCommand command, CancellationToken ct)
    {
        await Validate(actorId, command, ct); return new(await Affected(command, ct));
    }
    public async Task<PublishResult> PublishAsync(int actorId, ScheduleCommand command, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        await Authorize(actorId, command.BranchId, command.DentistId, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command))));
        var receipt = await db.Set<ScheduleReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == command.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PublishResult>(receipt.ResultJson)!;
        }
        var previous = await Validate(actorId, command, ct);
        var affected = await Affected(command, ct);
        if (affected.Length > 0) throw new BusinessException(409, new("MSG38", $"Thay đổi này ảnh hưởng đến {affected.Length} lịch hẹn đã xác nhận. Vui lòng xử lý các lịch liên quan trước khi cập nhật."));
        var week = previous ?? new WorkingWeek { BranchId = command.BranchId, EmployeeId = command.DentistId, WeekStart = command.WeekStart };
        if (previous != null)
        {
            db.Attach(week); db.RemoveRange(week.Periods); week.Periods = []; week.Version++;
        }
        else db.Add(week);
        week.PublishedBy = actorId; week.PublishedAt = clock.GetUtcNow();
        var newPeriods = command.Intervals.Select(p => new WorkingPeriod { WeekId = week.Id, Day = p.Day, StartMinute = p.StartMinute, EndMinute = p.EndMinute, Kind = p.Kind, Reason = p.Reason.Trim() }).ToArray();
        week.Periods.AddRange(newPeriods);
        db.AddRange(newPeriods);
        var result = new PublishResult(Dto(week, command.BranchId, command.DentistId, command.WeekStart), SystemMessages.Get("MSG39"));
        db.Add(new ScheduleReceipt { ActorId = actorId, RequestKey = command.RequestKey, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    }
    private async Task Lock(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);

    public async Task<AvailabilityResult> QueryAsync(AvailabilityQuery query, int? staffActorId, CancellationToken ct)
    {
        if (staffActorId.HasValue) await Authorize(staffActorId.Value, query.BranchId, null, ct);
        return await Query(query, !staffActorId.HasValue, null, ct);
    }
    public async Task<AvailabilityResult> PreviewAvailabilityAsync(int actorId, PreviewAvailabilityRequest request, CancellationToken ct)
    {
        await Validate(actorId, request.Schedule, ct);
        if (request.Query.BranchId != request.Schedule.BranchId || request.Query.DentistId != request.Schedule.DentistId
            || Monday(request.Query.Date) != request.Schedule.WeekStart) throw Error("MSG110");
        return await Query(request.Query, false, request.Schedule, ct);
    }
    private async Task ValidateQuery(AvailabilityQuery q, bool isPublic, CancellationToken ct)
    {
        if (!ScheduleRules.ValidDuration(q.DurationMinutes, isPublic) || q.Date.Year is < 2000 or > 2100 || q.AssistantId == q.DentistId) throw Error("MSG110");
        var members = await ActiveMembers(ct);
        if (!members.Any(x => x.BranchId == q.BranchId && x.EmployeeId == q.DentistId && x.CanDentist)
            || (q.AssistantId.HasValue && !members.Any(x => x.BranchId == q.BranchId && x.EmployeeId == q.AssistantId))) throw Error("MSG110");
        var service = q.ServiceId.HasValue
            ? await db.Set<SchedulingService>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.ServiceId && x.BranchId == q.BranchId && x.Active, ct)
            : null;
        if ((q.ServiceId.HasValue && service == null) || (isPublic && (service == null || service.PublicDurationMinutes != q.DurationMinutes))) throw Error("MSG110");
    }
    private async Task<AvailabilityResult> Query(AvailabilityQuery q, bool isPublic, ScheduleCommand? draft, CancellationToken ct)
    {
        await ValidateQuery(q, isPublic, ct);
        var slots = new List<AvailabilitySlot>();
        // Load resources once per query; candidate starts all share the same resource snapshot.
        var data = await Load(q, draft, ct);
        for (var minute = 0; minute + q.DurationMinutes <= 1440; minute += 15)
        {
            var slot = Evaluate(q, isPublic, Local(q.Date, minute), null, null, null, data);
            if (slot != null) slots.Add(slot);
        }
        return new("Asia/Ho_Chi_Minh", q.Date, slots.ToArray());
    }
    private sealed record Resources(WorkingWeek? Main, WorkingWeek? Assistant, SchedulePeriod[] MainPeriods,
        SchedulePeriod[] AssistantPeriods, SchedulingChair[] Chairs, ResourceReservation[] Occupied);
    private async Task<Resources> Load(AvailabilityQuery q, ScheduleCommand? draft, CancellationToken ct)
    {
        var weekStart = Monday(q.Date);
        var main = await Week(q.BranchId, q.DentistId, weekStart, ct);
        var assistant = q.AssistantId.HasValue ? await Week(q.BranchId, q.AssistantId.Value, weekStart, ct) : null;
        var from = Local(q.Date).ToUniversalTime(); var to = from.AddDays(1);
        return new(main, assistant,
            draft != null && draft.DentistId == q.DentistId ? draft.Intervals : Periods(main),
            draft != null && draft.DentistId == q.AssistantId ? draft.Intervals : Periods(assistant),
            await db.Set<SchedulingChair>().AsNoTracking().Where(x => x.BranchId == q.BranchId && x.Active).OrderBy(x => x.Id).ToArrayAsync(ct),
            await db.Set<ResourceReservation>().AsNoTracking().Where(x => (x.Status == "Confirmed" || x.Status == "Checked-in") && x.Start < to && x.End > from).ToArrayAsync(ct));
    }
    private AvailabilitySlot? Evaluate(AvailabilityQuery q, bool isPublic, DateTimeOffset start, int? chairId, string? attendeeKey, Guid? exclude, Resources data, bool throwOnConflict = false)
    {
        AvailabilitySlot? Fail(string code)
        {
            if (!throwOnConflict) return null;
            if (code == "MSG26") throw new BusinessException(409, new("MSG26", $"Không còn khoảng thời gian liên tiếp đủ {q.DurationMinutes} phút. Vui lòng chọn giờ khác."));
            throw Error(code, 409);
        }
        var end = start.AddMinutes(q.DurationMinutes); var minute = start.Hour * 60 + start.Minute; var day = q.Date.DayNumber - Monday(q.Date).DayNumber;
        if (start.Second != 0 || start.Ticks % TimeSpan.TicksPerSecond != 0 || minute % 15 != 0 || minute + q.DurationMinutes > 1440
            ) return Fail("MSG110");
        if (isPublic && start < clock.GetUtcNow().AddMinutes(60)) return Fail("MSG24");
        if (!ScheduleRules.Covers(data.MainPeriods, day, minute, minute + q.DurationMinutes)
            || (q.AssistantId.HasValue && !ScheduleRules.Covers(data.AssistantPeriods, day, minute, minute + q.DurationMinutes))) return Fail("MSG26");
        var overlaps = data.Occupied.Where(r => r.Id != exclude && r.Start < end && start < r.End).ToArray();
        var staff = new[] { q.DentistId, q.AssistantId ?? q.DentistId };
        if (overlaps.Any(r => staff.Contains(r.DentistId) || (r.AssistantId.HasValue && staff.Contains(r.AssistantId.Value))
            || (!string.IsNullOrWhiteSpace(attendeeKey) && r.AttendeeKey == attendeeKey))) return Fail("MSG27");
        var chair = data.Chairs.FirstOrDefault(c => (!chairId.HasValue || c.Id == chairId) && c.OpenMinute <= minute && c.CloseMinute >= minute + q.DurationMinutes && !overlaps.Any(r => r.ChairId == c.Id));
        return chair == null ? Fail("MSG28") : new(start, end, chair.Id, q.DentistId, q.AssistantId, data.Main?.Version ?? 0, data.Assistant?.Version);
    }
    private async Task<AvailabilitySlot?> Find(AvailabilityQuery q, bool isPublic, DateTimeOffset start, int? chairId, string? attendeeKey, Guid? exclude, ScheduleCommand? draft, CancellationToken ct)
    {
        try { await ValidateQuery(q, isPublic, ct); }
        catch (BusinessException) { return null; }
        return Evaluate(q, isPublic, start, chairId, attendeeKey, exclude, await Load(q, draft, ct));
    }
    public async Task<AvailabilitySlot> CheckInTransactionAsync(ResourceCheck request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Resource checks require the caller's reservation transaction.");
        if (db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Resource checks require ReadCommitted isolation.");
        await Lock(ct);
        var start = request.Start.ToOffset(TimeSpan.FromHours(7));
        var q = new AvailabilityQuery(request.BranchId, request.DentistId, request.ServiceId, DateOnly.FromDateTime(start.DateTime), request.DurationMinutes, request.AssistantId);
        await ValidateQuery(q, request.Public, ct);
        var data = await Load(q, null, ct);
        if ((data.Main?.Version ?? 0) != request.ExpectedScheduleVersion || (q.AssistantId.HasValue && data.Assistant?.Version != request.ExpectedAssistantScheduleVersion)) throw Error("MSG111", 409);
        return Evaluate(q, request.Public, start, request.ChairId, request.AttendeeKey, request.ExcludeReservationId, data, true)!;
    }
}
