using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DCMS.Infrastructure.Features.Notifications;

/// <summary>Durable, account-scoped notification store for S26.</summary>
public sealed class EfNotificationStore(DcmsDbContext db) : INotificationStore
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(int? accountId, bool? unreadOnly, int? limit,
        string? cursor, CancellationToken ct)
    {
        var query = db.InAppNotifications.AsNoTracking().Include(x => x.Deliveries).AsQueryable();
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId);
        if (unreadOnly == true) query = query.Where(x => x.ReadAt == null);
        if (!string.IsNullOrWhiteSpace(cursor) && DateTimeOffset.TryParse(cursor, out var before))
            query = query.Where(x => x.CreatedAt < before);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.PublicId)
            .Take(Math.Clamp(limit ?? 20, 1, 100)).ToListAsync(ct);
        return rows.Select(Map).ToArray();
    }

    public async Task<NotificationDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        var row = await db.InAppNotifications.AsNoTracking().Include(x => x.Deliveries)
            .SingleOrDefaultAsync(x => x.PublicId == id, ct);
        return row == null ? null : Map(row);
    }

    public async Task<NotificationDto> UpsertAsync(NotificationDto dto, CancellationToken ct)
    {
        var existing = await db.InAppNotifications.Include(x => x.Deliveries)
            .SingleOrDefaultAsync(x => x.DedupeKey == dto.DedupeKey, ct);
        if (existing != null) return Map(existing);
        var row = new InAppNotification
        {
            PublicId = dto.Id, AccountId = dto.AccountId, Kind = dto.Kind.ToString(), Title = dto.Title,
            Body = dto.Body, CreatedAt = dto.CreatedAt, ReadAt = dto.ReadAt,
            AppointmentId = Guid.TryParse(dto.AppointmentId, out var appointmentId) ? appointmentId : null,
            RequestId = Guid.TryParse(dto.RequestId, out var requestId) ? requestId : null,
            DeepLink = dto.DeepLink, HasExternalFailure = dto.HasExternalFailure, DedupeKey = dto.DedupeKey
        };
        row.Deliveries = dto.Attempts.Select(x => new NotificationDelivery
        {
            NotificationId = row.Id, Channel = x.Channel.ToString(), Status = x.Status.ToString(),
            AttemptCount = x.AttemptCount, LastAttemptAt = x.LastAttemptAt,
            NextRetryAt = x.NextRetryAt, LastError = x.ErrorMessage
        }).ToList();
        db.InAppNotifications.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public Task<int> CountUnreadAsync(int accountId, CancellationToken ct) =>
        db.InAppNotifications.CountAsync(x => x.AccountId == accountId && x.ReadAt == null, ct);

    public async Task<bool> MarkReadAsync(string id, int expectedAccountId, CancellationToken ct)
    {
        var row = await db.InAppNotifications.SingleOrDefaultAsync(x => x.PublicId == id && x.AccountId == expectedAccountId, ct);
        if (row == null) return false;
        if (row.ReadAt == null) { row.ReadAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
        return true;
    }

    public Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken ct) =>
        db.InAppNotifications.AnyAsync(x => x.DedupeKey == dedupeKey, ct);

    public async Task<string?> GetCursorForAccountAsync(int accountId, DateTimeOffset since, CancellationToken ct)
    {
        var created = await db.InAppNotifications.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.CreatedAt > since)
            .OrderByDescending(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefaultAsync(ct);
        return created?.ToString("O");
    }

    public async Task<bool> CanOpenAppointmentAsync(int accountId, string appointmentRef, CancellationToken ct)
    {
        var isId = Guid.TryParse(appointmentRef, out var id);
        var appointment = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Code == appointmentRef || (isId && x.Id == id), ct);
        if (appointment == null) return false;
        if (appointment.BookerAccountId == accountId) return true;
        return appointment.PatientId.HasValue && await db.PatientAccountLinks.AsNoTracking().AnyAsync(x =>
            x.AccountId == accountId && x.PatientId == appointment.PatientId && x.Status == PatientLinkStatus.Verified, ct);
    }

    private static NotificationDto Map(InAppNotification row) => new(row.PublicId, row.AccountId,
        Enum.Parse<NotificationKind>(row.Kind), row.Title, row.Body, row.CreatedAt, row.ReadAt,
        row.AppointmentId?.ToString(), row.RequestId?.ToString(), row.DeepLink,
        row.Deliveries.OrderBy(x => x.Channel).Select(x => new NotificationAttempt(
            Enum.Parse<NotificationChannel>(x.Channel), Enum.Parse<DispatchStatus>(x.Status), x.AttemptCount,
            x.LastAttemptAt, x.NextRetryAt, x.LastError)).ToArray(), row.HasExternalFailure, row.DedupeKey);
}
