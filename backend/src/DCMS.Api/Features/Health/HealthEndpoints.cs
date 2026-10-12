using DCMS.Application.Abstractions.Persistence;

namespace DCMS.Api.Features.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (IDatabaseConnectionCheck database, CancellationToken cancellationToken) =>
        {
            try
            {
                var connected = await database.CanConnectAsync(cancellationToken);
                var response = new HealthResponse("healthy", connected ? "healthy" : "unavailable");
                return connected ? Results.Ok(response) : Results.Json(response, statusCode: 503);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new HealthResponse("healthy", "unavailable"), statusCode: 503);
            }
        })
        .AllowAnonymous()
        .WithName("GetHealth")
        .WithSummary("Check API and PostgreSQL connectivity");

        return endpoints;
    }

    private sealed record HealthResponse(string Api, string Database);
}
