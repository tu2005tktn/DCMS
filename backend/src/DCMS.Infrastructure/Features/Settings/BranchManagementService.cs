using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Finance;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Settings;

// docs/specs/S58-BRANCHES-AND-CHAIRS.md §1.5. SCHEDULING_* rows stay the source for booking; profiles share their keys.
public sealed partial class BranchManagementService(DcmsDbContext db, ISettingsAccess access, BranchAssetStore assets,
    IBranchLabObligationPort lab, TimeProvider clock) : IBranchManagementService
{
    private const string Lock = "SELECT pg_advisory_xact_lock(6462362099)";
    private static readonly string[] ClosedAppointmentStatuses = ["Cancelled", "NoShow", "Withdrawn"];
    private static readonly JsonSerializerOptions Json = ClinicSettingsStore.Json;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")] private static partial Regex CodePattern();
    [GeneratedRegex("^[0-9]{1,50}$")] private static partial Regex AccountPattern();
    [GeneratedRegex(@"^\+?[0-9 .()-]{1,20}$")] private static partial Regex PhonePattern();

    private static BusinessException Fail(string code, int status = 400, string? field = null, string label = "Thông tin", int? currentVersion = null, BlockerItem[]? blockers = null) =>
        new(status, SystemMessages.Get(code, label) with { Field = field, CurrentVersion = currentVersion, Blockers = blockers?.Cast<object>().ToArray() });

    private static string Hash(object payload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, Json)));

    private async Task<IDbContextTransaction> BeginAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
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

    private static string BranchCode(int id, BranchProfile? p) => p?.Code ?? $"BR-{id:D2}";
    private static string ChairCode(int id, BranchChair? c) => c?.Code ?? $"CHAIR-{id:D2}";

    private static ChairDto ChairDto(SchedulingChair s, BranchChair? c) =>
        new(s.Id, s.BranchId, ChairCode(s.Id, c), s.Name, s.OpenMinute, s.CloseMinute, s.Active, c?.Version ?? 1);

    // ---------- reads ----------

    public async Task<BranchDto[]> BranchesAsync(int actor, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var profiles = await db.Set<BranchProfile>().AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var chairCounts = await db.Set<SchedulingChair>().AsNoTracking().Where(c => c.Active).GroupBy(c => c.BranchId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        return branches.Select(s =>
        {
            var p = profiles.GetValueOrDefault(s.Id);
            return new BranchDto(s.Id, BranchCode(s.Id, p), s.Name, p?.Address, p?.Phone, p?.Email, p?.Website, p?.LogoAssetId,
                p?.TimeZoneId ?? "Asia/Ho_Chi_Minh", p?.BankCode, p?.AccountNumber, p?.PayeeName,
                s.SlotIntervalMinutes, s.BookingSettingsVersion, s.Active, p?.Version ?? 1, chairCounts.GetValueOrDefault(s.Id));
        }).ToArray();
    }

    public async Task<BranchDetailDto> BranchAsync(int actor, int id, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        return await DetailAsync(id, ct);
    }

    private async Task<BranchDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var s = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw Fail("MSG113", 404);
        var p = await db.Set<BranchProfile>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct);
        return new BranchDetailDto(s.Id, BranchCode(s.Id, p), s.Name, p?.Address, p?.Phone, p?.Email, p?.Website, p?.LogoAssetId,
            p?.TimeZoneId ?? "Asia/Ho_Chi_Minh", p?.BankCode, p?.AccountNumber, p?.PayeeName,
            s.SlotIntervalMinutes, s.BookingSettingsVersion, s.Active, p?.Version ?? 1, await ChairRowsAsync(id, ct));
    }

    public async Task<ChairDto[]> ChairsAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.read", ct);
        if (!await db.Set<SchedulingBranch>().AnyAsync(x => x.Id == branchId, ct)) throw Fail("MSG113", 404);
        return await ChairRowsAsync(branchId, ct);
    }

    private async Task<ChairDto[]> ChairRowsAsync(int branchId, CancellationToken ct)
    {
        var chairs = await db.Set<SchedulingChair>().AsNoTracking().Where(c => c.BranchId == branchId).ToListAsync(ct);
        var profiles = await db.Set<BranchChair>().AsNoTracking().Where(c => c.BranchId == branchId).ToDictionaryAsync(c => c.Id, ct);
        return chairs.Select(c => ChairDto(c, profiles.GetValueOrDefault(c.Id))).OrderBy(c => c.Code).ThenBy(c => c.Id).ToArray();
    }

    // ---------- branch writes ----------

    private sealed record BranchValues(string Code, string Name, string? Address, string? Phone, string? Email, string? Website,
        string TimeZoneId, string? BankCode, string? AccountNumber, string? PayeeName);

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
        var website = Optional(c.Website, 255, "website", "Website");
        if (website != null && (!Uri.TryCreate(website, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw Fail("MSG04", field: "website", label: "Website");
        var tz = string.IsNullOrWhiteSpace(c.TimeZoneId) ? "Asia/Ho_Chi_Minh" : c.TimeZoneId.Trim();
        if (tz.Length > 64 || !TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _)) throw Fail("MSG04", field: "timeZoneId", label: "Múi giờ");
        var bank = Optional(c.BankCode, 20, "bankCode", "Mã ngân hàng");
        var account = Optional(c.AccountNumber, 50, "accountNumber", "Số tài khoản");
        if (account != null && !AccountPattern().IsMatch(account)) throw Fail("MSG04", field: "accountNumber", label: "Số tài khoản");
        var payee = Optional(c.PayeeName, 160, "payeeName", "Tên chủ tài khoản");
        var filled = new[] { bank, account, payee }.Count(x => x != null);
        if (filled is not (0 or 3)) throw Fail("MSG02", field: bank == null ? "bankCode" : account == null ? "accountNumber" : "payeeName", label: "Tài khoản nhận chuyển khoản");
        return new(code, name, Optional(c.Address, 1000, "address", "Địa chỉ"), phone, email, website, tz, bank?.ToUpperInvariant(), account, payee);
    }

    private async Task EnsureUniqueBranchCodeAsync(string code, int? selfId, CancellationToken ct)
    {
        var upper = code.ToUpperInvariant();
        if (await db.Set<BranchProfile>().AnyAsync(x => x.Code.ToUpper() == upper && x.Id != selfId, ct)) throw Fail("MSG201", 409, "code", "chi nhánh");
        // Branches without a profile still display their generated BR-xx code.
        var withoutProfile = await db.Set<SchedulingBranch>().Where(s => s.Id != selfId && !db.Set<BranchProfile>().Any(p => p.Id == s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (withoutProfile.Any(id => BranchCode(id, null).Equals(code, StringComparison.OrdinalIgnoreCase))) throw Fail("MSG201", 409, "code", "chi nhánh");
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
        BranchProfile profile;
        var before = "{}";
        if (id is { } branchId)
        {
            sched = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(b => b.Id == branchId, ct) ?? throw Fail("MSG113", 404);
            var existing = await db.Set<BranchProfile>().SingleOrDefaultAsync(b => b.Id == branchId, ct);
            var current = existing?.Version ?? 1;
            if (current != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current);
            before = JsonSerializer.Serialize(await DetailAsync(branchId, ct), Json);
            await EnsureUniqueBranchCodeAsync(v.Code, branchId, ct);
            profile = existing ?? new BranchProfile { Id = branchId, Version = 1, IsActive = sched.Active };
            if (existing == null) db.Add(profile);
            profile.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            if (c.LogoAssetId.HasValue) throw Fail("MSG113", 404, "logoAssetId", "Logo");
            await EnsureUniqueBranchCodeAsync(v.Code, null, ct);
            sched = new SchedulingBranch { Name = v.Name, Active = true };
            db.Add(sched);
            await db.SaveChangesAsync(ct);
            profile = new BranchProfile { Id = sched.Id, Version = 1, IsActive = true };
            db.Add(profile);
        }

        if (c.LogoAssetId is { } logo && logo != profile.LogoAssetId
            && !await db.Set<BranchAsset>().AnyAsync(x => x.Id == logo && x.BranchId == profile.Id, ct))
            throw Fail("MSG113", 404, "logoAssetId", "Logo");

        profile.Code = v.Code;
        profile.Name = v.Name;
        profile.Address = v.Address;
        profile.Phone = v.Phone;
        profile.Email = v.Email;
        profile.Website = v.Website;
        profile.LogoAssetId = c.LogoAssetId;
        profile.TimeZoneId = v.TimeZoneId;
        profile.BankCode = v.BankCode;
        profile.AccountNumber = v.AccountNumber;
        profile.PayeeName = v.PayeeName;
        profile.SlotIntervalMinutes = sched.SlotIntervalMinutes;
        profile.BookingSettingsVersion = sched.BookingSettingsVersion;
        sched.Name = v.Name;
        await db.SaveChangesAsync(ct);

        var result = await DetailAsync(profile.Id, ct);
        Record(actor, profile.Id, profile.Code, profile.Version, id.HasValue ? "BranchUpdate" : "BranchCreate", "Lưu thông tin chi nhánh", before, result, c.RequestKey, hash);
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
            .CountAsync(a => a.BranchId == id && a.StartAt >= now && !ClosedAppointmentStatuses.Contains(a.Status), ct);
        if (appointments > 0)
            blockers.Add(new("Appointments", $"Còn {appointments} lịch hẹn tương lai chưa hủy tại chi nhánh.", appointments, $"/appointments?branchId={id}"));

        var visits = await db.Set<Visit>().AsNoTracking()
            .CountAsync(v => v.BranchId == id && (v.Status == VisitStatuses.Queued || v.Status == VisitStatuses.InProgress), ct);
        if (visits > 0)
            blockers.Add(new("Visits", $"Còn {visits} lượt khám đang chờ hoặc đang diễn ra.", visits, $"/reception?branchId={id}"));

        var reservations = await db.Set<ResourceReservation>().AsNoTracking()
            .CountAsync(r => r.BranchId == id && r.End > now && r.Status == "Confirmed", ct);
        if (reservations > 0)
            blockers.Add(new("Reservations", $"Còn {reservations} giữ chỗ ghế/bác sĩ chưa kết thúc.", reservations, $"/appointments?branchId={id}"));

        var pendingReceipts = await db.Set<PaymentReceipt>().AsNoTracking().CountAsync(r => r.BranchId == id && r.State == "Pending", ct);
        var outstanding = await db.Set<FinanceCharge>().AsNoTracking()
            .Where(ch => ch.BranchId == id)
            .CountAsync(ch => ch.AmountVnd > (db.Set<PaymentAllocation>()
                .Where(a => a.ChargeId == ch.Id && db.Set<PaymentReceipt>().Any(r => r.Id == a.PaymentId && r.State == "Posted"))
                .Sum(a => (long?)a.AmountVnd) ?? 0), ct);
        if (pendingReceipts + outstanding > 0)
            blockers.Add(new("Finance", $"Còn {outstanding} khoản phí chưa thu đủ và {pendingReceipts} phiếu thu chờ xác nhận.", pendingReceipts + outstanding, $"/payments?branchId={id}"));

        var payOs = await db.Set<DossierPayOsOrder>().AsNoTracking()
            .CountAsync(o => o.BranchId == id && (o.State == "Creating" || o.State == "Pending" || o.State == "Review"), ct);
        if (payOs > 0)
            blockers.Add(new("PayOsOrders", $"Còn {payOs} đơn chuyển khoản PayOS chưa hoàn tất.", payOs, $"/payments?branchId={id}"));

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
        var profile = await db.Set<BranchProfile>().SingleOrDefaultAsync(b => b.Id == id, ct);
        var current = profile?.Version ?? 1;
        if (current != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current);
        if (!c.Active)
        {
            var blockers = await BranchBlockersAsync(id, ct);
            if (blockers.Length > 0) throw Fail("MSG205", 409, label: "Chi nhánh", blockers: blockers);
        }

        var before = JsonSerializer.Serialize(await DetailAsync(id, ct), Json);
        if (profile == null)
        {
            profile = new BranchProfile { Id = id, Code = BranchCode(id, null), Name = sched.Name, Version = 1,
                SlotIntervalMinutes = sched.SlotIntervalMinutes, BookingSettingsVersion = sched.BookingSettingsVersion };
            db.Add(profile);
        }
        profile.IsActive = c.Active;
        profile.Version++;
        sched.Active = c.Active;
        await db.SaveChangesAsync(ct);

        var result = await DetailAsync(id, ct);
        Record(actor, id, profile.Code, profile.Version, c.Active ? "BranchRestore" : "BranchDeactivate", reason, before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---------- logo ----------

    public async Task<BranchLogoUploadResult> UploadLogoAsync(int actor, int id, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (!await db.Set<SchedulingBranch>().AnyAsync(x => x.Id == id, ct)) throw Fail("MSG113", 404);
        if (length is <= 0 or > BranchAssetStore.MaxBytes) throw Fail("MSG04", field: "file", label: "Tệp logo");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await fileStream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > BranchAssetStore.MaxBytes) throw Fail("MSG04", field: "file", label: "Tệp logo");
        }
        var bytes = buffer.ToArray();
        var detected = BranchAssetStore.Detect(bytes) ?? throw Fail("MSG04", field: "file", label: "Định dạng logo");
        var declared = contentType?.Split(';')[0].Trim().ToLowerInvariant() switch { "image/jpg" => "image/jpeg", var x => x };
        if (declared != detected) throw Fail("MSG04", field: "file", label: "Định dạng logo");
        var cleaned = BranchAssetStore.StripMetadata(bytes, detected) ?? throw Fail("MSG04", field: "file", label: "Tệp logo");

        var asset = new BranchAsset
        {
            BranchId = id, ContentType = detected, SizeBytes = cleaned.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(cleaned)), UploadedBy = actor, UploadedAt = clock.GetUtcNow()
        };
        await assets.WriteAsync(asset.Id, cleaned, ct);
        try
        {
            await using var tx = await BeginAsync(ct);
            db.Add(asset);
            var safeName = Path.GetFileName(fileName ?? "");
            var result = new BranchLogoUploadResult(asset.Id, safeName.Length > 200 ? safeName[..200] : safeName, cleaned.Length, detected);
            var profile = await db.Set<BranchProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            Record(actor, id, BranchCode(id, profile), profile?.Version ?? 1, "LogoUpload", "Tải logo chi nhánh", "{}", result, Guid.Empty, "");
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            assets.Discard(asset.Id);
            throw;
        }
    }

    public async Task<BranchLogoFile> LogoAsync(int actor, int id, Guid assetId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var found = await assets.ReadAsync(id, assetId, ct) ?? throw Fail("MSG113", 404);
        return new BranchLogoFile(found.Asset.ContentType, found.Bytes);
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
        if (await db.Set<BranchChair>().AnyAsync(x => x.BranchId == branchId && x.Code.ToUpper() == upper && x.Id != selfId, ct))
            throw Fail("MSG201", 409, "code", "ghế");
        var withoutProfile = await db.Set<SchedulingChair>()
            .Where(s => s.BranchId == branchId && s.Id != selfId && !db.Set<BranchChair>().Any(p => p.Id == s.Id)).Select(s => s.Id).ToListAsync(ct);
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

        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw Fail("MSG113", 404);
        SchedulingChair sched;
        BranchChair chair;
        var before = "{}";
        if (chairId is { } id)
        {
            sched = await db.Set<SchedulingChair>().SingleOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct) ?? throw Fail("MSG113", 404);
            var existing = await db.Set<BranchChair>().SingleOrDefaultAsync(x => x.Id == id, ct);
            var current = existing?.Version ?? 1;
            if (current != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current);
            before = JsonSerializer.Serialize(ChairDto(sched, existing), Json);
            await EnsureUniqueChairCodeAsync(branchId, v.Code, id, ct);
            chair = existing ?? new BranchChair { Id = id, BranchId = branchId, Version = 1, IsActive = sched.Active };
            if (existing == null) db.Add(chair);
            chair.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            if (!branch.Active) throw Fail("MSG205", 409, "branchId", "Chi nhánh");
            await EnsureUniqueChairCodeAsync(branchId, v.Code, null, ct);
            sched = new SchedulingChair { BranchId = branchId, Name = v.Name, OpenMinute = v.Open, CloseMinute = v.Close, Active = true };
            db.Add(sched);
            await db.SaveChangesAsync(ct);
            chair = new BranchChair { Id = sched.Id, BranchId = branchId, Version = 1, IsActive = true };
            db.Add(chair);
        }
        chair.Code = v.Code;
        chair.Name = v.Name;
        chair.OpenMinute = v.Open;
        chair.CloseMinute = v.Close;
        sched.Name = v.Name;
        sched.OpenMinute = v.Open;
        sched.CloseMinute = v.Close;
        await db.SaveChangesAsync(ct);

        var result = ChairDto(sched, chair);
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
            .CountAsync(a => a.BranchId == branchId && a.ChairId == chairId && a.StartAt >= now && !ClosedAppointmentStatuses.Contains(a.Status), ct);
        if (appointments > 0)
            blockers.Add(new("Appointments", $"Ghế còn {appointments} lịch hẹn tương lai chưa hủy.", appointments, $"/appointments?branchId={branchId}"));
        var visits = await (from v in db.Set<Visit>().AsNoTracking()
                            join a in db.Set<Appointment>().AsNoTracking() on v.AppointmentId equals a.Id
                            where v.BranchId == branchId && a.ChairId == chairId && (v.Status == VisitStatuses.Queued || v.Status == VisitStatuses.InProgress)
                            select v.Id).CountAsync(ct);
        if (visits > 0)
            blockers.Add(new("Visits", $"Ghế đang có {visits} lượt khám chờ hoặc đang điều trị.", visits, $"/reception?branchId={branchId}"));
        var reservations = await db.Set<ResourceReservation>().AsNoTracking()
            .CountAsync(r => r.BranchId == branchId && r.ChairId == chairId && r.End > now && r.Status == "Confirmed", ct);
        if (reservations > 0)
            blockers.Add(new("Reservations", $"Ghế còn {reservations} giữ chỗ chưa kết thúc.", reservations, $"/appointments?branchId={branchId}"));
        return blockers.ToArray();
    }

    public async Task<ImpactPreviewResult> ChairStatePreviewAsync(int actor, int branchId, int chairId, bool targetActive, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (!await db.Set<SchedulingChair>().AnyAsync(x => x.Id == chairId && x.BranchId == branchId, ct)) throw Fail("MSG113", 404);
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

        var sched = await db.Set<SchedulingChair>().SingleOrDefaultAsync(x => x.Id == chairId && x.BranchId == branchId, ct) ?? throw Fail("MSG113", 404);
        var chair = await db.Set<BranchChair>().SingleOrDefaultAsync(x => x.Id == chairId, ct);
        var current = chair?.Version ?? 1;
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

        var before = JsonSerializer.Serialize(ChairDto(sched, chair), Json);
        if (chair == null)
        {
            chair = new BranchChair { Id = chairId, BranchId = branchId, Code = ChairCode(chairId, null), Name = sched.Name,
                OpenMinute = sched.OpenMinute, CloseMinute = sched.CloseMinute, Version = 1 };
            db.Add(chair);
        }
        chair.IsActive = c.Active;
        chair.Version++;
        sched.Active = c.Active;
        await db.SaveChangesAsync(ct);

        var result = ChairDto(sched, chair);
        Record(actor, branchId, $"CHAIR:{chair.Code}", chair.Version, c.Active ? "ChairRestore" : "ChairDeactivate", reason, before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

public sealed class BranchLabObligationPort(DcmsDbContext db) : IBranchLabObligationPort
{
    public async Task<BranchObligationResult> OpenLabOrdersAsync(int branchId, CancellationToken ct) =>
        new(true, await db.LabOrders.AsNoTracking().CountAsync(x => x.BranchId == branchId && x.Status != LabOrderStatus.Delivered, ct));
}
