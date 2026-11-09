using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Prescriptions;

// Development-only synthetic data (docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md "Demo data"):
// verified self links for the synthetic mobile accounts and a few saved patient-profile prescriptions.
public sealed class DevelopmentPrescriptionSeeder(DcmsDbContext db, TimeProvider clock)
{
    private const string DemoBranch = "S09 - Chi nhánh thử nghiệm";
    private const string DemoNote = "Dữ liệu minh họa — không phải hướng dẫn điều trị thực tế.";

    private sealed record DemoItem(string MedicineCode, int Quantity, string Instructions);
    private sealed record DemoPrescription(Guid Id, string PatientCode, int DaysAgo, string Diagnosis, string? OtherDiagnosis, string Advice, DemoItem[] Items);

    private static readonly (string Account, string PatientCode)[] Links =
    [
        ("patient.demo", "BN-DEMO-0001"),
        ("patient2.demo", "BN-DEMO-0002"),
    ];

    private static readonly DemoPrescription[] Prescriptions =
    [
        new(Guid.Parse("5d3f2a10-7c1e-4b8a-9f00-000000000001"), "BN-DEMO-0001", 14, "Viêm tủy răng", "Răng 36 đau nhức về đêm",
            $"Súc miệng nước muối ấm sau ăn. Tái khám sau 7 ngày. {DemoNote}",
            [new("MED-STARTER-001", 10, "Uống 1 viên khi đau, cách nhau ít nhất 6 giờ, tối đa 4 viên/ngày."),
             new("MED-STARTER-003", 15, "Uống 1 viên x 3 lần/ngày sau ăn, trong 5 ngày.")]),
        new(Guid.Parse("5d3f2a10-7c1e-4b8a-9f00-000000000002"), "BN-DEMO-0001", 2, "Biến chứng sau nhổ răng/tiểu phẫu", null,
            $"Không súc miệng mạnh trong 24 giờ đầu, chườm lạnh má ngoài. Ăn đồ mềm, nguội. {DemoNote}",
            [new("MED-STARTER-006", 10, "Uống 1 viên x 2 lần/ngày sau ăn sáng và tối, trong 5 ngày."),
             new("MED-STARTER-007", 12, "Ngậm dưới lưỡi 2 viên x 2 lần/ngày."),
             new("MED-STARTER-005", 250, "Súc miệng 10 ml x 2 lần/ngày sau khi chải răng, từ ngày thứ 2.")]),
        new(Guid.Parse("5d3f2a10-7c1e-4b8a-9f00-000000000003"), "BN-DEMO-0002", 5, "Viêm nha chu", "Chảy máu nướu khi chải răng",
            $"Chải răng bằng bàn chải lông mềm, dùng chỉ nha khoa mỗi tối. Lấy cao răng định kỳ 6 tháng. {DemoNote}",
            [new("MED-STARTER-010", 1, "Bôi lên vùng nướu viêm 2 lần/ngày sau khi chải răng, trong 7 ngày."),
             new("MED-STARTER-005", 250, "Súc miệng 10 ml x 2 lần/ngày.")]),
    ];

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6462362002)", ct);
        var now = clock.GetUtcNow();

        var dentist = await (from e in db.Employees join u in db.Users on e.AccountId equals u.Id
                             where u.UserName == "dentist.demo" select new { Employee = e, AccountId = u.Id }).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Chưa có tài khoản dentist.demo: chạy --seed-demo trước.");
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(x => x.Name == DemoBranch, ct)
            ?? throw new InvalidOperationException($"Chưa có chi nhánh \"{DemoBranch}\": chạy --seed-demo trước.");
        var codes = Links.Select(x => x.PatientCode).Concat(Prescriptions.Select(x => x.PatientCode)).Distinct().ToArray();
        var patients = await db.Patients.AsNoTracking().Where(x => codes.Contains(x.PatientCode)).ToDictionaryAsync(x => x.PatientCode, ct);

        // Verified links only for the synthetic accounts/profiles, and only when neither side has an open or verified link.
        var linked = 0;
        foreach (var (accountName, patientCode) in Links)
        {
            var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == accountName && x.AccountType == "Public", ct);
            if (account == null || !patients.TryGetValue(patientCode, out var patient)) continue;
            var busy = await db.PatientAccountLinks.AnyAsync(x =>
                (x.AccountId == account.Id && (x.Status == PatientLinkStatus.Pending || x.Status == PatientLinkStatus.Verified))
                || (x.PatientId == patient.Id && x.Status == PatientLinkStatus.Verified), ct);
            if (busy) continue;
            var link = new PatientAccountLink
            {
                RequestCode = PatientLinkSupport.NewRequestCode(now), AccountId = account.Id, PatientId = patient.Id,
                Status = PatientLinkStatus.Verified, RequestedAt = now, DeclaredFullName = patient.FullName,
                DeclaredBirthDate = patient.BirthDate, DeclaredPatientCode = patient.PatientCode,
                ReviewingEmployeeId = dentist.Employee.Id, VerificationNote = "Liên kết dữ liệu demo (Development)", ReviewedAt = now
            };
            db.PatientAccountLinks.Add(link);
            await db.SaveChangesAsync(ct);
            db.PatientLinkAudits.Add(new PatientLinkAudit
            {
                LinkId = link.Id, ActorAccountId = dentist.AccountId, Action = "DevelopmentSeed", Reason = "Dữ liệu giả development",
                OccurredAt = now, AfterJson = $"{{\"developmentDemo\":true,\"patientId\":{patient.Id}}}"
            });
            linked++;
        }

        var medicineCodes = Prescriptions.SelectMany(x => x.Items).Select(x => x.MedicineCode).Distinct().ToArray();
        var medicines = await db.CatalogMedicines.AsNoTracking()
            .Where(x => medicineCodes.Contains(x.Code) && x.IsActive && x.PrescriptionEligible).ToDictionaryAsync(x => x.Code, ct);
        var ids = Prescriptions.Select(x => x.Id).ToArray();
        var existing = await db.Set<PatientPrescription>().AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var added = 0;
        foreach (var demo in Prescriptions)
        {
            if (existing.Contains(demo.Id) || !patients.TryGetValue(demo.PatientCode, out var patient)) continue;
            if (demo.Items.Any(x => !medicines.ContainsKey(x.MedicineCode))) continue;
            var rx = new PatientPrescription
            {
                Id = demo.Id, PatientId = patient.Id, BranchId = branch.Id, PrescribedOn = today.AddDays(-demo.DaysAgo),
                Diagnosis = demo.Diagnosis, OtherDiagnosis = demo.OtherDiagnosis, Advice = demo.Advice,
                PrescriberEmployeeId = dentist.Employee.Id, CreatedAt = now.AddDays(-demo.DaysAgo), UpdatedAt = now.AddDays(-demo.DaysAgo)
            };
            for (var index = 0; index < demo.Items.Length; index++)
            {
                var item = demo.Items[index];
                var medicine = medicines[item.MedicineCode];
                rx.Items.Add(new PatientPrescriptionItem
                {
                    PrescriptionId = rx.Id, MedicineId = medicine.Id, MedicineCode = medicine.Code, MedicineName = medicine.Name,
                    Strength = medicine.Strength, DosageForm = medicine.DosageForm, Unit = medicine.BaseUnit,
                    Quantity = item.Quantity, Instructions = item.Instructions, Position = index
                });
            }
            db.Add(rx);
            db.Add(new PrescriptionAudit
            {
                ActorId = dentist.AccountId, BranchId = branch.Id, Action = "PatientPrescriptionCreate", Reason = "Dữ liệu giả development",
                AfterJson = $"{{\"developmentDemo\":true,\"id\":\"{rx.Id}\"}}", At = now
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return $"Đơn thuốc demo: thêm {linked} liên kết mobile Verified, {added}/{Prescriptions.Length} đơn.";
    }
}
