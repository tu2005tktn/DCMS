using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Accounts;
using DCMS.Domain.Features.Accounts;

namespace DCMS.Api.Features.Accounts;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/accounts").RequireAuthorization("ManageAccounts").WithTags("UC-31 Accounts");
        group.MapGet("/", async (IAccountService service, string? search, int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, pageSize ?? 20, ct)));
        group.MapGet("/roles", () => Results.Ok(AccountRoles.Staff));
        group.MapPost("/", async (HttpContext context, CreateStaffRequest request, IAccountService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(AuthenticationEndpoints.AccountId(context.User), request, ct);
            return Results.Created($"/api/accounts/{result.Account.Id}", result);
        });
        group.MapPut("/{id:int}", async (HttpContext context, int id, UpdateStaffRequest request, IAccountService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        group.MapPut("/{id:int}/state", async (HttpContext context, int id, AccountStateRequest request, IAccountService service, CancellationToken ct) =>
            Results.Ok(await service.SetStateAsync(AuthenticationEndpoints.AccountId(context.User), id, request, ct)));
        group.MapGet("/{id:int}/audit", async (int id, IAccountService service, CancellationToken ct) =>
            Results.Ok(await service.AuditAsync(id, ct)));
    }
}
