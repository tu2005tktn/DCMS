using System.Data;
using DCMS.Application.Common;
using DCMS.Application.Features.PatientLinks;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.PatientLinks.PatientLinkSupport;

namespace DCMS.Infrastructure.Features.PatientLinks;

// S08: only an active Clinic Assistant with an employee profile decides; every decision rechecks state in one transaction.
public sealed class PatientLinkReviewService(DcmsDbContext db, UserManager<ApplicationUser> users) : IPatientLinkReviewService
{
    public async Task<LinkQueuePage> QueueAsync(string? status, DateOnly? from, DateOnly? to, string? keyword,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize) Invalid("Phân trang");
        var filter = string.IsNullOrWhiteSpace(status) ? PatientLinkStatus.Pending : status.Trim().ToUpperInvariant();
        if (filter != "ALL" && !PatientLinkStatus.All.Contains(filter)) Invalid("Trạng thái");
        if (from > to) Invalid("Khoảng ngày");
        var term = Optional(keyword, "Từ khóa", 100);

        var query = from link in db.PatientAccountLinks.AsNoTracking()
                    join user in db.Users.AsNoTracking() on link.AccountId equals user.Id
                    select new { link, user };
        if (filter != "ALL") query = query.Where(x => x.link.Status == filter);
        if (from is { } start)
        {
            var since = ClinicStartOfDay(start);
            query = query.Where(x => x.link.RequestedAt >= since);
        }
        if (to is { } end)
        {
            var until = ClinicStartOfDay(end.AddDays(1));
            query = query.Where(x => x.link.RequestedAt < until);
        }
        if (term != null)
        {
            var upper = term.ToUpperInvariant();
            query = query.Where(x => x.link.RequestCode.Contains(upper) || x.user.NormalizedUserName!.Contains(upper)
                || x.user.FullName.ToUpper().Contains(upper) || x.link.DeclaredFullName.ToUpper().Contains(upper)
                || (x.user.NormalizedEmail != null && x.user.NormalizedEmail.Contains(upper))
                || (x.user.PhoneNumber != null && x.user.PhoneNumber.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        // Pending is worked oldest first; history is read newest first.
        var ordered = filter == PatientLinkStatus.Pending
            ? query.OrderBy(x => x.link.RequestedAt).ThenBy(x => x.link.Id)
            : query.OrderByDescending(x => x.link.RequestedAt).ThenByDescending(x => x.link.Id);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var handlerIds = rows.Select(x => x.link.RevokingEmployeeId ?? x.link.ReviewingEmployeeId).OfType<int>().Distinct().ToArray();
        var handlers = await db.Employees.AsNoTracking().Where(x => handlerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        return new(rows.Select(x => new LinkQueueItem(x.link.Id, x.link.RequestCode, x.user.Id, x.user.UserName!, x.user.FullName,
            x.link.DeclaredFullName, x.link.RequestedAt, x.link.Status,
            (x.link.RevokingEmployeeId ?? x.link.ReviewingEmployeeId) is { } h ? handlers.GetValueOrDefault(h) : null,
            x.link.RevokedAt ?? x.link.ReviewedAt ?? x.link.WithdrawnAt)).ToArray(), total, page, pageSize);
    }

    public async Task<LinkReviewDetail> DetailAsync(int id, CancellationToken ct) =>
        await DetailAsync(await db.PatientAccountLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound(), ct);

    public async Task<IReadOnlyList<PatientCandidate>> PatientsAsync(int id, string? keyword, CancellationToken ct)
    {
        var term = Required(keyword, "Từ khóa", 100)!;
        if (!await db.PatientAccountLinks.AnyAsync(x => x.Id == id, ct)) throw NotFound();
        var upper = term.ToUpperInvariant();
        var patients = await db.Patients.AsNoTracking()
            .Where(x => x.PatientCode.ToUpper().Contains(upper) || x.FullName.ToUpper().Contains(upper)
                || (x.Phone != null && x.Phone.Contains(term)))
            .OrderBy(x => x.PatientCode).Take(20).ToListAsync(ct);
        var ids = patients.Select(x => x.Id).ToArray();
        var linked = await db.PatientAccountLinks.AsNoTracking()
            .Where(x => x.Status == PatientLinkStatus.Verified && x.PatientId != null && ids.Contains(x.PatientId.Value))
            .Select(x => x.PatientId!.Value).ToListAsync(ct);
        return patients.Select(x => new PatientCandidate(new(x.Id, x.PatientCode, x.FullName, x.BirthDate, x.Gender, x.Phone),
            linked.Contains(x.Id))).ToArray();
    }

    public Task<LinkDecisionResult> VerifyAsync(int actorId, int id, VerifyLinkRequest request, CancellationToken ct) => GuardAsync(async () =>
    {
        var note = Required(request.VerificationNote, "Kết quả đối chiếu", 1000);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await RequireAssistantAsync(actorId, ct);
        var link = await LoadForDecisionAsync(id, request.Version, PatientLinkStatus.Pending, ct);
        var account = await db.Users.AsNoTracking().SingleAsync(x => x.Id == link.AccountId, ct);
        if (!account.IsActive || account.AccountType != "Public") throw Insufficient();
        // A Patient exists only after first check-in (GBR-016); no record means no link may be approved.
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PatientId, ct)
            ?? throw new BusinessException(400, SystemMessages.Get("MSG17"));
        if (NormalizeName(patient.FullName) != NormalizeName(link.DeclaredFullName)
            || (link.DeclaredBirthDate != null && patient.BirthDate != null && link.DeclaredBirthDate != patient.BirthDate)
            || (link.DeclaredPatientCode != null && !string.Equals(link.DeclaredPatientCode, patient.PatientCode, StringComparison.OrdinalIgnoreCase)))
            throw Insufficient();
        if (await db.PatientAccountLinks.AnyAsync(x => x.Status == PatientLinkStatus.Verified
                && (x.AccountId == link.AccountId || x.PatientId == patient.Id), ct))
            throw new BusinessException(409, SystemMessages.Get("MSG142"));

        var before = Snapshot(link);
        var now = DateTimeOffset.UtcNow;
        link.Status = PatientLinkStatus.Verified;
        link.PatientId = patient.Id;
        link.ReviewingEmployeeId = employee.Id;
        link.VerificationNote = note;
        link.ReviewedAt = now;
        link.Version++;
        AddAudit(db, link, actorId, "Verify", note, before, now);
        // Deferred (UC-34): enqueue MSG19 as an in-app notification to the account in this transaction.
        await CommitAsync(db, transaction, ct);
        return new LinkDecisionResult(await DetailAsync(link, ct), SystemMessages.Get("MSG19"));
    });

    public Task<LinkDecisionResult> RejectAsync(int actorId, int id, RejectLinkRequest request, CancellationToken ct) => GuardAsync(async () =>
    {
        var reason = Required(request.DecisionReason, "Lý do", 500);
        var note = Optional(request.VerificationNote, "Kết quả đối chiếu", 1000);
        var publicNote = Optional(request.PublicNote, "Nội dung gửi người dùng", 500);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await RequireAssistantAsync(actorId, ct);
        var link = await LoadForDecisionAsync(id, request.Version, PatientLinkStatus.Pending, ct);
        var before = Snapshot(link);
        var now = DateTimeOffset.UtcNow;
        link.Status = PatientLinkStatus.Rejected;
        link.ReviewingEmployeeId = employee.Id;
        link.VerificationNote = note ?? link.VerificationNote;
        link.DecisionReason = reason;
        link.PublicNote = publicNote;
        link.ReviewedAt = now;
        link.Version++;
        AddAudit(db, link, actorId, "Reject", reason, before, now);
        // Deferred (UC-34): enqueue MSG20 as an in-app notification to the account in this transaction.
        await CommitAsync(db, transaction, ct);
        return new LinkDecisionResult(await DetailAsync(link, ct), SystemMessages.Get("MSG20"));
    });

    public Task<LinkDecisionResult> RevokeAsync(int actorId, int id, RevokeLinkRequest request, CancellationToken ct) => GuardAsync(async () =>
    {
        var reason = Required(request.DecisionReason, "Lý do", 500);
        var publicNote = Optional(request.PublicNote, "Nội dung gửi người dùng", 500);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await RequireAssistantAsync(actorId, ct);
        var link = await LoadForDecisionAsync(id, request.Version, PatientLinkStatus.Verified, ct);
        var before = Snapshot(link);
        var now = DateTimeOffset.UtcNow;
        // The Patient record and clinical data are retained; only the access link changes state.
        link.Status = PatientLinkStatus.Revoked;
        link.RevokingEmployeeId = employee.Id;
        link.RevokedAt = now;
        link.DecisionReason = reason;
        link.PublicNote = publicNote;
        link.Version++;
        AddAudit(db, link, actorId, "Revoke", reason, before, now);
        // Deferred (UC-34): cancel NOTIFICATION rows with recipient_link_id = link.Id and status PENDING here,
        // then enqueue MSG20. Own-record reads already re-check the link on every request.
        await CommitAsync(db, transaction, ct);
        return new LinkDecisionResult(await DetailAsync(link, ct), SystemMessages.Get("MSG20"));
    });

    public async Task<IReadOnlyList<LinkAuditDto>> AuditAsync(int id, CancellationToken ct)
    {
        if (!await db.PatientAccountLinks.AnyAsync(x => x.Id == id, ct)) throw NotFound();
        return await (from audit in db.PatientLinkAudits.AsNoTracking()
                      join user in db.Users.AsNoTracking() on audit.ActorAccountId equals user.Id
                      where audit.LinkId == id
                      orderby audit.Id descending
                      select new LinkAuditDto(audit.Id, audit.ActorAccountId, user.FullName, audit.Action, audit.Reason,
                          audit.OccurredAt, audit.BeforeJson, audit.AfterJson)).Take(100).ToListAsync(ct);
    }

    private async Task<EmployeeProfile> RequireAssistantAsync(int actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && x.AccountType == "Staff", ct);
        if (actor == null || !await users.IsInRoleAsync(actor, AccountRoles.ClinicAssistant)) throw Forbidden();
        return await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct) ?? throw Forbidden();
    }

