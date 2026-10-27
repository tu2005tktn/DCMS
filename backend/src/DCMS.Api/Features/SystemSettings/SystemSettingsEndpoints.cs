using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.SystemSettings;
using Microsoft.AspNetCore.Mvc;
namespace DCMS.Api.Features.SystemSettings;
public static class SystemSettingsEndpoints
{
    public static void MapSystemSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/api/system-settings/material-categories").RequireAuthorization().WithTags("System settings");
        groups.MapGet("/", async (HttpContext c, string? q, int? page, int? pageSize, ISystemCatalogService s, CancellationToken ct) =>
            Results.Ok(await s.CategoriesAsync(AuthenticationEndpoints.AccountId(c.User), q, page ?? 1, pageSize ?? 20, ct)));
        groups.MapGet("/{id:guid}", async (HttpContext c, Guid id, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.CategoryAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        groups.MapPost("/", async (HttpContext c, CreateCategoryRequest r, ISystemCatalogService s, CancellationToken ct) => { var result = await s.CreateCategoryAsync(AuthenticationEndpoints.AccountId(c.User), r, ct); return Results.Created($"/api/system-settings/material-categories/{result.Id}", result); });
        groups.MapPut("/{id:guid}", async (HttpContext c, Guid id, UpdateCategoryRequest r, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.UpdateCategoryAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        groups.MapDelete("/{id:guid}", async (HttpContext c, Guid id, [FromBody] DeleteCatalogRequest r, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.DeleteCategoryAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        var items = app.MapGroup("/api/system-settings/materials").RequireAuthorization().WithTags("System settings");
        items.MapGet("/", async (HttpContext c, string? q, Guid? categoryId, int? page, int? pageSize, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.MaterialsAsync(AuthenticationEndpoints.AccountId(c.User), q, categoryId, page ?? 1, pageSize ?? 20, ct)));
        items.MapGet("/{id:guid}", async (HttpContext c, Guid id, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.MaterialAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        items.MapPost("/", async (HttpContext c, CreateMaterialRequest r, ISystemCatalogService s, CancellationToken ct) => { var result = await s.CreateMaterialAsync(AuthenticationEndpoints.AccountId(c.User), r, ct); return Results.Created($"/api/system-settings/materials/{result.Id}", result); });
        items.MapPut("/{id:guid}", async (HttpContext c, Guid id, UpdateMaterialRequest r, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.UpdateMaterialAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        items.MapDelete("/{id:guid}", async (HttpContext c, Guid id, [FromBody] DeleteCatalogRequest r, ISystemCatalogService s, CancellationToken ct) => Results.Ok(await s.DeleteMaterialAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
    }
}
