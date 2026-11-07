using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class PrescriptionModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<PatientPrescription>(b =>
        {
            b.ToTable("PATIENT_PRESCRIPTION"); b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Diagnosis).HasMaxLength(1000); b.Property(x => x.OtherDiagnosis).HasMaxLength(1000);
            b.Property(x => x.Advice).HasMaxLength(4000); b.HasIndex(x => new { x.PatientId, x.PrescribedOn });
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.PrescriberEmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PatientPrescriptionItem>(b =>
        {
            b.ToTable("PATIENT_PRESCRIPTION_ITEM", t => t.HasCheckConstraint("CK_PATIENT_PRESCRIPTION_ITEM_QUANTITY", "\"Quantity\" > 0"));
            b.HasKey(x => x.Id); b.Property(x => x.MedicineCode).HasMaxLength(40); b.Property(x => x.MedicineName).HasMaxLength(200);
            b.Property(x => x.Strength).HasMaxLength(200); b.Property(x => x.DosageForm).HasMaxLength(200);
            b.Property(x => x.Unit).HasMaxLength(100); b.Property(x => x.Instructions).HasMaxLength(1000);
            b.HasOne<CatalogMedicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientPrescriptionReceipt>(b =>
        {
            b.ToTable("PATIENT_PRESCRIPTION_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientPrescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExternalPrescriptionLine>(b =>
        {
            b.ToTable("EXTERNAL_PRESCRIPTION_LINE", t => t.HasCheckConstraint("CK_EXTERNAL_PRESCRIPTION_QUANTITY", "\"Quantity\" > 0"));
            b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.MedicineCode).HasMaxLength(40); b.Property(x => x.MedicineName).HasMaxLength(200);
            b.Property(x => x.Strength).HasMaxLength(200); b.Property(x => x.DosageForm).HasMaxLength(200);
            b.Property(x => x.Unit).HasMaxLength(100); b.Property(x => x.Instructions).HasMaxLength(1000);
            b.HasIndex(x => x.VisitId); b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CatalogMedicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PrescriptionCommandReceipt>(b =>
        {
            b.ToTable("PRESCRIPTION_COMMAND_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
