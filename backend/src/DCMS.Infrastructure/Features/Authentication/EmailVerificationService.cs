using System.Data;
using System.Security.Cryptography;
using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DCMS.Infrastructure.Features.Authentication;

// Email ownership is established after S02 registration. The account and destination always come
// from the authenticated session; this workflow never establishes Patient identity.
public sealed class EmailVerificationService(
    DcmsDbContext db,
    IEmailVerificationDelivery delivery,
    IOptions<RecoverySettings> recovery,
    IOptions<JwtSettings> jwt) : IEmailVerificationService
{
    private readonly RecoverySettings settings = recovery.Value;
    private readonly byte[] hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(jwt.Value.SigningKey + ":email-verification"));

    public async Task<EmailVerificationStatus> GetStatusAsync(int accountId, CancellationToken ct)
    {
        var user = await PublicUserAsync(accountId, ct);
        return new EmailVerificationStatus(user.Email ?? "", user.EmailConfirmed, settings.CodeLength);
    }

    public async Task<EmailVerificationRequestResult> RequestAsync(int accountId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAccountAsync(accountId, ct);
        var user = await PublicUserAsync(accountId, ct);
        if (user.EmailConfirmed) return new("Email của bạn đã được xác minh.", 0);
        if (string.IsNullOrWhiteSpace(user.Email) || !string.Equals(
                RecoveryContactFormat.Normalize(user.Email), user.NormalizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(400, SystemMessages.Get("MSG04", "Email"));

        var now = DateTimeOffset.UtcNow;
        var hourlyCount = await db.EmailVerificationTokens.CountAsync(x =>
            x.AccountId == accountId && x.CreatedAt >= now.AddHours(-1), ct);
        if (hourlyCount >= settings.RateLimitPerAccountPerHour)
            return new("Đã đạt giới hạn gửi mã. Vui lòng thử lại sau.", 3600);

        var latest = await db.EmailVerificationTokens.Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (latest is { UsedAt: null } && latest.ExpiresAt > now)
        {
            var remaining = TimeSpan.FromMinutes(settings.ResendCooldownMinutes) - (now - latest.LastSentAt);
            if (remaining > TimeSpan.Zero)
                return new("Vui lòng đợi trước khi gửi lại mã.", (int)Math.Ceiling(remaining.TotalSeconds));
            if (latest.ResendCount >= settings.MaxResends)
                return new("Đã hết lượt gửi lại mã. Vui lòng thử lại sau.",
                    (int)Math.Ceiling((latest.ExpiresAt - now).TotalSeconds));
        }

        var code = RecoveryProof.GenerateCode(settings.CodeLength);
        // SMTP must accept the message before a code can be activated. Missing SMTP returns a
        // service error and cannot be mistaken for a successful send.
        if (!await delivery.SendAsync(user.Email!, code, settings.TokenLifetimeMinutes, ct))
            throw new BusinessException(503, SystemMessages.Get("MSG144"));

        if (latest is { UsedAt: null }) latest.UsedAt = now;
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            AccountId = accountId,
            NormalizedEmail = user.NormalizedEmail!,
            CodeHash = Hash(code),
            CreatedAt = now,
            LastSentAt = now,
            ExpiresAt = now.AddMinutes(settings.TokenLifetimeMinutes),
            ResendCount = latest is { ExpiresAt: var expiry } && expiry > now ? latest.ResendCount + 1 : 0
        });
        AddAudit(accountId, "EmailVerificationRequested", "Succeeded", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new("Mã xác minh đã được gửi tới email của bạn.", settings.ResendCooldownMinutes * 60);
    }

    public async Task<EmailVerificationStatus> ConfirmAsync(int accountId, string code, CancellationToken ct)
    {
        if (!RecoveryProof.IsWellFormedCode(code, settings.CodeLength))
            throw new BusinessException(400, SystemMessages.Get("MSG108"));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockAccountAsync(accountId, ct);
        var user = await PublicUserAsync(accountId, ct);
        if (user.EmailConfirmed)
            throw new BusinessException(400, SystemMessages.Get("MSG108"));
        var now = DateTimeOffset.UtcNow;
        var token = await db.EmailVerificationTokens.Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (token == null || token.UsedAt != null || token.ExpiresAt <= now ||
            token.VerifyAttempts >= settings.MaxVerifyAttempts ||
            !string.Equals(token.NormalizedEmail, user.NormalizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new BusinessException(400, SystemMessages.Get("MSG108"));

        if (!Matches(code, token.CodeHash))
        {
            token.VerifyAttempts++;
            if (token.VerifyAttempts >= settings.MaxVerifyAttempts) token.UsedAt = now;
            AddAudit(accountId, "EmailVerificationFailed", "InvalidProof", now);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new BusinessException(400, SystemMessages.Get("MSG108"));
        }

        token.UsedAt = now;
        user.EmailConfirmed = true;
        AddAudit(accountId, "EmailVerificationConfirmed", "Succeeded", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(user.Email ?? "", true, settings.CodeLength);
    }

    private async Task<ApplicationUser> PublicUserAsync(int accountId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == accountId, ct);
        if (user == null || user.AccountType != "Public")
            throw new BusinessException(403, SystemMessages.Get("MSG06"));
        return user;
    }

    private async Task LockAccountAsync(int accountId, CancellationToken ct)
    {
        _ = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"USER_ACCOUNT\" WHERE \"Id\" = {accountId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
    }

    private void AddAudit(int accountId, string action, string result, DateTimeOffset now) =>
        db.RecoveryAudits.Add(new RecoveryAudit
        {
            ActorId = accountId, TargetId = accountId, OccurredAt = now, Action = action,
            Reason = "", ChannelType = "Email", Result = result
        });

    private string Hash(string code) => Convert.ToHexString(HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(code)));

    private bool Matches(string code, string stored) => CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(Hash(code)), Convert.FromHexString(stored));
}
