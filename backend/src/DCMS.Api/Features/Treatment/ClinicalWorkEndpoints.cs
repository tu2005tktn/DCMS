using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Treatment;

namespace DCMS.Api.Features.Treatment;

public static class ClinicalWorkEndpoints
{
    public static void MapClinicalWorkEndpoints(this IEndpointRouteBuilder app)
    {
        var care = app.MapGroup("/api/care-tasks").RequireAuthorization()
            .WithTags("UC-25/53 S25/S60 Care task backend");
        care.MapGet("/", async (HttpContext http, int branchId, DateOnly? from, DateOnly? to,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.ListAsync(
            AuthenticationEndpoints.AccountId(http.User), branchId, from, to, ct)));
        // S60 staff Web (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md); GET routes need care-tasks.read.
        care.MapGet("/options", async (HttpContext http, ICareTaskService service, CancellationToken ct) =>
            Results.Ok(await service.OptionsAsync(AuthenticationEndpoints.AccountId(http.User), ct)));
        care.MapGet("/patient-options", async (HttpContext http, int branchId, string? search,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.PatientOptionsAsync(
            AuthenticationEndpoints.AccountId(http.User), branchId, search, ct)));
        care.MapGet("/worklist", async (HttpContext http, int? branchId, DateOnly? from, DateOnly? to,
            int? ownerAccountId, string? type, string? status, string? search, int? page, int? pageSize,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.WorklistAsync(
            AuthenticationEndpoints.AccountId(http.User), new CareWorklistQuery(branchId, from, to, ownerAccountId,
                type, status, search, page ?? 1, pageSize ?? 20), ct)));
        care.MapGet("/export", async (HttpContext http, int? branchId, DateOnly? from, DateOnly? to,
            int? ownerAccountId, string? type, string? status, string? search,
            ICareTaskService service, CancellationToken ct) =>
        {
            var bytes = await service.ExportAsync(AuthenticationEndpoints.AccountId(http.User),
                new CareWorklistQuery(branchId, from, to, ownerAccountId, type, status, search), ct);
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"cham-soc-khach-hang-{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx");
        });
        care.MapPost("/", async (HttpContext http, CreateCareTaskRequest request,
            ICareTaskService service, CancellationToken ct) => Results.Json(await service.CreateAsync(
            AuthenticationEndpoints.AccountId(http.User), request, ct), statusCode: 201));
        care.MapGet("/{id:guid}", async (HttpContext http, Guid id,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.GetAsync(
            AuthenticationEndpoints.AccountId(http.User), id, ct)));
        care.MapPatch("/{id:guid}", async (HttpContext http, Guid id, UpdateCareTaskRequest request,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.UpdateAsync(
            AuthenticationEndpoints.AccountId(http.User), id, request, ct)));
        care.MapPost("/{id:guid}/contact-attempts", async (HttpContext http, Guid id,
            ContactAttemptRequest request, ICareTaskService service, CancellationToken ct) =>
            Results.Json(await service.ContactAsync(AuthenticationEndpoints.AccountId(http.User), id,
                request, ct), statusCode: 201));
    }
}
