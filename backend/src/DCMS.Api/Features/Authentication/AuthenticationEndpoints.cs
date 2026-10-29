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
        group.MapGet("/google/config", (IConfiguration config) =>
        {
            var clientId = config["Google:WebClientId"]?.Trim();
            return Results.Ok(new GooglePublicConfig(!string.IsNullOrWhiteSpace(clientId), clientId));
        }).AllowAnonymous();
        group.MapPost("/google/sign-in", async (GoogleSignInRequest request, IGoogleAuthenticationService service, CancellationToken ct) =>
            Results.Ok(await service.SignInAsync(request, ct))).AllowAnonymous();
        group.MapPost("/register", async (RegisterRequest request, IRegistrationService service, CancellationToken ct) =>
        {
            var result = await service.RegisterAsync(request, ct);
            return Results.Json(result, statusCode: StatusCodes.Status201Created);
        }).AllowAnonymous().WithTags("UC-33 Registration");
        group.MapGet("/me", (HttpContext context) => Results.Ok((CurrentUser)context.Items["CurrentUser"]!)).RequireAuthorization();
        group.MapGet("/session/idle", async (HttpContext context, IStaffIdleService service, CancellationToken ct) =>
            Results.Ok(await service.StatusAsync(AccountId(context.User),
                Guid.Parse(context.User.FindFirstValue("sid")!), ct))).RequireAuthorization();
        group.MapPost("/session/activity", async (HttpContext context, StaffIdleActivityRequest request,
            IStaffIdleService service, CancellationToken ct) =>
            Results.Ok(await service.ActivityAsync(AccountId(context.User),
                Guid.Parse(context.User.FindFirstValue("sid")!), request, ct))).RequireAuthorization();
        endpoints.MapPost("/api/auth/session/idle-preview", async (StaffIdlePreviewRequest request, IStaffIdleService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewAsync(request, ct)));
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
