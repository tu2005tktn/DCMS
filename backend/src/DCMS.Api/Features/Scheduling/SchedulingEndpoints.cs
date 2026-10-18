using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Scheduling;

namespace DCMS.Api.Features.Scheduling;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/working-schedules").RequireAuthorization().WithTags("S09 UC-03");
        group.MapGet("/recurring", async (HttpContext context, int dentistId, DateOnly date, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetRecurringAsync(AuthenticationEndpoints.AccountId(context.User), dentistId, date, ct)));
        group.MapPost("/recurring/preview", async (HttpContext context, RecurringCommand request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewRecurringAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        group.MapPost("/recurring/publish", async (HttpContext context, RecurringCommand request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PublishRecurringAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        group.MapPost("/recurring/preview-availability", async (HttpContext context, RecurringPreviewRequest request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewRecurringAvailabilityAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        group.MapGet("/options", async (HttpContext context, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.OptionsAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        group.MapGet("/", async (HttpContext context, int branchId, int dentistId, DateOnly weekStart, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), branchId, dentistId, weekStart, ct)));
        group.MapPost("/preview", async (HttpContext context, ScheduleCommand request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        group.MapPost("/publish", async (HttpContext context, ScheduleCommand request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PublishAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        group.MapPost("/preview-availability", async (HttpContext context, PreviewAvailabilityRequest request, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewAvailabilityAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        endpoints.MapGet("/api/availability/options", async (int? branchId, IWorkingScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.PublicOptionsAsync(branchId ?? 0, ct))).AllowAnonymous().WithTags("Shared availability");
        endpoints.MapGet("/api/availability", async (HttpContext context, int branchId, int dentistId, int? serviceId, DateOnly date,
            int durationMinutes, int? assistantId, IAvailabilityService service, CancellationToken ct) =>
        {
            var current = context.Items["CurrentUser"] as CurrentUser;
            int? staffId = current?.AccountType == "Staff" ? current.Id : null;
            return Results.Ok(await service.QueryAsync(new(branchId, dentistId, serviceId, date, durationMinutes, assistantId), staffId, ct));
        }).AllowAnonymous().WithTags("Shared availability");
    }
}
