using System.Data;
using System.Security.Cryptography;
using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DCMS.Infrastructure.Features.Authentication;

/// <summary>
/// Shared UC-02 account recovery process used by S03 (self-service) and S04 (Admin-assisted).
/// Self-service responses are always neutral so account existence is never leaked.
/// Passwords, verification codes, reset grants and reset links are never logged or stored.
/// </summary>
public sealed class AccountRecoveryService(
    DcmsDbContext db,
    UserManager<ApplicationUser> users,
    IContactDeliveryAdapter delivery,
    IOptions<RecoverySettings> options,
    IMemoryCache cache,
    ILogger<AccountRecoveryService> logger) : IAccountRecoveryService
{
    private readonly RecoverySettings settings = options.Value;

    public async Task<RecoveryRequestOutcome> RequestAsync(string identifier, string? ipAddress, CancellationToken ct)
    {
        // Neutral answer used for every valid-format request, matching or not an account (GBR/UC-02 step 3).
        var neutral = new RecoveryRequestOutcome(true, "MSG12");
        var invalid = ValidateIdentifier(identifier);
        if (invalid != null) throw invalid;

        // Limits are checked before the lookup so "throttled" cannot be told apart from "unknown account".
        if (!AllowRequest(identifier, ipAddress)) return neutral;

        var user = await FindAsync(identifier, ct);
        if (user == null)
        {
            await EqualizeTimingAsync(ct);
            return neutral;
        }

        try
        {
            var current = await LatestTokenAsync(user.Id, ct);
            var token = await IssueAsync(user, current, null, ct);
            await AuditAsync(null, user.Id, "RecoveryRequested", "Yêu cầu đặt lại mật khẩu",
                ChannelName(token), "Succeeded", ct);
        }
        catch (RecoveryBlockedException blocked)
        {
            await AuditAsync(null, user.Id, "RecoveryBlocked", blocked.Reason,
                settings.Channel.ToString(), blocked.Result, ct);
        }

        return neutral;
    }

    public async Task<RecoveryVerifyOutcome> VerifyAsync(string identifier, string proof, string? ipAddress, CancellationToken ct)
    {
        var invalid = new RecoveryVerifyOutcome(false, null);
        var validation = ValidateIdentifier(identifier) ?? ValidateProof(proof);
        if (validation != null) throw validation;

        var user = await FindAsync(identifier, ct);
        if (user == null) return invalid;

        var current = await LatestTokenAsync(user.Id, ct);
        if (current == null) return invalid;

        // Over the attempt limit the proof is refused outright, exactly like an invalid proof.
        if (current.VerifyAttempts >= settings.MaxVerifyAttempts)
        {
            await AuditAsync(null, user.Id, "RecoveryBlocked", "Vượt quá số lần xác minh cho phép",
                ChannelName(current), "OverAttemptLimit", ct);
            return invalid;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Reload inside the transaction so two concurrent verifies cannot both pass.
        var row = await db.RecoveryTokens.SingleAsync(x => x.Id == current.Id, ct);
        row.VerifyAttempts++;

        if (row.UsedAt != null || row.ExpiresAt <= DateTimeOffset.UtcNow || !Matches(proof, row.TokenHash))
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            var result = row.UsedAt != null ? "UsedProof" : row.ExpiresAt <= DateTimeOffset.UtcNow ? "ExpiredProof" : "InvalidProof";
            await AuditAsync(null, user.Id, "RecoveryFailed", ReasonFor(result), ChannelName(row), result, ct);
            return invalid;
        }

        var grant = GenerateSecret();
        row.ResetGrantHash = Hash(grant);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await AuditAsync(null, user.Id, "RecoveryVerified", "Xác minh thành công", ChannelName(row), "Succeeded", ct);
        logger.LogInformation("Recovery verified for account {AccountId}; grant issued (grant not logged).", user.Id);
        return new RecoveryVerifyOutcome(true, grant);
    }

    public async Task<RecoveryResendOutcome> ResendAsync(string identifier, string? ipAddress, CancellationToken ct)
    {
        var validation = ValidateIdentifier(identifier);
        if (validation != null) throw validation;
        if (!AllowRequest(identifier, ipAddress)) return new RecoveryResendOutcome(false, "MSG12", null);

        var user = await FindAsync(identifier, ct);
        if (user == null)
        {
            await EqualizeTimingAsync(ct);
            return new RecoveryResendOutcome(false, "MSG12", null);
        }

        var current = await LatestTokenAsync(user.Id, ct);
        if (current == null) return new RecoveryResendOutcome(false, "MSG12", null);

        var throttle = ThrottleFor(current);
        if (throttle != null) return throttle;

        try
        {
            var token = await IssueAsync(user, current, null, ct);
            await AuditAsync(null, user.Id, "RecoveryRequested", "Gửi lại hướng dẫn đặt lại mật khẩu",
                ChannelName(token), "Succeeded", ct);
            return new RecoveryResendOutcome(true, "MSG12", null);
        }
        catch (RecoveryBlockedException blocked)
        {
            await AuditAsync(null, user.Id, "RecoveryBlocked", blocked.Reason, ChannelName(current), blocked.Result, ct);
            return new RecoveryResendOutcome(false, "MSG12", null);
        }
    }

    public async Task<RecoveryResetOutcome> ResetAsync(string identifier, string resetGrant, string newPassword,
        string confirmPassword, CancellationToken ct)
    {
        var validation = ValidateIdentifier(identifier) ?? ValidateProof(resetGrant)
            ?? ValidatePassword(newPassword, confirmPassword);
        if (validation != null) throw validation;

        var user = await FindAsync(identifier, ct);
        return await ApplyResetAsync(user, resetGrant, newPassword, null, ct);
    }

    public async Task<RecoveryStatusSnapshot> GetStatusAsync(string identifier, CancellationToken ct)
    {
        var validation = ValidateIdentifier(identifier);
        if (validation != null) throw validation;

        var user = await FindAsync(identifier, ct);
        if (user == null) return NeutralStatus();

        var current = await LatestTokenAsync(user.Id, ct);
        if (current == null) return NeutralStatus();
        return NeutralStatus() with
        {
            HasActiveRequest = true,
            ChannelType = ChannelName(current),
            AttemptsUsed = current.VerifyAttempts,
            ResendsUsed = current.ResendCount,
            SecondsUntilExpiry = SecondsLeft(current),
            RetryAfterSeconds = ThrottleSeconds(current)
        };
    }

    public async Task<AdminRecoveryRequestOutcome> RequestForAccountAsync(int actorId, int targetAccountId, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetAccountId, ct)
            ?? throw new BusinessException(404, SystemMessages.Get("MSG01"));

        var current = await LatestTokenAsync(user.Id, ct);
        var throttle = current == null ? null : ThrottleFor(current);
        if (throttle != null && !throttle.Accepted)
        {
            await AuditAsync(actorId, user.Id, "RecoveryBlocked", "Gửi lại quá sớm", ChannelName(current), "OverResendLimit", ct);
            return new AdminRecoveryRequestOutcome(false, "MSG12", user.Id, ChannelName(current),
                current!.NormalizedDestination, throttle.RetryAfterSeconds);
        }

        try
        {
            var token = await IssueAsync(user, current, actorId, ct);
            await AuditAsync(actorId, user.Id, "RecoveryRequested", "Hỗ trợ khôi phục bởi quản trị viên",
                ChannelName(token), "Succeeded", ct);
            return new AdminRecoveryRequestOutcome(true, "MSG12", user.Id, ChannelName(token),
                token.NormalizedDestination, null);
        }
        catch (RecoveryBlockedException blocked)
        {
            await AuditAsync(actorId, user.Id, "RecoveryBlocked", blocked.Reason,
                ChannelName(current), blocked.Result, ct);
            return new AdminRecoveryRequestOutcome(false, "MSG12", user.Id, ChannelName(current), "", null);
        }
    }

    public async Task<RecoveryVerifyOutcome> VerifyForAccountAsync(int actorId, int targetAccountId, string proof, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetAccountId, ct)
            ?? throw new BusinessException(404, SystemMessages.Get("MSG01"));
        return await VerifyAsync(user.UserName!, proof, null, ct);
    }

    public async Task<RecoveryResetOutcome> ResetForAccountAsync(int actorId, int targetAccountId, string newPassword,
        string confirmPassword, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        var validation = ValidatePassword(newPassword, confirmPassword);
        if (validation != null) throw validation;

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == targetAccountId, ct)
            ?? throw new BusinessException(404, SystemMessages.Get("MSG01"));

        // The Admin path reuses the same process: the account owner must already have completed
        // verification. An Admin may then set the new credential, and the action is audited.
        var current = await LatestTokenAsync(user.Id, ct);
        if (current == null || string.IsNullOrEmpty(current.ResetGrantHash) || current.UsedAt != null)
            throw new BusinessException(400, SystemMessages.Get("MSG108"));

        return await ApplyResetAsync(user, grant: null, newPassword, actorId, ct);
    }

    private async Task<RecoveryResetOutcome> ApplyResetAsync(ApplicationUser? user, string? grant, string newPassword,
        int? adminActor, CancellationToken ct)
    {
        var invalid = new RecoveryResetOutcome(false, "MSG108");
        if (user == null) return invalid;

        var current = await LatestTokenAsync(user.Id, ct);
        if (current == null) return invalid;

        // A grant supplied by the caller must match the stored hash; the Admin path passes null
        // because it has already been proven server-side that verification completed.
        if (grant != null && !Matches(grant, current.ResetGrantHash)) return invalid;

        if (current.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await AuditAsync(adminActor, user.Id, "RecoveryFailed", "Phiên đặt lại đã hết hạn",
                ChannelName(current), "ExpiredProof", ct);
            return invalid;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var row = await db.RecoveryTokens.SingleAsync(x => x.Id == current.Id, ct);
            if (row.UsedAt != null)
            {
                await transaction.RollbackAsync(ct);
                await AuditAsync(adminActor, user.Id, "RecoveryFailed", "Mã đặt lại đã được sử dụng",
                    ChannelName(row), "UsedProof", ct);
                return invalid;
            }

            // The password is only touched after the proof is confirmed, so a wrong or replayed
            // proof can never change the credential.
            var identityToken = await users.GeneratePasswordResetTokenAsync(user);
            var applied = await users.ResetPasswordAsync(user, identityToken, newPassword);
            if (!applied.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                throw new BusinessException(400, SystemMessages.Get("MSG109"));
            }

            var now = DateTimeOffset.UtcNow;
            row.UsedAt = now;
            // Lockout state is deliberately untouched: a reset never unlocks an account (GBR-073).
            await users.UpdateSecurityStampAsync(user);

            // Old sessions are revoked so the new credential cannot sit next to a session issued
            // with the old one (shared S01 session policy).
            await db.AuthSessions.Where(x => x.AccountId == user.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);

            var before = Snapshot(user);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await AuditAsync(adminActor, user.Id, "PasswordReset", "Đặt lại mật khẩu thành công",
                ChannelName(row), "Succeeded", ct, before);
            logger.LogInformation("Password reset completed for account {AccountId}; previous sessions revoked (no secrets logged).", user.Id);
            return new RecoveryResetOutcome(true, "MSG13");
        }
        catch (DbUpdateException exception) when (IsConcurrencyConflict(exception))
        {
            await SafeRollbackAsync(transaction, ct);
            await AuditAsync(adminActor, user.Id, "RecoveryFailed", "Xung đột thao tác đồng thời",
                ChannelName(current), "UsedProof", ct);
            return invalid;
        }
        catch (DbUpdateConcurrencyException)
        {
            await SafeRollbackAsync(transaction, ct);
            await AuditAsync(adminActor, user.Id, "RecoveryFailed", "Xung đột thao tác đồng thời",
                ChannelName(current), "UsedProof", ct);
            return invalid;
        }
    }

    private async Task<RecoveryToken> IssueAsync(ApplicationUser user, RecoveryToken? previous, int? adminActor, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        if (previous != null)
        {
            if (previous.ResendCount >= settings.MaxResends)
                throw new RecoveryBlockedException("Hết số lần gửi lại cho phép", "OverResendLimit");
            if (previous.LastResentAt.HasValue
                && now - previous.LastResentAt.Value < TimeSpan.FromMinutes(settings.ResendCooldownMinutes))
                throw new RecoveryBlockedException("Gửi lại quá sớm", "OverResendLimit");
        }

        if (!await HasVerifiedChannelAsync(user, ct))
            throw new RecoveryBlockedException("Không có kênh liên hệ đã xác minh", "DeliveryFailure");

        var proof = GenerateCode();
        var token = new RecoveryToken
        {
            AccountId = user.Id,
            TokenHash = Hash(proof),
            ResetGrantHash = "",
            ChannelType = ChannelName(),
            NormalizedDestination = await DestinationAsync(user, ct),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(settings.TokenLifetimeMinutes),
            ResendCount = previous?.ResendCount + 1 ?? 0,
            LastResentAt = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        // Issuing a new proof invalidates every older unused proof for this account, so two live
        // codes can never be valid at the same time (GBR-081: stale/concurrent change detection).
        var stale = await db.RecoveryTokens.Where(x => x.AccountId == user.Id && x.UsedAt == null).ToListAsync(ct);
        foreach (var old in stale) old.UsedAt = now;

        db.RecoveryTokens.Add(token);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var link = BuildVerificationLink(token);
        var sent = await delivery.SendRecoveryAsync(user.Id, settings.Channel, token.NormalizedDestination, proof, link, ct);
        if (!sent)
        {
            logger.LogWarning("Recovery delivery failed for account {AccountId} on channel {Channel}.",
                user.Id, token.ChannelType);
            throw new RecoveryBlockedException("Gửi mã xác minh thất bại", "DeliveryFailure");
        }

        return token;
    }

    private async Task<bool> HasVerifiedChannelAsync(ApplicationUser user, CancellationToken ct)
    {
        if (settings.Channel == RecoveryChannel.Phone)
            return !string.IsNullOrEmpty(user.PhoneNumber) && user.PhoneNumberConfirmed;
        return !string.IsNullOrEmpty(user.Email) && user.EmailConfirmed;
    }

    private async Task<string> DestinationAsync(ApplicationUser user, CancellationToken ct)
    {
        var contact = settings.Channel == RecoveryChannel.Phone ? user.PhoneNumber : user.Email;
        return string.IsNullOrEmpty(contact) ? "" : delivery.Normalize(contact);
    }

    private string BuildVerificationLink(RecoveryToken token)
    {
        if (!settings.IsLinkChannel || string.IsNullOrWhiteSpace(settings.VerificationBaseUrl)) return "";
        // The proof is never placed in a URL that can be logged; only the opaque recovery id travels,
        // and the user still types the code they received.
        return $"{settings.VerificationBaseUrl.TrimEnd('/')}/{token.Id}";
    }

    private RecoveryResendOutcome? ThrottleFor(RecoveryToken token)
    {
        var seconds = ThrottleSeconds(token);
        return seconds == null ? null : new RecoveryResendOutcome(false, "MSG12", seconds);
    }

    private int? ThrottleSeconds(RecoveryToken token)
    {
        if (token.ResendCount >= settings.MaxResends) return 0;
        if (!token.LastResentAt.HasValue) return null;
        var elapsed = DateTimeOffset.UtcNow - token.LastResentAt.Value;
        var remaining = TimeSpan.FromMinutes(settings.ResendCooldownMinutes) - elapsed;
        return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : null;
    }

    private static int? SecondsLeft(RecoveryToken token)
    {
        var remaining = token.ExpiresAt - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : 0;
    }

    private RecoveryStatusSnapshot NeutralStatus() => new(false, "", 0, 0, null,
        settings.MaxVerifyAttempts, settings.MaxResends, null);

    private async Task<RecoveryToken?> LatestTokenAsync(int accountId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var tokens = await db.RecoveryTokens.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct);
        // An unexpired unused proof wins; an expired one is still returned so the UI can show the
        // "request again" state instead of pretending nothing was requested.
        return tokens.FirstOrDefault(x => x.UsedAt == null && x.ExpiresAt > now)
            ?? tokens.FirstOrDefault(x => x.UsedAt == null);
    }

    private bool AllowRequest(string identifier, string? ipAddress)
    {
        var window = TimeSpan.FromHours(1);
        if (!Track($"recovery:account:{identifier.Trim().ToUpperInvariant()}", settings.RateLimitPerAccountPerHour, window))
            return false;
        if (string.IsNullOrEmpty(ipAddress)) return true;
        return Track($"recovery:ip:{ipAddress}", settings.RateLimitPerIpPerHour, window);
    }

    private bool Track(string key, int limit, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var hits = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpiration = now.Add(window);
            entry.Size = 1;
            return new List<DateTimeOffset>();
        })!;
        lock (hits)
        {
            hits.RemoveAll(x => now - x >= window);
            if (hits.Count >= limit) return false;
            hits.Add(now);
            return true;
        }
    }

    private async Task EqualizeTimingAsync(CancellationToken ct)
    {
        // Do comparable hashing work so "known account" and "unknown account" answers take a
        // similar amount of time. Nothing is written to the database.
        await Task.CompletedTask;
        var salt = RandomNumberGenerator.GetBytes(16);
        for (var i = 0; i < 3000; i++)
            SHA256.HashData([.. salt, .. BitConverter.GetBytes(i)]);
    }

    private async Task<ApplicationUser?> FindAsync(string identifier, CancellationToken ct)
    {
        var value = identifier.Trim();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUserName == value.ToUpperInvariant(), ct);
        if (user != null) return await db.Users.FirstOrDefaultAsync(x => x.Id == user.Id, ct);

        if (!delivery.IsValid(value)) return null;
        var contact = delivery.Normalize(value).ToUpperInvariant();
        var byContact = await db.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.NormalizedEmail == contact || (x.PhoneNumber != null && x.PhoneNumber.ToUpper() == contact), ct);
        return byContact == null ? null : await db.Users.FirstOrDefaultAsync(x => x.Id == byContact.Id, ct);
    }

    private static string Snapshot(ApplicationUser user) => System.Text.Json.JsonSerializer.Serialize(new
    {
        user.Id,
        user.UserName,
        user.IsActive,
        LockoutEnd = user.LockoutEnd?.UtcDateTime,
        SecurityStampChanged = true
    });

    private static string ReasonFor(string result) => result switch
    {
        "UsedProof" => "Mã xác minh đã được sử dụng",
        "ExpiredProof" => "Mã xác minh đã hết hạn",
        "InvalidProof" => "Mã xác minh không đúng",
        "OverAttemptLimit" => "Vượt quá số lần xác minh cho phép",
        "OverResendLimit" => "Vượt quá số lần gửi lại cho phép",
        "DeliveryFailure" => "Gửi mã xác minh thất bại",
        "Blocked" => "Yêu cầu bị chặn",
        _ => "Không rõ"
    };

    private async Task AuditAsync(int? actorId, int targetId, string action, string reason, string channelType,
        string result, CancellationToken ct, string? before = null)
    {
        db.RecoveryAudits.Add(new RecoveryAudit
        {
            ActorId = actorId,
            TargetId = targetId,
            OccurredAt = DateTimeOffset.UtcNow,
            Action = action,
            Reason = reason,
            ChannelType = channelType,
            Result = result
        });
        if (before != null)
        {
            // The account audit trail keeps actor/time/reason for the credential change itself.
            db.AccountAudits.Add(new AccountAudit
            {
                ActorId = actorId ?? targetId,
                TargetId = targetId,
                OccurredAt = DateTimeOffset.UtcNow,
                Action = action,
                Reason = reason,
                BeforeJson = before,
                AfterJson = "{\"passwordReset\":true}"
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RequireAdminAsync(int actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive, ct);
        if (actor == null || !await users.IsInRoleAsync(actor, AccountRoles.Admin))
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
    }

    private string ChannelName(RecoveryToken? token = null)
    {
        var value = string.IsNullOrEmpty(token?.ChannelType) ? settings.Channel.ToString() : token!.ChannelType;
        return value.Length > 16 ? value[..16] : value;
    }

    private static bool Matches(string submitted, string storedHash) => RecoveryProof.Matches(submitted, storedHash);

    private static string Hash(string value) => RecoveryProof.Hash(value);

    private string GenerateCode() => RecoveryProof.GenerateCode(settings.CodeLength);

    private static string GenerateSecret() => RecoveryProof.GenerateGrant();

    private static bool IsConcurrencyConflict(DbUpdateException exception)
    {
        for (Exception? cause = exception; cause != null; cause = cause.InnerException)
            if (cause is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
                return true;
        return false;
    }

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken ct)
    {
        try { await transaction.RollbackAsync(ct); } catch (Exception) { /* already rolled back */ }
    }

    private static BusinessException? ValidateIdentifier(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return new BusinessException(400, SystemMessages.Get("MSG02", "Tài khoản"));
        var value = identifier.Trim();
        if (value.Length > 64) return new BusinessException(400, SystemMessages.Get("MSG03", "Tài khoản", 64));
        return null;
    }

    private static BusinessException? ValidateProof(string? proof)
    {
        if (string.IsNullOrWhiteSpace(proof))
            return new BusinessException(400, SystemMessages.Get("MSG02", "Mã xác minh"));
        if (proof.Trim().Length is < 4 or > 64)
            return new BusinessException(400, SystemMessages.Get("MSG04", "Mã xác minh"));
        return null;
    }

    private static BusinessException? ValidatePassword(string? password, string? confirm)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new BusinessException(400, SystemMessages.Get("MSG02", "Mật khẩu mới"));
        if (string.IsNullOrWhiteSpace(confirm))
            return new BusinessException(400, SystemMessages.Get("MSG02", "Xác nhận mật khẩu"));
        if (password.Length > 256)
            return new BusinessException(400, SystemMessages.Get("MSG03", "Mật khẩu mới", 256));
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            // No exact MSG exists for a mismatch in v1.2; MSG04 is the closest match [PROPOSED].
            return new BusinessException(400, SystemMessages.Get("MSG04", "Xác nhận mật khẩu"));
        return null;
    }

    private sealed class RecoveryBlockedException(string reason, string result) : Exception(reason)
    {
        public string Reason { get; } = reason;
        public string Result { get; } = result;
    }
}
