using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

public static class SettingsModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SettingsLegacyArchive>();
        model.Entity<AccessGroup>(b => {
            b.ToTable("ACCESS_GROUP"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(40);
            b.Property(x => x.Name).HasMaxLength(160);
            b.Property(x => x.Description).HasMaxLength(1000); b.Property(x => x.BaseRole).HasMaxLength(40); b.Property(x => x.Version).IsConcurrencyToken();
        });

        model.Entity<SettingsReceipt>(b => {
            b.HasIndex(new[] { "ReceiptType", "ActorId", "RequestKey" }, "UX_SettingsReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorId_RequestKey"); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.ResultJson).HasColumnType("jsonb"); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SettingsAudit>(b => {
             b.Property(x => x.Section).HasMaxLength(40); b.Property(x => x.Target).HasMaxLength(100);
            b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb"); b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new {x.Section,x.At}); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
