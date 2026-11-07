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
        model.Entity<ExternalPrescription>(b =>
        {
            b.ToTable("EXTERNAL_PRESCRIPTION", t =>
            {
                t.HasCheckConstraint("CK_EXTERNAL_PRESCRIPTION_STATUS", "\"Status\" IN ('Draft','Finalized','Superseded')");
                t.HasCheckConstraint("CK_EXTERNAL_PRESCRIPTION_REVISION", "\"Revision\" >= 1");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.GeneralAdvice).HasMaxLength(1000);
            b.Property(x => x.Diagnosis).HasMaxLength(1000);
            b.Property(x => x.CorrectionReason).HasMaxLength(500);
            b.Property(x => x.VerificationCode).HasMaxLength(16);
            b.HasIndex(x => new { x.VisitId, x.Revision }).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.Status });
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExternalPrescription>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PrescribedByDoctorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.FinalizedBy).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ExternalPrescriptionLine>(b =>
        {
            b.ToTable("EXTERNAL_PRESCRIPTION_LINE", t => t.HasCheckConstraint("CK_EXTERNAL_PRESCRIPTION_QUANTITY", "\"Quantity\" > 0"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.PrescriptionId);
            b.HasOne<ExternalPrescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.MedicineCode).HasMaxLength(40);
            b.Property(x => x.MedicineName).HasMaxLength(200);
            b.Property(x => x.ActiveIngredient).HasMaxLength(200);
            b.Property(x => x.Strength).HasMaxLength(200);
            b.Property(x => x.DosageForm).HasMaxLength(200);
            b.Property(x => x.Unit).HasMaxLength(100);
            b.Property(x => x.Route).HasMaxLength(40).HasDefaultValue("Uống");
            b.Property(x => x.MorningDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.NoonDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.AfternoonDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.EveningDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.MealTiming).HasMaxLength(30).HasDefaultValue("AfterMeal");
            b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Draft");
            b.Property(x => x.Instructions).HasMaxLength(1000);
            b.HasIndex(x => x.VisitId);
            b.HasIndex(x => x.PatientId);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CatalogMedicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PrescriptionTemplate>(b =>
        {
            b.ToTable("PRESCRIPTION_TEMPLATE");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.DiagnosisKind).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PrescriptionTemplateLine>(b =>
        {
            b.ToTable("PRESCRIPTION_TEMPLATE_LINE", t => t.HasCheckConstraint("CK_PRESCRIPTION_TEMPLATE_QUANTITY", "\"Quantity\" > 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.MedicineCode).HasMaxLength(40);
            b.Property(x => x.MedicineName).HasMaxLength(200);
            b.Property(x => x.ActiveIngredient).HasMaxLength(200);
            b.Property(x => x.Strength).HasMaxLength(200);
            b.Property(x => x.DosageForm).HasMaxLength(200);
            b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Route).HasMaxLength(40).HasDefaultValue("Uống");
            b.Property(x => x.MorningDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.NoonDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.AfternoonDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.EveningDose).HasMaxLength(20).HasDefaultValue("0");
            b.Property(x => x.MealTiming).HasMaxLength(30).HasDefaultValue("AfterMeal");
            b.Property(x => x.Instructions).HasMaxLength(1000);
            b.HasOne<CatalogMedicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PrescriptionAudit>(b =>
        {
            b.ToTable("PRESCRIPTION_AUDIT");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PrescriptionCommandReceipt>(b =>
        {
            b.ToTable("PRESCRIPTION_COMMAND_RECEIPT");
            b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
