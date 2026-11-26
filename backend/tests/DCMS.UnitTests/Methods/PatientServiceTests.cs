using DCMS.Application.Common;
using DCMS.Application.Features.Patients;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.SystemSettings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Domain.Features.SystemSettings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Patients;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;
using GeneralSettings = DCMS.Application.Features.SystemSettings.GeneralSettings;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of PatientService (hồ sơ bệnh nhân): SearchAsync, DuplicatesAsync, GetAsync, CreateAsync, UpdateAsync, ArchiveAsync,
/// OwnAsync, UpdateOwnAsync, AuditAsync, FormPolicyAsync and their helpers (Validate, Apply, ValidateExtra, policy, duplicates).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PatientServiceTests
{
    private sealed class Access(bool owner, params int[] branches) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, true, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Actor, Customer;
        public DcmsDbContext Db => Pg.Db;
        public async ValueTask DisposeAsync()
        {
            await Db.ClinicSettingsVersions.ExecuteDeleteAsync();
            await Pg.DisposeAsync();
        }
        public PatientService Svc(bool owner = true, params int[] branches) => new(Db, new Access(owner, branches.Length == 0 ? [N] : branches));
        public string Tag => "z" + N;

        public CreatePatientRequest Req(Func<CreatePatientRequest, CreatePatientRequest>? change = null)
        {
            var r = new CreatePatientRequest(null, "Nguyễn Văn " + Tag, new DateOnly(1990, 5, 20), "male", "0912 345 678", "12 Lê Lợi", null, null, null, "Ghi chú", true);
            return change == null ? r : change(r);
        }
        public Task<PatientChange> Create(Func<CreatePatientRequest, CreatePatientRequest>? change = null, bool owner = true) =>
            Svc(owner).CreateAsync(Actor, Req(change), CancellationToken.None);
        public Task<PatientChange> Update(PatientDetail p, Func<UpdatePatientRequest, UpdatePatientRequest>? change = null)
        {
            var r = new UpdatePatientRequest(p.Version, p.FullName, p.BirthDate, p.Gender, p.Phone, p.Address, p.GuardianName, p.GuardianRelationship, p.GuardianPhone, p.Note, true);
            return Svc().UpdateAsync(Actor, p.Id, change == null ? r : change(r), CancellationToken.None);
        }
        public async Task<PatientProfile> Seed(string name, string? phone = null, DateOnly? birth = null, bool archived = false, string? code = null)
        {
            var p = new PatientProfile
            {
                PatientCode = code ?? $"TX{N}{Guid.NewGuid():N}"[..20].ToUpperInvariant(), FullName = name, NormalizedName = DCMS.Infrastructure.Features.PatientLinks.PatientLinkSupport.NormalizeName(name), Phone = phone,
                BirthDate = birth ?? new DateOnly(1985, 3, 3), Gender = PatientGenders.Female, IsArchived = archived, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };
            Db.Patients.Add(p); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return p;
        }
        public async Task<ReferenceEntry> Entry(string kind, bool active = true)
        {
            var e = new ReferenceEntry { Kind = kind, Code = ("E" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), Name = "Mục " + Guid.NewGuid().ToString("N")[..6], IsActive = active };
            Db.ReferenceEntries.Add(e); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return e;
        }
        public async Task SetPolicy(Func<GeneralSettings, GeneralSettings> change)
        {
            var version = await Db.ClinicSettingsVersions.MaxAsync(x => (int?)x.Version) ?? 0;
            Db.ClinicSettingsVersions.Add(new ClinicSettingsVersion
            {
                Version = version + 1, EffectiveAt = DateTimeOffset.UtcNow, ActorId = Actor, Reason = "test",
                ContentJson = System.Text.Json.JsonSerializer.Serialize(change(GeneralSettings.Default), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            });
            await Db.SaveChangesAsync(); Db.ChangeTracker.Clear();
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
        await db.ClinicSettingsVersions.ExecuteDeleteAsync();
        var n = CreateTestKit.NextId();
        var staff = await CreateTestKit.SeedStaffAsync(db, "ow" + n, AccountRoles.ClinicOwner, n);
        var customer = new ApplicationUser { UserName = "kh" + n, FullName = "Khách " + n, AccountType = "Public", IsActive = true };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Actor = staff.Id, Customer = customer.Id };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= SearchAsync =================
    [Fact]
    public async Task UT_PAT_01_SearchAsync_tim_theo_ma_ten_khong_dau_va_so_dien_thoai()
    {
        await using var env = await NewEnv();
        var a = await env.Seed("Lê Văn Cảnh " + env.Tag, phone: "0987" + (1000 + env.N % 8000) + "00");
        await env.Seed("Phạm Thị Dung " + env.Tag);
        var byName = await env.Svc().SearchAsync(env.Actor, "le van canh " + env.Tag, null, false, 1, 20, null, CancellationToken.None);
        Assert.Equal([a.Id], byName.Items.Select(x => x.Id));
        var byCode = await env.Svc().SearchAsync(env.Actor, a.PatientCode.ToLowerInvariant(), null, false, 1, 20, null, CancellationToken.None);
        Assert.Equal([a.Id], byCode.Items.Select(x => x.Id));
        var byPhone = await env.Svc().SearchAsync(env.Actor, a.Phone![2..], null, false, 1, 20, null, CancellationToken.None);
        Assert.Contains(a.Id, byPhone.Items.Select(x => x.Id));
        var byBirth = await env.Svc().SearchAsync(env.Actor, env.Tag, new DateOnly(1985, 3, 3), false, 1, 20, null, CancellationToken.None);
        Assert.Equal(2, byBirth.Total);
        var none = await env.Svc().SearchAsync(env.Actor, env.Tag, new DateOnly(1970, 1, 1), false, 1, 20, null, CancellationToken.None);
        Assert.Equal(0, none.Total);
    }

    [Fact]
    public async Task UT_PAT_02_SearchAsync_mac_dinh_an_ho_so_ngung_su_dung_va_sap_xep_theo_ten()
    {
        await using var env = await NewEnv();
        var b = await env.Seed("Bình " + env.Tag);
        var a = await env.Seed("An " + env.Tag);
        var old = await env.Seed("Cũ " + env.Tag, archived: true);
        var active = await env.Svc().SearchAsync(env.Actor, env.Tag, null, false, 1, 20, null, CancellationToken.None);
        Assert.Equal([a.Id, b.Id], active.Items.Select(x => x.Id));
        var all = await env.Svc().SearchAsync(env.Actor, env.Tag, null, true, 1, 20, null, CancellationToken.None);
        Assert.Equal(3, all.Total);
        Assert.Contains(all.Items, x => x.Id == old.Id && x.IsArchived);
        var page2 = await env.Svc().SearchAsync(env.Actor, env.Tag, null, true, 2, 20, null, CancellationToken.None);
        Assert.Empty(page2.Items);
        Assert.Equal(3, page2.Total);
    }

    public static TheoryData<string, int, int, string?> BadSearches => new()
    {
        { "UT-PAT-03 Trang 0", 0, 20, null },
        { "UT-PAT-04 Cỡ trang 10", 1, 10, null },
        { "UT-PAT-05 Từ khóa 101 ký tự", 1, 20, "k" },
    };

    [Theory, MemberData(nameof(BadSearches))]
    public async Task UT_PAT_Search_validation(string caseName, int page, int size, string? keyword)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var kw = keyword == null ? null : new string('k', 101);
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SearchAsync(env.Actor, kw, null, false, page, size, null, CancellationToken.None));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task UT_PAT_06_SearchAsync_nhan_vien_chi_thay_ho_so_chua_co_hoat_dong_hoac_thuoc_chi_nhanh_cua_minh()
    {
        await using var env = await NewEnv();
        var fresh = await env.Seed("Mới " + env.Tag);
        var page = await env.Svc(owner: false).SearchAsync(env.Actor, env.Tag, null, false, 1, 20, null, CancellationToken.None);
        Assert.Equal([fresh.Id], page.Items.Select(x => x.Id));
        await Rejects(() => env.Svc(owner: false).SearchAsync(env.Actor, null, null, false, 1, 20, env.N + 99999, CancellationToken.None), 403, "MSG06");
        var branchPage = await env.Svc(owner: false).SearchAsync(env.Actor, env.Tag, null, false, 1, 20, env.N, CancellationToken.None);
        Assert.Equal(0, branchPage.Total);
        var ownerBranch = await env.Svc(owner: true).SearchAsync(env.Actor, env.Tag, null, false, 1, 20, env.N + 12345, CancellationToken.None);
        Assert.Equal(0, ownerBranch.Total);
    }

    // ================= DuplicatesAsync =================
    [Fact]
    public async Task UT_PAT_07_DuplicatesAsync_tim_theo_ten_dien_thoai_va_ngay_sinh_cung_ten_goi()
    {
        await using var env = await NewEnv();
        var same = await env.Seed("Trần Thị Hoa " + env.Tag, phone: "0901" + (100000 + env.N % 800000));
        var otherName = await env.Seed("Lê Hoa", phone: "0911111111", birth: new DateOnly(2001, 2, 2));
        var byName = await env.Svc().DuplicatesAsync(new("Trần Thị Hoa " + env.Tag, null, null, null), CancellationToken.None);
        Assert.Equal([same.Id], byName.Select(x => x.Patient.Id));
        Assert.Equal(["Trùng họ tên"], byName[0].Reasons);
        var byPhone = await env.Svc().DuplicatesAsync(new("Khác hẳn", null, "0901 " + same.Phone![4..], null), CancellationToken.None);
        Assert.Contains(byPhone, x => x.Patient.Id == same.Id && x.Reasons.Contains("Trùng số điện thoại"));
        var byGiven = await env.Svc().DuplicatesAsync(new("Nguyễn Thị Hoa", new DateOnly(2001, 2, 2), null, null), CancellationToken.None);
        Assert.Contains(byGiven, x => x.Patient.Id == otherName.Id && x.Reasons.SequenceEqual(new[] { "Trùng ngày sinh" }));
        var excluded = await env.Svc().DuplicatesAsync(new("Trần Thị Hoa " + env.Tag, null, null, same.Id), CancellationToken.None);
        Assert.Empty(excluded);
        await Rejects(() => env.Svc().DuplicatesAsync(new(" ", null, null, null), CancellationToken.None), 400, "MSG02");
    }

    // ================= GetAsync =================
    [Fact]
    public async Task UT_PAT_08_GetAsync_tra_ve_chi_tiet_va_404_khi_khong_thay()
    {
        await using var env = await NewEnv();
        var p = await env.Seed("Hồ sơ " + env.Tag);
        var detail = await env.Svc().GetAsync(env.Actor, p.Id, CancellationToken.None);
        Assert.Equal((p.PatientCode, p.FullName, false), (detail.PatientCode, detail.FullName, detail.IsArchived));
        Assert.Null(detail.Link.VerifiedAccountUserName);
        await Rejects(() => env.Svc().GetAsync(env.Actor, int.MaxValue - 3, CancellationToken.None), 404, "MSG113");
    }

    // ================= CreateAsync =================
    [Fact]
    public async Task UT_PAT_09_CreateAsync_hop_le_cap_ma_BN_chuan_hoa_so_dien_thoai_va_ghi_nhat_ky()
    {
        await using var env = await NewEnv();
        var result = await env.Create();
        var p = result.Patient;
        Assert.StartsWith("BN", p.PatientCode);
        Assert.Equal(("Nguyễn Văn " + env.Tag, "MALE", "0912345678", 1, false), (p.FullName, p.Gender, p.Phone, p.Version, p.IsArchived));
        Assert.Equal("MSG222", result.Notification.Code);
        var audit = await env.Svc().AuditAsync(env.Actor, p.Id, CancellationToken.None);
        var entry = Assert.Single(audit);
        Assert.Equal(("CREATE", "ow" + env.N), (entry.Action, entry.ActorName));
        Assert.Equal("Mã bệnh nhân", entry.Changes[0].Field);
    }

    public static TheoryData<string, string> BadCreates => new()
    {
        { "UT-PAT-10 Họ tên rỗng", "noName" },
        { "UT-PAT-11 Họ tên 101 ký tự", "name101" },
        { "UT-PAT-12 Ngày sinh ở tương lai", "futureBirth" },
        { "UT-PAT-13 Ngày sinh trước 1900", "birth1899" },
        { "UT-PAT-14 Giới tính lạ", "badGender" },
        { "UT-PAT-15 Số điện thoại chứa chữ", "phoneLetters" },
        { "UT-PAT-16 Số điện thoại ít hơn 8 chữ số", "phoneShort" },
        { "UT-PAT-17 Địa chỉ 256 ký tự", "address256" },
        { "UT-PAT-18 Ghi chú 1001 ký tự", "note1001" },
        { "UT-PAT-19 Người giám hộ thiếu quan hệ", "guardianNoRel" },
        { "UT-PAT-20 Quan hệ giám hộ thiếu tên", "relNoGuardian" },
        { "UT-PAT-21 Mã bệnh nhân sai định dạng", "badCode" },
        { "UT-PAT-22 Mã yêu cầu rỗng", "emptyKey" },
        { "UT-PAT-23 Phiên bản chính sách cũ", "stalePolicy" },
        { "UT-PAT-24 Ngày lập hồ sơ ở tương lai", "futureProfile" },
        { "UT-PAT-25 Nguồn không tồn tại", "badSource" },
        { "UT-PAT-26 Tiền sử bị lặp", "dupIntake" },
        { "UT-PAT-27 Nguồn đã ngừng dùng", "inactiveSource" },
        { "UT-PAT-28 Nguồn sai loại danh mục", "wrongKind" },
    };

    [Theory, MemberData(nameof(BadCreates))]
    public async Task UT_PAT_Create_validation(string caseName, string kind)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var inactive = await env.Entry(ReferenceKinds.PatientSources, active: false);
        var occupation = await env.Entry(ReferenceKinds.Occupations);
        var tooLong = new string('a', 256);
        Func<CreatePatientRequest, CreatePatientRequest> change = kind switch
        {
            "noName" => r => r with { FullName = " " },
            "name101" => r => r with { FullName = new string('n', 101) },
            "futureBirth" => r => r with { BirthDate = new DateOnly(2999, 1, 1) },
            "birth1899" => r => r with { BirthDate = new DateOnly(1899, 12, 31) },
            "badGender" => r => r with { Gender = "unknown" },
            "phoneLetters" => r => r with { Phone = "09abc12345" },
            "phoneShort" => r => r with { Phone = "1234567" },
            "address256" => r => r with { Address = tooLong },
            "note1001" => r => r with { Note = new string('n', 1001) },
            "guardianNoRel" => r => r with { GuardianName = "Mẹ", GuardianRelationship = null },
            "relNoGuardian" => r => r with { GuardianRelationship = "Mẹ" },
            "badCode" => r => r with { PatientCode = "bad code" },
            "emptyKey" => r => r with { RequestKey = Guid.Empty },
            "stalePolicy" => r => r with { PolicyVersion = 77 },
            "futureProfile" => r => r with { ProfileDate = new DateOnly(2999, 1, 1) },
            "badSource" => r => r with { SourceId = Guid.NewGuid() },
            "dupIntake" => r => r with { IntakeHistoryIds = [occupation.Id, occupation.Id] },
            "inactiveSource" => r => r with { SourceId = inactive.Id },
            _ => r => r with { SourceId = occupation.Id },
        };
        var error = await CreateTestKit.ErrorOf(() => env.Create(change));
        Assert.Contains(error.Status, new[] { 400, 409 });
        Assert.StartsWith("MSG", error.Response.Code);
        if (kind == "stalePolicy") Assert.Equal((409, "policyVersion"), (error.Status, error.Response.Field));
        if (kind == "badSource" || kind == "inactiveSource") Assert.Equal("sourceId", error.Response.Field);
        if (kind == "noName") Assert.Equal("MSG02", error.Response.Code);
    }

    [Fact]
    public async Task UT_PAT_29_CreateAsync_nghi_trung_phai_duoc_xac_nhan_da_doi_chieu()
    {
        await using var env = await NewEnv();
        await env.Create();
        await Rejects(() => env.Create(r => r with { DuplicatesReviewed = false }), 400, "MSG02");
        var second = await env.Create(r => r with { DuplicatesReviewed = true });
        Assert.NotEqual(0, second.Patient.Id);
    }

    [Fact]
    public async Task UT_PAT_30_CreateAsync_ma_nhap_tay_viet_hoa_va_khong_duoc_trung()
    {
        await using var env = await NewEnv();
        var code = "kh" + env.N + "x";
        var first = await env.Create(r => r with { PatientCode = code });
        Assert.Equal(code.ToUpperInvariant(), first.Patient.PatientCode);
        await Rejects(() => env.Create(r => r with { FullName = "Người khác " + env.Tag, PatientCode = code }), 409, "MSG201");
    }

    [Fact]
    public async Task UT_PAT_31_CreateAsync_gui_lai_cung_khoa_tra_ve_ho_so_cu_khac_noi_dung_la_409()
    {
        await using var env = await NewEnv();
        var key = Guid.NewGuid();
        var first = await env.Create(r => r with { RequestKey = key });
        var again = await env.Create(r => r with { RequestKey = key });
        Assert.Equal((first.Patient.Id, first.Patient.PatientCode), (again.Patient.Id, again.Patient.PatientCode));
        await Rejects(() => env.Create(r => r with { RequestKey = key, Address = "Địa chỉ khác" }), 409, "MSG111");
    }

    [Fact]
    public async Task UT_PAT_32_CreateAsync_luu_nguon_nhom_nghe_tien_su_va_ghi_ten_vao_nhat_ky()
    {
        await using var env = await NewEnv();
        var source = await env.Entry(ReferenceKinds.PatientSources);
        var group = await env.Entry(ReferenceKinds.CustomerGroups);
        var job = await env.Entry(ReferenceKinds.Occupations);
        var history = await env.Entry(ReferenceKinds.IntakeHistory);
        var result = await env.Create(r => r with
        {
            ProfileDate = new DateOnly(2025, 1, 1), SourceId = source.Id, CustomerGroupId = group.Id, OccupationId = job.Id,
            IntakeHistoryIds = [history.Id], IntakeHistoryOther = "  Dị ứng penicillin  ", PolicyVersion = 0
        });
        var p = result.Patient;
        Assert.Equal(source.Name, p.Source!.Name);
        Assert.Equal((group.Name, job.Name, "Dị ứng penicillin"), (p.CustomerGroup!.Name, p.Occupation!.Name, p.IntakeHistoryOther));
        Assert.Equal([history.Id], p.IntakeHistory.Select(x => x.Id));
        Assert.Equal("ow" + env.N, p.IntakeRecordedByName);
        var audit = (await env.Svc().AuditAsync(env.Actor, p.Id, CancellationToken.None)).Single();
        Assert.Contains(audit.Changes, c => c.Field == "Nguồn" && c.After == source.Name);
        Assert.Contains(audit.Changes, c => c.Field == "Tiền sử" && c.After == history.Name);
    }

    [Fact]
    public async Task UT_PAT_33_CreateAsync_chinh_sach_bat_buoc_ngay_sinh_gioi_tinh_so_dien_thoai_dia_chi_nguon_nhom()
    {
        await using var env = await NewEnv();
        await env.SetPolicy(s => s with { RequiredFields = new(true, true, true, true, true, true, true) });
        Func<CreatePatientRequest, CreatePatientRequest>[] attempts =
        [
            r => r with { BirthDate = null }, r => r with { Gender = null }, r => r with { Phone = null }, r => r with { Address = null },
            r => r with { ProfileDate = null, SourceId = null, CustomerGroupId = null },
        ];
        foreach (var attempt in attempts) await Rejects(() => env.Create(attempt), 400, "MSG02");
        var policy = await env.Svc().FormPolicyAsync(CancellationToken.None);
        Assert.Equal((1, true), (policy.Version, policy.RequiredFields.Gender));
    }

    [Fact]
    public async Task UT_PAT_34_CreateAsync_che_do_ma_ManualOnly_bat_buoc_nhap_ma_va_AutoOnly_bo_qua_ma_gui_len()
    {
        await using var env = await NewEnv();
        await env.SetPolicy(s => s with { PatientCode = new(PatientCodeModes.ManualOnly, "BN") });
        await Rejects(() => env.Create(), 400, "MSG02");
        var manual = await env.Create(r => r with { PatientCode = "mn" + env.N });
        Assert.Equal("MN" + env.N, manual.Patient.PatientCode);
        await env.SetPolicy(s => s with { PatientCode = new(PatientCodeModes.AutoOnly, "TT") });
        var auto = await env.Create(r => r with { FullName = "Tự động " + env.Tag, PatientCode = "IGNORED", DuplicatesReviewed = true });
        Assert.StartsWith("TT", auto.Patient.PatientCode);
    }

    // ================= UpdateAsync =================
    [Fact]
    public async Task UT_PAT_35_UpdateAsync_doi_thong_tin_tang_phien_ban_va_ghi_nhat_ky()
    {
        await using var env = await NewEnv();
        var created = (await env.Create()).Patient;
        var result = await env.Update(created, r => r with { Address = "99 Trần Hưng Đạo", Phone = "+84 912 000 111" });
        Assert.Equal(("99 Trần Hưng Đạo", "+84912000111", 2), (result.Patient.Address, result.Patient.Phone, result.Patient.Version));
        var audit = await env.Svc().AuditAsync(env.Actor, created.Id, CancellationToken.None);
        Assert.Equal(["UPDATE", "CREATE"], audit.Select(x => x.Action));
        Assert.Equal(["Số điện thoại", "Địa chỉ"], audit[0].Changes.Select(c => c.Field));
    }

    [Fact]
    public async Task UT_PAT_36_UpdateAsync_khong_co_thay_doi_thi_khong_tang_phien_ban()
    {
        await using var env = await NewEnv();
        var created = (await env.Create()).Patient;
        var result = await env.Update(created);
        Assert.Equal(1, result.Patient.Version);
        Assert.Single(await env.Svc().AuditAsync(env.Actor, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UT_PAT_37_UpdateAsync_phien_ban_cu_ho_so_khong_co_ho_so_da_ngung_la_loi()
    {
        await using var env = await NewEnv();
        var created = (await env.Create()).Patient;
        await Rejects(() => env.Update(created, r => r with { Version = 9 }), 409, "MSG111");
        await Rejects(() => env.Svc().UpdateAsync(env.Actor, int.MaxValue - 3, new(1, "A", null, null, null, null, null, null, null, null, true), CancellationToken.None), 404, "MSG113");
        await Rejects(() => env.Update(created, r => r with { PolicyVersion = 55, Address = "x" }), 409, "MSG111");
        await Rejects(() => env.Update(created, r => r with { FullName = "" }), 400, "MSG02");
        var archived = await env.Svc().ArchiveAsync(env.Actor, created.Id, new(1, "Ngừng"), true, CancellationToken.None);
        await Rejects(() => env.Update(archived.Patient, r => r with { Address = "y" }), 409, "MSG202");
    }

    // ================= ArchiveAsync =================
    [Fact]
    public async Task UT_PAT_38_ArchiveAsync_ngung_va_khoi_phuc_ho_so_ghi_ly_do()
    {
        await using var env = await NewEnv();
        var created = (await env.Create()).Patient;
        var archived = await env.Svc().ArchiveAsync(env.Actor, created.Id, new(1, "  Chuyển phòng khám  "), true, CancellationToken.None);
        Assert.Equal((true, "Chuyển phòng khám", 2), (archived.Patient.IsArchived, archived.Patient.ArchiveReason, archived.Patient.Version));
        Assert.NotNull(archived.Patient.ArchivedAt);
        var restored = await env.Svc().ArchiveAsync(env.Actor, created.Id, new(2, "Quay lại"), false, CancellationToken.None);
        Assert.Equal((false, null, 3), (restored.Patient.IsArchived, restored.Patient.ArchiveReason, restored.Patient.Version));
        var audit = await env.Svc().AuditAsync(env.Actor, created.Id, CancellationToken.None);
        Assert.Equal(["RESTORE", "ARCHIVE", "CREATE"], audit.Select(x => x.Action));
        Assert.Equal("Quay lại", audit[0].Reason);
    }

    public static TheoryData<string, string, int, string> BadArchives => new()
    {
        { "UT-PAT-39 Thiếu lý do", "noReason", 400, "MSG02" },
        { "UT-PAT-40 Lý do 501 ký tự", "reason501", 400, "MSG03" },
        { "UT-PAT-41 Phiên bản cũ", "stale", 409, "MSG111" },
        { "UT-PAT-42 Khôi phục hồ sơ đang dùng", "restoreActive", 409, "MSG111" },
        { "UT-PAT-43 Không tìm thấy", "missing", 404, "MSG113" },
    };

    [Theory, MemberData(nameof(BadArchives))]
    public async Task UT_PAT_Archive_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var p = (await env.Create()).Patient;
        Func<Task> call = kind switch
        {
            "noReason" => () => env.Svc().ArchiveAsync(env.Actor, p.Id, new(1, " "), true, CancellationToken.None),
            "reason501" => () => env.Svc().ArchiveAsync(env.Actor, p.Id, new(1, new string('r', 501)), true, CancellationToken.None),
            "stale" => () => env.Svc().ArchiveAsync(env.Actor, p.Id, new(5, "x"), true, CancellationToken.None),
            "restoreActive" => () => env.Svc().ArchiveAsync(env.Actor, p.Id, new(1, "x"), false, CancellationToken.None),
            _ => () => env.Svc().ArchiveAsync(env.Actor, int.MaxValue - 3, new(1, "x"), true, CancellationToken.None),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_PAT_44_AuditAsync_khong_co_ho_so_la_404()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().AuditAsync(env.Actor, int.MaxValue - 3, CancellationToken.None), 404, "MSG113");
    }

    // ================= OwnAsync / UpdateOwnAsync =================
    private static async Task<(PatientProfile Patient, PatientAccountLink Link)> Link(Env env, string status = PatientLinkStatus.Verified)
    {
        var p = await env.Seed("Tự quản " + env.Tag);
        var link = new PatientAccountLink { RequestCode = "LK-" + Guid.NewGuid().ToString("N")[..10], AccountId = env.Customer, PatientId = p.Id, DeclaredFullName = p.FullName, Status = status, RequestedAt = DateTimeOffset.UtcNow };
        env.Db.PatientAccountLinks.Add(link); await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear();
        return (p, link);
    }

    [Fact]
    public async Task UT_PAT_45_OwnAsync_can_lien_ket_da_xac_minh_va_ghi_nhat_ky_doc()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OwnAsync(env.Customer, CancellationToken.None), 403, "MSG06");
        var (p, link) = await Link(env, PatientLinkStatus.Pending);
        await Rejects(() => env.Svc().OwnAsync(env.Customer, CancellationToken.None), 403, "MSG06");
        var verified = await env.Db.PatientAccountLinks.SingleAsync(x => x.Id == link.Id);
        verified.Status = PatientLinkStatus.Verified; await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear();
        var record = await env.Svc().OwnAsync(env.Customer, CancellationToken.None);
        Assert.Equal((p.PatientCode, p.FullName, false), (record.PatientCode, record.FullName, record.IsArchived));
        Assert.Equal(1, await env.Db.PatientLinkAudits.CountAsync(x => x.LinkId == link.Id && x.Action == "OwnRecordRead"));
    }

    [Fact]
    public async Task UT_PAT_46_UpdateOwnAsync_tu_cap_nhat_thong_tin_lien_he_va_ghi_nhat_ky()
    {
        await using var env = await NewEnv();
        var (p, _) = await Link(env);
        var request = new UpdateOwnPatientRequest(p.Version, p.FullName, p.BirthDate, p.Gender, "0933 444 555", "Địa chỉ mới", null, null, null);
        var result = await env.Svc().UpdateOwnAsync(env.Customer, request, CancellationToken.None);
        Assert.Equal(("0933444555", "Địa chỉ mới", p.Version + 1), (result.Record.Phone, result.Record.Address, result.Record.Version));
        Assert.Equal("MSG116", result.Notification.Code);
        var audit = await env.Svc().AuditAsync(env.Actor, p.Id, CancellationToken.None);
        Assert.Contains(audit, a => a.Reason == "Bệnh nhân tự cập nhật qua ứng dụng");
        var unchanged = await env.Svc().UpdateOwnAsync(env.Customer, request with { Version = result.Record.Version }, CancellationToken.None);
        Assert.Equal(result.Record.Version, unchanged.Record.Version);
    }

    [Fact]
    public async Task UT_PAT_47_UpdateOwnAsync_phien_ban_cu_ho_so_ngung_va_du_lieu_sai_la_loi()
    {
        await using var env = await NewEnv();
        var (p, _) = await Link(env);
        var ok = new UpdateOwnPatientRequest(p.Version, p.FullName, p.BirthDate, p.Gender, p.Phone, p.Address, null, null, null);
        await Rejects(() => env.Svc().UpdateOwnAsync(env.Customer, ok with { Version = 99 }, CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Svc().UpdateOwnAsync(env.Customer, ok with { FullName = " " }, CancellationToken.None), 400, "MSG02");
        await Rejects(() => env.Svc().UpdateOwnAsync(env.Customer, ok with { Gender = "x" }, CancellationToken.None), 400, "MSG04");
        await Rejects(() => env.Svc().UpdateOwnAsync(env.Actor, ok, CancellationToken.None), 403, "MSG06");
        var row = await env.Db.Patients.SingleAsync(x => x.Id == p.Id);
        row.IsArchived = true; await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear();
        await Rejects(() => env.Svc().UpdateOwnAsync(env.Customer, ok, CancellationToken.None), 409, "MSG202");
    }

    // ================= FormPolicyAsync =================
    [Fact]
    public async Task UT_PAT_48_FormPolicyAsync_mac_dinh_phien_ban_0_khong_bat_buoc_truong_nao()
    {
        await using var env = await NewEnv();
        var policy = await env.Svc().FormPolicyAsync(CancellationToken.None);
        Assert.Equal(0, policy.Version);
        Assert.Equal(GeneralSettings.Default.RequiredFields, policy.RequiredFields);
        Assert.Equal(GeneralSettings.Default.PatientCode, policy.PatientCode);
    }
}
