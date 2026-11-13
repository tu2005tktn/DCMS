using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class ClinicalWorkModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ClinicalActivity>(b =>
        {

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
            b.Property(x => x.Version).IsConcurrencyToken(false);
        });
        model.Entity<ClinicalReview>(b =>
        {

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
            b.Property(x => x.Version).IsConcurrencyToken(false);
        });
        model.Entity<ClinicalCareNote>(b =>
        {

            b.HasIndex(x => new { x.VisitId, x.RecordedAt });
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.Response).HasMaxLength(4000);
        });
        model.Entity<ClinicalCommandReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "RequestKey" }, "UX_ClinicalCommandReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_RequestKey");
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
    }
}
