using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Patients;

namespace DCMS.Api.Features.Patients;

public static class PatientEndpoints
{
    public const string ReadPolicy = "ReadPatients";
    public const string ManagePolicy = "ManagePatients";

    public static void MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // S05/S06: read for Clinic Assistant and Dentist (Owner via effective Dentist); writes for Clinic Assistant only.
        var group = endpoints.MapGroup("/api/patients").RequireAuthorization(ReadPolicy).WithTags("UC-08/09/38 S05/S06 Patients");
        group.MapGet("/", async (IPatientService service, string? keyword, DateOnly? birthDate, bool? includeArchived,
                int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(keyword, birthDate, includeArchived ?? false, page ?? 1, pageSize ?? 20, ct)));
        group.MapPost("/duplicates", async (DuplicateCheckRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.DuplicatesAsync(request, ct)));
        group.MapGet("/{id:int}", async (int id, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));
        group.MapGet("/{id:int}/audit", async (int id, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.AuditAsync(id, ct)));

        group.MapPost("/", async (HttpContext context, CreatePatientRequest request, IPatientService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), request, ct);
            return Results.Created($"/api/patients/{result.Patient.Id}", result);
        }).RequireAuthorization(ManagePolicy);
        group.MapPut("/{id:int}", async (HttpContext context, int id, UpdatePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)))
            .RequireAuthorization(ManagePolicy);
        group.MapPost("/{id:int}/archive", async (HttpContext context, int id, ArchivePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.ArchiveAsync(AuthenticationEndpoints.AccountId(context.User), id, request, true, ct)))
            .RequireAuthorization(ManagePolicy);
        group.MapPost("/{id:int}/restore", async (HttpContext context, int id, ArchivePatientRequest request, IPatientService service, CancellationToken ct) =>
            Results.Ok(await service.ArchiveAsync(AuthenticationEndpoints.AccountId(context.User), id, request, false, ct)))
            .RequireAuthorization(ManagePolicy);
    }
}
