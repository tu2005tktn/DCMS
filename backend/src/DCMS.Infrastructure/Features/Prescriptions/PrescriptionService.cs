using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Prescriptions;

public sealed class PrescriptionService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IPrescriptionService
{
    static PrescriptionLineDto Dto(ExternalPrescriptionLine x) => new(x.Id, x.VisitId, x.MedicineId, x.MedicineCode,
        x.MedicineName, x.Strength, x.DosageForm, x.Unit, x.Instructions, x.Quantity, x.Version);
    async Task<Visit> Visit(int actor, Guid id, CancellationToken ct)
    {
        var visit = await db.Set<Visit>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await access.OperationalAsync(actor, "examinations.manage", visit.BranchId, ct);
        return visit;
    }
    public async Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int branchId, string? search, CancellationToken ct)
    {
        await access.OperationalAsync(actor, "examinations.manage", branchId, ct);
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
        var visit = await Visit(actor, visitId, ct);
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
        await access.OperationalAsync(actor, "examinations.manage", visit.BranchId, ct);
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
