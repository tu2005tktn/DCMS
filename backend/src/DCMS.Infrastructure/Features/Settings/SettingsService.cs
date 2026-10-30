using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static DCMS.Infrastructure.Features.Settings.SettingsAccess;

namespace DCMS.Infrastructure.Features.Settings;

public sealed class SettingsService(DcmsDbContext db, ISettingsAccess access, UserManager<ApplicationUser> users, TimeProvider clock) : ISettingsService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    static void Paging(string? search, int page, int size) { if (search?.Length > 100 || page < 1 || page > 1000000 || size is not (20 or 50 or 100)) throw Error("MSG04"); }
    static void Reason(string? reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw Error("MSG02",field:"lý do"); }
    public async Task<SettingsCapabilities> CapabilitiesAsync(int actor, CancellationToken ct) {
        var state = await access.RequireAsync(actor,"settings.read",ct);
        var sections = SettingsGrants.CatalogKinds.Select((kind,i) => new SectionCapability(kind,SettingsGrants.CatalogLabels[i],$"/settings/{kind}",state.Permissions.Where(x => x.StartsWith($"catalog.{kind}.")).Select(x => x.Split('.').Last()).ToArray()))
            .Where(x => x.Actions.Contains("read")).ToList();
        if (state.Permissions.Contains("access-groups.read")) sections.Add(new("access-groups","Phân quyền truy cập","/settings/access-groups",state.Permissions.Where(x => x.StartsWith("access-groups.") || x == "staff.access.assign").ToArray()));
        return new("current-installation",sections.ToArray(),state.Permissions,state.BranchIds,state.Owner,await db.Set<SchedulingBranch>().Where(x=>state.BranchIds.Contains(x.Id)).Select(x=>new ScopeBranch(x.Id,x.Name)).ToArrayAsync(ct));
    }
    async Task<GroupDto> GroupDto(AccessGroup row,PermissionState state,CancellationToken ct) {
        var assignments=await db.Set<StaffAccessAssignment>().AsNoTracking().Where(x=>x.GroupId==row.Id).Select(x=>x.BranchIds).ToListAsync(ct);
        return new(row.Id,row.Code,row.Name,row.BaseRole,row.Description,row.Active,row.Version,row.Grants.Select(x=>x.Permission).Order().ToArray(),assignments.Count(x=>state.Owner||x.Length>0&&x.All(state.BranchIds.Contains)));
    }
    async Task<AccessGroup> Group(Guid id,CancellationToken ct)=>await db.Set<AccessGroup>().Include(x=>x.Grants).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw Error("MSG113",404);
    async Task ManageGroup(PermissionState actor,int actorId,Guid id,CancellationToken ct) {
        var assigned=await db.Set<StaffAccessAssignment>().AsNoTracking().Where(x=>x.GroupId==id).ToListAsync(ct);
        if(assigned.Any(x=>x.AccountId==actorId)||!actor.Owner&&assigned.Any(x=>x.BranchIds.Length==0||x.BranchIds.Any(b=>!actor.BranchIds.Contains(b))))throw Error("MSG06",403);
    }
    static void ValidateGroup(AccessGroupValue input,PermissionState actor) {
        if(input==null||string.IsNullOrWhiteSpace(input.Code)||input.Code.Length>40||string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>160||input.Description?.Length>1000
            ||!new[]{AccountRoles.Admin,AccountRoles.Dentist,AccountRoles.ClinicAssistant}.Contains(input.BaseRole)||input.Grants==null||input.Grants.Distinct().Count()!=input.Grants.Length)throw Error("MSG04");
        foreach(var grant in input.Grants){
            var definition=SettingsGrants.Definitions.SingleOrDefault(x=>x.Key==grant)??throw Error("MSG04");
            if(!SettingsGrants.RoleAllows(grant,[input.BaseRole])||!actor.Owner&&!actor.Permissions.Contains(grant))throw Error("MSG06",403);
            if(definition.Parent!=null&&!input.Grants.Contains(definition.Parent))throw Error("MSG04");
        }
    }
    public async Task<GroupDto[]> GroupsAsync(int actor,CancellationToken ct) {
        var state=await access.RequireAsync(actor,"access-groups.read",ct);var groups=await db.Set<AccessGroup>().AsNoTracking().Include(x=>x.Grants).OrderBy(x=>x.Name).ToListAsync(ct);
        var result=new List<GroupDto>();foreach(var group in groups)result.Add(await GroupDto(group,state,ct));return result.ToArray();
    }
    public async Task<object> GroupPreviewAsync(int actor,Guid id,GroupCommand command,CancellationToken ct) {
        var state=await access.RequireAsync(actor,"access-groups.manage",ct);await ManageGroup(state,actor,id,ct);ValidateGroup(command.Entry,state);
        var row=await Group(id,ct);if(row.Version!=command.ExpectedVersion)throw Error("MSG111",409);var before=await GroupDto(row,state,ct);
        return new {before,added=command.Entry.Grants.Except(before.Grants).ToArray(),removed=before.Grants.Except(command.Entry.Grants).ToArray(),affected=before.AssignedCount};
    }
    public Task<GroupDto> SaveGroupAsync(int actor,Guid? id,GroupCommand command,CancellationToken ct)=>Run(actor,"access-groups.manage",command.RequestKey,"group.save","access-groups:"+(id?.ToString()??"new"),command,id==null?"Tạo nhóm quyền":command.Reason,async state=>{
        ValidateGroup(command.Entry,state);var input=command.Entry;var name=Normalize(input.Name);var code=Normalize(input.Code);
        if(await db.Set<AccessGroup>().AnyAsync(x=>x.Id!=id&&(x.NormalizedName==name||x.NormalizedCode==code),ct))throw Error("MSG201",409,"code/name");
        var row=id==null?new AccessGroup():await Group(id.Value,ct);var before=id==null?null:await GroupDto(row,state,ct);
        if(id!=null){if(row.Version!=command.ExpectedVersion)throw Error("MSG111",409);if(input.BaseRole!=row.BaseRole&&await db.Set<StaffAccessAssignment>().AnyAsync(x=>x.GroupId==row.Id,ct))throw Error("MSG205",409);row.Version++;}else db.Add(row);
        row.Code=input.Code.Trim();row.NormalizedCode=code;row.Name=input.Name.Trim();row.NormalizedName=name;row.BaseRole=input.BaseRole;row.Description=input.Description?.Trim();
        var removed=row.Grants.Where(x=>!input.Grants.Contains(x.Permission)).ToArray();db.RemoveRange(removed);foreach(var item in removed)row.Grants.Remove(item);
        foreach(var grant in input.Grants.Where(x=>!row.Grants.Any(g=>g.Permission==x)))row.Grants.Add(new AccessGroupGrant {GroupId=row.Id,Permission=grant});var result=await GroupDto(row,state,ct);
        return(result,row.Version,(object?)before??new{},(object)result);
    },ct,id==null?null:state=>ManageGroup(state,actor,id.Value,ct));
    public Task<GroupDto> GroupStateAsync(int actor,Guid id,StateCommand command,CancellationToken ct)=>Run(actor,"access-groups.manage",command.RequestKey,"group.state","access-groups:"+id,command,command.Reason,async state=>{
        var row=await Group(id,ct);if(row.Version!=command.ExpectedVersion)throw Error("MSG111",409);
        if(!command.Active&&await db.Set<StaffAccessAssignment>().AnyAsync(x=>x.GroupId==id,ct))throw Error("MSG205",409,"nhóm còn nhân viên");
        var before=await GroupDto(row,state,ct);row.Active=command.Active;row.Version++;var result=await GroupDto(row,state,ct);return(result,row.Version,(object)before,(object)result);
    },ct,state=>ManageGroup(state,actor,id,ct));
    public async Task<StaffAccessDto[]> StaffAsync(int actor,CancellationToken ct) {
        var state=await access.RequireAsync(actor,"access-groups.read",ct);
        var employees=await (from e in db.Employees.AsNoTracking() join u in db.Users.AsNoTracking() on e.AccountId equals u.Id where u.AccountType=="Staff" select new {e.Id,e.AccountId,e.FullName,u.IsActive,u.Version}).ToListAsync(ct);
        var assignments=await db.Set<StaffAccessAssignment>().AsNoTracking().ToListAsync(ct);var members=await db.Set<SchedulingMember>().AsNoTracking().Where(x=>x.Active).ToListAsync(ct);
        return employees.Where(e=>state.Owner||members.Where(m=>m.EmployeeId==e.Id).Any()&&members.Where(m=>m.EmployeeId==e.Id).All(m=>state.BranchIds.Contains(m.BranchId)))
            .Select(e=>{var a=assignments.SingleOrDefault(x=>x.AccountId==e.AccountId);return new StaffAccessDto(e.Id,e.AccountId,e.FullName,e.IsActive,e.Version,a?.GroupId,a?.Version??0,a?.BranchIds??members.Where(m=>m.EmployeeId==e.Id).Select(m=>m.BranchId).ToArray());}).ToArray();
    }
    public Task<StaffAccessDto> AssignAsync(int actor,int employeeId,AssignGroup command,CancellationToken ct)=>Run(actor,"staff.access.assign",command.RequestKey,"group.assign","staff:"+employeeId,command,command.Reason,async state=>{
        var employee=await db.Employees.SingleAsync(x=>x.Id==employeeId,ct);var user=await db.Users.SingleAsync(x=>x.Id==employee.AccountId,ct);var group=await Group(command.GroupId,ct);
        if(!group.Active)throw Error("MSG202",409);if(user.Version!=command.ExpectedVersion||group.Version!=command.GroupVersion)throw Error("MSG111",409);
        var roles=AccountRoles.Effective(await users.GetRolesAsync(user));if(!roles.Contains(group.BaseRole))throw Error("MSG06",403);
        if(!state.Owner&&group.Grants.Any(x=>!state.Permissions.Contains(x.Permission)))throw Error("MSG06",403);
        var membership=await db.Set<SchedulingMember>().Where(x=>x.EmployeeId==employeeId&&x.Active).Select(x=>x.BranchId).ToArrayAsync(ct);
        if(command.BranchIds==null||command.BranchIds.Length==0||command.BranchIds.Distinct().Count()!=command.BranchIds.Length||command.BranchIds.Any(x=>!membership.Contains(x)||!state.BranchIds.Contains(x)))throw Error("MSG06",403);
        var assignment=await db.Set<StaffAccessAssignment>().SingleOrDefaultAsync(x=>x.AccountId==user.Id,ct);object before=assignment==null?new{}:new{assignment.GroupId,assignment.Version,assignment.BranchIds};
        if(assignment==null){assignment=new(){AccountId=user.Id};db.Add(assignment);}else assignment.Version++;
        if(assignment.GroupId!=Guid.Empty&&assignment.GroupId!=group.Id){var previousGroup=await Group(assignment.GroupId,ct);previousGroup.Version++;}
        group.Version++;
        assignment.GroupId=group.Id;assignment.BranchIds=command.BranchIds.Order().ToArray();user.Version++;user.UpdatedAt=clock.GetUtcNow();
        var result=new StaffAccessDto(employee.Id,user.Id,user.FullName,user.IsActive,user.Version,group.Id,assignment.Version,assignment.BranchIds);
        return(result,user.Version,before,(object)result);
    },ct,async state=>{
        var employee=await db.Employees.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==employeeId,ct)??throw Error("MSG113",404);
        if(employee.AccountId==actor)throw Error("MSG06",403);
        var target=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==employee.AccountId,ct);if(target.AccountType!="Staff")throw Error("MSG06",403);
        var roles=await users.GetRolesAsync(target);if(roles.Contains(AccountRoles.ClinicOwner))throw Error("MSG06",403);
        var branches=await db.Set<SchedulingMember>().Where(x=>x.EmployeeId==employeeId&&x.Active).Select(x=>x.BranchId).ToArrayAsync(ct);
        if(!state.Owner&&(branches.Length==0||branches.Any(b=>!state.BranchIds.Contains(b))))throw Error("MSG06",403);
    });
    public async Task<Page<object>> AuditAsync(int actor,string? section,int page,int pageSize,CancellationToken ct) {
        var state=await access.RequireAsync(actor,"settings.audit.read",ct);Paging(null,page,pageSize);
        var allowed=SettingsGrants.CatalogKinds.Where(k=>state.Permissions.Contains($"catalog.{k}.read")).ToList();
        if(state.Permissions.Contains("settings.general.read"))allowed.Add("general");if(state.Permissions.Contains("access-groups.read"))allowed.Add("access-groups");
        if(section!=null&&!allowed.Contains(section))throw Error("MSG06",403);
        var rows=db.Set<SettingsAudit>().AsNoTracking().Where(x=>allowed.Contains(x.Section)&&(section==null||x.Section==section));
        if(!state.Owner)rows=rows.Where(x=>x.Section!="access-groups"||x.BranchIds.All(branch=>state.BranchIds.Contains(branch)));
        var total=await rows.CountAsync(ct);var items=await rows.OrderByDescending(x=>x.At).ThenBy(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).Select(x=>(object)new{x.Id,x.ActorId,x.Section,x.Target,x.Action,x.Version,x.Reason,x.At,x.BeforeJson,x.AfterJson}).ToArrayAsync(ct);
        return new(items,total,page,pageSize);
    }
    async Task<T> Run<T>(int actor,string permission,Guid key,string action,string target,object payload,string? reason,Func<PermissionState,Task<(T Result,int Version,object Before,object After)>> change,CancellationToken ct,Func<PermissionState,Task>? guard=null) {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({WorkingScheduleService.TransactionLock})",ct);
        var state=await access.RequireAsync(actor,permission,ct);if(guard!=null)await guard(state);
        if(key==Guid.Empty)throw Error("MSG04");Reason(reason);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{action,target,payload},Json))));
        var old=await db.Set<SettingsReceipt>().AsNoTracking().SingleOrDefaultAsync(x=>x.ActorId==actor&&x.RequestKey==key,ct);
        if(old!=null){if(old.Fingerprint!=fingerprint)throw Error("MSG111",409);await tx.CommitAsync(ct);return JsonSerializer.Deserialize<T>(old.ResultJson,Json)!;}
        var result=await change(state);
        var section=target.StartsWith("staff:")?"access-groups":target.Split(':')[0];
        var auditTarget=result.Result is ReferenceDto reference ? reference.Kind+":"+reference.Id : result.Result is GroupDto group ? "access-groups:"+group.Id : target;
        var auditBranches=result.Result is StaffAccessDto assignedStaff ? assignedStaff.BranchIds : result.Result is GroupDto changedGroup
            ? (await db.Set<StaffAccessAssignment>().Where(x=>x.GroupId==changedGroup.Id).Select(x=>x.BranchIds).ToListAsync(ct)).SelectMany(x=>x).Distinct().ToArray() : Array.Empty<int>();
        if(result.Result is StaffAccessDto && result.Before is not null){
            using var beforeDocument=JsonDocument.Parse(JsonSerializer.Serialize(result.Before,Json));
            if(beforeDocument.RootElement.TryGetProperty("branchIds",out var beforeBranches))auditBranches=auditBranches.Concat(beforeBranches.EnumerateArray().Select(x=>x.GetInt32())).Distinct().ToArray();
        }
        db.Add(new SettingsAudit{ActorId=actor,Section=section,BranchIds=auditBranches,Target=auditTarget,Action=action,Version=result.Version,Reason=reason!.Trim(),At=clock.GetUtcNow(),BeforeJson=JsonSerializer.Serialize(result.Before,Json),AfterJson=JsonSerializer.Serialize(result.After,Json)});
        db.Add(new SettingsReceipt{ActorId=actor,RequestKey=key,Fingerprint=fingerprint,ResultJson=JsonSerializer.Serialize(result.Result,Json)});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result.Result;
    }
}
