using DCMS.Application.Common;
using DCMS.Application.Features.Appointments;
using DCMS.Api.Features.Authentication;

namespace DCMS.Api.Features.Appointments;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // AllowAnonymous lets UC-06 distinguish an unauthenticated guest (MSG32) from an invalid bearer (MSG07).
        endpoints.MapGet("/api/appointments/{code}/availability", async (HttpContext context, string code, DateOnly date,
            IAppointmentChangeService service, CancellationToken ct) =>
            Results.Ok(await service.AvailabilityAsync(ChangeActor(context), code, date, ct)))
            .AllowAnonymous().WithTags("UC-06 S12/S16 Appointment changes");
        endpoints.MapPost("/api/appointments/{code}/reschedule", async (HttpContext context, string code,
            RescheduleAppointmentRequest request, IAppointmentChangeService service, CancellationToken ct) =>
            Results.Ok(await service.RescheduleAsync(ChangeActor(context), code, request, ct)))
            .AllowAnonymous().WithTags("UC-06 S12/S16 Appointment changes");
        endpoints.MapPost("/api/appointments/{code}/cancel", async (HttpContext context, string code,
            CancelAppointmentRequest request, IAppointmentChangeService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(ChangeActor(context), code, request, ct)))
            .AllowAnonymous().WithTags("UC-06 S12/S16 Appointment changes");
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

    private static int ChangeActor(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            throw new BusinessException(401, SystemMessages.Get(context.Request.Headers.ContainsKey("Authorization") ? "MSG07" : "MSG32"));
        return AuthenticationEndpoints.AccountId(context.User);
    }
}
