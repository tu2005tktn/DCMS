using DCMS.Application.Common;
using DCMS.Application.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Prescriptions;
using DCMS.Domain.Features.Examinations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    DcmsDbContext db,
    IConfiguration configuration,
    IRealtimeNotifier realtime,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task<ListNotificationsResult> ListAsync(int accountId, ListNotificationsQuery query, CancellationToken ct)
    {
        return ListInternalAsync(accountId, query, ct);
    }

    private async Task<ListNotificationsResult> ListInternalAsync(int accountId, ListNotificationsQuery query, CancellationToken ct)
    {
        var limit = query.Limit is > 0 and <= 100 ? query.Limit.Value : 20;
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
        if (deepLink == "/appointments") return new CheckDeepLinkResult(true, null);
        if (deepLink == "/prescriptions")
            return new CheckDeepLinkResult(await db.PatientAccountLinks.AsNoTracking().AnyAsync(x =>
                x.AccountId == accountId && x.Status == PatientLinkStatus.Verified, ct), null);
        if (deepLink == "/book-appointment")
        {
            var canBook = await db.FollowUpInstructions.AsNoTracking().AnyAsync(x =>
                x.Status == "NeedsBooking" && db.PatientAccountLinks.Any(link =>
                    link.AccountId == accountId && link.Status == PatientLinkStatus.Verified
                    && link.PatientId == x.PatientId), ct);
            return canBook ? new CheckDeepLinkResult(true, null)
                : new CheckDeepLinkResult(false, "Yêu cầu đặt lịch không còn hiệu lực.");
        }
        var parts = deepLink.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return new CheckDeepLinkResult(false, "Loại liên kết chưa hỗ trợ.");
        bool allowed;
        if (parts[0] == "appointments")
            allowed = await store.CanOpenAppointmentAsync(accountId, parts[1], ct);
        else if (Guid.TryParse(parts[1], out var id))
        {
            var patients = db.PatientAccountLinks.AsNoTracking().Where(x => x.AccountId == accountId
                && x.Status == PatientLinkStatus.Verified && x.PatientId.HasValue)
                .Select(x => x.PatientId!.Value);
            allowed = parts[0] switch
            {
                "visit-results" => await db.Visits.AsNoTracking().AnyAsync(x => x.Id == id
                    && patients.Contains(x.PatientId) && db.Set<VisitExamination>()
                        .Any(e => e.VisitId == x.Id && e.Confirmed), ct),
                "prescriptions" => await db.Set<PatientPrescription>().AsNoTracking().AnyAsync(x =>
                    x.Id == id && patients.Contains(x.PatientId), ct)
                    || await db.Set<ExternalPrescription>().AsNoTracking().AnyAsync(x =>
                        x.VisitId == id && x.Status == PrescriptionStatuses.Finalized && patients.Contains(x.PatientId), ct),
                "medical-records" => await db.TreatmentDossiers.AsNoTracking().AnyAsync(x =>
                    x.Id == id && x.PlanState == "Committed" && x.State != "Deleted"
                    && patients.Contains(x.PatientId), ct),
                _ => false
            };
        }
        else allowed = false;
        return !allowed
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
        var id = $"NT-{Guid.NewGuid():N}";
        var effectiveChannels = channels is { Count: > 0 }
            ? channels
            : (IReadOnlyList<NotificationChannel>)new[] { NotificationChannel.InApp };
        if (accountId.HasValue && !string.IsNullOrWhiteSpace(configuration["Firebase:ServiceAccountPath"])
            && await db.PushDevices.AnyAsync(x => x.AccountId == accountId.Value, ct)
            && !effectiveChannels.Contains(NotificationChannel.Push))
            effectiveChannels = [.. effectiveChannels, NotificationChannel.Push];

        var now = DateTimeOffset.UtcNow;
        var attempts = effectiveChannels.Select(ch => new NotificationAttempt(
            ch,
            ch == NotificationChannel.InApp ? DispatchStatus.Delivered : DispatchStatus.Pending,
            ch == NotificationChannel.InApp ? 1 : 0,
            ch == NotificationChannel.InApp ? now : null,
            ch == NotificationChannel.InApp ? null : now.AddMinutes(1),
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

        dto = await store.UpsertAsync(dto, ct);

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
