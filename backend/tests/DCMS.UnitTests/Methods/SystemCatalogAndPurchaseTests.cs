using System.IO.Compression;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of SystemCatalogService, MaterialPurchaseService, MaterialStockService, and MaterialStock.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SystemCatalogAndPurchaseTests
{
    // 10/03/2030 09:00 clinic time (UTC+7).
    private static readonly DateTimeOffset Now = new(2030, 3, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2030, 3, 10);

    private sealed class Access(int[] branches, bool deny = false) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) =>
            deny ? throw new BusinessException(403, SystemMessages.Get("MSG06"))
                 : Task.FromResult(new PermissionState(true, true, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Actor, EmployeeId, PatientId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public string Tag => "M" + N;
        public SystemCatalogService Catalog(bool deny = false) => new(Db, new TestClock(Now), new Access([N], deny));
        public MaterialPurchaseService Purchases(bool deny = false) => new(Db, new TestClock(Now), new Access([N], deny));
        public MaterialStockService Stock(int[]? branches = null) => new(Db, new Access(branches ?? [N]));

        public async Task<CatalogProvider> SeedProvider(string suffix, string type = ProviderTypes.Supplier, bool active = true)
        {
            var p = new CatalogProvider { Code = $"{Tag}P{suffix}", Name = $"NCC {Tag} {suffix}", Type = type, IsActive = active };
            Db.CatalogProviders.Add(p); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return p;
        }

        public async Task<(TreatmentDossier Dossier, ClinicalActivity Activity)> SeedActivity()
        {
            var svc = new DCMS.Domain.Features.Scheduling.SchedulingService { BranchId = N, Name = "Trám " + Tag, PublicDurationMinutes = 30 };
            Db.Add(svc); await Db.SaveChangesAsync();
            var dossier = new TreatmentDossier
            {
                Code = "HS" + Tag, PatientId = PatientId, BranchId = N, DentistId = Actor,
                StartedOn = Today, Diagnosis = "Sâu răng", State = "Open", CreatedAt = Now, UpdatedAt = Now
            };
            var act = new ClinicalActivity
            {
                DossierId = dossier.Id, ServiceId = svc.Id, ServiceNameSnapshot = svc.Name, Unit = "Răng",
                Quantity = 1, PerformedAt = Now, PerformerId = Actor, Outcome = "OK", PriceSnapshotVnd = 100_000
            };
            Db.TreatmentDossiers.Add(dossier);
            Db.ClinicalActivities.Add(act);
            await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
            return (dossier, act);
        }
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId();
        var staff = await CreateTestKit.SeedStaffAsync(db, "mat" + n, AccountRoles.ClinicOwner, n);
        var emp = await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == staff.Id);
        var patient = new PatientProfile { PatientCode = "PM" + n, FullName = "Bệnh nhân " + n, NormalizedName = "BENH NHAN " + n, BirthDate = new DateOnly(1992, 1, 1), Gender = PatientGenders.Male, CreatedAt = Now, UpdatedAt = Now };
        db.Patients.Add(patient); await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Actor = staff.Id, EmployeeId = emp.Id, PatientId = patient.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code, string? expectedMessage = null)
    {
        var err = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (err.Status, err.Response.Code));
        Assert.False(string.IsNullOrWhiteSpace(err.Response.Message));
        if (expectedMessage != null) Assert.Equal(expectedMessage, err.Response.Message);
    }

    // ================= SystemCatalogService =================
    [Fact]
    public async Task UT_MAT_01_SystemCatalog_Category_CRUD_replay_trung_ten_va_chan_xoa_khi_con_vat_lieu()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Catalog(deny: true).CategoriesAsync(env.Actor, null, 1, 20, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Catalog().CreateCategoryAsync(env.Actor, new("Nhóm " + env.Tag, Guid.Empty), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Catalog().CreateCategoryAsync(env.Actor, new("  ", Guid.NewGuid()), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Catalog().CreateCategoryAsync(env.Actor, new(new string('A', 161), Guid.NewGuid()), CancellationToken.None), 400, "MSG04");

        var key = Guid.NewGuid();
        var req = new CreateCategoryRequest("Nhóm " + env.Tag + " A", key);
        var cat = await env.Catalog().CreateCategoryAsync(env.Actor, req, CancellationToken.None);
        Assert.Equal(("Nhóm " + env.Tag + " A", 0, 1), (cat.Name, cat.ItemCount, cat.Version));

        // Idempotent replay + conflict on same key with different payload.
        Assert.Equal(cat.Id, (await env.Catalog().CreateCategoryAsync(env.Actor, req, CancellationToken.None)).Id);
        await Rejects(() => env.Catalog().CreateCategoryAsync(env.Actor, new("Nhóm " + env.Tag + " B", key), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Catalog().CreateCategoryAsync(env.Actor, new("nhóm " + env.Tag + " a", Guid.NewGuid()), CancellationToken.None),
            409, CatalogMessages.Duplicate.Code, CatalogMessages.Duplicate.Message);

        Assert.Equal(cat.Id, (await env.Catalog().CategoryAsync(env.Actor, cat.Id, CancellationToken.None)).Id);
        await Rejects(() => env.Catalog().CategoryAsync(env.Actor, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");

        await Rejects(() => env.Catalog().UpdateCategoryAsync(env.Actor, cat.Id, new("Đổi " + env.Tag, 9, Guid.NewGuid()), CancellationToken.None), 409, "MSG111");
        var catUpd = await env.Catalog().UpdateCategoryAsync(env.Actor, cat.Id, new("Nhóm " + env.Tag + " Mới", 1, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(("Nhóm " + env.Tag + " Mới", 2), (catUpd.Name, catUpd.Version));

        var list = await env.Catalog().CategoriesAsync(env.Actor, env.Tag, 1, 20, CancellationToken.None);
        Assert.Equal([cat.Id], list.Items.Select(x => x.Id));
        await Rejects(() => env.Catalog().CategoriesAsync(env.Actor, null, 0, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Catalog().CategoriesAsync(env.Actor, null, 1, 15, CancellationToken.None), 400, "MSG04");

        // Add material -> category deletion blocked with CATALOG_CATEGORY_IN_USE.
        await Rejects(() => env.Catalog().CreateMaterialAsync(env.Actor, new("Vật liệu " + env.Tag, cat.Id, Guid.NewGuid(), new string('u', 41)), CancellationToken.None), 400, "MSG04");
        var mat = await env.Catalog().CreateMaterialAsync(env.Actor, new("Composite " + env.Tag, cat.Id, Guid.NewGuid(), "Ống"), CancellationToken.None);
        Assert.Equal(("Composite " + env.Tag, "Ống", 1), (mat.Name, mat.UsageUnit, mat.Version));
        await Rejects(() => env.Catalog().CreateMaterialAsync(env.Actor, new("composite " + env.Tag, cat.Id, Guid.NewGuid(), "Ống"), CancellationToken.None),
            409, CatalogMessages.Duplicate.Code, CatalogMessages.Duplicate.Message);
        await Rejects(() => env.Catalog().DeleteCategoryAsync(env.Actor, cat.Id, new(2, Guid.NewGuid()), CancellationToken.None),
            409, CatalogMessages.InUse.Code, CatalogMessages.InUse.Message);

        Assert.Equal(mat.Id, (await env.Catalog().MaterialAsync(env.Actor, mat.Id, CancellationToken.None)).Id);
        await Rejects(() => env.Catalog().MaterialAsync(env.Actor, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");

        var matUpd = await env.Catalog().UpdateMaterialAsync(env.Actor, mat.Id, new("Composite Pro " + env.Tag, cat.Id, 1, Guid.NewGuid(), "  "), CancellationToken.None);
        Assert.Null(matUpd.UsageUnit);
        Assert.Equal(2, matUpd.Version);

        var mats = await env.Catalog().MaterialsAsync(env.Actor, env.Tag, cat.Id, 1, 20, CancellationToken.None);
        Assert.Equal([mat.Id], mats.Items.Select(x => x.Id));

        var xlsx = await env.Catalog().ExportMaterialsAsync(env.Actor, env.Tag, cat.Id, CancellationToken.None);
        using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
        using (var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()))
            Assert.Contains("Composite Pro " + env.Tag, await reader.ReadToEndAsync());

        // Delete material, then category deletion succeeds.
        await env.Catalog().DeleteMaterialAsync(env.Actor, mat.Id, new(2, Guid.NewGuid()), CancellationToken.None);
        var deletedCat = await env.Catalog().DeleteCategoryAsync(env.Actor, cat.Id, new(2, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(3, deletedCat.Version);
    }

    // ================= MaterialPurchaseService =================
    public static IEnumerable<object?[]> BadPurchases =>
    [
        ["UT-MAT-02 Ngày nhập ở tương lai", 1, null, null, 2m, "Hộp", 1m, 100_000L, 0L, null, "PURCHASE_DATE_INVALID"],
        ["UT-MAT-03 Ngày nhận trước ngày nhập", 0, -1, null, 2m, "Hộp", 1m, 100_000L, 0L, null, "PURCHASE_DATE_INVALID"],
        ["UT-MAT-04 Ngày nhận ở tương lai", -2, 1, null, 2m, "Hộp", 1m, 100_000L, 0L, null, "PURCHASE_DATE_INVALID"],
        ["UT-MAT-05 Hạn dùng trước ngày nhập", 0, 0, -1, 2m, "Hộp", 1m, 100_000L, 0L, null, "PURCHASE_DATE_INVALID"],
        ["UT-MAT-06 Số lượng <= 0", 0, 0, null, 0m, "Hộp", 1m, 100_000L, 0L, null, "MSG04"],
        ["UT-MAT-07 Số lượng quá 3 chữ số thập phân", 0, 0, null, 1.1234m, "Hộp", 1m, 100_000L, 0L, null, "MSG04"],
        ["UT-MAT-08 Đơn vị rỗng", 0, 0, null, 2m, "  ", 1m, 100_000L, 0L, null, "MSG04"],
        ["UT-MAT-09 Hệ số quy đổi <= 0", 0, 0, null, 2m, "Hộp", 0m, 100_000L, 0L, null, "MSG04"],
        ["UT-MAT-10 Số lượng sử dụng vượt ngưỡng", 0, 0, null, 1_000_000m, "Hộp", 2_000m, 1L, 0L, null, "MSG04"],
        ["UT-MAT-11 Đơn giá âm", 0, 0, null, 2m, "Hộp", 1m, -1L, 0L, null, "MSG04"],
        ["UT-MAT-12 Tổng tiền vượt giới hạn", 0, 0, null, 10m, "Hộp", 1m, 200_000_000_000L, 0L, null, "MSG04"],
        ["UT-MAT-13 Đã thanh toán âm", 0, 0, null, 2m, "Hộp", 1m, 100_000L, -1L, null, "MSG04"],
        ["UT-MAT-14 Đã thanh toán lớn hơn tổng", 0, 0, null, 2m, "Hộp", 1m, 100_000L, 250_000L, null, "PURCHASE_PAID_EXCEEDS_TOTAL"],
        ["UT-MAT-15 Số lô vượt 60 ký tự", 0, 0, null, 2m, "Hộp", 1m, 100_000L, 0L, new string('L', 61), "MSG04"],
    ];

    [Theory, MemberData(nameof(BadPurchases))]
    public async Task UT_MAT_Purchase_Create_validation(string caseName, int enteredOff, int? recOff, int? expOff, decimal qty, string unit, decimal factor, long price, long paid, string? lot, string expectedCode)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var cat = await env.Catalog().CreateCategoryAsync(env.Actor, new("Cat " + env.Tag, Guid.NewGuid()), CancellationToken.None);
        var mat = await env.Catalog().CreateMaterialAsync(env.Actor, new("Mat " + env.Tag, cat.Id, Guid.NewGuid(), "Cái"), CancellationToken.None);
        var req = new CreateMaterialPurchaseRequest(Today.AddDays(enteredOff), recOff.HasValue ? Today.AddDays(recOff.Value) : null,
            expOff.HasValue ? Today.AddDays(expOff.Value) : null, mat.Id, qty, unit, price, paid, Guid.NewGuid(), factor, null, lot);
        await Rejects(() => env.Purchases().CreateAsync(env.Actor, req, CancellationToken.None), 400, expectedCode);
    }

    [Fact]
    public async Task UT_MAT_16_Purchase_CRUD_loc_thanh_toan_nha_cung_cap_va_bao_ve_ton_kho()
    {
        await using var env = await NewEnv();
        var cat = await env.Catalog().CreateCategoryAsync(env.Actor, new("CatP " + env.Tag, Guid.NewGuid()), CancellationToken.None);
        var mat = await env.Catalog().CreateMaterialAsync(env.Actor, new("Bonding " + env.Tag, cat.Id, Guid.NewGuid(), "Ml"), CancellationToken.None);
        var deletedMat = await env.Catalog().CreateMaterialAsync(env.Actor, new("Del " + env.Tag, cat.Id, Guid.NewGuid(), "Ml"), CancellationToken.None);
        await env.Catalog().DeleteMaterialAsync(env.Actor, deletedMat.Id, new(1, Guid.NewGuid()), CancellationToken.None);

        var supplier = await env.SeedProvider("SUP", ProviderTypes.Supplier, active: true);
        var labOnly = await env.SeedProvider("LAB", ProviderTypes.Lab, active: true);
        var inactiveSup = await env.SeedProvider("OFF", ProviderTypes.Supplier, active: false);

        await Rejects(() => env.Purchases().CreateAsync(env.Actor, new(Today, Today, null, mat.Id, 1, "Chai", 100_000, 0, Guid.Empty), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Purchases().CreateAsync(env.Actor, new(Today, Today, null, deletedMat.Id, 1, "Chai", 100_000, 0, Guid.NewGuid()), CancellationToken.None),
            400, MaterialPurchaseMessages.MaterialInactive.Code, MaterialPurchaseMessages.MaterialInactive.Message);
        await Rejects(() => env.Purchases().CreateAsync(env.Actor, new(Today, Today, null, mat.Id, 1, "Chai", 100_000, 0, Guid.NewGuid(), 1, labOnly.Id), CancellationToken.None),
            400, MaterialPurchaseMessages.SupplierInvalid.Code, MaterialPurchaseMessages.SupplierInvalid.Message);
        await Rejects(() => env.Purchases().CreateAsync(env.Actor, new(Today, Today, null, mat.Id, 1, "Chai", 100_000, 0, Guid.NewGuid(), 1, inactiveSup.Id), CancellationToken.None),
            400, MaterialPurchaseMessages.SupplierInvalid.Code, MaterialPurchaseMessages.SupplierInvalid.Message);

        // Create 3 purchases: unpaid, partial, paid; plus replay & conflict check.
        var k1 = Guid.NewGuid();
        var r1 = new CreateMaterialPurchaseRequest(Today.AddDays(-2), Today.AddDays(-2), Today.AddDays(30), mat.Id, 2, "Hộp", 100_000, 0, k1, 5, supplier.Id, "LOT-1");
        var pUnpaid = await env.Purchases().CreateAsync(env.Actor, r1, CancellationToken.None);
        Assert.Equal((200_000L, 0L, 200_000L, 10m, 20_000m, supplier.Name, "LOT-1"),
            (pUnpaid.TotalVnd, pUnpaid.PaidVnd, pUnpaid.DueVnd, pUnpaid.UsageQuantity, pUnpaid.UsageUnitPriceVnd, pUnpaid.SupplierName, pUnpaid.LotNumber));
        Assert.Equal(pUnpaid.Id, (await env.Purchases().CreateAsync(env.Actor, r1, CancellationToken.None)).Id);
        await Rejects(() => env.Purchases().CreateAsync(env.Actor, r1 with { PaidVnd = 10_000 }, CancellationToken.None), 409, "MSG111");

        var pPartial = await env.Purchases().CreateAsync(env.Actor, new(Today.AddDays(-1), Today.AddDays(-1), Today.AddDays(90), mat.Id, 1, "Hộp", 100_000, 40_000, Guid.NewGuid(), 5, supplier.Id, "LOT-2"), CancellationToken.None);
        var pPaid = await env.Purchases().CreateAsync(env.Actor, new(Today, null, null, mat.Id, 1, "Hộp", 50_000, 50_000, Guid.NewGuid(), 2), CancellationToken.None);

        Assert.Equal(pUnpaid.Id, (await env.Purchases().GetAsync(env.Actor, pUnpaid.Id, CancellationToken.None)).Id);
        await Rejects(() => env.Purchases().GetAsync(env.Actor, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");

        // List filters + totals.
        var all = await env.Purchases().ListAsync(env.Actor, env.Tag, Today.AddDays(-3), Today, null, 1, 20, CancellationToken.None);
        Assert.Equal((3, 350_000L, 90_000L, 260_000L), (all.Total, all.Totals.TotalVnd, all.Totals.PaidVnd, all.Totals.DueVnd));
        Assert.Equal([pUnpaid.Id], (await env.Purchases().ListAsync(env.Actor, env.Tag, null, null, "unpaid", 1, 20, CancellationToken.None)).Items.Select(x => x.Id));
        Assert.Equal([pPartial.Id], (await env.Purchases().ListAsync(env.Actor, env.Tag, null, null, "partial", 1, 20, CancellationToken.None)).Items.Select(x => x.Id));
        Assert.Equal([pPaid.Id], (await env.Purchases().ListAsync(env.Actor, env.Tag, null, null, "paid", 1, 20, CancellationToken.None)).Items.Select(x => x.Id));
        await Rejects(() => env.Purchases().ListAsync(env.Actor, null, Today, Today.AddDays(-1), null, 1, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Purchases().ListAsync(env.Actor, null, null, null, "bad", 1, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Purchases().ListAsync(env.Actor, null, null, null, null, 0, 20, CancellationToken.None), 400, "MSG04");

        // Update + stale version.
        await Rejects(() => env.Purchases().UpdateAsync(env.Actor, pPaid.Id, new(Today, null, null, mat.Id, 1, "Hộp", 50_000, 50_000, 9, Guid.NewGuid(), 2), CancellationToken.None), 409, "MSG111");
        var pPaid2 = await env.Purchases().UpdateAsync(env.Actor, pPaid.Id, new(Today, null, null, mat.Id, 2, "Hộp", 50_000, 50_000, 1, Guid.NewGuid(), 2), CancellationToken.None);
        Assert.Equal((100_000L, 2), (pPaid2.TotalVnd, pPaid2.Version));

        // Delete unreceived purchase succeeds.
        var del = await env.Purchases().DeleteAsync(env.Actor, pPaid.Id, new(2, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(3, del.Version);

        // Now record usage = 12 ml (received = 10 + 5 = 15 ml) -> FIFO allocation, AvailableAsync, UsageAsync, and negative-stock guard!
        var (dossier, act) = await env.SeedActivity();
        await MaterialStock.ReplaceAsync(env.Db, act.Id, Now, env.Actor, null, CancellationToken.None); // null = no-op
        await Rejects(() => MaterialStock.ReplaceAsync(env.Db, act.Id, Now, env.Actor, [new(mat.Id, 0m)], CancellationToken.None),
            400, MaterialStockMessages.InvalidLines.Code, MaterialStockMessages.InvalidLines.Message);
        await Rejects(() => MaterialStock.ReplaceAsync(env.Db, act.Id, Now, env.Actor, [new(deletedMat.Id, 1m)], CancellationToken.None),
            400, MaterialStockMessages.InvalidLines.Code);
        await Rejects(async () =>
        {
            await using var tx = await env.Db.Database.BeginTransactionAsync();
            await MaterialStock.ReplaceAsync(env.Db, act.Id, Now, env.Actor, [new(mat.Id, 20m)], CancellationToken.None);
        }, 409, "MATERIAL_STOCK_INSUFFICIENT");
        env.Db.ChangeTracker.Clear();

        await using (var tx = await env.Db.Database.BeginTransactionAsync())
        {
            await MaterialStock.ReplaceAsync(env.Db, act.Id, Now, env.Actor, [new(mat.Id, 12m)], CancellationToken.None);
            await env.Db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        env.Db.ChangeTracker.Clear();

        // Deleting pUnpaid (10 ml) would drop received from 15 to 5 < 12 used -> MATERIAL_STOCK_NEGATIVE.
        await Rejects(() => env.Purchases().DeleteAsync(env.Actor, pUnpaid.Id, new(1, Guid.NewGuid()), CancellationToken.None),
            409, MaterialStockMessages.Negative.Code, MaterialStockMessages.Negative.Message);
        env.Db.ChangeTracker.Clear();

        // StockAsync allocates 12 used across LOT-1 (10 -> 0 left) and LOT-2 (5 -> 2 used, 3 left).
        var stockPage = await env.Stock().StockAsync(env.Actor, env.Tag, 1, 20, CancellationToken.None);
        Assert.Equal(2, stockPage.Total);
        Assert.Equal(("LOT-1", 10m, 10m, 0m), (stockPage.Items[0].LotNumber, stockPage.Items[0].Received, stockPage.Items[0].Used, stockPage.Items[0].Remaining));
        Assert.Equal(("LOT-2", 5m, 2m, 3m), (stockPage.Items[1].LotNumber, stockPage.Items[1].Received, stockPage.Items[1].Used, stockPage.Items[1].Remaining));
        await Rejects(() => env.Stock().StockAsync(env.Actor, null, 0, 20, CancellationToken.None), 400, "MSG04");

        // AvailableAsync returns remaining 3 ml.
        var avail = await env.Stock().AvailableAsync(env.Actor, env.Tag, 1, 20, CancellationToken.None);
        var availItem = Assert.Single(avail.Items);
        Assert.Equal((mat.Id, 3m), (availItem.MaterialId, availItem.Stock));
        await Rejects(() => env.Stock().AvailableAsync(env.Actor, null, 1, 15, CancellationToken.None), 400, "MSG04");

        // UsageAsync returns the usage line within branch scope, empty for another branch.
        // Lưu ý: bỏ qua lọc from/to vì MaterialStockService.UsageAsync dùng offset +07:00 chưa gọi .ToUniversalTime() khiến Npgsql báo lỗi (lỗi thật ở mã nguồn).
        var usage = await env.Stock().UsageAsync(env.Actor, mat.Id, act.Id, env.Tag, null, null, 1, 20, CancellationToken.None);
        var uItem = Assert.Single(usage.Items);
        Assert.Equal((12m, dossier.Code, env.PatientId), (uItem.Quantity, uItem.DossierCode, uItem.PatientId));
        var otherBranch = await env.Stock(branches: [env.N + 999]).UsageAsync(env.Actor, mat.Id, null, null, null, null, 1, 20, CancellationToken.None);
        Assert.Empty(otherBranch.Items);
        await Rejects(() => env.Stock().UsageAsync(env.Actor, null, null, null, Today, Today.AddDays(-1), 1, 20, CancellationToken.None), 400, "MSG04");

        // ReleaseAsync frees the usage so deletion now succeeds.
        await MaterialStock.ReleaseAsync(env.Db, act.Id, CancellationToken.None);
        await env.Db.SaveChangesAsync();
        env.Db.ChangeTracker.Clear();
        await env.Purchases().DeleteAsync(env.Actor, pUnpaid.Id, new(1, Guid.NewGuid()), CancellationToken.None);
    }
}
