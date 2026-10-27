using DCMS.Domain.Features.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DCMS.Infrastructure.Persistence;
internal static class SystemSettingsModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<MaterialCategory>(b => {
            b.ToTable("MATERIAL_CATEGORY"); b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedName).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.NormalizedName).IsUnique().HasFilter("NOT \"IsDeleted\"");
        });
        model.Entity<MaterialCatalogItem>(b => {
            b.ToTable("MATERIAL_CATALOG_ITEM"); b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedName).HasMaxLength(160);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CategoryId, x.NormalizedName }).IsUnique().HasFilter("NOT \"IsDeleted\"");
        });
        model.Entity<SystemCatalogChange>(b => {
            b.ToTable("SYSTEM_CATALOG_CHANGE"); b.HasKey(x => x.RequestKey); b.Property(x => x.RequestKey).ValueGeneratedNever();
            b.Property(x => x.Command).HasMaxLength(48); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.ResourceId, x.OccurredAt });
        });
        model.Entity<ClinicSettingsVersion>(b => {
            b.ToTable("CLINIC_SETTINGS_VERSION"); b.HasKey(x => x.Version); b.Property(x => x.Version).ValueGeneratedNever();
            b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.ContentJson).HasColumnType("jsonb");
        });
        model.Entity<ReferenceEntry>(b => {
            b.ToTable("REFERENCE_ENTRY"); b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedName).HasMaxLength(160); b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.Kind, x.NormalizedName }).IsUnique();
            b.HasIndex(x => new { x.Kind, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
        });
        // S58 đợt 4: shared identity columns for typed catalogs.
        static void Supply<T>(EntityTypeBuilder<T> b, string table, int nameMax) where T : SupplyEntity
        {
            b.ToTable(table); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.Name).HasMaxLength(nameMax); b.Property(x => x.NormalizedName).HasMaxLength(nameMax);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.NormalizedName).IsUnique();
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
        model.Entity<CatalogMedicine>(b => {
            Supply(b, "CATALOG_MEDICINE", 200);
            b.Property(x => x.ActiveIngredient).HasMaxLength(200); b.Property(x => x.Strength).HasMaxLength(200); b.Property(x => x.DosageForm).HasMaxLength(200);
            b.Property(x => x.BaseUnit).HasMaxLength(40); b.Property(x => x.Packaging).HasMaxLength(160); b.Property(x => x.Instructions).HasMaxLength(1000);
        });
    }
}
