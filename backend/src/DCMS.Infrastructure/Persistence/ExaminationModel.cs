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
            b.ToTable("VISIT"); b.HasKey(x => x.VisitId);
            b.Property(x => x.VisitId).HasColumnName("Id");
            b.Property(x => x.ContentJson).HasColumnName("ExaminationContentJson").HasColumnType("jsonb");
            b.Property(x => x.UpdatedBy).HasColumnName("UpdatedBy");
            b.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
            b.Property<int>("Version").IsConcurrencyToken();
            b.HasOne<Visit>().WithOne().HasForeignKey<VisitExamination>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ExaminationReceipt>(b => {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_ExaminationReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Ignore<InvestigationOrder>();
        model.Ignore<DocumentReview>();
        model.Ignore<ClinicalDocumentMetadata>();
        model.Entity<ClinicalDocument>(b => {
            b.ToTable("CLINICAL_DOCUMENT", t => {
                t.HasCheckConstraint("CK_CLINICAL_DOCUMENT_State", "\"FileState\" IN ('PendingScan','Ready','Rejected')");
                t.HasCheckConstraint("CK_CLINICAL_DOCUMENT_Context", "\"VisitId\" IS NOT NULL OR \"DossierId\" IS NOT NULL");
            });
            b.Property(x => x.FileName).HasMaxLength(255); b.Property(x => x.ContentType).HasMaxLength(64);
            b.Property(x => x.DocumentType).HasMaxLength(24); b.Property(x => x.FileState).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => new { x.PatientId, x.VisitId });
            b.Ignore(x => x.Source); b.Ignore(x => x.Description); b.Ignore(x => x.SizeBytes);
            b.Ignore(x => x.Sha256); b.Ignore(x => x.RejectReason); b.Ignore(x => x.InvestigationId);
            b.ComplexProperty(x => x.Metadata, m => m.ToJson("Metadata"));
            b.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
            b.ComplexCollection(x => x.Reviews).ToJson();
            b.ComplexCollection(x => x.AiAnalyses, a => { a.ToJson(); a.ComplexCollection(x => x.Findings); });
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploaderId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ClinicalSourceArchive>();
        model.Entity<DocumentAudit>(b => {
            b.Property(x => x.Action).HasMaxLength(32); b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => new { x.DocumentId, x.OccurredAt });
            b.HasOne<ClinicalDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentReceipt>(b => {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_DocumentReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
    }
}
