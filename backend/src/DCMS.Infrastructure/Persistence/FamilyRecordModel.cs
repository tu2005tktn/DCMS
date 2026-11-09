using DCMS.Domain.Features.FamilyMembers;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class FamilyRecordModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FamilyRecordAccess>(b => {
            b.ToTable("FAMILY_RECORD_ACCESS"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.DeclaredFullName).HasMaxLength(100);
            b.Property(x => x.Relationship).HasMaxLength(20); b.Property(x => x.DeclaredPatientCode).HasMaxLength(40);
            b.Property(x => x.MatchingInformation).HasMaxLength(1000); b.Property(x => x.VerificationNote).HasMaxLength(1000);
            b.Property(x => x.SharingBasis).HasMaxLength(32); b.Property(x => x.DecisionReason).HasMaxLength(500);
            b.Property(x => x.PublicNote).HasMaxLength(500); b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Scopes).HasColumnType("text[]");
            b.HasIndex(x => x.FamilyMemberId).IsUnique().HasFilter("\"Status\" IN ('Pending','Verified')");
            b.HasIndex(x => new { x.OwnerAccountId, x.RequestedAt });
            b.HasOne<FamilyMember>().WithMany().HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FamilyRecordAudit>(b => {
            b.ToTable("FAMILY_RECORD_AUDIT"); b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.AccessId, x.OccurredAt });
            b.HasOne<FamilyRecordAccess>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FamilyRecordReceipt>(b => {
            b.ToTable("FAMILY_RECORD_RECEIPT"); b.HasKey(x => new { x.ActorAccountId, x.RequestKey });
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<FamilyRecordAccess>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
