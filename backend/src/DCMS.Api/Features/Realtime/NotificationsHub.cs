using System.Security.Claims;
using DCMS.Application.Features.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DCMS.Api.Features.Realtime;

/// <summary>
/// SignalR hub cho S26+ realtime notification.
///
/// Client (mobile + web staff) kết nối tới <c>/hubs/notifications</c> với JWT bearer token
/// (qua query string <c>?access_token=...</c> vì SignalR browser client không gửi header tuỳ ý).
/// Mỗi connection tự động join:
///  - group <c>account:{accountId}</c>: cá nhân notification cho user.
///  - group <c>staff:branch:{branchId}</c>: nếu user có role staff + branchId trong token (lấy từ DB).
///
/// Server→client methods:
///  - <c>notificationCreated</c>: payload <c>NotificationDto</c> (đã cache shape cho mobile).
///  - <c>appointmentChanged</c>: payload <c>{ appointmentId, status, version }</c>.
///  - <c>supportRequestUpdated</c>: payload <c>{ requestId, status }</c>.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var accountId = AccountIdOrNull(Context.User);
        if (accountId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"account:{accountId.Value}");

            // Nếu là staff, join thêm group staff:branch:* (một user có thể thuộc nhiều branch).
            var branchIds = await StaffBranchAccessor.GetBranchIdsAsync(accountId.Value, Context);
            foreach (var branchId in branchIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"staff:branch:{branchId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR tự xoá connection khỏi group khi disconnect; override chỉ để log.
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client có thể ping để test connection. Echo connectionId.</summary>
    public Task<string> Ping() => Task.FromResult(Context.ConnectionId);

    private static int? AccountIdOrNull(ClaimsPrincipal? user)
    {
        var sub = user?.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }
}

/// <summary>Helper truy xuất branch của staff — injected qua DI từ Program.</summary>
public static class StaffBranchAccessor
{
    public delegate Task<IReadOnlyList<int>> Resolver(int accountId, HubCallerContext context);

    private static Resolver? _resolver;

    public static void Configure(Resolver resolver) => _resolver = resolver;

    public static Task<IReadOnlyList<int>> GetBranchIdsAsync(int accountId, HubCallerContext context)
    {
        if (_resolver is null) return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        return _resolver(accountId, context);
    }
}
