using System.Security.Cryptography;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Prescriptions;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Settings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DCMS.Infrastructure.Features.Prescriptions;

// Direct patient-profile prescriptions (S23) using simplified 11-column PRESCRIPTION and 10-column CATALOG_MEDICINE.
public sealed class PrescriptionService(DcmsDbContext db, ISettingsAccess access, TimeProvider clock) : IPrescriptionService
{
    private const string Lock = "SELECT pg_advisory_xact_lock(6462362088)";
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

    public async Task<PrescriptionMedicineOption[]> OptionsAsync(int actor, int? branchId, string? search, CancellationToken ct)
    {
        if (branchId is { } branch) await access.OperationalAsync(actor, "prescriptions.manage", branch, ct);
        else await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (search?.Length > 100) throw Fail("MSG04", field: "q", label: "Từ khóa");
        var q = search?.Trim() ?? "";
        var active = await db.CatalogMedicines.AsNoTracking()
            .Where(x => x.IsActive && x.PrescriptionEligible)
            .ToListAsync(ct);
        return active
            .Where(x => q == ""
                || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.Spec.ActiveIngredient != null && x.Spec.ActiveIngredient.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Name.ToUpperInvariant()).ThenBy(x => x.Id).Take(50)
            .Select(x => new PrescriptionMedicineOption(
                x.Id, x.Code, x.Name, x.Spec.ActiveIngredient, x.Spec.Strength, x.Spec.DosageForm,
                x.BaseUnit, x.Spec.Packaging, x.Spec.Instructions, x.ReferencePrice))
            .ToArray();
    }

    private async Task<PatientProfilePrescriptionDto> PatientProfileDtoAsync(PatientPrescription rx, CancellationToken ct)
    {
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleAsync(x => x.Id == rx.BranchId, ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.Id == rx.PrescriberEmployeeId, ct);
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleAsync(x => x.Id == rx.PatientId, ct);
        var items = rx.Items.OrderBy(x => x.Position).Select(x => new PatientProfilePrescriptionItemDto(x.Id, x.MedicineId, x.MedicineName,
                x.MedicineCode, x.Strength, x.DosageForm, x.Unit, x.Quantity, x.Instructions)).ToArray();
        return new(rx.Id, rx.PatientId, rx.BranchId, branch.Name, rx.PrescribedOn, rx.Diagnosis, rx.OtherDiagnosis,
            rx.Advice, employee.FullName, rx.Version, items, employee.Id, NonEmpty(branch.Address), NonEmpty(branch.Phone),
            NonEmpty(branch.Email), patient.PatientCode, patient.FullName, patient.BirthDate, patient.Gender);
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateOnly ClinicToday() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    // Active members allowed to examine (CanDentist) in the given branches, with an active account.
    private async Task<PrescriptionDoctorOption[]> DoctorOptionsAsync(int[] branchIds, CancellationToken ct) =>
        await (from m in db.SchedulingMembers.AsNoTracking()
               join e in db.Employees.AsNoTracking() on m.EmployeeId equals e.Id
               join u in db.Users.AsNoTracking() on e.AccountId equals u.Id
               where branchIds.Contains(m.BranchId) && m.Active && m.CanDentist && u.IsActive
               orderby e.FullName
               select new PrescriptionDoctorOption(e.Id, e.FullName, m.BranchId)).ToArrayAsync(ct);

    private async Task<PatientProfile> ReadablePatientAsync(int patientId, CancellationToken ct)
    {
        var patient = await db.Set<PatientProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == patientId, ct)
            ?? throw Fail("MSG113", 404);
        if (patient.IsArchived) throw Fail("MSG113", 404);
        return patient;
    }

