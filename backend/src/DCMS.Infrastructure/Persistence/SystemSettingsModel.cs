using System.Text.Json;
using DCMS.Domain.Features.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DCMS.Infrastructure.Persistence;
internal static class SystemSettingsModel
{
    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> JsonConv<T>() where T : class =>
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null) ?? Activator.CreateInstance<T>());
    private static Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<T> JsonCompare<T>() where T : class =>
        new((a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<MaterialCategory>(b => {
            b.ToTable("MATERIAL_CATEGORY"); b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<MaterialCatalogItem>(b => {
            b.ToTable("MATERIAL_CATALOG_ITEM"); b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.UsageUnit).HasMaxLength(40);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaterialPurchase>(b => {
            b.ToTable("MATERIAL_PURCHASE"); b.HasKey(x => x.Id);
            b.Property(x => x.Quantity).HasPrecision(12, 3); b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.ConversionFactor).HasPrecision(12, 3); b.Property(x => x.UsageQuantity).HasPrecision(18, 3);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.LotNumber).HasMaxLength(60);
            b.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.EnteredOn, x.Id }).HasFilter("NOT \"IsDeleted\"");
        });
        model.Entity<MaterialUsage>(b => {
            b.ToTable("MATERIAL_USAGE"); b.HasKey(x => x.Id);
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.HasOne<DCMS.Domain.Features.Treatment.ClinicalActivity>().WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<MaterialCatalogItem>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.MaterialId); b.HasIndex(x => x.ActivityId); b.HasIndex(x => x.UsedAt);
        });
        model.Entity<SystemCatalogChange>(b => {
            b.Property(x => x.Command).HasMaxLength(48); b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.HasIndex(new[] { "ReceiptType", "RequestKey" }, "UX_SystemCatalogChange_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_RequestKey");
            b.HasIndex(x => new { x.ResourceId, x.OccurredAt });
        });
        model.Entity<ClinicSettingsVersion>(b => {
            b.ToTable("CLINIC_SETTINGS_VERSION"); b.HasKey(x => x.Version); b.Property(x => x.Version).ValueGeneratedNever();
            b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.ContentJson).HasColumnType("jsonb");
        });
        model.Entity<ReferenceEntry>(b => {
            b.ToTable("REFERENCE_ENTRY"); b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.Kind, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        });
        // S58 đợt 4: shared identity columns for typed catalogs (simplified to 9 columns each).
        static void Supply<T>(EntityTypeBuilder<T> b, string table, int nameMax) where T : SupplyEntity
        {
            b.ToTable(table); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(nameMax);
            b.Property(x => x.Version).HasDefaultValue(1).ValueGeneratedNever().IsConcurrencyToken();
            b.HasIndex(x => x.Code).IsUnique();
        }
        model.Entity<CatalogProvider>(b => {
            Supply(b, "CATALOG_PROVIDER", 160);
            b.Property(x => x.Type).HasMaxLength(16); b.Property(x => x.Address).HasMaxLength(1000);
            b.Property(x => x.Phone).HasMaxLength(20); b.Property(x => x.Email).HasMaxLength(254);
        });
        model.Entity<LabWorkType>(b => {
            Supply(b, "LAB_WORK_TYPE", 160);
            b.Property(x => x.Unit).HasMaxLength(16); b.Property(x => x.Warranty).HasMaxLength(1000);
            b.HasOne<CatalogProvider>().WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Ignore<CatalogMedicineSpec>();
        model.Entity<CatalogMedicine>(b => {
            b.ToTable("CATALOG_MEDICINE"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.BaseUnit).HasMaxLength(40);
            b.Property(x => x.Version).HasDefaultValue(1).ValueGeneratedNever().IsConcurrencyToken();
            b.Property(x => x.Spec).HasColumnType("jsonb").HasConversion(JsonConv<CatalogMedicineSpec>(), JsonCompare<CatalogMedicineSpec>());
            b.HasIndex(x => x.Code).IsUnique();
        });
    }
}
