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

    public override async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken ct = default) =>
        await db.Users.Where(x => x.Claims.Any(value => value.Type == claim.Type && value.Value == claim.Value)).ToListAsync(ct);

    public override Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken ct = default)
    {
        user.Logins.Add(new AccountLogin { Provider = login.LoginProvider, Key = login.ProviderKey, DisplayName = login.ProviderDisplayName });
        if (login.LoginProvider == "Google") user.GoogleSubject = login.ProviderKey;
        return Task.CompletedTask;
    }

    public override Task RemoveLoginAsync(ApplicationUser user, string provider, string key, CancellationToken ct = default)
    {
        user.Logins.RemoveAll(x => x.Provider == provider && x.Key == key);
        if (provider == "Google") user.GoogleSubject = user.Logins.FirstOrDefault(x => x.Provider == provider)?.Key;
        return Task.CompletedTask;
    }

    public override Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken ct = default) =>
        Task.FromResult<IList<UserLoginInfo>>(user.Logins.Select(x => new UserLoginInfo(x.Provider, x.Key, x.DisplayName)).ToArray());

    public override Task<ApplicationUser?> FindByLoginAsync(string provider, string key, CancellationToken ct = default) =>
        db.Users.SingleOrDefaultAsync(x => x.Logins.Any(value => value.Provider == provider && value.Key == key), ct);

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
