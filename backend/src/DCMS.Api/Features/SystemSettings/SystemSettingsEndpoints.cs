using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.SystemSettings;
using Microsoft.AspNetCore.Mvc;
namespace DCMS.Api.Features.SystemSettings;
public static class SystemSettingsEndpoints
{
    private static void MapSupply<TInput, TDto>(RouteGroupBuilder settings, string resource) where TInput : class where TDto : class
    {
        var g = settings.MapGroup($"/catalogs/{resource}");
        g.MapGet("/", async (HttpContext c, string? q, string? status, string? type, int? page, int? pageSize, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(AuthenticationEndpoints.AccountId(c.User), q, status, type, page ?? 1, pageSize ?? 20, ct)));
        g.MapGet("/{id:guid}", async (HttpContext c, Guid id, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) => Results.Ok(await s.GetAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
        g.MapPost("/", async (HttpContext c, CreateSupplyRequest<TInput> r, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) =>
            Results.Created($"/api/system-settings/settings/catalogs/{resource}", await s.CreateAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        g.MapPut("/{id:guid}", async (HttpContext c, Guid id, UpdateSupplyRequest<TInput> r, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) => Results.Ok(await s.UpdateAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        g.MapPost("/{id:guid}/state-preview", async (HttpContext c, Guid id, ReferenceStatePreviewRequest r, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) => Results.Ok(await s.StatePreviewAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        g.MapPost("/{id:guid}/state", async (HttpContext c, Guid id, ReferenceStateRequest r, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) => Results.Ok(await s.SetStateAsync(AuthenticationEndpoints.AccountId(c.User), id, r, ct)));
        g.MapGet("/{id:guid}/history", async (HttpContext c, Guid id, ISupplyCatalog<TInput, TDto> s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(AuthenticationEndpoints.AccountId(c.User), id, ct)));
    }

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
        // S58 Thiết lập chung (docs/specs/S58-GENERAL-SETTINGS.md); the service checks the ClinicOwner role on every request.
        var settings = app.MapGroup("/api/system-settings/settings").RequireAuthorization().WithTags("S58 Settings");
        settings.MapGet("/capabilities", async (HttpContext c, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.CapabilitiesAsync(AuthenticationEndpoints.AccountId(c.User), ct)));
        settings.MapGet("/general", async (HttpContext c, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.GetAsync(AuthenticationEndpoints.AccountId(c.User), ct)));
        settings.MapPost("/general/preview", async (HttpContext c, PreviewGeneralRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.PreviewAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        settings.MapPut("/general", async (HttpContext c, SaveGeneralRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.SaveAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        settings.MapGet("/general/history", async (HttpContext c, int? page, int? pageSize, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(AuthenticationEndpoints.AccountId(c.User), page ?? 1, pageSize ?? 20, ct)));
        settings.MapPost("/general/restore-preview", async (HttpContext c, RestorePreviewRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.RestorePreviewAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        settings.MapPost("/general/restore", async (HttpContext c, RestoreGeneralRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.RestoreAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        // DCMS-122 patient code sequence: forward-only, separate from the general payload.
        settings.MapGet("/patient-code-sequence", async (HttpContext c, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.SequenceAsync(AuthenticationEndpoints.AccountId(c.User), ct)));
        settings.MapPost("/patient-code-sequence/preview", async (HttpContext c, PatientCodeSequencePreviewRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.SequencePreviewAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        settings.MapPut("/patient-code-sequence", async (HttpContext c, UpdatePatientCodeSequenceRequest r, IGeneralSettingsService s, CancellationToken ct) => Results.Ok(await s.UpdateSequenceAsync(AuthenticationEndpoints.AccountId(c.User), r, ct)));
        // S58 đợt 4 typed catalogs (docs/specs/S58-SUPPLY-CATALOGS.md); literal segments win over {kind} below.
        MapSupply<ProviderInput, ProviderDto>(settings, "providers");
        MapSupply<LabWorkTypeInput, LabWorkTypeDto>(settings, "lab-work-types");
        settings.MapGet("/catalogs/medicines/export", async (HttpContext c, string? q, string? status, IMedicineCatalog s, CancellationToken ct) =>
            Results.File(await s.ExportAsync(AuthenticationEndpoints.AccountId(c.User), q, status, ct),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"danh-muc-thuoc-{DateTime.UtcNow:yyyyMMdd}.xlsx"));
        MapSupply<MedicineInput, MedicineDto>(settings, "medicines");
        // S58 danh mục tham chiếu (docs/specs/S58-REFERENCE-CATALOGS.md).
        var catalogs = settings.MapGroup("/catalogs/{kind}");
        catalogs.MapGet("/", async (HttpContext c, string kind, string? q, string? status, int? page, int? pageSize, IReferenceCatalogService s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(AuthenticationEndpoints.AccountId(c.User), kind, q, status, page ?? 1, pageSize ?? 20, ct)));
        catalogs.MapGet("/{id:guid}", async (HttpContext c, string kind, Guid id, IReferenceCatalogService s, CancellationToken ct) => Results.Ok(await s.GetAsync(AuthenticationEndpoints.AccountId(c.User), kind, id, ct)));
        catalogs.MapPost("/", async (HttpContext c, string kind, CreateReferenceRequest r, IReferenceCatalogService s, CancellationToken ct) =>
        { var result = await s.CreateAsync(AuthenticationEndpoints.AccountId(c.User), kind, r, ct); return Results.Created($"/api/system-settings/settings/catalogs/{kind}/{result.Id}", result); });
        catalogs.MapPut("/{id:guid}", async (HttpContext c, string kind, Guid id, UpdateReferenceRequest r, IReferenceCatalogService s, CancellationToken ct) => Results.Ok(await s.UpdateAsync(AuthenticationEndpoints.AccountId(c.User), kind, id, r, ct)));
        catalogs.MapPost("/{id:guid}/state-preview", async (HttpContext c, string kind, Guid id, ReferenceStatePreviewRequest r, IReferenceCatalogService s, CancellationToken ct) => Results.Ok(await s.StatePreviewAsync(AuthenticationEndpoints.AccountId(c.User), kind, id, r, ct)));
        catalogs.MapPost("/{id:guid}/state", async (HttpContext c, string kind, Guid id, ReferenceStateRequest r, IReferenceCatalogService s, CancellationToken ct) => Results.Ok(await s.SetStateAsync(AuthenticationEndpoints.AccountId(c.User), kind, id, r, ct)));
        catalogs.MapGet("/{id:guid}/history", async (HttpContext c, string kind, Guid id, IReferenceCatalogService s, CancellationToken ct) => Results.Ok(await s.HistoryAsync(AuthenticationEndpoints.AccountId(c.User), kind, id, ct)));
        app.MapGet("/api/system-settings/reference-options", async (HttpContext c, string? kind, string? search, IReferenceCatalogService s, CancellationToken ct) =>
            Results.Ok(await s.OptionsAsync(AuthenticationEndpoints.AccountId(c.User), kind, search, ct))).RequireAuthorization().WithTags("S58 Settings");
    }
}
