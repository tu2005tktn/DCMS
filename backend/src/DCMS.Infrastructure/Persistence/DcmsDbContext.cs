using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;

namespace DCMS.Infrastructure.Persistence;

public sealed class DcmsDbContext(DbContextOptions<DcmsDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<EmployeeProfile> Employees => Set<EmployeeProfile>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AccountAudit> AccountAudits => Set<AccountAudit>();
    public DbSet<RecoveryToken> RecoveryTokens => Set<RecoveryToken>();
    public DbSet<RecoveryAudit> RecoveryAudits => Set<RecoveryAudit>();
    public DbSet<RecoveryOutbox> RecoveryOutbox => Set<RecoveryOutbox>();

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
        model.Entity<RecoveryToken>(b =>
        {
            b.ToTable("RECOVERY_TOKEN");
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
            b.Property(x => x.ResetGrantHash).HasMaxLength(256).IsRequired();
            b.Property(x => x.ChannelType).HasMaxLength(16).IsRequired();
            b.Property(x => x.NormalizedDestination).HasMaxLength(256).IsRequired();
            b.HasIndex(x => new { x.AccountId, x.UsedAt });
            b.HasIndex(x => x.ExpiresAt);
            b.HasIndex(x => new { x.AccountId, x.CreatedAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<RecoveryAudit>(b =>
        {
            b.ToTable("RECOVERY_AUDIT");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(40).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.ChannelType).HasMaxLength(16);
            b.Property(x => x.Result).HasMaxLength(20);
            b.HasIndex(x => new { x.TargetId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RecoveryOutbox>(b =>
        {
            b.ToTable("RECOVERY_OUTBOX");
            b.HasKey(x => x.Id);
            b.Property(x => x.ChannelType).HasMaxLength(16).IsRequired();
            b.Property(x => x.Destination).HasMaxLength(256).IsRequired();
            b.Property(x => x.Provider).HasMaxLength(32).IsRequired();
            b.Property(x => x.Status).HasMaxLength(16).IsRequired();
            b.Property(x => x.ProviderMessageId).HasMaxLength(64);
            b.HasIndex(x => new { x.AccountId, x.OccurredAt });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
