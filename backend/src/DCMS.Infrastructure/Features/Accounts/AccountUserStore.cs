using System.Security.Claims;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed class AccountUserStore : UserStore<ApplicationUser, IdentityRole<int>, DcmsDbContext, int>
{
    private DcmsDbContext db => Context;
    public AccountUserStore(DcmsDbContext context, IdentityErrorDescriber errors) : base(context, errors) { }

    public override async Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken ct = default)
    {
        var role = await db.Roles.SingleAsync(x => x.NormalizedName == normalizedRoleName, ct);
        user.AssignedRoles = user.AssignedRoles.Append(role.Name!).Distinct(StringComparer.Ordinal).ToArray();
    }

    public override async Task RemoveFromRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken ct = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, ct);
        if (role != null) user.AssignedRoles = user.AssignedRoles.Where(value => value != role.Name).ToArray();
    }

    public override Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct = default) =>
        Task.FromResult<IList<string>>(user.AssignedRoles.ToArray());

    public override async Task<bool> IsInRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken ct = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, ct);
        return role != null && user.AssignedRoles.Contains(role.Name!, StringComparer.Ordinal);
    }

    public override async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken ct = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, ct);
        return role == null ? [] : await db.Users.Where(x => x.AssignedRoles.Contains(role.Name!)).ToListAsync(ct);
    }

    public override Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken ct = default) =>
        Task.FromResult<IList<Claim>>(user.Claims.Select(x => new Claim(x.Type ?? "", x.Value ?? "")).ToArray());

    public override async Task AddClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken ct = default)
    {
        foreach (var claim in claims)
            user.Claims.Add(new AccountClaim { Id = checked((int)await db.NextAccountEntryIdAsync(ct)), Type = claim.Type, Value = claim.Value });
    }

    public override Task ReplaceClaimAsync(ApplicationUser user, Claim claim, Claim replacement, CancellationToken ct = default)
    {
        foreach (var item in user.Claims.Where(x => x.Type == claim.Type && x.Value == claim.Value))
        {
            item.Type = replacement.Type;
            item.Value = replacement.Value;
        }
        return Task.CompletedTask;
    }

    public override Task RemoveClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken ct = default)
    {
        foreach (var claim in claims) user.Claims.RemoveAll(x => x.Type == claim.Type && x.Value == claim.Value);
        return Task.CompletedTask;
    }

    public override async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct = default)
    {
        var ids = await db.Database.SqlQuery<int>($"""
            SELECT u."Id" AS "Value"
            FROM "USER_ACCOUNT" u
            CROSS JOIN LATERAL jsonb_array_elements(COALESCE(u."AuthData"->'Claims', '[]'::jsonb)) c
            WHERE COALESCE(c->>'Type', '') = {claim.Type} AND COALESCE(c->>'Value', '') = {claim.Value}
            """).ToArrayAsync(ct);
        return ids.Length == 0 ? [] : await db.Users.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
    }

    public override Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken ct = default)
    {
        user.Logins.Add(new AccountLogin { Provider = login.LoginProvider, Key = login.ProviderKey, DisplayName = login.ProviderDisplayName });
        return Task.CompletedTask;
    }

    public override Task RemoveLoginAsync(ApplicationUser user, string provider, string key, CancellationToken ct = default)
    {
        user.Logins.RemoveAll(x => x.Provider == provider && x.Key == key);
        return Task.CompletedTask;
    }

    public override Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken ct = default) =>
        Task.FromResult<IList<UserLoginInfo>>(user.Logins.Select(x => new UserLoginInfo(x.Provider, x.Key, x.DisplayName)).ToArray());

    public override async Task<ApplicationUser?> FindByLoginAsync(string provider, string key, CancellationToken ct = default)
    {
        var id = await db.Database.SqlQuery<int?>($"""
            SELECT u."Id" AS "Value"
            FROM "USER_ACCOUNT" u
            CROSS JOIN LATERAL jsonb_array_elements(COALESCE(u."AuthData"->'Logins', '[]'::jsonb)) l
            WHERE l->>'Provider' = {provider} AND l->>'Key' = {key}
            LIMIT 1
            """).SingleOrDefaultAsync(ct);
        return id.HasValue ? await db.Users.SingleOrDefaultAsync(x => x.Id == id.Value, ct) : null;
    }

    public override Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(x => x.UserName != null && x.UserName.ToUpper() == normalizedUserName, ct);

    public override Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.SingleOrDefaultAsync(x => x.Email != null && x.Email.ToUpper() == normalizedEmail, ct);

    public override Task SetTokenAsync(ApplicationUser user, string provider, string name, string? value, CancellationToken ct = default)
    {
        var token = user.IdentityTokens.SingleOrDefault(x => x.Provider == provider && x.Name == name);
        if (token == null) user.IdentityTokens.Add(new AccountIdentityToken { Provider = provider, Name = name, Value = value });
        else token.Value = value;
        return Task.CompletedTask;
    }

    public override Task<string?> GetTokenAsync(ApplicationUser user, string provider, string name, CancellationToken ct = default) =>
        Task.FromResult(user.IdentityTokens.SingleOrDefault(x => x.Provider == provider && x.Name == name)?.Value);

    public override Task RemoveTokenAsync(ApplicationUser user, string provider, string name, CancellationToken ct = default)
    {
        user.IdentityTokens.RemoveAll(x => x.Provider == provider && x.Name == name);
        return Task.CompletedTask;
    }
}
