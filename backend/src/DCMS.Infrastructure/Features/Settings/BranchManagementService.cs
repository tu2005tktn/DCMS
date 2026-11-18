using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Settings;

// docs/specs/S58-BRANCHES-AND-CHAIRS.md §1.5 & docs/specs/SCHEDULING-BRANCH-TABLE-SIMPLIFICATION.vi.md.
public sealed partial class BranchManagementService(DcmsDbContext db, ISettingsAccess access,
    IBranchLabObligationPort lab, TimeProvider clock) : IBranchManagementService
{
    private const string Lock = "SELECT pg_advisory_xact_lock(6462362099)";
    private static readonly string[] ClosedAppointmentStatuses = ["Cancelled", "NoShow", "Withdrawn"];
    private static readonly JsonSerializerOptions Json = ClinicSettingsStore.Json;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")] private static partial Regex CodePattern();
    [GeneratedRegex(@"^\+?[0-9 .()-]{1,20}$")] private static partial Regex PhonePattern();

    private static BusinessException Fail(string code, int status = 400, string? field = null, string label = "Thông tin", int? currentVersion = null, BlockerItem[]? blockers = null) =>
        new(status, SystemMessages.Get(code, label) with { Field = field, CurrentVersion = currentVersion, Blockers = blockers?.Cast<object>().ToArray() });

    private static string Hash(object payload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, Json)));

    private async Task<IDbContextTransaction> BeginAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(90403)", ct);
        await db.Database.ExecuteSqlRawAsync(Lock, ct);
        return tx;
    }

    private async Task<(bool Found, T? Result)> ReplayAsync<T>(int actor, Guid key, string hash, CancellationToken ct)
    {
        var prior = await db.Set<SettingsReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == key, ct);
        if (prior == null) return (false, default);
        if (prior.Fingerprint != hash) throw Fail("MSG111", 409);
        return (true, JsonSerializer.Deserialize<T>(prior.ResultJson, Json));
    }

    private void Record(int actor, int branchId, string target, int version, string action, string reason, string before, object result, Guid key, string hash)
    {
        var after = JsonSerializer.Serialize(result, Json);
        db.Add(new SettingsAudit
        {
            ActorId = actor, BranchIds = [branchId], Section = "branches", Target = target.Length > 100 ? target[..100] : target,
            Version = version, Action = action, Reason = reason, BeforeJson = before, AfterJson = after, At = clock.GetUtcNow()
        });
        if (key != Guid.Empty) db.Add(new SettingsReceipt { ActorId = actor, RequestKey = key, Fingerprint = hash, ResultJson = after });
    }

    private static string BranchCode(SchedulingBranch b) => string.IsNullOrWhiteSpace(b.Code) ? $"BR-{b.Id:D2}" : b.Code;
    private static string ChairCode(int id, BranchChair? c) => c?.Code ?? $"CHAIR-{id:D2}";
    private static int ChairVersion(SchedulingChair chair) => chair.Code == null ? 1 : chair.Version;

    private static ChairDto ChairDto(SchedulingChair s, BranchChair? c) =>
        new(s.Id, s.BranchId, ChairCode(s.Id, c), s.Name, s.OpenMinute, s.CloseMinute, s.Active, c?.Version ?? 1);

    private static ChairDto ChairDto(SchedulingChair chair) =>
        new(chair.Id, chair.BranchId, chair.Code ?? $"CHAIR-{chair.Id:D2}", chair.Name,
            chair.OpenMinute, chair.CloseMinute, chair.Active, ChairVersion(chair));

    // ---------- reads ----------

    public async Task<BranchDto[]> BranchesAsync(int actor, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var chairCounts = await db.SchedulingChairs.AsNoTracking().Where(c => c.Active).GroupBy(c => c.BranchId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        return branches.Select(s => new BranchDto(s.Id, BranchCode(s), s.Name, s.Address, s.Phone, s.Email,
            s.SlotIntervalMinutes, s.Active, s.Version, chairCounts.GetValueOrDefault(s.Id))).ToArray();
    }

    public async Task<BranchDetailDto> BranchAsync(int actor, int id, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        return await DetailAsync(id, ct);
    }

    private async Task<BranchDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var s = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw Fail("MSG113", 404);
        return new BranchDetailDto(s.Id, BranchCode(s), s.Name, s.Address, s.Phone, s.Email,
            s.SlotIntervalMinutes, s.Active, s.Version, await ChairRowsAsync(id, ct));
    }

    public async Task<ChairDto[]> ChairsAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.read", ct);
        if (!await db.Set<SchedulingBranch>().AnyAsync(x => x.Id == branchId, ct)) throw Fail("MSG113", 404);
        return await ChairRowsAsync(branchId, ct);
    }

    private async Task<ChairDto[]> ChairRowsAsync(int branchId, CancellationToken ct)
    {
        var chairs = await db.SchedulingChairs.AsNoTracking().Where(c => c.BranchId == branchId).ToListAsync(ct);
        var profiles = await db.BranchChairs.AsNoTracking().Where(c => c.BranchId == branchId).ToDictionaryAsync(c => c.Id, ct);
        return chairs.Select(c => ChairDto(c, profiles.GetValueOrDefault(c.Id))).OrderBy(c => c.Code).ThenBy(c => c.Id).ToArray();
    }

    // ---------- branch writes ----------

    private sealed record BranchValues(string Code, string Name, string? Address, string? Phone, string? Email);

    private static string? Optional(string? value, int max, string field, string label)
    {
        var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (v?.Length > max) throw Fail("MSG04", field: field, label: label);
        return v;
    }

    private static BranchValues Validate(SaveBranchCommand c)
    {
        var code = c.Code?.Trim() ?? "";
        if (code.Length == 0) throw Fail("MSG02", field: "code", label: "Mã chi nhánh");
        if (!CodePattern().IsMatch(code)) throw Fail("MSG04", field: "code", label: "Mã chi nhánh");
        var name = c.Name?.Trim() ?? "";
        if (name.Length == 0) throw Fail("MSG02", field: "name", label: "Tên chi nhánh");
        if (name.Length > 160) throw Fail("MSG04", field: "name", label: "Tên chi nhánh");
        var phone = Optional(c.Phone, 20, "phone", "Số điện thoại");
        if (phone != null && !PhonePattern().IsMatch(phone)) throw Fail("MSG04", field: "phone", label: "Số điện thoại");
        var email = Optional(c.Email, 254, "email", "Email");
        if (email != null && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)) throw Fail("MSG04", field: "email", label: "Email");
        return new(code, name, Optional(c.Address, 1000, "address", "Địa chỉ"), phone, email);
    }

    private async Task EnsureUniqueBranchCodeAsync(string code, int? selfId, CancellationToken ct)
    {
        var upper = code.ToUpperInvariant();
        if (await db.Set<SchedulingBranch>().AnyAsync(x => (!selfId.HasValue || x.Id != selfId.Value) && x.Code != null && x.Code.ToUpper() == upper, ct))
            throw Fail("MSG201", 409, "code", "chi nhánh");
        // Branches without a custom code still display their generated BR-xx code.
        var uncoded = await db.Set<SchedulingBranch>().Where(s => (!selfId.HasValue || s.Id != selfId.Value) && s.Code == null).Select(s => s.Id).ToListAsync(ct);
        if (uncoded.Any(id => $"BR-{id:D2}".Equals(code, StringComparison.OrdinalIgnoreCase))) throw Fail("MSG201", 409, "code", "chi nhánh");
    }

    public async Task<BranchDetailDto> SaveBranchAsync(int actor, int? id, SaveBranchCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var v = Validate(c);
        var hash = Hash(new { op = "branch.save", id, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<BranchDetailDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        SchedulingBranch sched;
        var before = "{}";
        if (id is { } branchId)
        {
            sched = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(b => b.Id == branchId, ct) ?? throw Fail("MSG113", 404);
            if (sched.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: sched.Version);
            before = JsonSerializer.Serialize(await DetailAsync(branchId, ct), Json);
            await EnsureUniqueBranchCodeAsync(v.Code, branchId, ct);
            sched.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            await EnsureUniqueBranchCodeAsync(v.Code, null, ct);
            sched = new SchedulingBranch { Active = true, Version = 1 };
            db.Add(sched);
        }

        sched.Code = v.Code;
        sched.Name = v.Name;
        sched.Address = v.Address;
        sched.Phone = v.Phone;
        sched.Email = v.Email;
        await db.SaveChangesAsync(ct);

        var result = await DetailAsync(sched.Id, ct);
        Record(actor, sched.Id, sched.Code!, sched.Version, id.HasValue ? "BranchUpdate" : "BranchCreate", "Lưu thông tin chi nhánh", before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<BlockerItem[]> BranchBlockersAsync(int id, CancellationToken ct)
    {
        var labOrders = await lab.OpenLabOrdersAsync(id, ct);
        if (!labOrders.Available) throw Fail("MSG144", 503);

        var now = clock.GetUtcNow();
        var blockers = new List<BlockerItem>();
        var appointments = await db.Set<Appointment>().AsNoTracking()
            .CountAsync(a => a.BranchId == id && a.EndAt > now && !ClosedAppointmentStatuses.Contains(a.Status), ct);
        if (appointments > 0)
            blockers.Add(new("Appointments", $"Còn {appointments} lịch hẹn tương lai chưa hủy tại chi nhánh.", appointments, $"/appointments?branchId={id}"));

        var visits = await db.Set<Visit>().AsNoTracking()
            .CountAsync(v => v.BranchId == id && (v.Status == VisitStatuses.Queued || v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.ExaminationConfirmed), ct);
        if (visits > 0)
            blockers.Add(new("Visits", $"Còn {visits} lượt khám đang chờ hoặc đang diễn ra.", visits, $"/reception?branchId={id}"));

        var dossiers = await db.Set<TreatmentDossier>().AsNoTracking().CountAsync(d => d.BranchId == id && d.State == "Open", ct);
        if (dossiers > 0)
            blockers.Add(new("TreatmentDossiers", $"Còn {dossiers} hồ sơ điều trị đang mở tại chi nhánh.", dossiers, "/patients"));

        if (labOrders.Count > 0)
            blockers.Add(new("LabOrders", $"Còn {labOrders.Count} phiếu labo chưa giao bệnh nhân.", labOrders.Count, "/lab-orders"));
        return blockers.ToArray();
    }

    private static ImpactPreviewResult Preview(BlockerItem[] blockers, string ok, string label) => new(blockers.Length == 0, blockers,
        blockers.Length == 0 ? ok : $"Không thể ngừng {label} vì còn {blockers.Length} nhóm nghĩa vụ cần xử lý trước. Hệ thống không tự hủy hay dời lịch.");

    public async Task<ImpactPreviewResult> BranchStatePreviewAsync(int actor, int id, bool targetActive, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (!await db.Set<SchedulingBranch>().AnyAsync(x => x.Id == id, ct)) throw Fail("MSG113", 404);
        if (targetActive) return new(true, [], "Kích hoạt lại chi nhánh để tiếp nhận lịch hẹn và bệnh nhân.");
        return Preview(await BranchBlockersAsync(id, ct), "Chi nhánh không còn nghĩa vụ ràng buộc. Có thể ngừng hoạt động.", "chi nhánh");
    }

    private static string StateReason(bool active, string? reason)
    {
        var r = reason?.Trim() ?? "";
        if (!active && r.Length == 0) throw Fail("MSG02", field: "reason", label: "Lý do");
        if (r.Length > 1000) throw Fail("MSG04", field: "reason", label: "Lý do");
        return r.Length > 0 ? r : "Khôi phục hoạt động";
    }

    public async Task<BranchDetailDto> SetBranchStateAsync(int actor, int id, BranchStateCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var reason = StateReason(c.Active, c.Reason);
        var hash = Hash(new { op = "branch.state", id, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<BranchDetailDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var sched = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw Fail("MSG113", 404);
        if (sched.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: sched.Version);
        if (!c.Active)
        {
            var blockers = await BranchBlockersAsync(id, ct);
            if (blockers.Length > 0) throw Fail("MSG205", 409, label: "Chi nhánh", blockers: blockers);
        }

        var before = JsonSerializer.Serialize(await DetailAsync(id, ct), Json);
        sched.Code ??= BranchCode(sched);
        sched.Active = c.Active;
        sched.Version++;
        await db.SaveChangesAsync(ct);

        var result = await DetailAsync(id, ct);
        Record(actor, id, sched.Code, sched.Version, c.Active ? "BranchRestore" : "BranchDeactivate", reason, before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---------- chairs ----------

    private static (string Code, string Name, int Open, int Close) Validate(SaveChairCommand c)
    {
        var code = c.Code?.Trim() ?? "";
        if (code.Length == 0) throw Fail("MSG02", field: "code", label: "Mã ghế");
        if (!CodePattern().IsMatch(code)) throw Fail("MSG04", field: "code", label: "Mã ghế");
        var name = c.Name?.Trim() ?? "";
        if (name.Length == 0) throw Fail("MSG02", field: "name", label: "Tên ghế");
        if (name.Length > 160) throw Fail("MSG04", field: "name", label: "Tên ghế");
        // Ghế chỉ là điều kiện sức chứa; giờ làm việc lấy từ lịch bác sĩ nên giờ mở/đóng luôn là 0–1440.
        return (code, name, 0, 1440);
    }

    private async Task EnsureUniqueChairCodeAsync(int branchId, string code, int? selfId, CancellationToken ct)
    {
        var upper = code.ToUpperInvariant();
        if (await db.BranchChairs.AnyAsync(x => x.BranchId == branchId && x.Code.ToUpper() == upper && x.Id != selfId, ct))
            throw Fail("MSG201", 409, "code", "ghế");
        var withoutProfile = await db.SchedulingChairs
            .Where(s => s.BranchId == branchId && s.Id != selfId && !db.BranchChairs.Any(p => p.Id == s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (withoutProfile.Any(id => ChairCode(id, null).Equals(code, StringComparison.OrdinalIgnoreCase))) throw Fail("MSG201", 409, "code", "ghế");
    }

    public async Task<ChairDto> SaveChairAsync(int actor, int branchId, int? chairId, SaveChairCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var v = Validate(c);
        var hash = Hash(new { op = "chair.save", branchId, chairId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<ChairDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw Fail("MSG113", 404);
        SchedulingChair chair;
        var before = "{}";
        if (chairId is { } id)
        {
            chair = branch.Chairs.SingleOrDefault(x => x.Id == id) ?? throw Fail("MSG113", 404);
            var current = ChairVersion(chair);
            if (current != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current);
            before = JsonSerializer.Serialize(ChairDto(chair), Json);
            await EnsureUniqueChairCodeAsync(branchId, v.Code, id, ct);
            chair.Version = current + 1;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            if (!branch.Active) throw Fail("MSG205", 409, "branchId");
            await EnsureUniqueChairCodeAsync(branchId, v.Code, null, ct);
            chair = new SchedulingChair { Id = await db.NextChairIdAsync(ct), BranchId = branchId };
            branch.Chairs.Add(chair);
        }
        chair.Code = v.Code; chair.Name = v.Name;
        chair.OpenMinute = v.Open; chair.CloseMinute = v.Close;
        var result = ChairDto(chair);
        Record(actor, branchId, $"CHAIR:{chair.Code}", chair.Version, chairId.HasValue ? "ChairUpdate" : "ChairCreate", "Lưu cấu hình ghế", before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<BlockerItem[]> ChairBlockersAsync(int branchId, int chairId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var blockers = new List<BlockerItem>();
        var appointments = await db.Set<Appointment>().AsNoTracking()
            .CountAsync(a => a.BranchId == branchId && a.ChairId == chairId && a.EndAt > now && !ClosedAppointmentStatuses.Contains(a.Status), ct);
        if (appointments > 0)
            blockers.Add(new("Appointments", $"Ghế còn {appointments} lịch hẹn tương lai chưa hủy.", appointments, $"/appointments?branchId={branchId}"));
        var visits = await (from v in db.Set<Visit>().AsNoTracking()
                            join a in db.Set<Appointment>().AsNoTracking() on v.AppointmentId equals a.Id
                            where v.BranchId == branchId && a.ChairId == chairId && (v.Status == VisitStatuses.Queued || v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.ExaminationConfirmed)
                            select v.Id).CountAsync(ct);
        if (visits > 0)
            blockers.Add(new("Visits", $"Ghế đang có {visits} lượt khám chờ hoặc đang điều trị.", visits, $"/reception?branchId={branchId}"));
        return blockers.ToArray();
    }

    public async Task<ImpactPreviewResult> ChairStatePreviewAsync(int actor, int branchId, int chairId, bool targetActive, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (!await db.SchedulingChairs.AnyAsync(x => x.Id == chairId && x.BranchId == branchId, ct)) throw Fail("MSG113", 404);
        if (targetActive)
        {
            var branchActive = await db.Set<SchedulingBranch>().Where(x => x.Id == branchId).Select(x => x.Active).SingleAsync(ct);
            return branchActive
                ? new(true, [], "Kích hoạt lại ghế để phân bổ lịch hẹn.")
                : new(false, [new("Branch", "Chi nhánh đang ngừng hoạt động; khôi phục chi nhánh trước.", 1, "/settings/branches")], "Không thể khôi phục ghế khi chi nhánh đang ngừng hoạt động.");
        }
        return Preview(await ChairBlockersAsync(branchId, chairId, ct), "Ghế không còn lịch hẹn, lượt khám hay giữ chỗ. Có thể tạm ngừng.", "ghế");
    }

    public async Task<ChairDto> SetChairStateAsync(int actor, int branchId, int chairId, ChairStateCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var reason = StateReason(c.Active, c.Reason);
        var hash = Hash(new { op = "chair.state", branchId, chairId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<ChairDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var chair = await db.LoadChairAsync(branchId, chairId, ct) ?? throw Fail("MSG113", 404);
        var current = ChairVersion(chair);
        if (current != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current);
        if (c.Active)
        {
            if (!await db.Set<SchedulingBranch>().Where(x => x.Id == branchId).Select(x => x.Active).SingleAsync(ct))
                throw Fail("MSG205", 409, "branchId", "Chi nhánh",
                    blockers: [new("Branch", "Chi nhánh đang ngừng hoạt động; khôi phục chi nhánh trước.", 1, "/settings/branches")]);
        }
        else
        {
            var blockers = await ChairBlockersAsync(branchId, chairId, ct);
            if (blockers.Length > 0) throw Fail("MSG205", 409, label: "Ghế", blockers: blockers);
        }

        var before = JsonSerializer.Serialize(ChairDto(chair), Json);
        chair.Code ??= $"CHAIR-{chair.Id:D2}";
        chair.Active = c.Active;
        chair.Version = current + 1;
        var result = ChairDto(chair);
        Record(actor, branchId, $"CHAIR:{chair.Code}", chair.Version, c.Active ? "ChairRestore" : "ChairDeactivate", reason, before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

public sealed class BranchLabObligationPort(DcmsDbContext db) : IBranchLabObligationPort
{
    public async Task<BranchObligationResult> OpenLabOrdersAsync(int branchId, CancellationToken ct) =>
        new(true, await (
            from x in db.LabOrders.AsNoTracking()
            join a in db.ClinicalActivities.AsNoTracking() on x.ActivityId equals a.Id
            join d in db.TreatmentDossiers.AsNoTracking() on a.DossierId equals d.Id
            where d.BranchId == branchId && x.Status != LabOrderStatus.Delivered
            select x.Id
        ).CountAsync(ct));
}
