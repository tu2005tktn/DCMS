using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Reception;
using DCMS.Domain.Features.Accounts;

namespace DCMS.Api.Features.Reception;

public static class ReceptionEndpoints
{
    public static void MapReceptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reception").RequireAuthorization(policy =>
            policy.RequireRole(AccountRoles.ClinicAssistant, AccountRoles.Dentist)).WithTags("S17/S18 Reception and Visit queue");
        static int Actor(HttpContext context) => AuthenticationEndpoints.AccountId(context.User);
        group.MapGet("/options", async (HttpContext c, IReceptionService s, CancellationToken ct) => Results.Ok(await s.OptionsAsync(Actor(c), ct)));
        group.MapGet("/", async (HttpContext c, int branchId, DateOnly date, int? dentistId, string? appointmentStatus,
            string? visitStatus, string? keyword, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.ReadAsync(Actor(c), new(branchId, date, dentistId, appointmentStatus, visitStatus, keyword), ct)));
        group.MapGet("/appointments/{appointment}", async (HttpContext c, string appointment, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.DetailAsync(Actor(c), appointment, ct)));
        group.MapGet("/sources", async (HttpContext c, int branchId, int patientId, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.SourcesAsync(Actor(c), branchId, patientId, ct)));
        group.MapGet("/visits/{id:guid}", async (HttpContext c, Guid id, IReceptionService s, CancellationToken ct) => Results.Ok(await s.VisitAsync(Actor(c), id, ct)));
        group.MapGet("/visits/{id:guid}/history", async (HttpContext c, Guid id, IReceptionService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(Actor(c), id, ct)));
        group.MapPost("/check-ins", async (HttpContext c, CheckInRequest request, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.CheckInAsync(Actor(c), request, ct))).RequireAuthorization(p => p.RequireRole(AccountRoles.ClinicAssistant));
        group.MapPost("/visits/{id:guid}/reorder", async (HttpContext c, Guid id, ReorderVisitRequest request, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.ReorderAsync(Actor(c), id, request, ct))).RequireAuthorization(p => p.RequireRole(AccountRoles.ClinicAssistant));
        group.MapPost("/visits/{id:guid}/priority", async (HttpContext c, Guid id, VisitPriorityRequest request, IReceptionService s, CancellationToken ct) =>
            Results.Ok(await s.PriorityAsync(Actor(c), id, request, ct))).RequireAuthorization(p => p.RequireRole(AccountRoles.Dentist));
    }
}
