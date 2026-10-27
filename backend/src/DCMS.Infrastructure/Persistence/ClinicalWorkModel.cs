using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class ClinicalWorkModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ClinicalActivity>(b =>
        {
            b.ToTable("CLINICAL_ACTIVITY"); b.HasKey(x => x.Id);
            b.Property(x => x.SourceKind).HasMaxLength(16).HasDefaultValue("Visit");
            b.Property(x => x.ServiceNameSnapshot).HasMaxLength(160);
            b.Property(x => x.LabWorkSnapshot).HasMaxLength(1000);
            b.HasIndex(x => new { x.ItemId, x.PlanVersion }).IsUnique().HasFilter("\"IsPlanTransfer\" = TRUE");
            b.HasIndex(x => new { x.VisitId, x.State });
            b.HasIndex(x => new { x.PatientId, x.DossierId, x.StepId });
            b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Quantity).HasPrecision(12, 3);
            b.Property(x => x.PriceSnapshotVnd).HasPrecision(18, 0);
            b.Property(x => x.Outcome).HasMaxLength(4000);
            b.Property(x => x.NextSteps).HasMaxLength(2000);
            b.Property(x => x.ChargingBasis).HasMaxLength(16);
            b.Property(x => x.ChargingBasisEvidence).HasMaxLength(1000);
            b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.FeeStatus).HasMaxLength(24).HasDefaultValue("Unavailable");
            b.Property(x => x.Version).IsConcurrencyToken();
            b.ToTable(t => t.HasCheckConstraint("CK_CLINICAL_ACTIVITY_VALUES",
                "\"Quantity\" > 0 AND \"PriceSnapshotVnd\" >= 0"));
        });
        model.Entity<ClinicalReview>(b =>
        {
            b.ToTable("CLINICAL_REVIEW"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.VisitId, x.RecordedAt });
            b.HasIndex(x => new { x.SourceActivityId, x.SourceItemId });
            b.Property(x => x.Complaint).HasMaxLength(4000);
            b.Property(x => x.Findings).HasMaxLength(4000);
            b.Property(x => x.Coverage).HasMaxLength(24);
            b.Property(x => x.Outcome).HasMaxLength(32);
            b.Property(x => x.CoveredComponent).HasMaxLength(500);
            b.Property(x => x.Exclusions).HasMaxLength(1000);
            b.Property(x => x.Policy).HasMaxLength(160);
            b.Property(x => x.PolicyVersion).HasMaxLength(40);
            b.Property(x => x.Basis).HasMaxLength(1000);
            b.Property(x => x.NextAction).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<ClinicalCareNote>(b =>
        {
            b.ToTable("CLINICAL_CARE_NOTE"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.VisitId, x.RecordedAt });
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.Response).HasMaxLength(4000);
        });
        model.Entity<ClinicalCommandReceipt>(b =>
        {
            b.ToTable("CLINICAL_COMMAND_RECEIPT"); b.HasKey(x => x.RequestKey);
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
        model.Entity<CareTask>(b =>
        {
            b.ToTable("CARE_TASK"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BranchId, x.DueAt, x.Status });
            b.HasIndex(x => x.EpisodeKey).IsUnique().HasFilter("\"EpisodeKey\" IS NOT NULL");
            b.Property(x => x.EpisodeKey).HasMaxLength(200);
            b.Property(x => x.Type).HasMaxLength(40);
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<CareContactAttempt>(b =>
        {
            b.ToTable("CARE_CONTACT_ATTEMPT"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TaskId, x.OccurredAt });
            b.Property(x => x.Channel).HasMaxLength(32);
            b.Property(x => x.Result).HasMaxLength(40);
            b.Property(x => x.NextAction).HasMaxLength(1000);
            b.HasOne<CareTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CareEscalation>(b =>
        {
            b.ToTable("CARE_ESCALATION"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TaskId, x.CreatedAt });
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.Resolution).HasMaxLength(4000);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<CareTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
