using DCMS.Domain.Features.FamilyMembers;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;
namespace DCMS.Infrastructure.Persistence;
internal static class FamilyMemberModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FamilyMember>(b => {
            b.ToTable("CUSTOMER_FAMILY_MEMBER"); b.HasKey(x => x.Id);
            b.Property(x => x.FullName).HasMaxLength(100); b.Property(x => x.Relationship).HasMaxLength(20);
            b.Property(x => x.Gender).HasMaxLength(16); b.Property(x => x.ContactPhone).HasMaxLength(10);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.OwnerAccountId, x.DeletedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FamilyMemberReceipt>(b => {
            b.ToTable("CUSTOMER_FAMILY_RECEIPT"); b.HasKey(x => new { x.OwnerAccountId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
