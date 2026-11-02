using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Treatment;
using Microsoft.AspNetCore.Mvc;
namespace DCMS.Api.Features.Treatment;
public static class ItemPlanEndpoints
{
    public static void MapItemPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/treatment-templates/item-plans").RequireAuthorization(TreatmentEndpoints.DentistPolicy).WithTags("Reusable treatment item plans");
        group.MapGet("", async (HttpContext c, IItemPlanService s, int branchId, string? keyword, int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(AuthenticationEndpoints.AccountId(c.User), branchId, keyword, page ?? 1, pageSize ?? 20, ct)));
        group.MapGet("/options", async (HttpContext c, IItemPlanService s, int branchId, CancellationToken ct) => Results.Ok(await s.OptionsAsync(AuthenticationEndpoints.AccountId(c.User), branchId, ct)));
        group.MapGet("/{id:guid}", async (HttpContext c, IItemPlanService s, Guid id, CancellationToken ct) => Results.Ok(await s.GetAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        group.MapPost("", async (HttpContext c, IItemPlanService s, ItemPlanCreate r, CancellationToken ct) => Results.Ok(await s.CreateAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        group.MapPut("/{id:guid}", async (HttpContext c, IItemPlanService s, Guid id, ItemPlanRename r, CancellationToken ct) => Results.Ok(await s.RenameAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        group.MapDelete("/{id:guid}", async (HttpContext c, IItemPlanService s, Guid id, [FromBody] ItemPlanDelete r, CancellationToken ct) => Results.Ok(await s.DeleteAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        group.MapPost("/{id:guid}/items", async (HttpContext c, IItemPlanService s, Guid id, ItemPlanRowInput r, CancellationToken ct) => Results.Ok(await s.SaveRowAsync(AuthenticationEndpoints.AccountId(c.User), id, null, r, ct)));
        group.MapPut("/{id:guid}/items/{itemId:guid}", async (HttpContext c, IItemPlanService s, Guid id, Guid itemId, ItemPlanRowInput r, CancellationToken ct) => Results.Ok(await s.SaveRowAsync(AuthenticationEndpoints.AccountId(c.User), id, itemId, r, ct)));
        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (HttpContext c, IItemPlanService s, Guid id, Guid itemId, [FromBody] ItemPlanDelete r, CancellationToken ct) => Results.Ok(await s.DeleteRowAsync(AuthenticationEndpoints.AccountId(c.User), id, itemId, r, ct)));
    }
}