    public async Task<PatientPrescriptionContext> PatientContextAsync(int actor, int patientId, CancellationToken ct)
    {
        await ReadablePatientAsync(patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.manage", ct);
        if (!state.Roles.Contains(AccountRoles.Dentist)) throw Fail("MSG06", 403);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actor, ct);
        var branchIds = state.BranchIds;
        var schedulingBranches = await db.Set<SchedulingBranch>().AsNoTracking().Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var branches = branchIds.Where(schedulingBranches.ContainsKey).Select(id =>
        {
            var b = schedulingBranches[id];
            return new PrescriptionBranchOption(id, b.Name, NonEmpty(b.Address),
                NonEmpty(b.Phone), NonEmpty(b.Email));
        }).OrderBy(x => x.Name).ToArray();
        var doctors = (await DoctorOptionsAsync(branchIds, ct)).ToList();
        // The signed-in prescriber is always selectable in each of their branches, even without a CanDentist membership.
        foreach (var branch in branches.Where(b => !doctors.Any(d => d.BranchId == b.Id && d.EmployeeId == employee.Id)))
            doctors.Add(new PrescriptionDoctorOption(employee.Id, employee.FullName, branch.Id));
        return new(branchIds, employee.FullName, employee.Id, branches, doctors.OrderBy(x => x.Name).ToArray());
    }

