using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence.Migrations;

internal static class TreatmentMigrationSnapshot
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TreatmentDossier>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.PatientId).HasColumnType("integer");
            b.Property(x => x.BranchId).HasColumnType("integer");
            b.Property(x => x.DentistId).HasColumnType("integer");
            b.Property(x => x.StartedOn).HasColumnType("date");
            b.Property(x => x.ClosedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.ClosedBy).HasColumnType("integer");
            b.Property(x => x.Version).HasColumnType("integer");
            b.Property(x => x.PlanId).HasColumnType("uuid");
            b.Property(x => x.PlanVersion).HasColumnType("integer");
            b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.UpdatedAt });
            b.HasIndex(x => new { x.BranchId, x.DentistId });
            b.Property(x => x.Code).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Diagnosis).IsRequired().HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property(x => x.Note).HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property(x => x.State).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.ClosureOutcome).HasMaxLength(32).HasColumnType("character varying(32)");
            b.Property(x => x.ClosureReason).HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property(x => x.PlanState).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.Goals).HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property(x => x.PaymentTermsJson).IsRequired().HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne("DCMS.Domain.Features.Patients.PatientProfile", null).WithMany().HasForeignKey("PatientId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingBranch", null).WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingMember", null).WithMany()
                .HasForeignKey("BranchId", "DentistId")
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentItem>(b =>
        {
            b.ToTable("TREATMENT_PLAN_ITEM");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.SourceDiagnosisId).HasColumnType("integer");
            b.Property(x => x.ServiceId).HasColumnType("integer");
            b.Property(x => x.EstimatedMinutes).HasColumnType("integer");
            b.Property(x => x.EstimatedStart).HasColumnType("date");
            b.Property(x => x.EstimatedEnd).HasColumnType("date");
            b.Property(x => x.AgreedUnitPriceVnd).HasColumnType("bigint");
            b.Property(x => x.PlannedOrder).HasColumnType("integer");
            b.Property(x => x.DecidedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.DecidedBy).HasColumnType("integer");
            b.Property(x => x.ConsentDocumentId).HasColumnType("uuid");
            b.Property(x => x.ConsentPlanVersion).HasColumnType("integer");
            b.Property(x => x.WarrantyConfirmedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.WarrantyEndsOn).HasColumnType("date");
            b.Property(x => x.WarrantyConfirmedBy).HasColumnType("integer");
            b.HasIndex(x => new { x.DossierId, x.PlannedOrder });
            b.Property(x => x.ServiceName).IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.Region).HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property(x => x.Unit).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Quantity).HasPrecision(12, 3).HasColumnType("numeric(12,3)");
            b.Property(x => x.Decision).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.Progress).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.Counseling).HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property(x => x.DecisionMaker).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.RepresentativeBasis).HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property(x => x.WarrantyPolicy).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.WarrantyPolicyVersion).HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.WarrantyScope).HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.ToTable(t => t.HasCheckConstraint("CK_TREATMENT_ITEM_VALUES", "\"Quantity\" > 0 AND \"AgreedUnitPriceVnd\" >= 0"));
        });
        model.Entity<TreatmentEvent>(b =>
        {
            b.ToTable("TREATMENT_EVENT");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.DossierVersion).HasColumnType("integer");
            b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
            b.HasIndex(x => new { x.DossierId, x.OccurredAt });
            b.Property(x => x.Kind).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentReceipt>(b =>
        {
            b.ToTable("TREATMENT_RECEIPT");
            b.HasKey(x => x.RequestKey);
            b.HasIndex(x => x.DossierId);
            b.Property(x => x.RequestKey).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.Command).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property(x => x.ResultJson).IsRequired().HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

