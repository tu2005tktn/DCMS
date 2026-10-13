using System.Data;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.PatientLinks;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.PatientLinks.PatientLinkSupport;

namespace DCMS.Infrastructure.Features.PatientLinks;

// S07: the account in the session is the only subject; no AccountId or PatientId is accepted from the caller.
public sealed partial class PatientLinkService(DcmsDbContext db) : IPatientLinkService
{
    public async Task<SelfLinkOverview> OverviewAsync(int accountId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, ct) ?? throw Forbidden();
        if (user.AccountType != "Public") throw Forbidden();
        var links = await db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).Take(50).ToListAsync(ct);
        var current = links.FirstOrDefault(x => x.Status == PatientLinkStatus.Verified) ?? links.FirstOrDefault();
        var status = current == null || current.Status == PatientLinkStatus.Withdrawn ? PatientLinkStatus.NotLinked : current.Status;
        var canSubmit = status is not (PatientLinkStatus.Pending or PatientLinkStatus.Verified);
        return new(new(user.Id, user.UserName!, user.FullName, user.EmailConfirmed ? user.Email : null,
                user.PhoneNumberConfirmed ? user.PhoneNumber : null),
            status, current == null ? null : Map(current), links.Select(Map).ToArray(), canSubmit);
    }

    public Task<SelfLinkChange> SubmitAsync(int accountId, SubmitLinkRequest request, CancellationToken ct) => GuardAsync(async () =>
    {
        var fullName = Required(request.FullName, "Họ tên", 160)!;
        if (request.BirthDate is { } birth && (birth > ClinicToday() || birth < new DateOnly(1900, 1, 1))) Invalid("Ngày sinh");
        var patientCode = Optional(request.PatientCode, "Mã hồ sơ", 40);
        if (patientCode != null && !PatientCodePattern().IsMatch(patientCode)) Invalid("Mã hồ sơ");
        var matching = Optional(request.MatchingInformation, "Thông tin đối chiếu", 1000);
        // Booking for a relative never qualifies: the requester must confirm the record is their own.
        if (!request.ConfirmSelf) throw new BusinessException(400, SystemMessages.Get("MSG02", "xác nhận đây là hồ sơ của chính bạn"));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.IsActive, ct);
        if (user == null || user.AccountType != "Public") throw Forbidden();
        var open = await db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId
            && (x.Status == PatientLinkStatus.Pending || x.Status == PatientLinkStatus.Verified)).ToListAsync(ct);
        if (open.Any(x => x.Status == PatientLinkStatus.Verified)) throw new BusinessException(409, SystemMessages.Get("MSG142"));
        // Repeated submission returns the request already being processed instead of creating a duplicate.
        if (open.SingleOrDefault() is { } pending) return new SelfLinkChange(Map(pending), SystemMessages.Get("MSG18"), false);

        var now = DateTimeOffset.UtcNow;
        var link = new PatientAccountLink
        {
            RequestCode = NewRequestCode(now), AccountId = accountId, Status = PatientLinkStatus.Pending, RequestedAt = now,
            DeclaredFullName = fullName, DeclaredBirthDate = request.BirthDate, DeclaredPatientCode = patientCode?.ToUpperInvariant(),
            RequestNote = matching
        };
        db.PatientAccountLinks.Add(link);
        AddAudit(db, link, accountId, "Submit", null, null, now);
        await CommitAsync(db, transaction, ct);
        return new SelfLinkChange(Map(link), SystemMessages.Get("MSG18"), true);
    });

    public async Task<SelfLinkRequestDto> GetAsync(int accountId, int id, CancellationToken ct) =>
        Map(await db.PatientAccountLinks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId, ct)
            ?? throw NotFound());

    public Task<SelfLinkRequestDto> WithdrawAsync(int accountId, int id, WithdrawLinkRequest request, CancellationToken ct) => GuardAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Another account's request id is indistinguishable from a missing one.
        var link = await db.PatientAccountLinks.SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId, ct) ?? throw NotFound();
        if (link.Version != request.Version || link.Status != PatientLinkStatus.Pending) throw Stale();
        var before = Snapshot(link);
        var now = DateTimeOffset.UtcNow;
        link.Status = PatientLinkStatus.Withdrawn;
        link.WithdrawnAt = now;
        link.Version++;
        AddAudit(db, link, accountId, "Withdraw", null, before, now);
        await CommitAsync(db, transaction, ct);
        return Map(link);
    });

    public async Task<OwnRecordSummary> OwnRecordAsync(int accountId, CancellationToken ct)
    {
        // Checked against the database on every request so a revocation blocks the next read of an existing session.
        var own = await (from link in db.PatientAccountLinks
                         join patient in db.Patients on link.PatientId equals patient.Id
                         where link.AccountId == accountId && link.Status == PatientLinkStatus.Verified
                         select new { link, patient }).AsNoTracking().SingleOrDefaultAsync(ct) ?? throw Forbidden();
        db.PatientLinkAudits.Add(new PatientLinkAudit
        {
            LinkId = own.link.Id, ActorAccountId = accountId, Action = "OwnRecordRead", OccurredAt = DateTimeOffset.UtcNow,
            AfterJson = $"{{\"patientId\":{own.patient.Id}}}"
        });
        await db.SaveChangesAsync(ct);
        return new(own.patient.PatientCode, own.patient.FullName, own.patient.BirthDate, own.link.ReviewedAt ?? own.link.RequestedAt);
    }

    private static SelfLinkRequestDto Map(PatientAccountLink x)
    {
        var decided = x.Status switch
        {
            PatientLinkStatus.Revoked => x.RevokedAt,
            PatientLinkStatus.Withdrawn => x.WithdrawnAt,
            _ => x.ReviewedAt
        };
        var disclosed = x.Status is PatientLinkStatus.Rejected or PatientLinkStatus.Revoked ? x.PublicNote : null;
        return new(x.Id, x.RequestCode, x.Status, x.RequestedAt, x.DeclaredFullName, x.DeclaredBirthDate,
            x.DeclaredPatientCode, x.RequestNote, decided, PublicResult(x.Status), disclosed, x.Version);
    }

    private static BusinessException Forbidden() => new(403, SystemMessages.Get("MSG06"));
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,40}$")] private static partial Regex PatientCodePattern();
}
