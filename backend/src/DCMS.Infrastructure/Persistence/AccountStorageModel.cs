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
        model.Ignore<UserAuthData>();
        model.HasSequence<long>("account_entry_seq");
        model.HasSequence<int>("employee_id_seq");
        model.Entity<ApplicationUser>(b =>
        {
            b.Property(x => x.EmployeeId).HasColumnName("EmployeeId").HasDefaultValueSql("nextval('employee_id_seq')");
            b.HasAlternateKey(x => x.EmployeeId).HasName("AK_USER_ACCOUNT_EmployeeId");
            b.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(160);
            b.Property(x => x.AssignedRoles).HasColumnType("text[]");
            b.Ignore(x => x.NormalizedUserName);
            b.Ignore(x => x.NormalizedEmail);
            b.Ignore(x => x.NormalizedPhone);
            b.Ignore(x => x.ConcurrencyStamp);
            b.Ignore(x => x.SecurityStamp);
            b.Ignore(x => x.PhoneNumberConfirmed);
            b.Ignore(x => x.TwoFactorEnabled);
            b.Ignore(x => x.LockoutEnd);
            b.Ignore(x => x.LockoutEnabled);
            b.Ignore(x => x.AccessFailedCount);
            b.Ignore(x => x.Claims);
            b.Ignore(x => x.Logins);
            b.Ignore(x => x.IdentityTokens);
            b.Ignore(x => x.Sessions);
            b.Ignore(x => x.EmailVerifications);
            b.Ignore(x => x.Recoveries);
            b.Ignore(x => x.RecoveryDeliveries);
            b.Property(x => x.AuthData)
                .HasColumnName("AuthData")
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(SharedStorageModel.JsonConv<UserAuthData>(), SharedStorageModel.JsonCompare<UserAuthData>());
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
            b.Property(x => x.FullName).HasColumnName("FullName").HasMaxLength(160);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<EmployeeProfile>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StaffAccessAssignment>(b =>
        {
            b.ToTable("USER_ACCOUNT"); b.HasKey(x => x.AccountId);
            b.Property(x => x.AccountId).HasColumnName("Id");
            b.Ignore(x => x.Version);
            b.HasOne<ApplicationUser>().WithOne().HasForeignKey<StaffAccessAssignment>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccessGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        // PostgreSQL hidden system column xmin serves as the shared row-version concurrency token across all 3 split entities.
        foreach (var type in new[] { typeof(ApplicationUser), typeof(EmployeeProfile), typeof(StaffAccessAssignment) })
            model.Entity(type).Property<uint>("xmin").IsRowVersion();
        model.Entity<AccessGroup>(b =>
        {
            b.Property(x => x.RecordKind).HasMaxLength(24);
            b.HasQueryFilter(x => x.RecordKind == "PermissionGroup");
            b.HasIndex(x => x.IdentityRoleId).IsUnique();
        });
        // Clinical foreign keys continue to use the historical employee identifier.
        var employee = model.Entity<EmployeeProfile>().Metadata;
        foreach (var fk in model.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).Where(x => x.PrincipalEntityType == employee).ToArray())
            model.Entity(fk.DeclaringEntityType.ClrType).HasOne(typeof(EmployeeProfile)).WithMany()
                .HasForeignKey(fk.Properties.Select(x => x.Name).ToArray()).HasPrincipalKey(nameof(EmployeeProfile.Id));
    }
}
