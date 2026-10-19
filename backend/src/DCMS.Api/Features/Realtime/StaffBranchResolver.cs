using DCMS.Application.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Realtime;

/// <summary>
/// Staff branch resolver — truy vấn membership đang hoạt động để hub chỉ join đúng
/// các group chi nhánh mà account staff hiện được phân công.
/// </summary>
internal static class StaffBranchResolver
{
    public static async Task<IReadOnlyList<int>> ResolveAsync(int accountId, HubCallerContext context, IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == accountId);
            if (user is null || !user.IsActive || user.AccountType != "Staff") return Array.Empty<int>();
            return await (from member in db.Set<SchedulingMember>().AsNoTracking()
                          join employee in db.Employees.AsNoTracking() on member.EmployeeId equals employee.Id
                          join branch in db.Set<SchedulingBranch>().AsNoTracking() on member.BranchId equals branch.Id
                          where employee.AccountId == accountId && member.Active && branch.Active
                          select member.BranchId).Distinct().ToArrayAsync(context.ConnectionAborted);
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}
