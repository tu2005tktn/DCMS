using System.Globalization;
using System.Security.Claims;
using DCMS.Domain.Features.Authentication;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Accounts;

public sealed class AccountRoleStore(DcmsDbContext db, IdentityErrorDescriber errors)
    : IQueryableRoleStore<IdentityRole<int>>, IRoleClaimStore<IdentityRole<int>>
{
    public IQueryable<IdentityRole<int>> Roles => db.Roles;
    private IQueryable<AccessGroup> Records => db.Set<AccessGroup>().IgnoreQueryFilters().Where(x => x.RecordKind == "IdentityRole");

    public async Task<IdentityResult> CreateAsync(IdentityRole<int> role, CancellationToken ct)
    {
        role.Id = checked((int)await db.NextAccountEntryIdAsync(ct));
        role.ConcurrencyStamp = "1";
        db.Add(new AccessGroup { RecordKind = "IdentityRole", IdentityRoleId = role.Id, Code = role.Name!,
            Name = role.Name!, BaseRole = role.Name!, Version = 1 });
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(IdentityRole<int> role, CancellationToken ct)
    {
        var row = await Records.SingleAsync(x => x.IdentityRoleId == role.Id, ct);
        if (row.Version.ToString(CultureInfo.InvariantCulture) != role.ConcurrencyStamp) return IdentityResult.Failed(errors.ConcurrencyFailure());
        var oldName = row.Name;
        row.Name = row.Code = row.BaseRole = role.Name!;
        row.Version++;
        role.ConcurrencyStamp = row.Version.ToString(CultureInfo.InvariantCulture);
        foreach (var user in await db.Users.Where(x => x.AssignedRoles.Contains(oldName)).ToArrayAsync(ct))
            user.AssignedRoles = user.AssignedRoles.Select(value => value == oldName ? role.Name! : value).ToArray();
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(IdentityRole<int> role, CancellationToken ct)
    {
        var row = await Records.SingleAsync(x => x.IdentityRoleId == role.Id, ct);
        foreach (var user in await db.Users.Where(x => x.AssignedRoles.Contains(row.Name)).ToArrayAsync(ct))
            user.AssignedRoles = user.AssignedRoles.Where(value => value != row.Name).ToArray();
        db.Remove(row);
        await db.SaveChangesAsync(ct);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(IdentityRole<int> role, CancellationToken ct) => Task.FromResult(role.Id.ToString(CultureInfo.InvariantCulture));
    public Task<string?> GetRoleNameAsync(IdentityRole<int> role, CancellationToken ct) => Task.FromResult(role.Name);
    public Task SetRoleNameAsync(IdentityRole<int> role, string? name, CancellationToken ct) { role.Name = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<int> role, CancellationToken ct) => Task.FromResult(role.NormalizedName);
    public Task SetNormalizedRoleNameAsync(IdentityRole<int> role, string? name, CancellationToken ct) { role.NormalizedName = name; return Task.CompletedTask; }
    public Task<IdentityRole<int>?> FindByIdAsync(string id, CancellationToken ct) =>
        int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? Roles.SingleOrDefaultAsync(x => x.Id == number, ct) : Task.FromResult<IdentityRole<int>?>(null);
    public Task<IdentityRole<int>?> FindByNameAsync(string name, CancellationToken ct) => Roles.SingleOrDefaultAsync(x => x.NormalizedName == name, ct);

    public async Task<IList<Claim>> GetClaimsAsync(IdentityRole<int> role, CancellationToken ct = default)
    {
        var row = await Records.SingleAsync(x => x.IdentityRoleId == role.Id, ct);
        return row.Grants.Select(x => new Claim(x.Permission, x.ClaimValue ?? "")).ToArray();
    }

    public async Task AddClaimAsync(IdentityRole<int> role, Claim claim, CancellationToken ct = default)
    {
        var row = await Records.SingleAsync(x => x.IdentityRoleId == role.Id, ct);
        row.Grants.Add(new AccessGroupGrant { GroupId = row.Id, Permission = claim.Type,
            ClaimId = checked((int)await db.NextAccountEntryIdAsync(ct)), ClaimValue = claim.Value });
    }

    public async Task RemoveClaimAsync(IdentityRole<int> role, Claim claim, CancellationToken ct = default)
    {
        var row = await Records.SingleAsync(x => x.IdentityRoleId == role.Id, ct);
        row.Grants.RemoveAll(x => x.Permission == claim.Type && (x.ClaimValue ?? "") == claim.Value);
    }

    public void Dispose() { }
}
