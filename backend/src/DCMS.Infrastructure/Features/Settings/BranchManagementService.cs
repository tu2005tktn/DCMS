using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Settings;

public sealed class BranchManagementService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IBranchManagementService
{
    private static ChairDto ToChairDto(BranchChair c) => new(
        c.Id, c.BranchId, c.Code, c.Name, c.OpenMinute, c.CloseMinute, c.IsActive, c.Version);

    private static ChairDto ToChairDto(SchedulingChair c, int version = 1) => new(
        c.Id, c.BranchId, $"CHAIR-{c.Id:D2}", c.Name, c.OpenMinute, c.CloseMinute, c.Active, version);

    public async Task<BranchDto[]> BranchesAsync(int actor, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var profiles = await db.Set<BranchProfile>().AsNoTracking().ToListAsync(ct);
        var scheduling = await db.Set<SchedulingBranch>().AsNoTracking().ToListAsync(ct);
        var chairCounts = await db.Set<SchedulingChair>().AsNoTracking()
            .Where(c => c.Active).GroupBy(c => c.BranchId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        // Merge profiles with scheduling branches
        var result = new List<BranchDto>();
        var seenIds = new HashSet<int>();

        foreach (var p in profiles)
        {
            seenIds.Add(p.Id);
            result.Add(new BranchDto(
                p.Id, p.Code, p.Name, p.Address, p.Phone, p.Email, p.Website,
                p.LogoAssetId, p.TimeZoneId, p.BankCode, p.AccountNumber, p.PayeeName,
                p.SlotIntervalMinutes, p.BookingSettingsVersion, p.IsActive, p.Version,
                chairCounts.GetValueOrDefault(p.Id, 0)));
        }

        foreach (var s in scheduling.Where(s => !seenIds.Contains(s.Id)))
        {
            result.Add(new BranchDto(
                s.Id, $"BR-{s.Id:D2}", s.Name, null, null, null, null,
                null, "Asia/Ho_Chi_Minh", null, null, null,
                s.SlotIntervalMinutes, s.BookingSettingsVersion, s.Active, 1,
                chairCounts.GetValueOrDefault(s.Id, 0)));
        }

        return result.OrderBy(x => x.Id).ToArray();
    }

    public async Task<BranchDetailDto> BranchAsync(int actor, int id, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var profile = await db.Set<BranchProfile>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct);
        var sched = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct);
        if (profile == null && sched == null) throw Error("MSG113", 404);

        var chairs = await db.Set<BranchChair>().AsNoTracking().Where(c => c.BranchId == id).ToListAsync(ct);
        var schedChairs = await db.Set<SchedulingChair>().AsNoTracking().Where(c => c.BranchId == id).ToListAsync(ct);

        var chairDtos = new List<ChairDto>();
        var seenChairIds = new HashSet<int>();
        foreach (var c in chairs)
        {
            seenChairIds.Add(c.Id);
            chairDtos.Add(ToChairDto(c));
        }
        foreach (var sc in schedChairs.Where(sc => !seenChairIds.Contains(sc.Id)))
        {
            chairDtos.Add(ToChairDto(sc));
        }

        var code = profile?.Code ?? $"BR-{id:D2}";
        var name = profile?.Name ?? sched?.Name ?? "Chi nhánh";
        var addr = profile?.Address;
        var phone = profile?.Phone;
        var email = profile?.Email;
        var web = profile?.Website;
        var logo = profile?.LogoAssetId;
        var tz = profile?.TimeZoneId ?? "Asia/Ho_Chi_Minh";
        var bank = profile?.BankCode;
        var acc = profile?.AccountNumber;
        var payee = profile?.PayeeName;
        var slot = profile?.SlotIntervalMinutes ?? sched?.SlotIntervalMinutes ?? 15;
        var bVer = profile?.BookingSettingsVersion ?? sched?.BookingSettingsVersion ?? 1;
        var active = profile?.IsActive ?? sched?.Active ?? true;
        var ver = profile?.Version ?? 1;

