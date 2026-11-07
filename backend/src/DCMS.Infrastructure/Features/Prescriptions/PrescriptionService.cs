using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Prescriptions;

public sealed class PrescriptionService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IPrescriptionService
{
    private static PrescriptionLineDto LineDto(ExternalPrescriptionLine x, string? doctorName = null) => new(
        x.Id, x.VisitId, x.PatientId, x.BranchId, x.PrescribedByDoctorId, doctorName,
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

    private async Task<Visit> Visit(int actor, Guid id, CancellationToken ct, string permission = "prescriptions.read")
    {
        var visit = await db.Set<Visit>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, permission, visit.BranchId, ct);
        return visit;
    }

    public async Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int branchId, string? search, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "prescriptions.manage", branchId, ct);
        if (search?.Length > 100) throw Error("MSG04");
        var q = search?.Trim().ToUpperInvariant() ?? "";
        return await db.Set<CatalogMedicine>().AsNoTracking()
            .Where(x => x.IsActive && x.PrescriptionEligible &&
                (q == "" || x.NormalizedName.Contains(q) || x.Code.Contains(q) || (x.ActiveIngredient != null && x.ActiveIngredient.ToUpper().Contains(q))))
            .OrderBy(x => x.NormalizedName).Take(50)
            .Select(x => new PrescriptionMedicineOption(
                x.Id, x.Code, x.Name, x.ActiveIngredient, x.Strength, x.DosageForm,
                x.BaseUnit, x.Route, x.Packaging, x.Contraindications, x.AllergyWarnings,
                x.Instructions, x.ReferencePrice))
            .ToArrayAsync(ct);
    }

    public async Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct)
    {
        await Visit(actor, visitId, ct);
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking()
            .Where(x => x.VisitId == visitId).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        var doctorIds = lines.Select(x => x.PrescribedByDoctorId).Distinct().ToArray();
        var doctors = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(u => doctorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return lines.Select(l => LineDto(l, doctors.GetValueOrDefault(l.PrescribedByDoctorId))).ToArray();
    }

    public async Task<ClinicalAllergyWarning[]> CheckSafetyAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct);
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == visit.PatientId, ct);
        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => x.VisitId == visitId).ToArrayAsync(ct);

        var warnings = new List<ClinicalAllergyWarning>();
        var allergyText = $"{patient?.IntakeHistoryOther} {patient?.Note}".ToLowerInvariant();

        // 1. Check allergy against patient history
        foreach (var line in lines)
        {
            var medName = line.MedicineName.ToLowerInvariant();
            var activeIng = (line.ActiveIngredient ?? "").ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(activeIng) && allergyText.Contains(activeIng))
            {
                warnings.Add(new ClinicalAllergyWarning(line.MedicineName, line.ActiveIngredient, "Allergy",
                    $"Bệnh nhân có tiền sử ghi nhận liên quan đến hoạt chất: {line.ActiveIngredient}"));
            }
            else if (allergyText.Contains(medName))
            {
                warnings.Add(new ClinicalAllergyWarning(line.MedicineName, line.ActiveIngredient, "Allergy",
                    $"Bệnh nhân có tiền sử ghi nhận liên quan đến thuốc: {line.MedicineName}"));
            }
        }

        // 2. Check duplicate active ingredients
        var ingredientGroups = lines.Where(l => !string.IsNullOrWhiteSpace(l.ActiveIngredient))
            .GroupBy(l => l.ActiveIngredient!.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1);

        foreach (var group in ingredientGroups)
        {
            var medNames = string.Join(", ", group.Select(x => x.MedicineName).Distinct());
            warnings.Add(new ClinicalAllergyWarning(medNames, group.Key, "DuplicateIngredient",
                $"Phát hiện trùng lặp hoạt chất '{group.Key}' giữa các thuốc: {medNames}. Vui lòng kiểm tra tổng liều để tránh quá liều."));
        }

        return warnings.ToArray();
    }

    public async Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine c, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct, "prescriptions.manage");
        if (c.RequestKey == Guid.Empty || c.Quantity is < 1 or > 10_000 || c.Instructions?.Length > 1000)
            throw Error("MSG04");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { visitId, lineId, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionLineDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var medicine = await db.Set<CatalogMedicine>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == c.MedicineId && x.IsActive && x.PrescriptionEligible, ct)
            ?? throw Error("MSG113", 404);

        ExternalPrescriptionLine line;
        string beforeJson = "{}";
        if (lineId.HasValue)
        {
            line = await db.Set<ExternalPrescriptionLine>().SingleOrDefaultAsync(x => x.Id == lineId && x.VisitId == visitId, ct)
                ?? throw Error("MSG113", 404);
            if (line.Status == "Finalized")
                throw Error("MSG205", 409); // Cannot directly modify finalized prescription line
            if (line.Version != c.ExpectedVersion)
                throw Error("MSG111", 409);

            beforeJson = JsonSerializer.Serialize(line, ClinicSettingsStore.Json);
            line.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Error("MSG111", 409);
            line = new ExternalPrescriptionLine
            {
                VisitId = visit.Id,
                PatientId = visit.PatientId,
                BranchId = visit.BranchId,
                PrescribedByDoctorId = actor,
                CreatedBy = actor,
                CreatedAt = clock.GetUtcNow(),
                Status = "Draft"
            };
            db.Add(line);
        }

        line.MedicineId = medicine.Id;
        line.MedicineCode = medicine.Code;
        line.MedicineName = medicine.Name;
        line.ActiveIngredient = medicine.ActiveIngredient;
        line.Strength = medicine.Strength;
        line.DosageForm = medicine.DosageForm;
        line.Unit = medicine.BaseUnit;
        line.Route = string.IsNullOrWhiteSpace(c.Route) ? (medicine.Route ?? "Uống") : c.Route.Trim();
        line.MorningDose = c.MorningDose ?? "0";
        line.NoonDose = c.NoonDose ?? "0";
        line.AfternoonDose = c.AfternoonDose ?? "0";
        line.EveningDose = c.EveningDose ?? "0";
        line.MealTiming = c.MealTiming ?? "AfterMeal";
        line.Days = c.Days.GetValueOrDefault(1);
        line.Quantity = c.Quantity;
        line.Instructions = string.IsNullOrWhiteSpace(c.Instructions) ? (medicine.Instructions ?? "") : c.Instructions.Trim();
        line.UpdatedBy = actor;
        line.UpdatedAt = clock.GetUtcNow();

        var doctor = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor, ct);
        var result = LineDto(line, doctor?.FullName);
        var afterJson = JsonSerializer.Serialize(line, ClinicSettingsStore.Json);

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            BranchId = visit.BranchId,
            VisitId = visit.Id,
            Action = lineId.HasValue ? "LineUpdate" : "LineCreate",
            Reason = "Kê đơn thuốc tại lượt khám",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = c.RequestKey,
            Fingerprint = hash,
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task DeleteLineAsync(int actor, Guid visitId, Guid lineId, Guid requestKey, int expectedVersion, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct, "prescriptions.manage");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { visitId, lineId, requestKey, expectedVersion }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == requestKey, ct);
        if (prior != null) return;

        var line = await db.Set<ExternalPrescriptionLine>().SingleOrDefaultAsync(x => x.Id == lineId && x.VisitId == visitId, ct)
            ?? throw Error("MSG113", 404);
        if (line.Status == "Finalized") throw Error("MSG205", 409);
        if (line.Version != expectedVersion) throw Error("MSG111", 409);

        var beforeJson = JsonSerializer.Serialize(line, ClinicSettingsStore.Json);
        db.Remove(line);

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            BranchId = visit.BranchId,
            VisitId = visit.Id,
            Action = "LineDelete",
            Reason = "Xóa dòng thuốc khỏi đơn nháp",
            BeforeJson = beforeJson,
            AfterJson = "{}",
            At = clock.GetUtcNow()
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = requestKey,
            Fingerprint = hash,
            ResultJson = "{}"
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PrescriptionLineDto[]> ApplyTemplateAsync(int actor, Guid visitId, ApplyPrescriptionTemplateCommand command, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct, "prescriptions.manage");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { visitId, command }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == command.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionLineDto[]>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines)
            .AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TemplateId && t.IsActive, ct)
            ?? throw Error("MSG113", 404);

        var now = clock.GetUtcNow();
        var doctor = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor, ct);
        var createdLines = new List<ExternalPrescriptionLine>();

        foreach (var tLine in template.Lines.OrderBy(l => l.SortOrder))
        {
            var line = new ExternalPrescriptionLine
            {
                VisitId = visit.Id,
                PatientId = visit.PatientId,
                BranchId = visit.BranchId,
                PrescribedByDoctorId = actor,
                MedicineId = tLine.MedicineId,
                MedicineCode = tLine.MedicineCode,
                MedicineName = tLine.MedicineName,
                ActiveIngredient = tLine.ActiveIngredient,
                Strength = tLine.Strength,
                DosageForm = tLine.DosageForm,
                Unit = tLine.Unit,
                Route = tLine.Route,
                MorningDose = tLine.MorningDose,
                NoonDose = tLine.NoonDose,
                AfternoonDose = tLine.AfternoonDose,
                EveningDose = tLine.EveningDose,
                MealTiming = tLine.MealTiming,
                Days = tLine.Days,
                Quantity = tLine.Quantity,
                Instructions = tLine.Instructions,
                Status = "Draft",
                CreatedBy = actor,
                CreatedAt = now,
                UpdatedBy = actor,
                UpdatedAt = now,
                Version = 1
            };
            db.Add(line);
            createdLines.Add(line);
        }

        var result = createdLines.Select(l => LineDto(l, doctor?.FullName)).ToArray();

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            BranchId = visit.BranchId,
            VisitId = visit.Id,
            TemplateId = template.Id,
            Action = "TemplateApply",
            Reason = $"Áp dụng mẫu đơn thuốc: {template.Name}",
            BeforeJson = "{}",
            AfterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json),
            At = now
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = command.RequestKey,
            Fingerprint = hash,
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionLineDto[]> FinalizeAsync(int actor, Guid visitId, FinalizePrescriptionCommand command, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct, "prescriptions.manage");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { visitId, command }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == command.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionLineDto[]>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var lines = await db.Set<ExternalPrescriptionLine>().Where(x => x.VisitId == visitId).ToArrayAsync(ct);
        if (lines.Length == 0) throw Error("MSG04"); // Cannot finalize empty prescription

        var now = clock.GetUtcNow();
        foreach (var line in lines)
        {
            line.Status = "Finalized";
            line.FinalizedAt = now;
            line.UpdatedBy = actor;
            line.UpdatedAt = now;
            line.Version++;
        }

        var doctor = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor, ct);
        var result = lines.Select(l => LineDto(l, doctor?.FullName)).ToArray();

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            BranchId = visit.BranchId,
            VisitId = visit.Id,
            Action = "Finalize",
            Reason = command.ClinicalNote ?? "Hoàn tất và ký số đơn thuốc",
            BeforeJson = "{}",
            AfterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json),
            At = now
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = command.RequestKey,
            Fingerprint = hash,
            ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json)
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionPrintDto> PrintDataAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct);
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == visit.PatientId, ct)
            ?? throw Error("MSG113", 404);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == visit.BranchId, ct);
        var branchProfile = await db.Set<BranchProfile>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == visit.BranchId, ct);
        var doctor = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == visit.DentistId, ct);
        var employee = await db.Set<EmployeeProfile>().AsNoTracking().SingleOrDefaultAsync(e => e.AccountId == visit.DentistId, ct);

        // Get clinical examination diagnosis
        var exam = await db.Set<VisitExamination>().AsNoTracking().SingleOrDefaultAsync(x => x.VisitId == visitId, ct);
        string diagnosis = "Khám răng miệng định kỳ";
        if (exam != null && !string.IsNullOrWhiteSpace(exam.ContentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(exam.ContentJson);
                if (doc.RootElement.TryGetProperty("diagnosis", out var diagProp))
                    diagnosis = diagProp.GetString() ?? diagnosis;
                else if (doc.RootElement.TryGetProperty("summary", out var sumProp))
                    diagnosis = sumProp.GetString() ?? diagnosis;
            }
            catch { }
        }

        var lines = await db.Set<ExternalPrescriptionLine>().AsNoTracking()
            .Where(x => x.VisitId == visitId).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);

        var items = lines.Select((l, i) =>
        {
            var doseParts = new List<string>();
            if (l.MorningDose != "0") doseParts.Add($"Sáng {l.MorningDose} {l.Unit}");
            if (l.NoonDose != "0") doseParts.Add($"Trưa {l.NoonDose} {l.Unit}");
            if (l.AfternoonDose != "0") doseParts.Add($"Chiều {l.AfternoonDose} {l.Unit}");
            if (l.EveningDose != "0") doseParts.Add($"Tối {l.EveningDose} {l.Unit}");
            var doseStr = doseParts.Count > 0 ? string.Join(", ", doseParts) : $"{l.Quantity} {l.Unit}";
            var meal = l.MealTiming == "BeforeMeal" ? "trước ăn" : l.MealTiming == "WithMeal" ? "trong bữa ăn" : "sau ăn";

            return new PrescriptionPrintItem(
                i + 1,
                l.MedicineName,
                l.ActiveIngredient,
                l.Strength,
                l.DosageForm,
                l.Quantity,
                l.Unit,
                l.Route,
                $"{doseStr} ({meal}) x {l.Days} ngày",
                $"{l.Route}. {doseStr} ({meal}) x {l.Days} ngày. {l.Instructions}".Trim());
        }).ToArray();

        var printTime = lines.FirstOrDefault()?.FinalizedAt ?? clock.GetUtcNow();
        var qrData = $"ODENTAL-RX:{visit.Id}:{patient.PatientCode}:{printTime:yyyyMMddHHmmss}";

        return new PrescriptionPrintDto(
            visit.Id,
            "HỆ THỐNG NHA KHOA KỸ THUẬT CAO ODENTAL",
            branchProfile?.Name ?? branch?.Name ?? "Chi nhánh trung tâm",
            branchProfile?.Address ?? "Hà Nội, Việt Nam",
            branchProfile?.Phone ?? "1900 6868",
            branchProfile?.LogoAssetId.HasValue == true ? $"/api/assets/{branchProfile.LogoAssetId}" : null,
            doctor?.FullName ?? "Bác sĩ điều trị",
            employee?.LicenseNumber ?? "012345/BYT-CCHN",
            patient.PatientCode,
            patient.FullName,
            patient.BirthDate?.Year,
            patient.Gender == "MALE" ? "Nam" : patient.Gender == "FEMALE" ? "Nữ" : "Khác",
            patient.Address ?? "Chưa cập nhật",
            diagnosis,
            printTime,
            items,
            "Uống thuốc đúng liều lượng chỉ dẫn. Mang theo đơn thuốc khi tái khám. Liên hệ hotline nếu có phản ứng bất thường.",
            qrData);
    }

    public async Task<PrescriptionTemplateDto[]> TemplatesAsync(int actor, string? search, string? diagnosis, int page, int pageSize, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        var q = search?.Trim().ToUpperInvariant() ?? "";
        var d = diagnosis?.Trim().ToUpperInvariant() ?? "";

        var query = db.Set<PrescriptionTemplate>().Include(t => t.Lines)
            .AsNoTracking().Where(t => t.IsActive);

        if (!string.IsNullOrEmpty(q))
            query = query.Where(t => t.Name.ToUpper().Contains(q) || t.Code.ToUpper().Contains(q));
        if (!string.IsNullOrEmpty(d))
            query = query.Where(t => t.DiagnosisKind != null && t.DiagnosisKind.ToUpper().Contains(d));

        var items = await query.OrderBy(t => t.Name).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return items.Select(TemplateDto).ToArray();
    }

    public async Task<PrescriptionTemplateDto> TemplateAsync(int actor, Guid id, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        var template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines)
            .AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw Error("MSG113", 404);
        return TemplateDto(template);
    }

    public async Task<PrescriptionTemplateDto> SaveTemplateAsync(int actor, Guid? id, SavePrescriptionTemplate command, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (command.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(command.Name) || command.Lines == null || command.Lines.Length == 0)
            throw Error("MSG04");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, command }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == command.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionTemplateDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        PrescriptionTemplate template;
        string beforeJson = "{}";
        var now = clock.GetUtcNow();

        if (id.HasValue)
        {
            template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines)
                .SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Error("MSG113", 404);
            if (template.Version != command.ExpectedVersion) throw Error("MSG111", 409);

            beforeJson = JsonSerializer.Serialize(TemplateDto(template), ClinicSettingsStore.Json);
            template.Lines.Clear();
            template.Version++;
        }
        else
        {
            if (command.ExpectedVersion != 0) throw Error("MSG111", 409);
            var exists = await db.Set<PrescriptionTemplate>().AnyAsync(t => t.Code == command.Code, ct);
            if (exists) throw Error("MSG201", 409); // Unique code constraint

            template = new PrescriptionTemplate
            {
                Code = command.Code.Trim(),
                CreatedBy = actor,
                CreatedAt = now,
                IsActive = true
            };
            db.Add(template);
        }

        template.Name = command.Name.Trim();
        template.DiagnosisKind = command.DiagnosisKind?.Trim();
        template.Description = command.Description?.Trim();
        template.UpdatedBy = actor;
        template.UpdatedAt = now;

        var medicineIds = command.Lines.Select(l => l.MedicineId).Distinct().ToArray();
        var medicines = await db.Set<CatalogMedicine>().AsNoTracking()
            .Where(m => medicineIds.Contains(m.Id) && m.IsActive).ToDictionaryAsync(m => m.Id, ct);

        foreach (var lineInput in command.Lines)
        {
            if (!medicines.TryGetValue(lineInput.MedicineId, out var med))
                throw Error("MSG113", 404);

            template.Lines.Add(new PrescriptionTemplateLine
            {
                TemplateId = template.Id,
                MedicineId = med.Id,
                MedicineCode = med.Code,
                MedicineName = med.Name,
                ActiveIngredient = med.ActiveIngredient,
                Strength = med.Strength,
                DosageForm = med.DosageForm,
                Unit = med.BaseUnit,
                Route = lineInput.Route,
                MorningDose = lineInput.MorningDose,
                NoonDose = lineInput.NoonDose,
                AfternoonDose = lineInput.AfternoonDose,
                EveningDose = lineInput.EveningDose,
                MealTiming = lineInput.MealTiming,
                Days = lineInput.Days,
                Quantity = lineInput.Quantity,
                Instructions = string.IsNullOrWhiteSpace(lineInput.Instructions) ? (med.Instructions ?? "") : lineInput.Instructions,
                SortOrder = lineInput.SortOrder
            });
        }

        var result = TemplateDto(template);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            TemplateId = template.Id,
            Action = id.HasValue ? "TemplateUpdate" : "TemplateCreate",
            Reason = id.HasValue ? "Cập nhật mẫu đơn thuốc" : "Tạo mẫu đơn thuốc mới",
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = now
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = command.RequestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PrescriptionTemplateDto> SetTemplateStateAsync(int actor, Guid id, Guid requestKey, int expectedVersion, bool active, string reason, CancellationToken ct)
    {
        await access.RequireAsync(actor, "prescriptions.manage", ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, requestKey, expectedVersion, active, reason }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == requestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionTemplateDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }

        var template = await db.Set<PrescriptionTemplate>().Include(t => t.Lines)
            .SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw Error("MSG113", 404);
        if (template.Version != expectedVersion) throw Error("MSG111", 409);

        var beforeJson = JsonSerializer.Serialize(TemplateDto(template), ClinicSettingsStore.Json);
        template.IsActive = active;
        template.Version++;
        template.UpdatedBy = actor;
        template.UpdatedAt = clock.GetUtcNow();

        var result = TemplateDto(template);
        var afterJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json);

        db.Add(new PrescriptionAudit
        {
            ActorId = actor,
            TemplateId = template.Id,
            Action = "TemplateState",
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            At = clock.GetUtcNow()
        });

        db.Add(new PrescriptionCommandReceipt
        {
            ActorId = actor,
            RequestKey = requestKey,
            Fingerprint = hash,
            ResultJson = afterJson
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PatientPrescriptionDto[]> PatientPrescriptionsAsync(int actor, CancellationToken ct)
    {
        // 1. Verify patient link
        var link = await db.Set<PatientAccountLink>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccountId == actor && x.Status == PatientLinkStatus.Verified, ct)
            ?? throw Error("MSG06", 403);

        if (!link.PatientId.HasValue) return [];

        var visits = await db.Set<Visit>().AsNoTracking()
            .Where(v => v.PatientId == link.PatientId.Value).Select(v => v.Id).ToArrayAsync(ct);

        var finalizedLines = await db.Set<ExternalPrescriptionLine>().AsNoTracking()
            .Where(l => visits.Contains(l.VisitId) && l.Status == "Finalized")
            .OrderByDescending(l => l.FinalizedAt).ToArrayAsync(ct);

        if (finalizedLines.Length == 0) return [];

        var visitGroups = finalizedLines.GroupBy(l => l.VisitId);
        var result = new List<PatientPrescriptionDto>();

        foreach (var group in visitGroups)
        {
            var first = group.First();
            var visit = await db.Set<Visit>().AsNoTracking().SingleOrDefaultAsync(v => v.Id == group.Key, ct);
            var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(b => b.Id == first.BranchId, ct);
            var doctor = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(u => u.Id == first.PrescribedByDoctorId, ct);

            var exam = await db.Set<VisitExamination>().AsNoTracking().SingleOrDefaultAsync(x => x.VisitId == group.Key, ct);
            string diag = "Khám răng định kỳ";
            if (exam != null && !string.IsNullOrWhiteSpace(exam.ContentJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(exam.ContentJson);
                    if (doc.RootElement.TryGetProperty("diagnosis", out var diagProp)) diag = diagProp.GetString() ?? diag;
                }
                catch { }
            }

            var meds = group.Select(l => new PatientPrescriptionMedicineDto(
                l.MedicineName, l.Strength, l.DosageForm, l.Quantity, l.Unit, l.Route,
                l.MorningDose, l.NoonDose, l.AfternoonDose, l.EveningDose, l.MealTiming,
                l.Days, l.Instructions)).ToArray();

            result.Add(new PatientPrescriptionDto(
                group.Key,
                branch?.Name ?? "Chi nhánh cơ sở",
                doctor?.FullName ?? "Bác sĩ phụ trách",
                diag,
                first.FinalizedAt ?? first.CreatedAt,
                first.Status,
                "Đơn thuốc chỉ định mua ngoài tại các hiệu thuốc tư nhân hoặc nhà thuốc bệnh viện. Phòng khám không bán thuốc tại chỗ.",
                meds));
        }

        return result.ToArray();
    }

    public async Task<PatientPrescriptionDto> PatientPrescriptionAsync(int actor, Guid visitId, CancellationToken ct)
    {
        var list = await PatientPrescriptionsAsync(actor, ct);
        return list.FirstOrDefault(x => x.VisitId == visitId) ?? throw Error("MSG113", 404);
    }
}
