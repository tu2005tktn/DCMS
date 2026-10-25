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
            b.HasIndex(x => x.SourceTemplateId);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingMember>().WithMany().HasForeignKey(x => new { x.BranchId, EmployeeId = x.DentistId })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        ClinicalWorkModel.Configure(model);
        model.Entity<TreatmentItem>(b =>
        {
            b.ToTable("TREATMENT_PLAN_ITEM");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlannedOrder });
            b.Property(x => x.ServiceName).HasMaxLength(160);
            b.Property(x => x.Material).HasMaxLength(160);
            b.Property(x => x.DecisionPlanVersion).HasColumnType("integer");
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
        model.Entity<TreatmentRevisionDraft>(b =>
        {
            b.ToTable("TREATMENT_REVISION_DRAFT");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.ItemId, x.ActorId }).IsUnique();
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentTemplate>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BranchId, x.Name });
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentTemplateVersion>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE_VERSION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
            b.HasIndex(x => x.TemplateId).IsUnique().HasDatabaseName("IX_TREATMENT_TEMPLATE_VERSION_Active")
                .HasFilter("\"State\" = 'Active'");
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.StepsJson).HasColumnType("jsonb");
        });
        model.Entity<TreatmentStep>(b =>
        {
            b.ToTable("TREATMENT_STEP"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlanVersion });
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Capability).HasMaxLength(160);
            b.Property(x => x.LabRequirement).HasMaxLength(160);
            b.Property(x => x.ChargingCoverage).HasMaxLength(40);
            b.Property(x => x.Outcome).HasMaxLength(16);
            b.Property(x => x.HoldReason).HasMaxLength(1000);
            b.Property(x => x.ReviewCondition).HasMaxLength(1000);
            b.Property(x => x.PredecessorsJson).HasColumnType("jsonb");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_STEP_DURATION", "\"EstimatedMinutes\" > 0 AND \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\"");
                t.HasCheckConstraint("CK_STEP_ESTIMATE_RANGE", "(\"EstimatedMinMinutes\" IS NULL AND \"EstimatedMaxMinutes\" IS NULL) OR (\"EstimatedMinMinutes\" IS NOT NULL AND \"EstimatedMaxMinutes\" IS NOT NULL AND \"EstimatedMinMinutes\" > 0 AND \"EstimatedMinMinutes\" <= \"EstimatedMinutes\" AND \"EstimatedMinutes\" <= \"EstimatedMaxMinutes\" AND \"EstimatedMaxMinutes\" <= 480)");
            });
        });
        model.Entity<FollowUpInstruction>(b =>
        {
            b.ToTable("FOLLOW_UP_INSTRUCTION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PatientId, x.Status });
            b.HasIndex(x => x.StepId).IsUnique().HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_ActiveStep")
                .HasFilter("\"StepId\" IS NOT NULL AND \"Status\" IN ('NeedsBooking','Booked')");
            b.HasIndex(x => new { x.DossierId, x.ItemId }).IsUnique()
                .HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_ActiveItemOnly")
                .HasFilter("\"DossierId\" IS NOT NULL AND \"ItemId\" IS NOT NULL AND \"StepId\" IS NULL AND \"Status\" IN ('NeedsBooking','Booked')");
            b.HasIndex(x => new { x.StepId, x.SourceCompletionEventId }).IsUnique()
                .HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_Step_Completion")
                .HasFilter("\"StepId\" IS NOT NULL AND \"SourceCompletionEventId\" IS NOT NULL");
            b.HasIndex(x => x.ActiveAppointmentId).IsUnique().HasFilter("\"ActiveAppointmentId\" IS NOT NULL");
            b.Property(x => x.Purpose).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.Guidance).HasMaxLength(4000);
            b.Property(x => x.Capability).HasMaxLength(160);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FOLLOW_UP_WINDOW", "\"EarliestOn\" <= \"TargetOn\" AND \"TargetOn\" <= \"LatestOn\"");
                t.HasCheckConstraint("CK_FOLLOW_UP_DURATION", "\"DurationMinutes\" BETWEEN 15 AND 480 AND \"DurationMinutes\" % 15 = 0");
            });
        });
        model.Entity<TreatmentProgressionReceipt>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_RECEIPT"); b.HasKey(x => x.RequestKey);
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
        model.Entity<TreatmentProgressionEvent>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_EVENT"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.StableKey).IsUnique().HasFilter("\"StableKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AggregateId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(80);
            b.Property(x => x.StableKey).HasMaxLength(160);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
    }
}
