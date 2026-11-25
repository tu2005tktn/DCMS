using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.Prescriptions;

namespace DCMS.Api.Features.Prescriptions;

// docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md & docs/specs/PRESCRIPTION-AND-MEDICINE-TABLE-SIMPLIFICATION.vi.md
public static class PrescriptionEndpoints
{
    public static void MapPrescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);

        var g = app.MapGroup("/api/prescriptions").RequireAuthorization().WithTags("Patient prescriptions");

        g.MapGet("/medicine-options", async (HttpContext c, int? branchId, string? q, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.OptionsAsync(Actor(c), branchId, q, ct)));

        g.MapGet("/patients/{patientId:int}", async (HttpContext c, int patientId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PatientProfilePrescriptionsAsync(Actor(c), patientId, ct)));
        g.MapGet("/patients/{patientId:int}/context", async (HttpContext c, int patientId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.PatientContextAsync(Actor(c), patientId, ct)));
        g.MapPost("/patients/{patientId:int}", async (HttpContext c, int patientId, SavePatientPrescription command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SavePatientProfilePrescriptionAsync(Actor(c), patientId, null, command, ct)));
        g.MapPut("/patients/{patientId:int}/{id:guid}", async (HttpContext c, int patientId, Guid id, SavePatientPrescription command, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.SavePatientProfilePrescriptionAsync(Actor(c), patientId, id, command, ct)));

        // Patient self-service: only prescriptions belonging to verified linked patient profiles.
        var p = app.MapGroup("/api/patients/me/prescriptions").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy).WithTags("Patient prescriptions");
        p.MapGet("/", async (HttpContext c, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.OwnPatientPrescriptionsAsync(Actor(c), ct)));
        p.MapGet("/{prescriptionId:guid}", async (HttpContext c, Guid prescriptionId, IPrescriptionService s, CancellationToken ct) =>
            Results.Ok(await s.OwnPatientPrescriptionAsync(Actor(c), prescriptionId, ct)));
    }
}