    public async Task<PatientProfilePrescriptionDto[]> PatientProfilePrescriptionsAsync(int actor, int patientId, CancellationToken ct)
    {
        await ReadablePatientAsync(patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.read", ct);
        var rows = await db.PatientPrescriptions.AsNoTracking().Where(x => x.PatientId == patientId && state.BranchIds.Contains(x.BranchId))
            .OrderByDescending(x => x.PrescribedOn).ThenByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var result = new PatientProfilePrescriptionDto[rows.Length];
        for (var i = 0; i < rows.Length; i++) result[i] = await PatientProfileDtoAsync(rows[i], ct);
        return result;
    }

    public async Task<PatientProfilePrescriptionDto> PatientProfilePrescriptionAsync(int actor, int patientId, Guid id, CancellationToken ct)
    {
        await ReadablePatientAsync(patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.read", ct);
        var rx = await db.PatientPrescriptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId && state.BranchIds.Contains(x.BranchId), ct)
            ?? throw Fail("MSG113", 404);
        return await PatientProfileDtoAsync(rx, ct);
    }

    public async Task<PatientProfilePrescriptionDto> SavePatientProfilePrescriptionAsync(int actor, int patientId, Guid? id, SavePatientPrescription c, CancellationToken ct)
    {
        var patient = await ReadablePatientAsync(patientId, ct);
        var state = await access.RequireAsync(actor, "prescriptions.manage", ct);
        var employee = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == actor, ct);
        if (!state.Roles.Contains(AccountRoles.Dentist) || !state.BranchIds.Contains(c.BranchId)) throw Fail("MSG06", 403);
        if (c.RequestKey == Guid.Empty || c.Items is not { Length: > 0 and <= 30 }) throw Fail("MSG04", field: "items", label: "Danh sách thuốc");
        if (c.Diagnosis?.Length > 1000 || c.OtherDiagnosis?.Length > 1000 || c.Advice?.Length > 4000)
            throw Fail("MSG04", field: "diagnosis");
        if (c.Items.Any(x => x.MedicineId == Guid.Empty || x.Quantity is < 1 or > 10000 || x.Instructions?.Length > 1000 || string.IsNullOrWhiteSpace(x.Unit) || x.Unit.Trim().Length > 40)) throw Fail("MSG04", field: "items");
        if (c.PrescribedOn > ClinicToday() || c.PrescribedOn.Year < 2000) throw Fail("MSG04", field: "prescribedOn", label: "Ngày kê đơn");
        var prescriberId = c.PrescriberEmployeeId ?? employee.Id;
        if (prescriberId != employee.Id && !(await DoctorOptionsAsync([c.BranchId], ct)).Any(x => x.EmployeeId == prescriberId))
            throw Fail("MSG04", field: "prescriberEmployeeId", label: "Bác sĩ kê đơn");
        var hash = Hash(new { op = "patient-profile.save", patientId, id, c });
        await using var tx = await BeginAsync(ct);
        var receipt = await db.Set<PatientPrescriptionReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor && x.RequestKey == c.RequestKey, ct);
        if (receipt != null)
        {
            if (receipt.Fingerprint != hash) throw Fail("MSG111", 409);
            var replayed = await db.PatientPrescriptions.AsNoTracking().SingleAsync(x => x.Id == receipt.PrescriptionId, ct);
            await tx.CommitAsync(ct); return await PatientProfileDtoAsync(replayed, ct);
        }
        PatientPrescription rx;
        var beforeJson = "{}";
        if (id.HasValue)
        {
            rx = await db.PatientPrescriptions.SingleOrDefaultAsync(x => x.Id == id && x.PatientId == patientId, ct) ?? throw Fail("MSG113", 404);
            if (!state.BranchIds.Contains(rx.BranchId)) throw Fail("MSG06", 403);
            if (rx.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: rx.Version);
            beforeJson = JsonSerializer.Serialize(rx, Json);
            rx.Items.Clear(); rx.Version++;
        }
        else
        {
            if (c.ExpectedVersion != 0) throw Fail("MSG111", 409);
            rx = new PatientPrescription { PatientId = patient.Id, PrescriberEmployeeId = prescriberId, CreatedAt = clock.GetUtcNow() };
            db.Add(rx);
        }
        var meds = await db.CatalogMedicines.AsNoTracking().Where(x => c.Items.Select(i => i.MedicineId).Contains(x.Id) && x.IsActive && x.PrescriptionEligible).ToDictionaryAsync(x => x.Id, ct);
        if (meds.Count != c.Items.Select(x => x.MedicineId).Distinct().Count()) throw Fail("MSG113", 404, "items");
        rx.BranchId = c.BranchId; rx.PrescriberEmployeeId = prescriberId; rx.PrescribedOn = c.PrescribedOn; rx.Diagnosis = c.Diagnosis?.Trim() ?? "";
        rx.OtherDiagnosis = c.OtherDiagnosis?.Trim(); rx.Advice = c.Advice?.Trim();
        for (var index = 0; index < c.Items.Length; index++)
        {
            var input = c.Items[index]; var med = meds[input.MedicineId];
            rx.Items.Add(new PatientPrescriptionItem
            {
                IsExternalPurchase = true,
                PrescriptionId = rx.Id, MedicineId = med.Id, MedicineCode = med.Code,
                MedicineName = med.Name, Strength = med.Spec.Strength, DosageForm = null,
                Unit = input.Unit?.Trim() ?? "",
                Quantity = input.Quantity, Instructions = input.Instructions?.Trim() ?? med.Spec.Instructions ?? "", Position = index
            });
        }
        db.Add(new PatientPrescriptionReceipt { ActorId = actor, RequestKey = c.RequestKey, Fingerprint = hash, PrescriptionId = rx.Id });
        db.Add(new PrescriptionAudit { ActorId = actor, BranchId = rx.BranchId, Action = id.HasValue ? "PatientPrescriptionUpdate" : "PatientPrescriptionCreate",
            Reason = id.HasValue ? "Cập nhật đơn tại hồ sơ bệnh nhân" : "Kê đơn tại hồ sơ bệnh nhân", BeforeJson = beforeJson,
            AfterJson = JsonSerializer.Serialize(new { rx.Id, rx.PatientId, rx.BranchId, rx.PrescriberEmployeeId, rx.PrescribedOn, rx.Diagnosis, rx.OtherDiagnosis, rx.Advice, Items = c.Items }, Json), At = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await PatientProfileDtoAsync(rx, ct);
    }

    private IQueryable<int> VerifiedLinkedPatients(int actor) => db.Set<PatientAccountLink>().AsNoTracking()
        .Where(x => x.AccountId == actor && x.Status == PatientLinkStatus.Verified && x.PatientId != null).Select(x => x.PatientId!.Value);

    public async Task<PatientProfilePrescriptionDto[]> OwnPatientPrescriptionsAsync(int actor, CancellationToken ct)
    {
        var patients = VerifiedLinkedPatients(actor);
        var rows = await db.PatientPrescriptions.AsNoTracking().Where(x => patients.Contains(x.PatientId))
            .OrderByDescending(x => x.PrescribedOn).ThenByDescending(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var result = new PatientProfilePrescriptionDto[rows.Length];
        for (var i = 0; i < rows.Length; i++) result[i] = await PatientProfileDtoAsync(rows[i], ct);
        return result;
    }

    public async Task<PatientProfilePrescriptionDto> OwnPatientPrescriptionAsync(int actor, Guid id, CancellationToken ct)
    {
        var patients = VerifiedLinkedPatients(actor);
        var rx = await db.PatientPrescriptions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && patients.Contains(x.PatientId), ct)
            ?? throw Fail("MSG113", 404);
        return await PatientProfileDtoAsync(rx, ct);
    }
}
