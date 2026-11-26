using System.IdentityModel.Tokens.Jwt;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of AuthenticationService (sign-in, session validation, sign-out, refresh-token rotation, sign-out-all, session list)
/// and its helpers (CreateSessionAsync, CurrentAsync, NormalizeDeviceLabel, token hashing). Runs on PostgreSQL (row locks, jsonb token lookup).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private const string Password = "Abcdef1!";

    private sealed class Perms(params string[] permissions) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], permissions, []));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public UserManager<ApplicationUser> Users => CreateTestKit.NewRoleUserManager(Db);
        public AuthenticationService Svc(params string[] permissions) => new(Db, Users,
            Options.Create(new JwtSettings { SigningKey = "unit-test-signing-key-0123456789-abcdef", AccessTokenMinutes = 15, RefreshTokenDays = 30 }),
            new Perms(permissions), new StaffIdleService(Db, new TestClock(Now)), new TestClock(Now));

        public async Task<ApplicationUser> User(string type = "Staff", string[]? roles = null, string password = Password, bool active = true, string? email = null, bool employee = true, string? name = null)
        {
            var n = CreateTestKit.NextId();
            var user = new ApplicationUser { UserName = name ?? "u" + n, FullName = "Người dùng " + n, AccountType = type, IsActive = active, Email = email,
                AssignedRoles = roles ?? (type == "Staff" ? [AccountRoles.ClinicAssistant] : [AccountRoles.Patient]) };
            var result = await Users.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(e => e.Description)));
            if (type == "Staff" && employee) { Db.Employees.Add(new EmployeeProfile { AccountId = user.Id, EmployeeCode = "NV" + user.Id, FullName = user.FullName }); await Db.SaveChangesAsync(); }
            return user;
        }
        public async Task<ApplicationUser> Reload(int id) { Db.ChangeTracker.Clear(); return await Db.Users.AsNoTracking().SingleAsync(x => x.Id == id); }
        public async Task Mutate(int id, Action<ApplicationUser> change) { Db.ChangeTracker.Clear(); var u = await Db.Users.SingleAsync(x => x.Id == id); change(u); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public Task<SignInResponse> SignIn(string name, string password = Password, string? device = null, string[]? permissions = null) =>
            Svc(permissions ?? []).SignInAsync(new SignInRequest(name, password, device), default);
    }

    private static async Task<Env> NewEnv() => new() { Pg = await CreateTestKit.NewPostgresDbAsync() };

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= SignInAsync =================
    // Input: bad credentials -> Expected: 401 MSG09 whatever the reason (no hint which part is wrong).
    [Theory]
    [InlineData("UT-AUT-01 Tên đăng nhập rỗng", "", "Abcdef1!")]
    [InlineData("UT-AUT-02 Tên đăng nhập toàn khoảng trắng", "   ", "Abcdef1!")]
    [InlineData("UT-AUT-03 Mật khẩu rỗng", "known", "")]
    [InlineData("UT-AUT-04 Mật khẩu 257 ký tự", "known", "A1!aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("UT-AUT-05 Sai mật khẩu", "known", "Wrong123!")]
    [InlineData("UT-AUT-06 Tài khoản không tồn tại", "ghost-account", "Abcdef1!")]
    public async Task SignIn_rejects_bad_credentials(string caseName, string userName, string password)
    {
        await using var env = await NewEnv();
        var user = await env.User();
        var name = userName == "known" ? user.UserName! : userName;
        await Rejects(() => env.SignIn(name, password), 401, "MSG09");
    }

    [Fact]
    public async Task SignIn_UT_AUT_07_inactive_locked_roleless_and_malformed_accounts_are_denied()
    {
        await using var env = await NewEnv();
        var inactive = await env.User(active: false);
        await Rejects(() => env.SignIn(inactive.UserName!), 401, "MSG09");
        var locked = await env.User();
        await env.Mutate(locked.Id, u => { u.LockoutEnabled = true; u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1); });
        await Rejects(() => env.SignIn(locked.UserName!), 401, "MSG09");
        await env.Mutate(locked.Id, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(-1));      // an expired lockout no longer blocks
        Assert.NotNull(await env.SignIn(locked.UserName!));
        var roleless = await env.User(roles: []);
        await Rejects(() => env.SignIn(roleless.UserName!), 401, "MSG09");
        var staffWithPatientRole = await env.User("Staff", [AccountRoles.Patient]);
        await Rejects(() => env.SignIn(staffWithPatientRole.UserName!), 401, "MSG09");
        var staffWithoutEmployee = await env.User("Staff", [AccountRoles.Dentist], employee: false);
        await Rejects(() => env.SignIn(staffWithoutEmployee.UserName!), 401, "MSG09");
        var publicAdmin = await env.User("Public", [AccountRoles.Admin]);
        await Rejects(() => env.SignIn(publicAdmin.UserName!), 401, "MSG09");
        var unknownType = await env.User("Robot", [AccountRoles.Patient]);
        await Rejects(() => env.SignIn(unknownType.UserName!), 401, "MSG09");
    }

    [Fact]
    public async Task SignIn_UT_AUT_08_staff_gets_a_staff_web_session_with_hashed_refresh_token_and_audit()
    {
        await using var env = await NewEnv();
        var user = await env.User("Staff", [AccountRoles.ClinicOwner]);
        var result = await env.SignIn("  " + user.UserName + "  ", device: "  Laptop  ", permissions: ["reception.read"]);
        Assert.Equal((user.Id, "Staff", Now.AddMinutes(15)), (result.User.Id, result.User.AccountType, result.ExpiresAt));
        Assert.Equal([AccountRoles.ClinicOwner], result.User.Roles);
        Assert.Contains(AccountRoles.Dentist, result.User.EffectiveRoles);
        Assert.Contains("reception.read", result.User.Permissions);
        Assert.Contains("settings.general.manage", result.User.Permissions);       // the owner always manages general settings
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal((user.Id.ToString(), result.SessionId.ToString()), (jwt.Subject, jwt.Claims.Single(x => x.Type == "sid").Value));
        var saved = await env.Reload(user.Id);
        var session = Assert.Single(saved.Sessions);
        Assert.Equal(("StaffWeb", "Laptop", Now.AddDays(30), Now, false), (session.ClientKind, session.DeviceLabel, session.ExpiresAt, session.LastInteractiveActivityAt, session.RevokedAt != null));
        var token = Assert.Single(session.RefreshTokens);
        Assert.Equal("ACTIVE", token.Status);
        Assert.NotEqual(result.RefreshToken, token.TokenHash);                       // only the hash is stored
        Assert.Equal(64, token.TokenHash.Length);
        var audit = await env.Db.AccountAudits.AsNoTracking().Where(x => x.TargetId == user.Id).SingleAsync();
        Assert.Equal(("SignIn", user.Id), (audit.Action, audit.ActorId));
    }

    [Fact]
    public async Task SignIn_UT_AUT_09_public_account_session_is_short_and_has_no_staff_permissions()
    {
        await using var env = await NewEnv();
        var user = await env.User("Public", [AccountRoles.Patient]);
        var result = await env.SignIn(user.UserName!, permissions: ["reception.read"]);
        Assert.Empty(result.User.Permissions);
        Assert.False(result.User.HasVerifiedPatientLink);
        Assert.Contains(AccountRoles.Guest, result.User.EffectiveRoles);               // Patient includes Guest
        var session = Assert.Single((await env.Reload(user.Id)).Sessions);
        Assert.Equal(("Patient", Now.AddMinutes(15), "Unknown device"), (session.ClientKind, session.ExpiresAt, session.DeviceLabel));
    }

    [Fact]
    public async Task SignIn_UT_AUT_10_public_account_may_sign_in_with_a_unique_email_but_staff_may_not()
    {
        await using var env = await NewEnv();
        var email = $"mai{CreateTestKit.NextId()}@dcms.vn";
        var user = await env.User("Public", [AccountRoles.Guest], email: email);
        Assert.Equal(user.Id, (await env.SignIn(email.ToUpperInvariant())).User.Id);
        var staffEmail = $"nv{CreateTestKit.NextId()}@dcms.vn";
        await env.User("Staff", email: staffEmail);
        await Rejects(() => env.SignIn(staffEmail), 401, "MSG09");
        await Rejects(() => env.SignIn($"none{CreateTestKit.NextId()}@dcms.vn"), 401, "MSG09");
    }

    [Fact]
    public async Task SignIn_UT_AUT_11_the_database_prevents_two_accounts_sharing_an_email_so_the_ambiguity_guard_is_defensive()
    {
        await using var env = await NewEnv();
        var email = $"dup{CreateTestKit.NextId()}@dcms.vn";
        await env.User("Public", [AccountRoles.Guest], email: email);
        // The unique EmailIndex rejects the second account, so the service's "two matches -> deny" branch cannot be reached through data.
        await Assert.ThrowsAsync<DbUpdateException>(() => env.User("Public", [AccountRoles.Guest], email: email));
    }

    // Input: device label -> Expected: stored DeviceLabel.
    [Theory]
    [InlineData("UT-AUT-12 null", null, "Unknown device")]
    [InlineData("UT-AUT-13 chuỗi rỗng", "", "Unknown device")]
    [InlineData("UT-AUT-14 khoảng trắng", "   ", "Unknown device")]
    [InlineData("UT-AUT-15 có khoảng trắng hai đầu", "  iPhone 15  ", "iPhone 15")]
    [InlineData("UT-AUT-16 đúng 120 ký tự", "{120}", "{120}")]
    [InlineData("UT-AUT-17 121 ký tự bị cắt", "{121}", "{120}")]
    public async Task SignIn_normalizes_the_device_label(string caseName, string? label, string expected)
    {
        await using var env = await NewEnv();
        var user = await env.User();
        string Expand(string? s) => s switch { "{120}" => new string('a', 120), "{121}" => new string('a', 121), _ => s! };
        await env.SignIn(user.UserName!, device: label == null ? null : Expand(label));
        Assert.Equal(expected.StartsWith('{') ? Expand(expected) : expected, Assert.Single((await env.Reload(user.Id)).Sessions).DeviceLabel);
    }

    // ================= CreateSessionAsync =================
    [Fact]
    public async Task CreateSession_UT_AUT_18_denies_unknown_inactive_and_locked_accounts_and_records_the_audit_action()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().CreateSessionAsync(987654321, null, "SignIn", default), 401, "MSG09");
        var inactive = await env.User(active: false);
        await Rejects(() => env.Svc().CreateSessionAsync(inactive.Id, null, "SignIn", default), 401, "MSG09");
        var ok = await env.User();
        var result = await env.Svc().CreateSessionAsync(ok.Id, "Máy A", "GoogleSignIn", default);
        Assert.Equal(ok.Id, result.User.Id);
        Assert.Equal("GoogleSignIn", (await env.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == ok.Id)).Action);
    }

    // ================= ValidateSessionAsync =================
    [Fact]
    public async Task ValidateSession_UT_AUT_19_accepts_a_live_session_and_refreshes_last_seen()
    {
        await using var env = await NewEnv();
        var user = await env.User("Public");
        var signed = await env.SignIn(user.UserName!);
        var current = await env.Svc().ValidateSessionAsync(user.Id, signed.SessionId, default);
        Assert.Equal(user.Id, current!.Id);
        Assert.Equal(Now, Assert.Single((await env.Reload(user.Id)).Sessions).LastSeenAt);
    }

    [Fact]
    public async Task ValidateSession_UT_AUT_20_rejects_unknown_revoked_expired_and_disabled()
    {
        await using var env = await NewEnv();
        var user = await env.User("Public");
        var signed = await env.SignIn(user.UserName!);
        Assert.Null(await env.Svc().ValidateSessionAsync(user.Id, Guid.NewGuid(), default));       // unknown session
        Assert.Null(await env.Svc().ValidateSessionAsync(987654321, signed.SessionId, default));   // unknown account
        await env.Mutate(user.Id, u => u.Sessions[0].ExpiresAt = Now);                              // ExpiresAt <= now
        Assert.Null(await env.Svc().ValidateSessionAsync(user.Id, signed.SessionId, default));
        await env.Mutate(user.Id, u => { u.Sessions[0].ExpiresAt = Now.AddHours(1); u.Sessions[0].RevokedAt = Now; });
        Assert.Null(await env.Svc().ValidateSessionAsync(user.Id, signed.SessionId, default));
        await env.Mutate(user.Id, u => { u.Sessions[0].RevokedAt = null; u.IsActive = false; });
        Assert.Null(await env.Svc().ValidateSessionAsync(user.Id, signed.SessionId, default));
        await env.Mutate(user.Id, u => { u.IsActive = true; u.LockoutEnabled = true; u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1); });
        Assert.Null(await env.Svc().ValidateSessionAsync(user.Id, signed.SessionId, default));
    }

    [Fact]
    public async Task ValidateSession_UT_AUT_21_an_idle_staff_session_expires_but_a_public_one_does_not()
    {
        await using var env = await NewEnv();
        var idle = await new StaffIdleService(env.Db, new TestClock(Now)).MinutesAsync(default);
        var staff = await env.User("Staff");
        var signed = await env.SignIn(staff.UserName!);
        await env.Mutate(staff.Id, u => u.Sessions[0].LastInteractiveActivityAt = Now.AddMinutes(-idle + 1));   // still inside the timeout
        Assert.NotNull(await env.Svc().ValidateSessionAsync(staff.Id, signed.SessionId, default));
        await env.Mutate(staff.Id, u => u.Sessions[0].LastInteractiveActivityAt = Now.AddMinutes(-idle));       // exactly at the limit -> expired
        Assert.Null(await env.Svc().ValidateSessionAsync(staff.Id, signed.SessionId, default));
        Assert.NotNull((await env.Reload(staff.Id)).Sessions[0].RevokedAt);
        var patient = await env.User("Public");
        var ps = await env.SignIn(patient.UserName!);
        await env.Mutate(patient.Id, u => u.Sessions[0].LastInteractiveActivityAt = Now.AddDays(-30));
        Assert.NotNull(await env.Svc().ValidateSessionAsync(patient.Id, ps.SessionId, default));
    }

    // ================= SignOutAsync / SignOutAllAsync / ListSessionsAsync =================
    [Fact]
    public async Task SignOut_UT_AUT_22_revokes_the_session_and_its_tokens_once()
    {
        await using var env = await NewEnv();
        var user = await env.User();
        var signed = await env.SignIn(user.UserName!);
        await env.Svc().SignOutAsync(user.Id, Guid.NewGuid(), default);                 // unknown session: no-op
        Assert.Equal(1, await env.Db.AccountAudits.CountAsync(x => x.TargetId == user.Id));
        await env.Svc().SignOutAsync(user.Id, signed.SessionId, default);
        var saved = await env.Reload(user.Id);
        var session = Assert.Single(saved.Sessions);
        Assert.Equal((Now, "REVOKED"), (session.RevokedAt, session.RefreshTokens.Single().Status));
        await env.Svc().SignOutAsync(user.Id, signed.SessionId, default);               // already revoked: no new audit
        Assert.Equal(2, await env.Db.AccountAudits.CountAsync(x => x.TargetId == user.Id));
        Assert.Equal("SignOut", (await env.Db.AccountAudits.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync(x => x.TargetId == user.Id)).Action);
    }

    [Fact]
    public async Task SignOutAll_UT_AUT_23_revokes_every_session_with_default_or_custom_reason()
    {
        await using var env = await NewEnv();
        var user = await env.User();
        var a = await env.SignIn(user.UserName!, device: "A"); await env.SignIn(user.UserName!, device: "B");
        await env.Svc().SignOutAllAsync(user.Id, a.SessionId, null, default);
        await env.Svc().SignOutAllAsync(user.Id, a.SessionId, "  Mất điện thoại  ", default);
        var saved = await env.Reload(user.Id);
        Assert.All(saved.Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.All(saved.Sessions.SelectMany(s => s.RefreshTokens), t => Assert.Equal("REVOKED", t.Status));
        var reasons = await env.Db.AccountAudits.AsNoTracking().Where(x => x.TargetId == user.Id && x.Action == "SignOutAll").OrderBy(x => x.Id).Select(x => x.Reason).ToListAsync();
        Assert.Equal(["Đăng xuất khỏi mọi thiết bị", "Mất điện thoại"], reasons);
    }

    [Fact]
    public async Task ListSessions_UT_AUT_24_lists_only_live_sessions_newest_first_and_flags_the_current_one()
    {
        await using var env = await NewEnv();
        var user = await env.User("Staff");
        var a = await env.SignIn(user.UserName!, device: "A"); var b = await env.SignIn(user.UserName!, device: "B"); var c = await env.SignIn(user.UserName!, device: "C");
        await env.Mutate(user.Id, u =>
        {
            u.Sessions.Single(s => s.Id == a.SessionId).LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-3);
            u.Sessions.Single(s => s.Id == b.SessionId).LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            u.Sessions.Single(s => s.Id == c.SessionId).RevokedAt = Now;
        });
        var list = await env.Svc().ListSessionsAsync(user.Id, a.SessionId, default);
        Assert.Equal(["B", "A"], list.Select(x => x.DeviceLabel));
        Assert.Equal([false, true], list.Select(x => x.CurrentSession));
        await env.Mutate(user.Id, u => u.Sessions.Single(s => s.Id == a.SessionId).ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1));   // expired sessions are hidden
        Assert.Equal(["B"], (await env.Svc().ListSessionsAsync(user.Id, a.SessionId, default)).Select(x => x.DeviceLabel));
        Assert.Empty(await env.Svc().ListSessionsAsync(987654321, Guid.NewGuid(), default));
    }

    // ================= RefreshAsync =================
    [Theory]
    [InlineData("UT-AUT-25 Refresh token null", null)]
    [InlineData("UT-AUT-26 Refresh token rỗng", "")]
    [InlineData("UT-AUT-27 Refresh token khoảng trắng", "   ")]
    [InlineData("UT-AUT-28 Refresh token không tồn tại", "not-a-real-token")]
    public async Task Refresh_rejects_missing_or_unknown_tokens(string caseName, string? token)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(token!), default), 401, "MSG07");
    }

    [Fact]
    public async Task Refresh_UT_AUT_29_rotates_the_token_and_marks_the_old_one_replaced()
    {
        await using var env = await NewEnv();
        var user = await env.User("Staff");
        var signed = await env.SignIn(user.UserName!);
        var result = await env.Svc().RefreshAsync(new RefreshRequest(signed.RefreshToken), default);
        Assert.Equal(signed.SessionId, result.SessionId);
        Assert.NotEqual(signed.RefreshToken, result.RefreshToken);
        Assert.NotEqual(signed.AccessToken, result.AccessToken);
        var session = Assert.Single((await env.Reload(user.Id)).Sessions);
        Assert.Equal(["ACTIVE", "REPLACED"], session.RefreshTokens.Select(t => t.Status).Order());
        var old = session.RefreshTokens.Single(t => t.Status == "REPLACED");
        Assert.Equal(session.RefreshTokens.Single(t => t.Status == "ACTIVE").Id, old.ReplacedById);
        Assert.Contains(env.Db.AccountAudits.Where(x => x.TargetId == user.Id), a => a.Action == "Refresh");
        // the rotated token keeps working
        Assert.NotNull(await env.Svc().RefreshAsync(new RefreshRequest(result.RefreshToken), default));
    }

    [Fact]
    public async Task Refresh_UT_AUT_30_reusing_a_replaced_token_revokes_every_session()
    {
        await using var env = await NewEnv();
        var user = await env.User("Staff");
        var first = await env.SignIn(user.UserName!); await env.SignIn(user.UserName!, device: "other");
        await env.Svc().RefreshAsync(new RefreshRequest(first.RefreshToken), default);
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(first.RefreshToken), default), 401, "MSG07");
        var saved = await env.Reload(user.Id);
        Assert.All(saved.Sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.Contains(env.Db.AccountAudits.Where(x => x.TargetId == user.Id), a => a.Action == "RefreshReuse");
    }

    [Fact]
    public async Task Refresh_UT_AUT_31_revoked_expired_disabled_and_idle_sessions_cannot_refresh()
    {
        await using var env = await NewEnv();
        var user = await env.User("Public");
        var signed = await env.SignIn(user.UserName!);
        await env.Mutate(user.Id, u => u.Sessions[0].RevokedAt = Now);
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(signed.RefreshToken), default), 401, "MSG07");

        var second = await env.User("Public"); var s2 = await env.SignIn(second.UserName!);
        await env.Mutate(second.Id, u => u.Sessions[0].ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1));
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(s2.RefreshToken), default), 401, "MSG07");
        Assert.NotNull((await env.Reload(second.Id)).Sessions[0].RevokedAt);                     // the chain was revoked

        var third = await env.User("Public"); var s3 = await env.SignIn(third.UserName!);
        await env.Mutate(third.Id, u => u.IsActive = false);
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(s3.RefreshToken), default), 401, "MSG07");
        Assert.NotNull((await env.Reload(third.Id)).Sessions[0].RevokedAt);

        var staff = await env.User("Staff"); var s4 = await env.SignIn(staff.UserName!);
        await env.Mutate(staff.Id, u => u.Sessions[0].LastInteractiveActivityAt = DateTimeOffset.UtcNow.AddDays(-1));
        await Rejects(() => env.Svc().RefreshAsync(new RefreshRequest(s4.RefreshToken), default), 401, "MSG07");
        Assert.NotNull((await env.Reload(staff.Id)).Sessions[0].RevokedAt);                      // idle expiry revokes
    }

    [Fact]
    public async Task Refresh_UT_AUT_32_a_public_session_gets_a_short_expiry_and_a_staff_session_a_long_one()
    {
        await using var env = await NewEnv();
        var staff = await env.User("Staff"); var s = await env.SignIn(staff.UserName!);
        var pub = await env.User("Public"); var p = await env.SignIn(pub.UserName!);
        // Refresh uses the real clock, so compare the span rather than an absolute instant.
        var rs = await env.Svc().RefreshAsync(new RefreshRequest(s.RefreshToken), default);
        var rp = await env.Svc().RefreshAsync(new RefreshRequest(p.RefreshToken), default);
        var staffSession = (await env.Reload(staff.Id)).Sessions[0]; var pubSession = (await env.Reload(pub.Id)).Sessions[0];
        Assert.InRange((staffSession.ExpiresAt - DateTimeOffset.UtcNow).TotalDays, 29.9, 30.1);
        Assert.InRange((pubSession.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes, 14, 16);
        Assert.InRange((rs.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes, 14, 16);
        Assert.Equal(pub.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(rp.AccessToken).Subject);
    }
}
