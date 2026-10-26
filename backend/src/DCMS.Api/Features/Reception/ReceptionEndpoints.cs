using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Reception;

namespace DCMS.Api.Features.Reception;

public static class ReceptionEndpoints
{
    public static void MapReceptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reception").RequireAuthorization().WithTags("DCMS-111 S17 S18 Reception");
        group.MapGet("/options", async (HttpContext c, IReceptionService s, CancellationToken ct) => Results.Ok(await s.OptionsAsync(AuthenticationEndpoints.AccountId(c.User), ct)));
        group.MapGet("/appointments", async (HttpContext c, int branchId, DateOnly date, int? dentistId, string? status, string? keyword,
            int? page, int? pageSize, IReceptionService s, CancellationToken ct) => Results.Ok(await s.AppointmentsAsync(AuthenticationEndpoints.AccountId(c.User), new(branchId, date, dentistId, status, keyword, page ?? 1, pageSize ?? 20), ct)));
        group.MapGet("/queue", async (HttpContext c, int branchId, DateOnly date, int? dentistId, string? status, string? keyword,
            int? page, int? pageSize, IReceptionService s, CancellationToken ct) => Results.Ok(await s.QueueAsync(AuthenticationEndpoints.AccountId(c.User), new(branchId, date, dentistId, status, keyword, page ?? 1, pageSize ?? 20), ct)));
        group.MapGet("/appointments/{appointmentId:guid}", async (HttpContext c, Guid appointmentId, IReceptionService s, CancellationToken ct) => Results.Ok(await s.DetailAsync(AuthenticationEndpoints.AccountId(c.User), appointmentId, ct)));
        group.MapGet("/appointments/{appointmentId:guid}/courses", async (HttpContext c, Guid appointmentId, int patientId, IReceptionService s, CancellationToken ct) => Results.Ok(await s.CoursesAsync(AuthenticationEndpoints.AccountId(c.User), appointmentId, patientId, ct)));
        group.MapPost("/appointments/{appointmentId:guid}/check-in", async (HttpContext c, Guid appointmentId, CheckInRequest request, IReceptionService s, CancellationToken ct) => Results.Ok(await s.CheckInAsync(AuthenticationEndpoints.AccountId(c.User), appointmentId, request, ct)));
        endpoints.MapGet("/api/visits/{visitId:guid}", async (HttpContext c, Guid visitId, IReceptionService s, CancellationToken ct) => Results.Ok(await s.VisitAsync(AuthenticationEndpoints.AccountId(c.User), visitId, ct))).RequireAuthorization().WithTags("Visits");
    }
}
