using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Scheduling;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.IntegrationTests;

// Lịch sử dùng vật tư chỉ hiển thị dòng thuộc chi nhánh trong phạm vi người xem (docs/specs/S58-BRANCHES-AND-CHAIRS.md §4).
public sealed class MaterialUsageBranchTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpClient> Login(string user)
    {
        var c = fixture.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(user, fixture.Password));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await r.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return c;
    }

    [Fact]
    public async Task MaterialUsageListOnlyShowsOwnBranchRowsAndOwnerSeesAll()
    {
        int a, b; var name = "SYNTHETIC-VT-" + Guid.NewGuid().ToString("N")[..8];
        using (var s = fixture.Services.CreateScope())
        {
            await s.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Scheduling.DevelopmentSchedulingSeeder>().SeedAsync();
            var db = s.ServiceProvider.GetRequiredService<DcmsDbContext>();
            a = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh thử nghiệm").Select(x => x.Id).SingleAsync();
            b = await db.Set<SchedulingBranch>().Where(x => x.Name == "S09 - Chi nhánh B").Select(x => x.Id).SingleAsync();
            var cat = new MaterialCategory { Name = name, NormalizedName = name.ToUpperInvariant(), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var item = new MaterialCatalogItem { Name = name, NormalizedName = name.ToUpperInvariant(), CategoryId = cat.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.AddRange(cat, item); await db.SaveChangesAsync();
            foreach (var branch in new[] { a, b })
                db.MaterialUsages.Add(new MaterialUsage { ActivityId = Guid.NewGuid(), MaterialId = item.Id, MaterialName = $"{name}-{branch}", Quantity = 1,
                    PatientId = await db.Patients.Select(x => x.Id).FirstAsync(), BranchId = branch, UsedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        string Url() => $"/api/system-settings/material-usage?q={name}&pageSize=100";
        async Task<string[]> Names(HttpClient c) => (await c.GetFromJsonAsync<DCMS.Application.Features.SystemSettings.MaterialUsagePage>(Url()))!.Items.Select(x => x.MaterialName).ToArray();
        using var dentistA = await Login("dentist.demo"); using var dentistB = await Login("dentist.b.demo"); using var owner = await Login("owner.demo");
        Assert.Equal([$"{name}-{a}"], await Names(dentistA));
        Assert.Equal([$"{name}-{b}"], await Names(dentistB));
        Assert.Equal(2, (await Names(owner)).Length);
    }
}
