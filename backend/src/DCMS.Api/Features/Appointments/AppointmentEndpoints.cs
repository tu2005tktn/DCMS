using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;

namespace DCMS.Api.Features.Appointments;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/appointments", async (HttpContext context, CreateAppointmentRequest request,
            IAppointmentService service, CancellationToken ct) =>
        {
            // A rejected bearer token must not be silently treated as an anonymous booking.
            if (context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true)
                throw new BusinessException(401, SystemMessages.Get("MSG07"));
            var current = context.Items["CurrentUser"] as DCMS.Application.Features.Authentication.CurrentUser;
            var outcome = await service.CreateAsync(current?.Id, request, ct);
            return Results.Json(outcome.Result, statusCode: outcome.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        }).AllowAnonymous().WithTags("UC-04 S15/S11 Appointment booking");
    }
}
