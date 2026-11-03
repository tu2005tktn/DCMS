using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Finance;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

public sealed class ClinicalPresetPriceApiTests(ReceptionFixture fixture):IClassFixture<ReceptionFixture>
{
    readonly ReceptionApiTests reception=new(fixture);

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task MissingAndNullPricesAreRejectedBeforeCreatingCatalogEntry(bool examination,bool omit)
    {
        var setup=await reception.Prepare();using var d=await reception.Login("dentist.demo");
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var count=await db.Set<SchedulingService>().CountAsync();
        var payload=new Dictionary<string,object?> { ["requestKey"]=Guid.NewGuid(),["branchId"]=setup.BranchId,["name"]="SYNTHETIC preset "+Guid.NewGuid(),["kind"]="Lab",["unit"]="Lần" };
        if(examination){payload.Remove("kind");payload.Remove("unit");}
        if(!omit)payload["priceVnd"]=null;
        var response=await d.PostAsJsonAsync(examination?"/api/finance/examination-services":"/api/finance/services",payload);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        var error=await response.Content.ReadFromJsonAsync<SystemMessage>();Assert.Equal("MSG02",error!.Code);Assert.Equal("priceVnd",error.Field);
        Assert.Equal(count,await db.Set<SchedulingService>().CountAsync());
    }

    [Theory]
    [InlineData(false,-1L)][InlineData(true,-1L)][InlineData(false,1000000000001L)][InlineData(true,1000000000001L)]
    public async Task InvalidPricesCannotReplaceConfiguredPrice(bool examination,long price)
    {
        var setup=await reception.Prepare();using var d=await reception.Login("dentist.demo");
        var prefix=examination?"/api/finance/examination-services":"/api/finance/services";
        int id,version;
        if(examination){var row=Assert.Single((await d.GetFromJsonAsync<ExaminationServiceOption[]>($"{prefix}?branchId={setup.BranchId}"))!);id=row.Id;version=row.Version;}
        else{var created=await d.PostAsJsonAsync(prefix,new SaveInvestigationService(Guid.NewGuid(),setup.BranchId,"SYNTHETIC preset "+Guid.NewGuid(),"Lab","Lần",220000));Assert.Equal(HttpStatusCode.OK,created.StatusCode);var row=(await created.Content.ReadFromJsonAsync<InvestigationServiceDto>())!;id=row.Id;version=row.Version;}
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<DcmsDbContext>();var before=await db.Set<SchedulingService>().AsNoTracking().SingleAsync(x=>x.Id==id);
        var response=examination?await d.PutAsJsonAsync($"{prefix}/{id}",new SaveExaminationService(Guid.NewGuid(),setup.BranchId,before.Name,price,version)):
            await d.PutAsJsonAsync($"{prefix}/{id}",new SaveInvestigationService(Guid.NewGuid(),setup.BranchId,before.Name,"Lab","Lần",price,version));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);var after=await db.Set<SchedulingService>().AsNoTracking().SingleAsync(x=>x.Id==id);Assert.Equal(before.ReferenceUnitPriceVnd,after.ReferenceUnitPriceVnd);Assert.Equal(version,after.PriceVersion);
    }

    [Theory]
    [InlineData(false,0L)][InlineData(true,0L)][InlineData(false,250000L)][InlineData(true,120000L)]
    public async Task ConcreteCatalogPricesIncludingZeroAreReadBackAndReplaySafe(bool examination,long price)
    {
        var setup=await reception.Prepare();using var d=await reception.Login("dentist.demo");
        string path;object command;
        if(examination){var row=Assert.Single((await d.GetFromJsonAsync<ExaminationServiceOption[]>($"/api/finance/examination-services?branchId={setup.BranchId}"))!);path=$"/api/finance/examination-services/{row.Id}";command=new SaveExaminationService(Guid.NewGuid(),setup.BranchId,row.Name,price,row.Version);}
        else{path="/api/finance/services";command=new SaveInvestigationService(Guid.NewGuid(),setup.BranchId,"SYNTHETIC preset "+Guid.NewGuid(),"Lab","Lần",price);}
        async Task<JsonElement> Send(){var response=examination?await d.PutAsJsonAsync(path,command):await d.PostAsJsonAsync(path,command);Assert.Equal(HttpStatusCode.OK,response.StatusCode);return await response.Content.ReadFromJsonAsync<JsonElement>();}
        var first=await Send();var replay=await Send();Assert.Equal(price,first.GetProperty("priceVnd").GetInt64());Assert.Equal(first.GetRawText(),replay.GetRawText());
    }
}
