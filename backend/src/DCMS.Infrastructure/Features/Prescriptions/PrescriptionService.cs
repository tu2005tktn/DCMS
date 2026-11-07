using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Prescriptions;

public sealed class PrescriptionService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IPrescriptionService
{
    public async Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct)
    {
        var state = await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId && !x.IsArchived, ct)) throw Error("MSG113", 404);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actor, ct) ?? throw Error("MSG06", 403);
        var branches = await db.Set<SchedulingMember>().AsNoTracking().Where(x => x.EmployeeId == employee.Id && x.Active && x.CanDentist && state.BranchIds.Contains(x.BranchId))
            .Select(x => x.BranchId).ToArrayAsync(ct);
        return new(branches, employee.FullName);
    }
    async Task<PatientPrescriptionDto> PatientDto(PatientPrescription x, CancellationToken ct)
    {
        var name = await db.Employees.AsNoTracking().Where(e => e.Id == x.PrescriberEmployeeId).Select(e => e.FullName).SingleAsync(ct);
        return new(x.Id, x.PatientId, x.BranchId, x.PrescribedOn, x.Diagnosis, x.OtherDiagnosis, x.Advice,
            x.PrescriberEmployeeId, name, x.Version, x.Items.OrderBy(i => i.Position).Select(i =>
                new PatientPrescriptionItemDto(i.Id, i.MedicineId, i.MedicineCode, i.MedicineName, i.Strength,
                    i.DosageForm, i.Unit, i.Quantity, i.Instructions)).ToArray());
    }
    public async Task<PatientPrescriptionDto[]> PatientListAsync(int actor, int patientId, CancellationToken ct)
    {
        var state = await access.RequireAsync(actor, "prescriptions.read", ct);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId, ct)) throw Error("MSG113", 404);
        var docs = await db.Set<PatientPrescription>().AsNoTracking().Include(x => x.Items)
            .Where(x => x.PatientId == patientId && state.BranchIds.Contains(x.BranchId))
            .OrderByDescending(x => x.PrescribedOn).ThenByDescending(x => x.CreatedAt).ToArrayAsync(ct);
        var result = new List<PatientPrescriptionDto>();
        foreach (var doc in docs) result.Add(await PatientDto(doc, ct));
        return result.ToArray();
    }
    public async Task<PatientPrescriptionDto> PatientDetailAsync(int actor, int patientId, Guid id, CancellationToken ct)
    {
        var doc = await db.Set<PatientPrescription>().AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "prescriptions.read", doc.BranchId, ct);
        return await PatientDto(doc, ct);
    }
    public async Task<PatientPrescriptionDto> SavePatientAsync(int actor, int patientId, Guid? id, SavePatientPrescription c, CancellationToken ct)
    {
        if (c.RequestKey == Guid.Empty || c.BranchId <= 0 || c.PrescribedOn == default ||
            string.IsNullOrWhiteSpace(c.Diagnosis) || c.Diagnosis.Length > 1000 || c.OtherDiagnosis?.Length > 1000 ||
            c.Advice?.Length > 4000 || c.Items is not { Length: >= 1 and <= 50 } ||
            c.Items.Any(i => i.MedicineId == Guid.Empty || i.Quantity is < 1 or > 10_000 || i.Instructions?.Length > 1000)) throw Error("MSG04");
        await access.OperationalAsync(actor, "prescriptions.manage", c.BranchId, ct);
        if (!await db.Patients.AnyAsync(x => x.Id == patientId && !x.IsArchived, ct)) throw Error("MSG113", 404);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actor, ct) ?? throw Error("MSG06", 403);
        if (!await db.Set<SchedulingMember>().AnyAsync(x => x.BranchId == c.BranchId && x.EmployeeId == employee.Id && x.Active && x.CanDentist, ct)) throw Error("MSG06", 403);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362089)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { patientId, id, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PatientPrescriptionReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return await PatientDetailAsync(actor, patientId, prior.PrescriptionId, ct);
        }
        PatientPrescription doc;
        if (id.HasValue)
        {
            doc = await db.Set<PatientPrescription>().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId, ct) ?? throw Error("MSG113", 404);
            await access.OperationalAsync(actor, "prescriptions.manage", doc.BranchId, ct);
            if (doc.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            doc.Version++;
            db.RemoveRange(doc.Items); doc.Items.Clear();
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Error("MSG111", 409);
            doc = new() { PatientId = patientId, CreatedAt = clock.GetUtcNow() }; db.Add(doc);
        }
        var ids = c.Items.Select(x => x.MedicineId).Distinct().ToArray();
        var medicines = await db.Set<CatalogMedicine>().AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive && x.PrescriptionEligible).ToDictionaryAsync(x => x.Id, ct);
        if (medicines.Count != ids.Length) throw Error("MSG113", 404);
        doc.BranchId = c.BranchId; doc.PrescribedOn = c.PrescribedOn; doc.Diagnosis = c.Diagnosis.Trim();
        doc.OtherDiagnosis = c.OtherDiagnosis?.Trim(); doc.Advice = c.Advice?.Trim(); doc.PrescriberEmployeeId = employee.Id;
        doc.UpdatedAt = clock.GetUtcNow();
        for (var n = 0; n < c.Items.Length; n++)
        {
            var item = c.Items[n]; var medicine = medicines[item.MedicineId];
            doc.Items.Add(new PatientPrescriptionItem { MedicineId = medicine.Id, MedicineCode = medicine.Code,
                MedicineName = medicine.Name, Strength = medicine.Strength, DosageForm = medicine.DosageForm,
                Unit = medicine.BaseUnit, Quantity = item.Quantity,
                Instructions = string.IsNullOrWhiteSpace(item.Instructions) ? medicine.Instructions ?? "" : item.Instructions.Trim(), Position = n });
        }
        db.Add(new PatientPrescriptionReceipt { ActorId = actor, RequestKey = c.RequestKey, Fingerprint = hash, PrescriptionId = doc.Id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await PatientDto(doc, ct);
    }
    static PrescriptionLineDto Dto(ExternalPrescriptionLine x) => new(x.Id, x.VisitId, x.MedicineId, x.MedicineCode,
        x.MedicineName, x.Strength, x.DosageForm, x.Unit, x.Instructions, x.Quantity, x.Version);
    async Task<Visit> Visit(int actor, Guid id, CancellationToken ct, string permission = "prescriptions.read")
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
        return await db.Set<CatalogMedicine>().AsNoTracking().Where(x => x.IsActive && x.PrescriptionEligible &&
            (q == "" || x.NormalizedName.Contains(q) || x.Code.Contains(q))).OrderBy(x => x.NormalizedName).Take(50)
            .Select(x => new PrescriptionMedicineOption(x.Id, x.Code, x.Name, x.ActiveIngredient, x.Strength,
                x.DosageForm, x.BaseUnit, x.Packaging, x.Instructions)).ToArrayAsync(ct);
    }
    public async Task<PrescriptionLineDto[]> LinesAsync(int actor, Guid visitId, CancellationToken ct)
    {
        await Visit(actor, visitId, ct);
        return (await db.Set<ExternalPrescriptionLine>().AsNoTracking().Where(x => x.VisitId == visitId)
            .OrderBy(x => x.CreatedAt).ToArrayAsync(ct)).Select(Dto).ToArray();
    }
    public async Task<PrescriptionLineDto> SaveLineAsync(int actor, Guid visitId, Guid? lineId, SavePrescriptionLine c, CancellationToken ct)
    {
        var visit = await Visit(actor, visitId, ct, "prescriptions.manage");
        if (c.RequestKey == Guid.Empty || c.Quantity is < 1 or > 10_000 || c.Instructions?.Length > 1000) throw Error("MSG04");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362088)", ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { visitId, lineId, c }, ClinicSettingsStore.Json))));
        var prior = await db.Set<PrescriptionCommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (prior != null)
        {
            if (prior.Fingerprint != hash) throw Error("MSG111", 409);
            return JsonSerializer.Deserialize<PrescriptionLineDto>(prior.ResultJson, ClinicSettingsStore.Json)!;
        }
        await access.OperationalAsync(actor, "prescriptions.manage", visit.BranchId, ct);
        var medicine = await db.Set<CatalogMedicine>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.MedicineId && x.IsActive && x.PrescriptionEligible, ct)
            ?? throw Error("MSG113", 404);
        ExternalPrescriptionLine line;
        if (lineId.HasValue)
        {
            line = await db.Set<ExternalPrescriptionLine>().SingleOrDefaultAsync(x => x.Id == lineId && x.VisitId == visitId, ct) ?? throw Error("MSG113", 404);
            if (line.Version != c.ExpectedVersion) throw Error("MSG111", 409);
            line.Version++;
        }
        else { if (c.ExpectedVersion != 0) throw Error("MSG111", 409); line = new() { VisitId = visit.Id, PatientId = visit.PatientId, BranchId = visit.BranchId, CreatedBy = actor, CreatedAt = clock.GetUtcNow() }; db.Add(line); }
        line.MedicineId = medicine.Id; line.MedicineCode = medicine.Code; line.MedicineName = medicine.Name;
        line.Strength = medicine.Strength; line.DosageForm = medicine.DosageForm; line.Unit = medicine.BaseUnit;
        line.Quantity = c.Quantity; line.Instructions = string.IsNullOrWhiteSpace(c.Instructions) ? medicine.Instructions ?? "" : c.Instructions.Trim();
        line.UpdatedBy = actor; line.UpdatedAt = clock.GetUtcNow();
        var result = Dto(line);
        db.Add(new PrescriptionCommandReceipt { ActorId = actor, RequestKey = c.RequestKey, Fingerprint = hash, ResultJson = JsonSerializer.Serialize(result, ClinicSettingsStore.Json) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
}
