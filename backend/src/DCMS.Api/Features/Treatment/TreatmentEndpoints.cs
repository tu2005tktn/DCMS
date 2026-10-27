using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Treatment;
using DCMS.Api.Features.PatientLinks;
using Microsoft.AspNetCore.Mvc;

namespace DCMS.Api.Features.Treatment;

public static class TreatmentEndpoints
{
    public const string DentistPolicy = "TreatmentDentist";

    public static void MapTreatmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/patients/{patientId:int}/dossiers/financial-context", async (HttpContext context,
            int patientId, ITreatmentFinancialWorkspace service, CancellationToken ct) => Results.Ok(await service.ListAsync(
                AuthenticationEndpoints.AccountId(context.User), patientId, ct))).RequireAuthorization();
        var prices = endpoints.MapGroup("/api/service-prices").RequireAuthorization().WithTags("S21 reference prices");
        prices.MapGet("/", async (HttpContext context, IServicePriceService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        prices.MapGet("/{serviceId:int}", async (HttpContext context, int serviceId, IServicePriceService service,
            CancellationToken ct) => Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), serviceId, ct)));
        prices.MapPut("/{serviceId:int}", async (HttpContext context, int serviceId, SaveServicePriceRequest request,
            IServicePriceService service, CancellationToken ct) => Results.Ok(await service.SaveAsync(
                AuthenticationEndpoints.AccountId(context.User), serviceId, request, ct)));
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
        patient.MapGet("/summary", async (HttpContext context, int patientId, int? branchId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.SummaryAsync(
                AuthenticationEndpoints.AccountId(context.User), patientId, branchId, ct)));

        var dossier = endpoints.MapGroup("/api/dossiers/{dossierId:guid}")
            .RequireAuthorization(DentistPolicy).WithTags("UC-13/39 S21/S43 Treatment");
        dossier.MapGet("/", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapGet("/sheet-context", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SheetContextAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapGet("/activities", async (HttpContext context, Guid dossierId, Guid? visitId, DateOnly? from, DateOnly? to,
            int? page, int? pageSize, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.ActivitiesAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, visitId, from, to, page ?? 1, pageSize ?? 20, ct)));
        dossier.MapPost("/sheet-drafts/{rowId:guid}/confirm-plan", async (HttpContext context, Guid dossierId, Guid rowId,
            ConfirmSheetPlanRequest request, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.ConfirmSheetPlanAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, rowId, request, ct)));
        dossier.MapPost("/sheet-drafts/{rowId:guid}/confirm-actual", async (HttpContext context, Guid dossierId, Guid rowId,
            ConfirmSheetActualRequest request, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.ConfirmSheetActualAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, rowId, request, ct)));
        dossier.MapPut("/sheet-activities/{activityId:guid}", async (HttpContext context, Guid dossierId, Guid activityId, EditSheetActivityRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.EditSheetActivityAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, request, ct)));
        dossier.MapDelete("/sheet-activities/{activityId:guid}", async (HttpContext context, Guid dossierId, Guid activityId, [FromBody] SheetActivityActionRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.DeleteSheetActivityAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, request, ct)));
        dossier.MapPut("/sheet-items/{itemId:guid}", async (HttpContext context, Guid dossierId, Guid itemId, EditSheetItemRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.EditSheetItemAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapPost("/sheet-items/{itemId:guid}/perform", async (HttpContext context, Guid dossierId, Guid itemId, PerformSheetItemRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.PerformSheetItemAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapPatch("/sheet-items/{itemId:guid}/display-completion", async (HttpContext context, Guid dossierId, Guid itemId, MarkSheetItemRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.MarkSheetItemAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapDelete("/sheet-items/{itemId:guid}", async (HttpContext context, Guid dossierId, Guid itemId, [FromBody] SheetItemActionRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.DeleteSheetItemAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapPatch("/plan/items/{itemId:guid}/schedule-note", async (HttpContext context, Guid dossierId, Guid itemId,
            ScheduleNoteRequest request, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.ScheduleNoteAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapPost("/plan/items/{itemId:guid}/decision", async (HttpContext context, Guid dossierId, Guid itemId,
            DecideSheetItemRequest request, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.DecideSheetItemAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        dossier.MapDelete("/", async (HttpContext context, Guid dossierId, [FromBody] DeleteDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.DeleteAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/display-status", async (HttpContext context, Guid dossierId,
            SetDossierDisplayStatusRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SetDisplayStatusAsync(AuthenticationEndpoints.AccountId(context.User),
                dossierId, request, ct)));
        dossier.MapPatch("/note", async (HttpContext context, Guid dossierId,
            UpdateDossierNoteRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateNoteAsync(AuthenticationEndpoints.AccountId(context.User),
                dossierId, request, ct)));
        dossier.MapGet("/sheet-drafts", async (HttpContext context, Guid dossierId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.SheetDraftsAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapPut("/sheet-drafts/{rowId:guid}", async (HttpContext context, Guid dossierId, Guid rowId,
            SaveSheetDraftRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSheetDraftAsync(AuthenticationEndpoints.AccountId(context.User),
                dossierId, rowId, request, ct)));
        dossier.MapDelete("/sheet-drafts/{rowId:guid}", async (HttpContext context, Guid dossierId, Guid rowId,
            int expectedVersion, ITreatmentDossierService service, CancellationToken ct) =>
        {
            await service.DeleteSheetDraftAsync(AuthenticationEndpoints.AccountId(context.User),
                dossierId, rowId, expectedVersion, ct);
            return Results.NoContent();
        });
        dossier.MapPost("/plan/draft", async (HttpContext context, Guid dossierId, SavePlanDraftRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SaveDraftAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/plan/commit", async (HttpContext context, Guid dossierId, CommitPlanRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.CommitAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapGet("/plan/preview", async (HttpContext context, Guid dossierId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.PricePreviewAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        var commercial = endpoints.MapGroup("/api/commercial/dossiers/{dossierId:guid}")
            .RequireAuthorization().WithTags("S21 commercial review");
        endpoints.MapGet("/api/commercial/dossiers", async (HttpContext context,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.CommercialDossiersAsync(
                AuthenticationEndpoints.AccountId(context.User), ct))).RequireAuthorization()
            .WithTags("S21 commercial review");
        commercial.MapGet("/plan", async (HttpContext context, Guid dossierId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.CommercialPlanAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        commercial.MapPost("/items/{itemId:guid}/discount/decision", async (HttpContext context,
            Guid dossierId, Guid itemId, DecideDiscountRequest request, ITreatmentDossierService service,
            CancellationToken ct) => Results.Ok(await service.DecideDiscountAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, request, ct)));
        commercial.MapPost("/deposit-exception", async (HttpContext context, Guid dossierId,
            DepositExceptionRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SetDepositExceptionAsync(AuthenticationEndpoints.AccountId(context.User),
                dossierId, request, ct)));
        dossier.MapGet("/plan/history", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapGet("/plan/revisions/context", async (HttpContext context, Guid dossierId, Guid itemId,
            ITreatmentRevisionService service, CancellationToken ct) =>
            Results.Ok(await service.ContextAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                itemId, ct)));
        dossier.MapGet("/plan/revision-drafts/{itemId:guid}", async (HttpContext context, Guid dossierId,
            Guid itemId, ITreatmentRevisionService service, CancellationToken ct) =>
            Results.Ok(await service.DraftAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                itemId, ct)));
        dossier.MapPut("/plan/revision-drafts/{itemId:guid}", async (HttpContext context, Guid dossierId,
            Guid itemId, SaveRevisionDraftRequest request, ITreatmentRevisionService service, CancellationToken ct) =>
            Results.Ok(await service.SaveDraftAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                itemId, request, ct)));
        dossier.MapDelete("/plan/revision-drafts/{itemId:guid}", async (HttpContext context, Guid dossierId,
            Guid itemId, ITreatmentRevisionService service, CancellationToken ct) =>
        {
            await service.DeleteDraftAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, ct);
            return Results.NoContent();
        });
        dossier.MapPost("/plan/revisions/preview", async (HttpContext context, Guid dossierId,
            PreviewRevisionRequest request, ITreatmentRevisionService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                request, ct)));
        dossier.MapPost("/plan/revisions", async (HttpContext context, Guid dossierId,
            CommitRevisionRequest request, ITreatmentRevisionService service, CancellationToken ct) =>
            Results.Ok(await service.CommitAsync(AuthenticationEndpoints.AccountId(context.User), dossierId,
                request, ct)));
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
