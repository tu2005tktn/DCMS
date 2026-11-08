using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Examinations;
using DCMS.Infrastructure.Features.Settings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Prescriptions;

// docs/specs/PRESCRIPTION-MANAGEMENT.md: external (buy-outside) prescriptions with immutable signed revisions.
public sealed class PrescriptionService(DcmsDbContext db, ISettingsAccess access, BranchAssetStore assets, TimeProvider clock) : IPrescriptionService
{
    private const string Lock = "SELECT pg_advisory_xact_lock(6462362088)";
    private const string ExternalPurchaseNotice = "Đơn thuốc mua ngoài tại hiệu thuốc. Phòng khám không bán thuốc tại chỗ.";
    private static readonly JsonSerializerOptions Json = ClinicSettingsStore.Json;

    private static BusinessException Fail(string code, int status = 400, string? field = null, string label = "Thông tin", int? currentVersion = null) =>
        new(status, SystemMessages.Get(code, label) with { Field = field, CurrentVersion = currentVersion });

    private static string Hash(object payload) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, Json)));

    private async Task<IDbContextTransaction> BeginAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(Lock, ct);
        return tx;
    }

    private async Task<(bool Found, T? Result)> ReplayAsync<T>(int actor, Guid key, string hash, CancellationToken ct)
    {
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == key, ct);
        if (prior == null) return (false, default);
        if (prior.Fingerprint != hash) throw Fail("MSG111", 409);
        return (true, JsonSerializer.Deserialize<T>(prior.ResultJson, Json));
    }

    private void Record(int actor, Visit? visit, Guid? templateId, string action, string reason, string before, object after, Guid key, string hash, object result)
    {
        var now = clock.GetUtcNow();
        db.Add(new PrescriptionAudit
        {
            ActorId = actor, BranchId = visit?.BranchId ?? 0, VisitId = visit?.Id, TemplateId = templateId,
            Action = action, Reason = reason.Length > 500 ? reason[..500] : reason,
            BeforeJson = before, AfterJson = JsonSerializer.Serialize(after, Json), At = now
        });
        db.Add(new PrescriptionCommandReceipt { ActorId = actor, RequestKey = key, Fingerprint = hash, ResultJson = JsonSerializer.Serialize(result, Json) });
    }

    // ---------- mapping ----------

    private static PrescriptionLineDto LineDto(ExternalPrescriptionLine x, string? doctorName) => new(
        x.Id, x.PrescriptionId, x.VisitId, x.PatientId, x.BranchId, x.PrescribedByDoctorId, doctorName,
        x.MedicineId, x.MedicineCode, x.MedicineName, x.ActiveIngredient, x.Strength,
        x.DosageForm, x.Unit, x.Route, x.MorningDose, x.NoonDose, x.AfternoonDose, x.EveningDose,
        x.MealTiming, x.Days, x.Quantity, x.Instructions, x.Status, x.FinalizedAt, x.Version);

    private static PrescriptionTemplateLineDto TemplateLineDto(PrescriptionTemplateLine x) => new(
        x.Id, x.TemplateId, x.MedicineId, x.MedicineCode, x.MedicineName, x.ActiveIngredient,
        x.Strength, x.DosageForm, x.Unit, x.Route, x.MorningDose, x.NoonDose, x.AfternoonDose,
        x.EveningDose, x.MealTiming, x.Days, x.Quantity, x.Instructions, x.SortOrder);

    private static PrescriptionTemplateDto TemplateDto(PrescriptionTemplate x) => new(
        x.Id, x.Code, x.Name, x.DiagnosisKind, x.Description, x.IsActive, x.Version,
        x.CreatedBy, x.CreatedAt, x.UpdatedBy, x.UpdatedAt,
        x.Lines.OrderBy(l => l.SortOrder).Select(TemplateLineDto).ToArray());

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var set = ids.Distinct().ToArray();
        return await db.Set<ApplicationUser>().AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private async Task<PrescriptionDto> DtoAsync(ExternalPrescription rx, IEnumerable<ExternalPrescriptionLine> lines, CancellationToken ct)
    {
        var rows = lines.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
        var names = await NamesAsync(rows.Select(x => x.PrescribedByDoctorId).Append(rx.PrescribedByDoctorId)
            .Concat(rx.FinalizedBy.HasValue ? new[] { rx.FinalizedBy.Value } : Array.Empty<int>()), ct);
        return new PrescriptionDto(rx.Id, rx.VisitId, rx.PatientId, rx.BranchId, rx.PrescribedByDoctorId,
            names.GetValueOrDefault(rx.PrescribedByDoctorId), rx.Revision, rx.Status, rx.GeneralAdvice, rx.Diagnosis,
            rx.CorrectionReason, rx.SupersedesId, rx.FinalizedAt, rx.FinalizedBy is { } by ? names.GetValueOrDefault(by) : null,
            rx.VerificationCode, rx.Version, rows.Select(l => LineDto(l, names.GetValueOrDefault(l.PrescribedByDoctorId))).ToArray());
    }

    // ---------- access & state guards ----------

    private async Task<Visit> ReadableVisitAsync(int actor, Guid id, CancellationToken ct)
    {
        var visit = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Fail("MSG113", 404);
        await access.OperationalAsync(actor, "prescriptions.read", visit.BranchId, ct);
        return visit;
    }

    // Writes need the grant, the visit's branch scope and a prescriber (Dentist) role; the UI is never trusted.
    private async Task<Visit> WritableVisitAsync(int actor, Guid id, CancellationToken ct)
    {
        var visit = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Fail("MSG113", 404);
        var state = await access.ResolveAsync(actor, ct);
        if (!state.Permissions.Contains("prescriptions.manage") || !state.BranchIds.Contains(visit.BranchId) || !state.Roles.Contains(AccountRoles.Dentist))
            throw Fail("MSG06", 403);
        return visit;
    }

    private static void EnsureEditable(Visit visit, ExternalPrescription? rx)
    {
        if (rx != null && rx.Status != PrescriptionStatuses.Draft) throw Fail("MSG205", 409, "status", "Đơn thuốc");
        var revision = rx?.Revision ?? 1;
        var visitOpen = visit.Status == VisitStatuses.InProgress || (revision > 1 && visit.Status == VisitStatuses.Completed);
        if (!visitOpen) throw Fail("MSG205", 409, "visit", "Lượt khám");
    }

    private Task<ExternalPrescription?> CurrentAsync(Guid visitId, CancellationToken ct) =>
        db.Set<ExternalPrescription>().Where(x => x.VisitId == visitId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);

    private ExternalPrescription NewDraft(Visit visit, int actor, int revision)
    {
        var now = clock.GetUtcNow();
        var rx = new ExternalPrescription
        {
            VisitId = visit.Id, PatientId = visit.PatientId, BranchId = visit.BranchId, PrescribedByDoctorId = actor,
            Revision = revision, Status = PrescriptionStatuses.Draft, CreatedBy = actor, CreatedAt = now, UpdatedBy = actor, UpdatedAt = now
        };
        db.Add(rx);
        return rx;
    }

    private void Touch(ExternalPrescription rx, int actor)
    {
        rx.Version++;
        rx.UpdatedBy = actor;
        rx.UpdatedAt = clock.GetUtcNow();
    }

    private sealed record LineValues(string Route, string Morning, string Noon, string Afternoon, string Evening, string Meal, int Days, int Quantity, string? Instructions);

    private static LineValues Validate(string? route, string? morning, string? noon, string? afternoon, string? evening, string? meal, int? days, int quantity, string? instructions)
    {
        static string Dose(string? value, string field)
        {
            var v = string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
            if (v.Length > PrescriptionRules.MaxDoseLength) throw Fail("MSG04", field: field, label: "Liều dùng");
            return v;
        }
        if (quantity is < 1 or > PrescriptionRules.MaxQuantity) throw Fail("MSG04", field: "quantity", label: "Số lượng");
        var d = days ?? 1;
        if (d is < 1 or > PrescriptionRules.MaxDays) throw Fail("MSG04", field: "days", label: "Số ngày dùng");
        var mealTiming = string.IsNullOrWhiteSpace(meal) ? "AfterMeal" : meal.Trim();
        if (!PrescriptionRules.MealTimings.Contains(mealTiming)) throw Fail("MSG04", field: "mealTiming", label: "Thời điểm dùng");
        var r = route?.Trim() ?? "";
        if (r.Length > 40) throw Fail("MSG04", field: "route", label: "Đường dùng");
        if (instructions?.Length > 1000) throw Fail("MSG04", field: "instructions", label: "Lời dặn");
        return new(r, Dose(morning, "morningDose"), Dose(noon, "noonDose"), Dose(afternoon, "afternoonDose"), Dose(evening, "eveningDose"),
            mealTiming, d, quantity, string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim());
    }

    private static void Apply(ExternalPrescriptionLine line, CatalogMedicine medicine, LineValues v)
    {
        line.MedicineId = medicine.Id;
        line.MedicineCode = medicine.Code;
        line.MedicineName = medicine.Name;
        line.ActiveIngredient = medicine.ActiveIngredient;
        line.Strength = medicine.Strength;
        line.DosageForm = medicine.DosageForm;
        line.Unit = medicine.BaseUnit;
        line.Route = v.Route.Length > 0 ? v.Route : string.IsNullOrWhiteSpace(medicine.Route) ? "Uống" : medicine.Route;
        line.MorningDose = v.Morning;
        line.NoonDose = v.Noon;
        line.AfternoonDose = v.Afternoon;
        line.EveningDose = v.Evening;
        line.MealTiming = v.Meal;
        line.Days = v.Days;
        line.Quantity = v.Quantity;
        line.Instructions = v.Instructions ?? medicine.Instructions ?? "";
    }

    private static void ApplyCustom(ExternalPrescriptionLine line, string name, string unit, LineValues v)
    {
        line.MedicineId = null;
        line.MedicineCode = "";
        line.MedicineName = name.Trim();
        line.ActiveIngredient = null;
        line.Strength = null;
        line.DosageForm = null;
        line.Unit = unit.Trim();
        line.Route = v.Route.Length > 0 ? v.Route : "Uống";
        line.MorningDose = v.Morning;
        line.NoonDose = v.Noon;
        line.AfternoonDose = v.Afternoon;
        line.EveningDose = v.Evening;
        line.MealTiming = v.Meal;
        line.Days = v.Days;
        line.Quantity = v.Quantity;
        line.Instructions = v.Instructions ?? "";
    }

    private Task<CatalogMedicine?> PrescribableAsync(Guid id, CancellationToken ct) =>
        db.Set<CatalogMedicine>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.PrescriptionEligible, ct);

    // ---------- staff reads ----------

    // With a branch: prescribing at a visit (branch scope). Without: global template management.
    public async Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int? branchId, string? search, CancellationToken ct)
    {
        if (branchId is { } branch) await access.OperationalAsync(actor, "prescriptions.manage", branch, ct);
        else await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (search?.Length > 100) throw Fail("MSG04", field: "q", label: "Từ khóa");
        var q = search?.Trim().ToUpperInvariant() ?? "";
        return await db.Set<CatalogMedicine>().AsNoTracking()
            .Where(x => x.IsActive && x.PrescriptionEligible &&
                (q == "" || x.NormalizedName.Contains(q) || x.Code.ToUpper().Contains(q) || (x.ActiveIngredient != null && x.ActiveIngredient.ToUpper().Contains(q))))
            .OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Take(50)
            .Select(x => new PrescriptionMedicineOption(
                x.Id, x.Code, x.Name, x.ActiveIngredient, x.Strength, x.DosageForm,
                x.BaseUnit, x.Route ?? "Uống", x.Packaging, x.Contraindications, x.AllergyWarnings,
                x.Instructions, x.ReferencePrice))
            .ToArrayAsync(ct);
    }

    public async Task<VisitPrescriptionDto> PrescriptionAsync(int actor, Guid visitId, CancellationToken ct)
    {
        await ReadableVisitAsync(actor, visitId, ct);
        var revisions = await db.Set<ExternalPrescription>().AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderByDescending(x => x.Revision).ToArrayAsync(ct);
        if (revisions.Length == 0) return new(null, []);
        var ids = revisions.Select(x => x.Id).ToArray();
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => ids.Contains(x.PrescriptionId)).ToArrayAsync(ct);
        var names = await NamesAsync(revisions.Select(x => x.PrescribedByDoctorId), ct);
        var current = revisions[0];
        var history = revisions.Select(x => new PrescriptionSummaryDto(x.Id, x.Revision, x.Status, names.GetValueOrDefault(x.PrescribedByDoctorId),
            x.CreatedAt, x.FinalizedAt, x.CorrectionReason, lines.Count(l => l.PrescriptionId == x.Id))).ToArray();
        return new(await DtoAsync(current, lines.Where(l => l.PrescriptionId == current.Id), ct), history);
    }

    public async Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct) =>
        (await PrescriptionAsync(actor, visitId, ct)).Current?.Lines ?? [];

    public async Task<ClinicalAllergyWarning[]> CheckSafetyAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var visit = await ReadableVisitAsync(actor, visitId, ct);
        var current = await db.Set<ExternalPrescription>().AsNoTracking().Where(x => x.VisitId == visitId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (current == null) return [];
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => x.PrescriptionId == current.Id).ToArrayAsync(ct);
        if (lines.Length == 0) return [];

        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == visit.PatientId, ct);
        var content = await ExaminationAsync(visitId, ct);
        var allergyText = Fold(string.Join(" ", new[]
        {
            content?.Allergies is { State: "Present" } a ? a.Detail : null,
            patient?.IntakeHistoryOther
        }.Where(x => !string.IsNullOrWhiteSpace(x))));

        var medicineIds = lines.Where(l => l.MedicineId.HasValue).Select(l => l.MedicineId!.Value).Distinct().ToArray();
        var catalog = await db.Set<CatalogMedicine>().AsNoTracking().Where(m => medicineIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var warnings = new List<ClinicalAllergyWarning>();

        foreach (var line in lines)
        {
            if (allergyText.Length > 0)
            {
                var hit = Ingredients(line.ActiveIngredient).FirstOrDefault(i => allergyText.Contains(i));
                if (hit != null)
                    warnings.Add(new(line.MedicineName, line.ActiveIngredient, "Allergy", $"Tiền sử dị ứng của bệnh nhân có ghi nhận hoạt chất \"{hit}\" trong thuốc {line.MedicineName}."));
                else if (allergyText.Contains(Fold(line.MedicineName)))
                    warnings.Add(new(line.MedicineName, line.ActiveIngredient, "Allergy", $"Tiền sử dị ứng của bệnh nhân có ghi nhận thuốc {line.MedicineName}."));
                else if (line.MedicineId is { } medicineId && catalog.GetValueOrDefault(medicineId)?.AllergyWarnings is { } groups
                    && Keywords(groups).FirstOrDefault(k => allergyText.Contains(k)) is { } group)
                    warnings.Add(new(line.MedicineName, line.ActiveIngredient, "Allergy", $"Thuốc {line.MedicineName} thuộc nhóm cảnh báo dị ứng \"{group}\" trùng với tiền sử dị ứng của bệnh nhân."));
            }
            if (line.MedicineId is { } contraMedicineId && catalog.GetValueOrDefault(contraMedicineId)?.Contraindications is { Length: > 0 } contra)
                warnings.Add(new(line.MedicineName, line.ActiveIngredient, "Contraindication", $"Chống chỉ định của {line.MedicineName}: {contra}"));
        }

        foreach (var group in lines.SelectMany(l => Ingredients(l.ActiveIngredient).Distinct().Select(i => (Ingredient: i, Line: l)))
                     .GroupBy(x => x.Ingredient).Where(g => g.Select(x => x.Line.Id).Distinct().Count() > 1))
        {
            var names = string.Join(", ", group.Select(x => x.Line.MedicineName).Distinct());
            warnings.Add(new(names, group.Key, "DuplicateIngredient",
                $"Trùng hoạt chất \"{group.Key}\" giữa các thuốc: {names}. Kiểm tra tổng liều để tránh quá liều."));
        }
        return warnings.ToArray();
    }

    private static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch == 'đ' || ch == 'Đ' ? 'd' : char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private static IEnumerable<string> Ingredients(string? activeIngredient) =>
        (activeIngredient ?? "").Split([',', '+', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Fold).Where(x => x.Length >= 3);

    private static IEnumerable<string> Keywords(string text) =>
        text.Split([',', ';', '\n', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Fold).Where(x => x.Length >= 3);

    private async Task<ExaminationContent?> ExaminationAsync(Guid visitId, CancellationToken ct)
    {
        var exam = await db.Set<VisitExamination>().AsNoTracking().SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        if (exam == null || string.IsNullOrWhiteSpace(exam.ContentJson)) return null;
        try { return JsonSerializer.Deserialize<ExaminationContent>(exam.ContentJson, ExaminationService.Json); }
        catch (JsonException) { return null; }
    }

    private static string DiagnosisText(ExaminationContent? content)
    {
        if (content == null) return "";
        var list = (content.Diagnoses ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (list.Count == 0 && !string.IsNullOrWhiteSpace(content.Diagnosis)) list.Add(content.Diagnosis.Trim());
        var text = string.Join("; ", list);
        return text.Length > 1000 ? text[..1000] : text;
    }

    // ---------- staff writes ----------

    public async Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var catalogMedicine = c.MedicineId is { } medicineId && medicineId != Guid.Empty;
        var customMedicine = !string.IsNullOrWhiteSpace(c.CustomMedicineName);
        var hasCustomPayload = c.CustomMedicineName is not null || c.CustomUnit is not null;
        if (catalogMedicine == customMedicine || (catalogMedicine && hasCustomPayload)) throw Fail("MSG04", field: "medicineId", label: "Thuốc");
        if (customMedicine && (c.CustomMedicineName!.Trim().Length > 200 || string.IsNullOrWhiteSpace(c.CustomUnit) || c.CustomUnit.Trim().Length > 100))
            throw Fail("MSG04", field: string.IsNullOrWhiteSpace(c.CustomUnit) ? "customUnit" : "customMedicineName", label: "Thuốc tùy chỉnh");
        var values = Validate(c.Route, c.MorningDose, c.NoonDose, c.AfternoonDose, c.EveningDose, c.MealTiming, c.Days, c.Quantity, c.Instructions);
        var hash = Hash(new { op = "line.save", visitId, lineId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionLineDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        ExternalPrescription rx;
        ExternalPrescriptionLine line;
        var before = "{}";
        if (lineId is { } id)
        {
            line = await db.Set<ExternalPrescriptionLine>().SingleOrDefaultAsync(x => x.Id == id && x.VisitId == visitId, ct) ?? throw Fail("MSG113", 404);
            rx = await db.Set<ExternalPrescription>().SingleAsync(x => x.Id == line.PrescriptionId, ct);
            EnsureEditable(visit, rx);
            if (line.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: line.Version);
            before = JsonSerializer.Serialize(LineDto(line, null), Json);
            if (customMedicine) ApplyCustom(line, c.CustomMedicineName!, c.CustomUnit!, values);
            else
            {
                var medicine = line.MedicineId == c.MedicineId
                    ? await db.Set<CatalogMedicine>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.MedicineId, ct)
                    : await PrescribableAsync(c.MedicineId!.Value, ct);
                Apply(line, medicine ?? throw Fail("MSG113", 404, "medicineId"), values);
            }
            line.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            var current = await CurrentAsync(visitId, ct);
            EnsureEditable(visit, current);
            rx = current ?? NewDraft(visit, actor, 1);
            var now = clock.GetUtcNow();
            line = new ExternalPrescriptionLine
            {
                PrescriptionId = rx.Id, VisitId = visit.Id, PatientId = visit.PatientId, BranchId = visit.BranchId,
                PrescribedByDoctorId = actor, CreatedBy = actor, CreatedAt = now, Status = PrescriptionStatuses.Draft, Version = 1
            };
            if (customMedicine) ApplyCustom(line, c.CustomMedicineName!, c.CustomUnit!, values);
            else
            {
                var medicine = await PrescribableAsync(c.MedicineId!.Value, ct) ?? throw Fail("MSG113", 404, "medicineId");
                Apply(line, medicine, values);
            }
            db.Add(line);
        }
        line.UpdatedBy = actor;
        line.UpdatedAt = clock.GetUtcNow();
        Touch(rx, actor);

        var names = await NamesAsync([line.PrescribedByDoctorId], ct);
        var result = LineDto(line, names.GetValueOrDefault(line.PrescribedByDoctorId));
        Record(actor, visit, null, lineId.HasValue ? "LineUpdate" : "LineCreate", "Kê đơn thuốc tại lượt khám", before, result, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task DeleteLineAsync(int actor, Guid visitId, Guid lineId, Guid requestKey, int expectedVersion, CancellationToken ct)
    {
        if (requestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var hash = Hash(new { op = "line.delete", visitId, lineId, expectedVersion });

        await using var tx = await BeginAsync(ct);
        if ((await ReplayAsync<JsonElement>(actor, requestKey, hash, ct)).Found) return;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        var line = await db.Set<ExternalPrescriptionLine>().SingleOrDefaultAsync(x => x.Id == lineId && x.VisitId == visitId, ct) ?? throw Fail("MSG113", 404);
        var rx = await db.Set<ExternalPrescription>().SingleAsync(x => x.Id == line.PrescriptionId, ct);
        EnsureEditable(visit, rx);
        if (line.Version != expectedVersion) throw Fail("MSG111", 409, currentVersion: line.Version);

        var before = JsonSerializer.Serialize(LineDto(line, null), Json);
        db.Remove(line);
        Touch(rx, actor);
        Record(actor, visit, null, "LineDelete", "Xóa dòng thuốc khỏi đơn nháp", before, new { }, requestKey, hash, new { });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PrescriptionDto> SaveAdviceAsync(int actor, Guid visitId, SavePrescriptionAdvice c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var advice = c.GeneralAdvice?.Trim() ?? "";
        if (advice.Length > 1000) throw Fail("MSG04", field: "generalAdvice", label: "Lời dặn chung");
        var hash = Hash(new { op = "advice", visitId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        var rx = await CurrentAsync(visitId, ct);
        if ((rx?.Version ?? 0) != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: rx?.Version ?? 0);
        EnsureEditable(visit, rx);
        rx ??= NewDraft(visit, actor, 1);
        var before = JsonSerializer.Serialize(new { rx.GeneralAdvice }, Json);
        rx.GeneralAdvice = advice;
        Touch(rx, actor);

        var lines = await db.Set<ExternalPrescriptionLine>().Where(x => x.PrescriptionId == rx.Id).ToArrayAsync(ct);
        var result = await DtoAsync(rx, lines, ct);
        Record(actor, visit, null, "AdviceUpdate", "Cập nhật lời dặn chung", before, new { rx.GeneralAdvice }, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ApplyTemplateResult> ApplyTemplateAsync(int actor, Guid visitId, ApplyPrescriptionTemplateCommand c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var hash = Hash(new { op = "template.apply", visitId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<ApplyTemplateResult>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        var template = await db.Set<PrescriptionTemplate>().AsNoTracking().Include(t => t.Lines)
            .SingleOrDefaultAsync(t => t.Id == c.TemplateId && t.IsActive, ct) ?? throw Fail("MSG113", 404, "templateId");
        var current = await CurrentAsync(visitId, ct);
        if ((current?.Version ?? 0) != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: current?.Version ?? 0);
        EnsureEditable(visit, current);
        var rx = current ?? NewDraft(visit, actor, 1);

        var medicineIds = template.Lines.Select(l => l.MedicineId).Distinct().ToArray();
        var medicines = await db.Set<CatalogMedicine>().AsNoTracking()
            .Where(m => medicineIds.Contains(m.Id) && m.IsActive && m.PrescriptionEligible).ToDictionaryAsync(m => m.Id, ct);
        var now = clock.GetUtcNow();
        var skipped = 0;
        var offset = 0;
        foreach (var t in template.Lines.OrderBy(l => l.SortOrder))
        {
            if (!medicines.TryGetValue(t.MedicineId, out var medicine)) { skipped++; continue; }
            var line = new ExternalPrescriptionLine
            {
                PrescriptionId = rx.Id, VisitId = visit.Id, PatientId = visit.PatientId, BranchId = visit.BranchId,
                PrescribedByDoctorId = actor, Status = PrescriptionStatuses.Draft, Version = 1,
                CreatedBy = actor, CreatedAt = now.AddTicks(offset++), UpdatedBy = actor, UpdatedAt = now
            };
            Apply(line, medicine, new LineValues(t.Route, t.MorningDose, t.NoonDose, t.AfternoonDose, t.EveningDose, t.MealTiming, t.Days, t.Quantity,
                string.IsNullOrWhiteSpace(t.Instructions) ? null : t.Instructions));
            db.Add(line);
        }
        Touch(rx, actor);

        await db.SaveChangesAsync(ct);
        var lines = await db.Set<ExternalPrescriptionLine>().Where(x => x.PrescriptionId == rx.Id).ToArrayAsync(ct);
        var result = new ApplyTemplateResult(await DtoAsync(rx, lines, ct), skipped);
        Record(actor, visit, template.Id, "TemplateApply", $"Áp dụng mẫu đơn thuốc: {template.Name}", "{}", result.Prescription, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionDto> FinalizeAsync(int actor, Guid visitId, FinalizePrescriptionCommand c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        if (c.ClinicalNote?.Length > 500) throw Fail("MSG04", field: "clinicalNote", label: "Ghi chú");
        var hash = Hash(new { op = "finalize", visitId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        var rx = await CurrentAsync(visitId, ct) ?? throw Fail("MSG113", 404);
        if (rx.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: rx.Version);
        EnsureEditable(visit, rx);
        var lines = await db.Set<ExternalPrescriptionLine>().Where(x => x.PrescriptionId == rx.Id).ToArrayAsync(ct);
        if (lines.Length == 0) throw Fail("MSG04", field: "lines", label: "Danh sách thuốc");

        var now = clock.GetUtcNow();
        rx.Diagnosis = DiagnosisText(await ExaminationAsync(visitId, ct));
        rx.Status = PrescriptionStatuses.Finalized;
        rx.FinalizedAt = now;
        rx.FinalizedBy = actor;
        foreach (var line in lines)
        {
            line.Status = PrescriptionStatuses.Finalized;
            line.FinalizedAt = now;
            line.UpdatedBy = actor;
            line.UpdatedAt = now;
            line.Version++;
        }
        rx.VerificationCode = Hash(new
        {
            rx.Id, rx.Revision, rx.PatientId, rx.BranchId, rx.GeneralAdvice, rx.Diagnosis, at = now,
            lines = lines.OrderBy(l => l.CreatedAt).Select(l => new { l.MedicineCode, l.Quantity, l.MorningDose, l.NoonDose, l.AfternoonDose, l.EveningDose, l.MealTiming, l.Days, l.Instructions })
        })[..16];
        Touch(rx, actor);
        if (rx.SupersedesId is { } previousId)
        {
            var previous = await db.Set<ExternalPrescription>().SingleAsync(x => x.Id == previousId, ct);
            previous.Status = PrescriptionStatuses.Superseded;
            previous.Version++;
            var previousLines = await db.Set<ExternalPrescriptionLine>().Where(x => x.PrescriptionId == previousId).ToArrayAsync(ct);
            foreach (var l in previousLines) { l.Status = PrescriptionStatuses.Superseded; l.Version++; }
        }

        var result = await DtoAsync(rx, lines, ct);
        Record(actor, visit, null, "Finalize", string.IsNullOrWhiteSpace(c.ClinicalNote) ? "Hoàn tất kê đơn" : c.ClinicalNote.Trim(), "{}", result, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionDto> CreateCorrectionAsync(int actor, Guid visitId, CreatePrescriptionCorrection c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var reason = c.Reason?.Trim() ?? "";
        if (reason.Length == 0) throw Fail("MSG02", field: "reason", label: "Lý do điều chỉnh");
        if (reason.Length > 500) throw Fail("MSG04", field: "reason", label: "Lý do điều chỉnh");
        var hash = Hash(new { op = "correction", visitId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var visit = await WritableVisitAsync(actor, visitId, ct);
        var signed = await CurrentAsync(visitId, ct) ?? throw Fail("MSG113", 404);
        if (signed.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: signed.Version);
        if (signed.Status != PrescriptionStatuses.Finalized) throw Fail("MSG205", 409, "status", "Đơn thuốc");
        if (visit.Status is not (VisitStatuses.InProgress or VisitStatuses.Completed)) throw Fail("MSG205", 409, "visit", "Lượt khám");

        var draft = NewDraft(visit, actor, signed.Revision + 1);
        draft.SupersedesId = signed.Id;
        draft.CorrectionReason = reason;
        draft.GeneralAdvice = signed.GeneralAdvice;
        var source = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => x.PrescriptionId == signed.Id).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        var now = clock.GetUtcNow();
        var copies = source.Select((s, i) => new ExternalPrescriptionLine
        {
            PrescriptionId = draft.Id, VisitId = s.VisitId, PatientId = s.PatientId, BranchId = s.BranchId, PrescribedByDoctorId = actor,
            MedicineId = s.MedicineId, MedicineCode = s.MedicineCode, MedicineName = s.MedicineName, ActiveIngredient = s.ActiveIngredient,
            Strength = s.Strength, DosageForm = s.DosageForm, Unit = s.Unit, Route = s.Route, MorningDose = s.MorningDose, NoonDose = s.NoonDose,
            AfternoonDose = s.AfternoonDose, EveningDose = s.EveningDose, MealTiming = s.MealTiming, Days = s.Days, Quantity = s.Quantity,
            Instructions = s.Instructions, Status = PrescriptionStatuses.Draft, Version = 1,
            CreatedBy = actor, CreatedAt = now.AddTicks(i), UpdatedBy = actor, UpdatedAt = now
        }).ToArray();
        db.AddRange(copies);

        var result = await DtoAsync(draft, copies, ct);
        Record(actor, visit, null, "CorrectionCreate", reason, JsonSerializer.Serialize(new { signed.Id, signed.Revision }, Json), result, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---------- print ----------

    public async Task<PrescriptionPrintDto> PrintDataAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var visit = await ReadableVisitAsync(actor, visitId, ct);
        var rx = await db.Set<ExternalPrescription>().AsNoTracking().Where(x => x.VisitId == visitId).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct)
            ?? throw Fail("MSG113", 404);
        return await PrintAsync(rx, visit, ct);
    }

    private async Task<PrescriptionPrintDto> PrintAsync(ExternalPrescription rx, Visit visit, CancellationToken ct)
    {
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == rx.PatientId, ct) ?? throw Fail("MSG113", 404);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == rx.BranchId, ct);
        var profile = await db.Set<BranchProfile>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == rx.BranchId, ct);
        var doctorId = rx.FinalizedBy ?? rx.PrescribedByDoctorId;
        var doctor = (await NamesAsync([doctorId], ct)).GetValueOrDefault(doctorId);
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => x.PrescriptionId == rx.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var diagnosis = rx.Diagnosis ?? DiagnosisText(await ExaminationAsync(visit.Id, ct));

        var items = lines.Select((l, i) => new PrescriptionPrintItem(i + 1, l.MedicineName, l.ActiveIngredient, l.Strength, l.DosageForm,
            l.Quantity, VietnameseNumberWords.ToWords(l.Quantity), l.Unit, l.Route, Dosage(l), l.Instructions)).ToArray();
        var signed = rx.Status != PrescriptionStatuses.Draft;
        return new PrescriptionPrintDto(rx.Id, rx.VisitId, rx.Revision, !signed,
            profile?.Name ?? branch?.Name ?? "", profile?.Address, profile?.Phone,
            await assets.DataUrlAsync(rx.BranchId, profile?.LogoAssetId, ct),
            doctor ?? "", null, patient.PatientCode, patient.FullName, patient.BirthDate?.Year,
            patient.Gender switch { PatientGenders.Male => "Nam", "FEMALE" => "Nữ", null => null, _ => "Khác" },
            patient.Address, diagnosis, rx.FinalizedAt ?? rx.UpdatedAt, items, rx.GeneralAdvice,
            signed ? rx.VerificationCode : null,
            signed ? $"ODENTAL-RX|{rx.Id:N}|R{rx.Revision}|{rx.VerificationCode}" : null);
    }

    private static string Dosage(ExternalPrescriptionLine l)
    {
        var parts = new List<string>();
        if (l.MorningDose != "0") parts.Add($"Sáng {l.MorningDose}");
        if (l.NoonDose != "0") parts.Add($"Trưa {l.NoonDose}");
        if (l.AfternoonDose != "0") parts.Add($"Chiều {l.AfternoonDose}");
        if (l.EveningDose != "0") parts.Add($"Tối {l.EveningDose}");
        var meal = l.MealTiming switch { "BeforeMeal" => "trước ăn", "WithMeal" => "trong bữa ăn", _ => "sau ăn" };
        var doses = parts.Count > 0 ? string.Join(", ", parts) : "Theo chỉ dẫn";
        return $"{l.Route}. {doses} ({l.Unit}, {meal}) × {l.Days} ngày";
    }

    // ---------- templates ----------

    public async Task<PrescriptionTemplateDto[]> TemplatesAsync(int actor, string? search, string? diagnosis, string? status, int page, int pageSize, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (search?.Length > 100) throw Fail("MSG04", field: "search", label: "Từ khóa");
        if (diagnosis?.Length > 100) throw Fail("MSG04", field: "diagnosis", label: "Chẩn đoán");
        if (page < 1) throw Fail("MSG04", field: "page");
        if (pageSize is not (20 or 50 or 100)) throw Fail("MSG04", field: "pageSize");
        var q = search?.Trim().ToUpper() ?? "";
        var d = diagnosis?.Trim().ToUpper() ?? "";

        var query = db.Set<PrescriptionTemplate>().AsNoTracking().Include(t => t.Lines).AsQueryable();
        query = (status ?? "Active") switch
        {
            "Active" => query.Where(t => t.IsActive),
            "Inactive" => query.Where(t => !t.IsActive),
            "All" => query,
            _ => throw Fail("MSG04", field: "status", label: "Trạng thái")
        };
        if (q.Length > 0) query = query.Where(t => t.Name.ToUpper().Contains(q) || t.Code.ToUpper().Contains(q));
        if (d.Length > 0) query = query.Where(t => t.DiagnosisKind != null && t.DiagnosisKind.ToUpper().Contains(d));
        var items = await query.OrderBy(t => t.Name).ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return items.Select(TemplateDto).ToArray();
    }

    public async Task<PrescriptionTemplateDto> TemplateAsync(int actor, Guid id, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        var template = await db.Set<PrescriptionTemplate>().AsNoTracking().Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw Fail("MSG113", 404);
        return TemplateDto(template);
    }

    public async Task<PrescriptionTemplateDto> SaveTemplateAsync(int actor, Guid? id, SavePrescriptionTemplate c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var code = c.Code?.Trim() ?? "";
        var name = c.Name?.Trim() ?? "";
        if (code.Length == 0) throw Fail("MSG02", field: "code", label: "Mã mẫu");
        if (code.Length > 40) throw Fail("MSG04", field: "code", label: "Mã mẫu");
        if (name.Length == 0) throw Fail("MSG02", field: "name", label: "Tên mẫu");
        if (name.Length > 200) throw Fail("MSG04", field: "name", label: "Tên mẫu");
        if (c.DiagnosisKind?.Trim().Length > 100) throw Fail("MSG04", field: "diagnosisKind", label: "Chẩn đoán gợi ý");
        if (c.Description?.Trim().Length > 1000) throw Fail("MSG04", field: "description", label: "Mô tả");
        if (c.Lines is not { Length: > 0 }) throw Fail("MSG02", field: "lines", label: "Danh sách thuốc");
        if (c.Lines.Length > PrescriptionRules.MaxTemplateLines) throw Fail("MSG04", field: "lines", label: "Danh sách thuốc");
        var values = c.Lines.Select(l => Validate(l.Route, l.MorningDose, l.NoonDose, l.AfternoonDose, l.EveningDose, l.MealTiming, l.Days, l.Quantity, l.Instructions)).ToArray();
        var hash = Hash(new { op = "template.save", id, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionTemplateDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        PrescriptionTemplate template;
        var before = "{}";
        var now = clock.GetUtcNow();
        if (id.HasValue)
        {
            template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Fail("MSG113", 404);
            if (template.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: template.Version);
            if (!string.Equals(template.Code, code, StringComparison.Ordinal)) throw Fail("MSG04", field: "code", label: "Mã mẫu");
            before = JsonSerializer.Serialize(TemplateDto(template), Json);
            template.Lines.Clear();
            template.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409, currentVersion: 0);
            if (await db.Set<PrescriptionTemplate>().AnyAsync(t => t.Code.ToUpper() == code.ToUpper(), ct)) throw Fail("MSG201", 409, "code", "mẫu đơn thuốc");
            template = new PrescriptionTemplate { Code = code, CreatedBy = actor, CreatedAt = now, IsActive = true, Version = 1 };
            db.Add(template);
        }
        template.Name = name;
        template.DiagnosisKind = string.IsNullOrWhiteSpace(c.DiagnosisKind) ? null : c.DiagnosisKind.Trim();
        template.Description = string.IsNullOrWhiteSpace(c.Description) ? null : c.Description.Trim();
        template.UpdatedBy = actor;
        template.UpdatedAt = now;

        var medicineIds = c.Lines.Select(l => l.MedicineId).Distinct().ToArray();
        var medicines = await db.Set<CatalogMedicine>().AsNoTracking()
            .Where(m => medicineIds.Contains(m.Id) && m.IsActive && m.PrescriptionEligible).ToDictionaryAsync(m => m.Id, ct);
        for (var i = 0; i < c.Lines.Length; i++)
        {
            var input = c.Lines[i];
            var v = values[i];
            if (!medicines.TryGetValue(input.MedicineId, out var med)) throw Fail("MSG113", 404, $"lines[{i}].medicineId");
            template.Lines.Add(new PrescriptionTemplateLine
            {
                TemplateId = template.Id, MedicineId = med.Id, MedicineCode = med.Code, MedicineName = med.Name,
                ActiveIngredient = med.ActiveIngredient, Strength = med.Strength, DosageForm = med.DosageForm, Unit = med.BaseUnit,
                Route = v.Route.Length > 0 ? v.Route : string.IsNullOrWhiteSpace(med.Route) ? "Uống" : med.Route,
                MorningDose = v.Morning, NoonDose = v.Noon, AfternoonDose = v.Afternoon, EveningDose = v.Evening, MealTiming = v.Meal,
                Days = v.Days, Quantity = v.Quantity, Instructions = v.Instructions ?? med.Instructions ?? "", SortOrder = i
            });
        }

        var result = TemplateDto(template);
        Record(actor, null, template.Id, id.HasValue ? "TemplateUpdate" : "TemplateCreate", id.HasValue ? "Cập nhật mẫu đơn thuốc" : "Tạo mẫu đơn thuốc",
            before, result, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionTemplateDto> SetTemplateStateAsync(int actor, Guid id, TemplateStateCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var reason = c.Reason?.Trim() ?? "";
        if (!c.Active && reason.Length == 0) throw Fail("MSG02", field: "reason", label: "Lý do");
        if (reason.Length > 500) throw Fail("MSG04", field: "reason", label: "Lý do");
        var hash = Hash(new { op = "template.state", id, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<PrescriptionTemplateDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines).SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Fail("MSG113", 404);
        if (template.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: template.Version);
        var before = JsonSerializer.Serialize(TemplateDto(template), Json);
        template.IsActive = c.Active;
        template.Version++;
        template.UpdatedBy = actor;
        template.UpdatedAt = clock.GetUtcNow();

        var result = TemplateDto(template);
        Record(actor, null, template.Id, "TemplateState", reason.Length > 0 ? reason : "Khôi phục mẫu đơn thuốc", before, result, c.RequestKey, hash, result);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---------- patient-profile prescriptions (single-step save) ----------

    private async Task<PatientProfilePrescriptionDto> PatientProfileDtoAsync(PatientPrescription rx, CancellationToken ct)
    {
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleAsync(x => x.Id == rx.BranchId, ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.Id == rx.PrescriberEmployeeId, ct);
        var items = await db.Set<PatientPrescriptionItem>().AsNoTracking().Where(x => x.PrescriptionId == rx.Id)
            .OrderBy(x => x.Position).Select(x => new PatientProfilePrescriptionItemDto(x.Id, x.MedicineId, x.MedicineName,
                x.MedicineCode, x.Strength, x.DosageForm, x.Unit, x.Quantity, x.Instructions)).ToArrayAsync(ct);
        return new(rx.Id, rx.PatientId, rx.BranchId, branch.Name, rx.PrescribedOn, rx.Diagnosis, rx.OtherDiagnosis,
            rx.Advice, employee.FullName, rx.Version, items);
    }

    private async Task<PatientProfile> ReadablePatientAsync(int actor, int patientId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "patients.read", ct);
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == patientId, ct)
            ?? throw Fail("MSG113", 404);
        if (patient.IsArchived) throw Fail("MSG113", 404);
        return patient;
    }

    public async Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct)
    {
        await ReadablePatientAsync(actor, patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (!state.Roles.Contains(AccountRoles.Dentist)) throw Fail("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actor, ct);
        return new(state.BranchIds, employee.FullName);
    }

    public async Task<PatientProfilePrescriptionDto[]> PatientProfilePrescriptionsAsync(int actor, int patientId, CancellationToken ct)
    {
        await ReadablePatientAsync(actor, patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.read", ct);
        var rows = await db.Set<PatientPrescription>().AsNoTracking().Where(x => x.PatientId == patientId && state.BranchIds.Contains(x.BranchId))
            .OrderByDescending(x => x.PrescribedOn).ThenByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var result = new PatientProfilePrescriptionDto[rows.Length];
        for (var i = 0; i < rows.Length; i++) result[i] = await PatientProfileDtoAsync(rows[i], ct);
        return result;
    }

    public async Task<PatientProfilePrescriptionDto> PatientProfilePrescriptionAsync(int actor, int patientId, Guid id, CancellationToken ct)
    {
        await ReadablePatientAsync(actor, patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.read", ct);
        var rx = await db.Set<PatientPrescription>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId && state.BranchIds.Contains(x.BranchId), ct)
            ?? throw Fail("MSG113", 404);
        return await PatientProfileDtoAsync(rx, ct);
    }

    public async Task<PatientProfilePrescriptionDto> SavePatientProfilePrescriptionAsync(int actor, int patientId, Guid? id, SavePatientPrescription c, CancellationToken ct)
    {
        var patient = await ReadablePatientAsync(actor, patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.manage", ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actor, ct);
        if (!state.Roles.Contains(AccountRoles.Dentist) || !state.BranchIds.Contains(c.BranchId)) throw Fail("MSG06", 403);
        if (c.RequestKey == Guid.Empty || c.Items is not { Length: > 0 and <= 30 }) throw Fail("MSG04", field: "items", label: "Danh sách thuốc");
        if (c.Diagnosis?.Length > 1000 || c.OtherDiagnosis?.Length > 1000 || c.Advice?.Length > 4000)
            throw Fail("MSG04", field: "diagnosis");
        if (c.Items.Any(x => x.MedicineId == Guid.Empty || x.Quantity is < 1 or > 10000 || x.Instructions?.Length > 1000)) throw Fail("MSG04", field: "items");
        var hash = Hash(new { op = "patient-profile.save", patientId, id, c });
        await using var tx = await BeginAsync(ct);
        var receipt = await db.Set<PatientPrescriptionReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != hash) throw Fail("MSG111", 409);
            var replayed = await db.Set<PatientPrescription>().AsNoTracking().SingleAsync(x => x.Id == receipt.PrescriptionId, ct);
            await tx.CommitAsync(ct); return await PatientProfileDtoAsync(replayed, ct);
        }
        PatientPrescription rx;
        var beforeJson = "{}";
        if (id.HasValue)
        {
            rx = await db.Set<PatientPrescription>().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId, ct) ?? throw Fail("MSG113", 404);
            if (!state.BranchIds.Contains(rx.BranchId)) throw Fail("MSG06", 403);
            if (rx.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: rx.Version);
            beforeJson = JsonSerializer.Serialize(rx, Json);
            rx.Items.Clear(); rx.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409);
            rx = new PatientPrescription { PatientId = patient.Id, PrescriberEmployeeId = employee.Id, CreatedAt = clock.GetUtcNow() };
            db.Add(rx);
        }
        var meds = await db.Set<CatalogMedicine>().AsNoTracking().Where(x => c.Items.Select(i => i.MedicineId).Contains(x.Id) && x.IsActive && x.PrescriptionEligible).ToDictionaryAsync(x => x.Id, ct);
        if (meds.Count != c.Items.Select(x => x.MedicineId).Distinct().Count()) throw Fail("MSG113", 404, "items");
        rx.BranchId = c.BranchId; rx.PrescriberEmployeeId = employee.Id; rx.PrescribedOn = c.PrescribedOn; rx.Diagnosis = c.Diagnosis?.Trim() ?? "";
        rx.OtherDiagnosis = c.OtherDiagnosis?.Trim(); rx.Advice = c.Advice?.Trim(); rx.UpdatedAt = clock.GetUtcNow();
        for (var index = 0; index < c.Items.Length; index++)
        {
            var input = c.Items[index]; var med = meds[input.MedicineId];
            rx.Items.Add(new PatientPrescriptionItem { PrescriptionId = rx.Id, MedicineId = med.Id, MedicineCode = med.Code,
                MedicineName = med.Name, Strength = med.Strength, DosageForm = med.DosageForm, Unit = med.BaseUnit,
                Quantity = input.Quantity, Instructions = input.Instructions?.Trim() ?? med.Instructions ?? "", Position = index });
        }
        db.Add(new PatientPrescriptionReceipt { ActorId = actor, RequestKey = c.RequestKey, Fingerprint = hash, PrescriptionId = rx.Id });
        db.Add(new PrescriptionAudit { ActorId = actor, BranchId = rx.BranchId, Action = id.HasValue ? "PatientPrescriptionUpdate" : "PatientPrescriptionCreate",
            Reason = id.HasValue ? "Cập nhật đơn tại hồ sơ bệnh nhân" : "Kê đơn tại hồ sơ bệnh nhân", BeforeJson = beforeJson,
            AfterJson = JsonSerializer.Serialize(new { rx.Id, rx.PatientId, rx.BranchId, rx.PrescribedOn, rx.Diagnosis, rx.OtherDiagnosis, rx.Advice, Items = c.Items }, Json), At = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await PatientProfileDtoAsync(rx, ct);
    }

    private IQueryable<int> VerifiedLinkedPatients(int actor) => db.Set<PatientAccountLink>().AsNoTracking()
        .Where(x => x.AccountId == actor && x.Status == PatientLinkStatus.Verified && x.PatientId != null).Select(x => x.PatientId!.Value);

    public async Task<PatientProfilePrescriptionDto[]> OwnPatientPrescriptionsAsync(int actor, CancellationToken ct)
    {
        var patients = VerifiedLinkedPatients(actor);
        var rows = await db.Set<PatientPrescription>().AsNoTracking().Where(x => patients.Contains(x.PatientId))
            .OrderByDescending(x => x.PrescribedOn).ThenByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var result = new PatientProfilePrescriptionDto[rows.Length];
        for (var i = 0; i < rows.Length; i++) result[i] = await PatientProfileDtoAsync(rows[i], ct);
        return result;
    }

    public async Task<PatientProfilePrescriptionDto> OwnPatientPrescriptionAsync(int actor, Guid id, CancellationToken ct)
    {
        var patients = VerifiedLinkedPatients(actor);
        var rx = await db.Set<PatientPrescription>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && patients.Contains(x.PatientId), ct)
            ?? throw Fail("MSG113", 404);
        return await PatientProfileDtoAsync(rx, ct);
    }

    // ---------- patient self-service ----------

    private IQueryable<int> LinkedPatients(int actor) => db.Set<PatientAccountLink>().AsNoTracking()
        .Where(x => x.AccountId == actor && x.Status == PatientLinkStatus.Verified && x.PatientId != null).Select(x => x.PatientId!.Value);

    public async Task<PatientPrescriptionDto[]> PatientPrescriptionsAsync(int actor, CancellationToken ct)
    {
        var patients = LinkedPatients(actor);
        var signed = await db.Set<ExternalPrescription>().AsNoTracking()
            .Where(x => x.Status == PrescriptionStatuses.Finalized && patients.Contains(x.PatientId))
            .OrderByDescending(x => x.FinalizedAt).ThenByDescending(x => x.Id).Take(200).ToArrayAsync(ct);
        return await PatientDtosAsync(signed, ct);
    }

    public async Task<PatientPrescriptionDetailDto> PatientPrescriptionAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var patients = LinkedPatients(actor);
        var rx = await db.Set<ExternalPrescription>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.VisitId == visitId && x.Status == PrescriptionStatuses.Finalized && patients.Contains(x.PatientId), ct)
            ?? throw Fail("MSG113", 404);
        var visit = await db.Set<Visit>().AsNoTracking().SingleAsync(x => x.Id == visitId, ct);
        var dto = (await PatientDtosAsync([rx], ct))[0];
        return new PatientPrescriptionDetailDto(dto, await PrintAsync(rx, visit, ct));
    }

    private async Task<PatientPrescriptionDto[]> PatientDtosAsync(ExternalPrescription[] rows, CancellationToken ct)
    {
        if (rows.Length == 0) return [];
        var ids = rows.Select(x => x.Id).ToArray();
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => ids.Contains(x.PrescriptionId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var branchIds = rows.Select(x => x.BranchId).Distinct().ToArray();
        var profiles = await db.Set<BranchProfile>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var branches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var names = await NamesAsync(rows.Select(x => x.FinalizedBy ?? x.PrescribedByDoctorId), ct);
        return rows.Select(rx => new PatientPrescriptionDto(rx.VisitId, rx.Id, rx.Revision, rx.Revision > 1,
            profiles.GetValueOrDefault(rx.BranchId) ?? branches.GetValueOrDefault(rx.BranchId) ?? "",
            names.GetValueOrDefault(rx.FinalizedBy ?? rx.PrescribedByDoctorId) ?? "",
            rx.Diagnosis ?? "", rx.FinalizedAt ?? rx.UpdatedAt, rx.GeneralAdvice, ExternalPurchaseNotice,
            lines.Where(l => l.PrescriptionId == rx.Id).Select(l => new PatientPrescriptionMedicineDto(
                l.MedicineName, l.Strength, l.DosageForm, l.Quantity, l.Unit, l.Route,
                l.MorningDose, l.NoonDose, l.AfternoonDose, l.EveningDose, l.MealTiming, l.Days, l.Instructions)).ToArray())).ToArray();
    }
}
