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

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<ApplicationUser>(b =>
        {
            b.ToTable("USER_ACCOUNT");
            b.Property(x => x.FullName).HasMaxLength(160);
            b.Property(x => x.AccountType).HasMaxLength(16);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
            b.Property(x => x.NormalizedPhone).HasComputedColumnSql(
                "NULLIF(regexp_replace(btrim(\"PhoneNumber\"), '[ ()-]', '', 'g'), '')", stored: true);
            b.HasIndex(x => x.NormalizedPhone).IsUnique();
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
    }
}
