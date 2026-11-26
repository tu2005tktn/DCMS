using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Domain.Features.Treatment;
using DCMS.Domain.Features.Visits;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of CareTaskService (chăm sóc khách hàng): ListAsync, GetAsync, CreateAsync, UpdateAsync, ContactAsync,
/// OptionsAsync, PatientOptionsAsync, WorklistAsync, ExportAsync and their helpers (authorization, replay, filters).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CareTaskServiceTests
{
    // 09:00 on 10/01/2030 clinic time.
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Due = new(2030, 1, 12, 9, 0, 0, TimeSpan.FromHours(7));

    private sealed class Access(bool owner, int[] branches) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, true, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Assistant, Dentist, Elsewhere, Inactive, Customer, PatientId, PatientId2;
        public required string PatientCode, PatientName;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public CareTaskService Svc(bool owner = false, int[]? branches = null) =>
            new(Db, CreateTestKit.NewRoleUserManager(Db), new TestClock(Now), new Access(owner, branches ?? [N]));

        public CreateCareTaskRequest Req(Func<CreateCareTaskRequest, CreateCareTaskRequest>? change = null)
        {
            var r = new CreateCareTaskRequest(PatientId, N, null, "FollowUp", "Gọi nhắc tái khám", Dentist, Due, Guid.NewGuid());
            return change == null ? r : change(r);
        }
        public Task<CareTaskDto> Create(Func<CreateCareTaskRequest, CreateCareTaskRequest>? change = null, int? actor = null, bool owner = false, int[]? branches = null) =>
            Svc(owner, branches).CreateAsync(actor ?? Assistant, Req(change), CancellationToken.None);

        public async Task<CareTask> Seed(string status = "Open", DateTimeOffset? due = null, int? owner = null, string type = "FollowUp", string content = "Việc", int? patient = null, int? branch = null, int version = 1)
        {
            var t = new CareTask { PatientId = patient ?? PatientId, BranchId = branch ?? N, Type = type, Content = content, OwnerAccountId = owner, DueAt = (due ?? Due).ToUniversalTime(), Status = status, Version = version };
            Db.CareTasks.Add(t); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return t;
        }
        public Task<ContactAttemptDto> Contact(Guid id, string channel = "Phone", string result = "Reached", string? next = null, Guid? rebook = null, int version = 1, Guid? key = null, int? actor = null) =>
            Svc().ContactAsync(actor ?? Assistant, id, new(channel, result, next, rebook, version, key ?? Guid.NewGuid()), CancellationToken.None);
        public Task<CareTaskDto> Update(Guid id, int version = 1, string status = "InProgress", int? owner = null, string content = "Nội dung mới", Guid? key = null, int? actor = null, DateTimeOffset? due = null) =>
            Svc().UpdateAsync(actor ?? Assistant, id, new(version, status, owner, due ?? Due, content, key ?? Guid.NewGuid()), CancellationToken.None);
        public CareWorklistQuery Q(Func<CareWorklistQuery, CareWorklistQuery>? change = null)
        {
            var q = new CareWorklistQuery(N, null, null, null, null, null, null);
            return change == null ? q : change(q);
        }
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
        var n = CreateTestKit.NextId(); var other = CreateTestKit.NextId();
        var assistant = await CreateTestKit.SeedStaffAsync(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var dentist = await CreateTestKit.SeedStaffAsync(db, "bs" + n, AccountRoles.Dentist, n);
        var elsewhere = await CreateTestKit.SeedStaffAsync(db, "el" + n, AccountRoles.Dentist, other);
        var inactive = await CreateTestKit.SeedStaffAsync(db, "in" + n, AccountRoles.Dentist, n);
        inactive.IsActive = false; await db.SaveChangesAsync();
        var customer = new ApplicationUser { UserName = "kh" + n, FullName = "Khách " + n, AccountType = "Customer", IsActive = true, AssignedRoles = [AccountRoles.Patient] };
        db.Users.Add(customer);
        var patient = new PatientProfile { PatientCode = "BN" + n, FullName = "Lê Văn Cảnh " + n, NormalizedName = "LE VAN CANH " + n, Phone = "09" + (10000000 + n % 89999999), BirthDate = new DateOnly(1990, 1, 11), Gender = PatientGenders.Male, CreatedAt = Now, UpdatedAt = Now };
        var patient2 = new PatientProfile { PatientCode = "BM" + n, FullName = "Phạm Thị Dung " + n, NormalizedName = "PHAM THI DUNG " + n, BirthDate = new DateOnly(1991, 2, 12), Gender = PatientGenders.Female, CreatedAt = Now, UpdatedAt = Now };
        db.Patients.AddRange(patient, patient2);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Other = other, Assistant = assistant.Id, Dentist = dentist.Id, Elsewhere = elsewhere.Id, Inactive = inactive.Id, Customer = customer.Id,
            PatientId = patient.Id, PatientId2 = patient2.Id, PatientCode = patient.PatientCode, PatientName = patient.FullName };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= Authorization (AuthorizedAsync via CreateAsync/ListAsync) =================
    // Input: who acts -> Expected: 403 MSG06.
    public static TheoryData<string, string> Forbidden => new()
    {
        { "UT-CARE-01 Tài khoản khách hàng (không phải nhân viên)", "customer" },
        { "UT-CARE-02 Nhân viên đã bị khóa", "inactive" },
        { "UT-CARE-03 Tài khoản không tồn tại", "missing" },
        { "UT-CARE-04 Nhân viên chi nhánh khác", "elsewhere" },
    };

    [Theory, MemberData(nameof(Forbidden))]
    public async Task UT_CARE_Authorization(string caseName, string who)
    {
        await using var env = await NewEnv();
        var actor = who switch { "customer" => env.Customer, "inactive" => env.Inactive, "missing" => int.MaxValue - 5, _ => env.Elsewhere };
        await Rejects(() => env.Svc().ListAsync(actor, env.N, null, null, CancellationToken.None), 403, "MSG06");
        _ = caseName;
    }

    [Fact]
    public async Task UT_CARE_05_Nhan_vien_khong_phai_thanh_vien_chi_nhanh_bi_chan()
    {
        await using var env = await NewEnv();
        var user = await CreateTestKit.SeedStaffAsync(env.Db, "nm" + env.N, AccountRoles.Dentist, env.N, member: false);
        await Rejects(() => env.Svc().ListAsync(user.Id, env.N, null, null, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_CARE_06_Vai_tro_Admin_khong_duoc_phep()
    {
        await using var env = await NewEnv();
        var admin = await CreateTestKit.SeedStaffAsync(env.Db, "ad" + env.N, AccountRoles.Admin, env.N);
        await Rejects(() => env.Svc().ListAsync(admin.Id, env.N, null, null, CancellationToken.None), 403, "MSG06");
    }

    // ================= ListAsync =================
    [Fact]
    public async Task UT_CARE_07_ListAsync_loc_theo_khoang_ngay_va_sap_xep_theo_han()
    {
        await using var env = await NewEnv();
        var late = await env.Seed(due: new DateTimeOffset(2030, 1, 20, 9, 0, 0, TimeSpan.FromHours(7)), content: "muộn");
        var early = await env.Seed(due: new DateTimeOffset(2030, 1, 5, 9, 0, 0, TimeSpan.FromHours(7)), content: "sớm");
        var all = await env.Svc().ListAsync(env.Assistant, env.N, null, null, CancellationToken.None);
        Assert.Equal([early.Id, late.Id], all.Select(x => x.Id));
        var ranged = await env.Svc().ListAsync(env.Assistant, env.N, new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 20), CancellationToken.None);
        Assert.Equal([late.Id], ranged.Select(x => x.Id));
        var onlyFrom = await env.Svc().ListAsync(env.Assistant, env.N, new DateOnly(2030, 1, 6), null, CancellationToken.None);
        Assert.Equal([late.Id], onlyFrom.Select(x => x.Id));
        var onlyTo = await env.Svc().ListAsync(env.Assistant, env.N, null, new DateOnly(2030, 1, 5), CancellationToken.None);
        Assert.Equal([early.Id], onlyTo.Select(x => x.Id));
    }

    [Fact]
    public async Task UT_CARE_08_ListAsync_tu_ngay_lon_hon_den_ngay_la_MSG04()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ListAsync(env.Assistant, env.N, new DateOnly(2030, 2, 1), new DateOnly(2030, 1, 1), CancellationToken.None), 400, "MSG04");
    }

    // ================= CreateAsync =================
    [Fact]
    public async Task UT_CARE_09_CreateAsync_hop_le_luu_cong_viec_va_cat_khoang_trang()
    {
        await using var env = await NewEnv();
        var dto = await env.Create(r => r with { Content = "  Gọi nhắc  ", Type = " FollowUp " });
        Assert.Equal(("Gọi nhắc", "FollowUp", "Open", 1, env.Dentist), (dto.Content, dto.Type, dto.Status, dto.Version, dto.OwnerAccountId));
        Assert.Equal(["update", "recordContact"], dto.AllowedActions);
        Assert.Equal(Due.ToUniversalTime(), dto.DueAt);
    }

    public static TheoryData<string, string> BadCreates => new()
    {
        { "UT-CARE-10 Nội dung rỗng", "emptyContent" },
        { "UT-CARE-11 Nội dung 4001 ký tự", "content4001" },
        { "UT-CARE-12 Loại rỗng", "emptyType" },
        { "UT-CARE-13 Loại 41 ký tự", "type41" },
        { "UT-CARE-14 Hạn xử lý mặc định", "noDue" },
        { "UT-CARE-15 Bệnh nhân không tồn tại", "noPatient" },
        { "UT-CARE-16 Mã yêu cầu rỗng", "noKey" },
    };

    [Theory, MemberData(nameof(BadCreates))]
    public async Task UT_CARE_Create_validation(string caseName, string kind)
    {
        await using var env = await NewEnv();
        Func<CreateCareTaskRequest, CreateCareTaskRequest> change = kind switch
        {
            "emptyContent" => r => r with { Content = "  " },
            "content4001" => r => r with { Content = new string('a', 4001) },
            "emptyType" => r => r with { Type = "" },
            "type41" => r => r with { Type = new string('t', 41) },
            "noDue" => r => r with { DueAt = default },
            "noPatient" => r => r with { PatientId = int.MaxValue - 9 },
            _ => r => r with { RequestKey = Guid.Empty },
        };
        await Rejects(() => env.Create(change), 400, "MSG04");
        _ = caseName;
    }

    [Fact]
    public async Task UT_CARE_17_CreateAsync_benh_nhan_da_luu_tru_bi_tu_choi()
    {
        await using var env = await NewEnv();
        var p = await env.Db.Patients.SingleAsync(x => x.Id == env.PatientId2);
        p.IsArchived = true; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Create(r => r with { PatientId = env.PatientId2 }), 400, "MSG04");
    }

    [Fact]
    public async Task UT_CARE_18_CreateAsync_nguoi_phu_trach_ngoai_chi_nhanh_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(r => r with { OwnerAccountId = env.Elsewhere }), 403, "MSG06");
    }

    [Fact]
    public async Task UT_CARE_19_CreateAsync_lich_hen_khong_ton_tai_la_404_MSG113()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(r => r with { AppointmentId = Guid.NewGuid() }), 404, "MSG113");
    }

    [Fact]
    public async Task UT_CARE_20_CreateAsync_gui_lai_cung_khoa_tra_ve_cong_viec_cu()
    {
        await using var env = await NewEnv();
        var req = env.Req();
        var first = await env.Svc().CreateAsync(env.Assistant, req, CancellationToken.None);
        var again = await env.Svc().CreateAsync(env.Assistant, req, CancellationToken.None);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, await env.Db.CareTasks.CountAsync(x => x.PatientId == env.PatientId && x.BranchId == env.N));
    }

    [Fact]
    public async Task UT_CARE_21_CreateAsync_cung_khoa_khac_noi_dung_la_409_MSG111()
    {
        await using var env = await NewEnv();
        var req = env.Req();
        await env.Svc().CreateAsync(env.Assistant, req, CancellationToken.None);
        await Rejects(() => env.Svc().CreateAsync(env.Assistant, req with { Content = "Khác" }, CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Svc().CreateAsync(env.Dentist, req, CancellationToken.None), 409, "MSG111");
    }

    // ================= GetAsync =================
    [Fact]
    public async Task UT_CARE_22_GetAsync_tra_ve_cong_viec_va_cac_lan_lien_he_theo_thoi_gian()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var first = await env.Contact(t.Id, "Phone", "NoAnswer");
        var second = await env.Contact(t.Id, "InPerson", "Reached", " Hẹn lại ", version: 2);
        var detail = await env.Svc().GetAsync(env.Assistant, t.Id, CancellationToken.None);
        Assert.Equal(t.Id, detail.Task.Id);
        Assert.Equal(3, detail.Task.Version);
        Assert.Equal([first.Id, second.Id], detail.Contacts.Select(x => x.Id));
        Assert.Equal("Hẹn lại", detail.Contacts[1].NextAction);
    }

    [Fact]
    public async Task UT_CARE_23_GetAsync_khong_ton_tai_la_404_MSG113()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().GetAsync(env.Assistant, Guid.NewGuid(), CancellationToken.None), 404, "MSG113");
    }

    [Fact]
    public async Task UT_CARE_24_GetAsync_chi_nhanh_khong_doc_duoc_la_403()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        await Rejects(() => env.Svc().GetAsync(env.Elsewhere, t.Id, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc().GetAsync(env.Customer, t.Id, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_CARE_25_GetAsync_chu_phong_kham_doc_duoc_moi_chi_nhanh()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var detail = await env.Svc(owner: true, branches: []).GetAsync(env.Assistant, t.Id, CancellationToken.None);
        Assert.Equal(t.Id, detail.Task.Id);
    }

    // ================= UpdateAsync =================
    [Fact]
    public async Task UT_CARE_26_UpdateAsync_doi_trang_thai_noi_dung_han_va_tang_phien_ban()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var newDue = new DateTimeOffset(2030, 2, 1, 10, 0, 0, TimeSpan.FromHours(7));
        var dto = await env.Update(t.Id, status: "InProgress", owner: env.Dentist, content: " Đã gọi lần 1 ", due: newDue);
        Assert.Equal(("InProgress", "Đã gọi lần 1", 2, env.Dentist), (dto.Status, dto.Content, dto.Version, dto.OwnerAccountId));
        Assert.Equal(newDue.ToUniversalTime(), dto.DueAt);
    }

    [Fact]
    public async Task UT_CARE_27_UpdateAsync_Done_khong_con_hanh_dong()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var dto = await env.Update(t.Id, status: "Done");
        Assert.Empty(dto.AllowedActions);
        await Rejects(() => env.Update(t.Id, version: 2, status: "Open"), 409, "MSG111");
    }

    public static TheoryData<string, string, int, int, string> BadUpdates => new()
    {
        { "UT-CARE-28 Phiên bản cũ", "staleVersion", 409, 0, "MSG111" },
        { "UT-CARE-29 Trạng thái không hợp lệ", "badStatus", 400, 0, "MSG04" },
        { "UT-CARE-30 Nội dung rỗng", "emptyContent", 400, 0, "MSG04" },
        { "UT-CARE-31 Hạn mặc định", "noDue", 400, 0, "MSG04" },
        { "UT-CARE-32 Mã yêu cầu rỗng", "noKey", 400, 0, "MSG04" },
        { "UT-CARE-33 Người phụ trách ngoài chi nhánh", "foreignOwner", 403, 0, "MSG06" },
    };

    [Theory, MemberData(nameof(BadUpdates))]
    public async Task UT_CARE_Update_validation(string caseName, string kind, int status, int unused, string code)
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        _ = (caseName, unused);
        Func<Task> call = kind switch
        {
            "staleVersion" => () => env.Update(t.Id, version: 9),
            "badStatus" => () => env.Update(t.Id, status: "Weird"),
            "emptyContent" => () => env.Update(t.Id, content: " "),
            "noDue" => () => env.Svc().UpdateAsync(env.Assistant, t.Id, new(1, "Open", null, default, "x", Guid.NewGuid()), CancellationToken.None),
            "noKey" => () => env.Update(t.Id, key: Guid.Empty),
            _ => () => env.Update(t.Id, owner: env.Elsewhere),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_CARE_34_UpdateAsync_khong_ton_tai_la_404()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Update(Guid.NewGuid()), 404, "MSG113");
    }

    [Fact]
    public async Task UT_CARE_35_UpdateAsync_gui_lai_cung_khoa_tra_ve_ket_qua_cu_va_khong_tang_phien_ban_lan_nua()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var key = Guid.NewGuid();
        var first = await env.Update(t.Id, key: key);
        var again = await env.Update(t.Id, key: key);
        Assert.Equal((first.Id, 2), (again.Id, again.Version));
        await Rejects(() => env.Update(t.Id, key: key, content: "Khác"), 409, "MSG111");
    }

    // ================= ContactAsync =================
    public static TheoryData<string, string, string, int, string> BadContacts => new()
    {
        { "UT-CARE-36 Kênh không hợp lệ", "badChannel", "", 400, "MSG04" },
        { "UT-CARE-37 Kết quả không hợp lệ", "badResult", "", 400, "MSG04" },
        { "UT-CARE-38 Ghi chú tiếp theo 1001 ký tự", "next1001", "", 400, "MSG04" },
        { "UT-CARE-39 Phiên bản cũ", "stale", "", 409, "MSG111" },
        { "UT-CARE-40 Đặt lại lịch nhưng thiếu mã lịch hẹn", "rebookNoId", "", 409, "MSG230" },
        { "UT-CARE-41 Đặt lại lịch với lịch hẹn không tồn tại", "rebookMissing", "", 409, "MSG230" },
        { "UT-CARE-42 Mã yêu cầu rỗng", "noKey", "", 400, "MSG04" },
    };

    [Theory, MemberData(nameof(BadContacts))]
    public async Task UT_CARE_Contact_validation(string caseName, string kind, string unused, int status, string code)
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        _ = (caseName, unused);
        Func<Task> call = kind switch
        {
            "badChannel" => () => env.Contact(t.Id, channel: "Fax"),
            "badResult" => () => env.Contact(t.Id, result: "Maybe"),
            "next1001" => () => env.Contact(t.Id, next: new string('n', 1001)),
            "stale" => () => env.Contact(t.Id, version: 7),
            "rebookNoId" => () => env.Contact(t.Id, result: "Rebooked"),
            "rebookMissing" => () => env.Contact(t.Id, result: "Rebooked", rebook: Guid.NewGuid()),
            _ => () => env.Contact(t.Id, key: Guid.Empty),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_CARE_43_ContactAsync_cong_viec_da_xong_la_409()
    {
        await using var env = await NewEnv();
        var t = await env.Seed(status: "Done");
        await Rejects(() => env.Contact(t.Id), 409, "MSG111");
    }

    [Fact]
    public async Task UT_CARE_44_ContactAsync_khong_ton_tai_la_404()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Contact(Guid.NewGuid()), 404, "MSG113");
    }

    [Fact]
    public async Task UT_CARE_45_ContactAsync_ghi_nhan_lien_he_va_tang_phien_ban_cong_viec()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var dto = await env.Contact(t.Id, "Phone", "Declined", "   ");
        Assert.Equal((t.Id, "Phone", "Declined", null, env.Assistant, Now), (dto.TaskId, dto.Channel, dto.Result, dto.NextAction, dto.ActorId, dto.OccurredAt));
        env.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await env.Db.CareTasks.SingleAsync(x => x.Id == t.Id)).Version);
    }

    [Fact]
    public async Task UT_CARE_46_ContactAsync_gui_lai_cung_khoa_tra_ve_lan_lien_he_cu()
    {
        await using var env = await NewEnv();
        var t = await env.Seed();
        var key = Guid.NewGuid();
        var first = await env.Contact(t.Id, key: key);
        var again = await env.Contact(t.Id, key: key);
        Assert.Equal(first.Id, again.Id);
        await Rejects(() => env.Contact(t.Id, key: key, result: "NoAnswer"), 409, "MSG111");
    }

    // ================= OptionsAsync =================
    [Fact]
    public async Task UT_CARE_47_OptionsAsync_nhan_vien_thay_chi_nhanh_cua_minh_va_nhan_su()
    {
        await using var env = await NewEnv();
        env.Db.ReferenceEntries.AddRange(
            new ReferenceEntry { Kind = ReferenceKinds.CareTypes, Code = "z" + env.N + "b", Name = "Zeta " + env.N },
            new ReferenceEntry { Kind = ReferenceKinds.CareTypes, Code = "a" + env.N + "a", Name = "Alpha " + env.N },
            new ReferenceEntry { Kind = ReferenceKinds.CareTypes, Code = "x" + env.N + "x", Name = "Ẩn " + env.N, IsActive = false });
        await env.Db.SaveChangesAsync();
        var options = await env.Svc().OptionsAsync(env.Assistant, CancellationToken.None);
        Assert.Equal((new DateOnly(2030, 1, 10), env.Assistant, false), (options.ClinicDate, options.ActorAccountId, options.AllBranches));
        var branch = Assert.Single(options.Branches);
        Assert.Equal((env.N, true), (branch.Id, branch.CanCreate));
        Assert.Contains(branch.Staff, s => s.AccountId == env.Dentist && s.Roles.SequenceEqual(new[] { AccountRoles.Dentist }));
        Assert.Contains(branch.Staff, s => s.AccountId == env.Assistant && s.Roles.SequenceEqual(new[] { AccountRoles.ClinicAssistant }));
        Assert.DoesNotContain(branch.Staff, s => s.AccountId == env.Elsewhere);
        var custom = options.Types.Where(x => !x.System && (x.Code.EndsWith("a") || x.Code.EndsWith("b") || x.Code.EndsWith("x"))).Select(x => x.Code).Where(c => c.Contains(env.N.ToString())).ToArray();
        Assert.Equal(["a" + env.N + "a", "z" + env.N + "b"], custom);
        Assert.Contains(options.Types, x => x is { Code: "FollowUp", System: true });
        Assert.Contains(options.Types, x => x is { Code: "MissedAppointment", Name: "Vắng hẹn" });
    }

    [Fact]
    public async Task UT_CARE_48_OptionsAsync_chu_phong_kham_thay_moi_chi_nhanh_dang_hoat_dong()
    {
        await using var env = await NewEnv();
        var options = await env.Svc(owner: true, branches: []).OptionsAsync(env.Assistant, CancellationToken.None);
        Assert.True(options.AllBranches);
        Assert.Contains(options.Branches, b => b.Id == env.N);
        Assert.Contains(options.Branches, b => b.Id == env.Other);
    }

    [Fact]
    public async Task UT_CARE_49_OptionsAsync_khach_hang_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OptionsAsync(env.Customer, CancellationToken.None), 403, "MSG06");
    }

    // ================= PatientOptionsAsync =================
    private static async Task SeedVisit(Env env, int patient, string status, DateTimeOffset arrival, int? branch = null)
    {
        var db = env.Db;
        var sb = await db.Set<DCMS.Domain.Features.Scheduling.SchedulingBranch>().SingleAsync(x => x.Id == env.N);
        var chair = sb.Chairs.FirstOrDefault();
        if (chair == null) { chair = new DCMS.Domain.Features.Scheduling.SchedulingChair { Id = env.N, BranchId = env.N, Name = "Ghế " + env.N }; sb.Chairs.Add(chair); await db.SaveChangesAsync(); }
        var emp = (await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == env.Dentist)).Id;
        var a = new DCMS.Domain.Features.Appointments.Appointment
        {
            Code = "LH" + CreateTestKit.NextId(), BranchId = env.N, DentistId = emp, ChairId = chair.Id, BookerAccountId = env.Customer, AttendeeName = "Khách",
            PatientId = patient, StartAt = arrival, EndAt = arrival.AddMinutes(30), Status = "Confirmed", Version = 1, CreatedAt = Now,
            BookingInfo = new DCMS.Domain.Features.Appointments.AppointmentBookingInfo { BookerName = "Người đặt", BookerContact = "0987654321" }
        };
        db.Appointments.Add(a); await db.SaveChangesAsync();
        db.Visits.Add(new Visit { AppointmentId = a.Id, PatientId = patient, BranchId = branch ?? env.N, DentistId = env.Dentist, Status = status, ArrivalAt = arrival, RecordedBy = env.Assistant });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UT_CARE_50_PatientOptionsAsync_chi_tra_benh_nhan_da_den_kham_theo_lan_gan_nhat()
    {
        await using var env = await NewEnv();
        await SeedVisit(env, env.PatientId, VisitStatuses.Completed, Now.AddDays(-3));
        await SeedVisit(env, env.PatientId, VisitStatuses.InProgress, Now.AddDays(-1));
        await SeedVisit(env, env.PatientId2, VisitStatuses.Queued, Now);
        var rows = await env.Svc().PatientOptionsAsync(env.Assistant, env.N, env.PatientCode, CancellationToken.None);
        var one = Assert.Single(rows);
        Assert.Equal((env.PatientId, env.PatientName, Now.AddDays(-1)), (one.PatientId, one.FullName, one.LastVisitAt));
        var byName = await env.Svc().PatientOptionsAsync(env.Assistant, env.N, "cảnh " + env.N, CancellationToken.None);
        Assert.Equal([env.PatientId], byName.Select(x => x.PatientId));
    }

    [Fact]
    public async Task UT_CARE_51_PatientOptionsAsync_tim_theo_so_dien_thoai_va_khong_nhan_ky_tu_dac_biet_nhu_wildcard()
    {
        await using var env = await NewEnv();
        await SeedVisit(env, env.PatientId, VisitStatuses.Completed, Now.AddDays(-3));
        var patient = await env.Db.Patients.AsNoTracking().SingleAsync(x => x.Id == env.PatientId);
        var byPhone = await env.Svc().PatientOptionsAsync(env.Assistant, env.N, patient.Phone!, CancellationToken.None);
        Assert.Equal([env.PatientId], byPhone.Select(x => x.PatientId));
        Assert.Empty(await env.Svc().PatientOptionsAsync(env.Assistant, env.N, "%", CancellationToken.None));
        Assert.Empty(await env.Svc().PatientOptionsAsync(env.Assistant, env.N, "_", CancellationToken.None));
    }

    [Fact]
    public async Task UT_CARE_52_PatientOptionsAsync_tu_khoa_rong_hoac_101_ky_tu_la_MSG04()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().PatientOptionsAsync(env.Assistant, env.N, "  ", CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc().PatientOptionsAsync(env.Assistant, env.N, null, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc().PatientOptionsAsync(env.Assistant, env.N, new string('a', 101), CancellationToken.None), 400, "MSG04");
    }

    [Fact]
    public async Task UT_CARE_53_PatientOptionsAsync_chi_nhanh_khac_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().PatientOptionsAsync(env.Elsewhere, env.N, "x", CancellationToken.None), 403, "MSG06");
    }

    // ================= WorklistAsync / FilterAsync / ItemsAsync =================
    public static TheoryData<string, string> BadQueries => new()
    {
        { "UT-CARE-54 Trang 0", "page0" },
        { "UT-CARE-55 Cỡ trang 10 không nằm trong 20/50/100", "size10" },
        { "UT-CARE-56 Từ ngày lớn hơn đến ngày", "reversed" },
        { "UT-CARE-57 Khoảng ngày hơn 366 ngày", "range367" },
        { "UT-CARE-58 Trạng thái lạ", "badStatus" },
        { "UT-CARE-59 Loại 41 ký tự", "type41" },
        { "UT-CARE-60 Từ khóa 101 ký tự", "search101" },
    };

    [Theory, MemberData(nameof(BadQueries))]
    public async Task UT_CARE_Worklist_validation(string caseName, string kind)
    {
        await using var env = await NewEnv();
        Func<CareWorklistQuery, CareWorklistQuery> change = kind switch
        {
            "page0" => q => q with { Page = 0 },
            "size10" => q => q with { PageSize = 10 },
            "reversed" => q => q with { From = new DateOnly(2030, 2, 1), To = new DateOnly(2030, 1, 1) },
            "range367" => q => q with { From = new DateOnly(2030, 1, 1), To = new DateOnly(2031, 1, 3) },
            "badStatus" => q => q with { Status = "Weird" },
            "type41" => q => q with { Type = new string('t', 41) },
            _ => q => q with { Search = new string('s', 101) },
        };
        await Rejects(() => env.Svc().WorklistAsync(env.Assistant, env.Q(change), CancellationToken.None), 400, "MSG04");
        _ = caseName;
    }

    [Fact]
    public async Task UT_CARE_61_WorklistAsync_chi_nhanh_khong_doc_duoc_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().WorklistAsync(env.Assistant, env.Q(q => q with { BranchId = env.Other }), CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_CARE_62_WorklistAsync_loc_theo_trang_thai_loai_nguoi_phu_trach_va_khoang_ngay()
    {
        await using var env = await NewEnv();
        var open = await env.Seed("Open", owner: env.Dentist, type: "FollowUp", due: new DateTimeOffset(2030, 1, 5, 9, 0, 0, TimeSpan.FromHours(7)));
        var prog = await env.Seed("InProgress", type: "LabBlocked", due: new DateTimeOffset(2030, 1, 6, 9, 0, 0, TimeSpan.FromHours(7)));
        var done = await env.Seed("Done", due: new DateTimeOffset(2030, 1, 7, 9, 0, 0, TimeSpan.FromHours(7)));
        Task<CareWorklistPage> Run(Func<CareWorklistQuery, CareWorklistQuery> f) => env.Svc().WorklistAsync(env.Assistant, env.Q(f), CancellationToken.None);
        Assert.Equal([open.Id, prog.Id, done.Id], (await Run(q => q)).Items.Select(x => x.Task.Id));
        Assert.Equal([open.Id, prog.Id], (await Run(q => q with { Status = "Pending" })).Items.Select(x => x.Task.Id));
        Assert.Equal([done.Id], (await Run(q => q with { Status = "Done" })).Items.Select(x => x.Task.Id));
        Assert.Equal([prog.Id], (await Run(q => q with { Type = " LabBlocked " })).Items.Select(x => x.Task.Id));
        Assert.Equal([open.Id], (await Run(q => q with { OwnerAccountId = env.Dentist })).Items.Select(x => x.Task.Id));
        Assert.Equal([prog.Id], (await Run(q => q with { From = new DateOnly(2030, 1, 6), To = new DateOnly(2030, 1, 6) })).Items.Select(x => x.Task.Id));
    }

    [Fact]
    public async Task UT_CARE_63_WorklistAsync_tim_theo_ten_ma_va_phan_trang()
    {
        await using var env = await NewEnv();
        var t1 = await env.Seed(due: Due);
        var t2 = await env.Seed(due: Due.AddHours(1), patient: env.PatientId2);
        var t3 = await env.Seed(due: Due.AddHours(2));
        var byName = await env.Svc().WorklistAsync(env.Assistant, env.Q(q => q with { Search = "dung " + env.N }), CancellationToken.None);
        Assert.Equal([t2.Id], byName.Items.Select(x => x.Task.Id));
        var byCode = await env.Svc().WorklistAsync(env.Assistant, env.Q(q => q with { Search = env.PatientCode }), CancellationToken.None);
        Assert.Equal([t1.Id, t3.Id], byCode.Items.Select(x => x.Task.Id));
        var paged = await env.Svc().WorklistAsync(env.Assistant, env.Q(q => q with { Page = 2, PageSize = 20 }), CancellationToken.None);
        Assert.Equal((3, 2, 20), (paged.Total, paged.Page, paged.PageSize));
        Assert.Empty(paged.Items);
    }

    [Fact]
    public async Task UT_CARE_64_WorklistAsync_bo_sung_ten_benh_nhan_nguoi_phu_trach_loai_va_lien_he_gan_nhat()
    {
        await using var env = await NewEnv();
        env.Db.ReferenceEntries.Add(new ReferenceEntry { Kind = ReferenceKinds.CareTypes, Code = "VIP" + env.N, Name = "Khách VIP" });
        await env.Db.SaveChangesAsync();
        var t = await env.Seed(owner: env.Dentist, type: "VIP" + env.N);
        var plain = await env.Seed(type: "KhongCoTrongDanhMuc", due: Due.AddHours(1));
        await env.Contact(t.Id, "Phone", "NoAnswer");
        await env.Contact(t.Id, "Phone", "Reached", version: 2);
        var page = await env.Svc().WorklistAsync(env.Assistant, env.Q(), CancellationToken.None);
        var item = page.Items.Single(x => x.Task.Id == t.Id);
        Assert.Equal((env.PatientCode, env.PatientName, "Khách VIP", "Chi nhánh " + env.N), (item.PatientCode, item.PatientName, item.TypeName, item.BranchName));
        Assert.Contains(item.LastContactResult, new[] { "Reached", "NoAnswer" });
        Assert.Equal("bs" + env.N, item.OwnerName);
        Assert.NotNull(item.LastContactAt);
        var other = page.Items.Single(x => x.Task.Id == plain.Id);
        Assert.Equal(("KhongCoTrongDanhMuc", null, null), (other.TypeName, other.OwnerName, other.LastContactResult));
    }

    [Fact]
    public async Task UT_CARE_65_WorklistAsync_khong_chon_chi_nhanh_gom_cac_chi_nhanh_doc_duoc()
    {
        await using var env = await NewEnv();
        var mine = await env.Seed();
        await env.Seed(branch: env.Other);
        var page = await env.Svc().WorklistAsync(env.Assistant, env.Q(q => q with { BranchId = null }), CancellationToken.None);
        Assert.Equal([mine.Id], page.Items.Select(x => x.Task.Id));
    }

    // ================= ExportAsync =================
    [Fact]
    public async Task UT_CARE_66_ExportAsync_tra_ve_tep_xlsx()
    {
        await using var env = await NewEnv();
        var t = await env.Seed(owner: env.Dentist);
        await env.Contact(t.Id, "Phone", "Reached");
        var bytes = await env.Svc().ExportAsync(env.Assistant, env.Q(), CancellationToken.None);
        Assert.True(bytes.Length > 100);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task UT_CARE_67_ExportAsync_bo_loc_sai_la_MSG04()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().ExportAsync(env.Assistant, env.Q(q => q with { Status = "Weird" }), CancellationToken.None), 400, "MSG04");
    }
}
