using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class TreatmentStorageModel
{
    internal static void Configure(ModelBuilder model)
    {
        foreach (var type in new[] { typeof(TreatmentStep), typeof(TreatmentSheetDraft), typeof(TreatmentRevisionDraft),
            typeof(TreatmentTemplateItem), typeof(TreatmentTemplateVersion),
            typeof(CareContactAttempt), typeof(CareEscalation), typeof(LabOrderStatusLog) }) model.Ignore(type);

        model.Entity<TreatmentDossier>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.SheetDrafts).ToJson();
            b.ComplexCollection(x => x.RevisionDrafts).ToJson();
        });
        model.Entity<TreatmentItem>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.Steps).ToJson();
        });
        model.Entity<TreatmentTemplate>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.Items).ToJson();
            b.ComplexCollection(x => x.Versions).ToJson();
        });
        model.Entity<CareTask>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.ContactAttempts).ToJson();
            b.ComplexCollection(x => x.Escalations).ToJson();
        });
        model.Entity<LabOrder>(b =>
        {
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.ComplexCollection(x => x.StatusLogs).ToJson();
        });
        model.Entity<TreatmentProcessRecord>(b =>
        {
            b.ToTable("CLINICAL_ACTIVITY", t =>
            {
                t.HasCheckConstraint("CK_CLINICAL_ACTIVITY_VALUES", "\"ProcessKind\" <> 'ClinicalActivity' OR (\"Quantity\" > 0 AND \"PriceSnapshotVnd\" >= 0)");
                t.HasCheckConstraint("CK_FOLLOW_UP_WINDOW", "\"ProcessKind\" <> 'FollowUpInstruction' OR (\"EarliestOn\" <= \"TargetOn\" AND \"TargetOn\" <= \"LatestOn\")");
                t.HasCheckConstraint("CK_FOLLOW_UP_DURATION", "\"ProcessKind\" <> 'FollowUpInstruction' OR \"DurationMinutes\" BETWEEN 15 AND 480 AND \"DurationMinutes\" % 15 = 0");
            });
            b.HasKey(x => x.StorageId);
            b.Property(x => x.StorageId).HasColumnName("Id").ValueGeneratedNever();
            b.Property(x => x.Id).HasColumnName("RecordId").ValueGeneratedNever();
            b.Property(x => x.Id).Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Throw);
            b.Property(x => x.StorageStamp).IsConcurrencyToken();
            b.HasDiscriminator<string>("ProcessKind");
            b.HasIndex("ProcessKind", nameof(TreatmentProcessRecord.Id)).IsUnique();
        });
        model.Entity<TreatmentEvent>().HasIndex(new[] { "AuditType", "Id" }, "UX_TreatmentEvent_LegacyId")
            .IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<TreatmentProgressionEvent>().HasIndex(new[] { "AuditType", "Id" }, "UX_TreatmentProgressionEvent_LegacyId")
            .IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
    }
}
