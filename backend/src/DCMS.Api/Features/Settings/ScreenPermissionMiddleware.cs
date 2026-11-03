using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Settings;

namespace DCMS.Api.Features.Settings;

public sealed class ScreenPermissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISettingsAccess access, ScreenPermissionScope scope)
    {
        if (context.Items["CurrentUser"] is not CurrentUser { AccountType: "Staff" } actor || context.GetEndpoint() is not RouteEndpoint endpoint) { await next(context); return; }
        JsonDocument? body = null;
        try {
            if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true) {
                if (context.Request.ContentLength > 4 * 1024 * 1024) throw new BusinessException(400, SystemMessages.Get("MSG04"));
                context.Request.EnableBuffering(65536, 4 * 1024 * 1024);
                try { body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted); } catch (JsonException) { }
                catch (IOException) { throw new BusinessException(400, SystemMessages.Get("MSG04")); }
                finally { context.Request.Body.Position = 0; }
            }
            var values = context.Request.RouteValues.ToDictionary(x => x.Key, x => x.Value?.ToString());
            var kind = body?.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("kind", out var draftKind) && draftKind.ValueKind == JsonValueKind.String ? draftKind.GetString() : null;
            var rule = ScreenEndpointPermissions.Resolve(endpoint.RoutePattern.RawText ?? "", context.Request.Method, values, kind);
            if (rule == null) throw new BusinessException(403, SystemMessages.Get("MSG06"));
            if (rule.Baseline == null && !rule.AnyOf.Any(actor.Permissions.Contains)) throw new BusinessException(403, SystemMessages.Get("MSG06"));
            if ((endpoint.RoutePattern.RawText ?? "").EndsWith("/change-appointment") && body?.RootElement.ValueKind == JsonValueKind.Object) {
                var permission = body.RootElement.TryGetProperty("newStartAt", out var newStart) && newStart.ValueKind != JsonValueKind.Null ? "appointments.reschedule" : "appointments.cancel";
                if (!actor.Permissions.Contains(permission)) throw new BusinessException(403, SystemMessages.Get("MSG06"));
            }
            if (rule.Baseline == null) await scope.EnforceAsync(actor.Id, context, body?.RootElement, context.RequestAborted);
            await next(context);
        } finally { body?.Dispose(); }
    }
}
