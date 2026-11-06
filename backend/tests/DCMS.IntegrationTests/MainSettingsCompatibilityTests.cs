using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Application.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DCMS.IntegrationTests;
// docs/specs/S58-SETTINGS-MERGE.vi.md: /api/system-settings is the only settings module; the old operational catalog is legacy.
public sealed class MainSettingsCompatibilityTests(ReceptionFixture fixture):IClassFixture<ReceptionFixture>
{
 [Fact]public async Task MergedSettingsModuleServesEverySectionAndOldRoutesAreGone()
 {
  var reception=new ReceptionApiTests(fixture);await reception.Prepare();using var owner=await reception.Login("owner.demo");
  foreach(var path in new[]{"/api/system-settings/access-capabilities","/api/system-settings/settings/capabilities","/api/system-settings/settings/general","/api/system-settings/settings/catalogs/customer-groups?page=1&pageSize=20","/api/system-settings/settings/catalogs/diagnoses?page=1&pageSize=20","/api/system-settings/access-groups/","/api/system-settings/settings/audit","/api/system-settings/reference-options?kind=intake-history","/api/patients/form-policy"})
  {var response=await owner.GetAsync(path);Assert.True(response.IsSuccessStatusCode,path+": "+await response.Content.ReadAsStringAsync());}
  foreach(var path in new[]{"/api/settings/capabilities","/api/settings/general","/api/settings/catalogs/diagnoses","/api/settings/audit","/api/access-groups/","/api/reference-options?kind=diagnoses&context=examination&branchId=1"})
   Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync(path)).StatusCode);
  using var dentist=await reception.Login("dentist.demo");Assert.Equal(HttpStatusCode.Forbidden,(await dentist.GetAsync("/api/system-settings/settings/general")).StatusCode);
  var me=await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");Assert.Contains(me.GetProperty("permissions").EnumerateArray(),p=>p.GetString()=="settings.general.manage");
 }

 [Fact]public async Task OperationalDiagnosesWereCopiedWithTheSameIdsAndAreSelectableInExaminations()
 {
  var reception=new ReceptionApiTests(fixture);using var dentist=await reception.Login("dentist.demo");
  using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
  var legacy=await db.Set<DCMS.Domain.Features.Settings.ReferenceEntry>().AsNoTracking().ToListAsync();
  var merged=await db.ReferenceEntries.AsNoTracking().ToListAsync();
  Assert.NotEmpty(legacy);
  foreach(var row in legacy)
   Assert.True(merged.Any(x=>x.Id==row.Id&&x.Kind==row.Kind&&x.Version==row.Version&&x.Name==row.Name)
    ||merged.Any(x=>x.Kind==row.Kind&&(x.NormalizedName==row.NormalizedName||x.Code!=null&&x.Code==row.NormalizedCode)),$"{row.Kind}/{row.Name} was not merged");
  var options=(await dentist.GetFromJsonAsync<ReferenceOption[]>("/api/system-settings/reference-options?kind=diagnoses"))!;
  var activeMerged=merged.Where(x=>x.Kind=="diagnoses"&&x.IsActive).Select(x=>x.Id).ToHashSet();
  Assert.All(legacy.Where(x=>x.Kind=="diagnoses"&&x.Active&&activeMerged.Contains(x.Id)),row=>Assert.Contains(options,o=>o.Id==row.Id&&o.Version==row.Version));
 }
}
