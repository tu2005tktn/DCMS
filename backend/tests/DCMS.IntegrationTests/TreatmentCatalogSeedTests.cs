using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Docs: docs/specs/TREATMENT-CATALOG-SEED.md
public sealed class TreatmentCatalogSeedTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task SeedsServicesAndSeventeenPlansOnceWithoutOverwritingChanges()
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSchedulingSeeder>().SeedAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentTreatmentCatalogSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        var demoBranch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Name == "S09 - Chi nhánh thử nghiệm");
        var examination = await db.Set<SchedulingService>().SingleAsync(x => x.BranchId == demoBranch.Id && x.IsInitialExamination);
        db.Add(new SchedulingService { BranchId = demoBranch.Id, Name = examination.Name, PublicBookable = false });
        await db.SaveChangesAsync();
        await seeder.SeedAsync();
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Name == "S09 - Chi nhánh thử nghiệm");
        var plans = await db.TreatmentTemplates.Where(x => x.BranchId == branch.Id && x.DefinitionKind == "ItemPlan" && x.DeletedAt == null).ToListAsync();
        Assert.Equal(17, plans.Count);
        var serviceIds = await db.Set<SchedulingService>().Where(x => x.BranchId == branch.Id).Select(x => x.Id).ToListAsync();
        foreach (var plan in plans)
        {
            var rows = await db.TreatmentTemplateItems.Where(x => x.TemplateId == plan.Id && x.DeletedAt == null).ToListAsync();
            Assert.NotEmpty(rows);
            Assert.All(rows, r => { Assert.Contains(r.ServiceId, serviceIds); Assert.InRange(r.EstimatedMinutes ?? 0, 1, 480); Assert.True(r.CostVnd >= 0); });
            Assert.All(rows.Where(r => r.ServiceName == examination.Name), r => Assert.Equal(examination.Id, r.ServiceId));
        }
        Assert.Equal(1, await db.Set<SchedulingService>().CountAsync(x => x.BranchId == branch.Id && x.IsInitialExamination));

        // Rerun: edited prices are kept; only the plan the user deleted comes back.
        var service = await db.Set<SchedulingService>().FirstAsync(x => x.BranchId == branch.Id && x.Name == "Trám răng composite");
        service.ReferenceUnitPriceVnd = 123_000; plans[0].DeletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        var second = await seeder.SeedAsync();
        Assert.Contains("Dịch vụ: thêm 0", second); Assert.Contains("Kế hoạch: thêm 1", second);
        Assert.Equal(123_000, (await db.Set<SchedulingService>().AsNoTracking().SingleAsync(x => x.Id == service.Id)).ReferenceUnitPriceVnd);
        Assert.Equal(17, await db.TreatmentTemplates.AsNoTracking().CountAsync(x => x.BranchId == branch.Id && x.DefinitionKind == "ItemPlan" && x.DeletedAt == null));
    }
}
