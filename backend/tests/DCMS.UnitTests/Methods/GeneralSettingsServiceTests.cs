using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using GeneralSettings = DCMS.Application.Features.SystemSettings.GeneralSettings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of GeneralSettingsService (thiết lập chung): CapabilitiesAsync, GetAsync, PreviewAsync, SaveAsync,
/// RestorePreviewAsync, RestoreAsync, HistoryAsync, SequenceAsync, SequencePreviewAsync, UpdateSequenceAsync and their helpers.
/// The settings versions are one global timeline, so each test starts from an empty timeline (the Postgres collection runs tests one by one).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GeneralSettingsServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);

    private sealed class Access(bool owner, string[] permissions, params string[] denied) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, false, [], permissions, []));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Owner;
        public DcmsDbContext Db => Pg.Db;
        public async ValueTask DisposeAsync()
        {
            await Db.ClinicSettingsVersions.ExecuteDeleteAsync();
            await Pg.DisposeAsync();
        }
        public GeneralSettingsService Svc(bool owner = true, string[]? perms = null, params string[] denied) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new Access(owner, perms ?? [], denied));

        public static GeneralSettings Settings(Func<GeneralSettings, GeneralSettings>? change = null) => change == null ? GeneralSettings.Default : change(GeneralSettings.Default);
        public Task<GeneralSettingsDto> Save(Func<GeneralSettings, GeneralSettings>? change = null, int version = 0, string? reason = "Điều chỉnh", Guid? key = null, int? actor = null) =>
            Svc().SaveAsync(actor ?? Owner, new(version, key ?? Guid.NewGuid(), reason, Settings(change ?? (s => s with { StaffIdleTimeoutMinutes = 30 }))), CancellationToken.None);
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var group = db.Set<DCMS.Domain.Features.Settings.AccessGroup>();
        foreach (var role in AccountRoles.Staff)
            if (!await group.IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                group.Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 100 + Array.IndexOf(AccountRoles.Staff, role), Code = role, Name = role, BaseRole = role });
        await db.SaveChangesAsync();
        await db.ClinicSettingsVersions.ExecuteDeleteAsync();
        var n = CreateTestKit.NextId();
        var owner = await CreateTestKit.SeedStaffAsync(db, "ow" + n, AccountRoles.ClinicOwner, n);
        return new Env { Pg = pg, N = n, Owner = owner.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= CapabilitiesAsync =================
    public static TheoryData<string, bool, string[], string> Capabilities => new()
    {
        { "UT-SET-01 Không có quyền nào", false, [], "" },
        { "UT-SET-02 Chỉ xem thiết lập chung", false, ["settings.general.read"], "general" },
        { "UT-SET-03 Xem và quản lý thiết lập chung", false, ["settings.general.read", "settings.general.manage"], "general:manage" },
        { "UT-SET-04 Xem một danh mục", false, ["catalog.diagnoses.read"], "catalogs:manage" },
        { "UT-SET-05 Xem vật tư (không sửa)", false, ["catalog.materials.read"], "materials" },
        { "UT-SET-06 Sửa vật tư", false, ["catalog.materials.read", "catalog.materials.update"], "materials:manage" },
        { "UT-SET-07 Xem nhà cung cấp/thuốc", false, ["catalog.medicines.read"], "supplies:manage" },
        { "UT-SET-08 Xem nhật ký thiết lập", false, ["settings.audit.read"], "audit" },
        { "UT-SET-09 Chủ phòng khám", true, [], "catalogs:manage,audit" },
    };

    [Theory, MemberData(nameof(Capabilities))]
    public async Task UT_SET_Capabilities(string caseName, bool owner, string[] permissions, string expected)
    {
        await using var env = await NewEnv();
        var caps = await env.Svc(owner, permissions).CapabilitiesAsync(env.Owner, CancellationToken.None);
        var actual = string.Join(',', caps.Sections.Select(x => x.Key + (x.CanManage ? ":manage" : "")));
        Assert.Equal(expected, actual);
        _ = caseName;
        if (owner) Assert.Equal(ReferenceKinds.All, caps.CatalogKinds);
        else if (permissions.Contains("catalog.diagnoses.read")) Assert.Equal([ReferenceKinds.Diagnoses], caps.CatalogKinds);
        else Assert.Empty(caps.CatalogKinds!);
    }

    // ================= GetAsync =================
    [Fact]
    public async Task UT_SET_10_GetAsync_chua_luu_lan_nao_tra_ve_mac_dinh_phien_ban_0()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().GetAsync(env.Owner, CancellationToken.None);
        Assert.Equal(0, dto.Version);
        Assert.Null(dto.EffectiveAt);
        Assert.Null(dto.ActorName);
        Assert.Equal(GeneralSettings.Default, dto.Settings);
    }

    [Fact]
    public async Task UT_SET_11_GetAsync_sau_khi_luu_tra_ve_ban_moi_nhat_va_nguoi_luu()
    {
        await using var env = await NewEnv();
        await env.Save();
        var dto = await env.Svc().GetAsync(env.Owner, CancellationToken.None);
        Assert.Equal((1, "ow" + env.N, "Điều chỉnh", 30), (dto.Version, dto.ActorName, dto.Reason, dto.Settings.StaffIdleTimeoutMinutes));
        Assert.Equal(Now, dto.EffectiveAt);
    }

    [Fact]
    public async Task UT_SET_12_GetAsync_khong_co_quyen_doc_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc(denied: "settings.general.read").GetAsync(env.Owner, CancellationToken.None), 403, "MSG06");
    }

    // ================= SaveAsync =================
    [Fact]
    public async Task UT_SET_13_SaveAsync_ghi_phien_ban_moi_va_chuan_hoa_tien_to_ma()
    {
        await using var env = await NewEnv();
        var dto = await env.Save(s => s with { PatientCode = new(PatientCodeModes.AutoOnly, " kh-1 ") });
        Assert.Equal((1, "KH-1", PatientCodeModes.AutoOnly), (dto.Version, dto.Settings.PatientCode.Prefix, dto.Settings.PatientCode.Mode));
        var second = await env.Save(s => s with { StaffIdleTimeoutMinutes = 60 }, version: 1);
        Assert.Equal(2, second.Version);
    }

    public static TheoryData<string, string, int, string> BadSaves => new()
    {
        { "UT-SET-14 Mã yêu cầu rỗng", "noKey", 400, "MSG04" },
        { "UT-SET-15 Thiếu lý do", "noReason", 400, "MSG02" },
        { "UT-SET-16 Lý do 501 ký tự", "reason501", 400, "MSG03" },
        { "UT-SET-17 Phiên bản cũ", "stale", 409, "MSG111" },
        { "UT-SET-18 Không có thay đổi", "same", 400, "SETTINGS_NO_CHANGE" },
        { "UT-SET-19 Thiết lập null", "nullSettings", 400, "MSG04" },
        { "UT-SET-20 Thời gian tự đăng xuất 7 phút không nằm trong lựa chọn", "idle7", 400, "MSG04" },
        { "UT-SET-21 Chế độ mã lạ", "badMode", 400, "MSG04" },
        { "UT-SET-22 Tiền tố có ký tự thường/đặc biệt", "badPrefix", 400, "MSG04" },
        { "UT-SET-23 Tiền tố 13 ký tự", "prefix13", 400, "MSG04" },
        { "UT-SET-24 Thiếu mẫu in phiếu thu", "noReceipt", 400, "MSG04" },
        { "UT-SET-25 Thiếu trường bắt buộc", "noRequired", 400, "MSG04" },
    };

    [Theory, MemberData(nameof(BadSaves))]
    public async Task UT_SET_Save_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var good = GeneralSettings.Default with { StaffIdleTimeoutMinutes = 30 };
        var request = new SaveGeneralRequest(0, Guid.NewGuid(), "Lý do", good);
        request = kind switch
        {
            "noKey" => request with { RequestKey = Guid.Empty },
            "noReason" => request with { Reason = "  " },
            "reason501" => request with { Reason = new string('r', 501) },
            "stale" => request with { ExpectedVersion = 3 },
            "same" => request with { Settings = GeneralSettings.Default },
            "nullSettings" => request with { Settings = null },
            "idle7" => request with { Settings = good with { StaffIdleTimeoutMinutes = 7 } },
            "badMode" => request with { Settings = good with { PatientCode = new("Random", "BN") } },
            "badPrefix" => request with { Settings = good with { PatientCode = new(PatientCodeModes.AutoOnly, "a_b") } },
            "prefix13" => request with { Settings = good with { PatientCode = new(PatientCodeModes.AutoOnly, new string('A', 13)) } },
            "noReceipt" => request with { Settings = good with { ReceiptPrint = null! } },
            "noRequired" => request with { Settings = good with { RequiredFields = null! } },
            _ => request
        };
        await Rejects(() => env.Svc().SaveAsync(env.Owner, request, CancellationToken.None), status, code);
    }

    [Fact]
    public async Task UT_SET_26_SaveAsync_khong_co_quyen_quan_ly_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc(denied: "settings.general.manage").SaveAsync(env.Owner, new(0, Guid.NewGuid(), "x", GeneralSettings.Default), CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_SET_27_SaveAsync_gui_lai_cung_khoa_tra_ve_ket_qua_cu_khac_noi_dung_la_409()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Save(key: key);
        var again = await env.Save(key: key);
        Assert.Equal((first.Version, first.EffectiveAt), (again.Version, again.EffectiveAt));
        Assert.Equal(1, await env.Db.ClinicSettingsVersions.CountAsync());
        await Rejects(() => env.Save(s => s with { StaffIdleTimeoutMinutes = 90 }, key: key), 409, "MSG111");
    }

    // ================= PreviewAsync =================
    [Fact]
    public async Task UT_SET_28_PreviewAsync_liet_ke_thay_doi_va_canh_bao()
    {
        await using var env = await NewEnv();
        var after = GeneralSettings.Default with
        {
            StaffIdleTimeoutMinutes = 30,
            PatientCode = new(PatientCodeModes.ManualOnly, "KH"),
            ReceiptPrint = GeneralSettings.Default.ReceiptPrint with { Discount = false }
        };
        var preview = await env.Svc().PreviewAsync(env.Owner, new(0, after), CancellationToken.None);
        Assert.False(preview.Stale);
        Assert.Equal(["staffIdleTimeoutMinutes", "patientCode.mode", "patientCode.prefix", "receiptPrint.discount"], preview.Changes.Select(x => x.Field));
        var idle = preview.Changes[0];
        Assert.Equal(("20", "30"), (idle.Before, idle.After));
        Assert.Equal(("Tự động, cho nhập tay", "Chỉ nhập tay"), (preview.Changes[1].Before, preview.Changes[1].After));
        Assert.Equal(("Bật", "Tắt"), (preview.Changes[3].Before, preview.Changes[3].After));
        Assert.Equal(3, preview.Warnings.Count);
        Assert.Empty(preview.MissingPatients);
    }

    [Fact]
    public async Task UT_SET_29_PreviewAsync_bat_truong_bat_buoc_moi_dem_ho_so_thieu_va_phien_ban_cu_la_Stale()
    {
        await using var env = await NewEnv();
        env.Db.Patients.Add(new PatientProfile { PatientCode = "SX" + env.N, FullName = "Thiếu thông tin", NormalizedName = "THIEU THONG TIN", BirthDate = new DateOnly(1990, 1, 1), Gender = PatientGenders.Male, CreatedAt = Now, UpdatedAt = Now });
        await env.Db.SaveChangesAsync();
        // ProfileDate is left off: counting patients without it cannot be translated to SQL today (reported separately).
        var required = new RequiredFieldsSettings(false, true, true, true, true, true, true);
        var preview = await env.Svc().PreviewAsync(env.Owner, new(4, GeneralSettings.Default with { RequiredFields = required }), CancellationToken.None);
        Assert.True(preview.Stale);
        Assert.Equal(6, preview.Changes.Count);
        Assert.Contains(preview.Warnings, w => w.StartsWith("Trường bắt buộc mới"));
        var address = preview.MissingPatients.Single(x => x.Field == "address");
        Assert.True(address.Count >= 1);
        Assert.Equal("Địa chỉ", address.Label);
        Assert.Equal(6, preview.MissingPatients.Count);
    }

    [Fact]
    public async Task UT_SET_30_PreviewAsync_thiet_lap_khong_hop_le_hoac_khong_co_quyen()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().PreviewAsync(env.Owner, new(0, null), CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc(denied: "settings.general.manage").PreviewAsync(env.Owner, new(0, GeneralSettings.Default), CancellationToken.None), 403, "MSG06");
    }

    // ================= RestorePreviewAsync / RestoreAsync =================
    [Fact]
    public async Task UT_SET_31_RestoreAsync_khoi_phuc_phien_ban_cu_thanh_phien_ban_moi()
    {
        await using var env = await NewEnv();
        await env.Save(s => s with { StaffIdleTimeoutMinutes = 30 });
        await env.Save(s => s with { StaffIdleTimeoutMinutes = 60 }, version: 1);
        var preview = await env.Svc().RestorePreviewAsync(env.Owner, new(1, 2), CancellationToken.None);
        Assert.Equal(("30", "60"), ((string?)preview.Changes.Single().After, preview.Changes.Single().Before));
        var restored = await env.Svc().RestoreAsync(env.Owner, new(1, 2, Guid.NewGuid(), "Quay lại"), CancellationToken.None);
        Assert.Equal((3, 30), (restored.Version, restored.Settings.StaffIdleTimeoutMinutes));
        var history = await env.Svc().HistoryAsync(env.Owner, 1, 20, CancellationToken.None);
        Assert.Equal(1, history.Items[0].RestoredFromVersion);
    }

    [Fact]
    public async Task UT_SET_32_RestoreAsync_phien_ban_khong_ton_tai_la_404_va_khoi_phuc_ban_dang_dung_la_khong_thay_doi()
    {
        await using var env = await NewEnv();
        await env.Save();
        await Rejects(() => env.Svc().RestorePreviewAsync(env.Owner, new(99, 1), CancellationToken.None), 404, "MSG113");
        await Rejects(() => env.Svc().RestoreAsync(env.Owner, new(99, 1, Guid.NewGuid(), "x"), CancellationToken.None), 404, "MSG113");
        await Rejects(() => env.Svc().RestoreAsync(env.Owner, new(1, 1, Guid.NewGuid(), "x"), CancellationToken.None), 400, "SETTINGS_NO_CHANGE");
        await Rejects(() => env.Svc().RestoreAsync(env.Owner, new(1, 5, Guid.NewGuid(), "x"), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Svc(denied: "settings.general.manage").RestorePreviewAsync(env.Owner, new(1, 1), CancellationToken.None), 403, "MSG06");
    }

    // ================= HistoryAsync =================
    [Fact]
    public async Task UT_SET_33_HistoryAsync_moi_nhat_truoc_va_phan_trang()
    {
        await using var env = await NewEnv();
        await env.Save(s => s with { StaffIdleTimeoutMinutes = 30 });
        await env.Save(s => s with { StaffIdleTimeoutMinutes = 60 }, version: 1);
        var page = await env.Svc().HistoryAsync(env.Owner, 1, 20, CancellationToken.None);
        Assert.Equal([2, 1], page.Items.Select(x => x.Version));
        Assert.Equal((2, 1, 20), (page.Total, page.Page, page.PageSize));
        Assert.Equal("ow" + env.N, page.Items[0].ActorName);
        var second = await env.Svc().HistoryAsync(env.Owner, 2, 20, CancellationToken.None);
        Assert.Empty(second.Items);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 10)]
    public async Task UT_SET_History_validation(int page, int size)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().HistoryAsync(env.Owner, page, size, CancellationToken.None), 400, "MSG04");
    }

    // ================= Patient code sequence =================
    [Fact]
    public async Task UT_SET_34_SequenceAsync_doc_so_tiep_theo_va_ma_mau()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().SequenceAsync(env.Owner, CancellationToken.None);
        Assert.Equal("BN", dto.Prefix);
        Assert.True(dto.NextNumber >= 1);
        Assert.Equal($"BN{dto.NextNumber:000000}", dto.NextCode);
        await Rejects(() => env.Svc(denied: "settings.general.read").SequenceAsync(env.Owner, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_SET_35_SequencePreviewAsync_so_hop_le_khong_hop_le_va_da_cu()
    {
        await using var env = await NewEnv();
        var current = (await env.Svc().SequenceAsync(env.Owner, CancellationToken.None)).NextNumber;
        var same = await env.Svc().SequencePreviewAsync(env.Owner, new(current, current), CancellationToken.None);
        Assert.Equal((false, false), (same.Stale, same.Allowed));
        var forward = await env.Svc().SequencePreviewAsync(env.Owner, new(current, current + 10), CancellationToken.None);
        Assert.Equal((false, true, $"BN{current + 10:000000}"), (forward.Stale, forward.Allowed, forward.NextCode));
        var backwards = await env.Svc().SequencePreviewAsync(env.Owner, new(current, 0), CancellationToken.None);
        Assert.False(backwards.Allowed);
        Assert.Contains("từ 1 đến", Assert.Single(backwards.Issues));
        var stale = await env.Svc().SequencePreviewAsync(env.Owner, new(current + 99, current + 5), CancellationToken.None);
        Assert.True(stale.Stale);
        if (current > 1)
        {
            var decrease = await env.Svc().SequencePreviewAsync(env.Owner, new(current, current - 1), CancellationToken.None);
            Assert.Contains("chỉ được tăng", Assert.Single(decrease.Issues));
        }
        var tooBig = await env.Svc().SequencePreviewAsync(env.Owner, new(current, 1_000_000_000_000_000), CancellationToken.None);
        Assert.False(tooBig.Allowed);
    }

    public static TheoryData<string, string, int, string> BadSequences => new()
    {
        { "UT-SET-36 Mã yêu cầu rỗng", "noKey", 400, "MSG04" },
        { "UT-SET-37 Thiếu lý do", "noReason", 400, "MSG02" },
        { "UT-SET-38 Lý do 501 ký tự", "reason501", 400, "MSG03" },
        { "UT-SET-39 Số kỳ vọng sai", "stale", 409, "MSG111" },
        { "UT-SET-40 Số tiếp theo không đổi", "same", 400, "SETTINGS_NO_CHANGE" },
        { "UT-SET-41 Số tiếp theo bằng 0", "zero", 400, "MSG04" },
    };

    [Theory, MemberData(nameof(BadSequences))]
    public async Task UT_SET_Sequence_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var current = (await env.Svc().SequenceAsync(env.Owner, CancellationToken.None)).NextNumber;
        var request = new UpdatePatientCodeSequenceRequest(current, Guid.NewGuid(), "Lý do", current + 1);
        request = kind switch
        {
            "noKey" => request with { RequestKey = Guid.Empty },
            "noReason" => request with { Reason = " " },
            "reason501" => request with { Reason = new string('r', 501) },
            "stale" => request with { ExpectedNextNumber = current + 50 },
            "same" => request with { NextNumber = current },
            _ => request with { NextNumber = 0 }
        };
        await Rejects(() => env.Svc().UpdateSequenceAsync(env.Owner, request, CancellationToken.None), status, code);
    }

    [Fact]
    public async Task UT_SET_42_UpdateSequenceAsync_chi_tang_va_gui_lai_cung_khoa_tra_ve_ket_qua_cu()
    {
        await using var env = await NewEnv();
        var current = (await env.Svc().SequenceAsync(env.Owner, CancellationToken.None)).NextNumber;
        var request = new UpdatePatientCodeSequenceRequest(current, Guid.NewGuid(), "Bỏ qua số đã dùng", current + 3);
        var result = await env.Svc().UpdateSequenceAsync(env.Owner, request, CancellationToken.None);
        Assert.Equal((current + 3, $"BN{current + 3:000000}"), (result.NextNumber, result.NextCode));
        var again = await env.Svc().UpdateSequenceAsync(env.Owner, request, CancellationToken.None);
        Assert.Equal(result.NextNumber, again.NextNumber);
        Assert.Equal(current + 3, (await env.Svc().SequenceAsync(env.Owner, CancellationToken.None)).NextNumber);
        await Rejects(() => env.Svc().UpdateSequenceAsync(env.Owner, request with { Reason = "Khác" }, CancellationToken.None), 409, "MSG111");
        var backwards = new UpdatePatientCodeSequenceRequest(current + 3, Guid.NewGuid(), "Lùi", current);
        await Rejects(() => env.Svc().UpdateSequenceAsync(env.Owner, backwards, CancellationToken.None), 400, "MSG04");
    }
}
