using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Common;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace DCMS.IntegrationTests;

// Docs: docs/specs/MATERIAL-STOCK.md. The stock rules are exercised through the same helper the treatment sheet calls.
public sealed class MaterialStockApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Purchases = "/api/system-settings/material-purchases";
    private async Task<HttpClient> Login()
    {
        var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest("dentist.demo", fixture.Password));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private async Task<MaterialDto> Material(HttpClient c)
    {
        var category = await c.PostAsJsonAsync("/api/system-settings/material-categories", new CreateCategoryRequest("SYNTHETIC group " + Guid.NewGuid().ToString("N"), Guid.NewGuid()));
        var group = (await category.Content.ReadFromJsonAsync<MaterialCategoryDto>())!;
        var r = await c.PostAsJsonAsync("/api/system-settings/materials", new CreateMaterialRequest("SYNTHETIC stock " + Guid.NewGuid().ToString("N"), group.Id, Guid.NewGuid(), "sợi"));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<MaterialDto>())!;
    }
    private async Task Use(Guid activity, params MaterialUseLine[]? lines)
    {
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DcmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await MaterialStock.ReplaceAsync(db, TimeProvider.System, activity, 1, 1, null, DateTimeOffset.UtcNow, null, lines, default);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    private async Task<(decimal Received, decimal Used, decimal Stock)> Stock(HttpClient c, MaterialDto m)
    {
        var items = (await c.GetFromJsonAsync<MaterialStockPage>($"/api/system-settings/material-stock?q={Uri.EscapeDataString(m.Name)}&pageSize=100"))!.Items.Where(x => x.MaterialId == m.Id).ToArray();
        return (items.Sum(x => x.Received), items.Sum(x => x.Used), items.Sum(x => x.Remaining));
    }

    [Fact]
    public async Task UsageDeductsReceivedStockOnlyAndIsReplacedOrReleasedPerActivity()
    {
        using var c = await Login(); var m = await Material(c);
        using (var seed = fixture.Services.CreateScope()) await seed.ServiceProvider.GetRequiredService<DCMS.Infrastructure.Features.Scheduling.DevelopmentSchedulingSeeder>().SeedAsync(); // lịch sử dùng lọc theo chi nhánh đang hoạt động
        // 2 hộp x 10 = 20 received; 5 not yet received must not count.
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, Today, null, m.Id, 2, "hộp", 1000, 0, Guid.NewGuid(), 10));
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, null, null, m.Id, 5, "hộp", 1000, 0, Guid.NewGuid(), 10));
        Assert.Equal(20, (await Stock(c, m)).Stock);
        var a = Guid.NewGuid();
        await Use(a, new MaterialUseLine(m.Id, 8));
        var s = await Stock(c, m); Assert.Equal(8, s.Used); Assert.Equal(12, s.Stock);
        await Use(a, new MaterialUseLine(m.Id, 20));                 // replaces 8 with 20 → exactly the stock
        Assert.Equal(0, (await Stock(c, m)).Stock);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Use(Guid.NewGuid(), new MaterialUseLine(m.Id, 0.001m)));
        Assert.Equal(409, ex.Status); Assert.Equal("MATERIAL_STOCK_INSUFFICIENT", ex.Response.Code);
        await Use(a, null);                                             // null keeps lines
        Assert.Equal(20, (await Stock(c, m)).Used);
        var history = (await c.GetFromJsonAsync<MaterialUsagePage>($"/api/system-settings/material-usage?materialId={m.Id}"))!;
        var line = Assert.Single(history.Items); Assert.Equal(20, line.Quantity); Assert.Equal("sợi", line.UsageUnit);
        await Use(a, []);                                               // empty list releases
        Assert.Equal(20, (await Stock(c, m)).Stock);
        Assert.Empty((await c.GetFromJsonAsync<MaterialUsagePage>($"/api/system-settings/material-usage?materialId={m.Id}"))!.Items);
    }

    [Fact]
    public async Task PurchaseChangesThatWouldMakeStockNegativeAreRejected()
    {
        using var c = await Login(); var m = await Material(c);
        var purchase = (await (await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, Today, null, m.Id, 3, "hộp", 1000, 0, Guid.NewGuid(), 1))).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        await Use(Guid.NewGuid(), new MaterialUseLine(m.Id, 2));
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"{Purchases}/{purchase.Id}") { Content = JsonContent.Create(new DeleteCatalogRequest(purchase.Version, Guid.NewGuid())) };
        var blocked = await c.SendAsync(delete);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode); Assert.Contains("MATERIAL_STOCK_NEGATIVE", await blocked.Content.ReadAsStringAsync());
        var shrink = await c.PutAsJsonAsync($"{Purchases}/{purchase.Id}", new UpdateMaterialPurchaseRequest(Today, Today, null, m.Id, 1, "hộp", 1000, 0, purchase.Version, Guid.NewGuid(), 1));
        Assert.Equal(HttpStatusCode.Conflict, shrink.StatusCode);
        Assert.Equal(3, (await Stock(c, m)).Received);
    }

    [Fact]
    public async Task InvalidUsageLinesAreRejected()
    {
        using var c = await Login(); var m = await Material(c);
        await Assert.ThrowsAsync<BusinessException>(() => Use(Guid.NewGuid(), new MaterialUseLine(m.Id, 0)));
        await Assert.ThrowsAsync<BusinessException>(() => Use(Guid.NewGuid(), new MaterialUseLine(m.Id, 1), new MaterialUseLine(m.Id, 1)));
        await Assert.ThrowsAsync<BusinessException>(() => Use(Guid.NewGuid(), new MaterialUseLine(Guid.NewGuid(), 1)));
    }

    [Fact]
    public async Task StockListsEachReceivedPurchaseAndAllocatesUsageToEarliestExpiryFirst()
    {
        using var c = await Login(); var m = await Material(c);
        var late = Today.AddDays(200); var soon = Today.AddDays(30);
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, Today, late, m.Id, 10, "hộp", 1000, 0, Guid.NewGuid(), 1));
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, Today, soon, m.Id, 4, "hộp", 1000, 0, Guid.NewGuid(), 1));
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, null, soon, m.Id, 9, "hộp", 1000, 0, Guid.NewGuid(), 1));
        await Use(Guid.NewGuid(), new MaterialUseLine(m.Id, 6));
        var rows = (await c.GetFromJsonAsync<MaterialStockPage>($"/api/system-settings/material-stock?q={Uri.EscapeDataString(m.Name)}&pageSize=100"))!.Items.Where(x => x.MaterialId == m.Id).ToArray();
        Assert.Equal(2, rows.Length);                                   // the unreceived purchase is not listed
        Assert.Equal(soon, rows[0].ExpiresOn); Assert.Equal(4, rows[0].Used); Assert.Equal(0, rows[0].Remaining);
        Assert.Equal(late, rows[1].ExpiresOn); Assert.Equal(2, rows[1].Used); Assert.Equal(8, rows[1].Remaining);
    }

    [Fact]
    public async Task AvailableListsOnlyMaterialsWithStockAndTheirTotals()
    {
        using var c = await Login(); var withStock = await Material(c); var empty = await Material(c);
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, Today, null, withStock.Id, 5, "hộp", 1000, 0, Guid.NewGuid(), 2));
        await c.PostAsJsonAsync(Purchases, new CreateMaterialPurchaseRequest(Today, null, null, empty.Id, 5, "hộp", 1000, 0, Guid.NewGuid(), 1));
        await Use(Guid.NewGuid(), new MaterialUseLine(withStock.Id, 4));
        var page = (await c.GetFromJsonAsync<MaterialAvailablePage>("/api/system-settings/material-stock/available?q=SYNTHETIC%20stock&pageSize=100"))!;
        var row = Assert.Single(page.Items, x => x.MaterialId == withStock.Id); Assert.Equal(6, row.Stock); Assert.Equal("sợi", row.UsageUnit);
        Assert.DoesNotContain(page.Items, x => x.MaterialId == empty.Id);
        await Use(Guid.NewGuid(), new MaterialUseLine(withStock.Id, 6));
        Assert.DoesNotContain((await c.GetFromJsonAsync<MaterialAvailablePage>("/api/system-settings/material-stock/available?q=SYNTHETIC%20stock&pageSize=100"))!.Items, x => x.MaterialId == withStock.Id);
    }
}
