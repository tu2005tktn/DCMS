using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;

namespace DCMS.Infrastructure.Persistence;

public sealed class DcmsDbContext(DbContextOptions<DcmsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<EmployeeProfile> Employees => Set<EmployeeProfile>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AccountAudit> AccountAudits => Set<AccountAudit>();
    public DbSet<PatientProfile> Patients => Set<PatientProfile>();
    public DbSet<PatientAccountLink> PatientAccountLinks => Set<PatientAccountLink>();
    public DbSet<PatientLinkAudit> PatientLinkAudits => Set<PatientLinkAudit>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<ApplicationUser>(b =>
        {
            b.ToTable("USER_ACCOUNT");
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.AccountType).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<IdentityRole<int>>().ToTable("ROLE");
        model.Entity<IdentityUserRole<int>>().ToTable("USER_ACCOUNT_ROLE");
        model.Entity<EmployeeProfile>(b =>
        {
            b.ToTable("EMPLOYEE_PROFILE");
            b.HasIndex(x => x.AccountId).IsUnique();
            b.HasIndex(x => x.EmployeeCode).IsUnique();
            b.Property(x => x.EmployeeCode).HasMaxLength(40);
            b.Property(x => x.FullName).HasMaxLength(160);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<EmployeeProfile>(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuthSession>(b =>
        {
            b.ToTable("AUTH_SESSION");
            b.HasIndex(x => new { x.AccountId, x.RevokedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AccountAudit>(b =>
        {
            b.ToTable("ACCOUNT_AUDIT");
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientProfile>(b =>
        {
            b.ToTable("PATIENT_PROFILE");
            b.HasIndex(x => x.PatientCode).IsUnique();
            b.Property(x => x.PatientCode).HasMaxLength(40);
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.Gender).HasMaxLength(16);
            b.Property(x => x.Phone).HasMaxLength(24);
            b.Property(x => x.Version).IsConcurrencyToken();
        });
        model.Entity<PatientAccountLink>(b =>
        {
            b.ToTable("PATIENT_ACCOUNT_LINK", t => t.HasCheckConstraint("CK_PATIENT_ACCOUNT_LINK_Status",
                "\"Status\" IN ('PENDING','VERIFIED','REJECTED','REVOKED','WITHDRAWN') AND (\"Status\" <> 'VERIFIED' OR \"PatientId\" IS NOT NULL)"));
            b.Property(x => x.RequestCode).HasMaxLength(32);
            b.Property(x => x.Status).HasMaxLength(16);
            b.Property(x => x.DeclaredFullName).HasMaxLength(160);
            b.Property(x => x.DeclaredPatientCode).HasMaxLength(40);
            b.Property(x => x.RequestNote).HasMaxLength(1000);
            b.Property(x => x.VerificationNote).HasMaxLength(1000);
            b.Property(x => x.DecisionReason).HasMaxLength(500);
            b.Property(x => x.PublicNote).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.RequestCode).IsUnique();
            b.HasIndex(x => new { x.Status, x.RequestedAt });
            // One-to-one active self link and no duplicate request being processed (GBR-072).
            // Named indexes: EF would otherwise merge the two filtered indexes on AccountId into one.
            b.HasIndex(x => x.AccountId, "UX_PATIENT_ACCOUNT_LINK_Account_Pending").IsUnique().HasFilter("\"Status\" = 'PENDING'");
            b.HasIndex(x => x.AccountId, "UX_PATIENT_ACCOUNT_LINK_Account_Verified").IsUnique().HasFilter("\"Status\" = 'VERIFIED'");
            b.HasIndex(x => x.PatientId, "UX_PATIENT_ACCOUNT_LINK_Patient_Verified").IsUnique().HasFilter("\"Status\" = 'VERIFIED'");
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.ReviewingEmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EmployeeProfile>().WithMany().HasForeignKey(x => x.RevokingEmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PatientLinkAudit>(b =>
        {
            b.ToTable("PATIENT_LINK_AUDIT");
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.LinkId, x.OccurredAt });
            b.HasOne(x => x.Link).WithMany().HasForeignKey(x => x.LinkId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
