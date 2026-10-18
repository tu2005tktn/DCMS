using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.SupportRequests;

namespace DCMS.Api.Features.Appointments;

public static class SupportRequestEndpoints
{
    public static void MapSupportRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/support-requests").RequireAuthorization()
            .WithTags("S12 Staff booking exceptions");
        group.MapGet("", async (HttpContext context, string? statuses, int? branchId, int? page, int? pageSize,
            ISupportRequestService service, CancellationToken ct) => Results.Ok(await service.ListAsync(
                AuthenticationEndpoints.AccountId(context.User), statuses, branchId, page ?? 1, pageSize ?? 20, ct)));
        group.MapGet("/{id}", async (HttpContext context, string id, ISupportRequestService service, CancellationToken ct) =>
            Results.Ok(await service.DetailAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        group.MapPost("/{id}/verify-requester", async (HttpContext context, string id, VerifyRequesterCommand command,
            ISupportRequestService service, CancellationToken ct) => Results.Ok(await service.VerifyRequesterAsync(
                AuthenticationEndpoints.AccountId(context.User), id, command, ct)));
        group.MapPost("/{id}/change-appointment", async (HttpContext context, string id, StaffSupportChangeCommand command,
            ISupportRequestService service, CancellationToken ct) => Results.Ok(await service.ChangeAppointmentAsync(
                AuthenticationEndpoints.AccountId(context.User), id, command, ct)));
        group.MapPost("/{id}/resolve", async (HttpContext context, string id, ResolveSupportCommand command,
            ISupportRequestService service, CancellationToken ct) => Results.Ok(await service.ResolveAsync(
                AuthenticationEndpoints.AccountId(context.User), id, command with { Decision = "Resolve" }, ct)));
        group.MapPost("/{id}/reject", async (HttpContext context, string id, ResolveSupportCommand command,
            ISupportRequestService service, CancellationToken ct) => Results.Ok(await service.ResolveAsync(
                AuthenticationEndpoints.AccountId(context.User), id, command with { Decision = "Reject" }, ct)));

        endpoints.MapPatch("/api/appointments/{id:guid}/attendance-confirmation", async (HttpContext context, Guid id,
            AttendanceConfirmationCommand command, ISupportRequestService service, CancellationToken ct) => Results.Ok(
                await service.SetAttendanceAsync(AuthenticationEndpoints.AccountId(context.User), id, command, ct)))
            .RequireAuthorization().WithTags("S12 Attendance confirmation");
    }
}
