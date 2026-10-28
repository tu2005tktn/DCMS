using DCMS.Api.Features.Authentication;
using DCMS.Application.Features.Settings;

namespace DCMS.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        static int Actor(HttpContext c)=>AuthenticationEndpoints.AccountId(c.User);
        var settings=endpoints.MapGroup("/api/settings").RequireAuthorization().WithTags("S58 System settings");
        settings.MapGet("/capabilities",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.CapabilitiesAsync(Actor(c),ct)));
        settings.MapGet("/general",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GeneralAsync(Actor(c),ct)));
        settings.MapPost("/general/preview",async(HttpContext c,GeneralCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GeneralPreviewAsync(Actor(c),command,ct)));
        settings.MapPut("/general",async(HttpContext c,GeneralCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveGeneralAsync(Actor(c),command,ct)));
        settings.MapPost("/general/restore-preview",async(HttpContext c,RestoreSettings command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.RestorePreviewAsync(Actor(c),command,ct)));
        settings.MapPost("/general/restore",async(HttpContext c,RestoreSettings command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.RestoreAsync(Actor(c),command,ct)));
        settings.MapGet("/catalogs/{kind}",async(HttpContext c,string kind,string? search,string? status,int? page,int? pageSize,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.CatalogAsync(Actor(c),kind,search,status??"Active",page??1,pageSize??20,ct)));
        settings.MapPost("/catalogs/{kind}",async(HttpContext c,string kind,ReferenceCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveReferenceAsync(Actor(c),kind,null,command,ct)));
        settings.MapGet("/catalogs/{kind}/{id:guid}",async(HttpContext c,string kind,Guid id,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.ReferenceAsync(Actor(c),kind,id,ct)));
        settings.MapPut("/catalogs/{kind}/{id:guid}",async(HttpContext c,string kind,Guid id,ReferenceCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveReferenceAsync(Actor(c),kind,id,command,ct)));
        settings.MapPost("/catalogs/{kind}/{id:guid}/state-preview",async(HttpContext c,string kind,Guid id,StateCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.ReferenceStatePreviewAsync(Actor(c),kind,id,command,ct)));
        settings.MapPost("/catalogs/{kind}/{id:guid}/state",async(HttpContext c,string kind,Guid id,StateCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.ReferenceStateAsync(Actor(c),kind,id,command,ct)));
        settings.MapGet("/audit",async(HttpContext c,string? section,int? page,int? pageSize,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.AuditAsync(Actor(c),section,page??1,pageSize??20,ct)));
        endpoints.MapGet("/api/reference-options",async(HttpContext c,string kind,string context,int branchId,string? search,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.OptionsAsync(Actor(c),kind,context,branchId,search,ct))).RequireAuthorization();
        var groups=endpoints.MapGroup("/api/access-groups").RequireAuthorization().WithTags("S58 Access groups");
        groups.MapGet("/",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupsAsync(Actor(c),ct)));
        groups.MapGet("/grant-definitions",async(HttpContext c,ISettingsAccess access,CancellationToken ct)=>{await access.RequireAsync(Actor(c),"access-groups.read",ct);return Results.Ok(SettingsGrants.Definitions);});
        groups.MapGet("/staff",async(HttpContext c,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.StaffAsync(Actor(c),ct)));
        groups.MapGet("/{id:guid}",async(HttpContext c,Guid id,ISettingsService s,CancellationToken ct)=>Results.Ok((await s.GroupsAsync(Actor(c),ct)).SingleOrDefault(x=>x.Id==id)??throw Infrastructure.Features.Settings.SettingsAccess.Error("MSG113",404)));
        groups.MapPost("/",async(HttpContext c,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveGroupAsync(Actor(c),null,command,ct)));
        groups.MapPut("/{id:guid}",async(HttpContext c,Guid id,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.SaveGroupAsync(Actor(c),id,command,ct)));
        groups.MapPost("/{id:guid}/preview",async(HttpContext c,Guid id,GroupCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupPreviewAsync(Actor(c),id,command,ct)));
        groups.MapPost("/{id:guid}/state",async(HttpContext c,Guid id,StateCommand command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.GroupStateAsync(Actor(c),id,command,ct)));
        endpoints.MapPut("/api/staff/{employeeId:int}/access-group",async(HttpContext c,int employeeId,AssignGroup command,ISettingsService s,CancellationToken ct)=>Results.Ok(await s.AssignAsync(Actor(c),employeeId,command,ct))).RequireAuthorization();
    }
}
