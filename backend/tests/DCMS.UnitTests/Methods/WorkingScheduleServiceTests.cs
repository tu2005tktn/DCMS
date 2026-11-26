using DCMS.Application.Common;
using DCMS.Application.Features.Scheduling;
using DCMS.Application.Features.Settings;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Appointments;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Scheduling;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of WorkingScheduleService (lịch làm việc và khả dụng): OptionsAsync, PublicOptionsAsync, GetAsync, PreviewAsync,
/// PublishAsync, QueryAsync, QueryForAppointmentAsync, PreviewAvailabilityAsync, ValidateReceptionAsync, CheckInTransactionAsync and
/// the recurring-template methods (GetRecurringAsync, PreviewRecurringAsync, PublishRecurringAsync, PreviewRecurringAvailabilityAsync).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkingScheduleServiceTests
{
    // Thursday 09:00 on 10/01/2030 clinic time; Monday 14/01/2030 is the first future week.
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2030, 1, 14);
    private static readonly DateOnly NextMonday = new(2030, 1, 21);

    private sealed class Access(bool owner, bool assigned, int[] branches, params string[] permissions) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, assigned, [], permissions, branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private static SchedulePeriod Work(int day = 0, int start = 480, int end = 720) => new(day, start, end, "Work");
    private static SchedulePeriod[] Week5(int start = 480, int end = 720) => Enumerable.Range(0, 5).Select(d => Work(d, start, end)).ToArray();

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Empty, Dentist, Dentist2, Assistant, Manager, Owner, Elsewhere, Customer;
        public required int DEmp, D2Emp, AEmp, MEmp, OwEmp, ElEmp, ChairId, ServiceId, PublicServiceId;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public WorkingScheduleService Svc(bool owner = false, int[]? branches = null, string[]? perms = null, DateTimeOffset? now = null) =>
            new(Db, new TestClock(now ?? Now), new Access(owner, true, branches ?? [N], perms ?? []));

        public ScheduleCommand Cmd(int? dentist = null, DateOnly? week = null, int version = 0, SchedulePeriod[]? intervals = null, Guid? key = null) =>
            new(N, dentist ?? DEmp, week ?? Monday, version, intervals ?? Week5(), key ?? Guid.NewGuid());
        public Task<PublishResult> Publish(int actor, ScheduleCommand cmd, bool owner = false) { Db.ChangeTracker.Clear(); return Svc(owner).PublishAsync(actor, cmd, CancellationToken.None); }
        public async Task<PublishResult> Standard(int? dentist = null, DateOnly? week = null, int version = 0) => await Publish(dentist is null || dentist == DEmp ? Dentist : Manager, Cmd(dentist, week, version));

        public async Task<Appointment> Appt(int start = 540, int minutes = 30, int? dentist = null, int? chair = null, string status = "Confirmed", DateOnly? date = null, int? patient = null)
        {
            var day = date ?? Monday;
            var s = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).AddMinutes(start).ToUniversalTime();
            var a = new Appointment
            {
                Code = "LH" + CreateTestKit.NextId(), BranchId = N, DentistId = dentist ?? DEmp, ChairId = chair ?? ChairId, BookerAccountId = Customer, AttendeeName = "Khách",
                PatientId = patient, StartAt = s, EndAt = s.AddMinutes(minutes), Status = status, Version = 1, CreatedAt = Now,
                BookingInfo = new AppointmentBookingInfo { BookerName = "Người đặt", BookerContact = "0987654321" }
            };
            Db.Appointments.Add(a); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); return a;
        }
        public AvailabilityQuery Q(DateOnly? date = null, int? dentist = null, int duration = 30, int? service = null, int? assistant = null) =>
            new(N, dentist ?? DEmp, service, date ?? Monday, duration, assistant);
        public Task<AvailabilityResult> Query(AvailabilityQuery q, int? staff = null, WorkingScheduleService? svc = null, bool pub = false) => (svc ?? Svc()).QueryAsync(q, staff ?? (pub ? null : Dentist), CancellationToken.None);
        public RecurringCommand Rec(string kind = "Template", DateOnly? date = null, int version = 0, BranchPeriod[]? intervals = null, string reason = "", Guid? key = null, int? dentist = null) =>
            new(dentist ?? DEmp, date ?? Monday, kind, version, intervals ?? Enumerable.Range(0, 5).Select(d => new BranchPeriod(N, d, 480, 720, "Work")).ToArray(), key ?? Guid.NewGuid(), reason);
    }

    private static async Task<(ApplicationUser User, int Emp)> Staff(DcmsDbContext db, string name, string role, int branch, bool manage = false)
    {
        var user = await CreateTestKit.SeedStaffAsync(db, name, role, branch);
        var emp = (await db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == user.Id)).Id;
        if (manage)
        {
            db.ChangeTracker.Clear(); (await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == branch)).Members.Single(m => m.EmployeeId == emp).CanManageSchedules = true;
            await db.SaveChangesAsync();
        }
        return (user, emp);
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
        var n = CreateTestKit.NextId(); var other = CreateTestKit.NextId(); var empty = CreateTestKit.NextId();
        var dentist = await Staff(db, "bs" + n, AccountRoles.Dentist, n);
        var dentist2 = await Staff(db, "bm" + n, AccountRoles.Dentist, n);
        var assistant = await Staff(db, "tl" + n, AccountRoles.ClinicAssistant, n);
        var manager = await Staff(db, "ql" + n, AccountRoles.ClinicAssistant, n, manage: true);
        var owner = await Staff(db, "ow" + n, AccountRoles.ClinicOwner, n);
        var elsewhere = await Staff(db, "el" + n, AccountRoles.Dentist, other);
        await Staff(db, "em" + n, AccountRoles.ClinicAssistant, empty);
        var customer = new ApplicationUser { UserName = "cu" + n, FullName = "Khách", AccountType = "Public", IsActive = true };
        db.Users.Add(customer);
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        var chair = new SchedulingChair { Id = n, BranchId = n, Name = "Ghế " + n };
        branch.Chairs.Add(chair);
        var service = new SchedulingService { BranchId = n, Name = "Trám răng", PublicDurationMinutes = 30, IsInitialExamination = false };
        var exam = new SchedulingService { BranchId = n, Name = "Khám tổng quát", PublicDurationMinutes = 30, IsInitialExamination = true, PublicBookable = true };
        db.Set<SchedulingService>().AddRange(service, exam);
        await db.SaveChangesAsync();
        return new Env
        {
            Pg = pg, N = n, Other = other, Empty = empty, Dentist = dentist.User.Id, Dentist2 = dentist2.User.Id, Assistant = assistant.User.Id, Manager = manager.User.Id,
            Owner = owner.User.Id, Elsewhere = elsewhere.User.Id, Customer = customer.Id, DEmp = dentist.Emp, D2Emp = dentist2.Emp, AEmp = assistant.Emp, MEmp = manager.Emp,
            OwEmp = owner.Emp, ElEmp = elsewhere.Emp, ChairId = chair.Id, ServiceId = service.Id, PublicServiceId = exam.Id
        };
    }

    private static async Task Rejects(Func<Task> call, int status, string code)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((status, code), (error.Status, error.Response.Code));
    }

    // ================= OptionsAsync / PublicOptionsAsync =================
    [Fact]
    public async Task UT_SCH_01_OptionsAsync_bac_si_thay_chi_nhanh_cua_minh_voi_chinh_minh_la_Self()
    {
        await using var env = await NewEnv();
        var options = await env.Svc().OptionsAsync(env.Dentist, CancellationToken.None);
        Assert.Equal([env.N], options.Branches.Select(x => x.Id));
        var self = options.Employees.Single(x => x.Id == env.DEmp);
        Assert.True(self.Self);
        Assert.True(self.CanDentist);
        Assert.False(self.CanEdit);
        Assert.Contains(options.Chairs, c => c.Id == env.ChairId);
        Assert.Contains(options.Services, s => s.Id == env.ServiceId);
    }

    [Fact]
    public async Task UT_SCH_02_OptionsAsync_co_quyen_working_schedules_manage_thi_CanEdit()
    {
        await using var env = await NewEnv();
        var options = await env.Svc(perms: ["working-schedules.manage"]).OptionsAsync(env.Dentist, CancellationToken.None);
        Assert.True(options.Employees.Single(x => x.Id == env.DEmp).CanEdit);
        Assert.False(options.Employees.Single(x => x.Id == env.D2Emp).CanEdit);
        var manager = await env.Svc(perms: ["working-schedules.manage"]).OptionsAsync(env.Manager, CancellationToken.None);
        Assert.All(manager.Employees, e => Assert.True(e.CanEdit));
    }

    [Fact]
    public async Task UT_SCH_03_OptionsAsync_tro_ly_khong_quan_ly_lich_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().OptionsAsync(env.Assistant, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc().OptionsAsync(env.Customer, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_SCH_04_OptionsAsync_chu_phong_kham_thay_moi_chi_nhanh_dang_hoat_dong()
    {
        await using var env = await NewEnv();
        var options = await env.Svc(owner: true, branches: []).OptionsAsync(env.Owner, CancellationToken.None);
        Assert.Contains(options.Branches, b => b.Id == env.N);
        Assert.Contains(options.Branches, b => b.Id == env.Other);
    }

    [Fact]
    public async Task UT_SCH_05_PublicOptionsAsync_chi_tra_bac_si_va_dich_vu_kham_ban_dau_cong_khai()
    {
        await using var env = await NewEnv();
        var options = await env.Svc().PublicOptionsAsync(env.N, CancellationToken.None);
        Assert.Equal([env.N], options.Branches.Select(x => x.Id));
        Assert.Equal(new[] { env.DEmp, env.D2Emp, env.OwEmp }.Order(), options.Employees.Select(x => x.Id).Order());
        Assert.All(options.Employees, e => Assert.Equal((true, false, false), (e.CanDentist, e.CanEdit, e.Self)));
        Assert.Empty(options.Chairs);
        var exam = Assert.Single(options.Services);
        Assert.Equal((env.PublicServiceId, "Khám", 30), (exam.Id, exam.Name, exam.PublicDurationMinutes));
        var all = await env.Svc().PublicOptionsAsync(0, CancellationToken.None);
        Assert.Contains(all.Branches, b => b.Id == env.Other);
        Assert.Empty((await env.Svc().PublicOptionsAsync(env.Empty, CancellationToken.None)).Employees);
    }

    // ================= GetAsync =================
    [Fact]
    public async Task UT_SCH_06_GetAsync_tuan_chua_xuat_ban_la_Unpublished_phien_ban_0()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().GetAsync(env.Dentist, env.N, env.DEmp, Monday, CancellationToken.None);
        Assert.Equal(("Unpublished", 0, 0), (dto.Status, dto.Version, dto.Intervals.Length));
        Assert.Null(dto.Id);
    }

    [Fact]
    public async Task UT_SCH_07_GetAsync_tuan_da_xuat_ban_tra_ve_cac_khung_gio()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var dto = await env.Svc().GetAsync(env.Dentist, env.N, env.DEmp, Monday, CancellationToken.None);
        Assert.Equal(("Published", 1, 5, env.Dentist), (dto.Status, dto.Version, dto.Intervals.Length, dto.PublishedBy ?? 0));
        Assert.Equal(Now, dto.PublishedAt);
    }

    public static TheoryData<string, string, int, string> BadGets => new()
    {
        { "UT-SCH-08 Ngày không phải thứ Hai", "notMonday", 400, "MSG110" },
        { "UT-SCH-09 Chi nhánh ngoài phạm vi gán", "otherBranch", 403, "MSG06" },
        { "UT-SCH-10 Xem lịch của bác sĩ khác khi không có quyền quản lý", "otherDentist", 403, "MSG06" },
        { "UT-SCH-11 Nhân sự không thuộc chi nhánh", "notMember", 403, "MSG06" },
    };

    [Theory, MemberData(nameof(BadGets))]
    public async Task UT_SCH_Get_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var svc = env.Svc();
        Func<Task> call = kind switch
        {
            "notMonday" => () => svc.GetAsync(env.Dentist, env.N, env.DEmp, Monday.AddDays(1), CancellationToken.None),
            "otherBranch" => () => svc.GetAsync(env.Dentist, env.Other, env.DEmp, Monday, CancellationToken.None),
            "otherDentist" => () => svc.GetAsync(env.Dentist, env.N, env.D2Emp, Monday, CancellationToken.None),
            _ => () => env.Svc(branches: [env.N, env.Other]).GetAsync(env.Manager, env.Other, env.ElEmp, Monday, CancellationToken.None),
        };
        await Rejects(call, status, code);
    }

    [Fact]
    public async Task UT_SCH_12_GetAsync_nguoi_quan_ly_xem_duoc_lich_dong_nghiep_va_chu_phong_kham_xem_moi_chi_nhanh()
    {
        await using var env = await NewEnv();
        Assert.Equal("Unpublished", (await env.Svc().GetAsync(env.Manager, env.N, env.D2Emp, Monday, CancellationToken.None)).Status);
        Assert.Equal("Unpublished", (await env.Svc(owner: true, branches: []).GetAsync(env.Owner, env.N, env.D2Emp, Monday, CancellationToken.None)).Status);
        await Rejects(() => env.Svc(owner: true, branches: []).GetAsync(env.Owner, env.N, env.ElEmp, Monday, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc(owner: true, branches: []).GetAsync(env.Owner, env.Empty + 1000, env.D2Emp, Monday, CancellationToken.None), 403, "MSG06");
    }

    // ================= PublishAsync =================
    [Fact]
    public async Task UT_SCH_13_PublishAsync_lan_dau_tao_tuan_phien_ban_1_va_thong_bao_MSG39()
    {
        await using var env = await NewEnv();
        var result = await env.Standard();
        Assert.Equal(("Published", 1, 5), (result.Schedule.Status, result.Schedule.Version, result.Schedule.Intervals.Length));
        Assert.Equal("MSG39", result.Notification.Code);
    }

    [Fact]
    public async Task UT_SCH_14_PublishAsync_lan_hai_thay_khung_gio_va_tang_phien_ban()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var second = await env.Publish(env.Dentist, env.Cmd(version: 1, intervals: [Work(0, 540, 600), new(0, 555, 570, "Break", "Nghỉ")]));
        Assert.Equal(2, second.Schedule.Version);
        Assert.Equal(2, second.Schedule.Intervals.Length);
        env.Db.ChangeTracker.Clear();
        var again = await env.Svc().GetAsync(env.Dentist, env.N, env.DEmp, Monday, CancellationToken.None);
        Assert.Equal((2, 2), (again.Version, again.Intervals.Length));
    }

    public static TheoryData<string, string, int, string> BadPublishes => new()
    {
        { "UT-SCH-15 Mã yêu cầu rỗng", "noKey", 400, "MSG110" },
        { "UT-SCH-16 Danh sách khung giờ null", "nullIntervals", 400, "MSG110" },
        { "UT-SCH-17 Ngày bắt đầu không phải thứ Hai", "notMonday", 400, "MSG110" },
        { "UT-SCH-18 Giờ không chia hết cho 15 phút", "step", 400, "MSG110" },
        { "UT-SCH-19 Giờ bắt đầu không nhỏ hơn giờ kết thúc", "reversed", 400, "MSG110" },
        { "UT-SCH-20 Hai khung làm việc chồng nhau", "overlap", 400, "MSG110" },
        { "UT-SCH-21 Giờ nghỉ không có lý do", "breakNoReason", 400, "MSG110" },
        { "UT-SCH-22 Giờ nghỉ nằm ngoài giờ làm", "breakOutside", 400, "MSG110" },
        { "UT-SCH-23 Loại khung giờ lạ", "badKind", 400, "MSG110" },
        { "UT-SCH-24 Phiên bản kỳ vọng sai", "stale", 409, "MSG111" },
    };

    [Theory, MemberData(nameof(BadPublishes))]
    public async Task UT_SCH_Publish_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var cmd = kind switch
        {
            "noKey" => env.Cmd(key: Guid.Empty),
            "nullIntervals" => env.Cmd() with { Intervals = null! },
            "notMonday" => env.Cmd(week: Monday.AddDays(2)),
            "step" => env.Cmd(intervals: [Work(0, 485, 600)]),
            "reversed" => env.Cmd(intervals: [Work(0, 600, 600)]),
            "overlap" => env.Cmd(intervals: [Work(0, 480, 600), Work(0, 570, 660)]),
            "breakNoReason" => env.Cmd(intervals: [Work(0, 480, 720), new(0, 540, 570, "Break", "")]),
            "breakOutside" => env.Cmd(intervals: [Work(0, 480, 600), new(0, 660, 690, "Break", "Nghỉ")]),
            "badKind" => env.Cmd(intervals: [new(0, 480, 600, "Other")]),
            _ => env.Cmd(version: 5),
        };
        await Rejects(() => env.Publish(env.Dentist, cmd), status, code);
    }

    [Fact]
    public async Task UT_SCH_25_PublishAsync_gui_lai_cung_khoa_tra_ve_ket_qua_cu_khac_noi_dung_la_409()
    {
        await using var env = await NewEnv();
        var cmd = env.Cmd();
        var first = await env.Publish(env.Dentist, cmd);
        var again = await env.Publish(env.Dentist, cmd);
        Assert.Equal((first.Schedule.Version, first.Schedule.Id), (again.Schedule.Version, again.Schedule.Id));
        await Rejects(() => env.Publish(env.Dentist, cmd with { Intervals = [Work(1, 480, 540)] }), 409, "MSG111");
    }

    [Fact]
    public async Task UT_SCH_26_PublishAsync_khong_du_quyen_hoac_ngoai_chi_nhanh_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Publish(env.Dentist, env.Cmd(dentist: env.D2Emp)), 403, "MSG06");
        await Rejects(() => env.Publish(env.Assistant, env.Cmd(dentist: env.AEmp)), 403, "MSG06");
        await Rejects(() => env.Publish(env.Elsewhere, env.Cmd(dentist: env.ElEmp)), 403, "MSG06");
        var byManager = await env.Publish(env.Manager, env.Cmd(dentist: env.D2Emp));
        Assert.Equal(1, byManager.Schedule.Version);
        var byOwner = await env.Publish(env.Owner, env.Cmd(dentist: env.OwEmp), owner: true);
        Assert.Equal(1, byOwner.Schedule.Version);
    }

    [Fact]
    public async Task UT_SCH_27_PublishAsync_lich_hen_da_xac_nhan_bi_anh_huong_la_409_MSG38()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Appt(start: 540);
        var cmd = env.Cmd(version: 1, intervals: [Work(0, 600, 720)]);
        await Rejects(() => env.Publish(env.Dentist, cmd), 409, "MSG38");
        var preview = await env.Svc().PreviewAsync(env.Dentist, cmd, CancellationToken.None);
        var affected = Assert.Single(preview.AffectedAppointments);
        Assert.Equal("Confirmed", affected.Status);
    }

    [Fact]
    public async Task UT_SCH_28_PreviewAsync_khong_anh_huong_khi_lich_hen_van_nam_trong_ca()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Appt(start: 540);
        var preview = await env.Svc().PreviewAsync(env.Dentist, env.Cmd(version: 1, intervals: Week5(480, 780)), CancellationToken.None);
        Assert.Empty(preview.AffectedAppointments);
    }

    [Fact]
    public async Task UT_SCH_29_PublishAsync_khi_da_co_lich_dinh_ky_thi_409()
    {
        await using var env = await NewEnv();
        await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec(), CancellationToken.None);
        await Rejects(() => env.Publish(env.Dentist, env.Cmd()), 409, "MSG111");
    }

    // ================= QueryAsync / ValidateQuery / Evaluate =================
    [Fact]
    public async Task UT_SCH_30_QueryAsync_tra_ve_khung_gio_moi_15_phut_trong_ca()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var result = await env.Query(env.Q(), env.Dentist);
        Assert.Equal(("Asia/Ho_Chi_Minh", Monday, 15), (result.TimeZone, result.Date, result.Slots.Length));
        var first = result.Slots[0];
        Assert.Equal((new DateTimeOffset(2030, 1, 14, 8, 0, 0, TimeSpan.FromHours(7)), env.ChairId, env.DEmp, 1), (first.Start, first.ChairId, first.DentistId, first.ScheduleVersion));
        Assert.Equal(first.Start.AddMinutes(30), first.End);
        Assert.Equal(new DateTimeOffset(2030, 1, 14, 11, 30, 0, TimeSpan.FromHours(7)), result.Slots[^1].Start);
    }

    [Fact]
    public async Task UT_SCH_31_QueryAsync_bo_cac_khung_da_co_lich_hen_trung_bac_si_hoac_ghe()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Standard(env.D2Emp);
        await env.Appt(start: 540, dentist: env.DEmp);
        var dentist = await env.Query(env.Q());
        Assert.Equal(12, dentist.Slots.Length);
        // The other dentist is free but the only chair is taken in the same minutes.
        var other = await env.Query(env.Q(dentist: env.D2Emp));
        Assert.Equal(12, other.Slots.Length);
        Assert.DoesNotContain(other.Slots, s => s.Start.Hour == 9 && s.Start.Minute is 0 or 15);
    }

    [Fact]
    public async Task UT_SCH_32_QueryAsync_ngay_khong_co_ca_hoac_chua_xuat_ban_tra_ve_rong()
    {
        await using var env = await NewEnv();
        await env.Standard();
        Assert.Empty((await env.Query(env.Q(date: Monday.AddDays(5)))).Slots);
        Assert.Empty((await env.Query(env.Q(date: NextMonday))).Slots);
    }

    [Fact]
    public async Task UT_SCH_33_QueryAsync_khong_tra_khung_gio_da_qua()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var result = await env.Svc(now: new DateTimeOffset(2030, 1, 14, 3, 0, 0, TimeSpan.Zero)).QueryAsync(env.Q(), env.Dentist, CancellationToken.None);
        Assert.All(result.Slots, s => Assert.True(s.Start > new DateTimeOffset(2030, 1, 14, 3, 0, 0, TimeSpan.Zero)));
        Assert.True(result.Slots.Length < 15);
    }

    public static TheoryData<string, string> BadQueries => new()
    {
        { "UT-SCH-34 Thời lượng 0 phút", "duration0" },
        { "UT-SCH-35 Thời lượng 481 phút", "duration481" },
        { "UT-SCH-36 Năm 1999", "year1999" },
        { "UT-SCH-37 Trợ lý trùng bác sĩ", "assistantIsDentist" },
        { "UT-SCH-38 Bác sĩ không thuộc chi nhánh", "dentistElsewhere" },
        { "UT-SCH-39 Nhân sự không phải bác sĩ", "notDentist" },
        { "UT-SCH-40 Trợ lý không thuộc chi nhánh", "assistantElsewhere" },
        { "UT-SCH-41 Dịch vụ không tồn tại", "noService" },
    };

    [Theory, MemberData(nameof(BadQueries))]
    public async Task UT_SCH_Query_validation(string caseName, string kind)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var q = kind switch
        {
            "duration0" => env.Q(duration: 0),
            "duration481" => env.Q(duration: 481),
            "year1999" => env.Q(date: new DateOnly(1999, 1, 4)),
            "assistantIsDentist" => env.Q(assistant: env.DEmp),
            "dentistElsewhere" => env.Q(dentist: env.ElEmp),
            "notDentist" => env.Q(dentist: env.AEmp),
            "assistantElsewhere" => env.Q(assistant: env.ElEmp),
            _ => env.Q(service: int.MaxValue - 3),
        };
        await Rejects(() => env.Query(q), 400, "MSG110");
    }

    [Fact]
    public async Task UT_SCH_42_QueryAsync_cong_khai_chi_nhan_dich_vu_kham_ban_dau_dung_thoi_luong()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var ok = await env.Query(env.Q(service: env.PublicServiceId), pub: true);
        Assert.Equal(15, ok.Slots.Length);
        await Rejects(() => env.Query(env.Q(service: env.ServiceId), pub: true), 400, "MSG110");
        await Rejects(() => env.Query(env.Q(service: env.PublicServiceId, duration: 45), pub: true), 400, "MSG110");
        await Rejects(() => env.Query(env.Q(), pub: true), 400, "MSG110");
    }

    [Fact]
    public async Task UT_SCH_43_QueryAsync_khach_cong_khai_khong_duoc_dat_trong_vong_60_phut()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var svc = env.Svc(now: new DateTimeOffset(2030, 1, 14, 0, 30, 0, TimeSpan.Zero)); // 07:30 clinic
        var result = await svc.QueryAsync(env.Q(service: env.PublicServiceId), null, CancellationToken.None);
        Assert.Empty(result.Slots.Where(s => s.Start < new DateTimeOffset(2030, 1, 14, 8, 30, 0, TimeSpan.FromHours(7))));
        Assert.Contains(result.Slots, s => s.Start == new DateTimeOffset(2030, 1, 14, 8, 30, 0, TimeSpan.FromHours(7)));
    }

    [Fact]
    public async Task UT_SCH_44_QueryAsync_nhan_vien_khong_thuoc_chi_nhanh_la_403()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await Rejects(() => env.Query(env.Q(), env.Elsewhere), 403, "MSG06");
    }

    [Fact]
    public async Task UT_SCH_45_QueryAsync_voi_tro_ly_chi_giu_khung_ca_hai_cung_ranh()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Publish(env.Manager, env.Cmd(dentist: env.MEmp, intervals: [Work(0, 480, 540)]));
        var result = await env.Query(env.Q(assistant: env.MEmp));
        Assert.All(result.Slots, s => Assert.True(s.Start.Hour * 60 + s.Start.Minute + 30 <= 540));
        Assert.Equal((env.MEmp, 1), (result.Slots[0].AssistantId, result.Slots[0].AssistantScheduleVersion));
    }

    // ================= QueryAnyDentist =================
    [Fact]
    public async Task UT_SCH_46_QueryAsync_khong_chon_bac_si_gop_khung_gio_cua_cac_bac_si_va_chon_nguoi_it_viec_nhat()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Standard(env.D2Emp);
        await env.Appt(start: 480, dentist: env.DEmp, chair: env.ChairId);
        var result = await env.Query(env.Q(dentist: 0));
        // 15 start times; the 08:00 and 08:15 starts overlap the booked chair so they are not offered.
        Assert.Equal(13, result.Slots.Length);
        Assert.Equal(result.Slots.Length, result.Slots.Select(s => s.Start).Distinct().Count());
        Assert.All(result.Slots, s => Assert.Equal(env.D2Emp, s.DentistId));
    }

    [Fact]
    public async Task UT_SCH_47_QueryAsync_khong_chon_bac_si_voi_tro_ly_la_MSG110_va_chi_nhanh_khong_co_bac_si_tra_rong()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Query(env.Q(dentist: 0, assistant: env.AEmp)), 400, "MSG110");
        var empty = await env.Query(new(env.Empty, 0, null, Monday, 30), pub: true);
        Assert.Empty(empty.Slots);
    }

    // ================= QueryForAppointmentAsync =================
    [Fact]
    public async Task UT_SCH_48_QueryForAppointmentAsync_bo_qua_lich_hen_dang_doi_va_tinh_ca_khung_da_qua()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var booked = await env.Appt(start: 540);
        var withSelf = await env.Svc().QueryForAppointmentAsync(env.Q(), booked.Id, null, CancellationToken.None);
        Assert.Equal(15, withSelf.Slots.Length);
        var withoutExclusion = await env.Svc().QueryForAppointmentAsync(env.Q(), Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal(12, withoutExclusion.Slots.Length);
        await Rejects(() => env.Svc().QueryForAppointmentAsync(env.Q(duration: 0), booked.Id, null, CancellationToken.None), 400, "MSG110");
    }

    [Fact]
    public async Task UT_SCH_49_QueryForAppointmentAsync_loai_khung_trung_lich_hen_cua_cung_benh_nhan()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Standard(env.D2Emp);
        var patient = new DCMS.Domain.Features.Patients.PatientProfile { PatientCode = "BN" + env.N, FullName = "A", NormalizedName = "A", BirthDate = new DateOnly(1990, 1, 1), Gender = DCMS.Domain.Features.Patients.PatientGenders.Male, CreatedAt = Now, UpdatedAt = Now };
        env.Db.Patients.Add(patient); await env.Db.SaveChangesAsync();
        await env.Appt(start: 540, dentist: env.D2Emp, patient: patient.Id);
        var same = await env.Svc().QueryForAppointmentAsync(env.Q(), Guid.NewGuid(), patient.Id, CancellationToken.None);
        var anon = await env.Svc().QueryForAppointmentAsync(env.Q(), Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal(12, same.Slots.Length);
        Assert.Equal(12, anon.Slots.Length);
    }

    // ================= PreviewAvailabilityAsync =================
    [Fact]
    public async Task UT_SCH_50_PreviewAvailabilityAsync_dung_ban_nhap_thay_cho_lich_da_xuat_ban()
    {
        await using var env = await NewEnv();
        var draft = env.Cmd(intervals: [Work(0, 480, 540)]);
        var result = await env.Svc().PreviewAvailabilityAsync(env.Dentist, new(draft, env.Q()), CancellationToken.None);
        Assert.Equal(3, result.Slots.Length);
    }

    [Fact]
    public async Task UT_SCH_51_PreviewAvailabilityAsync_truy_van_khong_khop_ban_nhap_la_MSG110()
    {
        await using var env = await NewEnv();
        var draft = env.Cmd();
        await Rejects(() => env.Svc().PreviewAvailabilityAsync(env.Dentist, new(draft, env.Q(date: NextMonday)), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().PreviewAvailabilityAsync(env.Dentist, new(draft, env.Q() with { BranchId = env.Other }), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().PreviewAvailabilityAsync(env.Manager, new(draft, env.Q(dentist: env.D2Emp)), CancellationToken.None), 400, "MSG110");
    }

    // ================= ValidateReceptionAsync / CheckInTransactionAsync =================
    [Fact]
    public async Task UT_SCH_52_ValidateReceptionAsync_can_giao_dich_dang_mo()
    {
        await using var env = await NewEnv();
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Svc().ValidateReceptionAsync(Guid.NewGuid(), 1, CancellationToken.None));
    }

    [Fact]
    public async Task UT_SCH_53_ValidateReceptionAsync_lich_hen_hop_le_khong_nem_loi()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var a = await env.Appt(start: 540);
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        await env.Svc().ValidateReceptionAsync(a.Id, int.MaxValue - 7, CancellationToken.None);
    }

    [Fact]
    public async Task UT_SCH_54_ValidateReceptionAsync_lich_hen_khong_ton_tai_hoac_khong_con_Confirmed_la_409_MSG47()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var cancelled = await env.Appt(start: 540, status: "Cancelled");
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        await Rejects(() => env.Svc().ValidateReceptionAsync(Guid.NewGuid(), 1, CancellationToken.None), 409, "MSG47");
        await Rejects(() => env.Svc().ValidateReceptionAsync(cancelled.Id, 1, CancellationToken.None), 409, "MSG47");
    }

    [Fact]
    public async Task UT_SCH_55_ValidateReceptionAsync_ngoai_ca_lam_viec_la_409_MSG110()
    {
        await using var env = await NewEnv();
        var a = await env.Appt(start: 540); // no schedule published
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        await Rejects(() => env.Svc().ValidateReceptionAsync(a.Id, 1, CancellationToken.None), 409, "MSG110");
    }

    [Fact]
    public async Task UT_SCH_56_CheckInTransactionAsync_khong_co_giao_dich_la_loi_lap_trinh()
    {
        await using var env = await NewEnv();
        var request = new ResourceCheck(env.N, env.DEmp, null, new DateTimeOffset(2030, 1, 14, 9, 0, 0, TimeSpan.FromHours(7)), 30, null, null, null, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Svc().CheckInTransactionAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task UT_SCH_57_CheckInTransactionAsync_tra_ve_khung_phu_hop_va_chan_phien_ban_cu()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var start = new DateTimeOffset(2030, 1, 14, 9, 0, 0, TimeSpan.FromHours(7));
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        var ok = await env.Svc().CheckInTransactionAsync(new(env.N, env.DEmp, null, start, 30, null, null, null, 1), CancellationToken.None);
        Assert.Equal((start, env.ChairId, env.DEmp), (ok.Start, ok.ChairId, ok.DentistId));
        await Rejects(() => env.Svc().CheckInTransactionAsync(new(env.N, env.DEmp, null, start, 30, null, null, null, 9), CancellationToken.None), 409, "MSG111");
        await Rejects(() => env.Svc().CheckInTransactionAsync(new(env.N, env.DEmp, null, start, 30, null, null, null, 1, BookingSettingsVersion: 99), CancellationToken.None), 409, "MSG111");
        var version = (await env.Db.Set<SchedulingBranch>().AsNoTracking().SingleAsync(x => x.Id == env.N)).Version;
        var sameVersion = await env.Svc().CheckInTransactionAsync(new(env.N, env.DEmp, null, start, 30, null, null, null, 1, BookingSettingsVersion: version), CancellationToken.None);
        Assert.Equal(version, sameVersion.BookingSettingsVersion);
    }

    [Fact]
    public async Task UT_SCH_58_CheckInTransactionAsync_khung_da_co_lich_hen_la_409_MSG27()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await env.Appt(start: 540);
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(env.N, env.DEmp, null, new DateTimeOffset(2030, 1, 14, 9, 0, 0, TimeSpan.FromHours(7)), 30, null, null, null, 1);
        await Rejects(() => env.Svc().CheckInTransactionAsync(request, CancellationToken.None), 409, "MSG27");
        await Rejects(() => env.Svc().CheckInTransactionAsync(request with { Start = new DateTimeOffset(2030, 1, 14, 11, 45, 0, TimeSpan.FromHours(7)) }, CancellationToken.None), 409, "MSG26");
        await Rejects(() => env.Svc().CheckInTransactionAsync(request with { Start = new DateTimeOffset(2030, 1, 14, 9, 7, 0, TimeSpan.FromHours(7)), DentistId = env.DEmp }, CancellationToken.None), 409, "MSG110");
    }

    [Fact]
    public async Task UT_SCH_59_CheckInTransactionAsync_ghe_da_bi_chiem_la_409_MSG28()
    {
        await using var env = await NewEnv();
        await env.Standard(env.D2Emp);
        await env.Appt(start: 540, dentist: env.DEmp);
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(env.N, env.D2Emp, null, new DateTimeOffset(2030, 1, 14, 9, 0, 0, TimeSpan.FromHours(7)), 30, null, null, null, 1);
        await Rejects(() => env.Svc().CheckInTransactionAsync(request, CancellationToken.None), 409, "MSG28");
    }

    [Fact]
    public async Task UT_SCH_60_CheckInTransactionAsync_khach_cong_khai_phai_dat_truoc_it_nhat_60_phut()
    {
        await using var env = await NewEnv();
        await env.Standard();
        await using var tx = await env.Db.Database.BeginTransactionAsync();
        var request = new ResourceCheck(env.N, env.DEmp, env.PublicServiceId, new DateTimeOffset(2030, 1, 14, 9, 0, 0, TimeSpan.FromHours(7)), 30, null, null, null, 1, Public: true);
        Assert.Equal(env.DEmp, (await env.Svc().CheckInTransactionAsync(request, CancellationToken.None)).DentistId);
        var late = env.Svc(now: new DateTimeOffset(2030, 1, 14, 1, 30, 0, TimeSpan.Zero)); // 08:30 clinic, 30 minutes before 09:00
        await Rejects(() => late.CheckInTransactionAsync(request, CancellationToken.None), 409, "MSG24");
    }

    // ================= Recurring schedules =================
    [Fact]
    public async Task UT_SCH_61_GetRecurringAsync_chua_co_mau_tra_ve_rong_phien_ban_0()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().GetRecurringAsync(env.Dentist, env.DEmp, Monday, CancellationToken.None);
        Assert.Equal((env.DEmp, 0), (dto.DentistId, dto.Version));
        Assert.Null(dto.Template);
        Assert.Null(dto.Exception);
        Assert.Empty(dto.FutureChanges);
        Assert.Empty(dto.LegacyWeek);
    }

    [Fact]
    public async Task UT_SCH_62_GetRecurringAsync_tuan_cu_xuat_ban_theo_tung_tuan_hien_trong_Legacy()
    {
        await using var env = await NewEnv();
        await env.Standard();
        var dto = await env.Svc().GetRecurringAsync(env.Dentist, env.DEmp, Monday, CancellationToken.None);
        var legacy = Assert.Single(dto.LegacyWeek);
        Assert.Equal((env.N, Monday), (legacy.BranchId, legacy.WeekStart));
        Assert.Equal(1, dto.Version);
    }

    [Fact]
    public async Task UT_SCH_63_GetRecurringAsync_nam_ngoai_2000_2100_la_MSG110_va_nhan_su_khong_co_chi_nhanh_la_403()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Svc().GetRecurringAsync(env.Dentist, env.DEmp, new DateOnly(1999, 1, 4), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().GetRecurringAsync(env.Dentist, int.MaxValue - 2, Monday, CancellationToken.None), 403, "MSG06");
        await Rejects(() => env.Svc().GetRecurringAsync(env.Dentist, env.D2Emp, Monday, CancellationToken.None), 403, "MSG06");
    }

    [Fact]
    public async Task UT_SCH_64_PublishRecurringAsync_mau_tuan_va_ngoai_le_theo_ngay()
    {
        await using var env = await NewEnv();
        var template = await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec(), CancellationToken.None);
        Assert.Equal("MSG39", template.Notification.Code);
        Assert.Equal(1, template.Schedule.Version);
        Assert.Equal(5, template.Schedule.Template!.Intervals.Length);
        var exception = await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec("Exception", Monday, 1, [new(env.N, 0, 600, 660, "Work")], "Họp"), CancellationToken.None);
        Assert.Equal(2, exception.Schedule.Version);
        Assert.Equal("Họp", exception.Schedule.Exception!.Reason);
        var later = await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec("Template", NextMonday, 2, null), CancellationToken.None);
        Assert.Equal(3, later.Schedule.Version);
        var current = await env.Svc().GetRecurringAsync(env.Dentist, env.DEmp, Monday, CancellationToken.None);
        Assert.Equal(NextMonday, Assert.Single(current.FutureChanges).Date);
    }

    public static TheoryData<string, string, int, string> BadRecurring => new()
    {
        { "UT-SCH-65 Mã yêu cầu rỗng", "noKey", 400, "MSG110" },
        { "UT-SCH-66 Ngày đã qua", "past", 400, "MSG110" },
        { "UT-SCH-67 Năm 2101", "year2101", 400, "MSG110" },
        { "UT-SCH-68 Loại lạ", "badKind", 400, "MSG110" },
        { "UT-SCH-69 Lý do 501 ký tự", "reason501", 400, "MSG110" },
        { "UT-SCH-70 Ngoại lệ thiếu lý do", "exceptionNoReason", 400, "MSG110" },
        { "UT-SCH-71 Giờ không chia hết cho 30 phút", "step30", 400, "MSG110" },
        { "UT-SCH-72 Một ngày làm ở hai chi nhánh", "twoBranches", 400, "MSG110" },
        { "UT-SCH-73 Ngoại lệ có khung ngày khác", "exceptionOtherDay", 400, "MSG110" },
        { "UT-SCH-74 Khung giờ null", "nullPeriod", 400, "MSG110" },
        { "UT-SCH-75 Phiên bản kỳ vọng sai", "stale", 409, "MSG111" },
        { "UT-SCH-76 Chi nhánh trong khung giờ ngoài quyền", "foreignBranch", 403, "MSG06" },
    };

    [Theory, MemberData(nameof(BadRecurring))]
    public async Task UT_SCH_Recurring_validation(string caseName, string kind, int status, string code)
    {
        await using var env = await NewEnv();
        _ = caseName;
        var ok = env.Rec();
        var cmd = kind switch
        {
            "noKey" => env.Rec(key: Guid.Empty),
            "past" => env.Rec(date: new DateOnly(2030, 1, 7)),
            "year2101" => env.Rec(date: new DateOnly(2101, 1, 4)),
            "badKind" => env.Rec("Weekly"),
            "reason501" => env.Rec(reason: new string('r', 501)),
            "exceptionNoReason" => env.Rec("Exception", intervals: [new(env.N, 0, 480, 540, "Work")]),
            "step30" => env.Rec(intervals: [new(env.N, 0, 480, 545, "Work")]),
            "twoBranches" => env.Rec(intervals: [new(env.N, 0, 480, 540, "Work"), new(env.N + 1, 0, 600, 660, "Work")]),
            "exceptionOtherDay" => env.Rec("Exception", intervals: [new(env.N, 2, 480, 540, "Work")], reason: "x"),
            "nullPeriod" => ok with { Intervals = [null!] },
            "stale" => env.Rec(version: 4),
            _ => env.Rec(intervals: [new(env.Other, 0, 480, 540, "Work")]),
        };
        await Rejects(() => env.Svc().PreviewRecurringAsync(env.Dentist, cmd, CancellationToken.None), status, code);
    }

    [Fact]
    public async Task UT_SCH_77_PublishRecurringAsync_gui_lai_cung_khoa_tra_ve_ket_qua_cu_khac_noi_dung_la_409()
    {
        await using var env = await NewEnv();
        var cmd = env.Rec();
        var first = await env.Svc().PublishRecurringAsync(env.Dentist, cmd, CancellationToken.None);
        var again = await env.Svc().PublishRecurringAsync(env.Dentist, cmd, CancellationToken.None);
        Assert.Equal(first.Schedule.Version, again.Schedule.Version);
        await Rejects(() => env.Svc().PublishRecurringAsync(env.Dentist, cmd with { Reason = "khác" }, CancellationToken.None), 409, "MSG111");
    }

    [Fact]
    public async Task UT_SCH_78_PublishRecurringAsync_lich_hen_bi_anh_huong_la_409_MSG38_va_Preview_liet_ke_lich()
    {
        await using var env = await NewEnv();
        await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec(), CancellationToken.None);
        var booked = await env.Appt(start: 540);
        var narrower = env.Rec(version: 1, intervals: [new(env.N, 0, 600, 660, "Work")]);
        await Rejects(() => env.Svc().PublishRecurringAsync(env.Dentist, narrower, CancellationToken.None), 409, "MSG38");
        var preview = await env.Svc().PreviewRecurringAsync(env.Dentist, narrower, CancellationToken.None);
        Assert.Equal([booked.Id], preview.AffectedAppointments.Select(x => x.Id));
        var exceptionElsewhere = env.Rec("Exception", Monday.AddDays(1), 1, [new(env.N, 1, 480, 540, "Work")], "Nghỉ");
        Assert.Empty((await env.Svc().PreviewRecurringAsync(env.Dentist, exceptionElsewhere, CancellationToken.None)).AffectedAppointments);
    }

    [Fact]
    public async Task UT_SCH_79_Query_dung_lich_dinh_ky_ngoai_le_thang_mau()
    {
        await using var env = await NewEnv();
        await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec(), CancellationToken.None);
        Assert.Equal(15, (await env.Query(env.Q())).Slots.Length);
        await env.Svc().PublishRecurringAsync(env.Dentist, env.Rec("Exception", Monday, 1, [new(env.N, 0, 480, 540, "Work")], "Họp"), CancellationToken.None);
        Assert.Equal(3, (await env.Query(env.Q())).Slots.Length);
        Assert.Equal(15, (await env.Query(env.Q(date: Monday.AddDays(1)))).Slots.Length);
    }

    [Fact]
    public async Task UT_SCH_80_PreviewRecurringAvailabilityAsync_tinh_khung_gio_theo_ban_nhap_cho_tung_ngay()
    {
        await using var env = await NewEnv();
        var request = new RecurringPreviewRequest(env.Rec(intervals: [new(env.N, 0, 480, 540, "Work"), new(env.N, 1, 480, 600, "Work")]), env.N, null, Monday, Monday.AddDays(2), 30);
        var result = await env.Svc().PreviewRecurringAvailabilityAsync(env.Dentist, request, CancellationToken.None);
        Assert.Equal([3, 7, 0], result.Days.Select(x => x.Slots.Length));
    }

    [Fact]
    public async Task UT_SCH_81_PreviewRecurringAvailabilityAsync_khoang_ngay_khong_hop_le_la_MSG110()
    {
        await using var env = await NewEnv();
        var rec = env.Rec();
        await Rejects(() => env.Svc().PreviewRecurringAvailabilityAsync(env.Dentist, new(rec, env.N, null, Monday, Monday.AddDays(-1), 30), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().PreviewRecurringAvailabilityAsync(env.Dentist, new(rec, env.N, null, Monday, Monday.AddDays(31), 30), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().PreviewRecurringAvailabilityAsync(env.Dentist, new(rec, env.N, null, new DateOnly(1999, 1, 1), Monday, 30), CancellationToken.None), 400, "MSG110");
        await Rejects(() => env.Svc().PreviewRecurringAvailabilityAsync(env.Dentist, new(rec, env.Other, null, Monday, Monday, 30), CancellationToken.None), 403, "MSG06");
    }
}
