using System.Text.Json;
using System.Text.RegularExpressions;
using DCMS.Application.Common;
using DCMS.Application.Features.Patients;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static DCMS.Infrastructure.Features.PatientLinks.PatientLinkSupport;

namespace DCMS.Infrastructure.Features.Patients;

// S05/S06 (UC-08, UC-09, UC-38). Search never creates, merges or changes a record; shared phones never merge identities.
public sealed partial class PatientService(DcmsDbContext db) : IPatientService
{
    private static readonly int[] PageSizes = [20, 50, 100];
    private const string Entity = "bệnh nhân";

    public async Task<PatientPage> SearchAsync(string? keyword, DateOnly? birthDate, bool includeArchived, int page, int pageSize,
        CancellationToken ct)
    {
        if (page < 1 || !PageSizes.Contains(pageSize) || page > int.MaxValue / pageSize) Invalid("Phân trang");
        var term = Optional(keyword, "Từ khóa", 100);
        var query = db.Patients.AsNoTracking();
        if (!includeArchived) query = query.Where(x => !x.IsArchived);
        if (birthDate is { } birth) query = query.Where(x => x.BirthDate == birth);
        if (term != null)
        {
            var code = term.ToUpperInvariant();
            var name = NormalizeName(term);
            var digits = Digits(term);
            query = digits.Length >= 3
                ? query.Where(x => x.PatientCode.Contains(code) || x.NormalizedName.Contains(name) || (x.Phone != null && x.Phone.Contains(digits)))
                : query.Where(x => x.PatientCode.Contains(code) || x.NormalizedName.Contains(name));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.NormalizedName).ThenBy(x => x.PatientCode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(ToItem).ToArray(), total, page, pageSize);
    }

    public async Task<IReadOnlyList<DuplicateCandidate>> DuplicatesAsync(DuplicateCheckRequest request, CancellationToken ct)
    {
        var name = Required(request.FullName, "Họ tên", 100)!;
        var phone = NormalizePhone(request.Phone);
        return await FindDuplicatesAsync(NormalizeName(name), request.BirthDate, phone, request.ExcludeId, ct);
    }

    public async Task<PatientDetail> GetAsync(int id, CancellationToken ct) =>
        await DetailAsync(await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing(), ct);

    public async Task<PatientChange> CreateAsync(int actorId, CreatePatientRequest request, CancellationToken ct)
    {
        var input = Validate(request.FullName, request.BirthDate, request.Gender, request.Phone, request.Address,
            request.GuardianName, request.GuardianRelationship, request.GuardianPhone, request.Note);
        var manualCode = Optional(request.PatientCode, "Mã bệnh nhân", 40)?.ToUpperInvariant();
        if (manualCode != null && !CodePattern().IsMatch(manualCode)) Invalid("Mã bệnh nhân");
        await RequireReviewAsync(input, null, request.DuplicatesReviewed, ct);
        if (manualCode != null && await db.Patients.AnyAsync(x => x.PatientCode == manualCode, ct)) throw DuplicateCode();

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var patient = new PatientProfile { PatientCode = manualCode ?? await NextCodeAsync(ct), CreatedAt = now, UpdatedAt = now };
        var changes = Apply(patient, input);
        await SaveAsync(() =>
        {
            db.Patients.Add(patient);
            return Task.CompletedTask;
        }, ct);
        await AuditAsync(patient.Id, actorId, PatientAuditActions.Create, null,
            [new("Mã bệnh nhân", null, patient.PatientCode), .. changes], now, ct);
        await transaction.CommitAsync(ct);
        return new(await DetailAsync(patient, ct), SystemMessages.Get("MSG222", "hồ sơ bệnh nhân", value: patient.PatientCode));
    }

