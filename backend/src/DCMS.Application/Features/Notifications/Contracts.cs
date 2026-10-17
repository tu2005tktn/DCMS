using DCMS.Application.Common;

namespace DCMS.Application.Features.Notifications;

// ============================================================================
// S26 — Notification contracts (TV5 server side)
// ============================================================================

/// <summary>Loại notification — khớp với mobile DTO. KHÔNG chứa chẩn đoán (GBR-081).</summary>
public enum NotificationKind
{
    AppointmentBooked,
    AppointmentRescheduled,
    AppointmentCancelled,
    ReminderUpcoming,
    SupportRequestUpdate,
    NoShow,
    ScheduleReduced,
}

/// <summary>Kênh dispatch — InApp authoritative, external qua adapter.</summary>
public enum NotificationChannel
{
    InApp,
    Email,
    Push,
    Sms,
}

/// <summary>Trạng thái dispatch.</summary>
public enum DispatchStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Retrying,
}

/// <summary>Một lần gửi qua một kênh (tracking retry).</summary>
public sealed record NotificationAttempt(
    NotificationChannel Channel,
    DispatchStatus Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextRetryAt,
    string? ErrorMessage);

/// <summary>Notification DTO trả cho client.</summary>
public sealed record NotificationDto(
    string Id,
    int? AccountId,
    NotificationKind Kind,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string? AppointmentId,
    string? RequestId,
    string? DeepLink,
    IReadOnlyList<NotificationAttempt> Attempts,
    bool HasExternalFailure,
    string DedupeKey);

/// <summary>Query string cho GET /api/notifications.</summary>
public sealed record ListNotificationsQuery(bool? UnreadOnly, int? Limit, string? Cursor);

/// <summary>Kết quả list (cursor-based pagination).</summary>
public sealed record ListNotificationsResult(IReadOnlyList<NotificationDto> Items, string? NextCursor);

/// <summary>Body cho mark-read (1 hoặc nhiều id).</summary>
public sealed record MarkReadRequest(IReadOnlyList<string> NotificationIds, int ExpectedAccountId);

/// <summary>Kết quả mark-read (id nào thành công, id nào skip vì lỗi quyền/timeout).</summary>
public sealed record MarkReadResult(IReadOnlyList<string> Marked, IReadOnlyList<string> Skipped);

/// <summary>Body cho check-deep-link (server re-check quyền).</summary>
public sealed record CheckDeepLinkRequest(string DeepLink);

/// <summary>Kết quả check-deep-link.</summary>
public sealed record CheckDeepLinkResult(bool Allowed, string? Reason);

/// <summary>Body cho test ping (chỉ Development env).</summary>
public sealed record TestRealtimePingRequest(
    int? AccountId,
    int? BranchId,
    NotificationKind Kind,
    string Title,
    string Body,
    string? DeepLink,
    string? DedupeKey);

/// <summary>
/// Port service. UI gọi qua INotificationService; storage do infra triển khai INotificationStore.
/// In-memory store đầu tiên (S26+ phase 2), sau này thay bằng EF Core giữ nguyên interface.
/// </summary>
public interface INotificationService
{
    /// <summary>Lấy notification của account hiện tại, có phân trang.</summary>
    Task<ListNotificationsResult> ListAsync(int accountId, ListNotificationsQuery query, CancellationToken ct);

    /// <summary>Mark 1 hoặc nhiều notification là đã đọc; trả danh sách id đã mark / skip.</summary>
    Task<MarkReadResult> MarkReadAsync(int accountId, MarkReadRequest request, CancellationToken ct);

    /// <summary>Mark 1 notification (tiện cho tap-to-read).</summary>
    Task<MarkReadResult> MarkReadOneAsync(string id, int expectedAccountId, CancellationToken ct);

    /// <summary>Đếm unread (cho badge).</summary>
    Task<int> CountUnreadAsync(int accountId, CancellationToken ct);

    /// <summary>Server re-check quyền trước khi mở deep link (S26).</summary>
    Task<CheckDeepLinkResult> CheckDeepLinkAsync(int accountId, string deepLink, CancellationToken ct);

    /// <summary>Tạo + dispatch notification (gọi từ booking handler hoặc test ping).</summary>
    /// <returns>DTO đã tạo (idempotent theo dedupeKey).</returns>
    Task<NotificationDto> CreateAsync(
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
        CancellationToken ct);
}

/// <summary>Abstraction cho storage — dễ thay bằng EF Core ở phase tiếp theo.</summary>
public interface INotificationStore
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(int? accountId, bool? unreadOnly, int? limit, string? cursor, CancellationToken ct);
    Task<NotificationDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<NotificationDto> UpsertAsync(NotificationDto dto, CancellationToken ct);
    Task<int> CountUnreadAsync(int accountId, CancellationToken ct);
    Task<bool> MarkReadAsync(string id, int expectedAccountId, CancellationToken ct);
    Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct);
    Task<string?> GetCursorForAccountAsync(int accountId, DateTimeOffset since, CancellationToken ct);
}
