using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Reception;

public sealed class ReceptionSourceReader(DcmsDbContext db) : IReceptionSourceReader
{
    public async Task<IReadOnlyList<ReceptionSource>> ReadAsync(int branchId, int patientId, CancellationToken ct) =>
        await db.Set<ClinicalSourceReference>().AsNoTracking().Where(x => x.BranchId == branchId && x.PatientId == patientId)
            .OrderBy(x => x.Kind).ThenBy(x => x.Label)
            .Select(x => new ReceptionSource(x.Id, x.Kind, x.ParentId, x.Label, x.Blocker, x.Version)).ToArrayAsync(ct);
}

public sealed class ReceptionService(DcmsDbContext db, UserManager<ApplicationUser> users,
    IAppointmentQueryService appointments, IAvailabilityService availability, IReceptionSourceReader sources,
    TimeProvider clock) : IReceptionService
{
    private static readonly TimeZoneInfo ClinicZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly string[] AppointmentStatuses = ["Confirmed", "CheckedIn", "InProgress", "Completed", "Cancelled", "NoShow"];
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateOnly Date(DateTimeOffset value) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, ClinicZone).DateTime);
    private static DateTimeOffset Midnight(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ClinicZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
    private static string Fingerprint(object command) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command))));
    private sealed record Scope(int EmployeeId, bool Assistant, bool Dentist);

    private async Task<Scope> ActorAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.AccountType != "Staff") throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        var assistant = roles.Contains(AccountRoles.ClinicAssistant);
        var dentist = roles.Contains(AccountRoles.Dentist);
        if (!assistant && !dentist) throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG06", 403);
        return new(employee.Id, assistant, dentist);
    }

    private async Task<Scope> ScopeAsync(int actorId, int branchId, bool write, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        if (write && !actor.Assistant) throw Error("MSG06", 403);
        if (!await (from m in db.Set<SchedulingMember>() join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == actor.EmployeeId && m.BranchId == branchId && m.Active && b.Active
                && (actor.Assistant || m.CanDentist) select m).AnyAsync(ct)) throw Error("MSG06", 403);
        return actor;
    }

    private static void Own(Scope actor, int dentistId)
    {
        if (!actor.Assistant && actor.EmployeeId != dentistId) throw Error("MSG06", 403);
    }

    public async Task<ReceptionOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        var options = await appointments.OptionsAsync(actorId, ct);
        // Scheduling managers have a broader calendar grant; reception dentists remain assigned-only.
        if (!actor.Assistant) options = options with { Dentists = options.Dentists.Where(x => x.Id == actor.EmployeeId).ToArray(), OwnScheduleOnly = true };
        return new(options, actor.Assistant, actor.Assistant, actor.Dentist);
    }

    public async Task<ReceptionView> ReadAsync(int actorId, ReceptionQuery query, CancellationToken ct)
    {
        if (query.Date.Year is < 2000 or > 2100 || query.Keyword?.Length > 100
            || query.AppointmentStatus != null && !AppointmentStatuses.Contains(query.AppointmentStatus)
            || query.VisitStatus != null && !VisitStatuses.All.Contains(query.VisitStatus)) throw Error("MSG04");
        var actor = await ScopeAsync(actorId, query.BranchId, false, ct);
        var dentist = actor.Assistant ? query.DentistId : actor.EmployeeId;
        var from = Midnight(query.Date); var until = Midnight(query.Date.AddDays(1));
        var rows = await db.Appointments.AsNoTracking().Where(x => x.BranchId == query.BranchId && x.StartAt >= from && x.StartAt < until
            && (!dentist.HasValue || x.DentistId == dentist)).OrderBy(x => x.StartAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var visits = await db.Set<Visit>().AsNoTracking().Where(x => ids.Contains(x.AppointmentId)).OrderBy(x => x.QueueOrder)
            .ThenBy(x => x.ActualArrivalAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var patientIds = rows.Where(x => x.PatientId.HasValue).Select(x => x.PatientId!.Value).Concat(visits.Select(x => x.PatientId)).Distinct().ToArray();
        var patients = await db.Patients.AsNoTracking().Where(x => patientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var names = await NamesAsync(rows.Select(x => x.DentistId), ct);
        var keyword = PatientLinkSupport.NormalizeName(query.Keyword ?? "");
        bool Matches(Appointment a) => keyword.Length == 0 || PatientLinkSupport.NormalizeName(a.AttendeeName).Contains(keyword)
            || PatientLinkSupport.NormalizeName(a.BookerName).Contains(keyword) || a.Code.ToUpperInvariant().Contains(keyword)
            || a.PatientId.HasValue && patients.TryGetValue(a.PatientId.Value, out var p)
                && (PatientLinkSupport.NormalizeName(p.FullName).Contains(keyword) || p.PatientCode.ToUpperInvariant().Contains(keyword));
        var received = visits.Select(x => x.AppointmentId).ToHashSet();
        var notArrived = rows.Where(x => !received.Contains(x.Id) && (query.AppointmentStatus == null || x.Status == query.AppointmentStatus) && Matches(x))
            .Select(x => AppointmentDto(x, names, patients)).ToArray();
        var queue = new List<VisitResult>();
        var position = 0;
        foreach (var visit in visits)
        {
            if (visit.Status == VisitStatuses.Queued) position++;
            var a = rows.Single(x => x.Id == visit.AppointmentId);
            if ((query.VisitStatus == null || visit.Status == query.VisitStatus) && Matches(a))
                queue.Add(await ResultAsync(visit, a, visit.Status == VisitStatuses.Queued ? position : 0, ct));
        }
        return new(query.Date, "Asia/Ho_Chi_Minh", clock.GetUtcNow(), notArrived, queue);
    }

    private async Task<Appointment> AppointmentAsync(string locator, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locator) || locator.Length > 40) throw Error("MSG04");
        Guid.TryParse(locator, out var id);
        return await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id || x.Code == locator, ct) ?? throw Error("MSG113", 404);
    }

    public async Task<ReceptionDetail> DetailAsync(int actorId, string appointment, CancellationToken ct)
    {
        var a = await AppointmentAsync(appointment, ct);
        Own(await ScopeAsync(actorId, a.BranchId, false, ct), a.DentistId);
        var names = await NamesAsync([a.DentistId], ct);
        var patients = await db.Patients.AsNoTracking().Where(x => x.Id == a.PatientId).ToDictionaryAsync(x => x.Id, ct);
        var v = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == a.Id, ct);
        return new(AppointmentDto(a, names, patients), v == null ? null : await ResultAsync(v, a, null, ct));
    }

    public async Task<IReadOnlyList<ReceptionSource>> SourcesAsync(int actorId, int branchId, int patientId, CancellationToken ct)
    {
        var actor = await ScopeAsync(actorId, branchId, false, ct);
        if (!actor.Assistant && !await db.Appointments.AnyAsync(x => x.BranchId == branchId && x.PatientId == patientId && x.DentistId == actor.EmployeeId, ct))
            throw Error("MSG06", 403);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId && !x.IsArchived, ct)) throw Error("MSG202");
        return await sources.ReadAsync(branchId, patientId, ct);
    }

    public async Task<VisitResult> VisitAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        var v = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId, ct) ?? throw Error("MSG113", 404);
        Own(await ScopeAsync(actorId, v.BranchId, false, ct), v.DentistId);
        var a = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == v.AppointmentId, ct);
        return await ResultAsync(v, a, null, ct);
    }

    private async Task LockAsync(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);

    private async Task<VisitResult?> ReplayAsync(int actorId, Guid key, string fingerprint, CancellationToken ct)
    {
        var receipt = await db.Set<ReceptionCommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorAccountId == actorId && x.RequestKey == key, ct);
        if (receipt == null) return null;
        if (receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
        return await VisitAsync(actorId, receipt.VisitId, ct);
    }

    private void Receipt(int actorId, Guid key, string fingerprint, Guid visitId) => db.Add(new ReceptionCommandReceipt
        { ActorAccountId = actorId, RequestKey = key, Fingerprint = fingerprint, VisitId = visitId });

    public async Task<VisitResult> CheckInAsync(int actorId, CheckInRequest request, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty || request.ExpectedVersion < 1 || request.PatientId <= 0 || !request.IdentityVerified
            || Clean(request.IdentityNote) == null || request.IdentityNote?.Length > 500 || request.Note?.Length > 1000
            || request.AdditionalConcern?.Length > 1000 || request.ArrivalReason?.Length > 500
            || !AttendancePurposes.All.Contains(request.Purpose) || request.ReviewReason is not (null or "Postoperative" or "Warranty")) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        await ScopeAsync(actorId, request.BranchId, true, ct);
        var fingerprint = Fingerprint(new { action = "CheckIn", request });
        if (await ReplayAsync(actorId, request.RequestKey, fingerprint, ct) is { } replay) return replay;
        var found = await AppointmentAsync(request.Appointment, ct);
        if (found.BranchId != request.BranchId) throw Error("MSG06", 403);
        var a = await db.Appointments.FromSqlInterpolated($"SELECT * FROM \"APPOINTMENT\" WHERE \"Id\" = {found.Id} FOR UPDATE").SingleAsync(ct);
        var existing = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == a.Id, ct);
        if (existing != null)
        {
            if (existing.PatientId != request.PatientId) throw Error("MSG43", 409);
            Receipt(actorId, request.RequestKey, fingerprint, existing.Id);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await VisitAsync(actorId, existing.Id, ct);
        }
        var now = clock.GetUtcNow();
        if (a.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (a.Status != "Confirmed") throw Error(a.Status == "NoShow" ? "MSG47" : "MSG42", 409);
        if (Date(a.StartAt) != Date(now) || a.EndAt <= now) throw Error("MSG42", 409);
        var patient = await db.Patients.FromSqlInterpolated($"SELECT * FROM \"PATIENT_PROFILE\" WHERE \"Id\" = {request.PatientId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw Error("MSG113", 404);
        if (patient.IsArchived) throw Error("MSG202", 409);
        if (a.PatientId.HasValue && a.PatientId != patient.Id) throw Error("MSG43", 409);
        var arrival = request.ActualArrivalAt?.ToUniversalTime() ?? now;
        if (arrival > now || Date(arrival) != Date(now)) throw Error("MSG04");
        if (request.ActualArrivalAt.HasValue && arrival < now && Clean(request.ArrivalReason) == null) throw Error("MSG02");
        var context = request.Source ?? new VisitSource();
        // Lock published source rows against concurrent ownership/hold changes until reception commits.
        var sourceIds = SourceIds(context);
        if (sourceIds.Length > 0) await db.Set<ClinicalSourceReference>()
            .FromSqlInterpolated($"SELECT * FROM \"CLINICAL_SOURCE_REFERENCE\" WHERE \"Id\" = ANY({sourceIds}) FOR SHARE").AsNoTracking().ToArrayAsync(ct);
        ValidateSources(context, await sources.ReadAsync(a.BranchId, patient.Id, ct));
        var reservation = await db.Set<ResourceReservation>().SingleOrDefaultAsync(x => x.Id == a.ReservationId, ct) ?? throw Error("MSG47", 409);
        if (reservation.Status != "Confirmed" || reservation.BranchId != a.BranchId || reservation.DentistId != a.DentistId
            || reservation.AssistantId != a.AssistantId || reservation.ChairId != a.ChairId || reservation.Start != a.StartAt || reservation.End != a.EndAt)
            throw Error("MSG47", 409);
        await availability.ValidateReceptionAsync(a.ReservationId, patient.Id, ct);
        var visit = new Visit
        {
            AppointmentId = a.Id, BranchId = a.BranchId, PatientId = patient.Id, DentistId = a.DentistId,
            Purpose = request.Purpose, DossierId = context.DossierId, ItemId = context.ItemId, StepId = context.StepId, ActivityId = context.ActivityId,
            ReviewReason = request.ReviewReason, AdditionalConcern = Clean(request.AdditionalConcern), Note = Clean(request.Note),
            IdentityNote = request.IdentityNote!.Trim(), ActualArrivalAt = arrival, EnteredAt = now, EnteredBy = actorId,
            ArrivalReason = Clean(request.ArrivalReason), QueueOrder = a.StartAt.ToUnixTimeMilliseconds()
        };
        visit.Code = "VS-" + visit.Id.ToString("N").ToUpperInvariant();
        var before = JsonSerializer.Serialize(new { a.PatientId, a.Status, a.Version, a.AttendeeName, a.AttendeeBirthDate });
        a.PatientId = patient.Id; a.Status = "CheckedIn"; a.Version++;
        reservation.AttendeeKey = $"patient:{patient.Id}"; reservation.AttendeeLabel = patient.FullName; reservation.Status = "Checked-in";
        db.Add(visit);
        Audit(visit, actorId, "CheckIn", visit.IdentityNote, before, null);
        Receipt(actorId, request.RequestKey, fingerprint, visit.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await VisitAsync(actorId, visit.Id, ct);
    }

    private static Guid[] SourceIds(VisitSource source) => new[] { source.DossierId, source.ItemId, source.StepId, source.ActivityId }
        .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
    private static VisitSource Source(Visit visit) => new(visit.DossierId, visit.ItemId, visit.StepId, visit.ActivityId);

    private static void ValidateSources(VisitSource source, IReadOnlyList<ReceptionSource> choices)
    {
        var selected = new[] { (source.DossierId, "Dossier"), (source.ItemId, "Item"), (source.StepId, "Step"), (source.ActivityId, "Activity") };
        foreach (var (id, kind) in selected.Where(x => x.Item1.HasValue))
        {
            var node = choices.SingleOrDefault(x => x.Id == id && x.Kind == kind) ?? throw Error("MSG237");
            var seen = new HashSet<Guid> { node.Id };
            while (node.ParentId is { } parentId)
            {
                node = choices.SingleOrDefault(x => x.Id == parentId) ?? throw Error("MSG237");
                if (!seen.Add(node.Id)) throw Error("MSG237");
            }
        }
        bool Ancestor(Guid child, Guid parent)
        {
            var node = choices.Single(x => x.Id == child);
            while (node.ParentId is { } id) { if (id == parent) return true; node = choices.Single(x => x.Id == id); }
            return false;
        }
        var ordered = selected.Where(x => x.Item1.HasValue).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            for (var j = i + 1; j < ordered.Length; j++)
                if (!Ancestor(ordered[j].Item1!.Value, ordered[i].Item1!.Value)) throw Error("MSG237");
    }

    public async Task<VisitResult> ReorderAsync(int actorId, Guid visitId, ReorderVisitRequest request, CancellationToken ct)
    {
        ValidateCommand(request.RequestKey, request.ExpectedVersion, request.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var visit = await LockedVisitAsync(actorId, visitId, true, ct);
        var fingerprint = Fingerprint(new { action = "Reorder", visitId, request });
        if (await ReplayAsync(actorId, request.RequestKey, fingerprint, ct) is { } replay) return replay;
        if (visit.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (visit.Status != VisitStatuses.Queued || request.BeforeVisitId == visitId) throw Error("MSG04");
        var appointment = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id == visit.AppointmentId, ct);
        var since = Midnight(Date(appointment.StartAt)); var until = since.AddDays(1);
        var queue = await (from v in db.Set<Visit>() join a in db.Appointments on v.AppointmentId equals a.Id
            where v.BranchId == visit.BranchId && v.Status == VisitStatuses.Queued && a.StartAt >= since && a.StartAt < until
            orderby v.QueueOrder, v.ActualArrivalAt, v.Id select v).ToListAsync(ct);
        var before = JsonSerializer.Serialize(queue.Select(x => new { x.Id, x.QueueOrder }));
        queue.RemoveAll(x => x.Id == visit.Id);
        var index = request.BeforeVisitId.HasValue ? queue.FindIndex(x => x.Id == request.BeforeVisitId) : queue.Count;
        if (index < 0) throw Error("MSG04");
        queue.Insert(index, visit);
        for (var i = 0; i < queue.Count; i++)
        {
            var order = since.ToUnixTimeMilliseconds() + (i + 1) * 1024L;
            if (queue[i].QueueOrder != order) { queue[i].QueueOrder = order; queue[i].Version++; }
        }
        Audit(visit, actorId, "Reorder", request.Reason!.Trim(), before, null,
            JsonSerializer.Serialize(queue.Select(x => new { x.Id, x.QueueOrder, x.Version })));
        Receipt(actorId, request.RequestKey, fingerprint, visitId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await VisitAsync(actorId, visit.Id, ct);
    }

    public async Task<VisitResult> PriorityAsync(int actorId, Guid visitId, VisitPriorityRequest request, CancellationToken ct)
    {
        ValidateCommand(request.RequestKey, request.ExpectedVersion, request.Reason);
        if (request.Priority is not ("Urgent" or "Routine")) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(ct);
        var visit = await LockedVisitAsync(actorId, visitId, false, ct);
        var actor = await ScopeAsync(actorId, visit.BranchId, false, ct);
        if (!actor.Dentist || actor.EmployeeId != visit.DentistId) throw Error("MSG06", 403);
        var fingerprint = Fingerprint(new { action = "Priority", visitId, request });
        if (await ReplayAsync(actorId, request.RequestKey, fingerprint, ct) is { } replay) return replay;
        if (visit.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (visit.Status != VisitStatuses.Queued) throw Error("MSG04");
        var before = JsonSerializer.Serialize(new { visit.ClinicalPriority, visit.PriorityDentistId });
        visit.ClinicalPriority = request.Priority; visit.PriorityDentistId = actor.EmployeeId; visit.Version++;
        Audit(visit, actorId, "ClinicalPriority", request.Reason!.Trim(), before, actor.EmployeeId);
        Receipt(actorId, request.RequestKey, fingerprint, visitId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await VisitAsync(actorId, visitId, ct);
    }

    private static void ValidateCommand(Guid key, int version, string? reason)
    {
        if (key == Guid.Empty || version < 1) throw Error("MSG04");
        if (Clean(reason) == null) throw Error("MSG02");
        if (reason!.Length > 500) throw Error("MSG03");
    }

    private async Task<Visit> LockedVisitAsync(int actorId, Guid visitId, bool write, CancellationToken ct)
    {
        var visit = await db.Set<Visit>().FromSqlInterpolated($"SELECT * FROM \"VISIT\" WHERE \"Id\" = {visitId} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw Error("MSG113", 404);
        Own(await ScopeAsync(actorId, visit.BranchId, write, ct), visit.DentistId);
        return visit;
    }

    private void Audit(Visit visit, int actorId, string action, string reason, string before, int? dentistId, string? after = null) => db.Add(new VisitAudit
    {
        VisitId = visit.Id, ActorAccountId = actorId, Action = action, Reason = reason, BeforeJson = before,
        AfterJson = after ?? JsonSerializer.Serialize(visit), DecidingDentistId = dentistId, OccurredAt = clock.GetUtcNow()
    });

    public async Task<IReadOnlyList<VisitHistory>> HistoryAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        await VisitAsync(actorId, visitId, ct);
        return await (from h in db.Set<VisitAudit>().AsNoTracking() join u in db.Users on h.ActorAccountId equals u.Id
            where h.VisitId == visitId orderby h.OccurredAt, h.Id
            select new VisitHistory(h.Id, h.Action, h.ActorAccountId, u.FullName, h.DecidingDentistId, h.Reason, h.OccurredAt, h.BeforeJson, h.AfterJson)).ToArrayAsync(ct);
    }

    private Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var selected = ids.Distinct().ToArray();
        return db.Employees.AsNoTracking().Where(x => selected.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
    }

    private ReceptionAppointment AppointmentDto(Appointment a, Dictionary<int, string> names, Dictionary<int, PatientProfile> patients) =>
        new(a.Id, a.Code, a.BranchId, a.StartAt, a.EndAt, a.Status, a.Version, a.DentistId, names.GetValueOrDefault(a.DentistId, ""), a.PatientId,
            a.PatientId.HasValue ? patients.GetValueOrDefault(a.PatientId.Value)?.PatientCode : null,
            a.BookerName, a.BookerPhone, a.BookerEmail, a.AttendeeName, a.AttendeeBirthDate, a.AttendeeRelation,
            a.Status == "Confirmed" && clock.GetUtcNow() >= a.StartAt.AddMinutes(15));

    private async Task<VisitResult> ResultAsync(Visit v, Appointment a, int? position, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().SingleAsync(x => x.Id == v.PatientId, ct);
        var names = await NamesAsync([v.DentistId], ct);
        var context = Source(v);
        var choices = await sources.ReadAsync(v.BranchId, v.PatientId, ct);
        var selected = SourceIds(context).ToHashSet();
        // Include ancestor blockers even when the UI selected only a step or activity.
        foreach (var id in selected.ToArray())
        {
            var node = choices.SingleOrDefault(x => x.Id == id);
            while (node?.ParentId is { } parent && selected.Add(parent)) node = choices.SingleOrDefault(x => x.Id == parent);
        }
        var blockers = choices.Where(x => selected.Contains(x.Id) && x.Blocker != null).Select(x => x.Blocker!).Distinct().ToList();
        var pending = selected.Any(id => choices.All(x => x.Id != id))
            || v.Purpose == AttendancePurposes.TreatmentContinuation && (!v.DossierId.HasValue || !v.ItemId.HasValue || !v.StepId.HasValue)
            || v.Purpose == AttendancePurposes.PostoperativeOrWarrantyReview && ((!v.ItemId.HasValue && !v.ActivityId.HasValue) || v.ReviewReason == null);
        if (pending) blockers.Add(SystemMessages.Get("MSG237").Message);
        if (!position.HasValue)
        {
            var since = Midnight(Date(a.StartAt)); var until = since.AddDays(1);
            var ordered = await (from row in db.Set<Visit>().AsNoTracking() join ap in db.Appointments on row.AppointmentId equals ap.Id
                where row.BranchId == v.BranchId && row.Status == VisitStatuses.Queued && ap.StartAt >= since && ap.StartAt < until
                orderby row.QueueOrder, row.ActualArrivalAt, row.Id select row.Id).ToArrayAsync(ct);
            position = Array.IndexOf(ordered, v.Id) + 1;
        }
        var minutes = (int)Math.Max(0, (clock.GetUtcNow() - v.ActualArrivalAt).TotalMinutes);
        return new(a.Id, a.Code, a.Status, a.Version, v.Id, v.Code, v.Status, v.Version, v.BranchId, v.PatientId, patient.PatientCode,
            patient.FullName, v.DentistId, names.GetValueOrDefault(v.DentistId, ""), a.StartAt, v.ActualArrivalAt, v.EnteredAt,
            v.Purpose, context, pending, blockers, new(position.Value, v.QueueOrder, minutes), v.ClinicalPriority, v.PriorityDentistId,
            v.AdditionalConcern, v.ReviewReason, v.Note, v.ArrivalReason, choices.Where(x => selected.Contains(x.Id)).ToArray());
    }
}
