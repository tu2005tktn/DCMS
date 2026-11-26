using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.SystemSettings;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of ReferenceCatalogService (danh mục tham chiếu): ListAsync, GetAsync, CreateAsync, UpdateAsync,
/// StatePreviewAsync, SetStateAsync, HistoryAsync, OptionsAsync and helpers (Validate, Reason, uniqueness, idempotent Command).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReferenceCatalogServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);

    private sealed class Access(params string[] denied) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(false, false, [], [], []));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) =>
            denied.Contains(permission) ? throw new BusinessException(403, SystemMessages.Get("MSG06")) : ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Owner, Dentist, Assistant, Customer, Inactive;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public ReferenceCatalogService Svc(params string[] denied) => new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new Access(denied));
        public string Code(string tag = "") => $"T{N}{tag}";
        public Task<ReferenceEntryDto> Create(string kind = ReferenceKinds.CareTypes, string? code = null, string? name = null, string? desc = null, Guid? key = null, int? actor = null, params string[] denied) =>
            Svc(denied).CreateAsync(actor ?? Owner, kind, new(key ?? Guid.NewGuid(), new(code ?? Code(), name ?? "Tên " + N, desc)), CancellationToken.None);
        public Task<ReferenceEntryDto> Update(Guid id, ReferenceEntryDto current, string? name = null, string? code = null, string? desc = null, string? reason = "Sửa", int? version = null, Guid? key = null, string? kind = null) =>
            Svc().UpdateAsync(Owner, kind ?? current.Kind, id, new(version ?? current.Version, key ?? Guid.NewGuid(), reason, new(code ?? current.Code, name ?? current.Name, desc ?? current.Description)), CancellationToken.None);
        public Task<ReferenceEntryDto> SetState(ReferenceEntryDto e, bool active, string? reason = "Lý do", int? version = null, Guid? key = null) =>
            Svc().SetStateAsync(Owner, e.Kind, e.Id, new(version ?? e.Version, key ?? Guid.NewGuid(), active, reason), CancellationToken.None);
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
        var n = CreateTestKit.NextId();
        var owner = await CreateTestKit.SeedStaffAsync(db, "ow" + n, AccountRoles.ClinicOwner, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var admin = await CreateTestKit.SeedStaffAsync(db, "ad" + n, AccountRoles.Admin, n);
        var inactive = await CreateTestKit.SeedStaffAsync(db, "in" + n, AccountRoles.Dentist, n);
        inactive.IsActive = false;
        var customer = new DCMS.Infrastructure.Features.Accounts.ApplicationUser { UserName = "kh" + n, FullName = "Khách " + n, AccountType = "Customer", IsActive = true, AssignedRoles = [AccountRoles.Patient] };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Owner = owner.Id, Dentist = dentist.Id, Assistant = assistant.Id, Customer = customer.Id, Inactive = inactive.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= CreateAsync =================
    [Fact]
    public async Task UT_REF_01_CreateAsync_hop_le_chuan_hoa_ma_va_ten()
    {
        await using var env = await NewEnv();
        var dto = await env.Create(code: " t" + env.N + "a ", name: "  Tên  " + env.N + " ", desc: "  Mô tả  ");
        Assert.Equal(($"T{env.N}A", $"Tên  {env.N}", "Mô tả", ReferenceKinds.CareTypes, true, 1, false), (dto.Code, dto.Name, dto.Description, dto.Kind, dto.Active, dto.Version, dto.Reserved));
        Assert.Null(dto.PatientCount);
    }

    [Fact]
    public async Task UT_REF_02_CreateAsync_chan_doan_duoc_phep_khong_co_ma()
    {
        await using var env = await NewEnv();
        var dto = await env.Create(ReferenceKinds.Diagnoses, code: " ", name: "Viêm tủy " + env.N);
        Assert.Null(dto.Code);
        var other = await env.Create(ReferenceKinds.PatientSources, code: env.Code("s"), name: "Nguồn " + env.N);
        Assert.Equal(0, other.PatientCount);
    }

    public static TheoryData<string, string, int, string> BadCreates => new()
    {
        { "UT-REF-03 Thiếu entry", "noEntry", 400, "MSG04" },
        { "UT-REF-04 Tên rỗng", "emptyName", 400, "MSG02" },
        { "UT-REF-05 Tên 161 ký tự", "name161", 400, "MSG03" },
        { "UT-REF-06 Thiếu mã (loại khác chẩn đoán)", "noCode", 400, "MSG02" },
        { "UT-REF-07 Mã 41 ký tự", "code41", 400, "MSG03" },
        { "UT-REF-08 Mã chứa khoảng trắng", "codeSpace", 400, "MSG04" },
        { "UT-REF-09 Mã bắt đầu bằng dấu chấm", "codeDot", 400, "MSG04" },
        { "UT-REF-10 Mô tả 1001 ký tự", "desc1001", 400, "MSG03" },
        { "UT-REF-11 Mã yêu cầu rỗng", "noKey", 400, "MSG04" },
        { "UT-REF-12 Loại danh mục lạ", "badKind", 400, "MSG04" },
    };

    [Theory, MemberData(nameof(BadCreates))]
    public async Task UT_REF_Create_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        var svc = env.Svc();
        var ok = new ReferenceEntryInput(env.Code("v"), "Tên " + env.N, null);
        ReferenceEntryInput? entry = kind switch
        {
            "noEntry" => null,
            "emptyName" => ok with { Name = "  " },
            "name161" => ok with { Name = new string('n', 161) },
            "noCode" => ok with { Code = null },
            "code41" => ok with { Code = new string('A', 41) },
            "codeSpace" => ok with { Code = "AB CD" },
            "codeDot" => ok with { Code = ".AB" },
            "desc1001" => ok with { Description = new string('d', 1001) },
            _ => ok
        };
        var key = kind == "noKey" ? Guid.Empty : Guid.NewGuid();
        var kindName = kind == "badKind" ? "unknown" : ReferenceKinds.CareTypes;
        await Rejects(() => svc.CreateAsync(env.Owner, kindName, new(key, entry), CancellationToken.None), status, code);
        _ = caseName;
    }

    [Fact]
    public async Task UT_REF_13_CreateAsync_trung_ma_hoac_ten_la_409_MSG201()
    {
        await using var env = await NewEnv();
        var first = await env.Create(code: env.Code("d"), name: "Trùng " + env.N);
        await Rejects(() => env.Create(code: env.Code("d"), name: "Khác " + env.N), 409, "MSG201");
        var byName = await CreateTestKit.ErrorOf(() => env.Create(code: env.Code("e"), name: "trùng " + env.N));
        Assert.Equal((409, "MSG201", "name"), (byName.Status, byName.Response.Code, byName.Response.Field));
        // The same code in another kind is allowed.
        var other = await env.Create(ReferenceKinds.Occupations, code: env.Code("d"), name: "Trùng " + env.N);
        Assert.NotEqual(first.Id, other.Id);
    }

    [Fact]
    public async Task UT_REF_14_CreateAsync_khong_du_quyen_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(denied: $"catalog.{ReferenceKinds.CareTypes}.create"), 403, "MSG06");
    }

    [Fact]
    public async Task UT_REF_15_CreateAsync_gui_lai_cung_khoa_tra_ve_ket_qua_cu()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Create(code: env.Code("k"), key: key);
        var again = await env.Create(code: env.Code("k"), key: key);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await env.Db.ReferenceEntries.CountAsync(x => x.Code == env.Code("k").ToUpperInvariant()));
        await Rejects(() => env.Create(code: env.Code("k"), name: "Khác", key: key), 409, "MSG111");
        await Rejects(() => env.Create(code: env.Code("k"), key: key, actor: env.Dentist), 409, "MSG111");
    }

    // ================= UpdateAsync =================
    [Fact]
    public async Task UT_REF_16_UpdateAsync_doi_ten_tang_phien_ban_va_ghi_lich_su()
    {
        await using var env = await NewEnv();
        var created = await env.Create(code: env.Code("u"));
        var updated = await env.Update(created.Id, created, name: "Tên mới " + env.N, desc: "Mô tả mới");
        Assert.Equal(("Tên mới " + env.N, "Mô tả mới", 2), (updated.Name, updated.Description, updated.Version));
        var history = await env.Svc().HistoryAsync(env.Owner, created.Kind, created.Id, CancellationToken.None);
        Assert.Equal(2, history.Count);
        var upd = Assert.Single(history, h => h.Command == "ReferenceUpdate");
        Assert.Equal(("Sửa", created.Name, updated.Name), (upd.Reason, upd.Before!.Name, upd.After.Name));
        var cre = Assert.Single(history, h => h.Command == "ReferenceCreate");
        Assert.Null(cre.Before);
        Assert.Equal("ow" + env.N, upd.ActorName);
    }

    public static TheoryData<string, string, int, string> BadUpdates => new()
    {
        { "UT-REF-17 Thiếu lý do", "noReason", 400, "MSG02" },
        { "UT-REF-18 Lý do 501 ký tự", "reason501", 400, "MSG03" },
        { "UT-REF-19 Phiên bản cũ", "stale", 409, "MSG111" },
        { "UT-REF-20 Không đổi gì", "noChange", 400, "SETTINGS_NO_CHANGE" },
        { "UT-REF-21 Tên rỗng", "emptyName", 400, "MSG02" },
        { "UT-REF-22 Trùng tên mục khác", "dupName", 409, "MSG201" },
        { "UT-REF-23 Trùng mã mục khác", "dupCode", 409, "MSG201" },
        { "UT-REF-24 Không tìm thấy", "missing", 404, "MSG113" },
        { "UT-REF-25 Sai loại danh mục", "wrongKind", 404, "MSG113" },
    };

    [Theory, MemberData(nameof(BadUpdates))]
    public async Task UT_REF_Update_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("a"), name: "A " + env.N);
        var b = await env.Create(code: env.Code("b"), name: "B " + env.N);
        _ = caseName;
        Func<Task> call = kind switch
        {
            "noReason" => () => env.Update(a.Id, a, name: "X " + env.N, reason: " "),
            "reason501" => () => env.Update(a.Id, a, name: "X " + env.N, reason: new string('r', 501)),
            "stale" => () => env.Update(a.Id, a, name: "X " + env.N, version: 9),
            "noChange" => () => env.Update(a.Id, a),
            "emptyName" => () => env.Update(a.Id, a, name: " "),
            "dupName" => () => env.Update(a.Id, a, name: b.Name),
            "dupCode" => () => env.Update(a.Id, a, code: b.Code),
            "missing" => () => env.Update(Guid.NewGuid(), a, name: "X"),
            _ => () => env.Update(a.Id, a, name: "X " + env.N, kind: ReferenceKinds.Occupations),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_REF_26_UpdateAsync_gui_lai_cung_khoa_khong_tang_phien_ban_lan_nua()
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("r"));
        var key = Guid.NewGuid();
        var first = await env.Update(a.Id, a, name: "Mới " + env.N, key: key);
        var again = await env.Update(a.Id, a, name: "Mới " + env.N, key: key);
        Assert.Equal((first.Version, first.Name), (again.Version, again.Name));
    }

    // ================= ListAsync / GetAsync =================
    [Fact]
    public async Task UT_REF_27_ListAsync_loc_theo_trang_thai_va_tu_khoa_sap_xep_theo_ten()
    {
        await using var env = await NewEnv();
        var tag = "lz" + env.N;
        var b = await env.Create(code: env.Code("lb"), name: tag + " Bình");
        var a = await env.Create(code: env.Code("la"), name: tag + " An");
        await env.SetState(b, false);
        var active = await env.Svc().ListAsync(env.Owner, ReferenceKinds.CareTypes, tag, null, 1, 20, CancellationToken.None);
        Assert.Equal([a.Id], active.Items.Select(x => x.Id));
        var inactive = await env.Svc().ListAsync(env.Owner, ReferenceKinds.CareTypes, tag, "Inactive", 1, 20, CancellationToken.None);
        Assert.Equal([b.Id], inactive.Items.Select(x => x.Id));
        var all = await env.Svc().ListAsync(env.Owner, ReferenceKinds.CareTypes, " " + tag.ToUpperInvariant() + " ", "All", 1, 20, CancellationToken.None);
        Assert.Equal([a.Id, b.Id], all.Items.Select(x => x.Id));
        Assert.Equal(2, all.Total);
        var byCode = await env.Svc().ListAsync(env.Owner, ReferenceKinds.CareTypes, env.Code("lb"), "All", 1, 20, CancellationToken.None);
        Assert.Equal([b.Id], byCode.Items.Select(x => x.Id));
    }

    public static TheoryData<string, string, int, string> BadLists => new()
    {
        { "UT-REF-28 Trang 0", "page0", 400, "MSG04" },
        { "UT-REF-29 Cỡ trang 7", "size7", 400, "MSG04" },
        { "UT-REF-30 Từ khóa 101 ký tự", "q101", 400, "MSG03" },
        { "UT-REF-31 Trạng thái lạ", "status", 400, "MSG04" },
        { "UT-REF-32 Loại danh mục lạ", "kind", 400, "MSG04" },
    };

    [Theory, MemberData(nameof(BadLists))]
    public async Task UT_REF_List_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var svc = env.Svc();
        Func<Task> call = kind switch
        {
            "page0" => () => svc.ListAsync(env.Owner, ReferenceKinds.CareTypes, null, null, 0, 20, CancellationToken.None),
            "size7" => () => svc.ListAsync(env.Owner, ReferenceKinds.CareTypes, null, null, 1, 7, CancellationToken.None),
            "q101" => () => svc.ListAsync(env.Owner, ReferenceKinds.CareTypes, new string('q', 101), null, 1, 20, CancellationToken.None),
            "status" => () => svc.ListAsync(env.Owner, ReferenceKinds.CareTypes, null, "Weird", 1, 20, CancellationToken.None),
            _ => () => svc.ListAsync(env.Owner, "unknown", null, null, 1, 20, CancellationToken.None),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_REF_33_ListAsync_khong_du_quyen_doc_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc($"catalog.{ReferenceKinds.CareTypes}.read").ListAsync(env.Owner, ReferenceKinds.CareTypes, null, null, 1, 20, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_REF_34_GetAsync_tra_ve_muc_va_404_khi_khong_thay()
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("g"));
        var got = await env.Svc().GetAsync(env.Owner, a.Kind, a.Id, CancellationToken.None);
        Assert.Equal(a, got);
        await Rejects(() => env.Svc().GetAsync(env.Owner, a.Kind, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
        await Rejects(() => env.Svc().GetAsync(env.Owner, ReferenceKinds.Occupations, a.Id, CancellationToken.None), 404, "MSG113");
    }

    // ================= StatePreviewAsync / SetStateAsync =================
    [Fact]
    public async Task UT_REF_35_SetStateAsync_ngung_dung_va_khoi_phuc()
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("s1"));
        var off = await env.SetState(a, false, reason: " Không dùng nữa ");
        Assert.Equal((false, 2), (off.Active, off.Version));
        var on = await env.SetState(off, true);
        Assert.Equal((true, 3), (on.Active, on.Version));
        var history = await env.Svc().HistoryAsync(env.Owner, a.Kind, a.Id, CancellationToken.None);
        Assert.Equal(["ReferenceCreate", "ReferenceState", "ReferenceState"], history.Select(x => x.Command).Order());
        Assert.Contains(history, h => h.Reason == "Không dùng nữa");
    }

    public static TheoryData<string, string, int, string> BadStates => new()
    {
        { "UT-REF-36 Thiếu lý do", "noReason", 400, "MSG02" },
        { "UT-REF-37 Phiên bản cũ", "stale", 409, "MSG111" },
        { "UT-REF-38 Đã ở trạng thái đó", "sameState", 409, "MSG111" },
        { "UT-REF-39 Mục hệ thống không được ngừng dùng", "reserved", 409, "SETTINGS_RESERVED_ENTRY" },
        { "UT-REF-40 Không tìm thấy", "missing", 404, "MSG113" },
    };

    [Theory, MemberData(nameof(BadStates))]
    public async Task UT_REF_State_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("s2"));
        _ = caseName;
        if (kind == "reserved")
        {
            var row = await env.Db.ReferenceEntries.SingleAsync(x => x.Id == a.Id);
            row.IsReserved = true; await env.Db.SaveChangesAsync();
        }
        Func<Task> call = kind switch
        {
            "noReason" => () => env.SetState(a, false, reason: null),
            "stale" => () => env.SetState(a, false, version: 9),
            "sameState" => () => env.SetState(a, true),
            "reserved" => () => env.SetState(a, false),
            _ => () => env.SetState(a with { Id = Guid.NewGuid() }, false),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_REF_41_StatePreviewAsync_cho_phep_ngung_dung_muc_binh_thuong()
    {
        await using var env = await NewEnv();
        var a = await env.Create(code: env.Code("p1"));
        var preview = await env.Svc().StatePreviewAsync(env.Owner, a.Kind, a.Id, new(false), CancellationToken.None);
        Assert.Equal((true, 0), (preview.Allowed, preview.Blockers.Count));
        Assert.StartsWith("Mục sẽ không còn được chọn", preview.Impact);
        var already = await env.Svc().StatePreviewAsync(env.Owner, a.Kind, a.Id, new(true), CancellationToken.None);
        Assert.False(already.Allowed);
        Assert.StartsWith("Mục sẽ được chọn lại", already.Impact);
    }

    [Fact]
    public async Task UT_REF_42_StatePreviewAsync_muc_he_thong_bi_chan()
    {
        await using var env = await NewEnv();
        var a = await env.Create(ReferenceKinds.ExpenseCategories, code: env.Code("p2"));
        var row = await env.Db.ReferenceEntries.SingleAsync(x => x.Id == a.Id);
        row.IsReserved = true; await env.Db.SaveChangesAsync();
        var preview = await env.Svc().StatePreviewAsync(env.Owner, a.Kind, a.Id, new(false), CancellationToken.None);
        Assert.False(preview.Allowed);
        Assert.Equal([ReferenceMessages.Reserved.Message], preview.Blockers);
    }

    [Fact]
    public async Task UT_REF_43_StatePreviewAsync_dem_ho_so_benh_nhan_dang_gan_muc()
    {
        await using var env = await NewEnv();
        var source = await env.Create(ReferenceKinds.PatientSources, code: env.Code("p3"));
        env.Db.Patients.AddRange(
            new PatientProfile { PatientCode = "P1" + env.N, FullName = "A", NormalizedName = "A", BirthDate = new DateOnly(1990, 1, 1), Gender = PatientGenders.Male, SourceId = source.Id, CreatedAt = Now, UpdatedAt = Now },
            new PatientProfile { PatientCode = "P2" + env.N, FullName = "B", NormalizedName = "B", BirthDate = new DateOnly(1990, 1, 1), Gender = PatientGenders.Male, SourceId = source.Id, CreatedAt = Now, UpdatedAt = Now },
            new PatientProfile { PatientCode = "P3" + env.N, FullName = "C", NormalizedName = "C", BirthDate = new DateOnly(1990, 1, 1), Gender = PatientGenders.Male, SourceId = source.Id, IsArchived = true, CreatedAt = Now, UpdatedAt = Now });
        await env.Db.SaveChangesAsync();
        var preview = await env.Svc().StatePreviewAsync(env.Owner, source.Kind, source.Id, new(false), CancellationToken.None);
        Assert.Equal(2, preview.Entry.PatientCount);
        Assert.True(preview.Allowed);
        Assert.Contains("Đang gắn với 2 hồ sơ bệnh nhân", preview.Impact);
    }

    // ================= HistoryAsync =================
    [Fact]
    public async Task UT_REF_44_HistoryAsync_404_khi_khong_thay_va_403_khi_khong_co_quyen()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().HistoryAsync(env.Owner, ReferenceKinds.CareTypes, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
        var a = await env.Create(code: env.Code("h"));
        await Rejects(() => env.Svc($"catalog.{ReferenceKinds.CareTypes}.read").HistoryAsync(env.Owner, a.Kind, a.Id, CancellationToken.None), 403, "MSG06");
    }

    // ================= OptionsAsync =================
    [Fact]
    public async Task UT_REF_45_OptionsAsync_bac_si_xem_chan_doan_dang_hoat_dong_theo_ten()
    {
        await using var env = await NewEnv();
        var tag = "ox" + env.N;
        var b = await env.Create(ReferenceKinds.Diagnoses, code: " ", name: tag + " B");
        var a = await env.Create(ReferenceKinds.Diagnoses, code: " ", name: tag + " A");
        var off = await env.Create(ReferenceKinds.Diagnoses, code: " ", name: tag + " Z");
        await env.SetState(off, false);
        var options = await env.Svc().OptionsAsync(env.Dentist, ReferenceKinds.Diagnoses, tag, CancellationToken.None);
        Assert.Equal([a.Id, b.Id], options.Select(x => x.Id));
        Assert.Equal((tag + " A", 1), (options[0].Name, options[0].Version));
    }

    public static TheoryData<string, string, string, bool> OptionAccess => new()
    {
        { "UT-REF-46 Trợ lý xem chẩn đoán", "assistant", ReferenceKinds.Diagnoses, false },
        { "UT-REF-47 Trợ lý xem loại CSKH", "assistant", ReferenceKinds.CareTypes, true },
        { "UT-REF-48 Bác sĩ xem nguồn khách", "dentist", ReferenceKinds.PatientSources, true },
        { "UT-REF-49 Bác sĩ xem loại chi", "dentist", ReferenceKinds.ExpenseCategories, false },
        { "UT-REF-50 Chủ phòng khám xem loại chi", "owner", ReferenceKinds.ExpenseCategories, true },
        { "UT-REF-51 Chủ phòng khám xem chẩn đoán (kế thừa bác sĩ)", "owner", ReferenceKinds.Diagnoses, true },
        { "UT-REF-52 Quản trị viên không xem được", "admin", ReferenceKinds.CareTypes, false },
    };

    [Theory, MemberData(nameof(OptionAccess))]
    public async Task UT_REF_Options_access(string caseName, string who, string kind, bool allowed)
    {
        await using var env = await NewEnv();
        var actor = who switch
        {
            "assistant" => env.Assistant, "dentist" => env.Dentist, "owner" => env.Owner,
            _ => (await env.Db.Users.SingleAsync(x => x.UserName == "ad" + env.N)).Id
        };
        _ = caseName;
        if (allowed) Assert.NotNull(await env.Svc().OptionsAsync(actor, kind, null, CancellationToken.None));
        else await Rejects(() => env.Svc().OptionsAsync(actor, kind, null, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_REF_53_OptionsAsync_khach_hang_hoac_tai_khoan_khoa_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OptionsAsync(env.Customer, ReferenceKinds.CareTypes, null, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc().OptionsAsync(env.Inactive, ReferenceKinds.CareTypes, null, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_REF_54_OptionsAsync_tu_khoa_101_ky_tu_va_loai_la_MSG04()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OptionsAsync(env.Dentist, ReferenceKinds.CareTypes, new string('s', 101), CancellationToken.None), 400, "MSG03");
        await Rejects(() => env.Svc().OptionsAsync(env.Dentist, "unknown", null, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc().OptionsAsync(env.Dentist, null, null, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc().OptionsAsync(env.Dentist, "suppliers", new string('s', 101), CancellationToken.None), 400, "MSG03");
    }

    [Fact]
    public async Task UT_REF_55_OptionsAsync_nha_cung_cap_va_labo_loc_theo_loai_doi_tac()
    {
        await using var env = await NewEnv();
        var tag = "pv" + env.N;
        env.Db.CatalogProviders.AddRange(
            new CatalogProvider { Code = "SU" + env.N, Name = tag + " Supplier", Type = ProviderTypes.Supplier },
            new CatalogProvider { Code = "LA" + env.N, Name = tag + " Lab", Type = ProviderTypes.Lab },
            new CatalogProvider { Code = "BO" + env.N, Name = tag + " Both", Type = ProviderTypes.Both },
            new CatalogProvider { Code = "OF" + env.N, Name = tag + " Off", Type = ProviderTypes.Both, IsActive = false });
        await env.Db.SaveChangesAsync();
        var suppliers = await env.Svc().OptionsAsync(env.Assistant, "suppliers", tag, CancellationToken.None);
        Assert.Equal([tag + " Both", tag + " Supplier"], suppliers.Select(x => x.Name));
        var labs = await env.Svc().OptionsAsync(env.Assistant, "lab-providers", "LA" + env.N, CancellationToken.None);
        Assert.Equal([tag + " Lab"], labs.Select(x => x.Name));
        var noTerm = await env.Svc().OptionsAsync(env.Assistant, "lab-providers", tag, CancellationToken.None);
        Assert.Contains(noTerm, x => x.Name == tag + " Both");
    }

    [Fact]
    public async Task UT_REF_56_OptionsAsync_loai_cong_viec_labo_va_thuoc()
    {
        await using var env = await NewEnv();
        var tag = "wm" + env.N;
        env.Db.LabWorkTypes.Add(new LabWorkType { Code = "WT" + env.N, Name = tag + " Răng sứ", Unit = "cái" });
        env.Db.CatalogMedicines.AddRange(
            new CatalogMedicine { Code = "MD" + env.N, Name = tag + " Amoxicillin", BaseUnit = "viên", PrescriptionEligible = true },
            new CatalogMedicine { Code = "MX" + env.N, Name = tag + " Không kê đơn", BaseUnit = "viên", PrescriptionEligible = false });
        await env.Db.SaveChangesAsync();
        var work = await env.Svc().OptionsAsync(env.Dentist, "lab-work-types", "wt" + env.N, CancellationToken.None);
        Assert.Equal([tag + " Răng sứ"], work.Select(x => x.Name));
        var meds = await env.Svc().OptionsAsync(env.Dentist, "medicines", tag, CancellationToken.None);
        Assert.Equal([tag + " Amoxicillin"], meds.Select(x => x.Name));
        var allMeds = await env.Svc().OptionsAsync(env.Dentist, "medicines", null, CancellationToken.None);
        Assert.NotNull(allMeds);
        await Rejects(() => env.Svc().OptionsAsync(env.Assistant, "medicines", null, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc().OptionsAsync(env.Customer, "suppliers", null, CancellationToken.None), 403, "MSG06");
    }
}
