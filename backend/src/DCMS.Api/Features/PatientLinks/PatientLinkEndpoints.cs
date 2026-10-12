using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.PatientLinks;

namespace DCMS.Api.Features.PatientLinks;

public static class PatientLinkEndpoints
{
    public const string PublicAccountPolicy = "PublicAccount";
    public const string VerifyPolicy = "VerifyPatientLinks";

    public static void MapPatientLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // S07: the subject is always the account of the current session.
        var self = endpoints.MapGroup("/api/patient-links/me").RequireAuthorization(PublicAccountPolicy).WithTags("UC-36 S07 Account link");
        self.MapGet("/", async (HttpContext context, IPatientLinkService service, CancellationToken ct) =>
            Results.Ok(await service.OverviewAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        self.MapPost("/requests", async (HttpContext context, SubmitLinkRequest request, IPatientLinkService service, CancellationToken ct) =>
        {
            var result = await service.SubmitAsync(AuthenticationEndpoints.AccountId(context.User), request, ct);
            return result.Created ? Results.Created($"/api/patient-links/me/requests/{result.Request.Id}", result) : Results.Ok(result);
        });
        self.MapGet("/requests/{id:int}", async (HttpContext context, int id, IPatientLinkService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(AuthenticationEndpoints.AccountId(context.User), id, ct)));
        self.MapPost("/requests/{id:int}/withdraw", async (HttpContext context, int id, WithdrawLinkRequest request, IPatientLinkService service, CancellationToken ct) =>
            Results.Ok(await service.WithdrawAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        self.MapGet("/record", async (HttpContext context, IPatientLinkService service, CancellationToken ct) =>
            Results.Ok(await service.OwnRecordAsync(AuthenticationEndpoints.AccountId(context.User), ct)));

        // S08: Clinic Assistant only.
        var review = endpoints.MapGroup("/api/patient-link-reviews").RequireAuthorization(VerifyPolicy).WithTags("UC-36 S08 Link verification");
        review.MapGet("/", async (IPatientLinkReviewService service, string? status, DateOnly? from, DateOnly? to, string? keyword,
                int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await service.QueueAsync(status, from, to, keyword, page ?? 1, pageSize ?? 20, ct)));
        review.MapGet("/{id:int}", async (int id, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.DetailAsync(id, ct)));
        review.MapGet("/{id:int}/patients", async (int id, string? keyword, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.PatientsAsync(id, keyword, ct)));
        review.MapPost("/{id:int}/verify", async (HttpContext context, int id, VerifyLinkRequest request, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.VerifyAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        review.MapPost("/{id:int}/reject", async (HttpContext context, int id, RejectLinkRequest request, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.RejectAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        review.MapPost("/{id:int}/revoke", async (HttpContext context, int id, RevokeLinkRequest request, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.RevokeAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        review.MapGet("/{id:int}/audit", async (int id, IPatientLinkReviewService service, CancellationToken ct) =>
            Results.Ok(await service.AuditAsync(id, ct)));
    }
}
