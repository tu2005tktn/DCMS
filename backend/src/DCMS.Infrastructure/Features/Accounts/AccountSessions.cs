using DCMS.Domain.Features.Authentication;

namespace DCMS.Infrastructure.Features.Accounts;

internal static class AccountSessions
{
    internal static void Revoke(ApplicationUser user, DateTimeOffset now)
    {
        foreach (var session in user.Sessions) Revoke(session, now);
    }

    internal static void Revoke(AuthSession session, DateTimeOffset now)
    {
        session.RevokedAt ??= now;
        foreach (var token in session.RefreshTokens.Where(x => x.Status == "ACTIVE"))
        {
            token.Status = "REVOKED";
            token.RevokedAt = now;
        }
    }
}
