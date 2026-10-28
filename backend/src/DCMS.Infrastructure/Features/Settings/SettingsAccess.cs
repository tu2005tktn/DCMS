using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Settings;

public sealed class SettingsAccess(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : ISettingsAccess
{
    public async Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        if (user == null || !user.IsActive || user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow()) throw Error("MSG07",401);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(user));
        if (user.AccountType != "Staff" || !roles.Any(x => AccountRoles.Staff.Contains(x))) return new(false,false,roles,[],[]);
        var owner = roles.Contains(AccountRoles.ClinicOwner);
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (employee == null) throw Error("MSG06",403);
        var branches = owner ? await db.Set<SchedulingBranch>().Where(x => x.Active).Select(x => x.Id).ToArrayAsync(ct)
            : await (from m in db.Set<SchedulingMember>() join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
                     where m.EmployeeId == employee.Id && m.Active && b.Active select b.Id).ToArrayAsync(ct);
        var assignment = await db.Set<StaffAccessAssignment>().AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId,ct);
        string[] permissions;
        if (owner) permissions = SettingsGrants.Definitions.Where(x => SettingsGrants.RoleAllows(x.Key,roles)).Select(x => x.Key).ToArray();
        else if (assignment == null) permissions = SettingsGrants.Legacy(roles);
        else {
            var group = await db.Set<AccessGroup>().AsNoTracking().Include(x => x.Grants).SingleOrDefaultAsync(x => x.Id == assignment.GroupId,ct);
            permissions = group is {Active:true} && roles.Contains(group.BaseRole)
                ? group.Grants.Where(x => SettingsGrants.RoleAllows(x.Permission,roles)).Select(x => x.Permission).ToArray() : [];
            branches = branches.Intersect(assignment.BranchIds).ToArray();
        }
        return new(owner,assignment != null,roles,permissions,branches);
    }
    public async Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct)
    {
        var state = await ResolveAsync(actorId,ct);
        if (!state.Permissions.Contains(permission)) throw Error("MSG06",403);
        return state;
    }
    public async Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct)
    {
        var state = await ResolveAsync(actorId,ct);
        if (!state.Permissions.Contains(permission) || !state.BranchIds.Contains(branchId)) throw Error("MSG06",403);
    }
    public static BusinessException Error(string code, int status = 400, string? field = null) => new(status,SystemMessages.Get(code,field:field ?? "Thông tin"));
}
