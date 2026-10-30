using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Settings;

namespace DCMS.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        static int Actor(HttpContext c)=>AuthenticationEndpoints.AccountId(c.User);
        // Merged into /api/system-settings (docs/specs/S58-SETTINGS-MERGE.vi.md): general settings and catalogs live in the
        // system-settings module; only access groups, capabilities and the settings audit remain here under the new prefix.
        endpoints.MapGet("/api/system-settings/access-capabilities",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.CapabilitiesAsync(Actor(c),ct))).RequireAuthorization().WithTags("S58 Access groups");
        endpoints.MapGet("/api/system-settings/settings/audit",async(HttpContext c,string? section,int? page,int? pageSize,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.AuditAsync(Actor(c),section,page??1,pageSize??20,ct))).RequireAuthorization().WithTags("S58 Settings");
        var groups=endpoints.MapGroup("/api/system-settings/access-groups").RequireAuthorization().WithTags("S58 Access groups");
        groups.MapGet("/",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupsAsync(Actor(c),ct)));
        groups.MapGet("/grant-definitions",async(HttpContext c,ISettingsAccess access,CancellationToken ct)=>{await access.RequireAsync(Actor(c),"access-groups.read",ct);return Results.Ok(SettingsGrants.Definitions);});
        groups.MapGet("/staff",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.StaffAsync(Actor(c),ct)));
        groups.MapGet("/{id:guid}",async(HttpContext c,Guid id,ISettingsService s,CancellationToken ct)=>Results.Ok((await s.GroupsAsync(Actor(c),ct)).SingleOrDefault(x=>x.Id==id)??throw Infrastructure.Features.Settings.SettingsAccess.Error("MSG113",404)));
        groups.MapPost("/",async(HttpContext c,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveGroupAsync(Actor(c),null,command,ct)));
        groups.MapPut("/{id:guid}",async(HttpContext c,Guid id,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveGroupAsync(Actor(c),id,command,ct)));
        groups.MapPost("/{id:guid}/preview",async(HttpContext c,Guid id,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupPreviewAsync(Actor(c),id,command,ct)));
        groups.MapPost("/{id:guid}/state",async(HttpContext c,Guid id,StateCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupStateAsync(Actor(c),id,command,ct)));
        endpoints.MapPut("/api/system-settings/staff/{employeeId:int}/access-group",async(HttpContext c,int employeeId,AssignGroup command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.AssignAsync(Actor(c),employeeId,command,ct))).RequireAuthorization();
    }
}