    public async Task<PatientChange> UpdateAsync(int actorId, int id, UpdatePatientRequest request, CancellationToken ct)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        if (patient.IsArchived) throw new BusinessException(409, SystemMessages.Get("MSG202"));
        if (patient.Version != request.Version) throw Stale();
        var input = Validate(request.FullName, request.BirthDate, request.Gender, request.Phone, request.Address,
            request.GuardianName, request.GuardianRelationship, request.GuardianPhone, request.Note);
        await RequireReviewAsync(input, id, request.DuplicatesReviewed, ct);
        var changes = Apply(patient, input);
        if (changes.Count == 0) return new(await DetailAsync(patient, ct), SystemMessages.Get("MSG116"));
        var now = DateTimeOffset.UtcNow;
        patient.UpdatedAt = now;
        patient.Version++;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SaveAsync(() => Task.CompletedTask, ct);
        await AuditAsync(patient.Id, actorId, PatientAuditActions.Update, null, changes, now, ct);
        await transaction.CommitAsync(ct);
        return new(await DetailAsync(patient, ct), SystemMessages.Get("MSG116"));
    }

    public async Task<PatientChange> ArchiveAsync(int actorId, int id, ArchivePatientRequest request, bool archive, CancellationToken ct)
    {
        var reason = Required(request.Reason, archive ? "Lý do ngừng sử dụng" : "Lý do khôi phục", 500);
        var patient = await db.Patients.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
        if (patient.Version != request.Version || patient.IsArchived == archive) throw Stale();
        var now = DateTimeOffset.UtcNow;
        patient.IsArchived = archive;
        patient.ArchivedAt = archive ? now : null;
        patient.ArchiveReason = archive ? reason : null;
        patient.UpdatedAt = now;
        patient.Version++;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SaveAsync(() => Task.CompletedTask, ct);
        await AuditAsync(patient.Id, actorId, archive ? PatientAuditActions.Archive : PatientAuditActions.Restore, reason,
            [new("Trạng thái", archive ? "Đang sử dụng" : "Ngừng sử dụng", archive ? "Ngừng sử dụng" : "Đang sử dụng")], now, ct);
        await transaction.CommitAsync(ct);
        return new(await DetailAsync(patient, ct),
            SystemMessages.Get("MSG222", archive ? "ngừng sử dụng hồ sơ" : "khôi phục hồ sơ", value: patient.PatientCode));
    }

    public async Task<IReadOnlyList<PatientAuditEntry>> AuditAsync(int id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(x => x.Id == id, ct)) throw Missing();
        var rows = await (from audit in db.PatientAudits.AsNoTracking()
                          join user in db.Users.AsNoTracking() on audit.ActorAccountId equals user.Id
                          where audit.PatientId == id
                          orderby audit.OccurredAt descending, audit.Id descending
                          select new { audit, user.FullName }).ToListAsync(ct);
        return rows.Select(x => new PatientAuditEntry(x.audit.Id, x.audit.Action, x.FullName, x.audit.Reason,
            JsonSerializer.Deserialize<PatientFieldChange[]>(x.audit.ChangesJson) ?? [], x.audit.OccurredAt)).ToArray();
    }

    private sealed record Input(string FullName, DateOnly? BirthDate, string? Gender, string? Phone, string? Address,
        string? GuardianName, string? GuardianRelationship, string? GuardianPhone, string? Note);

    private static Input Validate(string? fullName, DateOnly? birthDate, string? gender, string? phone, string? address,
        string? guardianName, string? guardianRelationship, string? guardianPhone, string? note)
    {
        var name = Required(fullName, "Họ tên", 100)!;
        if (birthDate > ClinicToday() || birthDate?.Year < 1900) Invalid("Ngày sinh");
        var sex = Optional(gender, "Giới tính", 16)?.ToUpperInvariant();
        if (sex != null && !PatientGenders.All.Contains(sex)) Invalid("Giới tính");
        var contact = ValidPhone(phone, "Số điện thoại");
        var guardian = Optional(guardianName, "Tên người giám hộ", 160);
        var relationship = Optional(guardianRelationship, "Quan hệ với bệnh nhân", 40);
        var guardianContact = ValidPhone(guardianPhone, "Số điện thoại người giám hộ");
        // A guardian entry must at least identify the person and the relationship [PROPOSED].
        if (guardian == null && (relationship != null || guardianContact != null)) Required(null, "tên người giám hộ", 160);
        if (guardian != null && relationship == null) Required(null, "quan hệ với bệnh nhân", 40);
        return new(name, birthDate, sex, contact, Optional(address, "Địa chỉ", 255), guardian, relationship, guardianContact,
            Optional(note, "Ghi chú", 1000));
    }

    private static string? ValidPhone(string? value, string field)
    {
        var trimmed = Optional(value, field, 20);
        if (trimmed == null) return null;
        if (!PhonePattern().IsMatch(trimmed)) Invalid(field);
        var normalized = NormalizePhone(trimmed)!;
        if (Digits(normalized).Length < 8) Invalid(field);
        return normalized;
    }

    // Stored as digits with an optional leading '+', so search by digits finds every written form.
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = Digits(value);
        if (digits.Length == 0) return null;
        return value.TrimStart().StartsWith('+') ? "+" + digits : digits;
    }

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static List<PatientFieldChange> Apply(PatientProfile patient, Input input)
    {
        var changes = new List<PatientFieldChange>();
        void Set(string field, string? before, string? after, Action assign)
        {
            if (before == after) return;
            changes.Add(new(field, before, after));
            assign();
        }
        Set("Họ tên", Empty(patient.FullName), input.FullName, () =>
        {
            patient.FullName = input.FullName;
            patient.NormalizedName = NormalizeName(input.FullName);
        });
        Set("Ngày sinh", patient.BirthDate?.ToString("yyyy-MM-dd"), input.BirthDate?.ToString("yyyy-MM-dd"), () => patient.BirthDate = input.BirthDate);
        Set("Giới tính", patient.Gender, input.Gender, () => patient.Gender = input.Gender);
        Set("Số điện thoại", patient.Phone, input.Phone, () => patient.Phone = input.Phone);
        Set("Địa chỉ", patient.Address, input.Address, () => patient.Address = input.Address);
        Set("Tên người giám hộ", patient.GuardianName, input.GuardianName, () => patient.GuardianName = input.GuardianName);
        Set("Quan hệ với bệnh nhân", patient.GuardianRelationship, input.GuardianRelationship, () => patient.GuardianRelationship = input.GuardianRelationship);
        Set("SĐT người giám hộ", patient.GuardianPhone, input.GuardianPhone, () => patient.GuardianPhone = input.GuardianPhone);
        Set("Ghi chú", patient.Note, input.Note, () => patient.Note = input.Note);
        return changes;
    }

    private static string? Empty(string value) => value.Length == 0 ? null : value;

    // Possible duplicates require human review; nothing is merged automatically (GBR-033, UC-38 A2).
    private async Task RequireReviewAsync(Input input, int? excludeId, bool reviewed, CancellationToken ct)
    {
        if (reviewed) return;
        if ((await FindDuplicatesAsync(NormalizeName(input.FullName), input.BirthDate, input.Phone, excludeId, ct)).Count > 0)
            Required(null, "xác nhận đã đối chiếu hồ sơ nghi trùng", 0);
    }

    private async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(string name, DateOnly? birthDate, string? phone,
        int? excludeId, CancellationToken ct)
    {
        var given = name.Split(' ').Last();
        var spacedGiven = " " + given;
        var rows = await db.Patients.AsNoTracking()
            .Where(x => x.Id != excludeId)
            .Where(x => x.NormalizedName == name || (phone != null && x.Phone == phone)
                || (birthDate != null && x.BirthDate == birthDate && (x.NormalizedName == given || x.NormalizedName.EndsWith(spacedGiven))))
            .OrderBy(x => x.PatientCode).Take(10).ToListAsync(ct);
        return rows.Select(x =>
        {
            var reasons = new List<string>();
            if (x.NormalizedName == name) reasons.Add("Trùng họ tên");
            if (phone != null && x.Phone == phone) reasons.Add("Trùng số điện thoại");
            if (birthDate != null && x.BirthDate == birthDate) reasons.Add("Trùng ngày sinh");
            return new DuplicateCandidate(ToItem(x), reasons);
        }).ToArray();
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var next = await db.Database.SqlQuery<long>($"SELECT nextval('patient_code_seq') AS \"Value\"").SingleAsync(ct);
            var code = $"BN{next:000000}";
            if (!await db.Patients.AnyAsync(x => x.PatientCode == code, ct)) return code;
        }
    }

    private async Task SaveAsync(Func<Task> change, CancellationToken ct)
    {
        await change();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Stale(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw DuplicateCode();
        }
    }

    private async Task AuditAsync(int patientId, int actorId, string action, string? reason, IReadOnlyList<PatientFieldChange> changes,
        DateTimeOffset at, CancellationToken ct)
    {
        db.PatientAudits.Add(new PatientAudit
        {
            PatientId = patientId, ActorAccountId = actorId, Action = action, Reason = reason, OccurredAt = at,
            ChangesJson = JsonSerializer.Serialize(changes)
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<PatientDetail> DetailAsync(PatientProfile x, CancellationToken ct)
    {
        var verified = await (from link in db.PatientAccountLinks.AsNoTracking()
                              join user in db.Users.AsNoTracking() on link.AccountId equals user.Id
                              where link.PatientId == x.Id && link.Status == PatientLinkStatus.Verified
                              select new { user.UserName, link.ReviewedAt }).FirstOrDefaultAsync(ct);
        return new(x.Id, x.PatientCode, x.FullName, x.BirthDate, x.Gender, x.Phone, x.Address, x.GuardianName,
            x.GuardianRelationship, x.GuardianPhone, x.Note, x.IsArchived, x.ArchivedAt, x.ArchiveReason, x.CreatedAt, x.UpdatedAt,
            x.Version, new(verified?.UserName, verified?.ReviewedAt));
    }

    // Last visit stays null until UC-07 check-in/VISIT exists; the UI shows "no data", never a fabricated date.
    private static PatientListItem ToItem(PatientProfile x) =>
        new(x.Id, x.PatientCode, x.FullName, x.BirthDate, x.Gender, x.Phone, x.IsArchived, null, x.UpdatedAt);

    private static BusinessException Missing() => new(404, SystemMessages.Get("MSG113"));
    private static BusinessException DuplicateCode() => new(409, SystemMessages.Get("MSG201", Entity));

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^\+?[0-9 ().-]+$")]
    private static partial Regex PhonePattern();
}
