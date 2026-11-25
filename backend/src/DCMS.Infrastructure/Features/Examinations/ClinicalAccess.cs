using DCMS.Application.Common;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ClinicalAccess(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock, DCMS.Application.Features.Settings.ISettingsAccess settingsAccess)
{
    public static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    public async Task<bool> BranchAsync(int actorId, int branchId, bool dentistOnly, CancellationToken ct, string? permission = null)
    {
        await settingsAccess.OperationalAsync(actorId,permission ?? (dentistOnly ? "examinations.manage" : "examinations.read"),branchId,ct);
        var u = await users.FindByIdAsync(actorId.ToString());
        if (u == null || !u.IsActive || u.LockoutEnabled && u.LockoutEnd > clock.GetUtcNow()) throw Error("MSG06", 401);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(u));
        var dentist = roles.Contains(AccountRoles.Dentist);
        var assistant = roles.Contains(AccountRoles.ClinicAssistant);
        // Staff in an access group act through the grant and assignment branch checked above, not their role or membership flags.
        var grantDriven = (await settingsAccess.ResolveAsync(actorId, ct)).GrantDriven;
        if (u.AccountType != "Staff" || !grantDriven && (!dentist && !assistant || dentistOnly && !dentist)) throw Error("MSG05", 403);
        var e = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (e == null || !grantDriven && !await (from m in db.SchedulingMembers join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == e.Id && m.BranchId == branchId && m.Active && b.Active && (dentistOnly ? m.CanDentist : assistant || m.CanDentist)
            select m).AnyAsync(ct)) throw Error("MSG05", 403);
        return dentist && await db.SchedulingMembers.AnyAsync(x => x.EmployeeId == e.Id && x.BranchId == branchId && x.Active && x.CanDentist, ct);
    }
    public async Task<Visit> VisitAsync(int actorId, Guid id, bool dentistOnly, CancellationToken ct, string? permission = null)
    {
        var v = await db.Visits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG42", 404);
        await BranchAsync(actorId, v.BranchId, dentistOnly, ct, permission);
        if (dentistOnly && !await ActsForAsync(actorId, v, ct)) throw Error("MSG05", 403);
        return v;
    }
    // The visit's own dentist, or a non-dentist in an access group who was granted the clinical action for the branch.
    public async Task<bool> ActsForAsync(int actorId, Visit v, CancellationToken ct)
    {
        if (await db.Employees.AnyAsync(x => x.Id == v.DentistId && x.AccountId == actorId, ct)) return true;
        var state = await settingsAccess.ResolveAsync(actorId, ct);
        return state.GrantDriven && !state.Roles.Contains(AccountRoles.Dentist);
    }
    public async Task<int[]> BranchesAsync(int actorId, CancellationToken ct)
    {
        var ids = await (from m in db.SchedulingMembers join e in db.Employees on m.EmployeeId equals e.Id
            where e.AccountId == actorId && m.Active select m.BranchId).ToArrayAsync(ct);
        var allowed = new List<int>();
        foreach (var id in ids) { try { await BranchAsync(actorId, id, false, ct); allowed.Add(id); } catch (BusinessException ex) when (ex.Status == 403) { } }
        if (allowed.Count == 0) throw Error("MSG05", 403);
        return allowed.ToArray();
    }
}
