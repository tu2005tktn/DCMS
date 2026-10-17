using System.Collections.Concurrent;
using DCMS.Application.Common;
using DCMS.Application.Features.Notifications;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Notifications;

/// <summary>
/// Notification service — orchestrate: lưu store + dispatch SignalR (qua IRealtimeNotifier).
///
/// S26+ phase 2: dispatch đơn giản (InApp authoritative; Email/Push chỉ đánh dấu Delivered
/// cho demo, retry 3 lần sau backoff 1/5/15p — phase tiếp theo sẽ tích hợp outbox thật).
/// </summary>
public sealed class NotificationService(
    INotificationStore store,
    IRealtimeNotifier realtime,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);
    private long _seq;

    public Task<ListNotificationsResult> ListAsync(int accountId, ListNotificationsQuery query, CancellationToken ct)
    {
        return ListInternalAsync(accountId, query, ct);
    }

    private async Task<ListNotificationsResult> ListInternalAsync(int accountId, ListNotificationsQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= 200 ? query.Limit.Value : 50;
        var items = await store.ListAsync(accountId, query.UnreadOnly, limit, query.Cursor, ct);
        string? nextCursor = items.Count == limit
            ? items[^1].CreatedAt.ToString("O")
            : null;
        return new ListNotificationsResult(items, nextCursor);
    }

    public async Task<MarkReadResult> MarkReadAsync(int accountId, MarkReadRequest request, CancellationToken ct)
    {
        var marked = new List<string>();
        var skipped = new List<string>();
        foreach (var id in request.NotificationIds)
        {
            var ok = await store.MarkReadAsync(id, accountId, ct);
            if (ok) marked.Add(id); else skipped.Add(id);
        }
        return new MarkReadResult(marked, skipped);
    }

    public async Task<MarkReadResult> MarkReadOneAsync(string id, int expectedAccountId, CancellationToken ct)
    {
        var ok = await store.MarkReadAsync(id, expectedAccountId, ct);
        return ok
            ? new MarkReadResult(new[] { id }, Array.Empty<string>())
            : new MarkReadResult(Array.Empty<string>(), new[] { id });
    }

    public Task<int> CountUnreadAsync(int accountId, CancellationToken ct) => store.CountUnreadAsync(accountId, ct);

    public async Task<CheckDeepLinkResult> CheckDeepLinkAsync(int accountId, string deepLink, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deepLink))
            return new CheckDeepLinkResult(false, "Liên kết không hợp lệ.");
        if (!deepLink.StartsWith("/appointments/", StringComparison.Ordinal))
            return new CheckDeepLinkResult(false, "Loại liên kết chưa hỗ trợ.");

        // Server re-check: tìm notification của account có appointmentId khớp deepLink.
        var list = await store.ListAsync(accountId, null, 200, null, ct);
        var id = deepLink["/appointments/".Length..];
        var match = list.FirstOrDefault(n => string.Equals(n.AppointmentId, id, StringComparison.Ordinal));
        return match is null
            ? new CheckDeepLinkResult(false, "Bạn không có quyền mở thông báo này.")
            : new CheckDeepLinkResult(true, null);
    }

    public async Task<NotificationDto> CreateAsync(
        int? accountId,
        int? branchId,
        NotificationKind kind,
        string title,
        string body,
        string? appointmentId,
        string? requestId,
        string? deepLink,
        IReadOnlyList<NotificationChannel>? channels,
        string dedupeKey,
        CancellationToken ct)
    {
        if (await store.ExistsByDedupeKeyAsync(dedupeKey, ct))
        {
            // Idempotent — trả về bản đã tồn tại.
            var list = await store.ListAsync(accountId, null, 1, null, ct);
            var existing = list.FirstOrDefault(n => string.Equals(n.DedupeKey, dedupeKey, StringComparison.Ordinal));
            if (existing is not null) return existing;
        }

        var id = $"NT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Interlocked.Increment(ref _seq):D6}";
        var effectiveChannels = channels is { Count: > 0 }
            ? channels
            : (IReadOnlyList<NotificationChannel>)new[] { NotificationChannel.InApp, NotificationChannel.Email };

        var now = DateTimeOffset.UtcNow;
        var attempts = effectiveChannels.Select(ch => new NotificationAttempt(
            ch,
            ch == NotificationChannel.InApp ? DispatchStatus.Delivered : DispatchStatus.Pending,
            ch == NotificationChannel.InApp ? 1 : 0,
            ch == NotificationChannel.InApp ? now : null,
            ch == NotificationChannel.InApp ? null : now,
            null)).ToList();

        var dto = new NotificationDto(
            Id: id,
            AccountId: accountId,
            Kind: kind,
            Title: title,
            Body: body,
            CreatedAt: now,
            ReadAt: null,
            AppointmentId: appointmentId,
            RequestId: requestId,
            DeepLink: deepLink,
            Attempts: attempts,
            HasExternalFailure: false,
            DedupeKey: dedupeKey);

        await store.UpsertAsync(dto, ct);

        // Push realtime — Phase 2: tới group account (cho user) HOẶC group staff:branch (cho staff).
        try
        {
            if (accountId.HasValue)
                await realtime.SendToAccountAsync(accountId.Value, "notificationCreated", dto);
            else if (branchId.HasValue)
                await realtime.SendToStaffBranchAsync(branchId.Value, "notificationCreated", dto);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push failed for notification {Id}; client vẫn có thể pull qua list.", id);
        }

        return dto;
    }
}

/// <summary>
/// Abstraction cho realtime push. <see cref="SignalRRealtimeNotifier"/> (phase 2 infra/api) sẽ
/// wrap SignalR IHubContext. Tách interface để unit test được mà không cần SignalR runtime.
/// </summary>
public interface IRealtimeNotifier
{
    Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default);
    Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default);
    Task SendToAllAsync(string method, object payload, CancellationToken ct = default);
}
