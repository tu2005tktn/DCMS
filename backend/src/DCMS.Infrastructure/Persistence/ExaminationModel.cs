using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.Visits;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class ExaminationModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<VisitExamination>(b => {
            b.ToTable("VISIT_EXAMINATION"); b.HasKey(x => x.VisitId);
            b.Property(x => x.ContentJson).HasColumnType("jsonb");
            b.HasOne<Visit>().WithOne().HasForeignKey<VisitExamination>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExaminationReceipt>(b => {
            b.ToTable("EXAMINATION_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<InvestigationOrder>(b => {
            b.ToTable("INVESTIGATION_ORDER", t => t.HasCheckConstraint("CK_INVESTIGATION_ORDER_STATE", "\"State\" IN ('Ordered','ResultAvailable','Reviewed','Cancelled')"));
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Region).HasMaxLength(200); b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.State).HasMaxLength(24); b.Property(x => x.ResultText).HasMaxLength(4000);
            b.Property(x => x.Interpretation).HasMaxLength(4000); b.Property(x => x.CancellationReason).HasMaxLength(1000);
            b.Property(x => x.ResultDocumentIdsJson).HasColumnType("jsonb"); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.VisitId, x.State }); b.HasIndex(x => new { x.CreatedBy, x.CreateRequestKey }).IsUnique();
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResultBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ClinicalDocument>(b => {
            b.ToTable("CLINICAL_DOCUMENT", t => t.HasCheckConstraint("CK_CLINICAL_DOCUMENT_State", "\"FileState\" IN ('PendingScan','Ready','Rejected')"));
            b.Property(x => x.FileName).HasMaxLength(255); b.Property(x => x.ContentType).HasMaxLength(64);
            b.Property(x => x.Sha256).HasMaxLength(64); b.Property(x => x.DocumentType).HasMaxLength(24);
            b.Property(x => x.Source).HasMaxLength(16); b.Property(x => x.FileState).HasMaxLength(16);
            b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.RejectReason).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => new { x.PatientId, x.VisitId });
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<InvestigationOrder>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploaderId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentReview>(b => {
            b.ToTable("DOCUMENT_REVIEW"); b.HasKey(x => new { x.DocumentId, x.VisitId });
            b.HasOne<ClinicalDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentAudit>(b => {
            b.ToTable("DOCUMENT_AUDIT"); b.Property(x => x.Action).HasMaxLength(32); b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => new { x.DocumentId, x.OccurredAt });
            b.HasOne<ClinicalDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentReceipt>(b => {
            b.ToTable("DOCUMENT_RECEIPT"); b.HasKey(x => new { x.ActorId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
    }
}
