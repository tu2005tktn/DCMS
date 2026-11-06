using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Docs: docs/specs/LAB-CATALOG-SEED.md
public sealed class LabCatalogSeedTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task SeedsLabsAndWorkTypesOnceWithoutOverwritingChanges()
    {
        using var scope = fixture.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentLabSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await seeder.SeedAsync();
        var labs = await db.Set<CatalogProvider>().Where(x => x.Code.StartsWith("LAB-0")).ToListAsync();
        Assert.Equal(2, labs.Count); Assert.All(labs, l => Assert.Equal("Lab", l.Type));
        var types = await db.LabWorkTypes.Where(x => x.Code.StartsWith("LW-")).ToListAsync();
        Assert.Equal(7, types.Count);
        Assert.All(types, t => { Assert.Contains(t.Unit, new[] { "Jaw", "HalfJaw", "Tooth", "Piece", "Set" }); Assert.Contains(labs, l => l.Id == t.ProviderId); Assert.Null(t.ReferencePrice); });

        labs[0].Name = "Xưởng đã đổi tên"; await db.SaveChangesAsync();
        var second = await seeder.SeedAsync();
        Assert.Contains("Công ty labo: thêm 0.", second); Assert.Contains("thêm 0/", second);
        Assert.Equal("Xưởng đã đổi tên", (await db.Set<CatalogProvider>().AsNoTracking().SingleAsync(x => x.Id == labs[0].Id)).Name);
    }
}
