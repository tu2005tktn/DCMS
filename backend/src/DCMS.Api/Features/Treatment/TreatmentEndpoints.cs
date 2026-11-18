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
        patient.MapGet("/create-context", async (HttpContext context, int patientId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.CreateContextAsync(AuthenticationEndpoints.AccountId(context.User), patientId, ct)));
        patient.MapPost("/quick", async (HttpContext context, int patientId, QuickDossierRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.QuickCreateAsync(AuthenticationEndpoints.AccountId(context.User), patientId, request, ct)));
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
        endpoints.MapGet("/api/patients/{patientId:int}/next-visit-candidates", async (HttpContext context, int patientId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.NextVisitCandidatesAsync(
                AuthenticationEndpoints.AccountId(context.User), patientId, ct))).RequireAuthorization(DentistPolicy)
            .WithTags("Next plan item to book");
        patient.MapGet("/summary", async (HttpContext context, int patientId, int? branchId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.SummaryAsync(
                AuthenticationEndpoints.AccountId(context.User), patientId, branchId, ct)));

        var dossier = endpoints.MapGroup("/api/dossiers/{dossierId:guid}")
            .RequireAuthorization(DentistPolicy).WithTags("UC-13/39 S21/S43 Treatment");
        dossier.MapPost("/sheet-items/from-item-plan", async (HttpContext context, Guid dossierId, ImportItemPlanRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ImportItemPlanAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
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
        var labWork = endpoints.MapGroup("/api/lab-orders").RequireAuthorization();
        labWork.MapGet("", async (HttpContext context, string? status, int? branchId, Guid? labProviderId, int? dentistId, DateOnly? from, DateOnly? to,
            string? q, ITreatmentDossierService service, CancellationToken ct, int page = 1, int pageSize = 20) =>
            Results.Ok(await service.LabWorkAsync(AuthenticationEndpoints.AccountId(context.User), status, branchId, labProviderId, dentistId, from, to, q, page, pageSize, ct)));
        labWork.MapGet("/{id:guid}/history", async (HttpContext context, Guid id, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.LabWorkHistoryAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        labWork.MapPut("/{id:guid}/status", async (HttpContext context, Guid id, ChangeLabStatusRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeLabStatusAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        dossier.MapGet("/lab-orders", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.LabOrdersAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapGet("/sheet-activities/{activityId:guid}/materials", async (HttpContext context, Guid dossierId, Guid activityId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ActivityMaterialsAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, ct)));
        dossier.MapGet("/sheet-activities/{activityId:guid}/lab-order", async (HttpContext context, Guid dossierId, Guid activityId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.LabOrderContextAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, ct)));
        dossier.MapPut("/sheet-activities/{activityId:guid}/lab-order", async (HttpContext context, Guid dossierId, Guid activityId, SaveLabOrderRequest request, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.SaveLabOrderAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, request, ct)));
        dossier.MapDelete("/sheet-activities/{activityId:guid}/lab-order", async (HttpContext context, Guid dossierId, Guid activityId, ITreatmentDossierService service, CancellationToken ct) =>
        {
            await service.DeleteLabOrderAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, activityId, ct);
            return Results.NoContent();
        });
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
        dossier.MapGet("/plan/items/{itemId:guid}/planned-date-preview", async (HttpContext context, Guid dossierId, Guid itemId,
            DateOnly plannedOn, ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.PlannedDatePreviewAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, itemId, plannedOn, ct)));
        dossier.MapPost("/plan/planned-dates", async (HttpContext context, Guid dossierId, ApplyPlannedDatesRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.ApplyPlannedDatesAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapDelete("/", async (HttpContext context, Guid dossierId, [FromBody] DeleteDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.DeleteAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
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
        dossier.MapGet("/plan/preview", async (HttpContext context, Guid dossierId,
            ITreatmentDossierService service, CancellationToken ct) => Results.Ok(await service.PricePreviewAsync(
                AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapGet("/plan/history", async (HttpContext context, Guid dossierId, ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, ct)));
        dossier.MapPost("/close", async (HttpContext context, Guid dossierId, CloseDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.CloseAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));
        dossier.MapPost("/reopen", async (HttpContext context, Guid dossierId, ReopenDossierRequest request,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.ReopenAsync(AuthenticationEndpoints.AccountId(context.User), dossierId, request, ct)));

        endpoints.MapGet("/api/patients/me/treatment-summary", async (HttpContext context,
            ITreatmentDossierService service, CancellationToken ct) =>
            Results.Ok(await service.OwnSummaryAsync(AuthenticationEndpoints.AccountId(context.User), ct)))
            .RequireAuthorization(PatientLinkEndpoints.PublicAccountPolicy)
            .WithTags("UC-37/55 S41 Own official treatment summary");
    }
}
