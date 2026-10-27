using DCMS.Application.Common;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Examinations;

public sealed class ClinicalAccess(DcmsDbContext db, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public static BusinessException Error(string code, int status = 400) => new(status, SystemMessages.Get(code));
    public async Task<bool> BranchAsync(int actorId, int branchId, bool dentistOnly, CancellationToken ct)
    {
        var u = await users.FindByIdAsync(actorId.ToString());
        if (u == null || !u.IsActive || u.LockoutEnabled && u.LockoutEnd > clock.GetUtcNow()) throw Error("MSG07", 401);
        var roles = AccountRoles.Effective(await users.GetRolesAsync(u));
        var dentist = roles.Contains(AccountRoles.Dentist);
        var assistant = roles.Contains(AccountRoles.ClinicAssistant);
        if (u.AccountType != "Staff" || !dentist && !assistant || dentistOnly && !dentist) throw Error("MSG06", 403);
        var e = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorId, ct);
        if (e == null || !await (from m in db.Set<SchedulingMember>() join b in db.Set<SchedulingBranch>() on m.BranchId equals b.Id
            where m.EmployeeId == e.Id && m.BranchId == branchId && m.Active && b.Active && (dentistOnly ? m.CanDentist : assistant || m.CanDentist)
            select m).AnyAsync(ct)) throw Error("MSG06", 403);
        return dentist && await db.Set<SchedulingMember>().AnyAsync(x => x.EmployeeId == e.Id && x.BranchId == branchId && x.Active && x.CanDentist, ct);
    }
    public async Task<Visit> VisitAsync(int actorId, Guid id, bool dentistOnly, CancellationToken ct)
    {
        var v = await db.Visits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("MSG113", 404);
        await BranchAsync(actorId, v.BranchId, dentistOnly, ct);
        if (dentistOnly && !await db.Employees.AnyAsync(x => x.Id == v.DentistId && x.AccountId == actorId, ct)) throw Error("MSG06", 403);
        return v;
    }
    public async Task<int[]> BranchesAsync(int actorId, CancellationToken ct)
    {
        var ids = await (from m in db.Set<SchedulingMember>() join e in db.Employees on m.EmployeeId equals e.Id
            where e.AccountId == actorId && m.Active select m.BranchId).ToArrayAsync(ct);
        var allowed = new List<int>();
        foreach (var id in ids) { try { await BranchAsync(actorId, id, false, ct); allowed.Add(id); } catch (BusinessException ex) when (ex.Status == 403) { } }
        if (allowed.Count == 0) throw Error("MSG06", 403);
        return allowed.ToArray();
    }
}
