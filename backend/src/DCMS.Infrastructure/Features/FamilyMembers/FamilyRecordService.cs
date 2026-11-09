using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.FamilyMembers;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DCMS.Infrastructure.Features.FamilyMembers;

public sealed partial class FamilyRecordService(DcmsDbContext db, TimeProvider clock, ISettingsAccess access,
    IConfiguration configuration) : IFamilyRecordService
{
    private static BusinessException Missing() => new(404, new("FAMILY_ACCESS_NOT_FOUND", "Không tìm thấy yêu cầu hoặc người thân."));
    private static BusinessException Denied() => new(403, new("FAMILY_ACCESS_DENIED", "Bạn chưa có quyền xem nội dung hồ sơ này hoặc quyền đã hết hiệu lực."));
    private static BusinessException Invalid(string field) => new(400, new("FAMILY_ACCESS_VALIDATION", $"Vui lòng kiểm tra {field}.") { Field = field });
    private static BusinessException Stale() => new(409, SystemMessages.Get("MSG111"));
    private static string? Text(string? value, int max, string field, bool required = false)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text?.Length > max || required && text == null) throw Invalid(field);
        return text;
    }
    private static void Page(int page, int size) { if (page < 1 || page > 1_000_000 || size is < 1 or > 50) throw Invalid("pagination"); }
    private string Status(FamilyRecordAccess row) => row.Status == "Verified" && row.ExpiresAt <= clock.GetUtcNow() ? "Expired" : row.Status;
    private FamilyAccessDto Dto(FamilyRecordAccess row) => new(row.Id, row.FamilyMemberId, row.Code, Status(row),
        row.DeclaredFullName, row.DeclaredBirthDate, row.DeclaredPatientCode, row.MatchingInformation,
        row.RequestedAt, row.DecidedAt, row.ExpiresAt, row.Scopes, row.PublicNote, row.Version);
    private async Task PublicAsync(int actor, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && x.AccountType == "Public" && x.IsActive
            && (!x.LockoutEnabled || x.LockoutEnd == null || x.LockoutEnd <= clock.GetUtcNow()), ct)) throw Denied();
    }
    private async Task<FamilyMember> MemberAsync(int actor, Guid memberId, CancellationToken ct)
    {
        await PublicAsync(actor, ct);
        return await db.Set<FamilyMember>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == memberId
            && x.OwnerAccountId == actor && x.DeletedAt == null, ct) ?? throw Missing();
    }
    private async Task<int[]> StaffAsync(int actor, string permission, CancellationToken ct)
    {
        var state = await access.RequireAsync(actor, permission, ct);
        if (!state.Roles.Contains(AccountRoles.ClinicAssistant)) throw Denied();
        return state.BranchIds;
    }
    private async Task<FamilyRecordAccess> ReviewRowAsync(int actor, Guid id, string permission, CancellationToken ct)
    {
        var branches = await StaffAsync(actor, permission, ct);
        return await db.Set<FamilyRecordAccess>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && (x.BranchId == null || branches.Contains(x.BranchId.Value)), ct) ?? throw Missing();
    }
    private void Audit(FamilyRecordAccess row, int actor, string action, string? reason = null) => db.Add(new FamilyRecordAudit {
        AccessId = row.Id, ActorAccountId = actor, Action = action, Reason = reason, OccurredAt = clock.GetUtcNow() });

    private async Task<FamilyAccessDto> WriteAsync(int actor, int owner, Guid key, object command,
        Func<Task> authorize, Func<Task<FamilyRecordAccess>> apply, CancellationToken ct)
    {
        try { return await WriteCoreAsync(actor, owner, key, command, authorize, apply, ct); }
        catch (DbUpdateConcurrencyException) { throw Stale(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" or "40001" }) { throw Stale(); }
    }
    private async Task<FamilyAccessDto> WriteCoreAsync(int actor, int owner, Guid key, object command,
        Func<Task> authorize, Func<Task<FamilyRecordAccess>> apply, CancellationToken ct)
    {
        if (key == Guid.Empty) throw Invalid("requestKey");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(90121, {owner})", ct);
        await authorize();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command))));
        var receipt = await db.Set<FamilyRecordReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorAccountId == actor && x.RequestKey == key, ct);
        if (receipt != null) {
            if (receipt.Fingerprint != hash) throw Stale();
            return JsonSerializer.Deserialize<FamilyAccessDto>(receipt.ResultJson)!;
        }
        var row = await apply();
        var result = Dto(row);
        db.Add(new FamilyRecordReceipt { ActorAccountId = actor, RequestKey = key, AccessId = row.Id,
            Fingerprint = hash, ResultJson = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }

    public async Task<FamilyAccessOverview> OverviewAsync(int actor, Guid memberId, CancellationToken ct)
    {
        await MemberAsync(actor, memberId, ct);
        var rows = await db.Set<FamilyRecordAccess>().AsNoTracking().Where(x => x.FamilyMemberId == memberId && x.OwnerAccountId == actor)
            .OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).Take(50).ToArrayAsync(ct);
        var current = rows.FirstOrDefault(x => Status(x) is "Pending" or "Verified") ?? rows.FirstOrDefault();
        return new(current == null ? null : Dto(current), rows.Select(Dto).ToArray(), current == null || Status(current) is not ("Pending" or "Verified"));
    }

    public async Task<FamilyAccessDto> SubmitAsync(int actor, Guid memberId, FamilyAccessRequest command, CancellationToken ct)
    {
        var member = await MemberAsync(actor, memberId, ct);
        var code = Text(command.PatientCode, 40, "patientCode");
        var note = Text(command.MatchingInformation, 1000, "matchingInformation");
        return await WriteAsync(actor, actor, command.RequestKey, new { action = "Submit", memberId, code, note },
            async () => { member = await MemberAsync(actor, memberId, ct); }, async () => {
                var current = await db.Set<FamilyRecordAccess>().SingleOrDefaultAsync(x => x.FamilyMemberId == memberId && (x.Status == "Pending" || x.Status == "Verified"), ct);
                if (current != null && Status(current) == "Verified") throw Stale();
                if (current?.Status == "Pending") return current;
                if (current != null) { current.Status = "Expired"; current.Version++; Audit(current, actor, "Expired"); await db.SaveChangesAsync(ct); }
                var row = new FamilyRecordAccess { FamilyMemberId = memberId, OwnerAccountId = actor,
                    Code = "FA-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), DeclaredFullName = member.FullName,
                    DeclaredBirthDate = member.BirthDate, Relationship = member.Relationship, DeclaredPatientCode = code,
                    MatchingInformation = note, RequestedAt = clock.GetUtcNow() };
                db.Add(row); Audit(row, actor, "Submitted"); return row;
            }, ct);
    }

    public async Task<FamilyAccessDto> WithdrawAsync(int actor, Guid memberId, Guid requestId, FamilyAccessWithdraw command, CancellationToken ct)
    {
        await MemberAsync(actor, memberId, ct);
        return await WriteAsync(actor, actor, command.RequestKey, new { action = "Withdraw", memberId, requestId, command.ExpectedVersion },
            async () => { await MemberAsync(actor, memberId, ct); }, async () => {
                var row = await db.Set<FamilyRecordAccess>().SingleOrDefaultAsync(x => x.Id == requestId && x.OwnerAccountId == actor && x.FamilyMemberId == memberId, ct) ?? throw Missing();
                if (row.Version != command.ExpectedVersion || row.Status is not ("Pending" or "Verified")) throw Stale();
                row.Status = row.Status == "Pending" ? "Withdrawn" : "Revoked"; row.Version++;
                row.DecidedAt = clock.GetUtcNow(); row.DecidedBy = actor;
                row.PublicNote = "Người yêu cầu đã chấm dứt yêu cầu/quyền theo dõi.";
                Audit(row, actor, row.Status); return row;
            }, ct);
    }

    public async Task<FamilyPage<FamilyReviewRow>> QueueAsync(int actor, string? status, string? keyword, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize); var branches = await StaffAsync(actor, "patient-links.read", ct);
        var filter = status ?? "Pending";
        if (!new[] { "All", "Pending", "Verified", "Rejected", "Revoked", "Withdrawn", "Expired" }.Contains(filter)) throw Invalid("status");
        var term = Text(keyword, 100, "keyword"); var now = clock.GetUtcNow();
        var query = from r in db.Set<FamilyRecordAccess>().AsNoTracking() join u in db.Users.AsNoTracking() on r.OwnerAccountId equals u.Id
            where r.BranchId == null || branches.Contains(r.BranchId.Value) select new { r, u };
        if (filter == "Expired") query = query.Where(x => x.r.Status == "Expired" || x.r.Status == "Verified" && x.r.ExpiresAt <= now);
        else if (filter != "All") query = query.Where(x => x.r.Status == filter && (filter != "Verified" || x.r.ExpiresAt > now));
        if (term != null) query = query.Where(x => x.r.DeclaredFullName.Contains(term) || x.r.Code.Contains(term) || x.u.UserName!.Contains(term));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.r.RequestedAt).ThenBy(x => x.r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(x => new FamilyReviewRow(x.r.Id, x.r.Code, x.u.UserName!, x.u.FullName, x.r.DeclaredFullName,
            Status(x.r), x.r.RequestedAt, x.r.BranchId, x.r.Version)).ToArray(), total, page, pageSize);
    }

    private IQueryable<int> PatientBranches(int patientId) => db.Visits.Where(x => x.PatientId == patientId).Select(x => x.BranchId)
        .Union(db.TreatmentDossiers.Where(x => x.PatientId == patientId && x.State != "Deleted").Select(x => x.BranchId));

    public async Task<FamilyReviewDetail> DetailAsync(int actor, Guid requestId, CancellationToken ct)
    {
        var row = await ReviewRowAsync(actor, requestId, "patient-links.read", ct);
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == row.OwnerAccountId, ct);
        var branches = await StaffAsync(actor, "patient-links.read", ct);
        var choices = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branches.Contains(x.Id) && x.Active).Select(x => new FamilyBranch(x.Id, x.Name)).ToArrayAsync(ct);
        var patient = row.PatientId.HasValue ? await db.Patients.AsNoTracking().SingleAsync(x => x.Id == row.PatientId, ct) : null;
        return new(Dto(row), owner.Id, owner.UserName!, owner.FullName, row.Relationship,
            patient == null ? null : new(patient.Id, patient.PatientCode, patient.FullName, patient.BirthDate, [row.BranchId!.Value]),
            row.BranchId, row.VerificationNote, row.SharingBasis, row.DecisionReason, choices);
    }

    public async Task<IReadOnlyList<FamilyPatientCandidate>> CandidatesAsync(int actor, Guid requestId, string? keyword, CancellationToken ct)
    {
        await ReviewRowAsync(actor, requestId, "patient-links.verify", ct);
        var branches = await StaffAsync(actor, "patient-links.verify", ct);
        var term = Text(keyword, 100, "keyword", true)!;
        var patientIds = db.Visits.Where(x => branches.Contains(x.BranchId)).Select(x => x.PatientId)
            .Union(db.TreatmentDossiers.Where(x => branches.Contains(x.BranchId) && x.State != "Deleted").Select(x => x.PatientId));
        var patients = await db.Patients.AsNoTracking().Where(x => !x.IsArchived && patientIds.Contains(x.Id)
            && (x.FullName.Contains(term) || x.PatientCode.Contains(term) || x.Phone != null && x.Phone.Contains(term)))
            .OrderBy(x => x.PatientCode).Take(20).ToArrayAsync(ct);
        var result = new List<FamilyPatientCandidate>();
        foreach (var patient in patients) result.Add(new(patient.Id, patient.PatientCode, patient.FullName, patient.BirthDate,
            await PatientBranches(patient.Id).Where(x => branches.Contains(x)).Distinct().ToArrayAsync(ct)));
        return result;
    }

    public async Task<IReadOnlyList<FamilyAuditDto>> AuditAsync(int actor, Guid requestId, CancellationToken ct)
    {
        await ReviewRowAsync(actor, requestId, "patient-links.read", ct);
        return await (from a in db.Set<FamilyRecordAudit>().AsNoTracking() join u in db.Users on a.ActorAccountId equals u.Id
            where a.AccessId == requestId orderby a.OccurredAt, a.Id select new FamilyAuditDto(a.Action, u.FullName, a.Reason, a.OccurredAt)).ToArrayAsync(ct);
    }

    public async Task<FamilyAccessDto> VerifyAsync(int actor, Guid requestId, VerifyFamilyAccess command, CancellationToken ct)
    {
        var initial = await ReviewRowAsync(actor, requestId, "patient-links.verify", ct);
        return await WriteAsync(actor, initial.OwnerAccountId, command.RequestKey, new { action = "Verify", requestId, command },
            async () => { await ReviewRowAsync(actor, requestId, "patient-links.verify", ct); }, async () => {
                var row = await db.Set<FamilyRecordAccess>().SingleAsync(x => x.Id == requestId, ct);
                if (row.Version != command.ExpectedVersion || row.Status != "Pending") throw Stale();
                var member = await MemberAsync(row.OwnerAccountId, row.FamilyMemberId, ct);
                var branches = await StaffAsync(actor, "patient-links.verify", ct);
                if (!branches.Contains(command.BranchId)) throw Denied();
                var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.PatientId && !x.IsArchived, ct) ?? throw Missing();
                if (!await PatientBranches(patient.Id).AnyAsync(x => x == command.BranchId, ct)) throw Denied();
                if (PatientLinkSupport.NormalizeName(patient.FullName) != PatientLinkSupport.NormalizeName(row.DeclaredFullName)
                    || row.DeclaredBirthDate.HasValue && row.DeclaredBirthDate != patient.BirthDate
                    || row.DeclaredPatientCode != null && !row.DeclaredPatientCode.Equals(patient.PatientCode, StringComparison.OrdinalIgnoreCase)
                    || member.FullName != row.DeclaredFullName || member.BirthDate != row.DeclaredBirthDate) throw Invalid("patientId");
                var note = Text(command.VerificationNote, 1000, "verificationNote", true);
                var scopes = command.Scopes?.Distinct().OrderBy(x => x).ToArray() ?? [];
                if (!command.ConsentConfirmed || command.SharingBasis is not ("PatientConsent" or "LegalRepresentative")) throw Invalid("consentConfirmed");
                if (scopes.Length == 0 || scopes.Any(x => !FamilyRecordScopes.All.Contains(x))) throw Invalid("scopes");
                if (command.ExpiresAt <= clock.GetUtcNow() || command.ExpiresAt > clock.GetUtcNow().AddDays(365)) throw Invalid("expiresAt");
                row.PatientId = patient.Id; row.BranchId = command.BranchId; row.Scopes = scopes;
                row.SharingBasis = command.SharingBasis; row.VerificationNote = note; row.ExpiresAt = command.ExpiresAt.ToUniversalTime();
                row.Status = "Verified"; row.Version++; row.DecidedBy = actor; row.DecidedAt = clock.GetUtcNow();
                Audit(row, actor, "Verified", $"Patient:{row.PatientId}; Branch:{row.BranchId}; {row.SharingBasis}; {string.Join(',', scopes)}; {row.ExpiresAt:O}"); return row;
            }, ct);
    }

    public async Task<FamilyAccessDto> DecideAsync(int actor, Guid requestId, bool revoke, DecideFamilyAccess command, CancellationToken ct)
    {
        var initial = await ReviewRowAsync(actor, requestId, "patient-links.verify", ct);
        var reason = Text(command.DecisionReason, 500, "decisionReason", true);
        var publicNote = Text(command.PublicNote, 500, "publicNote");
        return await WriteAsync(actor, initial.OwnerAccountId, command.RequestKey, new { action = revoke ? "Revoke" : "Reject", requestId, command },
            async () => { await ReviewRowAsync(actor, requestId, "patient-links.verify", ct); }, async () => {
                var row = await db.Set<FamilyRecordAccess>().SingleAsync(x => x.Id == requestId, ct);
                if (row.Version != command.ExpectedVersion || row.Status != (revoke ? "Verified" : "Pending")) throw Stale();
                row.Status = revoke ? "Revoked" : "Rejected"; row.Version++; row.DecidedBy = actor; row.DecidedAt = clock.GetUtcNow();
                row.DecisionReason = reason; row.PublicNote = publicNote; Audit(row, actor, row.Status, reason); return row;
            }, ct);
    }

    // Called inside FamilyMemberService's owner lock and transaction.
    internal static async Task EndAccessAsync(DcmsDbContext db, int actor, Guid memberId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var row in await db.Set<FamilyRecordAccess>().Where(x => x.FamilyMemberId == memberId && (x.Status == "Pending" || x.Status == "Verified")).ToArrayAsync(ct)) {
            row.Status = row.Status == "Pending" ? "Withdrawn" : "Revoked"; row.Version++;
            row.DecidedAt = now; row.DecidedBy = actor; row.PublicNote = reason;
            db.Add(new FamilyRecordAudit { AccessId = row.Id, ActorAccountId = actor, Action = row.Status, Reason = reason, OccurredAt = now });
        }
    }
}
