using DCMS.Domain.Features.SystemSettings;
using Microsoft.EntityFrameworkCore;
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
    }
}
