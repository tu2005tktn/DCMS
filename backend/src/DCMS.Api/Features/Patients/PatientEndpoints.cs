using DCMS.Api.Features.Authentication;
using DCMS.Api.Features.PatientLinks;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Appointments;
using DCMS.Application.Features.Examinations;

namespace DCMS.Api.Features.Patients;

public static class PatientEndpoints
{
    public const string ReadPolicy = "ReadPatients";
    public const string ManagePolicy = "ManagePatients";
    public const string CreatePolicy = "CreatePatients";
    public const string UpdatePolicy = "UpdatePatients";
    public const string ArchivePolicy = "ArchivePatients";

    public static void MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Patient app "Hồ sơ của tôi": subject from the session; the service requires a VERIFIED self link.
        var own = endpoints.MapGroup("/api/patients/me").RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy)
            .WithTags("UC-36 Own patient record");
        own.MapGet("/", async (HttpContext context, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.OwnAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        own.MapPut("/", async (HttpContext context, UpdateOwnPatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateOwnAsync(AuthenticationEndpoints.AccountId(context.User), request, ct)));
        // docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §3: official visit results, Ready documents and their content.
        own.MapGet("/visits", async (HttpContext context, int? page, int? pageSize, IOwnClinicalRecordService service, CancellationToken ct) =>
            Results.Ok(await service.VisitsAsync(AuthenticationEndpoints.AccountId(context.User), page ?? 1, pageSize ?? 20, ct)));
        own.MapGet("/visits/{visitId:guid}", async (HttpContext context, Guid visitId, IOwnClinicalRecordService service, CancellationToken ct) =>
            Results.Ok(await service.VisitAsync(AuthenticationEndpoints.AccountId(context.User), visitId, ct)));
        own.MapGet("/documents", async (HttpContext context, Guid? visitId, int? page, int? pageSize, IOwnClinicalRecordService service, CancellationToken ct) =>
            Results.Ok(await service.DocumentsAsync(AuthenticationEndpoints.AccountId(context.User), visitId, page ?? 1, pageSize ?? 20, ct)));
        own.MapGet("/documents/{documentId:guid}/content", async (HttpContext context, Guid documentId, IOwnClinicalRecordService service, CancellationToken ct) =>
        {
            var file = await service.DocumentContentAsync(AuthenticationEndpoints.AccountId(context.User), documentId, ct);
            context.Response.Headers.CacheControl = "no-store";
            return Results.File(file.Bytes, file.ContentType);
        });

        // S05/S06: read for Clinic Assistant and Dentist (Owner via effective Dentist); writes for Clinic Assistant only.
        var group = endpoints.MapGroup("/api/patients").RequireAuthorization(ReadPolicy).WithTags("UC-08/09/38 S05/S06 Patients");
        group.MapGet("/", async (HttpContext context, IPatientService service, string? keyword, DateOnly? birthDate, bool? includeArchived,
                int? page, int? pageSize, int? branchId, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(AuthenticationEndpoints.AccountId(context.User), keyword, birthDate, includeArchived ?? false, page ?? 1, pageSize ?? 20, branchId, ct)));
        group.MapPost("/duplicates", async (DuplicateCheckRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.DuplicatesAsync(request, ct)));
        // S58: required administrative fields and patient-code mode for the S05/S06 form.
        group.MapGet("/form-policy", async (IPatientService service, CancellationToken ct) => Results.Ok(await service.FormPolicyAsync(ct)));
        group.MapGet("/{id:int}", async (HttpContext context, int id, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        group.MapGet("/{id:int}/audit", async (HttpContext context, int id, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.AuditAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        group.MapGet("/{id:int}/appointments", async (HttpContext context, int id, string? scope, int? page, int? pageSize,
            IAppointmentQueryService service, CancellationToken ct) => Results.Ok(await service.ForPatientAsync(
                AuthenticationEndpoints.AccountId(context.User), id, scope ?? "ALL", page ?? 1, pageSize ?? 20, ct)));

        group.MapPost("/", async (HttpContext context, CreatePatientRequest request, IPatientService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), request, ct);
            return Results.Created($"/api/patients/{result.Patient.Id}", result);
        }).RequireAuthorization(CreatePolicy);
        group.MapPut("/{id:int}", async (HttpContext context, int id, UpdatePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)))
            .RequireAuthorization(UpdatePolicy);
        group.MapPost("/{id:int}/archive", async (HttpContext context, int id, ArchivePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.ArchiveAsync(AuthenticationEndpoints.AccountId(context.User), id, request, true, ct)))
            .RequireAuthorization(ArchivePolicy);
        group.MapPost("/{id:int}/restore", async (HttpContext context, int id, ArchivePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.ArchiveAsync(AuthenticationEndpoints.AccountId(context.User), id, request, false, ct)))
            .RequireAuthorization(ArchivePolicy);
    }
}
