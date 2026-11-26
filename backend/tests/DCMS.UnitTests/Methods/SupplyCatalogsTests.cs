using System.IO.Compression;
using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of ProviderCatalog, LabWorkTypeCatalog, MedicineCatalog, SupplyCatalog base & SupplyCatalogSupport.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SupplyCatalogsTests
{
    private static readonly DateTimeOffset Now = new(2030, 3, 15, 2, 0, 0, TimeSpan.Zero);

    private sealed class Access(bool deny = false) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) =>
            deny ? throw new BusinessException(403, SystemMessages.Get("MSG06"))
                 : Task.FromResult(new PermissionState(true, true, [], [], []));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Actor;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public string Tag => "S" + N;
        public ProviderCatalog Providers(bool deny = false) => new(Db, new TestClock(Now), new Access(deny));
        public LabWorkTypeCatalog LabTypes(bool deny = false) => new(Db, new TestClock(Now), new Access(deny));
        public MedicineCatalog Medicines(bool deny = false) => new(Db, new TestClock(Now), new Access(deny));
        public ProviderInput Prov(string suffix, string type = ProviderTypes.Both, string? phone = "0912 345 678", string? email = "ncc@example.com", string? addr = "12 Nguyễn Huệ") =>
            new($"{Tag}P{suffix}", $"Công ty {Tag} {suffix}", type, addr, phone, email);
        public LabWorkTypeInput Lab(string suffix, Guid? providerId = null, string unit = "Tooth", long? price = 500_000, string? warranty = "2 năm") =>
            new($"{Tag}L{suffix}", $"Răng sứ {Tag} {suffix}", providerId, unit, price, warranty);
        public MedicineInput Med(string suffix, bool rx = true, bool stock = true, long? price = 15_000) =>
            new($"{Tag}M{suffix}", $"Thuốc {Tag} {suffix}", "Amoxicillin", "500mg", "Viên nang", "Viên", "Hộp 10 vỉ", "Uống sau ăn", price, rx, stock);
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId();
        var staff = await CreateTestKit.SeedStaffAsync(db, "sup" + n, AccountRoles.ClinicOwner, n);
        return new Env { Pg = pg, N = n, Actor = staff.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code, string? expectedMessage = null)
    {
        var err = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (err.Status, err.Response.Code));
        Assert.False(string.IsNullOrWhiteSpace(err.Response.Message));
        if (expectedMessage != null) Assert.Equal(expectedMessage, err.Response.Message);
    }

    // ================= ProviderCatalog =================
    public static TheoryData<string, string?, string?, string?, string?, string?, string?, string> BadProviders => new()
    {
        { "UT-SUP-01 Thiếu mã", "", "Tên hợp lệ", ProviderTypes.Supplier, null, null, null, "MSG02" },
        { "UT-SUP-02 Mã vượt 40 ký tự", new string('A', 41), "Tên hợp lệ", ProviderTypes.Supplier, null, null, null, "MSG03" },
        { "UT-SUP-03 Mã sai định dạng", "-BAD CODE!", "Tên hợp lệ", ProviderTypes.Supplier, null, null, null, "MSG04" },
        { "UT-SUP-04 Thiếu tên", "VALID1", "   ", ProviderTypes.Supplier, null, null, null, "MSG02" },
        { "UT-SUP-05 Tên vượt 160 ký tự", "VALID2", new string('T', 161), ProviderTypes.Supplier, null, null, null, "MSG03" },
        { "UT-SUP-06 Loại công ty không hợp lệ", "VALID3", "Tên hợp lệ", "InvalidType", null, null, null, "MSG04" },
        { "UT-SUP-07 Địa chỉ vượt 1000 ký tự", "VALID4", "Tên hợp lệ", ProviderTypes.Lab, new string('D', 1001), null, null, "MSG03" },
        { "UT-SUP-08 SĐT quá ít chữ số", "VALID5", "Tên hợp lệ", ProviderTypes.Lab, null, "12345", null, "MSG04" },
        { "UT-SUP-09 SĐT chứa chữ cái", "VALID6", "Tên hợp lệ", ProviderTypes.Lab, null, "0912345abc", null, "MSG04" },
        { "UT-SUP-10 Email sai định dạng", "VALID7", "Tên hợp lệ", ProviderTypes.Lab, null, null, "not-an-email", "MSG04" },
    };

    [Theory, MemberData(nameof(BadProviders))]
    public async Task UT_SUP_Provider_Create_validation(string caseName, string? code, string? name, string? type, string? addr, string? phone, string? email, string expectedCode)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var input = new ProviderInput(code is "VALID1" or "VALID2" or "VALID3" or "VALID4" or "VALID5" or "VALID6" or "VALID7" ? env.Tag + code : code,
            name, type, addr, phone, email);
        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), input), CancellationToken.None), 400, expectedCode);
    }

    [Fact]
    public async Task UT_SUP_11_Provider_Create_null_entry_va_requestKey_rong_va_khong_co_quyen()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Providers(deny: true).CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("1")), CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(Guid.Empty, env.Prov("1")), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), null), CancellationToken.None), 400, "MSG04");
    }

    [Fact]
    public async Task UT_SUP_12_Provider_Create_Get_replay_xung_dot_va_trung_ma_ten()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var req = new CreateSupplyRequest<ProviderInput>(key, env.Prov("A", ProviderTypes.Supplier));
        var created = await env.Providers().CreateAsync(env.Actor, req, CancellationToken.None);
        Assert.Equal((env.Tag + "PA", ProviderTypes.Supplier, true, 1), (created.Code, created.Type, created.Active, created.Version));

        var replayed = await env.Providers().CreateAsync(env.Actor, req, CancellationToken.None);
        Assert.Equal(created.Id, replayed.Id);

        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(key, env.Prov("B")), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("A") with { Name = "Tên khác " + env.Tag }), CancellationToken.None),
            409, ReferenceMessages.DuplicateCode.Code, ReferenceMessages.DuplicateCode.Message);
        await Rejects(() => env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("A") with { Code = env.Tag + "PNEW" }), CancellationToken.None),
            409, ReferenceMessages.DuplicateName.Code, ReferenceMessages.DuplicateName.Message);

        var fetched = await env.Providers().GetAsync(env.Actor, created.Id, CancellationToken.None);
        Assert.Equal(created.Code, fetched.Code);
        await Rejects(() => env.Providers().GetAsync(env.Actor, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
    }

    [Fact]
    public async Task UT_SUP_13_Provider_List_loc_theo_loai_trang_thai_tu_khoa_va_loi_phan_trang()
    {
        await using var env = await NewEnv();
        var sup = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("S", ProviderTypes.Supplier)), CancellationToken.None);
        var lab = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("L", ProviderTypes.Lab)), CancellationToken.None);
        var both = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("B", ProviderTypes.Both)), CancellationToken.None);
        await env.Providers().SetStateAsync(env.Actor, lab.Id, new(1, Guid.NewGuid(), false, "Ngừng hợp tác"), CancellationToken.None);

        var suppliers = await env.Providers().ListAsync(env.Actor, env.Tag, "Active", ProviderTypes.Supplier, 1, 20, CancellationToken.None);
        Assert.Equal(new[] { both.Id, sup.Id }.Order(), suppliers.Items.Select(x => x.Id).Order());

        var labsAll = await env.Providers().ListAsync(env.Actor, env.Tag, "All", ProviderTypes.Lab, 1, 20, CancellationToken.None);
        Assert.Equal(new[] { both.Id, lab.Id }.Order(), labsAll.Items.Select(x => x.Id).Order());

        var inactive = await env.Providers().ListAsync(env.Actor, env.Tag, "Inactive", null, 1, 20, CancellationToken.None);
        Assert.Equal([lab.Id], inactive.Items.Select(x => x.Id));

        await Rejects(() => env.Providers().ListAsync(env.Actor, new string('q', 101), "Active", null, 1, 20, CancellationToken.None), 400, "MSG03");
        await Rejects(() => env.Providers().ListAsync(env.Actor, null, "Active", null, 0, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Providers().ListAsync(env.Actor, null, "Active", null, 1, 15, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Providers().ListAsync(env.Actor, null, "BadStatus", null, 1, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Providers().ListAsync(env.Actor, null, "Active", "BadType", 1, 20, CancellationToken.None), 400, "MSG04");
    }

    [Fact]
    public async Task UT_SUP_14_Provider_Update_StatePreview_SetState_History_va_chan_bo_vai_tro_Lab()
    {
        await using var env = await NewEnv();
        var p = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("U", ProviderTypes.Both)), CancellationToken.None);
        var labWork = await env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("1", p.Id)), CancellationToken.None);
        Assert.Equal((p.Id, p.Name, true), (labWork.ProviderId, labWork.ProviderName, labWork.ProviderActive));

        // Update guards: missing reason, long reason, stale version, no change, drop Lab role while referenced.
        await Rejects(() => env.Providers().UpdateAsync(env.Actor, p.Id, new(1, Guid.NewGuid(), " ", env.Prov("U", ProviderTypes.Both)), CancellationToken.None), 400, "MSG02");
        await Rejects(() => env.Providers().UpdateAsync(env.Actor, p.Id, new(1, Guid.NewGuid(), new string('r', 501), env.Prov("U", ProviderTypes.Both)), CancellationToken.None), 400, "MSG03");
        await Rejects(() => env.Providers().UpdateAsync(env.Actor, p.Id, new(9, Guid.NewGuid(), "Sửa", env.Prov("U", ProviderTypes.Lab)), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Providers().UpdateAsync(env.Actor, p.Id, new(1, Guid.NewGuid(), "Sửa", env.Prov("U", ProviderTypes.Both)), CancellationToken.None),
            400, GeneralSettingsMessages.NoChange.Code, GeneralSettingsMessages.NoChange.Message);
        await Rejects(() => env.Providers().UpdateAsync(env.Actor, p.Id, new(1, Guid.NewGuid(), "Đổi loại", env.Prov("U", ProviderTypes.Supplier)), CancellationToken.None), 409, "MSG205");
        env.Db.ChangeTracker.Clear();

        var updated = await env.Providers().UpdateAsync(env.Actor, p.Id, new(1, Guid.NewGuid(), "Cập nhật địa chỉ", env.Prov("U", ProviderTypes.Both, addr: "99 Hai Bà Trưng")), CancellationToken.None);
        Assert.Equal(("99 Hai Bà Trưng", 2), (updated.Address, updated.Version));

        var previewOff = await env.Providers().StatePreviewAsync(env.Actor, p.Id, new(false), CancellationToken.None);
        Assert.True(previewOff.Allowed);
        Assert.Contains("1 loại công việc labo", previewOff.Impact);

        var previewOn = await env.Providers().StatePreviewAsync(env.Actor, p.Id, new(true), CancellationToken.None);
        Assert.False(previewOn.Allowed);

        await Rejects(() => env.Providers().SetStateAsync(env.Actor, p.Id, new(2, Guid.NewGuid(), true, "Đã bật"), CancellationToken.None), 409, "MSG111");
        var off = await env.Providers().SetStateAsync(env.Actor, p.Id, new(2, Guid.NewGuid(), false, "Tạm ngừng"), CancellationToken.None);
        Assert.Equal((false, 3), (off.Active, off.Version));

        var history = await env.Providers().HistoryAsync(env.Actor, p.Id, CancellationToken.None);
        Assert.Equal(3, history.Count);
        Assert.Contains(history, x => x.Command == "state" && x.Reason == "Tạm ngừng" && x.Before!.Active && !x.After.Active);
    }

    // ================= LabWorkTypeCatalog =================
    [Fact]
    public async Task UT_SUP_15_LabWorkType_Create_Update_giu_provider_cu_ngung_dung_va_tu_choi_du_lieu_sai()
    {
        await using var env = await NewEnv();
        var supplierOnly = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("SO", ProviderTypes.Supplier)), CancellationToken.None);
        var labProv = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("LP", ProviderTypes.Lab)), CancellationToken.None);
        var inactiveLab = await env.Providers().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Prov("IL", ProviderTypes.Lab)), CancellationToken.None);
        await env.Providers().SetStateAsync(env.Actor, inactiveLab.Id, new(1, Guid.NewGuid(), false, "Ngừng"), CancellationToken.None);

        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), null), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U1", null, unit: "Invalid")), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U2", null, price: -1)), CancellationToken.None), 400, "MSG257");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U3", null, price: SupplyCatalogSupport.MaxPrice + 1)), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U4", Guid.NewGuid())), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U5", supplierOnly.Id)), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("U6", inactiveLab.Id)), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.LabTypes().ListAsync(env.Actor, null, "Active", "any", 1, 20, CancellationToken.None), 400, "MSG04");

        var item = await env.LabTypes().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Lab("OK", labProv.Id)), CancellationToken.None);
        // Deactivate the provider; updating other fields while keeping the same providerId must still succeed.
        await env.Providers().SetStateAsync(env.Actor, labProv.Id, new(1, Guid.NewGuid(), false, "Ngừng"), CancellationToken.None);
        var updated = await env.LabTypes().UpdateAsync(env.Actor, item.Id, new(1, Guid.NewGuid(), "Đổi giá", env.Lab("OK", labProv.Id, price: 650_000)), CancellationToken.None);
        Assert.Equal((650_000L, false, 2), (updated.ReferencePrice, updated.ProviderActive, updated.Version));
        // Switching to a different inactive provider is rejected.
        await Rejects(() => env.LabTypes().UpdateAsync(env.Actor, item.Id, new(2, Guid.NewGuid(), "Đổi lab", env.Lab("OK", inactiveLab.Id, price: 650_000)), CancellationToken.None), 400, "MSG04");
        // NoChange rejected.
        env.Db.ChangeTracker.Clear();
        await Rejects(() => env.LabTypes().UpdateAsync(env.Actor, item.Id, new(2, Guid.NewGuid(), "Không đổi", env.Lab("OK", labProv.Id, price: 650_000)), CancellationToken.None),
            400, GeneralSettingsMessages.NoChange.Code);
        var preview = await env.LabTypes().StatePreviewAsync(env.Actor, item.Id, new(false), CancellationToken.None);
        Assert.True(preview.Allowed);
    }

    // ================= MedicineCatalog =================
    [Fact]
    public async Task UT_SUP_16_Medicine_Create_Get_List_Update_SetState_History_va_Export()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), null), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("B1") with { BaseUnit = " " }), CancellationToken.None), 400, "MSG02");
        await Rejects(() => env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("B2", price: -10)), CancellationToken.None), 400, "MSG257");

        var clinicMed = await env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("C1", rx: true, stock: true)), CancellationToken.None);
        var extMed = await env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("E1", rx: true, stock: false)), CancellationToken.None);

        await Rejects(() => env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("C1") with { Name = "Khác " + env.Tag }), CancellationToken.None),
            409, ReferenceMessages.DuplicateCode.Code, ReferenceMessages.DuplicateCode.Message);
        await Rejects(() => env.Medicines().CreateAsync(env.Actor, new(Guid.NewGuid(), env.Med("C1") with { Code = env.Tag + "MC2" }), CancellationToken.None),
            409, ReferenceMessages.DuplicateName.Code, ReferenceMessages.DuplicateName.Message);

        Assert.Equal(clinicMed.Id, (await env.Medicines().GetAsync(env.Actor, clinicMed.Id, CancellationToken.None)).Id);
        await Rejects(() => env.Medicines().GetAsync(env.Actor, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");

        var clinicList = await env.Medicines().ListAsync(env.Actor, env.Tag, "Active", "Clinic", 1, 20, CancellationToken.None);
        Assert.Equal([clinicMed.Id], clinicList.Items.Select(x => x.Id));
        var extList = await env.Medicines().ListAsync(env.Actor, env.Tag, "All", "External", 1, 20, CancellationToken.None);
        Assert.Equal([extMed.Id], extList.Items.Select(x => x.Id));
        await Rejects(() => env.Medicines().ListAsync(env.Actor, null, "Active", "Bad", 1, 20, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Medicines().ListAsync(env.Actor, null, "Bad", null, 1, 20, CancellationToken.None), 400, "MSG04");

        await Rejects(() => env.Medicines().UpdateAsync(env.Actor, clinicMed.Id, new(9, Guid.NewGuid(), "Sửa", env.Med("C1", price: 20_000)), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Medicines().UpdateAsync(env.Actor, clinicMed.Id, new(1, Guid.NewGuid(), "Sửa", env.Med("C1")), CancellationToken.None),
            400, GeneralSettingsMessages.NoChange.Code, GeneralSettingsMessages.NoChange.Message);
        env.Db.ChangeTracker.Clear();

        var updated = await env.Medicines().UpdateAsync(env.Actor, clinicMed.Id, new(1, Guid.NewGuid(), "Cập nhật giá", env.Med("C1", price: 22_000)), CancellationToken.None);
        Assert.Equal((22_000L, 2), (updated.ReferencePrice, updated.Version));

        var preview = await env.Medicines().StatePreviewAsync(env.Actor, clinicMed.Id, new(false), CancellationToken.None);
        Assert.True(preview.Allowed);
        await Rejects(() => env.Medicines().SetStateAsync(env.Actor, clinicMed.Id, new(2, Guid.NewGuid(), true, "Đã bật"), CancellationToken.None), 409, "MSG111");
        var off = await env.Medicines().SetStateAsync(env.Actor, clinicMed.Id, new(2, Guid.NewGuid(), false, "Ngừng bán"), CancellationToken.None);
        Assert.Equal((false, 3), (off.Active, off.Version));

        var inactiveList = await env.Medicines().ListAsync(env.Actor, env.Tag, "Inactive", null, 1, 20, CancellationToken.None);
        Assert.Equal([clinicMed.Id], inactiveList.Items.Select(x => x.Id));

        var history = await env.Medicines().HistoryAsync(env.Actor, clinicMed.Id, CancellationToken.None);
        Assert.Equal(3, history.Count);

        var bytes = await env.Medicines().ExportAsync(env.Actor, env.Tag, "All", CancellationToken.None);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheetXml = await reader.ReadToEndAsync();
        Assert.Contains(clinicMed.Code, sheetXml);
        Assert.Contains(extMed.Code, sheetXml);
    }
}
