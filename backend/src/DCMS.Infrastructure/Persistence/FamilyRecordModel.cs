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
        model.Ignore<FamilyRecordAccessDetails>();
        model.Entity<FamilyRecordAccess>(b => {
            b.ToTable("FAMILY_RECORD_ACCESS"); b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40); b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Scopes).HasColumnType("text[]");
            b.Ignore(x => x.DeclaredFullName); b.Ignore(x => x.DeclaredBirthDate); b.Ignore(x => x.Relationship);
            b.Ignore(x => x.DeclaredPatientCode); b.Ignore(x => x.MatchingInformation); b.Ignore(x => x.SharingBasis);
            b.Ignore(x => x.VerificationNote); b.Ignore(x => x.DecisionReason); b.Ignore(x => x.PublicNote);
            b.Ignore(x => x.DecidedAt);
            b.ComplexProperty(x => x.Details, d => d.ToJson("Details"));
            b.HasIndex(x => x.FamilyMemberId).IsUnique().HasFilter("\"Status\" IN ('Pending','Verified')");
            b.HasIndex(x => new { x.OwnerAccountId, x.RequestedAt });
            b.HasOne<FamilyMember>().WithMany().HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerAccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SchedulingBranch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FamilyRecordAudit>(b => {

            b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(1000);
            b.HasIndex(x => new { x.AccessId, x.OccurredAt });
            b.HasOne<FamilyRecordAccess>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FamilyRecordReceipt>(b => {
            b.HasIndex(new[] { "ReceiptType", "ActorAccountId", "RequestKey" }, "UX_FamilyRecordReceipt_Scope").IsUnique().HasDatabaseName("UX_RECEIPT_ActorAccountId_RequestKey");
            b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("jsonb");
            b.HasOne<FamilyRecordAccess>().WithMany().HasForeignKey(x => x.AccessId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
