using System.Security.Claims;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class AccountStorageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task TwoTablesPreserveIdentitySessionEmployeeAndAssignmentAcrossRollback()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser { UserName = "storage." + suffix, FullName = "SYNTHETIC account",
            AccountType = "Staff", CreatedAt = now, UpdatedAt = now };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AccountRoles.Dentist)).Succeeded);
        Assert.True((await users.AddClaimAsync(user, new Claim("permission", "SYNTHETIC"))).Succeeded);
        Assert.True((await users.AddLoginAsync(user, new UserLoginInfo("Google", suffix, "SYNTHETIC"))).Succeeded);
        Assert.True((await users.SetAuthenticationTokenAsync(user, "SYNTHETIC", "test", "SYNTHETIC token")).Succeeded);
        var role = new IdentityRole<int>("Synthetic" + suffix[..12]);
        Assert.True((await roles.CreateAsync(role)).Succeeded);
        Assert.True((await roles.AddClaimAsync(role, new Claim("SYNTHETIC", "role claim"))).Succeeded);
        var employee = new EmployeeProfile { AccountId = user.Id, EmployeeCode = "ST-" + suffix[..12], FullName = user.FullName };
        var group = new AccessGroup { Code = "ST-" + suffix[..12], NormalizedCode = suffix, Name = "SYNTHETIC",
            NormalizedName = suffix, BaseRole = AccountRoles.Dentist, Grants = [new() { Permission = "patients.read" }] };
        var session = new AuthSession { Id = Guid.NewGuid(), AccountId = user.Id, DeviceLabel = "SYNTHETIC",
            CreatedAt = now, LastSeenAt = now, LastInteractiveActivityAt = now, ExpiresAt = now.AddDays(1), ClientKind = "StaffWeb" };
        session.RefreshTokens.Add(new() { Id = await db.NextAccountEntryIdAsync(default), AccountId = user.Id,
            SessionId = session.Id, TokenHash = "SYNTHETIC hash", IssuedAt = now, ExpiresAt = now.AddDays(1), Status = "ACTIVE" });
        user.Sessions.Add(session);
        user.Recoveries.Add(new() { Id = checked((int)await db.NextAccountEntryIdAsync(default)), AccountId = user.Id,
            TokenHash = "SYNTHETIC proof hash", ResetGrantHash = "SYNTHETIC grant hash", ChannelType = "Email",
            NormalizedDestination = "synthetic@example.invalid", CreatedAt = now, ExpiresAt = now.AddMinutes(10) });
        user.EmailVerifications.Add(new() { Id = await db.NextAccountEntryIdAsync(default), AccountId = user.Id,
            NormalizedEmail = "SYNTHETIC@EXAMPLE.INVALID", CodeHash = "SYNTHETIC hash", CreatedAt = now,
            LastSentAt = now, ExpiresAt = now.AddMinutes(10) });
        user.RecoveryDeliveries.Add(new() { Id = await db.NextAccountEntryIdAsync(default), AccountId = user.Id,
            ChannelType = "Email", Destination = "synthetic@example.invalid", Provider = "SYNTHETIC", Status = "Sent", OccurredAt = now });
        db.AddRange(employee, group, new StaffAccessAssignment { AccountId = user.Id, GroupId = group.Id, BranchIds = [1, 2] });
        await db.SaveChangesAsync();
        Assert.Equal(user.EmployeeId, employee.Id);
        var employeeId = employee.Id;
        db.ChangeTracker.Clear();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261112083713_SimplifyScreenStorage");
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();

        var tableNames = await db.Database.SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname=current_schema()").ToArrayAsync();
        Assert.Contains("USER_ACCOUNT", tableNames); Assert.Contains("ACCESS_GROUP", tableNames);
        foreach (var removed in new[] { "EMPLOYEE_PROFILE", "ROLE", "USER_ACCOUNT_ROLE", "STAFF_ACCESS_ASSIGNMENT", "AUTH_SESSION",
            "AUTH_REFRESH_TOKEN", "RECOVERY_TOKEN", "EMAIL_VERIFICATION_TOKEN", "RECOVERY_OUTBOX", "AspNetUserClaims", "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens" })
            Assert.DoesNotContain(removed, tableNames);
        var read = await users.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(read);
        Assert.True(await users.CheckPasswordAsync(read, fixture.Password));
        Assert.Contains(AccountRoles.Dentist, await users.GetRolesAsync(read));
        Assert.Equal("SYNTHETIC", Assert.Single(await users.GetClaimsAsync(read)).Value);
        Assert.Equal(suffix, Assert.Single(await users.GetLoginsAsync(read)).ProviderKey);
        Assert.Equal(user.Id, (await users.FindByLoginAsync("Google", suffix))!.Id);
        Assert.Equal("SYNTHETIC token", await users.GetAuthenticationTokenAsync(read, "SYNTHETIC", "test"));
        Assert.Equal("role claim", Assert.Single(await roles.GetClaimsAsync((await roles.FindByIdAsync(role.Id.ToString()))!)).Value);
        Assert.Equal(session.Id, Assert.Single(read.Sessions).Id);
        Assert.Equal(session.RefreshTokens[0].Id, Assert.Single(read.Sessions[0].RefreshTokens).Id);
        Assert.Equal(user.Recoveries[0].Id, Assert.Single(read.Recoveries).Id);
        Assert.Equal(user.EmailVerifications[0].Id, Assert.Single(read.EmailVerifications).Id);
        Assert.Equal(user.RecoveryDeliveries[0].Id, Assert.Single(read.RecoveryDeliveries).Id);
        Assert.Equal(employeeId, (await db.Employees.SingleAsync(x => x.AccountId == user.Id)).Id);
        var assignment = await db.Set<StaffAccessAssignment>().SingleAsync(x => x.AccountId == user.Id);
        Assert.Equal(new[] { 1, 2 }, assignment.BranchIds);
        db.Remove(assignment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<StaffAccessAssignment>().AnyAsync(x => x.AccountId == user.Id));
        Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id));
        Assert.True(await db.Employees.AnyAsync(x => x.Id == employeeId));
    }

    [Fact]
    public async Task StaleAccountWriteCannotDiscardAnotherSessionsChanges()
    {
        using var first = fixture.Services.CreateScope(); using var second = fixture.Services.CreateScope();
        var db1 = first.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var auth = first.ServiceProvider.GetRequiredService<DCMS.Application.Features.Authentication.IAuthenticationService>();
        var signedIn = await auth.SignInAsync(new("dentist.demo", fixture.Password, "SYNTHETIC"), default);
        Assert.Contains((await db1.Users.SingleAsync(x => x.UserName == "dentist.demo")).Sessions, x => x.Id == signedIn.SessionId);
        var one = await db1.Users.SingleAsync(x => x.UserName == "guest.demo");
        var two = await db2.Users.SingleAsync(x => x.Id == one.Id);
        one.Claims.Add(new() { Id = checked((int)await db1.NextAccountEntryIdAsync(default)), Type = "SYNTHETIC", Value = "kept" });
        await db1.SaveChangesAsync();
        two.FullName = "SYNTHETIC stale write";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        db2.ChangeTracker.Clear();
        Assert.Contains((await db2.Users.SingleAsync(x => x.Id == one.Id)).Claims, x => x.Value == "kept");
    }
}
