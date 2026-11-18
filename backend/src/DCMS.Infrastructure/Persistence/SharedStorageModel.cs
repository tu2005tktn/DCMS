using DCMS.Domain.Common;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Examinations;
using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class SharedStorageModel
{
    internal static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConv<T>() where T : class, new() =>
        new(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());

    internal static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<T> JsonCompare<T>() where T : class, new() =>
        new((a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) == System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null).GetHashCode(),
            v => System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null), (System.Text.Json.JsonSerializerOptions?)null)!);

    public static void Configure(ModelBuilder model)
    {
        model.HasSequence<long>("audit_log_number_seq");
        model.Ignore<WorkingPeriod>();
        model.Ignore<AccessGroupGrant>();

        model.Entity<CommandReceiptRecord>(b =>
        {
            b.ToTable("COMMAND_RECEIPT");
            b.HasKey(x => x.StorageId);
            b.Property(x => x.Fingerprint).HasMaxLength(64).HasDefaultValue("");
            b.Property(x => x.ResultJson).HasColumnType("jsonb").HasDefaultValue("{}");
            b.HasDiscriminator<string>("ReceiptType");
        });
        model.Entity<AuditRecord>(b =>
        {
            b.ToTable("AUDIT_LOG");
            b.HasKey(x => x.StorageId);
            b.HasDiscriminator<string>("AuditType");
        });
    }

    public static void ShareColumns(ModelBuilder model)
    {
        model.Entity<AccessGroup>().Property(x => x.Grants)
            .HasColumnName("Grants")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(JsonConv<List<AccessGroupGrant>>(), JsonCompare<List<AccessGroupGrant>>());

        model.Entity<AppointmentChange>().HasIndex(new[] { "AuditType", "Id" }, "UX_AppointmentChange_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<AppointmentSupportHistory>().HasIndex(new[] { "AuditType", "Id" }, "UX_AppointmentSupportHistory_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<AccountAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_AccountAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<RecoveryAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_RecoveryAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<DocumentAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_DocumentAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<FamilyRecordAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_FamilyRecordAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<PatientLinkAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_PatientLinkAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<PatientAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_PatientAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Number");
        model.Entity<PrescriptionAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_PrescriptionAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<SettingsAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_SettingsAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");
        model.Entity<VisitAudit>().HasIndex(new[] { "AuditType", "Id" }, "UX_VisitAudit_LegacyId").IsUnique().HasDatabaseName("UX_AUDIT_LOG_Type_Id");

        foreach (var root in new[] { typeof(CommandReceiptRecord), typeof(AuditRecord) })
        {
            var properties = model.Model.GetEntityTypes().Where(type => type.BaseType?.ClrType == root)
                .SelectMany(type => type.GetDeclaredProperties())
                .GroupBy(property => new { property.Name, Type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType });
            foreach (var group in properties)
            {
                var column = group.Key.Name;
                if (root == typeof(AuditRecord) && column == "Id")
                    column = group.Key.Type == typeof(long) ? "LegacyNumber" : "LegacyId";
                var maxLength = group.Max(property => property.GetMaxLength());
                // AUDIT_LOG.Action keeps the 64-character width it had while finance history shared the column.
                if (root == typeof(AuditRecord) && column == "Action") maxLength = Math.Max(maxLength ?? 0, 64);
                foreach (var property in group)
                {
                    var builder = model.Entity(property.DeclaringType.ClrType).Property(property.Name).HasColumnName(column);
                    if (maxLength.HasValue) builder.HasMaxLength(maxLength.Value);
                    if (property.Name.EndsWith("Json")) builder.HasColumnType("jsonb");
                    if (root == typeof(AuditRecord) && group.Key.Name == "Id" && group.Key.Type == typeof(long))
                        builder.HasDefaultValueSql("nextval('audit_log_number_seq')").ValueGeneratedOnAdd();
                }
            }
        }

        // System jobs record treatment events with actor 0, so these stay outside the account foreign key on ActorId.
        model.Entity<TreatmentEvent>().Property(x => x.ActorId).HasColumnName("EventActorId");
        model.Entity<TreatmentProgressionEvent>().Property(x => x.ActorId).HasColumnName("EventActorId");
    }
}
