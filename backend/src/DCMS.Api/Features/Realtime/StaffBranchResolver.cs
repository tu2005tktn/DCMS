using DCMS.Application.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Api.Features.Realtime;

/// <summary>
/// Staff branch resolver — truy vấn DB lấy branchId của user staff để hub join group.
/// Phase 2: EmployeeProfile chưa có BranchId; trả placeholder {AccountId} để staff nhận
/// event test ping. Phase tiếp theo sẽ thêm schema BranchId và sửa lại.
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
            if (user is null || user.AccountType != "Staff") return Array.Empty<int>();
            return new[] { accountId };
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}
