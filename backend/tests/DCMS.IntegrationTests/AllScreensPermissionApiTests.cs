using System.Net;
using System.Net.Http.Json;
using DCMS.Api.Features.Settings;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class AllScreensPermissionApiTests(ReceptionFixture fixture) : IClassFixture<ReceptionFixture>
{
    private readonly ReceptionApiTests reception = new(fixture);
    static async Task<T> Ok<T>(HttpResponseMessage response) { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<T>())!; }
    static string[] Grants(params string[] requested) {
        var result = requested.ToHashSet();
        foreach (var key in requested) {
            var parent = SettingsGrants.Definitions.Single(x => x.Key == key).Parent;
            while (parent != null) { result.Add(parent); parent = SettingsGrants.Definitions.Single(x => x.Key == parent).Parent; }
        }
        return result.ToArray();
    }
    async Task<(StaffAccessDto Staff, string Name)> Staff(int branch, string role) {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var name = "screen-" + Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = name, FullName = "SYNTHETIC screen staff", AccountType = "Staff", IsActive = true, CreatedAt = fixture.Clock.Now, UpdatedAt = fixture.Clock.Now };
        Assert.True((await users.CreateAsync(user, fixture.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var employee = new EmployeeProfile { AccountId = user.Id, FullName = user.FullName, EmployeeCode = name }; db.Add(employee); await db.SaveChangesAsync();
        await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = branch, EmployeeId = employee.Id, CanDentist = role == AccountRoles.Dentist }); await db.SaveChangesAsync();
        return (new(employee.Id, user.Id, user.FullName, true, user.Version, null, 0, [branch]), name);
    }
    async Task<GroupDto> Assign(HttpClient owner, StaffAccessDto staff, string role, params string[] grants) {
        var group = await Ok<GroupDto>(await owner.PostAsJsonAsync("/api/system-settings/access-groups/", new GroupCommand(Guid.NewGuid(), new("SCREEN-" + Guid.NewGuid().ToString("N"), "SYNTHETIC screen " + Guid.NewGuid(), role, Grants(grants)))));
        await Ok<StaffAccessDto>(await owner.PutAsJsonAsync($"/api/system-settings/staff/{staff.EmployeeId}/access-group", new AssignGroup(group.Id, staff.AccountVersion, group.Version, Guid.NewGuid(), staff.BranchIds, "SYNTHETIC grant")));
        return group;
    }
    [Fact] public void EveryBusinessEndpointHasAnExplicitPermissionOrBaselineContract() {
        using var client = fixture.CreateClient();
        var endpoints = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        Assert.True(endpoints.Length > 100);
        var missing = new List<string>();
        foreach (var endpoint in endpoints) foreach (var method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []) {
            var template = endpoint.RoutePattern.RawText!;
            var rule = ScreenEndpointPermissions.Resolve(template, method, new Dictionary<string,string?> { ["kind"] = "diagnoses" });
            if (rule == null) { missing.Add($"{method} {template}"); continue; }
            Assert.True(rule!.Baseline != null || rule.AnyOf.Length > 0, template);
            foreach (var grant in rule.AnyOf) Assert.Contains(SettingsGrants.Definitions, x => x.Key == grant);
        }
        Assert.True(missing.Count == 0, "Missing permissions: " + string.Join("; ", missing));
        Assert.Equal(SettingsGrants.Definitions.Length, SettingsGrants.Definitions.Select(x => x.Key).Distinct().Count());
        foreach (var grant in SettingsGrants.Definitions.Where(x => x.Parent != null))
            foreach (var role in new[] { AccountRoles.Admin, AccountRoles.Dentist, AccountRoles.ClinicAssistant })
                if (SettingsGrants.RoleAllows(grant.Key, [role])) Assert.True(SettingsGrants.RoleAllows(grant.Parent!, [role]), $"Incompatible parent: {role} {grant.Key}");
    }
    [Theory]
    [InlineData("appointments.read", "Dentist", "/api/appointments/options", "/api/appointments", "POST")]
    [InlineData("working-schedules.read", "Dentist", "/api/working-schedules/options", "/api/working-schedules/recurring/publish", "POST")]
    [InlineData("booking-settings.read", "Dentist", "/api/booking-settings/options", "/api/booking-settings/{branch}", "PUT")]
    [InlineData("treatment.templates.read", "Dentist", "/api/treatment-templates?branchId={branch}", "/api/treatment-templates", "POST")]
    [InlineData("treatment.templates.read", "Dentist", "/api/treatment-templates/item-plans?branchId={branch}", "/api/treatment-templates/item-plans", "POST")]
    [InlineData("treatment.prices.read", "Admin", "/api/service-prices", "/api/service-prices/{service}", "PUT")]
    [InlineData("treatment.commercial.read", "Admin", "/api/commercial/dossiers", "/api/commercial/dossiers/11111111-1111-4111-8111-111111111111/deposit-exception", "POST")]
    [InlineData("treatment.read", "Dentist", "/api/patients/{patient}/dossiers?branchId={branch}", "/api/patients/{patient}/dossiers", "POST")]
    [InlineData("documents.read", "Dentist", "/api/patients/{patient}/document-contexts", "/api/clinical-documents/11111111-1111-4111-8111-111111111111/review", "POST")]
    [InlineData("care-tasks.read", "ClinicAssistant", "/api/care-tasks?branchId={branch}", "/api/care-tasks", "POST")]
    [InlineData("accounts.read", "Admin", "/api/accounts", "/api/accounts", "POST")]
    [InlineData("finance.costs.read", "Admin", "/api/finance/providers", "/api/finance/providers", "POST")]
    [InlineData("settings.general.read", "Admin", "/api/system-settings/settings/general", "/api/system-settings/settings/general/preview", "POST")]
    [InlineData("catalog.materials.read", "Dentist", "/api/system-settings/materials", "/api/system-settings/materials", "POST")]
    [InlineData("materials.purchases.read", "Dentist", "/api/system-settings/material-purchases", "/api/system-settings/material-purchases", "POST")]
    [InlineData("materials.stock.read", "Dentist", "/api/system-settings/material-stock", "/api/system-settings/material-purchases", "POST")]
    [InlineData("materials.usage.read", "Dentist", "/api/system-settings/material-usage", "/api/system-settings/material-purchases", "POST")]
    [InlineData("finance.revenue.read", "Admin", "/api/finance/revenue", "/api/finance/visits/11111111-1111-4111-8111-111111111111/payments", "POST")]
    [InlineData("finance.work-statistics.read", "Admin", "/api/work-statistics", "/api/system-settings/material-purchases", "POST")]
    [InlineData("catalog.medicines.read", "Admin", "/api/system-settings/settings/catalogs/medicines", "/api/system-settings/settings/catalogs/medicines", "POST")]
    [InlineData("catalog.customer-groups.read", "ClinicAssistant", "/api/system-settings/settings/catalogs/customer-groups", "/api/system-settings/settings/catalogs/customer-groups", "POST")]
    [InlineData("notifications.read", "ClinicAssistant", "/api/notifications", "/api/notifications/mark-read", "POST")]
    [InlineData("patient-links.read", "ClinicAssistant", "/api/patient-link-reviews", "/api/patient-link-reviews/1/verify", "POST")]
    public async Task ReadOnlyGrantRejectsWriteAndRevocationAppliesToTheSameToken(string permission, string role, string read, string write, string method) {
        var setup = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        if (permission == "documents.read") { using var assistant = await reception.Login(); await reception.Check(assistant, setup); }
        var (staff, name) = await Staff(setup.BranchId, role); using var user = await reception.Login(name); var group = await Assign(owner, staff, role, permission);
        string Path(string path) => path.Replace("{branch}", setup.BranchId.ToString()).Replace("{patient}", setup.PatientId.ToString()).Replace("{service}", setup.ServiceId.ToString());
        var response = await user.GetAsync(Path(read)); Assert.True(response.IsSuccessStatusCode, permission + ": " + await response.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(new HttpMethod(method), Path(write)) { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Forbidden, (await user.SendAsync(request)).StatusCode);
        group = (await owner.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!;
        await Ok<GroupDto>(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}", new GroupCommand(Guid.NewGuid(), new(group.Code, group.Name, group.BaseRole, []), group.Version, "SYNTHETIC revoke")));
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(Path(read))).StatusCode);
    }
    [Fact] public async Task AssignedScheduleScopeFiltersOptionsAndDeniesAnotherBranch() {
        var first = await reception.Prepare(); var second = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        var (staff, name) = await Staff(first.BranchId, AccountRoles.Dentist);
        using (var scope = fixture.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>(); await db.AddSchedulingDataAsync(new SchedulingMember { BranchId = second.BranchId, EmployeeId = staff.EmployeeId, CanDentist = true }); await db.SaveChangesAsync(); }
        await Assign(owner, staff, AccountRoles.Dentist, "working-schedules.read", "appointments.read", "booking-settings.read"); using var user = await reception.Login(name);
        var options = await Ok<DCMS.Application.Features.Scheduling.ScheduleOptions>(await user.GetAsync("/api/working-schedules/options"));
        Assert.Equal(first.BranchId, Assert.Single(options.Branches).Id); Assert.All(options.Employees, employee => Assert.False(employee.CanEdit));
        Assert.Contains(options.Employees, employee => employee.Id == staff.EmployeeId && employee.CanView == true);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/booking-settings/{second.BranchId}")).StatusCode);
    }
    [Theory]
    [InlineData("materials.purchases.create", "Dentist", "/api/system-settings/material-purchases", "POST")]
    [InlineData("materials.purchases.update", "Dentist", "/api/system-settings/material-purchases/11111111-1111-4111-8111-111111111111", "PUT")]
    [InlineData("materials.purchases.delete", "Dentist", "/api/system-settings/material-purchases/11111111-1111-4111-8111-111111111111", "DELETE")]
    [InlineData("finance.payos.create", "ClinicAssistant", "/api/dossiers/11111111-1111-4111-8111-111111111111/payos-orders", "POST")]
    [InlineData("treatment.lab.manage", "Dentist", "/api/dossiers/11111111-1111-4111-8111-111111111111/sheet-activities/11111111-1111-4111-8111-111111111111/lab-order", "PUT")]
    [InlineData("treatment.lab.manage", "Dentist", "/api/dossiers/11111111-1111-4111-8111-111111111111/sheet-activities/11111111-1111-4111-8111-111111111111/lab-order", "DELETE")]
    public async Task NewWriteGrantsAreRequiredBeyondTheirParentAndCanBeRevoked(string permission, string role, string path, string method) {
        var setup = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        var (staff, name) = await Staff(setup.BranchId, role);
        if (permission == "finance.payos.create") {
            // Dossier endpoints still require the clinical role in addition to the cashier grant.
            using var scope = fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(staff.AccountId.ToString()))!, AccountRoles.Dentist)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
            (await db.LoadMemberAsync(staff.EmployeeId)).CanDentist = true;
            await db.SaveChangesAsync();
        }
        using var user = await reception.Login(name);
        var parent = SettingsGrants.Definitions.Single(x => x.Key == permission).Parent!;
        var group = await Assign(owner, staff, role, parent);
        async Task<HttpStatusCode> Attempt() {
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
            return (await user.SendAsync(request)).StatusCode;
        }
        async Task Change(params string[] grants) {
            group = await Ok<GroupDto>(await owner.GetAsync($"/api/system-settings/access-groups/{group.Id}"));
            group = await Ok<GroupDto>(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}",
                new GroupCommand(Guid.NewGuid(), new(group.Code, group.Name, group.BaseRole, Grants(grants)), group.Version, "SYNTHETIC new function grant")));
        }
        Assert.Equal(HttpStatusCode.Forbidden, await Attempt());
        await Change(permission);
        // Invalid synthetic input or a missing synthetic record stops before any write/provider request.
        Assert.Contains(await Attempt(), new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound });
        await Change(parent);
        Assert.Equal(HttpStatusCode.Forbidden, await Attempt());
    }
    [Fact] public async Task ParentReadGrantsDoNotOpenNewReportsOrInventoryTabs() {
        var setup = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        var (staff, name) = await Staff(setup.BranchId, AccountRoles.Dentist);
        await Assign(owner, staff, AccountRoles.Dentist, "finance.read", "catalog.materials.read"); using var user = await reception.Login(name);
        foreach (var path in new[] { "/api/finance/revenue", "/api/work-statistics", "/api/system-settings/material-stock", "/api/system-settings/material-usage", "/api/system-settings/material-purchases" })
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/system-settings/material-stock/available")).StatusCode);
    }
    [Fact] public async Task NewReportGrantsStillRejectBranchesOutsideTheAssignment() {
        var first = await reception.Prepare(); var second = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        var (staff, name) = await Staff(first.BranchId, AccountRoles.Dentist);
        await Assign(owner, staff, AccountRoles.Dentist, "finance.revenue.read", "finance.work-statistics.read"); using var user = await reception.Login(name);
        foreach (var path in new[] { "/api/finance/revenue", "/api/work-statistics" }) {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"{path}?branchId={second.BranchId}")).StatusCode);
        }
    }
    [Fact] public void UnassignedAdminDoesNotGainFinancialReportsOrMaterialOperations() {
        var legacy = SettingsGrants.Legacy([AccountRoles.Admin]);
        Assert.DoesNotContain("finance.revenue.read", legacy);
        Assert.DoesNotContain("finance.work-statistics.read", legacy);
        Assert.DoesNotContain(legacy, x => x.StartsWith("materials."));
    }
    [Fact] public async Task WholePlanSaveCannotBypassCreateDeleteOrPriceGrants() {
        var setup = await reception.Prepare(); using var owner = await reception.Login("owner.demo");
        var (staff, name) = await Staff(setup.BranchId, AccountRoles.Dentist);
        var roots = new[] { "treatment.dossiers.create", "treatment.plan.manage" };
        var group = await Assign(owner, staff, AccountRoles.Dentist, roots); using var user = await reception.Login(name);
        async Task Change(params string[] extra) {
            group = (await owner.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!;
            group = await Ok<GroupDto>(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}", new GroupCommand(Guid.NewGuid(), new(group.Code, group.Name, group.BaseRole, Grants([..roots, ..extra])), group.Version, "SYNTHETIC granular plan grant")));
        }
        var dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await user.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers", new DCMS.Application.Features.Treatment.CreateDossierRequest(setup.BranchId, staff.EmployeeId, "SYNTHETIC grant test", null, null, Guid.NewGuid())))).Dossier;
        DCMS.Application.Features.Treatment.TreatmentItemInput Item(Guid? id, int order, long price = 100000) => new(id, null, setup.ServiceId, "25", "răng", 1, order, 30, null, null, price, "Proposed", null, null, null, null);
        async Task<HttpResponseMessage> Save(params DCMS.Application.Features.Treatment.TreatmentItemInput[] items) => await user.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/plan/draft", new DCMS.Application.Features.Treatment.SavePlanDraftRequest(dossier.Version, "SYNTHETIC goal", items, [], Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(Item(null, 1), Item(null, 2))).StatusCode);
        await Change("treatment.items.create");
        dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await Save(Item(null, 1), Item(null, 2)))).Dossier;
        var first = dossier.Items[0].Id; var second = dossier.Items[1].Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(Item(first, 1, 200000), Item(second, 2))).StatusCode);
        dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await Save(Item(first, 1), Item(second, 2)))).Dossier;
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(Item(first, 1))).StatusCode);
        await Change("treatment.items.delete");
        dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await Save(Item(first, 1)))).Dossier;
        Assert.Single(dossier.Items);
        await Change("treatment.items.price");
        dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await Save(Item(first, 1, 200000)))).Dossier;
        Assert.Equal(200000, Assert.Single(dossier.Items).AgreedUnitPriceVnd);
        await Change();
        Assert.Equal(HttpStatusCode.Forbidden, (await Save(Item(first, 1, 300000))).StatusCode);
        group = (await owner.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!;
        await Ok<GroupDto>(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}", new GroupCommand(Guid.NewGuid(), new(group.Code, group.Name, group.BaseRole, ["treatment.read"]), group.Version, "SYNTHETIC read only")));
        var context = await Ok<DCMS.Application.Features.Treatment.TreatmentSheetContext>(await user.GetAsync($"/api/dossiers/{dossier.Id}/sheet-context"));
        Assert.Empty(context.AllowedActions);
        Assert.Empty(context.ConsentDocuments!);
    }
    [Fact] public async Task ReceptionVisitHeaderDoesNotRequireClinicalRead() {
        var setup = await reception.Prepare(); using var assistant = await reception.Login(); var visit = await reception.Check(assistant, setup);
        using var owner = await reception.Login("owner.demo"); var (staff, name) = await Staff(setup.BranchId, AccountRoles.ClinicAssistant);
        await Assign(owner, staff, AccountRoles.ClinicAssistant, "reception.read"); using var user = await reception.Login(name);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/visits/{visit.VisitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/visits/{visit.VisitId}/examination")).StatusCode);
    }
    [Fact] public async Task DossierPaymentRightsAreIndependentOfClinicalEditing() {
        var setup = await reception.Prepare(); using var owner = await reception.Login("owner.demo"); using var dentist = await reception.Login("dentist.demo");
        var dossier = (await Ok<DCMS.Application.Features.Treatment.TreatmentChange>(await dentist.PostAsJsonAsync($"/api/patients/{setup.PatientId}/dossiers", new DCMS.Application.Features.Treatment.CreateDossierRequest(setup.BranchId, setup.DentistId, "SYNTHETIC payment access", null, null, Guid.NewGuid())))).Dossier;
        var (staff, name) = await Staff(setup.BranchId, AccountRoles.Dentist);
        var group = await Assign(owner, staff, AccountRoles.Dentist, "treatment.dossiers.update"); using var user = await reception.Login(name);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/dossiers/{dossier.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/dossiers/{dossier.Id}/payments/context")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/payments", new {})).StatusCode);
        group = (await owner.GetFromJsonAsync<GroupDto>($"/api/system-settings/access-groups/{group.Id}"))!;
        await Ok<GroupDto>(await owner.PutAsJsonAsync($"/api/system-settings/access-groups/{group.Id}", new GroupCommand(Guid.NewGuid(), new(group.Code, group.Name, group.BaseRole, ["finance.read"]), group.Version, "SYNTHETIC finance only")));
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/dossiers/{dossier.Id}/payments/context")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/dossiers/{dossier.Id}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/dossiers/{dossier.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/payments", new {})).StatusCode);
    }
    [Fact] public async Task PrescriptionReadDoesNotRequireExaminationWriteOrReceptionAccess() {
        var setup = await reception.Prepare(); using var assistant = await reception.Login(); var visit = await reception.Check(assistant, setup);
        using var owner = await reception.Login("owner.demo"); var (staff, name) = await Staff(setup.BranchId, AccountRoles.Dentist);
        await Assign(owner, staff, AccountRoles.Dentist, "prescriptions.read"); using var user = await reception.Login(name);
        Assert.True((await user.GetAsync($"/api/prescriptions/visits/{visit.VisitId}/lines")).IsSuccessStatusCode);
        await Ok<DCMS.Application.Features.Examinations.ExaminationWorkspace>(await user.GetAsync($"/api/visits/{visit.VisitId}/examination"));
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync($"/api/prescriptions/visits/{visit.VisitId}/lines", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/reception/options")).StatusCode);
    }
}
