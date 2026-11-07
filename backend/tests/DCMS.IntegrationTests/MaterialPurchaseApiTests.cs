using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DCMS.Application.Features.Authentication;
using DCMS.Application.Features.SystemSettings;
using Xunit;
namespace DCMS.IntegrationTests;

public sealed class MaterialPurchaseApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Purchases = "/api/system-settings/material-purchases";
    private async Task<HttpClient> Login(string name = "dentist.demo")
    {
        var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(name, fixture.Password));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SignInResponse>())!.AccessToken);
        return client;
    }
    private static string Unique(string text) => text + Guid.NewGuid().ToString("N");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private async Task<MaterialDto> Material(HttpClient c, string? name = null, string? usageUnit = null)
    {
        var category = await c.PostAsJsonAsync("/api/system-settings/material-categories", new CreateCategoryRequest(Unique("SYNTHETIC group "), Guid.NewGuid()));
        var group = (await category.Content.ReadFromJsonAsync<MaterialCategoryDto>())!;
        var r = await c.PostAsJsonAsync("/api/system-settings/materials", new CreateMaterialRequest(name ?? Unique("SYNTHETIC material "), group.Id, Guid.NewGuid(), usageUnit));
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync()); return (await r.Content.ReadFromJsonAsync<MaterialDto>())!;
    }
    private static CreateMaterialPurchaseRequest Create(Guid material, decimal quantity = 2, long price = 150_000, long paid = 0, DateOnly? entered = null, DateOnly? received = null, string unit = "hộp", Guid? key = null, DateOnly? expires = null, decimal factor = 1) =>
        new(entered ?? Today, received, expires, material, quantity, unit, price, paid, key ?? Guid.NewGuid(), factor);
    private static async Task<string> Body(HttpResponseMessage r) => await r.Content.ReadAsStringAsync();

    [Fact]
    public async Task CreateComputesTotalRoundsAndSnapshotsNames()
    {
        using var c = await Login(); var material = await Material(c);
        var response = await c.PostAsJsonAsync(Purchases, Create(material.Id, quantity: 0.5m, price: 1_333_333, paid: 100_000, received: Today));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = (await response.Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(666_667, row.TotalVnd); Assert.Equal(566_667, row.DueVnd); Assert.Equal(material.Name, row.MaterialName); Assert.Equal(material.CategoryName, row.CategoryName);
        Assert.Equal("hộp", row.Unit); Assert.Equal(1, row.Version);
        var renamed = await c.PutAsJsonAsync($"/api/system-settings/materials/{material.Id}", new UpdateMaterialRequest("SYNTHETIC renamed " + Guid.NewGuid().ToString("N"), material.CategoryId, material.Version, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(material.Name, (await c.GetFromJsonAsync<MaterialPurchaseDto>($"{Purchases}/{row.Id}"))!.MaterialName);
    }

    [Fact]
    public async Task UsageUnitAndConversionFactorAreSnapshottedPerRow()
    {
        using var c = await Login(); var material = await Material(c, usageUnit: "  sợi ");
        Assert.Equal("sợi", material.UsageUnit);
        var row = (await (await c.PostAsJsonAsync(Purchases, Create(material.Id, quantity: 2.5m, price: 50_000, unit: "hộp", factor: 12))).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(12, row.ConversionFactor); Assert.Equal(30, row.UsageQuantity); Assert.Equal("sợi", row.UsageUnit); Assert.Equal(4166.67m, row.UsageUnitPriceVnd); Assert.Equal(125_000, row.TotalVnd);
        var cleared = await c.PutAsJsonAsync($"/api/system-settings/materials/{material.Id}", new UpdateMaterialRequest(material.Name, material.CategoryId, material.Version, Guid.NewGuid(), null));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode); Assert.Null((await cleared.Content.ReadFromJsonAsync<MaterialDto>())!.UsageUnit);
        var after = (await c.GetFromJsonAsync<MaterialPurchaseDto>($"{Purchases}/{row.Id}"))!;
        Assert.Equal("sợi", after.UsageUnit); Assert.Equal(30, after.UsageQuantity);
        var plain = (await (await c.PostAsJsonAsync(Purchases, Create(material.Id))).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(1, plain.ConversionFactor); Assert.Equal(2, plain.UsageQuantity); Assert.Null(plain.UsageUnit);
        var tooLong = await c.PostAsJsonAsync("/api/system-settings/materials", new CreateMaterialRequest(Unique("SYNTHETIC long "), material.CategoryId, Guid.NewGuid(), new string('a', 41)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode); Assert.Contains("usageUnit", await Body(tooLong));
    }

    [Fact]
    public async Task SupplierMustBeAnActiveNonLabCompanyAndIsSnapshotted()
    {
        using var c = await Login(); var material = await Material(c);
        async Task<ProviderDto> Provider(string type)
        {
            var code = "T" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            var r = await c.PostAsJsonAsync("/api/system-settings/settings/catalogs/providers", new CreateSupplyRequest<ProviderInput>(Guid.NewGuid(), new ProviderInput(code, Unique("SYNTHETIC company "), type, null, null, null)));
            Assert.True(r.IsSuccessStatusCode, await Body(r)); return (await r.Content.ReadFromJsonAsync<ProviderDto>())!;
        }
        var supplier = await Provider(ProviderTypes.Supplier); var lab = await Provider(ProviderTypes.Lab);
        var ok = (await (await c.PostAsJsonAsync(Purchases, Create(material.Id) with { SupplierId = supplier.Id })).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(supplier.Id, ok.SupplierId); Assert.Equal(supplier.Name, ok.SupplierName);
        var none = (await (await c.PostAsJsonAsync(Purchases, Create(material.Id))).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Null(none.SupplierId); Assert.Null(none.SupplierName);
        foreach (var bad in new[] { lab.Id, Guid.NewGuid() })
        {
            var r = await c.PostAsJsonAsync(Purchases, Create(material.Id) with { SupplierId = bad });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Contains("PURCHASE_SUPPLIER_INVALID", await Body(r));
        }
    }

    [Fact]
    public async Task InvalidConversionFactorIsRejectedWithField()
    {
        using var c = await Login(); var material = await Material(c, usageUnit: "g");
        foreach (var factor in new[] { 0m, -1m, 1.2345m, 1_000_001m })
        {
            var r = await c.PostAsJsonAsync(Purchases, Create(material.Id, factor: factor));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Contains("conversionFactor", await Body(r));
        }
    }

    [Fact]
    public async Task InvalidValuesAreRejectedWithField()
    {
        using var c = await Login(); var material = await Material(c);
        async Task Expect(CreateMaterialPurchaseRequest request, string code, string field)
        {
            var r = await c.PostAsJsonAsync(Purchases, request); var text = await Body(r);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Contains(field, text); if (code != "") Assert.Contains(code, text);
        }
        await Expect(Create(material.Id, paid: 300_001), "PURCHASE_PAID_EXCEEDS_TOTAL", "paidVnd");
        await Expect(Create(material.Id, entered: Today.AddDays(1)), "PURCHASE_DATE_INVALID", "enteredOn");
        await Expect(Create(material.Id, entered: Today, received: Today.AddDays(-1)), "PURCHASE_DATE_INVALID", "receivedOn");
        await Expect(Create(material.Id, received: Today.AddDays(1)), "PURCHASE_DATE_INVALID", "receivedOn");
        await Expect(Create(material.Id, entered: Today, expires: Today.AddDays(-1)), "PURCHASE_DATE_INVALID", "expiresOn");
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Purchases, Create(material.Id, expires: Today.AddYears(3)))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Purchases, Create(material.Id, expires: Today))).StatusCode);
        await Expect(Create(material.Id, quantity: 0), "", "quantity");
        await Expect(Create(material.Id, quantity: -1), "", "quantity");
        await Expect(Create(material.Id, quantity: 1.2345m), "", "quantity");
        await Expect(Create(material.Id, unit: "  "), "", "unit");
        await Expect(Create(material.Id, price: -1), "", "unitPriceVnd");
        await Expect(Create(Guid.NewGuid()), "PURCHASE_MATERIAL_INACTIVE", "materialId");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Purchases, Create(material.Id, key: Guid.Empty))).StatusCode);
    }

    [Fact]
    public async Task UpdateDeleteVersionAndRequestKeysFollowCatalogRules()
    {
        using var c = await Login(); var material = await Material(c); var other = await Material(c);
        var key = Guid.NewGuid(); var request = Create(material.Id, key: key);
        var created = (await (await c.PostAsJsonAsync(Purchases, request)).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        var replay = (await (await c.PostAsJsonAsync(Purchases, request)).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Purchases, request with { Quantity = 3 })).StatusCode);
        var update = new UpdateMaterialPurchaseRequest(Today, null, Today.AddYears(2), other.Id, 4, "lọ", 50_000, 200_000, created.Version, Guid.NewGuid());
        var updated = (await (await c.PutAsJsonAsync($"{Purchases}/{created.Id}", update)).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!;
        Assert.Equal(Today.AddYears(2), updated.ExpiresOn); Assert.Equal(2, updated.Version); Assert.Equal(200_000, updated.TotalVnd); Assert.Equal(0, updated.DueVnd); Assert.Equal(other.Name, updated.MaterialName);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Purchases}/{created.Id}", update with { RequestKey = Guid.NewGuid() })).StatusCode);
        var staleDelete = await c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{Purchases}/{created.Id}") { Content = JsonContent.Create(new DeleteCatalogRequest(1, Guid.NewGuid())) });
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var deleted = await c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{Purchases}/{created.Id}") { Content = JsonContent.Create(new DeleteCatalogRequest(updated.Version, Guid.NewGuid())) });
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Purchases}/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task ListFiltersAndTotalsCoverTheWholeFilteredSet()
    {
        using var c = await Login(); var name = Unique("SYNTHETIC filter "); var material = await Material(c, name);
        foreach (var (days, qty, paid) in new[] { (-10, 1m, 0L), (-5, 2m, 100_000L), (-1, 1m, 100_000L) })
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Purchases, Create(material.Id, qty, 100_000, paid, Today.AddDays(days)))).StatusCode);
        var all = (await c.GetFromJsonAsync<MaterialPurchasePage>($"{Purchases}?q={name.ToLowerInvariant()}&pageSize=20"))!;
        Assert.Equal(3, all.Total); Assert.Equal(400_000, all.Totals.TotalVnd); Assert.Equal(200_000, all.Totals.PaidVnd); Assert.Equal(200_000, all.Totals.DueVnd);
        Assert.Equal(Today.AddDays(-1), all.Items[0].EnteredOn);
        Assert.Equal(1, (await c.GetFromJsonAsync<MaterialPurchasePage>($"{Purchases}?q={name}&payment=unpaid"))!.Total);
        Assert.Equal(1, (await c.GetFromJsonAsync<MaterialPurchasePage>($"{Purchases}?q={name}&payment=partial"))!.Total);
        Assert.Equal(1, (await c.GetFromJsonAsync<MaterialPurchasePage>($"{Purchases}?q={name}&payment=paid"))!.Total);
        var range = (await c.GetFromJsonAsync<MaterialPurchasePage>($"{Purchases}?q={name}&from={Today.AddDays(-6):yyyy-MM-dd}&to={Today.AddDays(-2):yyyy-MM-dd}"))!;
        Assert.Equal(1, range.Total); Assert.Equal(200_000, range.Totals.TotalVnd);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Purchases}?pageSize=7")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Purchases}?payment=weird")).StatusCode);
    }

    [Fact]
    public async Task AnonymousAndUnauthorizedCallersAreRejected()
    {
        using var anonymous = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Purchases)).StatusCode);
        using var assistant = await Login("assistant.demo");
        var r = await assistant.GetAsync(Purchases);
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden, $"{r.StatusCode}: {await Body(r)}");
    }

    [Fact]
    public async Task LotNumberIsOptionalTrimmedAndLimited()
    {
        using var c = await Login(); var material = await Material(c);
        var created = await c.PostAsJsonAsync(Purchases, Create(material.Id) with { LotNumber = "  LOT-2026/A1 " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = (await created.Content.ReadFromJsonAsync<MaterialPurchaseDto>())!; Assert.Equal("LOT-2026/A1", row.LotNumber);
        var blank = (await (await c.PostAsJsonAsync(Purchases, Create(material.Id) with { LotNumber = "   " })).Content.ReadFromJsonAsync<MaterialPurchaseDto>())!; Assert.Null(blank.LotNumber);
        var tooLong = await c.PostAsJsonAsync(Purchases, Create(material.Id) with { LotNumber = new string('9', 61) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode); Assert.Contains("lotNumber", await Body(tooLong));
    }
}
