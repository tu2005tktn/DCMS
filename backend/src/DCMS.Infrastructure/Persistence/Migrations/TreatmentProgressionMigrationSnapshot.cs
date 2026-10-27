using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence.Migrations;

// Frozen model state after 20261019143000_TreatmentProgression.
internal static class TreatmentProgressionMigrationSnapshot
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TreatmentDossier>(b =>
        {
            b.Property(x => x.SourceTemplateId).HasColumnType("uuid");
            b.Property(x => x.SourceTemplateVersion).HasColumnType("integer");
            b.HasIndex(x => x.SourceTemplateId);
        });
        model.Entity<TreatmentTemplate>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BranchId, x.Name });
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.BranchId).HasColumnType("integer");
            b.Property(x => x.Name).IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.Version).HasColumnType("integer").IsConcurrencyToken();
            b.Property(x => x.CreatedBy).HasColumnType("integer");
            b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            b.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne("DCMS.Domain.Features.Scheduling.SchedulingBranch", null).WithMany()
                .HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentTemplateVersion>(b =>
        {
            b.ToTable("TREATMENT_TEMPLATE_VERSION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
            b.HasIndex(x => x.TemplateId).IsUnique().HasDatabaseName("IX_TREATMENT_TEMPLATE_VERSION_Active")
                .HasFilter("\"State\" = 'Active'");
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.TemplateId).HasColumnType("uuid");
            b.Property(x => x.Number).HasColumnType("integer");
            b.Property(x => x.State).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.StepsJson).IsRequired().HasColumnType("jsonb");
            b.Property(x => x.CreatedBy).HasColumnType("integer");
            b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.ActivatedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.RetiredAt).HasColumnType("timestamp with time zone");
        });
        model.Entity<TreatmentStep>(b =>
        {
            b.ToTable("TREATMENT_STEP"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlanVersion });
            b.HasIndex(x => x.ItemId);
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.PlanId).HasColumnType("uuid");
            b.Property(x => x.PlanVersion).HasColumnType("integer");
            b.Property(x => x.ItemId).HasColumnType("uuid");
            b.Property(x => x.Name).IsRequired().HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.Region).HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property(x => x.EstimatedMinutes).HasColumnType("integer");
            b.Property(x => x.ReservedMinutes).HasColumnType("integer");
            b.Property(x => x.Capability).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.LabRequirement).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.EarliestOffsetDays).HasColumnType("integer");
            b.Property(x => x.TargetOffsetDays).HasColumnType("integer");
            b.Property(x => x.LatestOffsetDays).HasColumnType("integer");
            b.Property(x => x.ChargingCoverage).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Outcome).IsRequired().HasMaxLength(16).HasColumnType("character varying(16)");
            b.Property(x => x.HoldReason).HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property(x => x.ReviewCondition).HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.Property(x => x.HoldEffectiveAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.CompletionEventId).HasColumnType("uuid");
            b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.PredecessorsJson).IsRequired().HasColumnType("jsonb");
            b.Property(x => x.Version).HasColumnType("integer").IsConcurrencyToken();
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("CK_STEP_DURATION", "\"EstimatedMinutes\" > 0 AND \"ReservedMinutes\" BETWEEN 15 AND 480 AND \"ReservedMinutes\" % 15 = 0 AND \"ReservedMinutes\" >= \"EstimatedMinutes\""));
        });
        model.Entity<FollowUpInstruction>(b =>
        {
            b.ToTable("FOLLOW_UP_INSTRUCTION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.PatientId, x.Status });
            b.HasIndex(x => new { x.StepId, x.SourceCompletionEventId }).IsUnique()
                .HasDatabaseName("IX_FOLLOW_UP_INSTRUCTION_Step_Completion")
                .HasFilter("\"StepId\" IS NOT NULL AND \"SourceCompletionEventId\" IS NOT NULL");
            b.HasIndex(x => x.ActiveAppointmentId).IsUnique().HasFilter("\"ActiveAppointmentId\" IS NOT NULL");
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.PatientId).HasColumnType("integer");
            b.Property(x => x.BranchId).HasColumnType("integer");
            b.Property(x => x.DossierId).HasColumnType("uuid");
            b.Property(x => x.ItemId).HasColumnType("uuid");
            b.Property(x => x.StepId).HasColumnType("uuid");
            b.Property(x => x.PlanVersion).HasColumnType("integer");
            b.Property(x => x.SourceVisitId).HasColumnType("uuid");
            b.Property(x => x.SourceActivityId).HasColumnType("uuid");
            b.Property(x => x.SourceCompletionEventId).HasColumnType("uuid");
            b.Property(x => x.Purpose).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Reason).IsRequired().HasMaxLength(2000).HasColumnType("character varying(2000)");
            b.Property(x => x.Guidance).HasMaxLength(4000).HasColumnType("character varying(4000)");
            b.Property(x => x.EarliestOn).HasColumnType("date");
            b.Property(x => x.TargetOn).HasColumnType("date");
            b.Property(x => x.LatestOn).HasColumnType("date");
            b.Property(x => x.DurationMinutes).HasColumnType("integer");
            b.Property(x => x.DentistId).HasColumnType("integer");
            b.Property(x => x.Capability).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.Status).IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property(x => x.ActiveAppointmentId).HasColumnType("uuid");
            b.Property(x => x.Version).HasColumnType("integer").IsConcurrencyToken();
            b.Property(x => x.CreatedBy).HasColumnType("integer");
            b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasOne("DCMS.Domain.Features.Patients.PatientProfile", null).WithMany()
                .HasForeignKey("PatientId").OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FOLLOW_UP_WINDOW", "\"EarliestOn\" <= \"TargetOn\" AND \"TargetOn\" <= \"LatestOn\"");
                t.HasCheckConstraint("CK_FOLLOW_UP_DURATION", "\"DurationMinutes\" BETWEEN 15 AND 480 AND \"DurationMinutes\" % 15 = 0");
            });
        });
        model.Entity<TreatmentProgressionReceipt>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_RECEIPT"); b.HasKey(x => x.RequestKey);
            b.Property(x => x.RequestKey).HasColumnType("uuid");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.Command).IsRequired().HasMaxLength(40).HasColumnType("character varying(40)");
            b.Property(x => x.Fingerprint).IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property(x => x.ResultJson).IsRequired().HasColumnType("jsonb");
        });
        model.Entity<TreatmentProgressionEvent>(b =>
        {
            b.ToTable("TREATMENT_PROGRESSION_EVENT"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.StableKey).IsUnique().HasFilter("\"StableKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AggregateId, x.OccurredAt });
            b.Property(x => x.Id).HasColumnType("uuid");
            b.Property(x => x.AggregateId).HasColumnType("uuid");
            b.Property(x => x.Kind).IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property(x => x.StableKey).HasMaxLength(160).HasColumnType("character varying(160)");
            b.Property(x => x.ActorId).HasColumnType("integer");
            b.Property(x => x.OccurredAt).HasColumnType("timestamp with time zone");
            b.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        });
    }
}
