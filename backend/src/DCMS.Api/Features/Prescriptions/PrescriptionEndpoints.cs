using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.Prescriptions;

namespace DCMS.Api.Features.Prescriptions;

// docs/specs/PRESCRIPTION-MANAGEMENT.md §5
public static class PrescriptionEndpoints
{
    public static void MapPrescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);

        var g = app.MapGroup("/api/prescriptions").RequireAuthorization().WithTags("External prescriptions");

        g.MapGet("/medicine-options", async (HttpContext c, int? branchId, string? q, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.OptionsAsync(Actor(c), branchId, q, ct)));

        g.MapGet("/visits/{visitId:guid}/prescription", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PrescriptionAsync(Actor(c), visitId, ct)));

        g.MapPut("/visits/{visitId:guid}/prescription", async (HttpContext c, Guid visitId, SavePrescriptionAdvice command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SaveAdviceAsync(Actor(c), visitId, command, ct)));

        g.MapGet("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.LinesAsync(Actor(c), visitId, ct)));

        g.MapGet("/visits/{visitId:guid}/safety-check", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.CheckSafetyAsync(Actor(c), visitId, ct)));

        g.MapPost("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SaveLineAsync(Actor(c), visitId, null, command, ct)));

        g.MapPut("/visits/{visitId:guid}/lines/{lineId:guid}", async (HttpContext c, Guid visitId, Guid lineId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SaveLineAsync(Actor(c), visitId, lineId, command, ct)));

        g.MapDelete("/visits/{visitId:guid}/lines/{lineId:guid}", async (HttpContext c, Guid visitId, Guid lineId, Guid requestKey, int expectedVersion, IPrescriptionService s, CancellationToken ct) =>
        {
            await s.DeleteLineAsync(Actor(c), visitId, lineId, requestKey, expectedVersion, ct);
            return Results.NoContent();
        });

        g.MapPost("/visits/{visitId:guid}/apply-template", async (HttpContext c, Guid visitId, ApplyPrescriptionTemplateCommand command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.ApplyTemplateAsync(Actor(c), visitId, command, ct)));

        g.MapPost("/visits/{visitId:guid}/finalize", async (HttpContext c, Guid visitId, FinalizePrescriptionCommand command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.FinalizeAsync(Actor(c), visitId, command, ct)));

        g.MapPost("/visits/{visitId:guid}/corrections", async (HttpContext c, Guid visitId, CreatePrescriptionCorrection command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.CreateCorrectionAsync(Actor(c), visitId, command, ct)));

        g.MapGet("/visits/{visitId:guid}/print", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PrintDataAsync(Actor(c), visitId, ct)));

        g.MapGet("/templates", async (HttpContext c, string? search, string? diagnosis, string? status, int? page, int? pageSize, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.TemplatesAsync(Actor(c), search, diagnosis, status, page ?? 1, pageSize ?? 20, ct)));

        g.MapGet("/templates/{id:guid}", async (HttpContext c, Guid id, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.TemplateAsync(Actor(c), id, ct)));

        g.MapPost("/templates", async (HttpContext c, SavePrescriptionTemplate command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SaveTemplateAsync(Actor(c), null, command, ct)));

        g.MapPut("/templates/{id:guid}", async (HttpContext c, Guid id, SavePrescriptionTemplate command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SaveTemplateAsync(Actor(c), id, command, ct)));

        g.MapPost("/templates/{id:guid}/state", async (HttpContext c, Guid id, TemplateStateCommand command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SetTemplateStateAsync(Actor(c), id, command, ct)));

        // Patient self-service: only finalized revisions of visits of verified linked profiles.
        var p = app.MapGroup("/api/patients/me/prescriptions").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("Patient prescriptions");
        p.MapGet("/", async (HttpContext c, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PatientPrescriptionsAsync(Actor(c), ct)));

        p.MapGet("/visits/{visitId:guid}", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PatientPrescriptionAsync(Actor(c), visitId, ct)));
    }
}
