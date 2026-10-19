using DCMS.Application.Features.Authentication;

namespace DCMS.Api.Features.Authentication;

public static class EmailVerificationEndpoints
{
    public static void MapEmailVerificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/me/email-verification")
            .WithTags("Public email verification").RequireAuthorization();
        group.MapGet("", async (HttpContext context, IEmailVerificationService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        group.MapPost("/request", async (HttpContext context, IEmailVerificationService service, CancellationToken ct) =>
            Results.Ok(await service.RequestAsync(AuthenticationEndpoints.AccountId(context.User), ct)));
        group.MapPost("/confirm", async (HttpContext context, EmailVerificationConfirmRequest request,
            IEmailVerificationService service, CancellationToken ct) =>
            Results.Ok(await service.ConfirmAsync(AuthenticationEndpoints.AccountId(context.User), request.Code, ct)));
    }
}
