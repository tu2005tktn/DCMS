using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Examinations;
using DCMS.Application.Features.Reception;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Settings;
using ReferenceEntry = DCMS.Domain.Features.SystemSettings.ReferenceEntry;
using AccessCapabilities = DCMS.Application.Features.Settings.SettingsCapabilities;
using GeneralSettings = DCMS.Application.Features.SystemSettings.GeneralSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class SettingsApiTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    readonly ReceptionApiTests reception=new(fixture);
    static async Task<T> Ok<T>(HttpResponseMessage response) { Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return(await response.Content.ReadFromJsonAsync<T>())!; }
    Task<HttpClient> Owner()=>reception.Login("owner.demo");
    static string Catalog(string kind="diagnoses",Guid? id=null)=>"/api/system-settings/settings/catalogs/"+kind+(id.HasValue?"/"+id:"");
    static GroupCommand NewGroup(string[] grants,string role=AccountRoles.ClinicAssistant)=>new(Guid.NewGuid(),new("TEST-"+Guid.NewGuid().ToString("N"),"SYNTHETIC nhóm "+Guid.NewGuid().ToString("N"),role,grants));
    async Task<StaffAccessDto> NewStaff(int branchId,string role=AccountRoles.ClinicAssistant,bool locked=false) {
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var name="s58-"+Guid.NewGuid().ToString("N");var user=new ApplicationUser {UserName=name,FullName="SYNTHETIC nhân viên",AccountType="Staff",IsActive=!locked,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};
        Assert.True((await users.CreateAsync(user,fixture.Password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);
        var employee=new EmployeeProfile {AccountId=user.Id,FullName=user.FullName,EmployeeCode=name};db.Add(employee);await db.SaveChangesAsync();db.Add(new SchedulingMember {BranchId=branchId,EmployeeId=employee.Id,CanDentist=role==AccountRoles.Dentist});await db.SaveChangesAsync();
        return new(employee.Id,user.Id,name,user.IsActive,user.Version,null,0,[branchId]);
    }
    Task<GroupDto> CreateGroup(HttpClient c,string[] grants,string role=AccountRoles.ClinicAssistant)=>CreateGroup(c,NewGroup(grants,role));
    Task<GroupDto> CreateGroup(HttpClient c,GroupCommand command)=>Post<GroupDto>(c,"/api/system-settings/access-groups/",command);
    static async Task<T> Post<T>(HttpClient c,string path,object command)=>await Ok<T>(await c.PostAsJsonAsync(path,command));
    static async Task<HttpResponseMessage> Assign(HttpClient c,StaffAccessDto target,GroupDto group,int[]? branches=null) { group=(await c.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!; return await c.PutAsJsonAsync($"/api/system-settings/staff/{target.EmployeeId}/access-group",new AssignGroup(group.Id,target.AccountVersion,group.Version,Guid.NewGuid(),branches??target.BranchIds,"SYNTHETIC gán nhóm")); }
    static async Task<GroupDto> EditGroup(HttpClient c,GroupDto group,string[] grants) { group=(await c.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!; return await Ok<GroupDto>(await c.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}",new GroupCommand(Guid.NewGuid(),new(group.Code,group.Name,group.BaseRole,grants,group.Description),group.Version,"SYNTHETIC đổi quyền"))); }

    [Fact] public async Task CatalogCommandsAreAtomicVersionedAndReplaySafe() {
        using var c=await Owner();var command=new CreateReferenceRequest(Guid.NewGuid(),new("T-"+Guid.NewGuid().ToString("N")[..20]," SYNTHETIC bệnh "+Guid.NewGuid().ToString("N")+" ",null));
        var created=await Post<ReferenceEntryDto>(c,Catalog(),command);var replay=await Post<ReferenceEntryDto>(c,Catalog(),command);Assert.Equal(created.Id,replay.Id);Assert.Equal(created.Version,replay.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Catalog(),command with {Entry=command.Entry! with {Name="khác"}})).StatusCode);
        var update=new UpdateReferenceRequest(created.Version,Guid.NewGuid(),"SYNTHETIC sửa",new(created.Code,created.Name+" sửa",null));
        var responses=await Task.WhenAll(c.PutAsJsonAsync(Catalog(id:created.Id),update),c.PutAsJsonAsync(Catalog(id:created.Id),update with {RequestKey=Guid.NewGuid()}));
        Assert.Single(responses,x=>x.IsSuccessStatusCode);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Catalog(),new CreateReferenceRequest(Guid.NewGuid(),new(created.Code,created.Name+" SỬA",null)))).StatusCode);
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();Assert.Equal(2,await db.SystemCatalogChanges.CountAsync(x=>x.ResourceId==created.Id));
    }
    [Theory] [InlineData("patient-sources")] [InlineData("occupations")] [InlineData("intake-history")] [InlineData("care-types")] [InlineData("expense-categories")]
    public async Task AuxiliaryCatalogsAreIndependentTypedAndSearchable(string kind) {
        using var c=await Owner();var code="SYN"+Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Catalog(kind),new CreateReferenceRequest(Guid.NewGuid(),new(null,"SYNTHETIC thiếu mã",null)))).StatusCode);
        var entry=await Post<ReferenceEntryDto>(c,Catalog(kind),new CreateReferenceRequest(Guid.NewGuid(),new(code,"SYNTHETIC có dấu "+code,null)));
        var found=await c.GetFromJsonAsync<CatalogPage<ReferenceEntryDto>>(Catalog(kind)+"?q="+code+"&status=Active");Assert.Equal(entry.Id,Assert.Single(found!.Items).Id);
        var other=await Post<ReferenceEntryDto>(c,Catalog(),new CreateReferenceRequest(Guid.NewGuid(),new(code,entry.Name,null)));Assert.NotEqual(entry.Id,other.Id);
    }
    [Fact] public async Task GeneralPreviewDoesNotWriteAndRestoreCreatesNewVersionWithoutCounters() {
        const string general="/api/system-settings/settings/general";
        using var c=await Owner();var initial=(await c.GetFromJsonAsync<GeneralSettingsDto>(general))!;
        var settings=GeneralSettings.Default with {StaffIdleTimeoutMinutes=initial.Settings.StaffIdleTimeoutMinutes==30?60:30};
        await Ok<object>(await c.PostAsJsonAsync(general+"/preview",new PreviewGeneralRequest(initial.Version,settings)));Assert.Equal(initial.Version,(await c.GetFromJsonAsync<GeneralSettingsDto>(general))!.Version);
        var input=new SaveGeneralRequest(initial.Version,Guid.NewGuid(),"SYNTHETIC cấu hình",settings);
        var one=await Ok<GeneralSettingsDto>(await c.PutAsJsonAsync(general,input));Assert.Equal(one.Version,(await Ok<GeneralSettingsDto>(await c.PutAsJsonAsync(general,input))).Version);
        var two=await Ok<GeneralSettingsDto>(await c.PutAsJsonAsync(general,input with {ExpectedVersion=one.Version,RequestKey=Guid.NewGuid(),Settings=settings with {StaffIdleTimeoutMinutes=90}}));
        await Ok<object>(await c.PostAsJsonAsync(general+"/restore-preview",new RestorePreviewRequest(one.Version,two.Version)));
        var three=await Post<GeneralSettingsDto>(c,general+"/restore",new RestoreGeneralRequest(one.Version,two.Version,Guid.NewGuid(),"SYNTHETIC khôi phục"));Assert.Equal(two.Version+1,three.Version);Assert.Equal(one.Settings,three.Settings);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(general,input with {ExpectedVersion=three.Version,RequestKey=Guid.NewGuid(),Settings=settings with {StaffIdleTimeoutMinutes=0}})).StatusCode);
        // The merged module owns general settings; the old /api/settings routes are gone.
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/api/settings/general")).StatusCode);
    }
    [Fact] public async Task CatalogOnlyGrantCannotReadOtherSectionsAndRevocationPrecedesReplay() {
        var setup=await reception.Prepare();using var owner=await Owner();var staff=await NewStaff(setup.BranchId);using var user=await reception.Login(staff.Name);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.GetAsync("/api/system-settings/access-capabilities")).StatusCode);
        var group=await CreateGroup(owner,["settings.read","catalog.diagnoses.read","catalog.diagnoses.create"]);await Ok<StaffAccessDto>(await Assign(owner,staff,group));
        var cap=(await user.GetFromJsonAsync<AccessCapabilities>("/api/system-settings/access-capabilities"))!;Assert.Equal("diagnoses",Assert.Single(cap.Sections).Key);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.GetAsync(Catalog("occupations"))).StatusCode);
        var command=new CreateReferenceRequest(Guid.NewGuid(),new(null,"SYNTHETIC delegated "+Guid.NewGuid().ToString("N"),null));var created=await Post<ReferenceEntryDto>(user,Catalog(),command);
        await EditGroup(owner,group,["settings.read","catalog.diagnoses.read"]);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PostAsJsonAsync(Catalog(),command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.GetAsync("/api/patients/")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.GetAsync("/api/support-requests")).StatusCode);
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();Assert.Single(await db.Set<ReferenceEntry>().Where(x=>x.Id==created.Id).ToArrayAsync());
    }
    [Fact] public async Task RoleGatesSelfAssignmentAndUnknownGrantsAreRejected() {
        var setup=await reception.Prepare();using var owner=await Owner();
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync("/api/system-settings/access-groups/",NewGroup(["examinations.read","examinations.manage"])) ).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsJsonAsync("/api/system-settings/access-groups/",NewGroup(["ledger.delete"])) ).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsJsonAsync("/api/system-settings/access-groups/",NewGroup(["catalog.diagnoses.create"])) ).StatusCode);
        var group=await CreateGroup(owner,[],AccountRoles.Dentist);var employees=await owner.GetFromJsonAsync<StaffAccessDto[]>("/api/system-settings/access-groups/staff");
        var ownerUser=(await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;var self=employees!.Single(x=>x.AccountId==ownerUser.Id);
        Assert.Equal(HttpStatusCode.Forbidden,(await Assign(owner,self,group,[setup.BranchId])).StatusCode);
        var assistant=await NewStaff(setup.BranchId);Assert.Equal(HttpStatusCode.Forbidden,(await Assign(owner,assistant,group)).StatusCode);
    }
    [Fact] public async Task OwnerDelegatesEligibleRoleGrantsWithoutAcquiringThoseRoles()
    {
        var setup=await reception.Prepare();using var owner=await Owner();var target=await NewStaff(setup.BranchId);
        var groups=(await owner.GetFromJsonAsync<GroupDto[]>("/api/system-settings/access-groups/"))!;var group=groups.Single(x=>x.Code=="Assistants");
        await Ok<StaffAccessDto>(await Assign(owner,target,group));
        var current=(await owner.GetFromJsonAsync<CurrentUser>("/api/auth/me"))!;Assert.DoesNotContain("patients.manage",current.Permissions);Assert.DoesNotContain(AccountRoles.ClinicAssistant,current.EffectiveRoles);
    }
    [Fact] public async Task GroupCountsIncludeLockedStaffAndInactiveGroupsCannotBeAssigned() {
        var setup=await reception.Prepare();using var owner=await Owner();var staff=await NewStaff(setup.BranchId,locked:true);var group=await CreateGroup(owner,[]);
        var assigned=await Ok<StaffAccessDto>(await Assign(owner,staff,group));var groups=(await owner.GetFromJsonAsync<GroupDto[]>("/api/system-settings/access-groups/"))!;group=groups.Single(x=>x.Id==group.Id);Assert.Equal(1,group.AssignedCount);
        var state=new StateCommand(group.Version,Guid.NewGuid(),false,"SYNTHETIC ngừng");Assert.Equal(HttpStatusCode.Conflict,(await owner.PostAsJsonAsync($"/api/system-settings/access-groups/{group.Id}/state",state)).StatusCode);
        var other=await CreateGroup(owner,[]);await Ok<StaffAccessDto>(await Assign(owner,assigned,other));group=(await owner.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!;var inactive=await Post<GroupDto>(owner,$"/api/system-settings/access-groups/{group.Id}/state",state with {ExpectedVersion=group.Version});
        var fresh=await NewStaff(setup.BranchId);Assert.Equal(HttpStatusCode.Conflict,(await Assign(owner,fresh,inactive)).StatusCode);
    }
    [Fact] public async Task SecurityAdminCannotEditOwnGroupOrDelegateBeyondScopeOrOwnGrants() {
        var setup=await reception.Prepare();using var owner=await Owner();var admin=await NewStaff(setup.BranchId,AccountRoles.Admin);
        var group=await CreateGroup(owner,["settings.read","access-groups.read","access-groups.manage","staff.access.assign"],AccountRoles.Admin);await Ok<StaffAccessDto>(await Assign(owner,admin,group));using var user=await reception.Login(admin.Name);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}",new GroupCommand(Guid.NewGuid(),new(group.Code,group.Name,group.BaseRole,group.Grants),group.Version,"SYNTHETIC self edit"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PostAsJsonAsync("/api/system-settings/access-groups/",NewGroup(["settings.read","catalog.diagnoses.read"]))).StatusCode);
        var other=await reception.Prepare();var target=await NewStaff(other.BranchId);var limited=await CreateGroup(owner,[]);
        Assert.Equal(HttpStatusCode.Forbidden,(await Assign(user,target,limited)).StatusCode);
    }
    [Fact] public async Task MembershipChangesInvalidateAnEarlierPermissionPreview()
    {
        var setup=await reception.Prepare();using var owner=await Owner();var group=await CreateGroup(owner,["settings.read","catalog.diagnoses.read"]);
        var change=new GroupCommand(Guid.NewGuid(),new(group.Code,group.Name,group.BaseRole,["settings.read"]),group.Version,"SYNTHETIC thu hồi");
        await Ok<object>(await owner.PostAsJsonAsync($"/api/system-settings/access-groups/{group.Id}/preview",change));
        var staff=await NewStaff(setup.BranchId);await Ok<StaffAccessDto>(await Assign(owner,staff,group));
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}",change)).StatusCode);
    }
    [Fact] public async Task AccessAuditDoesNotExposeStaffFromAnotherBranch()
    {
        var first=await reception.Prepare();var second=await reception.Prepare();using var owner=await Owner();
        var admin=await NewStaff(first.BranchId,AccountRoles.Admin);var manager=await CreateGroup(owner,["settings.read","access-groups.read","settings.audit.read"],AccountRoles.Admin);await Ok<StaffAccessDto>(await Assign(owner,admin,manager));
        var outside=await NewStaff(second.BranchId);var inside=await NewStaff(first.BranchId);var group=await CreateGroup(owner,[]);
        await Ok<StaffAccessDto>(await Assign(owner,outside,group));await Ok<StaffAccessDto>(await Assign(owner,inside,group));using var user=await reception.Login(admin.Name);
        var audit=(await user.GetFromJsonAsync<Page<System.Text.Json.JsonElement>>("/api/system-settings/settings/audit?section=access-groups&pageSize=100"))!;
        Assert.DoesNotContain(audit.Items,x=>x.GetProperty("target").GetString()=="staff:"+outside.EmployeeId);
        Assert.Contains(audit.Items,x=>x.GetProperty("target").GetString()=="staff:"+inside.EmployeeId);
    }
    [Fact] public async Task DiagnosisOptionsUseClinicalPermissionAndPreserveHistoricalSnapshots() {
        var setup=await reception.Prepare();using var owner=await Owner();using var dentist=await reception.Login("dentist.demo");using var assistant=await reception.Login();var visit=await reception.Check(assistant,setup);
        var started=await Post<ExaminationWorkspace>(dentist,$"/api/visits/{visit.VisitId}/start",new StartExamination(visit.PatientId,visit.VisitVersion,Guid.NewGuid()));
        var entry=await Post<ReferenceEntryDto>(owner,Catalog(),new CreateReferenceRequest(Guid.NewGuid(),new("SYN"+Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),"SYNTHETIC bệnh khám "+Guid.NewGuid().ToString("N"),null)));
        Assert.Equal(HttpStatusCode.Forbidden,(await dentist.GetAsync(Catalog())).StatusCode);
        var options=await dentist.GetFromJsonAsync<ReferenceOption[]>("/api/system-settings/reference-options?kind=diagnoses");Assert.Contains(options!,x=>x.Id==entry.Id&&x.Version==entry.Version);
        Assert.Equal(HttpStatusCode.NotFound,(await dentist.GetAsync($"/api/reference-options?kind=diagnoses&context=examination&branchId={setup.BranchId}")).StatusCode);
        var content=started.InitialContent with {Symptoms="SYNTHETIC khám",UnscopedDiagnoses=[],Diagnoses=[],Findings=[new(16,null,"",null,[entry.Name])],DiagnosisReferences=[new(entry.Id,entry.Version,entry.Name,entry.Code)],Advice="SYNTHETIC hướng dẫn",Disposition="ConsultationOnly"};
        var saved=await Ok<ExaminationWorkspace>(await dentist.PutAsJsonAsync($"/api/visits/{visit.VisitId}/examination",new SaveExamination(visit.PatientId,started.Visit.VisitVersion,Guid.NewGuid(),content,false)));
        var renamed=await Ok<ReferenceEntryDto>(await owner.PutAsJsonAsync(Catalog(id:entry.Id),new UpdateReferenceRequest(entry.Version,Guid.NewGuid(),"SYNTHETIC đổi",new(entry.Code,entry.Name+" đổi",null))));
        await Post<ReferenceEntryDto>(owner,Catalog(id:entry.Id)+"/state",new ReferenceStateRequest(renamed.Version,Guid.NewGuid(),false,"SYNTHETIC ngừng"));
        var current=await Ok<ExaminationWorkspace>(await dentist.PutAsJsonAsync($"/api/visits/{visit.VisitId}/examination",new SaveExamination(visit.PatientId,saved.Visit.VisitVersion,Guid.NewGuid(),saved.Examination!.Content,true)));
        Assert.Equal(entry.Name,Assert.Single(current.Examination!.Content.DiagnosisReferences!).Name);
        var other=await reception.Prepare();var newVisit=await reception.Check(assistant,other);var next=await Post<ExaminationWorkspace>(dentist,$"/api/visits/{newVisit.VisitId}/start",new StartExamination(newVisit.PatientId,newVisit.VisitVersion,Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict,(await dentist.PutAsJsonAsync($"/api/visits/{newVisit.VisitId}/examination",new SaveExamination(newVisit.PatientId,next.Visit.VisitVersion,Guid.NewGuid(),content,false))).StatusCode);
    }
}
