using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Treatment;
using DCMS.Api.Features.PatientLinks;

namespace DCMS.Api.Features.Treatment;

public static class TreatmentEndpoints
{
    public const string DentistPolicy = "TreatmentDentist";

    public static void MapTreatmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var patient = endpoints.MapGroup("/api/patients/{patientId:int}/dossiers")
            .RequireAuthorization(DentistPolicy).WithTags("UC-13/39 S21/S43 Treatment");
        patient.MapGet("/", async (HttpContext context, int patientId, int? branchId, int? page, int? pageSize,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(AuthenticationEndpoints.AccountId(context.User), patientId,
                branchId, page ?? 1, pageSize ?? 20, ct)));
        patient.MapPost("/", async (HttpContext context, int patientId, CreateDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), patientId, request, ct);
            return Results.Created($"/api/dossiers/{result.Dossier.Id}", result);
        });

        var dossier = endpoints.MapGroup("/api/dossiers/{dossierId:guid}")
            .RequireAuthorization(DentistPolicy).WithTags("UC-13/39 S21/S43 Treatment");
        dossier.MapGet("/", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapPost("/plan/draft", async (HttpContext context, Guid dossierId, SavePlanDraftRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SaveDraftAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/plan/commit", async (HttpContext context, Guid dossierId, CommitPlanRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.CommitAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapGet("/plan/history", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapPost("/close", async (HttpContext context, Guid dossierId, CloseDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.CloseAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/reopen", async (HttpContext context, Guid dossierId, ReopenDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ReopenAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/items/{itemId:guid}/warranty/confirm", async (HttpContext context, Guid dossierId,
            Guid itemId, ConfirmWarrantyRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ConfirmWarrantyAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                itemId, request, ct)));

        endpoints.MapGet("/api/patients/me/treatment-summary", async (HttpContext context,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.OwnSummaryAsync(AuthenticationEndpoints.AccountId(context.User), ct)))
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy)
            .WithTags("UC-37/55 S41 Own official treatment summary");
    }
}
