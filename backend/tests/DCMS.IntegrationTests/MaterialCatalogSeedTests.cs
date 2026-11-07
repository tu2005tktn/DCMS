using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Docs: docs/specs/MATERIAL-CATALOG-SEED.md
public sealed class MaterialCatalogSeedTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task SeedsOnlyCatalogNamesOnceWithoutOverwritingChanges()
    {
        using var scope = fixture.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentMaterialSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var purchasesBefore = await db.Set<MaterialPurchase>().CountAsync();
        var providersBefore = await db.Set<CatalogProvider>().CountAsync();
        await seeder.SeedAsync();

        var materials = await db.MaterialCatalogItems.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync();
        Assert.True(materials.Count >= 45);
        Assert.All(materials, m => Assert.False(string.IsNullOrEmpty(m.UsageUnit)));
        Assert.Equal(purchasesBefore, await db.Set<MaterialPurchase>().CountAsync());
        Assert.Equal(providersBefore, await db.Set<CatalogProvider>().CountAsync());

        var first = await db.MaterialCatalogItems.FirstAsync(x => x.Name == "Composite đặc"); first.UsageUnit = "gam"; await db.SaveChangesAsync();
        var second = await seeder.SeedAsync();
        Assert.Contains("Dụng cụ/vật liệu: thêm 0/", second);
        Assert.Equal("gam", (await db.MaterialCatalogItems.AsNoTracking().SingleAsync(x => x.Id == first.Id)).UsageUnit);
    }
}
