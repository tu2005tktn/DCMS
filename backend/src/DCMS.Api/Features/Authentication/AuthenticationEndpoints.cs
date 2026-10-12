using System.Security.Claims;
using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DCMS.Api.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("UC-01 Authentication");
        group.MapPost("/sign-in", async (SignInRequest request, IAuthenticationService service, CancellationToken ct) =>
            Results.Ok(await service.SignInAsync(request, ct))).AllowAnonymous();
        group.MapPost("/register", async (RegisterRequest request, IRegistrationService service, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(request, ct);
            return Results.Json(result, statusCode: StatusCodes.Status201Created);
        }).AllowAnonymous().WithTags("UC-33 Registration");
        group.MapGet("/me", (HttpContext context) => Results.Ok((CurrentUser)context.Items["CurrentUser"]!)).RequireAuthorization();
        group.MapPost("/sign-out", async (HttpContext context, IAuthenticationService service, CancellationToken ct) =>
        {
            await service.SignOutAsync(AccountId(context.User), Guid.Parse(context.User.FindFirstValue("sid")!), ct);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapGet("/policy", (IOptions<IdentityOptions> identity, IOptions<JwtSettings> jwt) =>
        {
            var policy = identity.Value.Password;
            return Results.Ok(new CredentialPolicy(policy.RequiredLength, policy.RequireDigit, policy.RequireLowercase,
                policy.RequireUppercase, policy.RequireNonAlphanumeric, jwt.Value.SessionMinutes));
        }).AllowAnonymous();
    }

    public static int AccountId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);
}
