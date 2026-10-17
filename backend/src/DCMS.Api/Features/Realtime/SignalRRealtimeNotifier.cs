using DCMS.Infrastructure.Features.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace DCMS.Api.Features.Realtime;

/// <summary>
/// SignalR implementation của <see cref="IRealtimeNotifier"/>.
/// Inject <see cref="IHubContext{NotificationsHub}"/> rồi forward tới groups tương ứng.
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hub) : IRealtimeNotifier
{
    public Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default)
        => hub.Clients.Group($"account:{accountId}").SendAsync(method, payload, ct);

    public Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default)
        => hub.Clients.Group($"staff:branch:{branchId}").SendAsync(method, payload, ct);

    public Task SendToAllAsync(string method, object payload, CancellationToken ct = default)
        => hub.Clients.All.SendAsync(method, payload, ct);
}
