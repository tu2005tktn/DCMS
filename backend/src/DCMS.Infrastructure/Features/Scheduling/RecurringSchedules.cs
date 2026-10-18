using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Scheduling;

public sealed partial class WorkingScheduleService
{
    private DateOnly ClinicToday => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
    private Task<List<WorkingScheduleRevision>> Revisions(int employee, CancellationToken ct) =>
        db.Set<WorkingScheduleRevision>().AsNoTracking().Include(x => x.Periods)
            .Where(x => x.EmployeeId == employee).ToListAsync(ct);
    private static int Head(IEnumerable<WorkingScheduleRevision> revisions) => revisions.Select(x => x.Version).DefaultIfEmpty().Max();
    private async Task<int> HeadVersion(int employee, IEnumerable<WorkingScheduleRevision> revisions, CancellationToken ct) =>
        Math.Max(Head(revisions), await db.Set<WorkingWeek>().Where(x => x.EmployeeId == employee).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0);
    private static RevisionDto Revision(WorkingScheduleRevision r) => new(r.Id, r.EmployeeId, r.Date, r.Kind, r.Version,
        r.Periods.Select(p => new BranchPeriod(p.BranchId, p.Day, p.StartMinute, p.EndMinute, p.Kind, p.Reason)).ToArray(), r.Reason, r.PublishedBy, r.PublishedAt);
    private static WorkingScheduleRevision? Effective(IEnumerable<WorkingScheduleRevision> revisions, DateOnly date) =>
        revisions.Where(x => x.Kind == "Exception" && x.Date == date).MaxBy(x => x.Version)
        ?? revisions.Where(x => x.Kind == "Template" && x.Date <= date).OrderByDescending(x => x.Date).ThenByDescending(x => x.Version).FirstOrDefault();
    private static WorkingScheduleRevision Proposed(RecurringCommand c, int version) => new()
    {
        EmployeeId = c.DentistId, Date = c.Date, Kind = c.Kind, Version = version, Reason = c.Reason.Trim(),
        Periods = c.Intervals.Select(p => new RevisionPeriod { BranchId = p.BranchId, Day = p.Day,
            StartMinute = p.StartMinute, EndMinute = p.EndMinute, Kind = p.Kind, Reason = p.Reason.Trim() }).ToList()
    };
    private async Task AuthorizeRecurring(int actor, int employee, CancellationToken ct)
    {
        var branches = (await ActiveMembers(ct)).Where(x => x.EmployeeId == employee).Select(x => x.BranchId).ToArray();
        if (branches.Length == 0) throw Error("MSG06", 403);
        foreach (var branch in branches) await Authorize(actor, branch, employee, ct);
        // An aggregate edit must not erase work at a branch outside the editor's rights.
        var publishedBranches = await db.Set<RevisionPeriod>().Where(p => db.Set<WorkingScheduleRevision>()
            .Any(r => r.Id == p.RevisionId && r.EmployeeId == employee)).Select(p => p.BranchId)
            .Union(db.Set<WorkingWeek>().Where(w => w.EmployeeId == employee).Select(w => w.BranchId)).Distinct().ToArrayAsync(ct);
        foreach (var branch in publishedBranches) await Authorize(actor, branch, employee, ct);
    }
    public async Task<RecurringScheduleDto> GetRecurringAsync(int actorId, int dentistId, DateOnly date, CancellationToken ct)
    {
        await AuthorizeRecurring(actorId, dentistId, ct);
        if (date.Year is < 2000 or > 2100) throw Error("MSG110");
        return await RecurringDto(dentistId, date, ct);
    }
    private async Task<RecurringScheduleDto> RecurringDto(int employee, DateOnly date, CancellationToken ct)
    {
        var revisions = await Revisions(employee, ct);
        var template = revisions.Where(x => x.Kind == "Template" && x.Date <= date)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Version).FirstOrDefault();
        var exception = revisions.Where(x => x.Kind == "Exception" && x.Date == date).MaxBy(x => x.Version);
        var monday = Monday(date);
        var legacy = await db.Set<WorkingWeek>().AsNoTracking().Include(x => x.Periods)
            .Where(x => x.EmployeeId == employee && x.WeekStart == monday).ToArrayAsync(ct);
        return new(employee, await HeadVersion(employee, revisions, ct), template == null ? null : Revision(template), exception == null ? null : Revision(exception),
            revisions.Where(x => x.Date > date).GroupBy(x => new { x.Kind, x.Date }).Select(g => g.MaxBy(x => x.Version)!)
                .OrderBy(x => x.Date).Select(Revision).ToArray(),
            legacy.Select(w => Dto(w, w.BranchId, employee, monday)).ToArray());
    }
    private async Task ValidateRecurring(int actor, RecurringCommand c, CancellationToken ct)
    {
        await AuthorizeRecurring(actor, c.DentistId, ct);
        if (c.RequestKey == Guid.Empty || c.Date < ClinicToday || c.Date.Year > 2100 || c.Kind is not ("Template" or "Exception")
            || c.Intervals == null || c.Intervals.Any(p => p == null) || c.Reason == null || c.Reason.Length > 500
            || (c.Kind == "Exception" && string.IsNullOrWhiteSpace(c.Reason))) throw Error("MSG110");
        var periods = c.Intervals.Select(p => new SchedulePeriod(p.Day, p.StartMinute, p.EndMinute, p.Kind, p.Reason)).ToArray();
        if (!ScheduleRules.ValidWeek(Monday(c.Date), periods)
            || c.Intervals.Any(p => p.StartMinute % 30 != 0 || p.EndMinute % 30 != 0)
            || c.Intervals.GroupBy(p => p.Day).Any(g => g.Select(p => p.BranchId).Distinct().Count() != 1)
            || (c.Kind == "Exception" && c.Intervals.Any(p => p.Day != c.Date.DayNumber - Monday(c.Date).DayNumber))) throw Error("MSG110");
        foreach (var branch in c.Intervals.Select(p => p.BranchId).Distinct()) await Authorize(actor, branch, c.DentistId, ct);
        if (c.ExpectedVersion != await HeadVersion(c.DentistId, await Revisions(c.DentistId, ct), ct)) throw Error("MSG111", 409);
    }
    // The same resolver is used by public GET, draft preview and transaction resource checks.
    private async Task<WorkingWeek?> Resolve(int branch, int employee, DateOnly date, RecurringCommand? draft, CancellationToken ct)
    {
        var revisions = await Revisions(employee, ct);
        var head = await HeadVersion(employee, revisions, ct);
        if (draft?.DentistId == employee) revisions.Add(Proposed(draft, head + 1));
        var selected = Effective(revisions, date);
        if (selected == null)
        {
            var legacy = await Week(branch, employee, Monday(date), ct);
            if (legacy != null && revisions.Count > 0) legacy.Version = head;
            return legacy;
        }
        return new WorkingWeek { Id = selected.Id, BranchId = branch, EmployeeId = employee, WeekStart = Monday(date),
            Version = head, PublishedAt = selected.PublishedAt, PublishedBy = selected.PublishedBy,
            Periods = selected.Periods.Where(p => p.BranchId == branch).Select(p => new WorkingPeriod {
                Day = p.Day, StartMinute = p.StartMinute, EndMinute = p.EndMinute, Kind = p.Kind, Reason = p.Reason }).ToList() };
    }
    private async Task<AffectedAppointment[]> AffectedRecurring(RecurringCommand c, CancellationToken ct)
    {
        var from = Local(c.Date).ToUniversalTime();
        var reservations = await db.Set<ResourceReservation>().AsNoTracking().Where(r => r.Status == "Confirmed"
            && (r.DentistId == c.DentistId || r.AssistantId == c.DentistId) && r.End > from
            && r.End > clock.GetUtcNow()).ToArrayAsync(ct);
        var affected = new List<AffectedAppointment>();
        foreach (var r in reservations)
        {
            var local = r.Start.ToOffset(TimeSpan.FromHours(7));
            var date = DateOnly.FromDateTime(local.DateTime);
            // A date exception only changes its own date. Later templates and other exceptions still win.
            if (date < c.Date || (c.Kind == "Exception" && date != c.Date)) continue;
            var q = new AvailabilityQuery(r.BranchId, r.DentistId, r.ServiceId, date, (int)(r.End - r.Start).TotalMinutes, r.AssistantId);
            try
            {
                await ValidateQuery(q, false, ct);
                if (Evaluate(q, false, local, r.ChairId, r.AttendeeKey, r.Id, await Load(q, null, ct, c)) != null) continue;
            }
            catch (BusinessException) { }
            affected.Add(new(r.Id, r.Code, r.Start, r.End, r.AttendeeLabel, r.Status));
        }
        return affected.ToArray();
    }
    public async Task<SchedulePreview> PreviewRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct)
    {
        await ValidateRecurring(actorId, command, ct);
        return new(await AffectedRecurring(command, ct));
    }
    public async Task<RecurringPublishResult> PublishRecurringAsync(int actorId, RecurringCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        await AuthorizeRecurring(actorId, command.DentistId, ct);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("recurring:" + JsonSerializer.Serialize(command))));
        var receipt = await db.Set<ScheduleReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actorId && x.RequestKey == command.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<RecurringPublishResult>(receipt.ResultJson)!;
        }
        await ValidateRecurring(actorId, command, ct);
        if ((await AffectedRecurring(command, ct)).Length > 0) throw Error("MSG38", 409);
        var revision = Proposed(command, command.ExpectedVersion + 1);
        revision.PublishedBy = actorId; revision.PublishedAt = clock.GetUtcNow();
        db.Add(revision);
        await db.SaveChangesAsync(ct);
        var result = new RecurringPublishResult(await RecurringDto(command.DentistId, command.Date, ct), SystemMessages.Get("MSG39"));
        db.Add(new ScheduleReceipt { ActorId = actorId, RequestKey = command.RequestKey, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
    public async Task<RecurringAvailabilityResult> PreviewRecurringAvailabilityAsync(int actorId, RecurringPreviewRequest request, CancellationToken ct)
    {
        await ValidateRecurring(actorId, request.Schedule, ct);
        await Authorize(actorId, request.BranchId, request.Schedule.DentistId, ct);
        if (request.From.Year < 2000 || request.To.Year > 2100 || request.To < request.From || request.To.DayNumber - request.From.DayNumber > 30) throw Error("MSG110");
        var result = new List<AvailabilityResult>();
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
            result.Add(await Query(new(request.BranchId, request.Schedule.DentistId, request.ServiceId, date, request.DurationMinutes, request.AssistantId), false, null, ct, request.Schedule));
        return new(result.ToArray());
    }
}
