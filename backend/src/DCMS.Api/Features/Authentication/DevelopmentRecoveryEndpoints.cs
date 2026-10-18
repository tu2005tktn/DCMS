using DCMS.Infrastructure.Features.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DCMS.Api.Features.Authentication;

/// <summary>
/// Development-only support endpoints for UC-02 account recovery.
///
/// The production recovery channel is not configured yet, so the Development contact adapter keeps
/// the delivered proof in process memory. This endpoint exposes it so a developer can complete the
/// flow locally. It is registered only when the host runs in Development, requires an Admin
/// session, and never appears in Production. The proof is never stored in the database, the outbox,
/// the logs or an audit entry.
/// </summary>
public static class DevelopmentRecoveryEndpoints
{
    public static void MapDevelopmentRecoveryEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.MapGet("/api/dev/recovery/proof/{accountId:int}", (int accountId,

            [FromServices] DevelopmentContactDeliveryAdapter adapter) =>
        {
            // Guard: only an Admin can inspect the proof, even in Development.
            var proof = adapter.TryGetProof(accountId);
            if (string.IsNullOrEmpty(proof)) return Results.NotFound();
            return Results.Ok(new { accountId, proof });
        }).RequireAuthorization("ManageAccounts");
    }
}
