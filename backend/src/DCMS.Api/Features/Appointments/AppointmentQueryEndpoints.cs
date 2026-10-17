using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.Appointments;

namespace DCMS.Api.Features.Appointments;

// UC-05 read API shared by S10/S12 (staff) and S14/S16 (patient app). Scope is decided by the service from the session.
public static class AppointmentQueryEndpoints
{
    public static void MapAppointmentQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapGroup("/api/appointments").RequireAuthorization().WithTags("UC-05 S10 Appointment queries");
        staff.MapGet("/options", async (HttpContext context, IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.OptionsAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        staff.MapGet("/calendar", async (HttpContext context, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status,
                IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.CalendarAsync(AuthenticationEndpoints.AccountId(context.User), branchId, from, to, dentistId, status, ct)));
        staff.MapGet("/", async (HttpContext context, int branchId, DateOnly from, DateOnly to, int? dentistId, string? status,
                int? page, int? pageSize, IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(AuthenticationEndpoints.AccountId(context.User),
                new(branchId, from, to, dentistId, status, page ?? 1, pageSize ?? 20), ct)));
        staff.MapGet("/{id:guid}", async (HttpContext context, Guid id, IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.DetailAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));

        var mine = endpoints.MapGroup("/api/me/appointments").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy)
            .WithTags("UC-05 S14/S16 My appointments");
        mine.MapGet("/", async (HttpContext context, string? scope, string? status, DateOnly? from, DateOnly? to, int? page, int? pageSize,
                IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.MineAsync(AuthenticationEndpoints.AccountId(context.User),
                new((scope ?? "UPCOMING").Trim().ToUpperInvariant(), status, from, to, page ?? 1, pageSize ?? 20), ct)));
        mine.MapGet("/{code}", async (HttpContext context, string code, IAppointmentQueryService service, CancellationToken ct) =>
            Results.Ok(await service.MineDetailAsync(AuthenticationEndpoints.AccountId(context.User), code, ct)));
    }
}
