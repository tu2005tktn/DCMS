using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Prescriptions;

namespace DCMS.Api.Features.Prescriptions;

public static class PrescriptionEndpoints
{
    public static void MapPrescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);
        var g = app.MapGroup("/api/prescriptions").RequireAuthorization().WithTags("External prescriptions");
        g.MapGet("/medicine-options", async (HttpContext c, int branchId, string? search, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.OptionsAsync(Actor(c), branchId, search, ct)));
        g.MapGet("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.LinesAsync(Actor(c), visitId, ct)));
        g.MapPost("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SaveLineAsync(Actor(c), visitId, null, command, ct)));
        g.MapPut("/visits/{visitId:guid}/lines/{lineId:guid}", async (HttpContext c, Guid visitId, Guid lineId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SaveLineAsync(Actor(c), visitId, lineId, command, ct)));
    }
}
