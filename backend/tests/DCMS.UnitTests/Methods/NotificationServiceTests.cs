using DCMS.Application.Features.Notifications;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.PatientLinks;
using DCMS.Domain.Features.Patients;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Notifications;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of NotificationService (orchestration: ListAsync, MarkReadAsync, MarkReadOneAsync, CountUnreadAsync,
/// CheckDeepLinkAsync, CreateAsync) and EfNotificationStore (ListAsync, GetByIdAsync, UpsertAsync, CountUnreadAsync, MarkReadAsync,
/// ExistsByDedupeKeyAsync, GetCursorForAccountAsync, CanOpenAppointmentAsync).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NotificationServiceTests
{
    private sealed class Realtime(bool fail = false) : IRealtimeNotifier
    {
        public List<(string Target, int Id, string Method)> Sent { get; } = [];
        public Task SendToAccountAsync(int accountId, string method, object payload, CancellationToken ct = default)
        {
            if (fail) throw new InvalidOperationException("hub down");
            Sent.Add(("account", accountId, method)); return Task.CompletedTask;
        }
        public Task SendToStaffBranchAsync(int branchId, string method, object payload, CancellationToken ct = default)
        {
            if (fail) throw new InvalidOperationException("hub down");
            Sent.Add(("branch", branchId, method)); return Task.CompletedTask;
        }
        public Task SendToAllAsync(string method, object payload, CancellationToken ct = default) { Sent.Add(("all", 0, method)); return Task.CompletedTask; }
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Account, Other;
        public Realtime Hub = new();
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public EfNotificationStore Store() => new(Db);
        public NotificationService Svc(Dictionary<string, string?>? config = null, Realtime? hub = null)
        {
            Hub = hub ?? Hub;
            return new(Store(), Db, new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build(), Hub, NullLogger<NotificationService>.Instance);
        }
        public string Key(string tag = "") => $"k{N}{tag}{Guid.NewGuid():N}";
        public NotificationDto Dto(int? account = null, string? key = null, DateTimeOffset? at = null, string title = "Tiêu đề", NotificationKind kind = NotificationKind.AppointmentBooked,
            string? appointment = null, string? request = null, string? link = null, params NotificationAttempt[] attempts) =>
            new("NT-" + Guid.NewGuid().ToString("N"), account ?? Account, kind, title, "Nội dung", at ?? DateTimeOffset.UtcNow, null, appointment, request, link,
                attempts.Length == 0 ? [new(NotificationChannel.InApp, DispatchStatus.Delivered, 1, DateTimeOffset.UtcNow, null, null)] : attempts, false, key ?? Key());
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var db = pg.Db;
        var n = CreateTestKit.NextId();
        var a = new ApplicationUser { UserName = "nt" + n, FullName = "Khách " + n, AccountType = "Public", IsActive = true };
        var b = new ApplicationUser { UserName = "nu" + n, FullName = "Khách khác " + n, AccountType = "Public", IsActive = true };
        db.Users.AddRange(a, b);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Account = a.Id, Other = b.Id };
    }

    // ================= EfNotificationStore =================
    [Fact]
    public async Task UT_NTF_01_UpsertAsync_luu_moi_va_trung_khoa_tra_ve_ban_cu()
    {
        await using var env = await NewEnv();
        var dto = env.Dto(request: Guid.NewGuid().ToString(), link: "/appointments",
            attempts: [new(NotificationChannel.Email, DispatchStatus.Pending, 0, null, DateTimeOffset.UtcNow.AddMinutes(1), "lỗi"), new(NotificationChannel.InApp, DispatchStatus.Delivered, 1, DateTimeOffset.UtcNow, null, null)]);
        var saved = await env.Store().UpsertAsync(dto, CancellationToken.None);
        Assert.Equal((dto.Id, dto.Title, dto.DedupeKey, "/appointments"), (saved.Id, saved.Title, saved.DedupeKey, saved.DeepLink));
        Assert.Equal([NotificationChannel.Email, NotificationChannel.InApp], saved.Attempts.Select(x => x.Channel));
        Assert.Equal("lỗi", saved.Attempts[0].ErrorMessage);
        var again = await env.Store().UpsertAsync(dto with { Id = "NT-other", Title = "Khác" }, CancellationToken.None);
        Assert.Equal((dto.Id, "Tiêu đề"), (again.Id, again.Title));
        Assert.True(await env.Store().ExistsByDedupeKeyAsync(dto.DedupeKey, CancellationToken.None));
        Assert.False(await env.Store().ExistsByDedupeKeyAsync("khong-co", CancellationToken.None));
    }

    [Fact]
    public async Task UT_NTF_02_GetByIdAsync_tim_theo_ma_cong_khai()
    {
        await using var env = await NewEnv();
        var saved = await env.Store().UpsertAsync(env.Dto(), CancellationToken.None);
        Assert.Equal(saved.Id, (await env.Store().GetByIdAsync(saved.Id, CancellationToken.None))!.Id);
        Assert.Null(await env.Store().GetByIdAsync("NT-missing", CancellationToken.None));
    }

    [Fact]
    public async Task UT_NTF_03_ListAsync_loc_theo_tai_khoan_chua_doc_con_tro_va_gioi_han()
    {
        await using var env = await NewEnv();
        var t0 = DateTimeOffset.UtcNow.AddHours(-3);
        var oldest = await env.Store().UpsertAsync(env.Dto(at: t0), CancellationToken.None);
        var mid = await env.Store().UpsertAsync(env.Dto(at: t0.AddHours(1)), CancellationToken.None);
        var newest = await env.Store().UpsertAsync(env.Dto(at: t0.AddHours(2)), CancellationToken.None);
        await env.Store().UpsertAsync(env.Dto(account: env.Other, at: t0.AddHours(2)), CancellationToken.None);
        await env.Store().MarkReadAsync(mid.Id, env.Account, CancellationToken.None);
        var all = await env.Store().ListAsync(env.Account, null, null, null, CancellationToken.None);
        Assert.Equal([newest.Id, mid.Id, oldest.Id], all.Select(x => x.Id));
        Assert.Equal([newest.Id, oldest.Id], (await env.Store().ListAsync(env.Account, true, null, null, CancellationToken.None)).Select(x => x.Id));
        Assert.Equal([oldest.Id], (await env.Store().ListAsync(env.Account, null, 5, mid.CreatedAt.ToString("O"), CancellationToken.None)).Select(x => x.Id));
        Assert.Single(await env.Store().ListAsync(env.Account, null, 1, null, CancellationToken.None));
        Assert.Equal(3, (await env.Store().ListAsync(env.Account, null, 1000, "không phải ngày", CancellationToken.None)).Count);
        Assert.Contains(await env.Store().ListAsync(null, null, 100, null, CancellationToken.None), x => x.AccountId == env.Other);
    }

    [Fact]
    public async Task UT_NTF_04_MarkReadAsync_dung_chu_so_huu_va_dem_chua_doc()
    {
        await using var env = await NewEnv();
        var saved = await env.Store().UpsertAsync(env.Dto(), CancellationToken.None);
        await env.Store().UpsertAsync(env.Dto(), CancellationToken.None);
        Assert.Equal(2, await env.Store().CountUnreadAsync(env.Account, CancellationToken.None));
        Assert.False(await env.Store().MarkReadAsync(saved.Id, env.Other, CancellationToken.None));
        Assert.False(await env.Store().MarkReadAsync("NT-missing", env.Account, CancellationToken.None));
        Assert.True(await env.Store().MarkReadAsync(saved.Id, env.Account, CancellationToken.None));
        Assert.True(await env.Store().MarkReadAsync(saved.Id, env.Account, CancellationToken.None));
        Assert.Equal(1, await env.Store().CountUnreadAsync(env.Account, CancellationToken.None));
        Assert.NotNull((await env.Store().GetByIdAsync(saved.Id, CancellationToken.None))!.ReadAt);
    }

    [Fact]
    public async Task UT_NTF_05_GetCursorForAccountAsync_tra_moc_thoi_gian_moi_nhat_sau_thoi_diem_since()
    {
        await using var env = await NewEnv();
        var t0 = DateTimeOffset.UtcNow.AddHours(-2);
        await env.Store().UpsertAsync(env.Dto(at: t0), CancellationToken.None);
        var newer = await env.Store().UpsertAsync(env.Dto(at: t0.AddHours(1)), CancellationToken.None);
        var cursor = await env.Store().GetCursorForAccountAsync(env.Account, t0.AddMinutes(-1), CancellationToken.None);
        Assert.True(Math.Abs((DateTimeOffset.Parse(cursor!) - newer.CreatedAt).TotalMilliseconds) < 1);
        Assert.Null(await env.Store().GetCursorForAccountAsync(env.Account, DateTimeOffset.UtcNow, CancellationToken.None));
    }

    private static async Task<Appointment> Appt(Env env, int? patient, int? booker)
    {
        var db = env.Db;
        var staff = await CreateTestKit.SeedStaffAsync(db, "bs" + env.N, AccountRoles.Dentist, env.N);
        var emp = (await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == staff.Id)).Id;
        var branch = await db.Set<DCMS.Domain.Features.Scheduling.SchedulingBranch>().SingleAsync(x => x.Id == env.N);
        branch.Chairs.Add(new DCMS.Domain.Features.Scheduling.SchedulingChair { Id = env.N, BranchId = env.N, Name = "Ghế" });
        await db.SaveChangesAsync();
        var a = new Appointment
        {
            Code = "LH" + env.N, BranchId = env.N, DentistId = emp, ChairId = env.N, BookerAccountId = booker, AttendeeName = "A", PatientId = patient,
            StartAt = DateTimeOffset.UtcNow.AddDays(1), EndAt = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30), Status = "Confirmed", CreatedAt = DateTimeOffset.UtcNow,
            BookingInfo = new AppointmentBookingInfo { BookerName = "B", BookerContact = "0987654321" }
        };
        db.Appointments.Add(a); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return a;
    }

    private static async Task<PatientProfile> VerifiedLink(Env env, int account)
    {
        var p = new PatientProfile { PatientCode = "NT" + env.N, FullName = "A", NormalizedName = "A", BirthDate = new DateOnly(1990, 1, 1), Gender = PatientGenders.Male, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        env.Db.Patients.Add(p); await env.Db.SaveChangesAsync();
        env.Db.PatientAccountLinks.Add(new PatientAccountLink { RequestCode = "LK" + env.N, AccountId = account, PatientId = p.Id, DeclaredFullName = "A", Status = PatientLinkStatus.Verified, RequestedAt = DateTimeOffset.UtcNow });
        await env.Db.SaveChangesAsync(); env.Db.ChangeTracker.Clear(); return p;
    }

    [Fact]
    public async Task UT_NTF_06_CanOpenAppointmentAsync_nguoi_dat_hoac_benh_nhan_da_lien_ket_duoc_mo()
    {
        await using var env = await NewEnv();
        var p = await VerifiedLink(env, env.Other);
        var a = await Appt(env, p.Id, env.Account);
        Assert.True(await env.Store().CanOpenAppointmentAsync(env.Account, a.Code, CancellationToken.None));
        Assert.True(await env.Store().CanOpenAppointmentAsync(env.Account, a.Id.ToString(), CancellationToken.None));
        Assert.True(await env.Store().CanOpenAppointmentAsync(env.Other, a.Code, CancellationToken.None));
        Assert.False(await env.Store().CanOpenAppointmentAsync(int.MaxValue - 5, a.Code, CancellationToken.None));
        Assert.False(await env.Store().CanOpenAppointmentAsync(env.Account, "LH-khong-co", CancellationToken.None));
        Assert.False(await env.Store().CanOpenAppointmentAsync(env.Account, Guid.NewGuid().ToString(), CancellationToken.None));
    }

    // ================= NotificationService =================
    [Fact]
    public async Task UT_NTF_07_ListAsync_tra_con_tro_tiep_khi_du_gioi_han()
    {
        await using var env = await NewEnv();
        var t0 = DateTimeOffset.UtcNow.AddHours(-5);
        for (var i = 0; i < 3; i++) await env.Store().UpsertAsync(env.Dto(at: t0.AddMinutes(i)), CancellationToken.None);
        var page = await env.Svc().ListAsync(env.Account, new(null, 2, null), CancellationToken.None);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(page.Items[^1].CreatedAt.ToString("O"), page.NextCursor);
        var rest = await env.Svc().ListAsync(env.Account, new(null, 2, page.NextCursor), CancellationToken.None);
        var last = Assert.Single(rest.Items);
        Assert.Null(rest.NextCursor);
        Assert.Equal(t0, last.CreatedAt, TimeSpan.FromSeconds(1));
        var defaultLimit = await env.Svc().ListAsync(env.Account, new(null, 0, null), CancellationToken.None);
        Assert.Equal(3, defaultLimit.Items.Count);
        var tooBig = await env.Svc().ListAsync(env.Account, new(true, 500, null), CancellationToken.None);
        Assert.Equal(3, tooBig.Items.Count);
    }

    [Fact]
    public async Task UT_NTF_08_MarkReadAsync_va_MarkReadOneAsync_tach_id_da_danh_dau_va_bo_qua()
    {
        await using var env = await NewEnv();
        var mine = await env.Store().UpsertAsync(env.Dto(), CancellationToken.None);
        var theirs = await env.Store().UpsertAsync(env.Dto(account: env.Other), CancellationToken.None);
        var result = await env.Svc().MarkReadAsync(env.Account, new([mine.Id, theirs.Id, "NT-none"], env.Account), CancellationToken.None);
        Assert.Equal([mine.Id], result.Marked);
        Assert.Equal([theirs.Id, "NT-none"], result.Skipped);
        var one = await env.Svc().MarkReadOneAsync(mine.Id, env.Account, CancellationToken.None);
        Assert.Equal([mine.Id], one.Marked);
        Assert.Empty(one.Skipped);
        var denied = await env.Svc().MarkReadOneAsync(theirs.Id, env.Account, CancellationToken.None);
        Assert.Equal((0, 1), (denied.Marked.Count, denied.Skipped.Count));
        Assert.Equal(0, await env.Svc().CountUnreadAsync(env.Account, CancellationToken.None));
        Assert.Equal(1, await env.Svc().CountUnreadAsync(env.Other, CancellationToken.None));
    }

    public static TheoryData<string, string?, bool, bool> DeepLinks => new()
    {
        { "UT-NTF-09 Liên kết rỗng", " ", false, false },
        { "UT-NTF-10 Danh sách lịch hẹn", "/appointments", true, false },
        { "UT-NTF-11 Đơn thuốc khi chưa liên kết hồ sơ", "/prescriptions", false, false },
        { "UT-NTF-12 Đơn thuốc khi đã liên kết hồ sơ", "/prescriptions", true, true },
        { "UT-NTF-13 Đặt lịch khi chưa liên kết hồ sơ", "/book-appointment", false, false },
        { "UT-NTF-14 Đặt lịch khi đã liên kết hồ sơ", "/book-appointment", true, true },
        { "UT-NTF-15 Liên kết 3 đoạn không hỗ trợ", "/a/b/c", false, false },
        { "UT-NTF-16 Loại không hỗ trợ với mã không phải GUID", "/other/123", false, false },
        { "UT-NTF-17 Kết quả khám không tồn tại", "visit-results/GUID", false, false },
        { "UT-NTF-18 Đơn thuốc không tồn tại", "prescriptions/GUID", false, false },
        { "UT-NTF-19 Hồ sơ điều trị không tồn tại", "medical-records/GUID", false, false },
        { "UT-NTF-20 Loại lạ với GUID hợp lệ", "unknown/GUID", false, false },
        { "UT-NTF-21 Lịch hẹn không tồn tại", "appointments/LH-khong-co", false, false },
    };

    [Theory, MemberData(nameof(DeepLinks))]
    public async Task UT_NTF_CheckDeepLink(string caseName, string? link, bool expected, bool linked)
    {
        await using var env = await NewEnv();
        _ = caseName;
        if (linked) await VerifiedLink(env, env.Account);
        var path = link?.Replace("GUID", Guid.NewGuid().ToString());
        var result = await env.Svc().CheckDeepLinkAsync(env.Account, path!, CancellationToken.None);
        Assert.Equal(expected, result.Allowed);
        if (!expected && link != "/prescriptions") Assert.False(string.IsNullOrEmpty(result.Reason));
        else if (expected) Assert.Null(result.Reason);
    }

    [Fact]
    public async Task UT_NTF_22_CheckDeepLinkAsync_lich_hen_cua_minh_duoc_mo_cua_nguoi_khac_bi_chan()
    {
        await using var env = await NewEnv();
        var a = await Appt(env, null, env.Account);
        Assert.True((await env.Svc().CheckDeepLinkAsync(env.Account, "appointments/" + a.Code, CancellationToken.None)).Allowed);
        var denied = await env.Svc().CheckDeepLinkAsync(env.Other, "appointments/" + a.Code, CancellationToken.None);
        Assert.False(denied.Allowed);
        Assert.Equal("Bạn không có quyền mở thông báo này.", denied.Reason);
    }

    [Fact]
    public async Task UT_NTF_23_CreateAsync_mac_dinh_kenh_InApp_da_giao_va_day_realtime_den_tai_khoan()
    {
        await using var env = await NewEnv();
        var svc = env.Svc();
        var dto = await svc.CreateAsync(env.Account, null, NotificationKind.AppointmentBooked, "Đặt lịch", "Nội dung", null, null, "/appointments", null, env.Key(), CancellationToken.None);
        Assert.StartsWith("NT-", dto.Id);
        var attempt = Assert.Single(dto.Attempts);
        Assert.Equal((NotificationChannel.InApp, DispatchStatus.Delivered, 1), (attempt.Channel, attempt.Status, attempt.AttemptCount));
        Assert.Equal([("account", env.Account, "notificationCreated")], env.Hub.Sent);
    }

    [Fact]
    public async Task UT_NTF_24_CreateAsync_kenh_ngoai_InApp_cho_xu_ly_va_hen_thu_lai_sau_1_phut()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().CreateAsync(env.Account, null, NotificationKind.ReminderUpcoming, "Nhắc", "N", null, null, null,
            [NotificationChannel.InApp, NotificationChannel.Email], env.Key(), CancellationToken.None);
        var email = dto.Attempts.Single(x => x.Channel == NotificationChannel.Email);
        Assert.Equal((DispatchStatus.Pending, 0), (email.Status, email.AttemptCount));
        Assert.NotNull(email.NextRetryAt);
        Assert.Null(email.LastAttemptAt);
    }

    [Fact]
    public async Task UT_NTF_25_CreateAsync_thong_bao_cho_nhan_vien_chi_nhanh_va_trung_khoa_khong_tao_lai()
    {
        await using var env = await NewEnv();
        var key = env.Key();
        var first = await env.Svc().CreateAsync(null, env.N, NotificationKind.SupportRequestUpdate, "Hỗ trợ", "N", null, null, null, null, key, CancellationToken.None);
        Assert.Equal([("branch", env.N, "notificationCreated")], env.Hub.Sent);
        var again = await env.Svc().CreateAsync(null, env.N, NotificationKind.SupportRequestUpdate, "Khác", "N", null, null, null, null, key, CancellationToken.None);
        Assert.Equal(first.Title, again.Title);
        var neither = await env.Svc(hub: new Realtime()).CreateAsync(null, null, NotificationKind.SupportRequestUpdate, "Không đích", "N", null, null, null, null, env.Key(), CancellationToken.None);
        Assert.Empty(env.Hub.Sent);
        Assert.NotNull(neither.Id);
    }

    [Fact]
    public async Task UT_NTF_26_CreateAsync_loi_realtime_khong_lam_hong_viec_tao_thong_bao()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc(hub: new Realtime(fail: true)).CreateAsync(env.Account, null, NotificationKind.AppointmentCancelled, "Hủy", "N", null, null, null, null, env.Key(), CancellationToken.None);
        Assert.NotNull(await env.Store().GetByIdAsync(dto.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UT_NTF_27_CreateAsync_co_cau_hinh_Firebase_va_thiet_bi_day_thi_them_kenh_Push()
    {
        await using var env = await NewEnv();
        var config = new Dictionary<string, string?> { ["Firebase:ServiceAccountPath"] = "x.json" };
        var without = await env.Svc(config).CreateAsync(env.Account, null, NotificationKind.AppointmentBooked, "A", "B", null, null, null, null, env.Key(), CancellationToken.None);
        Assert.DoesNotContain(without.Attempts, x => x.Channel == NotificationChannel.Push);
        env.Db.PushDevices.Add(new PushDevice { AccountId = env.Account, Token = "tok" + env.N, Platform = "android" });
        await env.Db.SaveChangesAsync();
        var with = await env.Svc(config).CreateAsync(env.Account, null, NotificationKind.AppointmentBooked, "A", "B", null, null, null, null, env.Key(), CancellationToken.None);
        Assert.Contains(with.Attempts, x => x.Channel == NotificationChannel.Push && x.Status == DispatchStatus.Pending);
        var already = await env.Svc(config).CreateAsync(env.Account, null, NotificationKind.AppointmentBooked, "A", "B", null, null, null, [NotificationChannel.Push], env.Key(), CancellationToken.None);
        Assert.Single(already.Attempts.Where(x => x.Channel == NotificationChannel.Push));
    }
}
