using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.SystemSettings;

// Shared by S58 writes (exclusive) and patient saves (shared) so a patient save sees one committed policy (SET-07).
internal static class ClinicSettingsStore
{
    public const string ExclusiveLock = "SELECT pg_advisory_xact_lock(6462362058)";
    public const string SharedLock = "SELECT pg_advisory_xact_lock_shared(6462362058)";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<(int Version, GeneralSettings Settings, ClinicSettingsVersion? Row)> CurrentAsync(DcmsDbContext db, CancellationToken ct)
    {
        var row = await db.ClinicSettingsVersions.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        return row == null ? (0, GeneralSettings.Default, null) : (row.Version, Read(row.ContentJson), row);
    }

    public static async Task<PatientFormPolicy> PolicyAsync(DcmsDbContext db, CancellationToken ct)
    {
        var (version, settings, _) = await CurrentAsync(db, ct);
        return new(version, settings.RequiredFields, settings.PatientCode);
    }

    public static GeneralSettings Read(string json) => JsonSerializer.Deserialize<GeneralSettings>(json, Json) ?? GeneralSettings.Default;
}

public sealed partial class GeneralSettingsService(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : IGeneralSettingsService
{
    private static readonly Guid Resource = new("00000000-0000-4000-8000-000000000058");
    private static readonly int[] IdleChoices = [5, 10, 20, 30, 60, 90, 120];
    private static readonly int[] PageSizes = [20, 50, 100];
    private static readonly (string Field, string Label, Func<RequiredFieldsSettings, bool> Get)[] RequiredLabels =
    [
        ("profileDate", "Ngày lập hồ sơ", x => x.ProfileDate), ("gender", "Giới tính", x => x.Gender), ("birthDate", "Ngày sinh", x => x.BirthDate),
        ("address", "Địa chỉ", x => x.Address), ("phone", "Số điện thoại", x => x.Phone), ("customerGroup", "Phân nhóm khách hàng", x => x.CustomerGroup),
        ("source", "Nguồn", x => x.Source)
    ];
    private static readonly (string Field, string Label, Func<ReceiptPrintSettings, bool> Get)[] ReceiptLabels =
    [
        ("internalUseNote", "Ghi chú dùng nội bộ", x => x.InternalUseNote), ("showReceiptNumber", "Hiện mã phiếu thu", x => x.ShowReceiptNumber),
        ("patientAddress", "Địa chỉ bệnh nhân", x => x.PatientAddress), ("patientPhone", "Số điện thoại bệnh nhân", x => x.PatientPhone),
        ("paymentMethod", "Phương thức thanh toán", x => x.PaymentMethod), ("dentistAssistant", "Bác sĩ, trợ thủ", x => x.DentistAssistant),
        ("totalFee", "Tổng phí", x => x.TotalFee), ("discount", "Giảm giá", x => x.Discount), ("previouslyPaid", "Đã thanh toán", x => x.PreviouslyPaid),
        ("remaining", "Còn lại", x => x.Remaining), ("patientTotalOutstanding", "Tổng công nợ của bệnh nhân", x => x.PatientTotalOutstanding),
        ("signatureLines", "Dòng ký tên", x => x.SignatureLines), ("defaultPayerToPatient", "Mặc định người nộp là bệnh nhân", x => x.DefaultPayerToPatient),
        ("defaultCollectorToActor", "Mặc định người thu là người thao tác", x => x.DefaultCollectorToActor), ("paymentNote", "Ghi chú thanh toán", x => x.PaymentNote)
    ];
    // Fields that do not exist on the patient master yet; enabling them would block every save. Empty since S58 đợt 3.
    private static readonly string[] Unavailable = [];
    private static readonly Dictionary<string, string> ModeLabels = new()
    {
        [PatientCodeModes.AutoWithManualOverride] = "Tự động, cho nhập tay", [PatientCodeModes.AutoOnly] = "Chỉ tự động",
        [PatientCodeModes.ManualOnly] = "Chỉ nhập tay"
    };

    private static BusinessException Error(string code, int status = 400, string label = "Thông tin", string? field = null, int max = 0) =>
        new(status, SystemMessages.Get(code, label, max) with { Field = field });

    private async Task<ApplicationUser> StaffAsync(int actorId)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is not { IsActive: true, AccountType: "Staff" }) throw Error("MSG06", 403);
        return user;
    }

    // No explicit grant store exists yet, so settings.general.* belongs to an assigned ClinicOwner only (AT90).
    private async Task AuthorizeAsync(int actorId)
    {
        if (!(await users.GetRolesAsync(await StaffAsync(actorId))).Contains(AccountRoles.ClinicOwner)) throw Error("MSG06", 403);
    }

    public async Task<SettingsCapabilities> CapabilitiesAsync(int actorId, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(await StaffAsync(actorId));
        var sections = new List<SettingsSection>();
        if (roles.Contains(AccountRoles.ClinicOwner)) { sections.Add(new("general", true)); sections.Add(new("catalogs", true)); }
        if (AccountRoles.Effective(roles).Contains(AccountRoles.Dentist)) { sections.Add(new("materials", true)); sections.Add(new("supplies", true)); }
        return new(sections);
    }

    public async Task<GeneralSettingsDto> GetAsync(int actorId, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var (version, settings, row) = await ClinicSettingsStore.CurrentAsync(db, ct);
        return new(version, row?.EffectiveAt, row == null ? null : await ActorName(row.ActorId, ct), row?.Reason, settings);
    }

    public async Task<GeneralPreview> PreviewAsync(int actorId, PreviewGeneralRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var after = Normalize(request.Settings);
        var (version, before, _) = await ClinicSettingsStore.CurrentAsync(db, ct);
        return await BuildPreview(version, request.ExpectedVersion, before, after, ct);
    }

    public async Task<GeneralPreview> RestorePreviewAsync(int actorId, RestorePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var source = await SourceAsync(request.SourceVersion, ct);
        var (version, before, _) = await ClinicSettingsStore.CurrentAsync(db, ct);
        return await BuildPreview(version, request.ExpectedVersion, before, source, ct);
    }

    public Task<GeneralSettingsDto> SaveAsync(int actorId, SaveGeneralRequest request, CancellationToken ct) =>
        Commit(actorId, request.RequestKey, "GeneralUpdate", request, request.Reason, request.ExpectedVersion,
            () => Task.FromResult((Normalize(request.Settings), (int?)null)), ct);

    public Task<GeneralSettingsDto> RestoreAsync(int actorId, RestoreGeneralRequest request, CancellationToken ct) =>
        Commit(actorId, request.RequestKey, "GeneralRestore", request, request.Reason, request.ExpectedVersion,
            async () => (await SourceAsync(request.SourceVersion, ct), (int?)request.SourceVersion), ct);

    public async Task<GeneralHistoryPage> HistoryAsync(int actorId, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (page < 1 || !PageSizes.Contains(pageSize) || page > int.MaxValue / pageSize) throw Error("MSG04", label: "Phân trang");
        var total = await db.ClinicSettingsVersions.CountAsync(ct);
        var rows = await (from row in db.ClinicSettingsVersions.AsNoTracking()
                          join user in db.Users.AsNoTracking() on row.ActorId equals user.Id
                          orderby row.Version descending
                          select new { row, user.FullName }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new GeneralHistoryItem(x.row.Version, x.row.EffectiveAt, x.FullName, x.row.Reason,
            x.row.RestoredFromVersion, ClinicSettingsStore.Read(x.row.ContentJson))).ToArray(), total, page, pageSize);
    }

    // Restoring re-validates the old content against today's constraints and writes it as a new version (SET-06, SET-AT-31).
    private async Task<GeneralSettings> SourceAsync(int version, CancellationToken ct)
    {
        var row = await db.ClinicSettingsVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Version == version, ct)
            ?? throw Error("MSG113", 404);
        return Normalize(ClinicSettingsStore.Read(row.ContentJson));
    }

    private async Task<GeneralSettingsDto> Commit(int actorId, Guid key, string command, object request, string? reason, int expectedVersion,
        Func<Task<(GeneralSettings Settings, int? RestoredFrom)>> resolve, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (key == Guid.Empty) throw Error("MSG04", label: "Mã yêu cầu", field: "requestKey");
        var why = reason?.Trim() ?? "";
        if (why.Length == 0) throw Error("MSG02", label: "Lý do", field: "reason");
        if (why.Length > 500) throw Error("MSG03", label: "Lý do", field: "reason", max: 500);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(ClinicSettingsStore.ExclusiveLock, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, ClinicSettingsStore.Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == key, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<GeneralSettingsDto>(prior.ResultJson, ClinicSettingsStore.Json) ?? throw Error("MSG144", 503);
        }
        var (version, current, _) = await ClinicSettingsStore.CurrentAsync(db, ct);
        if (version != expectedVersion) throw Error("MSG111", 409);
        var (settings, restoredFrom) = await resolve();
        if (settings == current) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
        var now = clock.GetUtcNow();
        db.ClinicSettingsVersions.Add(new ClinicSettingsVersion
        {
            Version = version + 1, EffectiveAt = now, ActorId = actorId, Reason = why, RestoredFromVersion = restoredFrom,
            ContentJson = JsonSerializer.Serialize(settings, ClinicSettingsStore.Json)
        });
        var result = new GeneralSettingsDto(version + 1, now, await ActorName(actorId, ct), why, settings);
        db.SystemCatalogChanges.Add(new SystemCatalogChange
        {
            RequestKey = key, ActorId = actorId, Command = command, ResourceId = Resource, Fingerprint = hash,
            BeforeJson = JsonSerializer.Serialize(new { version, settings = current }, ClinicSettingsStore.Json),
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json), OccurredAt = now
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // DCMS-122 patient code sequence (GBR-177, SET-AT-08). One PostgreSQL sequence serves every prefix clinic-wide.
    private static readonly Guid SequenceResource = new("00000000-0000-4000-8000-000000000122");
    private const long MaxSequence = 999_999_999_999_999;
    private sealed class SequenceRow { public long LastValue { get; set; } public bool IsCalled { get; set; } }

    // Reads pg state directly so a GET never issues a number (nextval is only called when a patient is created).
    private async Task<(long Next, long? LastIssued)> SequenceStateAsync(CancellationToken ct)
    {
        var row = await db.Database.SqlQueryRaw<SequenceRow>(
            "SELECT last_value AS \"LastValue\", is_called AS \"IsCalled\" FROM patient_code_seq").SingleAsync(ct);
        return row.IsCalled ? (row.LastValue + 1, row.LastValue) : (row.LastValue, null);
    }

    private Task<long?> HighestExistingAsync(string prefix, CancellationToken ct) =>
        db.Database.SqlQuery<long?>($"""
            SELECT MAX(CAST(SUBSTRING("PatientCode" FROM {prefix.Length + 1}) AS bigint)) AS "Value" FROM "PATIENT_PROFILE"
            WHERE "PatientCode" ~ {"^" + prefix + "[0-9]{1,15}$"}
            """).SingleAsync(ct);

    private Task<int> SkippedAsync(string prefix, long from, CancellationToken ct) =>
        db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM "PATIENT_PROFILE"
            WHERE "PatientCode" ~ {"^" + prefix + "[0-9]{1,15}$"} AND CAST(SUBSTRING("PatientCode" FROM {prefix.Length + 1}) AS bigint) >= {from}
            """).SingleAsync(ct);

    private static string CodeFor(string prefix, long number) => $"{prefix}{number:000000}";

    private async Task<PatientCodeSequenceDto> SequenceDtoAsync(CancellationToken ct)
    {
        var (_, settings, _) = await ClinicSettingsStore.CurrentAsync(db, ct);
        var prefix = settings.PatientCode.Prefix;
        var (next, last) = await SequenceStateAsync(ct);
        return new(prefix, next, CodeFor(prefix, next), last, await HighestExistingAsync(prefix, ct));
    }

    private static IReadOnlyList<string> SequenceIssues(string prefix, long current, long proposed)
    {
        var issues = new List<string>();
        if (proposed < 1 || proposed > MaxSequence) issues.Add($"Số tiếp theo phải từ 1 đến {MaxSequence}.");
        else if (proposed < current) issues.Add($"Số tiếp theo chỉ được tăng (hiện tại {current}). Không lùi hoặc tái dùng số đã cấp.");
        else if (CodeFor(prefix, proposed).Length > 40) issues.Add("Mã sinh ra vượt quá 40 ký tự.");
        return issues;
    }

    public async Task<PatientCodeSequenceDto> SequenceAsync(int actorId, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        return await SequenceDtoAsync(ct);
    }

    public async Task<PatientCodeSequencePreview> SequencePreviewAsync(int actorId, PatientCodeSequencePreviewRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        var current = await SequenceDtoAsync(ct);
        var issues = SequenceIssues(current.Prefix, current.NextNumber, request.NextNumber);
        var valid = issues.Count == 0;
        return new(current.NextNumber != request.ExpectedNextNumber, valid && request.NextNumber != current.NextNumber,
            valid ? CodeFor(current.Prefix, request.NextNumber) : current.NextCode, issues,
            valid ? await SkippedAsync(current.Prefix, request.NextNumber, ct) : 0);
    }

    public async Task<PatientCodeSequenceDto> UpdateSequenceAsync(int actorId, UpdatePatientCodeSequenceRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(actorId);
        if (request.RequestKey == Guid.Empty) throw Error("MSG04", label: "Mã yêu cầu", field: "requestKey");
        var why = request.Reason?.Trim() ?? "";
        if (why.Length == 0) throw Error("MSG02", label: "Lý do", field: "reason");
        if (why.Length > 500) throw Error("MSG03", label: "Lý do", field: "reason", max: 500);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Exclusive: patient creation holds the shared lock, so no number is issued while the sequence moves.
        await db.Database.ExecuteSqlRawAsync(ClinicSettingsStore.ExclusiveLock, ct);
        const string command = "PatientCodeSequence";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, ClinicSettingsStore.Json))));
        var prior = await db.SystemCatalogChanges.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        if (prior != null)
        {
            if (prior.ActorId != actorId || prior.Command != command || prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PatientCodeSequenceDto>(prior.ResultJson, ClinicSettingsStore.Json) ?? throw Error("MSG144", 503);
        }
        var before = await SequenceDtoAsync(ct);
        if (before.NextNumber != request.ExpectedNextNumber) throw Error("MSG111", 409);
        if (request.NextNumber == before.NextNumber) throw new BusinessException(400, GeneralSettingsMessages.NoChange);
        if (SequenceIssues(before.Prefix, before.NextNumber, request.NextNumber).Count > 0)
            throw Error("MSG04", label: "Số tiếp theo", field: "nextNumber");
        var result = before with { NextNumber = request.NextNumber, NextCode = CodeFor(before.Prefix, request.NextNumber) };
        db.SystemCatalogChanges.Add(new SystemCatalogChange
        {
            RequestKey = request.RequestKey, ActorId = actorId, Command = command, ResourceId = SequenceResource, Fingerprint = hash,
            BeforeJson = JsonSerializer.Serialize(new { before, reason = why }, ClinicSettingsStore.Json),
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json), OccurredAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
        // setval is not transactional; it runs last under the exclusive lock and only ever moves forward.
        await db.Database.ExecuteSqlAsync($"SELECT setval('patient_code_seq', {request.NextNumber}, false)", ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static GeneralSettings Normalize(GeneralSettings? settings)
    {
        if (settings == null) throw Error("MSG04", label: "Thiết lập", field: "settings");
        if (!IdleChoices.Contains(settings.StaffIdleTimeoutMinutes))
            throw Error("MSG04", label: "Thời gian tự đăng xuất", field: "staffIdleTimeoutMinutes");
        if (settings.RequiredFields is not { } required) throw Error("MSG04", label: "Hồ sơ bắt buộc", field: "requiredFields");
        foreach (var (field, _, get) in RequiredLabels)
            if (Unavailable.Contains(field) && get(required)) throw new BusinessException(400, GeneralSettingsMessages.FieldUnavailable($"requiredFields.{field}"));
        if (settings.PatientCode is not { } code || !PatientCodeModes.All.Contains(code.Mode))
            throw Error("MSG04", label: "Chế độ mã bệnh nhân", field: "patientCode.mode");
        var prefix = code.Prefix?.Trim().ToUpperInvariant() ?? "";
        if (!PrefixPattern().IsMatch(prefix)) throw Error("MSG04", label: "Tiền tố mã bệnh nhân", field: "patientCode.prefix");
        if (settings.ReceiptPrint == null) throw Error("MSG04", label: "Mẫu in phiếu thu", field: "receiptPrint");
        return settings with { PatientCode = code with { Prefix = prefix } };
    }

    private async Task<GeneralPreview> BuildPreview(int version, int expected, GeneralSettings before, GeneralSettings after, CancellationToken ct)
    {
        var changes = Flatten(before).Zip(Flatten(after))
            .Where(x => x.First.Value != x.Second.Value)
            .Select(x => new SettingsChange(x.First.Field, x.First.Label, x.First.Value, x.Second.Value)).ToList();
        var warnings = new List<string>();
        var missing = new List<MissingPatients>();
        var enabled = RequiredLabels.Where(x => x.Get(after.RequiredFields) && !x.Get(before.RequiredFields)).ToArray();
        if (enabled.Length > 0)
            warnings.Add("Trường bắt buộc mới áp dụng từ lần lưu hồ sơ tiếp theo. Hồ sơ cũ không bị sửa hoặc tự điền.");
        // Counts are aggregates only; the actor (ClinicOwner) already reads patients through the effective Dentist role.
        var active = db.Patients.AsNoTracking().Where(x => !x.IsArchived);
        foreach (var (field, label, _) in enabled)
        {
            int? count = field switch
            {
                "gender" => await active.CountAsync(x => x.Gender == null, ct),
                "birthDate" => await active.CountAsync(x => x.BirthDate == null, ct),
                "address" => await active.CountAsync(x => x.Address == null, ct),
                "phone" => await active.CountAsync(x => x.Phone == null, ct),
                "profileDate" => await active.CountAsync(x => x.ProfileDate == null, ct),
                "source" => await active.CountAsync(x => x.SourceId == null, ct),
                "customerGroup" => await active.CountAsync(x => x.CustomerGroupId == null, ct),
                _ => null
            };
            if (count is { } n) missing.Add(new(field, label, n));
        }
        if (before.PatientCode != after.PatientCode) warnings.Add("Chế độ và tiền tố mã chỉ áp dụng cho bệnh nhân tạo mới. Mã đã cấp không thay đổi.");
        if (before.StaffIdleTimeoutMinutes != after.StaffIdleTimeoutMinutes)
            warnings.Add("Thời gian tự đăng xuất mới áp dụng cho phiên nhân viên ngay sau khi lưu; hoạt động đã ghi nhận không được đặt lại.");
        if (before.ReceiptPrint != after.ReceiptPrint) warnings.Add("Mẫu in mới áp dụng cho lần in tiếp theo; phiếu thu đã phát hành giữ nguyên số tiền và số phiếu.");
        return new(version, version != expected, changes, warnings, missing);
    }

    private static IEnumerable<(string Field, string Label, string Value)> Flatten(GeneralSettings s)
    {
        static string OnOff(bool value) => value ? "Bật" : "Tắt";
        yield return ("staffIdleTimeoutMinutes", "Tự đăng xuất sau (phút)", s.StaffIdleTimeoutMinutes.ToString());
        foreach (var (field, label, get) in RequiredLabels) yield return ($"requiredFields.{field}", $"Bắt buộc {label}", OnOff(get(s.RequiredFields)));
        yield return ("patientCode.mode", "Chế độ mã bệnh nhân", ModeLabels.GetValueOrDefault(s.PatientCode.Mode, s.PatientCode.Mode));
        yield return ("patientCode.prefix", "Tiền tố mã bệnh nhân", s.PatientCode.Prefix);
        foreach (var (field, label, get) in ReceiptLabels) yield return ($"receiptPrint.{field}", $"In: {label}", OnOff(get(s.ReceiptPrint)));
    }

    private async Task<string> ActorName(int actorId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.Id == actorId).Select(x => x.FullName).SingleAsync(ct);

    [GeneratedRegex("^[A-Z0-9-]{0,12}$")]
    private static partial Regex PrefixPattern();
}
