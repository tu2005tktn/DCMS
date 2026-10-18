using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.Appointments;

namespace DCMS.Api.Features.Appointments;

// UC-06 support requests for the patient app (S16). Resolve/reject belong to TV5.
public static class AppointmentSupportEndpoints
{
    public static void MapAppointmentSupportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/appointments/{code}/support-requests", async (HttpContext context, string code,
            CreateSupportRequest request, IAppointmentSupportService service, CancellationToken ct) =>
        {
            var outcome = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), code, request, ct);
            return Results.Json(outcome.Result, statusCode: outcome.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created);
        }).RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("UC-06 S16 Support requests");
        endpoints.MapPost("/api/support-requests/{id:int}/withdraw", async (HttpContext context, int id,
            WithdrawSupportRequest request, IAppointmentSupportService service, CancellationToken ct) =>
            Results.Ok(await service.WithdrawAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)))
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("UC-06 S16 Support requests");
        endpoints.MapGet("/api/me/support-requests", async (HttpContext context, string? appointmentCode,
            IAppointmentSupportService service, CancellationToken ct) =>
            Results.Ok(await service.MineAsync(AuthenticationEndpoints.AccountId(context.User), appointmentCode, ct)))
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("UC-06 S16 Support requests");
    }
}
