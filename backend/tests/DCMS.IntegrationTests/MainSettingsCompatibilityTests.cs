using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DCMS.IntegrationTests;
public sealed class MainSettingsCompatibilityTests(ReceptionFixture fixture):IClassFixture<ReceptionFixture>
{
 [Fact]public async Task BothSettingsModulesAndPatientOptionsAreRoutableAndUseSeparateCatalogTables()
 {
  var reception=new ReceptionApiTests(fixture);await reception.Prepare();using var owner=await reception.Login("owner.demo");
  foreach(var path in new[]{"/api/settings/capabilities","/api/settings/general","/api/system-settings/settings/capabilities","/api/system-settings/settings/general","/api/system-settings/settings/catalogs/customer-groups?page=1&pageSize=20","/api/system-settings/reference-options?kind=intake-history","/api/patients/form-policy"})
  {var response=await owner.GetAsync(path);Assert.True(response.IsSuccessStatusCode,path+": "+await response.Content.ReadAsStringAsync());}
  using var dentist=await reception.Login("dentist.demo");Assert.Equal(HttpStatusCode.Forbidden,(await dentist.GetAsync("/api/system-settings/settings/general")).StatusCode);
  var me=await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");Assert.Contains(me.GetProperty("permissions").EnumerateArray(),p=>p.GetString()=="settings.general.manage");
  using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
  Assert.Equal("OPERATIONAL_REFERENCE_ENTRY",db.Model.FindEntityType(typeof(DCMS.Domain.Features.Settings.ReferenceEntry))!.GetTableName());
  Assert.Equal("REFERENCE_ENTRY",db.Model.FindEntityType(typeof(DCMS.Domain.Features.SystemSettings.ReferenceEntry))!.GetTableName());
  Assert.True(await db.Set<DCMS.Domain.Features.Settings.ReferenceEntry>().AnyAsync(x=>x.Kind=="diagnoses"));
  Assert.True(await db.Set<DCMS.Domain.Features.SystemSettings.ReferenceEntry>().AnyAsync(x=>x.Kind=="intake-history"));
 }
}
