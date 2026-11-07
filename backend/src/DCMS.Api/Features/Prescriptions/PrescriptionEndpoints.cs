using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Prescriptions;
using DCMS.Api.Features.PatientLinks;

namespace DCMS.Api.Features.Prescriptions;

public static class PrescriptionEndpoints
{
    public static void MapPrescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);
        var own = app.MapGroup("/api/me/prescriptions").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("My prescriptions");
        own.MapGet("/", async (HttpContext c, int? page, ICustomerPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(Actor(c), page ?? 1, ct)));
        own.MapGet("/{id:guid}", async (HttpContext c, Guid id, ICustomerPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.DetailAsync(Actor(c), id, ct)));
        var g = app.MapGroup("/api/prescriptions").RequireAuthorization().WithTags("External prescriptions");
        g.MapGet("/medicine-options", async (HttpContext c, int branchId, string? search, string? source, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.OptionsAsync(Actor(c), branchId, search, source, ct)));
        g.MapGet("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.LinesAsync(Actor(c), visitId, ct)));
        g.MapPost("/visits/{visitId:guid}/lines", async (HttpContext c, Guid visitId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SaveLineAsync(Actor(c), visitId, null, command, ct)));
        g.MapPut("/visits/{visitId:guid}/lines/{lineId:guid}", async (HttpContext c, Guid visitId, Guid lineId, SavePrescriptionLine command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SaveLineAsync(Actor(c), visitId, lineId, command, ct)));
        g.MapGet("/patients/{patientId:int}", async (HttpContext c, int patientId, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.PatientListAsync(Actor(c), patientId, ct)));
        g.MapGet("/patients/{patientId:int}/context", async (HttpContext c, int patientId, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.PatientContextAsync(Actor(c), patientId, ct)));
        g.MapGet("/patients/{patientId:int}/{id:guid}", async (HttpContext c, int patientId, Guid id, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.PatientDetailAsync(Actor(c), patientId, id, ct)));
        g.MapPost("/patients/{patientId:int}", async (HttpContext c, int patientId, SavePatientPrescription command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SavePatientAsync(Actor(c), patientId, null, command, ct)));
        g.MapPut("/patients/{patientId:int}/{id:guid}", async (HttpContext c, int patientId, Guid id, SavePatientPrescription command, IPrescriptionService s, CancellationToken ct) => Results.Ok(await s.SavePatientAsync(Actor(c), patientId, id, command, ct)));
    }
}
