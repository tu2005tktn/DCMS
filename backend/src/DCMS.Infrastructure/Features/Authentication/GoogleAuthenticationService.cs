using System.Data;
using System.Security.Cryptography;
using System.Text;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Authentication;

public sealed class GoogleAuthenticationService(
    IGoogleTokenVerifier verifier, IAuthenticationService sessions, DcmsDbContext db,
    UserManager<ApplicationUser> users, RoleManager<IdentityRole<int>> roles) : IGoogleAuthenticationService
{
    private const string Provider = "Google";

    public async Task<SignInResponse> SignInAsync(GoogleSignInRequest request, CancellationToken ct)
    {
        var identity = await verifier.VerifyAsync(request.IdToken, ct);
        var linked = await users.FindByLoginAsync(Provider, identity.Subject);
        if (linked != null)
        {
            if (linked.AccountType != "Public") throw Unavailable();
            return await sessions.CreateSessionAsync(linked.Id, request.DeviceLabel, "GoogleSignIn", ct);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Google subject identifies this account; a matching email never grants access to another account.
        if (!await roles.RoleExistsAsync(AccountRoles.Guest))
            throw new BusinessException(503, SystemMessages.Get("MSG144"));

        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = "g_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.Subject))).ToLowerInvariant(),
            Email = identity.Email, EmailConfirmed = true, FullName = identity.FullName,
            AccountType = "Public", IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        Ensure(await users.CreateAsync(user));
        Ensure(await users.AddToRoleAsync(user, AccountRoles.Guest));
        Ensure(await users.AddLoginAsync(user, new UserLoginInfo(Provider, identity.Subject, Provider)));
        db.AccountAudits.Add(new AccountAudit
        {
            ActorId = user.Id, TargetId = user.Id, Action = "GoogleRegister",
            Reason = "Tạo tài khoản Public qua Google", OccurredAt = now,
            AfterJson = "{\"accountType\":\"Public\",\"role\":\"Guest\"}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await sessions.CreateSessionAsync(user.Id, request.DeviceLabel, "GoogleSignIn", ct);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new BusinessException(409,
            new SystemMessage("GOOGLE_CONFLICT", "Không thể tạo tài khoản Google. Vui lòng thử lại."));
    }

    private static BusinessException Unavailable() => new(403, SystemMessages.Get("MSG06"));
}
