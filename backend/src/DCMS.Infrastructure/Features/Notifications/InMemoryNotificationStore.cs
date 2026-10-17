using System.Collections.Concurrent;
using DCMS.Application.Features.Notifications;

namespace DCMS.Infrastructure.Features.Notifications;

/// <summary>
/// In-memory notification store cho S26+ phase 2.
///
/// Tại sao in-memory đầu tiên:
///  - Cần unblock realtime demo (SignalR) mà chưa phải tạo EF Core entity + migration.
///  - Interface <see cref="INotificationStore"/> đã tách rời: phase tiếp theo chỉ cần
///    viết <c>EfNotificationStore</c> + đăng ký DI, không phá chỗ nào khác.
///
/// Đặc tính:
///  - Thread-safe qua <see cref="ConcurrentDictionary{TKey,TValue}"/>.
///  - Idempotent theo <c>DedupeKey</c> (booking commit lặp không tạo trùng).
///  - Cursor pagination: cursor = ISO-8601 của CreatedAt của item cuối trang trước.
/// </summary>
public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly ConcurrentDictionary<string, NotificationDto> _byId = new();
    private readonly ConcurrentDictionary<string, byte> _dedupeKeys = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<NotificationDto>> ListAsync(int? accountId, bool? unreadOnly, int? limit, string? cursor, CancellationToken ct)
    {
        IEnumerable<NotificationDto> q = _byId.Values;
        if (accountId.HasValue) q = q.Where(n => n.AccountId == accountId.Value);
        if (unreadOnly == true) q = q.Where(n => n.ReadAt is null);
        if (!string.IsNullOrWhiteSpace(cursor) && DateTimeOffset.TryParse(cursor, out var since))
            q = q.Where(n => n.CreatedAt < since);

        var items = q
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id, StringComparer.Ordinal)
            .Take(limit.GetValueOrDefault(50))
            .ToList();
        return Task.FromResult<IReadOnlyList<NotificationDto>>(items);
    }

    public Task<NotificationDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        _byId.TryGetValue(id, out var dto);
        return Task.FromResult<NotificationDto?>(dto);
    }

    public Task<NotificationDto> UpsertAsync(NotificationDto dto, CancellationToken ct)
    {
        _byId.AddOrUpdate(dto.Id, dto, (_, _) => dto);
        if (!string.IsNullOrWhiteSpace(dto.DedupeKey))
            _dedupeKeys.TryAdd(dto.DedupeKey, 0);
        return Task.FromResult(dto);
    }

    public Task<int> CountUnreadAsync(int accountId, CancellationToken ct)
    {
        var c = _byId.Values.Count(n => n.AccountId == accountId && n.ReadAt is null);
        return Task.FromResult(c);
    }

    public Task<bool> MarkReadAsync(string id, int expectedAccountId, CancellationToken ct)
    {
        if (!_byId.TryGetValue(id, out var dto)) return Task.FromResult(false);
        if (dto.AccountId != expectedAccountId) return Task.FromResult(false);
        if (dto.ReadAt is not null) return Task.FromResult(true);
        var updated = dto with { ReadAt = DateTimeOffset.UtcNow };
        _byId[id] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dedupeKey)) return Task.FromResult(false);
        return Task.FromResult(_dedupeKeys.ContainsKey(dedupeKey));
    }

    public Task<string?> GetCursorForAccountAsync(int accountId, DateTimeOffset since, CancellationToken ct)
    {
        var last = _byId.Values
            .Where(n => n.AccountId == accountId && n.CreatedAt > since)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => (DateTimeOffset?)n.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(last?.ToString("O"));
    }
}
