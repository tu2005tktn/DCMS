using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class TreatmentModel
{
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConv<T>() where T : class =>
        new(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? (T)(object)(typeof(T).IsArray ? Array.CreateInstance(typeof(T).GetElementType()!, 0) : Activator.CreateInstance(typeof(T))!));
    private static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<T> JsonCompare<T>() where T : class =>
        new((a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null), (System.Text.Json.JsonSerializerOptions?)null)!);

    public static void Configure(ModelBuilder model)
    {
        model.Entity<TreatmentDossier>(b =>
        {
            b.ToTable("TREATMENT_DOSSIER");
            b.HasOne<DCMS.Domain.Features.Visits.Visit>().WithMany().HasForeignKey(x => x.SourceExaminationVisitId).OnDelete(DeleteBehavior.Restrict);
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.BranchId, x.UpdatedAt });
            b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Diagnosis).HasMaxLength(4000);
            b.Property(x => x.Note).HasMaxLength(4000);
            b.Property(x => x.State).HasMaxLength(16);
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
            b.ToTable("TREATMENT_PLAN_ITEM", t =>
                t.HasCheckConstraint("CK_TREATMENT_ITEM_VALUES", "\"Quantity\" > 0 AND \"AgreedUnitPriceVnd\" >= 0"));
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.DossierId, x.PlannedOrder });
            b.Property(x => x.ServiceName).HasMaxLength(160);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Region).HasMaxLength(80);
            b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Quantity).HasPrecision(12, 3);
            b.Property(x => x.DiscountType).HasMaxLength(16);
            b.Property(x => x.DiscountReason).HasMaxLength(1000);
            b.Property(x => x.DiscountValue).HasPrecision(18, 3);
        });
        model.Entity<TreatmentEvent>(b =>
        {
            
            b.HasIndex(x => new { x.DossierId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TreatmentReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "RequestKey" }, "UX_TreatmentReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_RequestKey");
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<TreatmentDossier>().WithMany().HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Restrict);
        });


        model.Entity<TreatmentTemplate>(b =>
        {
            b.Property(x => x.DefinitionKind).HasMaxLength(24).HasDefaultValue("LegacySteps");
            b.ToTable("TREATMENT_TEMPLATE"); b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.BranchId, x.Name });
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });




        model.Entity<LabOrder>(b =>
        {
            b.ToTable("LAB_ORDER"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.WorkName).HasMaxLength(160);
            b.Property(x => x.MarginOuter).HasMaxLength(20); b.Property(x => x.MarginInner).HasMaxLength(20); b.Property(x => x.MarginSide).HasMaxLength(20);
            b.Property(x => x.BridgeShapes).HasColumnType("jsonb").HasConversion(JsonConv<string[]>(), JsonCompare<string[]>());
            b.Property(x => x.Teeth).HasColumnType("jsonb").HasConversion(JsonConv<string[]>(), JsonCompare<string[]>());
            b.Property(x => x.Shade).HasColumnType("jsonb").HasConversion(JsonConv<Dictionary<string, string>>(), JsonCompare<Dictionary<string, string>>());
            b.Property(x => x.Note).HasMaxLength(1000); b.Property(x => x.Material).HasMaxLength(100); b.Property(x => x.Warranty).HasMaxLength(100);
            b.HasIndex(x => x.ActivityId).IsUnique(); b.HasIndex(x => x.DossierId);
            b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
            b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(LabOrderStatus.NotSent);
            b.HasIndex(x => new { x.BranchId, x.Status, x.DueOn });
            b.HasOne<ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LAB_ORDER_STATUS", "\"Status\" IN ('NotSent','Sent','Received','Delivered','Rework')");
                t.HasCheckConstraint("CK_LAB_ORDER_FEES", "\"TotalFeeVnd\" >= 0 AND \"PaidVnd\" >= 0 AND \"PaidVnd\" <= \"TotalFeeVnd\"");
                t.HasCheckConstraint("CK_LAB_ORDER_QUANTITY", "\"Quantity\" BETWEEN 1 AND 999");
            });
        });
        model.Entity<TreatmentProgressionReceipt>(b =>
        {
            b.HasIndex(new[] { "ReceiptType", "RequestKey" }, "UX_TreatmentProgressionReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_RequestKey");
            b.Property(x => x.Command).HasMaxLength(40);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
        model.Entity<TreatmentProgressionEvent>(b =>
        { 
            b.HasIndex(x => x.StableKey).IsUnique().HasFilter("\"AuditType\" = 'TreatmentProgressionEvent' AND \"StableKey\" IS NOT NULL");
            b.HasIndex(x => new { x.AggregateId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(80);
            b.Property(x => x.StableKey).HasMaxLength(160);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
    }
}
