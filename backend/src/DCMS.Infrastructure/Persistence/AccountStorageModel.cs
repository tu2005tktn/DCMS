using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Persistence;

internal static class AccountStorageModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Ignore<IdentityRole<int>>();
        model.Ignore<IdentityUserRole<int>>();
        model.Ignore<IdentityUserClaim<int>>();
        model.Ignore<IdentityRoleClaim<int>>();
        model.Ignore<IdentityUserLogin<int>>();
        model.Ignore<IdentityUserToken<int>>();
        model.Ignore<AuthSession>();
        model.Ignore<AuthRefreshToken>();
        model.Ignore<RecoveryToken>();
        model.Ignore<EmailVerificationToken>();
        model.Ignore<RecoveryOutbox>();
        model.HasSequence<long>("account_entry_seq");
        model.HasSequence<int>("employee_id_seq");
        model.Entity<ApplicationUser>(b =>
        {
            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").HasDefaultValueSql("nextval('employee_id_seq')");
            b.HasAlternateKey(x => x.EmployeeId).HasName("AK_USER_ACCOUNT_EmployeeId");
            b.Property(x => x.AssignedRoles).HasColumnType("text[]");
            b.HasIndex(x => x.GoogleSubject).IsUnique();
            b.ComplexCollection(x => x.Claims).ToJson();
            b.ComplexCollection(x => x.Logins).ToJson();
            b.ComplexCollection(x => x.IdentityTokens).ToJson();
            b.ComplexCollection(x => x.Sessions, s =>
            {
                s.ToJson();
                s.ComplexCollection(x => x.RefreshTokens);
            });
            b.ComplexCollection(x => x.EmailVerifications).ToJson();
            b.ComplexCollection(x => x.Recoveries).ToJson();
            b.ComplexCollection(x => x.RecoveryDeliveries).ToJson();
        });
        model.Entity<EmployeeProfile>(b =>
        {
            b.ToTable("USER_ACCOUNT");
            b.HasKey(x => x.AccountId);
            b.Property(x => x.AccountId).HasColumnName("Id");
            b.Property(x => x.Id).HasColumnName("EmployeeId").HasDefaultValueSql("nextval('employee_id_seq')");
            b.HasAlternateKey(x => x.Id).HasName("AK_USER_ACCOUNT_EmployeeId");
            b.HasIndex(x => x.EmployeeCode).IsUnique().HasDatabaseName("IX_EMPLOYEE_PROFILE_EmployeeCode");
            b.Property(x => x.EmployeeCode).HasMaxLength(40);
            b.Property(x => x.FullName).HasColumnName("EmployeeFullName").HasMaxLength(160);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<EmployeeProfile>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StaffAccessAssignment>(b =>
        {
            b.ToTable("USER_ACCOUNT"); b.HasKey(x => x.AccountId);
            b.Property(x => x.AccountId).HasColumnName("Id");
            b.Property(x => x.Version).HasColumnName("AccessVersion").IsConcurrencyToken(false);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<StaffAccessAssignment>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccessGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        // Every representation of the shared row participates in its concurrency checks.
        foreach (var type in new[] { typeof(ApplicationUser), typeof(EmployeeProfile), typeof(StaffAccessAssignment) })
        {
            var b = model.Entity(type);
            if (type == typeof(StaffAccessAssignment)) b.Property<int>("AccountVersion").HasColumnName("Version").IsConcurrencyToken();
            else b.Property<int>("Version").HasColumnName("Version").IsConcurrencyToken();
            b.Property<string>("ConcurrencyStamp").HasColumnName("ConcurrencyStamp").IsConcurrencyToken();
        }
        model.Entity<AccessGroup>(b =>
        {
            b.Property(x => x.RecordKind).HasMaxLength(24);
            b.HasQueryFilter(x => x.RecordKind == "PermissionGroup");
            b.HasIndex(x => x.IdentityRoleId).IsUnique();
            b.HasIndex(x => new { x.RecordKind, x.NormalizedCode }).IsUnique();
            b.HasIndex(x => new { x.RecordKind, x.NormalizedName }).IsUnique();
            b.ComplexCollection(x => x.RoleClaims).ToJson();
        });
        // Clinical foreign keys continue to use the historical employee identifier.
        var employee = model.Entity<EmployeeProfile>().Metadata;
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).Where(x => x.PrincipalEntityType == employee).ToArray())
            model.Entity(fk.DeclaringEntityType.ClrType).HasOne(typeof(EmployeeProfile)).WithMany()
                .HasForeignKey(fk.Properties.Select(x => x.Name).ToArray()).HasPrincipalKey(nameof(EmployeeProfile.Id));
    }
}
