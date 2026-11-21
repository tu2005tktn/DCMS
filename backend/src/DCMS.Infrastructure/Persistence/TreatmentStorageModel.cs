using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class TreatmentStorageModel
{
    internal static void Configure(ModelBuilder model)
    {
        foreach (var type in new[] { typeof(TreatmentSheetDraft),
            typeof(TreatmentTemplateItem),
            typeof(CareContactAttempt), typeof(LabOrderStatusLog) }) model.Ignore(type);

        model.Entity<TreatmentDossier>(b =>
        {
            b.ComplexCollection(x => x.SheetDrafts).ToJson();
        });
        model.Entity<TreatmentTemplate>(b =>
        {
            b.ComplexCollection(x => x.Items).ToJson();
        });
        model.Entity<CareTask>(b =>
        {
            b.ComplexCollection(x => x.ContactAttempts).ToJson();
        });
        model.Entity<LabOrder>(b =>
        {
            b.ComplexCollection(x => x.StatusLogs).ToJson();
        });
        model.Entity<ClinicalActivity>(b =>
        {
            b.ToTable("CLINICAL_ACTIVITY", t =>
                t.HasCheckConstraint("CK_CLINICAL_ACTIVITY_VALUES", "\"Quantity\" > 0 AND \"PriceSnapshotVnd\" >= 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            // Version is the only concurrency token; every saved change bumps it (TreatmentStorage).
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<TreatmentEvent>().HasIndex(new[] { "AuditType", "Id" }, "UX_TreatmentEvent_LegacyId")
            .IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<TreatmentProgressionEvent>().HasIndex(new[] { "AuditType", "Id" }, "UX_TreatmentProgressionEvent_LegacyId")
            .IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
    }
}
