using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Notifications;

namespace DCMS.Api.Features.Realtime;

/// <summary>
/// Test/dev endpoints để demo realtime mà không cần flow booking thật.
///
///   POST /api/test/realtime/ping  — fire 1 notification tới account HOẶC branch tuỳ body.
///   GET  /api/test/realtime/state — dump store (in-memory) cho debug.
///
/// CHỈ đăng ký trong môi trường Development. Production chặn để tránh lộ dữ liệu.
/// </summary>
public static class RealtimeEndpoints
{
    public static void MapRealtimeEndpoints(this IEndpointRouteBuilder endpoints, bool isDevelopment)
    {
        if (!isDevelopment) return;

        var group = endpoints.MapGroup("/api/test/realtime").WithTags("DEV realtime test").RequireAuthorization();

        group.MapPost("/ping", async (HttpContext ctx, TestRealtimePingRequest body,
            INotificationService service, CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var dedupe = body.DedupeKey ?? $"dev-ping-{Guid.NewGuid():N}";
            // Caller account nếu không truyền accountId — dùng account từ token.
            var accountId = body.AccountId ?? AuthenticationEndpoints.AccountId(ctx.User);
            var dto = await service.CreateAsync(
                accountId: accountId,
                branchId: body.BranchId,
                kind: body.Kind,
                title: body.Title,
                body: body.Body,
                appointmentId: null,
                requestId: null,
                deepLink: body.DeepLink,
                channels: null,
                dedupeKey: dedupe,
                ct: ct);
            return Results.Ok(dto);
        });

        group.MapGet("/state", async (INotificationStore store, CancellationToken ct) =>
        {
            var all = await store.ListAsync(null, null, 200, null, ct);
            return Results.Ok(new { count = all.Count, items = all });
        });
    }
}