    private async Task<PatientAccountLink> LoadForDecisionAsync(int id, int version, string requiredStatus, CancellationToken ct)
    {
        var link = await db.PatientAccountLinks.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound();
        if (link.Version != version || link.Status != requiredStatus) throw Stale();
        return link;
    }

    private async Task<LinkReviewDetail> DetailAsync(PatientAccountLink link, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == link.AccountId, ct);
        var patient = link.PatientId == null ? null : await db.Patients.AsNoTracking().SingleAsync(x => x.Id == link.PatientId, ct);
        var employeeIds = new[] { link.ReviewingEmployeeId, link.RevokingEmployeeId }.OfType<int>().ToArray();
        var employees = await db.Employees.AsNoTracking().Where(x => employeeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var active = await db.PatientAccountLinks.AsNoTracking()
            .Where(x => x.AccountId == link.AccountId && x.Status == PatientLinkStatus.Verified && x.Id != link.Id)
            .Select(x => x.RequestCode).FirstOrDefaultAsync(ct);
        return new(link.Id, link.RequestCode, link.Status, link.RequestedAt,
            new(user.Id, user.UserName!, user.FullName, user.Email, user.EmailConfirmed, user.PhoneNumber, user.PhoneNumberConfirmed,
                user.IsActive, user.CreatedAt),
            link.DeclaredFullName, link.DeclaredBirthDate, link.DeclaredPatientCode, link.RequestNote,
            patient == null ? null : new(patient.Id, patient.PatientCode, patient.FullName, patient.BirthDate, patient.Gender, patient.Phone),
            link.VerificationNote, link.DecisionReason, link.PublicNote,
            link.ReviewingEmployeeId is { } reviewer ? employees.GetValueOrDefault(reviewer) : null, link.ReviewedAt,
            link.RevokingEmployeeId is { } revoker ? employees.GetValueOrDefault(revoker) : null, link.RevokedAt,
            link.WithdrawnAt, new(active != null, active), link.Version);
    }

    private static BusinessException Forbidden() => new(403, SystemMessages.Get("MSG06"));
    private static BusinessException Insufficient() => new(400, SystemMessages.Get("MSG143"));
}
