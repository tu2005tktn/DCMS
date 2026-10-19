using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class TreatmentModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TreatmentDossier>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.UpdatedAt });
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Diagnosis).HasMaxLength(4000);
            b.Property(x => x.Note).HasMaxLength(4000);
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.ClosureOutcome).HasMaxLength(32);
            b.Property(x => x.ClosureReason).HasMaxLength(1000);
            b.Property(x => x.PlanState).HasMaxLength(16);
            b.Property(x => x.Goals).HasMaxLength(4000);
            b.Property(x => x.PaymentTermsJson).HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingMember>().WithMany().HasForeignKey(x => new { x.BranchId, EmployeeId = x.DentistId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentItem>(b =>
        {
            b.ToTable("TREATMENT_PLAN_ITEM");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlannedOrder });
            b.Property(x => x.ServiceName).HasMaxLength(160);
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Quantity).HasPrecision(12, 3);
            b.Property(x => x.Decision).HasMaxLength(16);
            b.Property(x => x.Progress).HasMaxLength(16);
            b.Property(x => x.Counseling).HasMaxLength(4000);
            b.Property(x => x.DecisionMaker).HasMaxLength(160);
            b.Property(x => x.RepresentativeBasis).HasMaxLength(1000);
            b.Property(x => x.WarrantyPolicy).HasMaxLength(160);
            b.Property(x => x.WarrantyPolicyVersion).HasMaxLength(40);
            b.Property(x => x.WarrantyScope).HasMaxLength(1000);
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_ITEM_VALUES", "\"Quantity\" > 0 AND \"AgreedUnitPriceVnd\" >= 0"));
        });
        model.Entity<TreatmentEvent>(b =>
        {
            b.ToTable("TREATMENT_EVENT");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentReceipt>(b =>
        {
            b.ToTable("TREATMENT_RECEIPT");
            b.HasKey(x => x.RequestKey);
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
