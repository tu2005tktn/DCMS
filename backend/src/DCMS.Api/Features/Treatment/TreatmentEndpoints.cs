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

        dossier.MapPost("/plan/from-template", async (HttpContext context, Guid dossierId,
            ApplyTemplateRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.ApplyTemplateAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapGet("/steps", async (HttpContext context, Guid dossierId,
            ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.StepsAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapPost("/steps", async (HttpContext context, Guid dossierId, AddPatientStepRequest request,
            ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.AddPatientStepAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapGet("/steps/{stepId:guid}/readiness", async (HttpContext context, Guid dossierId, Guid stepId,
            ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok((await service.StepsAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct))
                .SingleOrDefault(x => x.Id == stepId)));
        dossier.MapPost("/steps/{stepId:guid}/progress", async (HttpContext context, Guid dossierId, Guid stepId,
            StepProgressRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.ProgressAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, stepId, request, ct)));
        dossier.MapPost("/steps/{stepId:guid}/hold", async (HttpContext context, Guid dossierId, Guid stepId,
            HoldStepRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.HoldAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, stepId, request, ct)));
        dossier.MapPost("/steps/{stepId:guid}/release-hold", async (HttpContext context, Guid dossierId, Guid stepId,
            HoldStepRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.ReleaseHoldAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, stepId, request, ct)));
        dossier.MapPost("/steps/{stepId:guid}/complete", async (HttpContext context, Guid dossierId, Guid stepId,
            CompleteStepRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.CompleteStepAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, stepId, request, ct)));

        var templates = endpoints.MapGroup("/api/treatment-templates")
            .RequireAuthorization(DentistPolicy).WithTags("UC-51 S58 Treatment templates");
        templates.MapGet("/", async (HttpContext context, int branchId, ITreatmentProgressionService service,
            CancellationToken ct) => Results.Ok(await service.ListTemplatesAsync(
                AuthenticationEndpoints.AccountId(context.User), branchId, ct)));
        templates.MapPost("/", async (HttpContext context, CreateTemplateRequest request,
            ITreatmentProgressionService service, CancellationToken ct) => Results.Json(await service.CreateTemplateAsync(
                AuthenticationEndpoints.AccountId(context.User), request, ct), statusCode: 201));
        templates.MapGet("/{id:guid}", async (HttpContext context, Guid id, ITreatmentProgressionService service,
            CancellationToken ct) => Results.Ok(await service.GetTemplateAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        templates.MapPost("/{id:guid}/versions", async (HttpContext context, Guid id, NewTemplateVersionRequest request,
            ITreatmentProgressionService service, CancellationToken ct) => Results.Ok(await service.NewVersionAsync(
                AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        templates.MapPost("/{id:guid}/versions/{number:int}/draft", async (HttpContext context, Guid id, int number,
            UpdateTemplateDraftRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateDraftAsync(AuthenticationEndpoints.AccountId(context.User), id,
                number, request, ct)));
        templates.MapPost("/{id:guid}/versions/{number:int}/activate", async (HttpContext context, Guid id, int number,
            TemplateStateRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.TemplateStateAsync(AuthenticationEndpoints.AccountId(context.User), id,
                number, "Active", request, ct)));
        templates.MapPost("/{id:guid}/versions/{number:int}/retire", async (HttpContext context, Guid id, int number,
            TemplateStateRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.TemplateStateAsync(AuthenticationEndpoints.AccountId(context.User), id,
                number, "Retired", request, ct)));

        var instructions = endpoints.MapGroup("/api/follow-up-instructions")
            .RequireAuthorization(DentistPolicy).WithTags("UC-17 S24 Follow-up instructions");
        instructions.MapGet("/", async (HttpContext context, int patientId, ITreatmentProgressionService service,
            CancellationToken ct) => Results.Ok(await service.ListFollowUpsAsync(
                AuthenticationEndpoints.AccountId(context.User), patientId, ct)));
        instructions.MapPost("/", async (HttpContext context, CreateFollowUpRequest request,
            ITreatmentProgressionService service, CancellationToken ct) => Results.Json(await service.CreateFollowUpAsync(
                AuthenticationEndpoints.AccountId(context.User), request, ct), statusCode: 201));
        instructions.MapGet("/{id:guid}", async (HttpContext context, Guid id, ITreatmentProgressionService service,
            CancellationToken ct) => Results.Ok(await service.GetFollowUpAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        instructions.MapPost("/{id:guid}/revise-window", async (HttpContext context, Guid id,
            ReviseFollowUpRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
            Results.Ok(await service.ReviseFollowUpAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        foreach (var action in new[] { "complete", "cancel", "supersede" })
        {
            var selected = action;
            instructions.MapPost("/{id:guid}/" + selected, async (HttpContext context, Guid id,
                FollowUpActionRequest request, ITreatmentProgressionService service, CancellationToken ct) =>
                Results.Ok(await service.ChangeFollowUpAsync(AuthenticationEndpoints.AccountId(context.User), id,
                    selected, request, ct)));
        }

        endpoints.MapGet("/api/patients/me/treatment-summary", async (HttpContext context,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.OwnSummaryAsync(AuthenticationEndpoints.AccountId(context.User), ct)))
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy)
            .WithTags("UC-37/55 S41 Own official treatment summary");
    }
}
