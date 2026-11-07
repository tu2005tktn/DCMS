using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Settings;
using Microsoft.AspNetCore.Mvc;

namespace DCMS.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        static int Actor(HttpContext c) => AuthenticationEndpoints.AccountId(c.User);

        // Access capabilities & Audit
        endpoints.MapGet("/api/system-settings/access-capabilities", async (HttpContext c, ISettingsService s, CancellationToken ct) =>
            Results.Ok(await s.CapabilitiesAsync(Actor(c), ct))).RequireAuthorization().WithTags("S58 Access groups");

        // DCMS-132: merged settings history (docs/specs/S58-DCMS-132-EXPORT-AUDIT.vi.md §3), filtered by section and clinic dates.
        endpoints.MapGet("/api/system-settings/settings/audit", async (HttpContext c, string? section, DateOnly? from, DateOnly? to, int? page, int? pageSize,
            DCMS.Application.Features.SystemSettings.ISettingsAuditService s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(Actor(c), section, from, to, page ?? 1, pageSize ?? 20, ct))).RequireAuthorization().WithTags("S58 Settings");

        // Access groups
        var groups = endpoints.MapGroup("/api/system-settings/access-groups").RequireAuthorization().WithTags("S58 Access groups");
        groups.MapGet("/", async (HttpContext c, ISettingsService s, CancellationToken ct) => Results.Ok(await s.GroupsAsync(Actor(c), ct)));
        groups.MapGet("/grant-definitions", async (HttpContext c, ISettingsAccess access, CancellationToken ct) => { await access.RequireAsync(Actor(c), "access-groups.read", ct); return Results.Ok(SettingsGrants.Definitions); });
        groups.MapGet("/grant-options", async (HttpContext c, ISettingsAccess access, CancellationToken ct) =>
        {
            await access.RequireAsync(Actor(c), "access-groups.read", ct);
            string[] baseRoles = [Domain.Features.Accounts.AccountRoles.Admin, Domain.Features.Accounts.AccountRoles.Dentist, Domain.Features.Accounts.AccountRoles.ClinicAssistant];
            return Results.Ok(SettingsGrants.Definitions.Where(x => x.Key is not ("patients.manage" or "accounts.manage"))
                .Select(x => new GrantOption(x.Key, x.Label, x.RequiredRole, x.Parent, baseRoles.Where(role => SettingsGrants.RoleAllows(x.Key, [role])).ToArray())));
        });
        groups.MapGet("/staff", async (HttpContext c, ISettingsService s, CancellationToken ct) => Results.Ok(await s.StaffAsync(Actor(c), ct)));
        groups.MapGet("/{id:guid}", async (HttpContext c, Guid id, ISettingsService s, CancellationToken ct) =>
            Results.Ok((await s.GroupsAsync(Actor(c), ct)).SingleOrDefault(x => x.Id == id) ?? throw Infrastructure.Features.Settings.SettingsAccess.Error("MSG113", 404)));
        groups.MapPost("/", async (HttpContext c, GroupCommand command, ISettingsService s, CancellationToken ct) => Results.Ok(await s.SaveGroupAsync(Actor(c), null, command, ct)));
        groups.MapPut("/{id:guid}", async (HttpContext c, Guid id, GroupCommand command, ISettingsService s, CancellationToken ct) => Results.Ok(await s.SaveGroupAsync(Actor(c), id, command, ct)));
        groups.MapPost("/{id:guid}/preview", async (HttpContext c, Guid id, GroupCommand command, ISettingsService s, CancellationToken ct) => Results.Ok(await s.GroupPreviewAsync(Actor(c), id, command, ct)));
        groups.MapPost("/{id:guid}/state", async (HttpContext c, Guid id, StateCommand command, ISettingsService s, CancellationToken ct) => Results.Ok(await s.GroupStateAsync(Actor(c), id, command, ct)));
        endpoints.MapPut("/api/system-settings/staff/{employeeId:int}/access-group", async (HttpContext c, int employeeId, AssignGroup command, ISettingsService s, CancellationToken ct) =>
            Results.Ok(await s.AssignAsync(Actor(c), employeeId, command, ct))).RequireAuthorization();

        // Branches & Chairs (DCMS-124)
        void MapBranchRoutes(RouteGroupBuilder branchGroup)
        {
            branchGroup.MapGet("/", async (HttpContext c, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.BranchesAsync(Actor(c), ct)));

            branchGroup.MapGet("/{id:int}", async (HttpContext c, int id, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.BranchAsync(Actor(c), id, ct)));

            branchGroup.MapPost("/", async (HttpContext c, SaveBranchCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SaveBranchAsync(Actor(c), null, command, ct)));

            branchGroup.MapPut("/{id:int}", async (HttpContext c, int id, SaveBranchCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SaveBranchAsync(Actor(c), id, command, ct)));

            branchGroup.MapPost("/{id:int}/state-preview", async (HttpContext c, int id, BranchStateCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.BranchStatePreviewAsync(Actor(c), id, command.Active, ct)));

            branchGroup.MapPost("/{id:int}/state", async (HttpContext c, int id, BranchStateCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SetBranchStateAsync(Actor(c), id, command, ct)));

            branchGroup.MapPost("/{id:int}/logo", async (HttpContext c, int id, IFormFile file, IBranchManagementService s, CancellationToken ct) =>
            {
                if (file == null || file.Length == 0) return Results.BadRequest(DCMS.Application.Common.SystemMessages.Get("MSG02", "Tệp logo") with { Field = "file" });
                await using var stream = file.OpenReadStream();
                var result = await s.UploadLogoAsync(Actor(c), id, stream, file.FileName, file.ContentType, file.Length, ct);
                return Results.Ok(result);
            }).DisableAntiforgery();

            branchGroup.MapGet("/{id:int}/logo/{assetId:guid}", async (HttpContext c, int id, Guid assetId, IBranchManagementService s, CancellationToken ct) =>
            {
                var logo = await s.LogoAsync(Actor(c), id, assetId, ct);
                c.Response.Headers.XContentTypeOptions = "nosniff";
                c.Response.Headers.CacheControl = "private, max-age=300";
                return Results.File(logo.Bytes, logo.ContentType);
            });

            // Chairs
            branchGroup.MapGet("/{id:int}/chairs", async (HttpContext c, int id, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.ChairsAsync(Actor(c), id, ct)));

            branchGroup.MapPost("/{id:int}/chairs", async (HttpContext c, int id, SaveChairCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SaveChairAsync(Actor(c), id, null, command, ct)));

            branchGroup.MapPut("/{id:int}/chairs/{chairId:int}", async (HttpContext c, int id, int chairId, SaveChairCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SaveChairAsync(Actor(c), id, chairId, command, ct)));

            branchGroup.MapPost("/{id:int}/chairs/{chairId:int}/state-preview", async (HttpContext c, int id, int chairId, ChairStateCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.ChairStatePreviewAsync(Actor(c), id, chairId, command.Active, ct)));

            branchGroup.MapPost("/{id:int}/chairs/{chairId:int}/state", async (HttpContext c, int id, int chairId, ChairStateCommand command, IBranchManagementService s, CancellationToken ct) =>
                Results.Ok(await s.SetChairStateAsync(Actor(c), id, chairId, command, ct)));
        }

        MapBranchRoutes(endpoints.MapGroup("/api/system-settings/branches").RequireAuthorization().WithTags("S58 Branches"));
        MapBranchRoutes(endpoints.MapGroup("/api/settings/branches").RequireAuthorization().WithTags("S58 Branches"));
    }
}
