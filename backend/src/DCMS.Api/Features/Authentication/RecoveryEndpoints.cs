using System.Security.Claims;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;

namespace DCMS.Api.Features.Authentication;

public static class RecoveryEndpoints
{
    public static void MapRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth/recovery").WithTags("UC-02 Account Recovery");

        // Step 1: request. Always neutral (MSG12) for a valid-format identifier, whether or not
        // the account exists, so the response cannot be used to enumerate accounts.
        group.MapPost("/request", async (RecoveryRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            await service.RequestAsync(request.Identifier, ip, ct);
            return Results.Ok(SystemMessages.Get("MSG12"));
        }).AllowAnonymous();

        // Step 2: verify the delivered code and receive a short-lived reset grant.
        group.MapPost("/verify", async (RecoveryVerifyRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var result = await service.VerifyAsync(request.Identifier, request.Proof, ip, ct);
            return result.Valid
                ? Results.Ok(new RecoveryGrantResponse(result.ResetGrant!))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        }).AllowAnonymous();

        // Resend inside the cooldown / resend cap; still a neutral response.
        group.MapPost("/resend", async (RecoveryRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var result = await service.ResendAsync(request.Identifier, ip, ct);
            return Results.Ok(new RecoveryResendResponse(result.RetryAfterSeconds));
        }).AllowAnonymous();

        // Step 3: apply the new password with the grant from the verify step.
        group.MapPost("/reset", async (RecoveryResetRequest request, IAccountRecoveryService service,
            CancellationToken ct) =>
        {
            var result = await service.ResetAsync(request.Identifier, request.ResetGrant, request.NewPassword,
                request.ConfirmPassword, ct);
            return result.Succeeded
                ? Results.Ok(SystemMessages.Get("MSG13"))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        }).AllowAnonymous();

        // Current request state so the UI can drive the resend countdown.
        group.MapGet("/status", async (string identifier, IAccountRecoveryService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(identifier, ct))).AllowAnonymous();

        // S04 integration point: an authorized Admin may start the same recovery process for an
        // account. The service re-checks the Admin permission server-side on every call.
        var admin = endpoints.MapGroup("/api/admin/recovery").RequireAuthorization("ManageAccounts")
            .WithTags("UC-31 Admin-assisted Recovery");
        admin.MapPost("/request/{accountId:int}", async (HttpContext context, int accountId,
            IAccountRecoveryService service, CancellationToken ct) =>
        {
            var result = await service.RequestForAccountAsync(AccountId(context.User), accountId, ct);
            return Results.Ok(new AdminRecoveryResponse(result.AccountId, result.ChannelType, result.Destination,
                result.RetryAfterSeconds));
        });
        admin.MapPost("/verify/{accountId:int}", async (HttpContext context, int accountId, RecoveryVerifyRequest request,
            IAccountRecoveryService service, CancellationToken ct) =>
        {
            var result = await service.VerifyForAccountAsync(AccountId(context.User), accountId, request.Proof, ct);
            return result.Valid
                ? Results.Ok(new RecoveryGrantResponse(result.ResetGrant!))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        });
        admin.MapPost("/reset/{accountId:int}", async (HttpContext context, int accountId,
            RecoveryAdminResetRequest request, IAccountRecoveryService service, CancellationToken ct) =>
        {
            var result = await service.ResetForAccountAsync(AccountId(context.User), accountId,
                request.NewPassword, request.ConfirmPassword, ct);
            return result.Succeeded
                ? Results.Ok(SystemMessages.Get("MSG13"))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        });
    }

    public static int AccountId(ClaimsPrincipal user) => AuthenticationEndpoints.AccountId(user);
}

public sealed record RecoveryRequest(string Identifier);
public sealed record RecoveryVerifyRequest(string Identifier, string Proof);
public sealed record RecoveryResetRequest(string Identifier, string ResetGrant, string NewPassword, string ConfirmPassword);
public sealed record RecoveryAdminResetRequest(string NewPassword, string ConfirmPassword);
public sealed record RecoveryGrantResponse(string ResetGrant);
public sealed record RecoveryResendResponse(int? RetryAfterSeconds);
public sealed record AdminRecoveryResponse(int AccountId, string ChannelType, string Destination, int? RetryAfterSeconds);