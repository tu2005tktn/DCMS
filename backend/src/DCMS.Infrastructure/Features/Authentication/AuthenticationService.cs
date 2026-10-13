using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        // Staff accounts require a staff profile, public accounts never inherit staff privileges.
        if (current == null) DenySignIn();
        var now = DateTimeOffset.UtcNow;
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), AccountId = user!.Id, CreatedAt = now,
            ExpiresAt = now.AddMinutes(settings.Value.SessionMinutes)
        };
        db.AuthSessions.Add(session);
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = user.Id, TargetId = user.Id, Action = "SignIn", Reason = "Đăng nhập",
            OccurredAt = now, AfterJson = "{\"sessionEstablished\":true}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var jwt = new JwtSecurityToken(settings.Value.Issuer, settings.Value.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("sid", session.Id.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            notBefore: now.UtcDateTime, expires: session.ExpiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Value.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), session.ExpiresAt, current!);
    }

    public async Task<CurrentUser?> ValidateSessionAsync(int accountId, Guid sessionId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await db.AuthSessions.AsNoTracking().AnyAsync(x => x.Id == sessionId && x.AccountId == accountId
            && x.RevokedAt == null && x.ExpiresAt > now, ct)) return null;
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
            db.AccountAudits.Add(new AccountAudit
            {
                ActorId = accountId, TargetId = accountId, Action = "SignOut", Reason = "Đăng xuất",
                OccurredAt = now, AfterJson = "{\"sessionRevoked\":true}"
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
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
        // UC-36: only a current VERIFIED self link counts; never inferred from a role or contact. Re-read on every request.
        var verifiedLink = user.AccountType == "Public" && await db.PatientAccountLinks.AsNoTracking()
            .AnyAsync(x => x.AccountId == user.Id && x.Status == PatientLinkStatus.Verified, ct);
        return new(user.Id, user.UserName!, user.FullName, user.AccountType, roles, effective, permissions, verifiedLink);
    }

    private static void DenySignIn() => throw new BusinessException(401, SystemMessages.Get("MSG09"));
}
