using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Scheduling;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Reception;

public sealed class UnavailableReceptionSourceReader : IReceptionSourceReader
{
    public Task<IReadOnlyList<ReceptionCourse>> CoursesAsync(int branchId, int patientId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReceptionCourse>>([]);
    public Task<VisitSource> ResolveAsync(int branchId, int patientId, Guid priorCourseId, CancellationToken ct) => throw new BusinessException(503, SystemMessages.Get("MSG144"));
}

public sealed class ReceptionService(DcmsDbContext db, UserManager<ApplicationUser> users,
    IAppointmentQueryService appointments, IAvailabilityService availability, IReceptionSourceReader sources,
    TimeProvider clock) : IReceptionService
{
    private static readonly string[] AppointmentStatuses = ["Confirmed", "CheckedIn", "InProgress", "Completed", "Cancelled", "NoShow"];
    private static readonly TimeSpan ClinicOffset = TimeSpan.FromHours(7);
    private static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateOnly ClinicDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(ClinicOffset).DateTime);
    private static DateTimeOffset Midnight(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ClinicOffset).ToUniversalTime();
    private sealed record Actor(int EmployeeId, bool Assistant, bool CanBackdate);

    private async Task<Actor> ActorAsync(int actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user == null || !user.IsActive || user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow()) throw Error("MSG07", 401);
        if (user.AccountType != "Staff") throw Error("MSG06", 403);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        var assistant = roles.Contains(AccountRoles.ClinicAssistant);
        if (!assistant && !roles.Contains(AccountRoles.Dentist)) throw Error("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Error("MSG06", 403);
        var backdate = assistant && (await users.GetClaimsAsync(user)).Any(x => x.Type == "permission" && x.Value == "reception.backdate");
        return new(employee.Id, assistant, backdate);
    }

    private async Task<Actor> ScopeAsync(int actorId, int branchId, bool write, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        if (write && !actor.Assistant) throw Error("MSG06", 403);
        if (!await (from m in db.Set<SchedulingMember>() join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == actor.EmployeeId && m.BranchId == branchId && m.Active && b.Active && (actor.Assistant || m.CanDentist)
            select m).AnyAsync(ct)) throw Error("MSG06", 403);
        return actor;
    }
    private static void Own(Actor actor, int dentistId)
    {
        if (!actor.Assistant && actor.EmployeeId != dentistId) throw Error("MSG06", 403);
    }

    public async Task<ReceptionOptions> OptionsAsync(int actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        var options = await appointments.OptionsAsync(actorId, ct);
        var branchIds = await (from m in db.Set<SchedulingMember>() join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == actor.EmployeeId && m.Active && b.Active && (actor.Assistant || m.CanDentist) select m.BranchId).ToArrayAsync(ct);
        return new(options with
        {
            Branches = options.Branches.Where(x => branchIds.Contains(x.Id)).ToArray(),
            Dentists = options.Dentists.Where(x => branchIds.Contains(x.BranchId) && (actor.Assistant || x.Id == actor.EmployeeId)).ToArray(),
            Chairs = options.Chairs.Where(x => branchIds.Contains(x.BranchId)).ToArray(),
            Services = options.Services.Where(x => branchIds.Contains(x.BranchId)).ToArray(), OwnScheduleOnly = !actor.Assistant
        }, actor.Assistant, ClinicDate(clock.GetUtcNow()), clock.GetUtcNow());
    }

    private IQueryable<Appointment> Query(ReceptionQuery q, Actor actor, bool queue)
    {
        if (q.Date.Year is < 2000 or > 2100 || q.Page < 1 || q.Page > 100000 || !new[] { 20, 50, 100 }.Contains(q.PageSize)
            || q.Keyword?.Length > 100 || q.DentistId is <= 0 || q.Status != null && !(queue ? VisitStatuses.All : AppointmentStatuses).Contains(q.Status)) throw Error("MSG04");
        var from = Midnight(q.Date); var to = Midnight(q.Date.AddDays(1));
        var rows = db.Appointments.AsNoTracking().Where(x => x.BranchId == q.BranchId && x.StartAt >= from && x.StartAt < to);
        var dentist = actor.Assistant ? q.DentistId : actor.EmployeeId;
        if (dentist.HasValue) rows = rows.Where(x => x.DentistId == dentist);
        if (!queue && q.Status != null) rows = rows.Where(x => x.Status == q.Status);
        var keyword = Clean(q.Keyword);
        if (keyword != null)
        {
            var pattern = $"%{keyword}%";
            rows = rows.Where(x => EF.Functions.ILike(x.AttendeeName, pattern) || EF.Functions.ILike(x.Code, pattern)
                || db.Patients.Any(p => p.Id == x.PatientId && (EF.Functions.ILike(p.FullName, pattern) || EF.Functions.ILike(p.PatientCode, pattern))));
        }
        return rows;
    }

