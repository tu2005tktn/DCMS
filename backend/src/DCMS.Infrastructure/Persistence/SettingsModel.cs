using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class SettingsModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SettingsVersion>(b => {
            b.ToTable("SETTINGS_VERSION"); b.HasKey(x => x.Version); b.Property(x => x.Version).ValueGeneratedNever();
            b.Property(x => x.ContentJson).HasColumnType("jsonb"); b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ReferenceEntry>(b => {
            b.ToTable("OPERATIONAL_REFERENCE_ENTRY"); b.HasKey(x => x.Id); b.Property(x => x.Kind).HasMaxLength(40);
            b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.NormalizedCode).HasMaxLength(40);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedName).HasMaxLength(160);
            b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new {x.Kind,x.NormalizedName}).IsUnique(); b.HasIndex(x => new {x.Kind,x.NormalizedCode}).IsUnique();
            b.HasIndex(x => new {x.Kind,x.Active});
        });
        model.Entity<AccessGroup>(b => {
            b.ToTable("ACCESS_GROUP"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(40); b.Property(x => x.NormalizedCode).HasMaxLength(40);
            b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.NormalizedName).HasMaxLength(160);
            b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.BaseRole).HasMaxLength(40); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.NormalizedCode).IsUnique(); b.HasIndex(x => x.NormalizedName).IsUnique();
            b.HasMany(x => x.Grants).WithOne().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<AccessGroupGrant>(b => { b.ToTable("ACCESS_GROUP_GRANT"); b.HasKey(x => new {x.GroupId,x.Permission}); b.Property(x => x.Permission).HasMaxLength(100); });
        model.Entity<StaffAccessAssignment>(b => {
            b.ToTable("STAFF_ACCESS_ASSIGNMENT"); b.HasKey(x => x.AccountId); b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<StaffAccessAssignment>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccessGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SettingsReceipt>(b => {
            b.ToTable("SETTINGS_RECEIPT"); b.HasKey(x => new {x.ActorId,x.RequestKey}); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb"); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SettingsAudit>(b => {
            b.ToTable("SETTINGS_AUDIT"); b.HasKey(x => x.Id); b.Property(x => x.Section).HasMaxLength(40); b.Property(x => x.Target).HasMaxLength(100);
            b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new {x.Section,x.At}); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
