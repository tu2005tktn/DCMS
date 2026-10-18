using System.Security.Claims;
using DCMS.Api.Features.Authentication;
using DCMS.Application.Common;
using DCMS.Application.Features.Notifications;

namespace DCMS.Api.Features.Notifications;

/// <summary>
/// Notification HTTP endpoints — thay thế cho mock mobile repo (S26+ phase 2).
///
/// Contract giống <c>notification.api.ts</c> trong mobile:
///   GET    /api/notifications?unreadOnly=&limit=&cursor=
///   POST   /api/notifications/mark-read        (MarkReadRequest: { notificationIds, expectedAccountId })
///   POST   /api/notifications/{id}/mark-read   (body: { expectedAccountId })
///   GET    /api/notifications/unread-count
///   POST   /api/notifications/check-deep-link  (body: { deepLink })
/// </summary>
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("S26 Notifications").RequireAuthorization();

        group.MapGet("", async (HttpContext ctx, INotificationService service,
            bool? unreadOnly, int? limit, string? cursor, CancellationToken ct) =>
        {
            var accountId = AuthenticationEndpoints.AccountId(ctx.User);
            var result = await service.ListAsync(accountId, new ListNotificationsQuery(unreadOnly, limit, cursor), ct);
            return Results.Ok(result);
        });

        group.MapPost("/mark-read", async (HttpContext ctx, MarkReadRequest request,
            INotificationService service, CancellationToken ct) =>
        {
            if (request is null || request.NotificationIds is null || request.NotificationIds.Count == 0
                || request.NotificationIds.Count > 100)
                throw new BusinessException(400, SystemMessages.Get("MSG09"));
            var accountId = AuthenticationEndpoints.AccountId(ctx.User);
            if (request.ExpectedAccountId != accountId)
                throw new BusinessException(409, SystemMessages.Get("MSG111"));
            // Đảm bảo chỉ mark của account hiện tại.
            var result = await service.MarkReadAsync(accountId, request with { ExpectedAccountId = accountId }, ct);
            return Results.Ok(result);
        });

        group.MapPost("/{id}/mark-read", async (HttpContext ctx, string id, MarkReadOneBody body,
            INotificationService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(id)) throw new BusinessException(400, SystemMessages.Get("MSG09"));
            var accountId = AuthenticationEndpoints.AccountId(ctx.User);
            if (body is null || body.ExpectedAccountId != accountId)
                throw new BusinessException(409, SystemMessages.Get("MSG111"));
            var r = await service.MarkReadOneAsync(id, accountId, ct);
            if (r.Skipped.Count > 0) throw new BusinessException(409, SystemMessages.Get("MSG111"));
            return Results.Ok(new { ok = true });
        });

        group.MapGet("/unread-count", async (HttpContext ctx, INotificationService service, CancellationToken ct) =>
        {
            var accountId = AuthenticationEndpoints.AccountId(ctx.User);
            var count = await service.CountUnreadAsync(accountId, ct);
            return Results.Ok(new { count });
        });

        group.MapPost("/check-deep-link", async (HttpContext ctx, CheckDeepLinkRequest body,
            INotificationService service, CancellationToken ct) =>
        {
            if (body is null || string.IsNullOrWhiteSpace(body.DeepLink))
                throw new BusinessException(400, SystemMessages.Get("MSG09"));
            var accountId = AuthenticationEndpoints.AccountId(ctx.User);
            var r = await service.CheckDeepLinkAsync(accountId, body.DeepLink, ct);
            return Results.Ok(r);
        });
    }

    public sealed record MarkReadOneBody(int ExpectedAccountId);
}
