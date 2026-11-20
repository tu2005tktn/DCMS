using System.Text.Json;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Settings;

// docs/specs/S58-BRANCHES-AND-CHAIRS.md, "Nhân sự chi nhánh" (BUG-02, SYS7-07 / SYS4-09).
// Members stay in the SCHEDULING_BRANCH JSON (no new table or column); a member is never deleted, only stopped.
public sealed partial class BranchManagementService
{
    public async Task<BranchMembersDto> MembersAsync(int actor, int branchId, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.read", ct);
        var branch = await db.Set<SchedulingBranch>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw Fail("MSG113", 404);
        return await MembersDtoAsync(branch, ct);
    }

    private async Task<BranchMembersDto> MembersDtoAsync(SchedulingBranch branch, CancellationToken ct)
    {
        var staff = await (from employee in db.Employees.AsNoTracking()
                           join account in db.Users.AsNoTracking() on employee.AccountId equals account.Id
                           where account.AccountType == "Staff" && account.IsActive
                           orderby employee.FullName
                           select new { employee.Id, employee.EmployeeCode, employee.FullName, account.UserName, account.AssignedRoles }).ToListAsync(ct);
        var members = branch.Members.ToDictionary(x => x.EmployeeId);
        var rows = staff.Select(x =>
        {
            members.TryGetValue(x.Id, out var m);
            return new BranchMemberDto(x.Id, x.EmployeeCode, x.FullName, x.UserName ?? "", x.AssignedRoles.ToArray(),
                m != null, m?.Active ?? false, m?.CanDentist ?? false, m?.CanManageSchedules ?? false);
        }).OrderByDescending(x => x.IsMember && x.Active).ThenByDescending(x => x.IsMember).ThenBy(x => x.FullName).ToArray();
        return new BranchMembersDto(branch.Id, branch.Version, rows);
    }

    public async Task<BranchMembersDto> SaveMemberAsync(int actor, int branchId, int employeeId, SaveBranchMemberCommand c, CancellationToken ct)
    {
        await access.RequireAsync(actor, "branches.manage", ct);
        if (c.RequestKey == Guid.Empty) throw Fail("MSG04", field: "requestKey");
        var hash = Hash(new { op = "branch.member", branchId, employeeId, c });

        await using var tx = await BeginAsync(ct);
        var replay = await ReplayAsync<BranchMembersDto>(actor, c.RequestKey, hash, ct);
        if (replay.Found) return replay.Result!;

        var branch = await db.Set<SchedulingBranch>().SingleOrDefaultAsync(x => x.Id == branchId, ct) ?? throw Fail("MSG113", 404);
        if (branch.Version != c.ExpectedVersion) throw Fail("MSG111", 409, currentVersion: branch.Version);
        var employee = await (from e in db.Employees.AsNoTracking()
                              join a in db.Users.AsNoTracking() on e.AccountId equals a.Id
                              where e.Id == employeeId && a.AccountType == "Staff"
                              select e).SingleOrDefaultAsync(ct) ?? throw Fail("MSG113", 404, "employeeId");

        var member = branch.Members.SingleOrDefault(x => x.EmployeeId == employeeId);
        var before = member == null ? "{}" : JsonSerializer.Serialize(member, Json);
        if (member == null)
        {
            if (!branch.Active) throw Fail("MSG205", 409, "branchId");
            member = new SchedulingMember { BranchId = branchId, EmployeeId = employeeId };
            branch.Members.Add(member);
        }
        else if (c.Active && !member.Active && !branch.Active) throw Fail("MSG205", 409, "branchId");
        var action = before == "{}" ? "MemberAdd" : "MemberUpdate";
        member.Active = c.Active; member.CanDentist = c.CanDentist; member.CanManageSchedules = c.CanManageSchedules;
        branch.Version += 1;

        var result = await MembersDtoAsync(branch, ct);
        Record(actor, branchId, $"MEMBER:{employee.EmployeeCode}", branch.Version, action,
            c.Active ? "Cập nhật nhân sự chi nhánh" : "Ngừng nhân sự chi nhánh", before, result, c.RequestKey, hash);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
