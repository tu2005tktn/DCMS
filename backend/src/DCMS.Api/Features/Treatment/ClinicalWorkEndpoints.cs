using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Treatment;

namespace DCMS.Api.Features.Treatment;

public static class ClinicalWorkEndpoints
{
    public static void MapClinicalWorkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/clinical-services", async (HttpContext http, int branchId,
            IClinicalWorkService service, CancellationToken ct) => Results.Ok(await service.ServicesAsync(
            AuthenticationEndpoints.AccountId(http.User), branchId, ct)))
            .RequireAuthorization(TreatmentEndpoints.DentistPolicy)
            .WithTags("UC-14 S22 Active clinical services");
        var visits = app.MapGroup("/api/visits/{visitId:guid}")
            .RequireAuthorization(TreatmentEndpoints.DentistPolicy)
            .WithTags("UC-14/39 S22/S63 Clinical work");
        visits.MapGet("/treatment-context", async (HttpContext http, Guid visitId,
            IClinicalWorkService service, CancellationToken ct) => Results.Ok(await service.ContextAsync(
            AuthenticationEndpoints.AccountId(http.User), visitId, ct)));
        visits.MapGet("/reviews", async (HttpContext http, Guid visitId,
            IClinicalWorkService service, CancellationToken ct) => Results.Ok(await service.ReviewsAsync(
            AuthenticationEndpoints.AccountId(http.User), visitId, ct)));
        visits.MapPost("/reviews", async (HttpContext http, Guid visitId,
            CreateReviewRequest request, IClinicalWorkService service, CancellationToken ct) =>
            request.VisitId != visitId ? Results.BadRequest() : Results.Json(await service.CreateReviewAsync(
                AuthenticationEndpoints.AccountId(http.User), request, ct), statusCode: 201));
        visits.MapPost("/activities/drafts", async (HttpContext http, Guid visitId,
            CreateActivityDraftRequest request, IClinicalWorkService service, CancellationToken ct) =>
            request.Activity.VisitId != visitId ? Results.BadRequest() : Results.Json(await service.CreateDraftAsync(
                AuthenticationEndpoints.AccountId(http.User), request, ct), statusCode: 201));
        visits.MapPost("/care-notes", async (HttpContext http, Guid visitId,
            CreateCareNoteRequest request, IClinicalWorkService service, CancellationToken ct) =>
            request.VisitId != visitId ? Results.BadRequest() : Results.Json(await service.AddCareNoteAsync(
                AuthenticationEndpoints.AccountId(http.User), request, ct), statusCode: 201));
        var activities = app.MapGroup("/api/activities").RequireAuthorization(TreatmentEndpoints.DentistPolicy)
            .WithTags("UC-14 S22 Actual treatment");
        activities.MapPut("/{id:guid}/draft", async (HttpContext http, Guid id,
            UpdateActivityDraftRequest request, IClinicalWorkService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateDraftAsync(AuthenticationEndpoints.AccountId(http.User), id, request, ct)));
        activities.MapPost("/{id:guid}/post", async (HttpContext http, Guid id,
            PostActivityRequest request, IClinicalWorkService service, CancellationToken ct) =>
            Results.Ok(await service.PostAsync(AuthenticationEndpoints.AccountId(http.User), id, request, ct)));
        app.MapGet("/api/reviews/{id:guid}", async (HttpContext http, Guid id,
            IClinicalWorkService service, CancellationToken ct) => Results.Ok(await service.ReviewAsync(
            AuthenticationEndpoints.AccountId(http.User), id, ct)))
            .RequireAuthorization(TreatmentEndpoints.DentistPolicy)
            .WithTags("UC-10/39 S63 Postoperative review");

        var care = app.MapGroup("/api/care-tasks").RequireAuthorization()
            .WithTags("UC-25/53 S25/S60 Care task backend");
        care.MapGet("/", async (HttpContext http, int branchId, DateOnly? from, DateOnly? to,
            ICareTaskService service, CancellationToken ct) => Results.Ok(await service.ListAsync(
            AuthenticationEndpoints.AccountId(http.User), branchId, from, to, ct)));
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
        care.MapPost("/{id:guid}/escalations", async (HttpContext http, Guid id,
            EscalateCareRequest request, ICareTaskService service, CancellationToken ct) =>
            Results.Json(await service.EscalateAsync(AuthenticationEndpoints.AccountId(http.User), id,
                request, ct), statusCode: 201));
        var escalations = app.MapGroup("/api/care-escalations").RequireAuthorization()
            .WithTags("UC-53 S60 Clinical handoff");
        escalations.MapPost("/{id:guid}/acknowledge", async (HttpContext http, Guid id,
            ResolveEscalationRequest request, ICareTaskService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeEscalationAsync(AuthenticationEndpoints.AccountId(http.User),
                id, false, request, ct)));
        escalations.MapPost("/{id:guid}/resolve", async (HttpContext http, Guid id,
            ResolveEscalationRequest request, ICareTaskService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeEscalationAsync(AuthenticationEndpoints.AccountId(http.User),
                id, true, request, ct)));
    }
}