    public async Task<ReceptionPage<ReceptionAppointment>> AppointmentsAsync(int actorId, ReceptionQuery query, CancellationToken ct)
    {
        var actor = await ScopeAsync(actorId, query.BranchId, false, ct);
        var rows = Query(query, actor, false); var total = await rows.CountAsync(ct);
        var page = await rows.OrderBy(x => x.StartAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var items = await AppointmentDtosAsync(page, actor.Assistant, ct);
        return new(items, total, query.Page, query.PageSize, ClinicDate(clock.GetUtcNow()), clock.GetUtcNow());
    }
    private static string? Ineligible(Appointment a, DateTimeOffset now) => a.Status != "Confirmed" ? "Lịch không còn ở trạng thái đã xác nhận."
        : ClinicDate(a.StartAt) != ClinicDate(now) ? "Chỉ tiếp nhận lịch trong ngày hiện tại của phòng khám."
        : a.EndAt <= now ? "Khoảng đặt lịch đã kết thúc. Vui lòng xếp lại lịch hợp lệ trước khi tiếp nhận." : null;

    private async Task<ReceptionAppointment[]> AppointmentDtosAsync(Appointment[] rows, bool assistant, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Id).ToArray(); var patientsIds = rows.Where(x => x.PatientId.HasValue).Select(x => x.PatientId!.Value).ToArray();
        var dentistIds = rows.Select(x => x.DentistId).ToArray(); var branchIds = rows.Select(x => x.BranchId).ToArray(); var chairIds = rows.Select(x => x.ChairId).ToArray();
        var patients = await db.Patients.AsNoTracking().Where(x => patientsIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var dentists = await db.Employees.AsNoTracking().Where(x => dentistIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var chairs = await db.Set<SchedulingChair>().AsNoTracking().Where(x => chairIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var visits = await db.Visits.AsNoTracking().Where(x => ids.Contains(x.AppointmentId)).ToDictionaryAsync(x => x.AppointmentId, x => x.Id, ct);
        var now = clock.GetUtcNow();
        return rows.Select(a =>
        {
            var patient = a.PatientId.HasValue ? patients.GetValueOrDefault(a.PatientId.Value) : null;
            Guid? visitId = visits.TryGetValue(a.Id, out var id) ? id : null; var reason = Ineligible(a, now);
            return new ReceptionAppointment(a.Id, a.Code, a.BranchId, branches[a.BranchId], a.DentistId, dentists[a.DentistId], a.ChairId,
                chairs[a.ChairId], a.AssistantId, a.StartAt, a.EndAt, a.Status, a.Version, a.PatientId, patient?.PatientCode, patient?.FullName,
                a.BookerName, a.BookerPhone, a.BookerEmail, a.AttendeeName, a.AttendeeBirthDate, a.AttendeeRelation, visitId,
                assistant && visitId == null && reason == null, reason);
        }).ToArray();
    }

    public async Task<ReceptionDetail> DetailAsync(int actorId, Guid appointmentId, CancellationToken ct)
    {
        var a = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appointmentId, ct) ?? throw Error("MSG113", 404);
        var actor = await ScopeAsync(actorId, a.BranchId, false, ct); Own(actor, a.DentistId);
        var dto = (await AppointmentDtosAsync([a], actor.Assistant, ct))[0];
        return new(dto, dto.VisitId.HasValue ? await VisitAsync(actorId, dto.VisitId.Value, ct) : null,
            ClinicDate(clock.GetUtcNow()), clock.GetUtcNow(), actor.CanBackdate);
    }
    public async Task<IReadOnlyList<ReceptionCourse>> CoursesAsync(int actorId, Guid appointmentId, int patientId, CancellationToken ct)
    {
        var detail = await DetailAsync(actorId, appointmentId, ct);
        if (patientId <= 0 || detail.Appointment.PatientId.HasValue && detail.Appointment.PatientId != patientId) throw Error("MSG43", 409);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == patientId && !x.IsArchived, ct) ?? throw Error("MSG113", 404);
        if (!detail.Appointment.PatientId.HasValue && (PatientLinkSupport.NormalizeName(detail.Appointment.AttendeeName) != PatientLinkSupport.NormalizeName(patient.FullName)
            || detail.Appointment.AttendeeBirthDate.HasValue && detail.Appointment.AttendeeBirthDate != patient.BirthDate)) throw Error("MSG43", 409);
        return await sources.CoursesAsync(detail.Appointment.BranchId, patientId, ct);
    }
    public async Task<ReceptionPage<VisitResult>> QueueAsync(int actorId, ReceptionQuery query, CancellationToken ct)
    {
        var actor = await ScopeAsync(actorId, query.BranchId, false, ct);
        var appointmentsQuery = Query(query, actor, true);
        var rows = db.Visits.AsNoTracking().Where(v => appointmentsQuery.Any(a => a.Id == v.AppointmentId));
        if (query.Status != null) rows = rows.Where(x => x.Status == query.Status);
        var total = await rows.CountAsync(ct);
        var page = await rows.OrderBy(x => x.ArrivalAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var items = await ResultsAsync(page, false, ct);
        return new(items, total, query.Page, query.PageSize, ClinicDate(clock.GetUtcNow()), clock.GetUtcNow());
    }
    public async Task<VisitResult> VisitAsync(int actorId, Guid visitId, CancellationToken ct)
    {
        var v = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId, ct) ?? throw Error("MSG113", 404);
        Own(await ScopeAsync(actorId, v.BranchId, false, ct), v.DentistId);
        return await ResultAsync(v, false, ct);
    }
    private async Task<VisitResult> ResultAsync(Visit v, bool replayed, CancellationToken ct) => (await ResultsAsync([v], replayed, ct))[0];
    private async Task<VisitResult[]> ResultsAsync(Visit[] visits, bool replayed, CancellationToken ct)
    {
        var ids = visits.Select(x => x.AppointmentId).ToArray(); var patientIds = visits.Select(x => x.PatientId).ToArray();
        var dentistIds = visits.Select(x => x.DentistId).ToArray(); var branchIds = visits.Select(x => x.BranchId).ToArray();
        var appointmentsById = await db.Appointments.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var patients = await db.Patients.AsNoTracking().Where(x => patientIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var dentists = await db.Employees.AsNoTracking().Where(x => dentistIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var now = clock.GetUtcNow();
        return visits.Select(v => { var a = appointmentsById[v.AppointmentId]; var p = patients[v.PatientId];
        return new VisitResult(v.Id, p.Id, p.PatientCode, p.FullName, a.Id, a.Code, v.BranchId, branches[v.BranchId], v.DentistId, dentists[v.DentistId],
            a.ChairId, a.AssistantId, a.StartAt, v.ArrivalAt, v.RecordedAt, v.Status, a.Status, v.Version, a.Version,
            v.Purpose, v.Note, v.ArrivalReason, v.SourceJson == null ? null : JsonSerializer.Deserialize<VisitSource>(v.SourceJson),
            v.RequiresClinicalSourceReview, v.Status == VisitStatuses.Queued ? Math.Max(0, (int)(now - v.ArrivalAt).TotalMinutes) : 0,
            replayed, $"/visits/{v.Id}"); }).ToArray();
    }

    public async Task<VisitResult> CheckInAsync(int actorId, Guid appointmentId, CheckInRequest request, CancellationToken ct)
    {
        var note = Clean(request.Note); var arrivalReason = Clean(request.ArrivalReason);
        if (appointmentId == Guid.Empty || request.PatientId <= 0 || request.ExpectedVersion <= 0 || request.RequestKey == Guid.Empty
            || !request.IdentityConfirmed || !AttendancePurposes.All.Contains(request.Purpose) || note?.Length > 1000 || arrivalReason?.Length > 500
            || request.PriorCourseId == Guid.Empty) throw Error("MSG04");
        var canonical = new { appointmentId, request.PatientId, request.ExpectedVersion, request.IdentityConfirmed,
            ArrivalAt = request.ArrivalAt?.ToUniversalTime(), arrivalReason, request.Purpose, request.PriorCourseId, note };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Same lock and ordering as booking/change; this also serializes newly identified Patient reservations.
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})", ct);
        var a = await db.Appointments.SingleOrDefaultAsync(x => x.Id == appointmentId, ct) ?? throw Error("MSG113", 404);
        var actor = await ScopeAsync(actorId, a.BranchId, true, ct);
        var patient = await db.Patients.FromSqlInterpolated($"SELECT * FROM \"PATIENT_PROFILE\" WHERE \"Id\" = {request.PatientId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw Error("MSG113", 404);
        if (patient.IsArchived) throw Error("MSG202", 409);
        if (a.PatientId.HasValue ? a.PatientId != patient.Id
            : PatientLinkSupport.NormalizeName(a.AttendeeName) != PatientLinkSupport.NormalizeName(patient.FullName)
                || a.AttendeeBirthDate.HasValue && a.AttendeeBirthDate != patient.BirthDate) throw Error("MSG43", 409);
        var receipt = await db.Set<ReceptionReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorAccountId == actorId && x.RequestKey == request.RequestKey, ct);
        if (receipt != null && receipt.Fingerprint != fingerprint) throw Error("MSG111", 409);
        var existing = await db.Visits.AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == a.Id, ct);
        if (receipt != null && (existing == null || receipt.VisitId != existing.Id)) throw Error("MSG111", 409);
        if (existing != null)
        {
            if (existing.PatientId != patient.Id) throw Error("MSG43", 409);
            if (receipt == null) db.Add(new ReceptionReceipt { ActorAccountId = actorId, RequestKey = request.RequestKey, VisitId = existing.Id, Fingerprint = fingerprint });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await ResultAsync(existing, true, ct);
        }
        var now = clock.GetUtcNow();
        if (a.Version != request.ExpectedVersion) throw Error("MSG111", 409);
        if (Ineligible(a, now) != null) throw Error(a.Status == "NoShow" ? "MSG47" : "MSG42", 409);
        var arrival = request.ArrivalAt?.ToUniversalTime() ?? now;
        if (arrival > now || ClinicDate(arrival) != ClinicDate(now)) throw Error("MSG04");
        if (request.ArrivalAt.HasValue)
        {
            if (!actor.CanBackdate) throw Error("MSG06", 403);
            if (arrivalReason == null) throw Error("MSG02");
        }
        VisitSource? source = null;
        if (request.PriorCourseId.HasValue)
        {
            source = await sources.ResolveAsync(a.BranchId, patient.Id, request.PriorCourseId.Value, ct);
            if (source.PriorCourseId != request.PriorCourseId || source.CourseVersion is not > 0) throw Error("MSG43", 409);
        }
        if (a.FollowUpId.HasValue) source = (source ?? new VisitSource()) with { FollowUpId = a.FollowUpId };
        var reservation = await db.Set<ResourceReservation>().SingleOrDefaultAsync(x => x.Id == a.ReservationId, ct) ?? throw Error("MSG47", 409);
        if (reservation.Status != "Confirmed" || reservation.BranchId != a.BranchId || reservation.DentistId != a.DentistId
            || reservation.AssistantId != a.AssistantId || reservation.ChairId != a.ChairId || reservation.Start != a.StartAt || reservation.End != a.EndAt
            || reservation.AttendeeKey != null && reservation.AttendeeKey != $"patient:{patient.Id}") throw Error("MSG47", 409);
        await availability.ValidateReceptionAsync(reservation.Id, patient.Id, ct);
        var before = JsonSerializer.Serialize(new { a.PatientId, a.Status, a.Version });
        var visit = new Visit { AppointmentId = a.Id, BranchId = a.BranchId, PatientId = patient.Id, DentistId = a.DentistId,
            ArrivalAt = arrival, RecordedAt = now, RecordedBy = actorId, ArrivalReason = arrivalReason, Purpose = request.Purpose, Note = note,
            SourceJson = source == null ? null : JsonSerializer.Serialize(source), RequiresClinicalSourceReview = request.Purpose != AttendancePurposes.InitialExamination };
        a.PatientId = patient.Id; a.Status = "CheckedIn"; a.Version++;
        reservation.AttendeeKey = $"patient:{patient.Id}"; reservation.AttendeeLabel = patient.FullName;
        db.Visits.Add(visit);
        db.Add(new VisitAudit { VisitId = visit.Id, ActorAccountId = actorId, OccurredAt = now, BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { visit.PatientId, visit.AppointmentId, visit.ArrivalAt, visit.RecordedAt,
                visit.ArrivalReason, visit.Purpose, request.IdentityConfirmed, source, visit.Note, a.Version }) });
        db.Add(new ReceptionReceipt { ActorAccountId = actorId, RequestKey = request.RequestKey, VisitId = visit.Id, Fingerprint = fingerprint });
        db.AppointmentChanges.Add(new AppointmentChange { AppointmentId = a.Id, ActorAccountId = actorId, ActorKind = "STAFF", Action = "CHECK_IN",
            OldStart = a.StartAt, OldEnd = a.EndAt, OldDentistId = a.DentistId, OldChairId = a.ChairId,
            Reason = "Đã đối chiếu đúng người đến khám", RequestKey = request.RequestKey, OccurredAt = now });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ResultAsync(visit, false, ct);
    }
}
