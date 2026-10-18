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

        // Resend. Always the same neutral answer: cooldown, cap and rate limits are enforced
        // server-side, but the response carries no field that would reveal whether an account
        // exists, whether a proof is still valid, or whether a message was actually sent.
        group.MapPost("/resend", async (RecoveryRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            await service.ResendAsync(request.Identifier, ip, ct);
            return Results.Ok(SystemMessages.Get("MSG12"));
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

        // Customer Web/Mobile use an email-only Public boundary. The staff S03 routes above keep
        // their existing contract and S04 remains separate.
        var customer = endpoints.MapGroup("/api/customer/recovery")
            .WithTags("UC-02 Public email recovery");
        customer.MapPost("/request", async (RecoveryRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            await service.RequestAsync(request.Identifier, context.Connection.RemoteIpAddress?.ToString(), ct,
                publicOnly: true);
            return Results.Ok(SystemMessages.Get("MSG12"));
        }).AllowAnonymous();
        customer.MapPost("/resend", async (RecoveryRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            await service.ResendAsync(request.Identifier, context.Connection.RemoteIpAddress?.ToString(), ct,
                publicOnly: true);
            return Results.Ok(SystemMessages.Get("MSG12"));
        }).AllowAnonymous();
        customer.MapPost("/verify", async (RecoveryVerifyRequest request, IAccountRecoveryService service,
            HttpContext context, CancellationToken ct) =>
        {
            var result = await service.VerifyAsync(request.Identifier, request.Proof,
                context.Connection.RemoteIpAddress?.ToString(), ct, publicOnly: true);
            return result.Valid ? Results.Ok(new RecoveryGrantResponse(result.ResetGrant!))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        }).AllowAnonymous();
        customer.MapPost("/reset", async (RecoveryResetRequest request, IAccountRecoveryService service,
            CancellationToken ct) =>
        {
            var result = await service.ResetAsync(request.Identifier, request.ResetGrant,
                request.NewPassword, request.ConfirmPassword, ct, publicOnly: true);
            return result.Succeeded ? Results.Ok(SystemMessages.Get("MSG13"))
                : Results.BadRequest(SystemMessages.Get("MSG108"));
        }).AllowAnonymous();

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
        // Admin-only because for an anonymous caller it would confirm that an account exists.
        admin.MapGet("/status/{accountId:int}", async (HttpContext context, int accountId,
            IAccountRecoveryService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(AccountId(context.User), accountId, ct)));
    }

    public static int AccountId(ClaimsPrincipal user) => AuthenticationEndpoints.AccountId(user);
}

public sealed record RecoveryRequest(string Identifier);
public sealed record RecoveryVerifyRequest(string Identifier, string Proof);
public sealed record RecoveryResetRequest(string Identifier, string ResetGrant, string NewPassword, string ConfirmPassword);
public sealed record RecoveryAdminResetRequest(string NewPassword, string ConfirmPassword);
public sealed record RecoveryGrantResponse(string ResetGrant);
public sealed record AdminRecoveryResponse(int AccountId, string ChannelType, string Destination, int? RetryAfterSeconds);
