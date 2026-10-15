using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed class AuthenticationService(DcmsDbContext db, UserManager<ApplicationUser> users,
    IOptions<JwtSettings> settings) : IAuthenticationService
{
    public async Task<SignInResponse> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 64
            || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256) DenySignIn();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user == null || !user.IsActive || await users.IsLockedOutAsync(user)
            || !await users.CheckPasswordAsync(user, request.Password)) DenySignIn();
        var current = await CurrentAsync(user!, ct);
        if (current == null) DenySignIn();
        var now = DateTimeOffset.UtcNow;
        var accessExpires = now.AddMinutes(settings.Value.AccessTokenMinutes);
        var refreshExpires = now.AddDays(settings.Value.RefreshTokenDays);
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), AccountId = user!.Id,
            DeviceLabel = NormalizeDeviceLabel(request.DeviceLabel),
            CreatedAt = now, LastSeenAt = now, ExpiresAt = accessExpires
        };
        var (refresh, refreshPlaintext) = CreateRefreshToken(session.AccountId, session.Id, refreshExpires);
        db.AuthSessions.Add(session);
        db.AuthRefreshTokens.Add(refresh);
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = user.Id, TargetId = user.Id, Action = "SignIn", Reason = "Đăng nhập",
            OccurredAt = now, AfterJson = "{\"sessionEstablished\":true,\"sessionId\":\"" + session.Id + "\"}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var access = IssueAccessToken(user.Id, session.Id, now, accessExpires);
        return new(access, accessExpires, current!, refreshPlaintext, session.Id);
    }

    public async Task<CurrentUser?> ValidateSessionAsync(int accountId, Guid sessionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var session = await db.AuthSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.AccountId == accountId, ct);
        if (session == null || session.RevokedAt != null || session.ExpiresAt <= now) return null;
        // Last-seen cập nhật best-effort, không chặn request khi lỗi.
        await db.AuthSessions.Where(x => x.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), ct).ConfigureAwait(false);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, ct);
        if (user == null || !user.IsActive || await users.IsLockedOutAsync(user)) return null;
        return await CurrentAsync(user, ct);
    }

    public async Task SignOutAsync(int accountId, Guid sessionId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var changed = await db.AuthSessions.Where(x => x.Id == sessionId && x.AccountId == accountId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        if (changed > 0)
        {
            // Thu hồi toàn bộ refresh của session này để tránh refresh sau khi đã sign-out.
            await db.AuthRefreshTokens.Where(x => x.SessionId == sessionId && x.Status == "ACTIVE")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVOKED").SetProperty(x => x.RevokedAt, now), ct);
            db.AccountAudits.Add(new AccountAudit
            {
                ActorId = accountId, TargetId = accountId, Action = "SignOut", Reason = "Đăng xuất",
                OccurredAt = now, AfterJson = "{\"sessionRevoked\":true,\"sessionId\":\"" + sessionId + "\"}"
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<RefreshResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) throw new BusinessException(401, SystemMessages.Get("MSG07"));
        var incomingHash = HashRefreshToken(request.RefreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTimeOffset.UtcNow;
        var existing = await db.AuthRefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == incomingHash, ct);
        if (existing == null)
        {
            // Không tìm thấy — token sai hoặc đã bị xóa. Trả 401, KHÔNG thu hồi chain.
            throw new BusinessException(401, SystemMessages.Get("MSG07"));
        }
        // Tài khoản và session còn hiệu lực?
        var session = await db.AuthSessions.SingleOrDefaultAsync(x => x.Id == existing.SessionId, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == existing.AccountId, ct);
        if (session == null || session.RevokedAt != null || session.ExpiresAt <= now
            || user == null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            await RevokeChainAsync(existing, "ServerInvalidated", now, ct);
            await transaction.CommitAsync(ct);
            throw new BusinessException(401, SystemMessages.Get("MSG07"));
        }
        // Reuse detection: token ACTIVE → xử lý bình thường. REPLACED/REVOKED → thu hồi chain.
        if (existing.Status == "REPLACED" || existing.Status == "REVOKED")
        {
            await RevokeChainAsync(existing, "ReuseDetected", now, ct);
            await transaction.CommitAsync(ct);
            throw new BusinessException(401, SystemMessages.Get("MSG07"));
        }
        // Rotation: cấp access mới, cấp refresh mới, đánh dấu token cũ REPLACED.
        var accessExpires = now.AddMinutes(settings.Value.AccessTokenMinutes);
        var refreshExpires = now.AddDays(settings.Value.RefreshTokenDays);
        var (newRefresh, newRefreshPlaintext) = CreateRefreshToken(existing.AccountId, existing.SessionId, refreshExpires);
        db.AuthRefreshTokens.Add(newRefresh);
        existing.Status = "REPLACED";
        existing.ReplacedById = newRefresh.Id;
        session.LastSeenAt = now;
        session.ExpiresAt = accessExpires;
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = existing.AccountId, TargetId = existing.AccountId, Action = "Refresh", Reason = "Refresh token rotation",
            OccurredAt = now, AfterJson = "{\"sessionId\":\"" + existing.SessionId + "\"}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var access = IssueAccessToken(existing.AccountId, existing.SessionId, now, accessExpires);
        return new(access, accessExpires, newRefreshPlaintext, existing.SessionId);
    }

    public async Task SignOutAllAsync(int accountId, Guid currentSessionId, string? reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTimeOffset.UtcNow;
        await db.AuthSessions.Where(x => x.AccountId == accountId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        await db.AuthRefreshTokens.Where(x => x.AccountId == accountId && x.Status == "ACTIVE")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVOKED").SetProperty(x => x.RevokedAt, now), ct);
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = accountId, TargetId = accountId, Action = "SignOutAll",
            Reason = string.IsNullOrWhiteSpace(reason) ? "Đăng xuất khỏi mọi thiết bị" : reason.Trim(),
            OccurredAt = now, AfterJson = "{\"allSessionsRevoked\":true}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(int accountId, Guid currentSessionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.AuthSessions.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.RevokedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new SessionInfo(x.Id, x.DeviceLabel, x.CreatedAt, x.LastSeenAt, x.ExpiresAt,
                x.Id == currentSessionId))
            .ToListAsync(ct);
    }

    private async Task<CurrentUser?> CurrentAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToArray();
        if (roles.Length == 0) return null;
        if (user.AccountType == "Staff")
        {
            if (roles.Any(x => !AccountRoles.Staff.Contains(x))
                || !await db.Employees.AsNoTracking().AnyAsync(x => x.AccountId == user.Id, ct)) return null;
        }
        else if (user.AccountType != "Public" || roles.Any(x => x != AccountRoles.Guest && x != AccountRoles.Patient)) return null;
        var effective = AccountRoles.Effective(roles);
        string[] permissions = [
            .. effective.Contains(AccountRoles.Admin) ? ["accounts.manage"] : Array.Empty<string>(),
            .. effective.Contains(AccountRoles.ClinicAssistant) ? ["patient-links.verify"] : Array.Empty<string>()
        ];
        // UC-36: re-read the verified link on every request; roles and contacts alone do not establish a link.
        var verifiedLink = user.AccountType == "Public" && await db.PatientAccountLinks.AsNoTracking()
            .AnyAsync(x => x.AccountId == user.Id && x.Status == PatientLinkStatus.Verified, ct);
        return new(user.Id, user.UserName!, user.FullName, user.AccountType, roles, effective, permissions, verifiedLink);
    }

    private string IssueAccessToken(int accountId, Guid sessionId, DateTimeOffset now, DateTimeOffset expires)
    {
        var jwt = new JwtSecurityToken(settings.Value.Issuer, settings.Value.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()), new Claim("sid", sessionId.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: now.UtcDateTime, expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Value.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    /// <summary>
    /// Tạo refresh token mới: 32 byte random từ CSPRNG, base64url encoding.
    /// Lưu SHA-256 hash hex vào DB. Plaintext trả về client MỘT LẦN.
    /// </summary>
    private static (AuthRefreshToken Entity, string Plaintext) CreateRefreshToken(
        int accountId, Guid sessionId, DateTimeOffset expires)
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var plaintext = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var entity = new AuthRefreshToken
        {
            AccountId = accountId, SessionId = sessionId,
            TokenHash = HashRefreshToken(plaintext),
            IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = expires, Status = "ACTIVE"
        };
        return (entity, plaintext);
    }

    private static string HashRefreshToken(string plaintext)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task RevokeChainAsync(AuthRefreshToken existing, string reason, DateTimeOffset now, CancellationToken ct)
    {
        // Thu hồi toàn bộ session cùng account (nếu chain reuse) — phòng token bị đánh cắp.
        await db.AuthSessions.Where(x => x.AccountId == existing.AccountId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        await db.AuthRefreshTokens.Where(x => x.AccountId == existing.AccountId && x.Status == "ACTIVE")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "REVOKED").SetProperty(x => x.RevokedAt, now), ct);
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = existing.AccountId, TargetId = existing.AccountId,
            Action = "RefreshReuse", Reason = reason, OccurredAt = now,
            AfterJson = "{\"reuseDetected\":true,\"sessionId\":\"" + existing.SessionId + "\"}"
        });
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeDeviceLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Unknown device";
        var trimmed = raw.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..120];
    }

    private static void DenySignIn() => throw new BusinessException(401, SystemMessages.Get("MSG09"));
}
