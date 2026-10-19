using System.Globalization;
using System.Text.Json;
using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Appointments;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DCMS.Infrastructure.Features.Notifications;

public interface IAppointmentNotificationDispatcher
{
    Task<int> ProcessPendingAsync(CancellationToken ct);
}

public sealed record ExternalDeliveryResult(bool Delivered, string? ProviderMessageId, string? Error);
public interface IExternalNotificationAdapter
{
    Task<ExternalDeliveryResult> SendAsync(NotificationChannel channel, NotificationDto notification, CancellationToken ct);
}

public sealed class UnavailableExternalNotificationAdapter : IExternalNotificationAdapter
{
    public Task<ExternalDeliveryResult> SendAsync(NotificationChannel channel, NotificationDto notification, CancellationToken ct) =>
        Task.FromResult(new ExternalDeliveryResult(false, null, $"{channel} adapter is not configured."));
}

/// <summary>
/// Consumes appointment outbox events only after their booking transaction commits.
/// Reprocessing is safe through notification/reminder unique dedupe keys.
/// </summary>
public sealed class AppointmentNotificationDispatcher(
    DcmsDbContext db, INotificationService notifications, INotificationStore store,
    IExternalNotificationAdapter external, TimeProvider clock) : IAppointmentNotificationDispatcher
{
    private static readonly int[] ReminderMinutes = [24 * 60, 2 * 60];
    private static readonly int[] RetryMinutes = [1, 5, 15];

    public async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var processed = 0;
        var events = await db.AppointmentOutboxEvents.Where(x => x.DispatchedAt == null)
            .OrderBy(x => x.OccurredAt).Take(50).ToListAsync(ct);
        foreach (var item in events)
        {
            var appointment = await db.Appointments.SingleOrDefaultAsync(x => x.Id == item.AggregateId, ct);
            if (appointment == null) { item.DispatchedAt = clock.GetUtcNow(); continue; }

            if (item.EventType.StartsWith("support.", StringComparison.OrdinalIgnoreCase))
            {
                using var payload = JsonDocument.Parse(item.PayloadJson);
                var requestId = payload.RootElement.GetProperty("requestId").GetGuid();
                var request = await db.AppointmentSupportRequests.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == requestId, ct);
                if (request?.RequesterAccountId is int requesterId)
                {
                    var rejected = item.EventType.EndsWith("rejected", StringComparison.OrdinalIgnoreCase);
                    await notifications.CreateAsync(requesterId, appointment.BranchId,
                        NotificationKind.SupportRequestUpdate,
                        rejected ? "Yêu cầu hỗ trợ bị từ chối" : "Yêu cầu hỗ trợ đã được xử lý",
                        rejected ? $"Yêu cầu {request.Code} đã bị từ chối."
                            : $"Yêu cầu {request.Code} đã được xử lý.",
                        appointment.Id.ToString(), request.Id.ToString(), $"/appointments/{appointment.Code}",
                        [NotificationChannel.InApp], $"support-event:{item.Id}", ct);
                }
                item.DispatchedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct); processed++; continue;
            }

            await SupersedeRemindersAsync(appointment.Id, ct);
            var cancelled = item.EventType.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                || appointment.Status == "Cancelled";
            if (!cancelled && appointment.BookerAccountId.HasValue)
                await ScheduleRemindersAsync(appointment, ct);

            if (item.EventType.Equals("appointment.attendance", StringComparison.OrdinalIgnoreCase))
            {
                item.DispatchedAt = clock.GetUtcNow(); await db.SaveChangesAsync(ct); processed++; continue;
            }

            if (appointment.BookerAccountId.HasValue)
            {
                var kind = cancelled ? NotificationKind.AppointmentCancelled
                    : item.EventType.Contains("resched", StringComparison.OrdinalIgnoreCase)
                        ? NotificationKind.AppointmentRescheduled : NotificationKind.AppointmentBooked;
                var when = appointment.StartAt.ToOffset(TimeSpan.FromHours(7))
                    .ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
                var title = kind switch
                {
                    NotificationKind.AppointmentCancelled => "Lịch hẹn đã huỷ",
                    NotificationKind.AppointmentRescheduled => "Lịch hẹn đã đổi",
                    _ => "Đặt lịch thành công"
                };
                var body = kind == NotificationKind.AppointmentCancelled
                    ? $"Lịch {appointment.Code} đã được huỷ."
                    : $"Lịch {appointment.Code} vào {when}.";
                await notifications.CreateAsync(appointment.BookerAccountId, appointment.BranchId, kind,
                    title, body, appointment.Id.ToString(), null, $"/appointments/{appointment.Code}",
                    [NotificationChannel.InApp], $"appointment-event:{item.Id}", ct);
            }
            item.DispatchedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            processed++;
        }

        var now = clock.GetUtcNow();
        var due = await db.AppointmentReminders.Where(x => x.Status == "Pending" && x.DueAt <= now)
            .OrderBy(x => x.DueAt).Take(50).ToListAsync(ct);
        foreach (var reminder in due)
        {
            var appointment = await db.Appointments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reminder.AppointmentId, ct);
            if (appointment == null || appointment.Version != reminder.AppointmentVersion
                || appointment.Status != "Confirmed" || appointment.BookerAccountId != reminder.AccountId)
            {
                reminder.Status = "Superseded"; reminder.ProcessedAt = now;
            }
            else
            {
                var when = appointment.StartAt.ToOffset(TimeSpan.FromHours(7))
                    .ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
                await notifications.CreateAsync(reminder.AccountId, appointment.BranchId,
                    NotificationKind.ReminderUpcoming, "Nhắc lịch hẹn",
                    $"Bạn có lịch {appointment.Code} vào {when}.", appointment.Id.ToString(), null,
                    $"/appointments/{appointment.Code}", [NotificationChannel.InApp], $"reminder:{reminder.Id}", ct);
                reminder.Status = "Sent"; reminder.ProcessedAt = now;
            }
            await db.SaveChangesAsync(ct);
            processed++;
        }

        var deliveries = await db.NotificationDeliveries
            .Where(x => (x.Status == "Pending" || x.Status == "Retrying")
                && x.Channel != "InApp" && x.NextRetryAt <= now)
            .OrderBy(x => x.NextRetryAt).Take(50).ToListAsync(ct);
        foreach (var delivery in deliveries)
        {
            var row = await db.InAppNotifications.SingleAsync(x => x.Id == delivery.NotificationId, ct);
            var dto = await store.GetByIdAsync(row.PublicId, ct);
            if (dto == null) { delivery.Status = "Failed"; await db.SaveChangesAsync(ct); continue; }
            var channel = Enum.Parse<NotificationChannel>(delivery.Channel);
            var outcome = await external.SendAsync(channel, dto, ct);
            delivery.AttemptCount++;
            delivery.LastAttemptAt = now;
            if (outcome.Delivered)
            {
                delivery.Status = "Delivered"; delivery.NextRetryAt = null; delivery.LastError = null;
            }
            else if (delivery.AttemptCount >= 3)
            {
                delivery.Status = "Failed"; delivery.NextRetryAt = null;
                delivery.LastError = outcome.Error ?? "External delivery failed.";
                row.HasExternalFailure = true;
                db.StaffHandlingItems.Add(new StaffHandlingItem
                {
                    BranchId = row.BranchId, AppointmentId = row.AppointmentId, NotificationId = row.Id,
                    AttemptCount = delivery.AttemptCount, Error = delivery.LastError, CreatedAt = now
                });
            }
            else
            {
                delivery.Status = "Retrying"; delivery.LastError = outcome.Error ?? "External delivery failed.";
                delivery.NextRetryAt = now.AddMinutes(RetryMinutes[delivery.AttemptCount]);
            }
            await db.SaveChangesAsync(ct); processed++;
        }
        return processed;
    }

    private async Task SupersedeRemindersAsync(Guid appointmentId, CancellationToken ct)
    {
        var old = await db.AppointmentReminders.Where(x => x.AppointmentId == appointmentId
            && (x.Status == "Pending" || x.Status == "Retrying")).ToListAsync(ct);
        foreach (var reminder in old) reminder.Status = "Superseded";
    }

    private async Task ScheduleRemindersAsync(Appointment appointment, CancellationToken ct)
    {
        foreach (var lead in ReminderMinutes)
        {
            var dueAt = appointment.StartAt.AddMinutes(-lead);
            if (dueAt <= clock.GetUtcNow()) continue;
            if (await db.AppointmentReminders.AnyAsync(x => x.AppointmentId == appointment.Id
                && x.AppointmentVersion == appointment.Version && x.LeadMinutes == lead, ct)) continue;
            db.AppointmentReminders.Add(new AppointmentReminder
            {
                AppointmentId = appointment.Id, AppointmentVersion = appointment.Version,
                AccountId = appointment.BookerAccountId!.Value, LeadMinutes = lead,
                DueAt = dueAt, CreatedAt = clock.GetUtcNow()
            });
        }
    }
}

public sealed class AppointmentNotificationWorker(
    IServiceScopeFactory scopes, ILogger<AppointmentNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IAppointmentNotificationDispatcher>()
                    .ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception error) { logger.LogError(error, "Appointment notification dispatcher failed."); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
