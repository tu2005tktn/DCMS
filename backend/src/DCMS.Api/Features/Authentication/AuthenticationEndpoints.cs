using System.Security.Claims;
using DCMS.Application.Common;
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
        group.MapGet("/me", (HttpContext context) => Results.Ok((CurrentUser)context.Items["CurrentUser"]!)).RequireAuthorization();
        group.MapPost("/sign-out", async (HttpContext context, IAuthenticationService service, CancellationToken ct) =>
        {
            await service.SignOutAsync(AccountId(context.User), Guid.Parse(context.User.FindFirstValue("sid")!), ct);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapPost("/refresh", async (RefreshRequest request, IAuthenticationService service, CancellationToken ct) =>
        {
            // Endpoint này được gọi khi access token hết hạn — không cần Authorization.
            // Chỉ chấp nhận khi client mang refresh token hợp lệ.
            var response = await service.RefreshAsync(request, ct);
            return Results.Ok(response);
        }).AllowAnonymous();
        group.MapPost("/sign-out-all", async (HttpContext context, SignOutAllRequest? body, IAuthenticationService service, CancellationToken ct) =>
        {
            await service.SignOutAllAsync(AccountId(context.User),
                Guid.Parse(context.User.FindFirstValue("sid")!),
                body?.Reason, ct);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapGet("/sessions", async (HttpContext context, IAuthenticationService service, CancellationToken ct) =>
            Results.Ok(await service.ListSessionsAsync(AccountId(context.User),
                Guid.Parse(context.User.FindFirstValue("sid")!), ct))).RequireAuthorization();
        group.MapGet("/policy", (IOptions<IdentityOptions> identity, IOptions<JwtSettings> jwt) =>
        {
            var policy = identity.Value.Password;
            return Results.Ok(new CredentialPolicy(policy.RequiredLength, policy.RequireDigit, policy.RequireLowercase,
                policy.RequireUppercase, policy.RequireNonAlphanumeric, jwt.Value.SessionMinutes));
        }).AllowAnonymous();
    }

    public static int AccountId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);
}

public sealed record SignOutAllRequest(string? Reason);
