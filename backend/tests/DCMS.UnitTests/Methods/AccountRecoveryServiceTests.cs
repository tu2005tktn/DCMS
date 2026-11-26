using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Core method-level tests of AccountRecoveryService (UC-02 password recovery, self-service and Admin-assisted):
/// RequestAsync, ResendAsync, VerifyAsync, ResetAsync, RequestForAccountAsync, VerifyForAccountAsync, ResetForAccountAsync, and GetStatusAsync. Runs on PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountRecoveryServiceTests
{
    private const string Password = "Abcdef1!";
    private const string NewPassword = "Newpass9#";
    private const string Neutral = "MSG12";

    private sealed class FakeDelivery : IContactDeliveryAdapter
    {
        public bool Succeeds = true;
        public readonly List<(int AccountId, RecoveryChannel Channel, string Destination, string Proof, string Link)> Sent = [];
        public RecoveryChannel DetectChannel(string contact) => RecoveryContactFormat.DetectChannel(contact);
        public bool IsValid(string contact) => RecoveryContactFormat.DetectChannel(contact) != RecoveryChannel.Unknown;
        public string Normalize(string contact) => RecoveryContactFormat.Normalize(contact);
        public Task<bool> SendRecoveryAsync(int accountId, RecoveryChannel channel, string destination, string proof, string resetLink, CancellationToken ct)
        {
            Sent.Add((accountId, channel, destination, proof, resetLink));
            return Task.FromResult(Succeeds);
        }
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int Tag;
        public required UserManager<ApplicationUser> Users;
        public required int Admin;
        public readonly RecoverySettings Settings = new() { ResendCooldownMinutes = 0, RateLimitPerAccountPerHour = 1000, RateLimitPerIpPerHour = 1000 };
        public readonly FakeDelivery Delivery = new();
        public readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 1000 });
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() { Cache.Dispose(); return Pg.DisposeAsync(); }

        public AccountRecoveryService Svc()
        {
            Db.ChangeTracker.Clear();
            return new AccountRecoveryService(Db, Users, Delivery, Options.Create(Settings), Cache, NullLogger<AccountRecoveryService>.Instance, new CreateTestKit.AllowAll());
        }

        public string UserName(int i) => $"rcv{Tag}u{i}";
        public string Email(int i) => $"Rcv{Tag}U{i}@Dcms.vn";

        public async Task<ApplicationUser> User(int i, string type = "Public", bool emailOk = true, bool phoneOk = false, bool noEmail = false, bool noPhone = false, bool active = true)
        {
            var u = new ApplicationUser
            {
                UserName = UserName(i), FullName = "Người dùng " + i, AccountType = type, IsActive = active,
                Email = noEmail ? null : Email(i), EmailConfirmed = emailOk,
                PhoneNumber = noPhone ? null : $"09{(Tag * 10 + i) % 100000000:D8}", PhoneNumberConfirmed = phoneOk,
                AssignedRoles = [type == "Public" ? AccountRoles.Guest : AccountRoles.ClinicAssistant]
            };
            Assert.True((await Users.CreateAsync(u, Password)).Succeeded);
            return u;
        }

        public string Proof => Delivery.Sent.Last().Proof;
        public async Task<ApplicationUser> Reload(int id) { Db.ChangeTracker.Clear(); return await Db.Users.AsNoTracking().SingleAsync(x => x.Id == id); }
        public async Task<List<RecoveryToken>> Tokens(int id) => (await Reload(id)).Recoveries.OrderBy(x => x.Id).ToList();
        public async Task<List<RecoveryAudit>> Audits(int id) => await Db.RecoveryAudits.AsNoTracking().Where(x => x.TargetId == id).OrderBy(x => x.Id).ToListAsync();
        public async Task Mutate(int id, Action<ApplicationUser> change) { Db.ChangeTracker.Clear(); var u = await Db.Users.SingleAsync(x => x.Id == id); change(u); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }

        /// <summary>Request, then verify with the delivered code; returns the reset grant.</summary>
        public async Task<string> Grant(ApplicationUser user)
        {
            await Svc().RequestAsync(user.UserName!, null, default);
            var outcome = await Svc().VerifyAsync(user.UserName!, Proof, null, default);
            Assert.True(outcome.Valid);
            return outcome.ResetGrant!;
        }
    }

    private static async Task<Env> NewEnv(Action<RecoverySettings>? tune = null)
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var tag = CreateTestKit.NextId();
        foreach (var role in AccountRoles.All)
            if (!await pg.Db.Set<AccessGroup>().IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                pg.Db.Set<AccessGroup>().Add(new() { RecordKind = "IdentityRole", IdentityRoleId = Array.IndexOf(AccountRoles.Staff, role) >= 0 ? 100 + Array.IndexOf(AccountRoles.Staff, role) : 200 + Array.IndexOf(AccountRoles.All, role), Code = role, Name = role, BaseRole = role });
        await pg.Db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(pg.Db);
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddIdentityCore<ApplicationUser>(options =>
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._")
            .AddRoles<IdentityRole<int>>().AddUserStore<AccountUserStore>().AddRoleStore<AccountRoleStore>().AddDefaultTokenProviders();
        var users = services.BuildServiceProvider().GetRequiredService<UserManager<ApplicationUser>>();

        var admin = new ApplicationUser { UserName = $"rcvadm{tag}", FullName = "Quản trị", AccountType = "Staff", IsActive = true, AssignedRoles = [AccountRoles.Admin] };
        Assert.True((await users.CreateAsync(admin, Password)).Succeeded);
        var env = new Env { Pg = pg, Tag = tag, Users = users, Admin = admin.Id };
        tune?.Invoke(env.Settings);
        return env;
    }

    private static async Task Rejects(Func<Task> call, int status, string code, string? message = null)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
    }

    private static readonly RecoveryRequestOutcome NeutralRequest = new(true, Neutral);

    // ================= Argument validation =================
    private static Task Invoke(Env env, string method, string identifier, bool publicOnly, string proof = "123456", string grant = "grant-123456", string password = NewPassword, string confirm = NewPassword)
    {
        var svc = env.Svc();
        return method switch
        {
            "Request" => svc.RequestAsync(identifier, null, default, publicOnly),
            "Resend" => svc.ResendAsync(identifier, null, default, publicOnly),
            "Verify" => svc.VerifyAsync(identifier, proof, null, default, publicOnly),
            _ => svc.ResetAsync(identifier, grant, password, confirm, default, publicOnly)
        };
    }

    public static TheoryData<string, string, string, bool, string> IdentifierRejected() => new()
    {
        { "UT-RCV-01 Request: định danh rỗng", "Request", "", false, "Vui lòng nhập Tài khoản." },
        { "UT-RCV-02 Request: định danh 65 ký tự", "Request", new string('a', 65), false, "Tài khoản không được vượt quá 64 ký tự." },
        { "UT-RCV-03 Request: khách hàng nhập số điện thoại", "Request", "0912345678", true, "Email không đúng định dạng." },
    };

    [Theory, MemberData(nameof(IdentifierRejected))]
    public async Task Self_service_methods_reject_invalid_identifiers(string caseName, string method, string identifier, bool publicOnly, string message)
    {
        await using var env = await NewEnv();
        await Rejects(() => Invoke(env, method, identifier, publicOnly), 400, message.StartsWith("Email") ? "MSG04" : message.StartsWith("Vui") ? "MSG02" : "MSG03", message);
        Assert.Empty(env.Delivery.Sent);
    }

    public static TheoryData<string, string, string, string, string> ProofRejected() => new()
    {
        { "UT-RCV-04 Verify: mã rỗng", "Verify", "", "MSG02", "Vui lòng nhập Mã xác minh." },
        { "UT-RCV-05 Verify: mã 3 ký tự", "Verify", "abc", "MSG04", "Mã xác minh không đúng định dạng." },
    };

    [Theory, MemberData(nameof(ProofRejected))]
    public async Task Verify_and_Reset_reject_invalid_proofs(string caseName, string method, string proof, string code, string message)
    {
        await using var env = await NewEnv();
        await Rejects(() => Invoke(env, method, "someone", false, proof: proof, grant: proof), 400, code, message);
    }

    public static TheoryData<string, string, string, string> PasswordRejected => new()
    {
        { "UT-RCV-06 Reset: mật khẩu mới rỗng", "", "", "Vui lòng nhập mật khẩu mới." },
        { "UT-RCV-07 Reset: xác nhận không khớp", NewPassword, NewPassword + "x", "Xác nhận mật khẩu không đúng định dạng." },
    };

    [Theory, MemberData(nameof(PasswordRejected))]
    public async Task Reset_and_ResetForAccount_reject_invalid_passwords(string caseName, string password, string confirm, string message)
    {
        await using var env = await NewEnv();
        var code = message.StartsWith("Vui") ? "MSG02" : "MSG04";
        await Rejects(() => env.Svc().ResetAsync("someone", "grant-123456", password, confirm, default), 400, code, message);
        await Rejects(() => env.Svc().ResetForAccountAsync(env.Admin, 1, password, confirm, default), 400, code, message);
    }

    // ================= RequestAsync =================
    [Fact]
    public async Task Request_UT_RCV_47_unknown_account_gets_the_neutral_answer_and_nothing_is_sent()
    {
        await using var env = await NewEnv();
        Assert.Equal(NeutralRequest, await env.Svc().RequestAsync($"missing{env.Tag}", "10.0.0.1", default));
        Assert.Empty(env.Delivery.Sent);
    }

    [Fact]
    public async Task Request_UT_RCV_53_stores_a_hashed_token_and_an_audit_row()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        var before = DateTimeOffset.UtcNow;

        await env.Svc().RequestAsync(env.UserName(1), null, default);

        var token = Assert.Single(await env.Tokens(user.Id));
        Assert.Equal(RecoveryProof.Hash(env.Proof), token.TokenHash);
        Assert.NotEqual(env.Proof, token.TokenHash);
        Assert.Equal("", token.ResetGrantHash);
        Assert.Equal(("Email", env.Email(1).ToLowerInvariant(), 0, 0), (token.ChannelType, token.NormalizedDestination, token.ResendCount, token.VerifyAttempts));
        Assert.Null(token.UsedAt);
        Assert.InRange(token.ExpiresAt, before.AddMinutes(15), DateTimeOffset.UtcNow.AddMinutes(15));
        var audit = Assert.Single(await env.Audits(user.Id));
        Assert.Equal((null, "RecoveryRequested", "Yêu cầu đặt lại mật khẩu", "Email", "Succeeded"), (audit.ActorId, audit.Action, audit.Reason, audit.ChannelType, audit.Result));
    }

    [Fact]
    public async Task Request_UT_RCV_57_staff_mode_needs_a_confirmed_email_otherwise_the_request_is_blocked_and_audited()
    {
        await using var env = await NewEnv();
        var user = await env.User(1, type: "Staff", emailOk: false);

        Assert.Equal(NeutralRequest, await env.Svc().RequestAsync(env.UserName(1), null, default));

        Assert.Empty(env.Delivery.Sent);
        var audit = Assert.Single(await env.Audits(user.Id));
        Assert.Equal(("RecoveryBlocked", "Không có kênh liên hệ đủ điều kiện", "Email", "DeliveryFailure"), (audit.Action, audit.Reason, audit.ChannelType, audit.Result));
        Assert.Empty(await env.Tokens(user.Id));
    }

    [Fact]
    public async Task Request_UT_RCV_68_second_request_inside_the_cooldown_is_blocked_and_audited()
    {
        await using var env = await NewEnv(s => s.ResendCooldownMinutes = 2);
        var user = await env.User(1);
        await env.Svc().RequestAsync(env.UserName(1), null, default);
        await env.Svc().RequestAsync(env.UserName(1), null, default);

        Assert.Single(env.Delivery.Sent);
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal(("RecoveryBlocked", "Gửi lại quá sớm", "OverResendLimit"), (audit.Action, audit.Reason, audit.Result));
    }

    [Fact]
    public async Task Request_UT_RCV_72_per_account_rate_limit_silently_drops_extra_requests_regardless_of_case_and_spaces()
    {
        await using var env = await NewEnv(s => s.RateLimitPerAccountPerHour = 2);
        await env.User(1);
        var name = env.UserName(1);
        foreach (var identifier in new[] { name, name.ToUpperInvariant(), $"  {name}  ", name })
            Assert.Equal(NeutralRequest, await env.Svc().RequestAsync(identifier, null, default));
        Assert.Equal(2, env.Delivery.Sent.Count);
    }

    // ================= ResendAsync =================
    private static readonly RecoveryResendOutcome NoResend = new(false, Neutral, null);

    [Fact]
    public async Task Resend_UT_RCV_76_unknown_account_gets_the_neutral_refusal()
    {
        await using var env = await NewEnv();
        Assert.Equal(NoResend, await env.Svc().ResendAsync($"missing{env.Tag}", null, default));
        Assert.Empty(env.Delivery.Sent);
    }

    [Fact]
    public async Task Resend_UT_RCV_78_second_resend_sends_again_and_invalidates_the_first_code()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        await env.Svc().ResendAsync(env.UserName(1), null, default);
        Assert.True((await env.Svc().ResendAsync(env.UserName(1), null, default)).Accepted);
        var tokens = await env.Tokens(user.Id);
        Assert.Equal((2, 1), (tokens.Count, tokens[1].ResendCount));
        Assert.NotNull(tokens[0].UsedAt);
    }

    [Fact]
    public async Task Resend_UT_RCV_80_resend_over_the_cap_is_refused_without_sending()
    {
        await using var env = await NewEnv(s => s.MaxResends = 1);
        await env.User(1);
        foreach (var _ in new[] { 1, 2 }) await env.Svc().ResendAsync(env.UserName(1), null, default);
        Assert.Equal(NoResend, await env.Svc().ResendAsync(env.UserName(1), null, default));
        Assert.Equal(2, env.Delivery.Sent.Count);
    }

    // ================= VerifyAsync =================
    [Fact]
    public async Task Verify_UT_RCV_86_correct_code_issues_a_grant_that_is_stored_hashed()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        await env.Svc().RequestAsync(env.UserName(1), null, default);

        var outcome = await env.Svc().VerifyAsync(env.UserName(1), env.Proof, null, default);

        Assert.True(outcome.Valid);
        Assert.Matches("^[0-9A-F]{64}$", outcome.ResetGrant);
        var token = (await env.Tokens(user.Id)).Single();
        Assert.Equal(RecoveryProof.Hash(outcome.ResetGrant!), token.ResetGrantHash);
        Assert.Equal(1, token.VerifyAttempts);
        Assert.Null(token.UsedAt);
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal((null, "RecoveryVerified", "Xác minh thành công", "Succeeded"), (audit.ActorId, audit.Action, audit.Reason, audit.Result));
    }

    [Fact]
    public async Task Verify_UT_RCV_87_wrong_code_counts_an_attempt_and_is_audited()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        await env.Svc().RequestAsync(env.UserName(1), null, default);

        Assert.Equal(new RecoveryVerifyOutcome(false, null), await env.Svc().VerifyAsync(env.UserName(1), "abcdef", null, default));

        Assert.Equal(1, (await env.Tokens(user.Id)).Single().VerifyAttempts);
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal(("RecoveryFailed", "Mã xác minh không đúng", "InvalidProof"), (audit.Action, audit.Reason, audit.Result));
    }

    [Fact]
    public async Task Verify_UT_RCV_89_expired_code_is_refused_even_when_correct()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        await env.Svc().RequestAsync(env.UserName(1), null, default);
        await env.Mutate(user.Id, u => u.Recoveries.OrderBy(x => x.Id).Last().ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False((await env.Svc().VerifyAsync(env.UserName(1), env.Proof, null, default)).Valid);

        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal(("RecoveryFailed", "Mã xác minh đã hết hạn", "ExpiredProof"), (audit.Action, audit.Reason, audit.Result));
    }

    [Fact]
    public async Task Verify_UT_RCV_90_after_MaxVerifyAttempts_even_the_correct_code_is_refused()
    {
        await using var env = await NewEnv(s => s.MaxVerifyAttempts = 2);
        var user = await env.User(1);
        await env.Svc().RequestAsync(env.UserName(1), null, default);
        foreach (var _ in new[] { 1, 2 }) await env.Svc().VerifyAsync(env.UserName(1), "abcdef", null, default);

        Assert.False((await env.Svc().VerifyAsync(env.UserName(1), env.Proof, null, default)).Valid);

        Assert.Equal(2, (await env.Tokens(user.Id)).Single().VerifyAttempts);     // the refused attempt is not counted
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal(("RecoveryBlocked", "Vượt quá số lần xác minh cho phép", "OverAttemptLimit"), (audit.Action, audit.Reason, audit.Result));
    }

    // ================= ResetAsync =================
    private static readonly RecoveryResetOutcome ResetRefused = new(false, "MSG108");

    [Fact]
    public async Task Reset_UT_RCV_95_reset_with_the_grant_changes_the_password_and_closes_the_request()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        var grant = await env.Grant(user);
        var stamp = (await env.Reload(user.Id)).SecurityStamp;

        var outcome = await env.Svc().ResetAsync(env.UserName(1), grant, NewPassword, NewPassword, default);

        Assert.Equal(new RecoveryResetOutcome(true, "MSG13"), outcome);
        var after = await env.Reload(user.Id);
        Assert.True(await env.Users.CheckPasswordAsync(after, NewPassword));
        Assert.False(await env.Users.CheckPasswordAsync(after, Password));
        Assert.NotEqual(stamp, after.SecurityStamp);
        Assert.NotNull(Assert.Single(after.Recoveries).UsedAt);
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal((null, "PasswordReset", "Đặt lại mật khẩu thành công", "Succeeded"), (audit.ActorId, audit.Action, audit.Reason, audit.Result));
        var accountAudit = await env.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == user.Id && x.Action == "PasswordReset");
        Assert.Equal(user.Id, accountAudit.ActorId);       // self-service: the account is its own actor
        Assert.Contains("passwordReset", accountAudit.AfterJson);
        Assert.DoesNotContain(NewPassword, accountAudit.BeforeJson + accountAudit.AfterJson);
    }

    [Fact]
    public async Task Reset_UT_RCV_96_reset_revokes_every_session_and_refresh_token_but_keeps_the_lockout_state()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        var lockout = DateTimeOffset.UtcNow.AddHours(3);
        var sessionId = Guid.NewGuid();
        await env.Mutate(user.Id, u =>
        {
            u.LockoutEnabled = true; u.LockoutEnd = lockout;
            u.Sessions.Add(new AuthSession { Id = sessionId, AccountId = u.Id, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), LastSeenAt = DateTimeOffset.UtcNow,
                RefreshTokens = [new AuthRefreshToken { Id = 1, AccountId = u.Id, SessionId = sessionId, TokenHash = "h", IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Status = "ACTIVE" }] });
        });
        var grant = await env.Grant(user);

        Assert.True((await env.Svc().ResetAsync(env.UserName(1), grant, NewPassword, NewPassword, default)).Succeeded);

        var after = await env.Reload(user.Id);
        var session = Assert.Single(after.Sessions);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal(("REVOKED", true), (session.RefreshTokens.Single().Status, session.RefreshTokens.Single().RevokedAt != null));
        Assert.Equal(lockout.ToUnixTimeSeconds(), after.LockoutEnd!.Value.ToUnixTimeSeconds());   // a reset never unlocks an account
    }

    [Fact]
    public async Task Reset_UT_RCV_98_wrong_grant_is_refused_and_the_password_is_untouched()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        await env.Grant(user);
        Assert.Equal(ResetRefused, await env.Svc().ResetAsync(env.UserName(1), "0000000000", NewPassword, NewPassword, default));
        Assert.True(await env.Users.CheckPasswordAsync(await env.Reload(user.Id), Password));
    }

    [Fact]
    public async Task Reset_UT_RCV_101_a_password_that_breaks_the_identity_policy_returns_400_MSG109_and_keeps_the_grant_usable()
    {
        await using var env = await NewEnv();
        var user = await env.User(1);
        var grant = await env.Grant(user);

        await Rejects(() => env.Svc().ResetAsync(env.UserName(1), grant, "abc", "abc", default), 400, "MSG109", "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.");

        Assert.Null((await env.Tokens(user.Id)).Single().UsedAt);
        Assert.True(await env.Users.CheckPasswordAsync(await env.Reload(user.Id), Password));
        Assert.True((await env.Svc().ResetAsync(env.UserName(1), grant, NewPassword, NewPassword, default)).Succeeded);
    }

    // ================= Admin-assisted recovery =================
    public static TheoryData<string, string, string> NonAdminCalls() => new()
    {
        { "UT-RCV-104 RequestForAccount: người gọi không phải Admin", "nonAdmin", "RequestForAccount" },
    };

    [Theory, MemberData(nameof(NonAdminCalls))]
    public async Task Admin_methods_reject_callers_that_are_not_active_admins(string caseName, string actor, string method)
    {
        await using var env = await NewEnv();
        var target = await env.User(1);
        var caller = await env.User(2, type: "Staff");
        await env.Mutate(caller.Id, u => u.AssignedRoles = [AccountRoles.ClinicOwner]);
        await Rejects(() => env.Svc().RequestForAccountAsync(caller.Id, target.Id, default), 403, "MSG06", "Bạn không có quyền thực hiện thao tác này.");
        Assert.Empty(env.Delivery.Sent);
    }

    [Fact]
    public async Task Admin_methods_UT_RCV_116_unknown_target_returns_404_MSG01()
    {
        await using var env = await NewEnv();
        const int missing = 2147481000;
        await Rejects(() => env.Svc().RequestForAccountAsync(env.Admin, missing, default), 404, "MSG01", "Không tìm thấy kết quả phù hợp.");
        await Rejects(() => env.Svc().VerifyForAccountAsync(env.Admin, missing, "123456", default), 404, "MSG01");
        await Rejects(() => env.Svc().ResetForAccountAsync(env.Admin, missing, NewPassword, NewPassword, default), 404, "MSG01");
        await Rejects(() => env.Svc().GetStatusAsync(env.Admin, missing, default), 404, "MSG01");
    }

    [Fact]
    public async Task RequestForAccount_UT_RCV_117_sends_a_code_and_reports_the_channel_and_destination()
    {
        await using var env = await NewEnv();
        var user = await env.User(1, type: "Staff");
        var outcome = await env.Svc().RequestForAccountAsync(env.Admin, user.Id, default);
        Assert.Equal(new AdminRecoveryRequestOutcome(true, Neutral, user.Id, "Email", env.Email(1).ToLowerInvariant(), null), outcome);
        Assert.Equal(env.Email(1).ToLowerInvariant(), Assert.Single(env.Delivery.Sent).Destination);
        var audit = Assert.Single(await env.Audits(user.Id));
        Assert.Equal((env.Admin, "RecoveryRequested", "Hỗ trợ khôi phục bởi quản trị viên", "Succeeded"), (audit.ActorId, audit.Action, audit.Reason, audit.Result));
    }

    [Fact]
    public async Task VerifyForAccount_UT_RCV_123_correct_code_returns_a_grant_and_audits_the_admin_as_actor()
    {
        await using var env = await NewEnv();
        var user = await env.User(1, type: "Staff");
        await env.Svc().RequestForAccountAsync(env.Admin, user.Id, default);
        var outcome = await env.Svc().VerifyForAccountAsync(env.Admin, user.Id, env.Proof, default);
        Assert.True(outcome.Valid);
        Assert.Matches("^[0-9A-F]{64}$", outcome.ResetGrant);
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal((env.Admin, "RecoveryVerified"), (audit.ActorId, audit.Action));
    }

    [Fact]
    public async Task ResetForAccount_UT_RCV_127_after_verification_the_admin_sets_the_new_password_and_is_recorded()
    {
        await using var env = await NewEnv();
        var user = await env.User(1, type: "Staff");
        await env.Svc().RequestForAccountAsync(env.Admin, user.Id, default);
        await env.Svc().VerifyForAccountAsync(env.Admin, user.Id, env.Proof, default);

        var outcome = await env.Svc().ResetForAccountAsync(env.Admin, user.Id, NewPassword, NewPassword, default);

        Assert.Equal(new RecoveryResetOutcome(true, "MSG13"), outcome);
        Assert.True(await env.Users.CheckPasswordAsync(await env.Reload(user.Id), NewPassword));
        var audit = (await env.Audits(user.Id)).Last();
        Assert.Equal((env.Admin, "PasswordReset", "Succeeded"), (audit.ActorId, audit.Action, audit.Result));
        Assert.Equal(env.Admin, (await env.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == user.Id && x.Action == "PasswordReset")).ActorId);
        // the token is closed, so a second reset is refused
        await Rejects(() => env.Svc().ResetForAccountAsync(env.Admin, user.Id, "Another9$x", "Another9$x", default), 400, "MSG108");
    }

    // ================= GetStatusAsync =================
    [Fact]
    public async Task GetStatus_UT_RCV_129_account_without_a_request_returns_the_empty_snapshot_with_the_limits()
    {
        await using var env = await NewEnv(s => { s.MaxVerifyAttempts = 4; s.MaxResends = 2; });
        var user = await env.User(1, type: "Staff");
        Assert.Equal(new RecoveryStatusSnapshot(false, "", 0, 0, null, 4, 2, null), await env.Svc().GetStatusAsync(env.Admin, user.Id, default));
    }

    [Fact]
    public async Task GetStatus_UT_RCV_130_active_request_reports_attempts_resends_expiry_and_cooldown()
    {
        await using var env = await NewEnv(s => s.ResendCooldownMinutes = 2);
        var user = await env.User(1, type: "Staff");
        await env.Svc().RequestForAccountAsync(env.Admin, user.Id, default);
        await env.Svc().VerifyForAccountAsync(env.Admin, user.Id, "abcdef", default);

        var status = await env.Svc().GetStatusAsync(env.Admin, user.Id, default);

        Assert.Equal((true, "Email", 1, 0, 5, 3), (status.HasActiveRequest, status.ChannelType, status.AttemptsUsed, status.ResendsUsed, status.MaxAttempts, status.MaxResends));
        Assert.InRange(status.SecondsUntilExpiry!.Value, 890, 900);
        Assert.InRange(status.RetryAfterSeconds!.Value, 1, 120);
    }
}