        return new BranchDetailDto(id, code, name, addr, phone, email, web, logo, tz, bank, acc, payee, slot, bVer, active, ver, chairDtos.OrderBy(c => c.Code).ToArray());
    }

    public async Task<BranchDetailDto> SaveBranchAsync(int actor, int? id, SaveBranchCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (c.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(c.Code) || string.IsNullOrWhiteSpace(c.Name))
            throw Error("MSG02", label: "Thông tin chi nhánh", field: "code");

        if (c.Code.Length > 40 || c.Name.Length > 160 || (c.Address?.Length > 1000) || (c.Phone?.Length > 20) || (c.Email?.Length > 254) || (c.Website?.Length > 255))
            throw Error("MSG04");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362099)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<SettingsReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<BranchDetailDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        BranchProfile profile;
        SchedulingBranch sched;
        string beforeJson = "{}";

        if (id.HasValue)
        {
            profile = await db.Set<BranchProfile>().SingleOrDefaultAsync(b => b.Id == id.Value, ct)
                ?? new BranchProfile { Id = id.Value };
            sched = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(b => b.Id == id.Value, ct)
                ?? new SchedulingBranch { Id = id.Value };

            if (profile.Id != 0 && profile.Version != c.ExpectedVersion) throw Error("MSG111", 409);

            beforeJson = JsonSerializer.Serialize(profile, ClinicSettingsStore.Json);
            profile.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Error("MSG111", 409);
            var codeExists = await db.Set<BranchProfile>().AnyAsync(b => b.Code == c.Code.Trim(), ct);
            if (codeExists) throw Error("MSG201", 409); // Unique branch code

            profile = new BranchProfile();
            sched = new SchedulingBranch();
            db.Add(profile);
            db.Add(sched);
        }

        profile.Code = c.Code.Trim();
        profile.Name = c.Name.Trim();
        profile.Address = c.Address?.Trim();
        profile.Phone = c.Phone?.Trim();
        profile.Email = c.Email?.Trim();
        profile.Website = c.Website?.Trim();
        profile.LogoAssetId = c.LogoAssetId;
        profile.TimeZoneId = string.IsNullOrWhiteSpace(c.TimeZoneId) ? "Asia/Ho_Chi_Minh" : c.TimeZoneId.Trim();
        profile.BankCode = c.BankCode?.Trim();
        profile.AccountNumber = c.AccountNumber?.Trim();
        profile.PayeeName = c.PayeeName?.Trim();

        sched.Name = profile.Name;
        sched.Active = profile.IsActive;

        await db.SaveChangesAsync(ct);

        // Keep ID synchronized if it was a new insert
        if (!id.HasValue)
        {
            sched.Id = profile.Id;
            await db.SaveChangesAsync(ct);
        }

        var result = await BranchAsync(actor, profile.Id, ct);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new SettingsAudit
        {
            ActorId = actor,
            BranchIds = [profile.Id],
            Section = "branches",
            Target = profile.Code,
            Version = profile.Version,
            Action = id.HasValue ? "BranchUpdate" : "BranchCreate",
            Reason = "Lưu thông tin chi nhánh",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new SettingsReceipt
        {
            ActorId = actor,
            RequestKey = c.RequestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ImpactPreviewResult> BranchStatePreviewAsync(int actor, int id, bool targetActive, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (targetActive)
        {
            return new ImpactPreviewResult(true, [], "Kích hoạt lại chi nhánh để tiếp nhận lịch hẹn và bệnh nhân bình thường.");
        }

        var blockers = new List<BlockerItem>();
        var now = clock.GetUtcNow();

        // 1. Check future appointments
        var futureAppts = await db.Set<Appointment>().AsNoTracking()
            .CountAsync(a => a.BranchId == id && a.Start >= now && a.Status != "Cancelled", ct);
        if (futureAppts > 0)
        {
            blockers.Add(new BlockerItem("Appointments",
                $"Còn {futureAppts} lịch hẹn trong tương lai chưa hoàn thành hoặc chưa hủy.",
                futureAppts, $"/appointments?branchId={id}"));
        }

        // 2. Check in-progress visits
        var inProgressVisits = await db.Set<Visit>().AsNoTracking()
            .CountAsync(v => v.BranchId == id && v.Status == "InProgress", ct);
        if (inProgressVisits > 0)
        {
            blockers.Add(new BlockerItem("Visits",
                $"Còn {inProgressVisits} lượt khám đang diễn ra tại chi nhánh.",
                inProgressVisits, $"/reception?branchId={id}"));
        }

        // 3. Check active resource reservations
        var activeReservations = await db.Set<ResourceReservation>().AsNoTracking()
            .CountAsync(r => r.BranchId == id && r.Start >= now && r.Status == "Confirmed", ct);
        if (activeReservations > 0)
        {
            blockers.Add(new BlockerItem("Reservations",
                $"Còn {activeReservations} phiếu giữ chỗ ghế/bác sĩ chưa kết thúc.",
                activeReservations, $"/working-schedules?branchId={id}"));
        }

        var allowed = blockers.Count == 0;
        var impact = allowed
            ? "Chi nhánh không còn lịch hẹn tương lai hoặc lượt khám dở dang. An toàn để ngừng hoạt động."
            : $"Không thể ngừng hoạt động chi nhánh vì còn {blockers.Count} nghĩa vụ ràng buộc cần giải quyết.";

        return new ImpactPreviewResult(allowed, blockers.ToArray(), impact);
    }

    public async Task<BranchDetailDto> SetBranchStateAsync(int actor, int id, BranchStateCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Error("MSG04");

        if (!c.Active)
        {
            var preview = await BranchStatePreviewAsync(actor, id, false, ct);
            if (!preview.Allowed)
            {
                var blockerSummary = string.Join("; ", preview.Blockers.Select(b => b.Message));
                throw new BusinessException(409, $"MSG205: {blockerSummary}");
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362099)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<SettingsReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<BranchDetailDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var profile = await db.Set<BranchProfile>().SingleOrDefaultAsync(b => b.Id == id, ct);
        var sched = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(b => b.Id == id, ct);
        if (profile == null && sched == null) throw Error("MSG113", 404);

        if (profile != null && profile.Version != c.ExpectedVersion) throw Error("MSG111", 409);

        var beforeJson = profile != null ? JsonSerializer.Serialize(profile, ClinicSettingsStore.Json) : "{}";
        if (profile != null)
        {
            profile.IsActive = c.Active;
            profile.Version++;
        }
        if (sched != null)
        {
            sched.Active = c.Active;
        }

        var result = await BranchAsync(actor, id, ct);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new SettingsAudit
        {
            ActorId = actor,
            BranchIds = [id],
            Section = "branches",
            Target = profile?.Code ?? $"BR-{id}",
            Version = profile?.Version ?? 1,
            Action = "BranchState",
            Reason = c.Reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new SettingsReceipt
        {
            ActorId = actor,
            RequestKey = c.RequestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<BranchLogoUploadResult> UploadLogoAsync(int actor, int id, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);

        // 1. Size check <= 2 MiB
        const long maxBytes = 2 * 1024 * 1024;
        if (length is <= 0 or > maxBytes)
            throw Error("MSG04");

        // 2. Read first 16 bytes for magic bytes verification
        var header = new byte[16];
        int bytesRead = await fileStream.ReadAsync(header.AsMemory(0, 16), ct);
        if (bytesRead < 4) throw Error("MSG04");

        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool isWebp = bytesRead >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                      && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        if (!isPng && !isJpeg && !isWebp)
            throw new BusinessException(400, "MSG04: Chỉ hỗ trợ tệp hình ảnh định dạng PNG, JPEG hoặc WebP hợp lệ.");

        // Reset stream position
        fileStream.Seek(0, SeekOrigin.Begin);

        var assetId = Guid.NewGuid();
        var ext = isPng ? ".png" : isJpeg ? ".jpg" : ".webp";
        var storageDir = Path.Combine(AppContext.BaseDirectory, ".storage", "assets");
        Directory.CreateDirectory(storageDir);
        var targetPath = Path.Combine(storageDir, $"{assetId}{ext}");

        await using var destStream = File.Create(targetPath);
        await fileStream.CopyToAsync(destStream, ct);

        return new BranchLogoUploadResult(assetId, Path.GetFileName(fileName), length);
    }

    public async Task<ChairDto[]> ChairsAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.read", ct);
        var chairs = await db.Set<BranchChair>().AsNoTracking()
            .Where(c => c.BranchId == branchId).ToListAsync(ct);
        var schedChairs = await db.Set<SchedulingChair>().AsNoTracking()
            .Where(c => c.BranchId == branchId).ToListAsync(ct);

        var result = new List<ChairDto>();
        var seenIds = new HashSet<int>();
        foreach (var c in chairs)
        {
            seenIds.Add(c.Id);
            result.Add(ToChairDto(c));
        }
        foreach (var sc in schedChairs.Where(sc => !seenIds.Contains(sc.Id)))
        {
            result.Add(ToChairDto(sc));
        }
        return result.OrderBy(c => c.Code).ToArray();
    }

    public async Task<ChairDto> SaveChairAsync(int actor, int branchId, int? chairId, SaveChairCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (c.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(c.Code) || string.IsNullOrWhiteSpace(c.Name))
            throw Error("MSG02", label: "Thông tin ghế", field: "code");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362099)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { branchId, chairId, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<SettingsReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<ChairDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        BranchChair chair;
        SchedulingChair schedChair;
        string beforeJson = "{}";

        if (chairId.HasValue)
        {
            chair = await db.Set<BranchChair>().SingleOrDefaultAsync(x => x.Id == chairId.Value && x.BranchId == branchId, ct)
                ?? new BranchChair { Id = chairId.Value, BranchId = branchId };
            schedChair = await db.Set<SchedulingChair>().SingleOrDefaultAsync(x => x.Id == chairId.Value && x.BranchId == branchId, ct)
                ?? new SchedulingChair { Id = chairId.Value, BranchId = branchId };

            if (chair.Id != 0 && chair.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            beforeJson = JsonSerializer.Serialize(chair, ClinicSettingsStore.Json);
            chair.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Error("MSG111", 409);
            var exists = await db.Set<BranchChair>().AnyAsync(x => x.BranchId == branchId && x.Code == c.Code.Trim(), ct);
            if (exists) throw Error("MSG201", 409); // Unique (BranchId, Code)

            chair = new BranchChair { BranchId = branchId };
            schedChair = new SchedulingChair { BranchId = branchId };
            db.Add(chair);
            db.Add(schedChair);
        }

        chair.Code = c.Code.Trim();
        chair.Name = c.Name.Trim();
        chair.OpenMinute = c.OpenMinute.GetValueOrDefault(480);
        chair.CloseMinute = c.CloseMinute.GetValueOrDefault(1080);

        schedChair.Name = chair.Name;
        schedChair.OpenMinute = chair.OpenMinute;
        schedChair.CloseMinute = chair.CloseMinute;
        schedChair.Active = chair.IsActive;

        await db.SaveChangesAsync(ct);
        if (!chairId.HasValue)
        {
            schedChair.Id = chair.Id;
            await db.SaveChangesAsync(ct);
        }

        var result = ToChairDto(chair);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new SettingsAudit
        {
            ActorId = actor,
            BranchIds = [branchId],
            Section = "branches",
            Target = $"CHAIR:{chair.Code}",
            Version = chair.Version,
            Action = chairId.HasValue ? "ChairUpdate" : "ChairCreate",
            Reason = "Lưu cấu hình ghế",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new SettingsReceipt
        {
            ActorId = actor,
            RequestKey = c.RequestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ImpactPreviewResult> ChairStatePreviewAsync(int actor, int branchId, int chairId, bool targetActive, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (targetActive)
        {
            return new ImpactPreviewResult(true, [], "Kích hoạt lại ghế để phân bổ bệnh nhân và bác sĩ bình thường.");
        }

        var blockers = new List<BlockerItem>();
        var now = clock.GetUtcNow();

        // 1. Check reservations for this chair
        var reservations = await db.Set<ResourceReservation>().AsNoTracking()
            .CountAsync(r => r.BranchId == branchId && r.ChairId == chairId && r.Start >= now && r.Status == "Confirmed", ct);
        if (reservations > 0)
        {
            blockers.Add(new BlockerItem("Reservations",
                $"Ghế đang có {reservations} lịch giữ chỗ / đặt hẹn trong tương lai.",
                reservations, $"/appointments?branchId={branchId}"));
        }

        var allowed = blockers.Count == 0;
        var impact = allowed
            ? "Ghế không có lịch giữ chỗ trong tương lai. An toàn để tạm ngừng."
            : $"Không thể tạm ngừng ghế vì còn {blockers.Count} ràng buộc chưa giải quyết.";

        return new ImpactPreviewResult(allowed, blockers.ToArray(), impact);
    }

    public async Task<ChairDto> SetChairStateAsync(int actor, int branchId, int chairId, ChairStateCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "chairs.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Error("MSG04");

        if (!c.Active)
        {
            var preview = await ChairStatePreviewAsync(actor, branchId, chairId, false, ct);
            if (!preview.Allowed)
            {
                var blockerSummary = string.Join("; ", preview.Blockers.Select(b => b.Message));
                throw new BusinessException(409, $"MSG205: {blockerSummary}");
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362099)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { branchId, chairId, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<SettingsReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<ChairDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var chair = await db.Set<BranchChair>().SingleOrDefaultAsync(x => x.Id == chairId && x.BranchId == branchId, ct);
        var schedChair = await db.Set<SchedulingChair>().SingleOrDefaultAsync(x => x.Id == chairId && x.BranchId == branchId, ct);
        if (chair == null && schedChair == null) throw Error("MSG113", 404);

        if (chair != null && chair.Version != c.ExpectedVersion) throw Error("MSG111", 409);

        var beforeJson = chair != null ? JsonSerializer.Serialize(chair, ClinicSettingsStore.Json) : "{}";
        if (chair != null)
        {
            chair.IsActive = c.Active;
            chair.Version++;
        }
        if (schedChair != null)
        {
            schedChair.Active = c.Active;
        }

        var result = chair != null ? ToChairDto(chair) : ToChairDto(schedChair!, 1);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new SettingsAudit
        {
            ActorId = actor,
            BranchIds = [branchId],
            Section = "branches",
            Target = $"CHAIR:{chair?.Code ?? chairId.ToString()}",
            Version = chair?.Version ?? 1,
            Action = "ChairState",
            Reason = c.Reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new SettingsReceipt
        {
            ActorId = actor,
            RequestKey = c.RequestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
