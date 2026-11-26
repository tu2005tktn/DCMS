using DCMS.Application.Common;
using DCMS.Application.Features.Settings;
using DCMS.Application.Features.Treatment;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Scheduling;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Treatment;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>Method-level tests of ServicePriceService: ListAsync, GetAsync, SaveAsync (the private AuthorizeAsync is reached through Get/Save).</summary>
[Collection(PostgresCollection.Name)]
public sealed class ServicePriceServiceTests
{
    private sealed class Scope(bool owner, bool assigned, params int[] branches) : ISettingsAccess
    {
        public Task<PermissionState> ResolveAsync(int actorId, CancellationToken ct) => Task.FromResult(new PermissionState(owner, assigned, [], [], branches));
        public Task<PermissionState> RequireAsync(int actorId, string permission, CancellationToken ct) => ResolveAsync(actorId, ct);
        public Task OperationalAsync(int actorId, string permission, int branchId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int N, Other, Alpha, Bravo, Off, OtherService;
        public required Dictionary<string, int> Actors;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public int Actor(string key) => key == "ghost" ? 987654321 : Actors[key];
        public ServicePriceService Svc(ISettingsAccess? access = null) => new(Db, CreateTestKit.NewRoleUserManager(Db), access ?? new Scope(false, false));
    }

    /// <summary>Branch N has services alpha/bravo (+ an inactive one); another branch has one. Actors are listed by the key used in the cases.</summary>
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
        var actors = new Dictionary<string, int>();
        async Task Staff(string key, string role, int branch, bool member = true) => actors[key] = (await CreateTestKit.SeedStaffAsync(db, key + n, role, branch, member)).Id;
        await Staff("admin", AccountRoles.Admin, n, member: false);
        await Staff("owner", AccountRoles.ClinicOwner, n);
        await Staff("ownerNoFlag", AccountRoles.ClinicOwner, n);
        await Staff("dentist", AccountRoles.Dentist, n);
        await Staff("dentistNoFlag", AccountRoles.Dentist, n);
        await Staff("assistant", AccountRoles.ClinicAssistant, n);
        await Staff("otherDentist", AccountRoles.Dentist, other);
        await Staff("outsider", AccountRoles.Dentist, n, member: false);
        await Staff("inactive", AccountRoles.Dentist, n);
        var customer = new ApplicationUser { UserName = "customer" + n, FullName = "Khách", AccountType = "Public", IsActive = true, AssignedRoles = [AccountRoles.Patient] };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        actors["customer"] = customer.Id;
        (await db.Users.SingleAsync(x => x.Id == actors["inactive"])).IsActive = false;
        // Dentist/owner whose membership of the branch does NOT allow examining.
        var branch = await db.Set<SchedulingBranch>().SingleAsync(x => x.Id == n);
        foreach (var key in new[] { "ownerNoFlag", "dentistNoFlag" })
        {
            var employeeId = (await db.Employees.SingleAsync(x => x.AccountId == actors[key])).Id;
            branch.Members.Single(m => m.EmployeeId == employeeId).CanDentist = false;
        }
        SchedulingService S(int b, string name, bool active = true, long? price = 100000) => new() { BranchId = b, Name = name, Active = active, ReferenceUnitPriceVnd = price };
        var alpha = S(n, "A-alpha"); var bravo = S(n, "B-bravo", price: null); var off = S(n, "Z-off", active: false); var far = S(other, "A-other");
        db.Set<SchedulingService>().AddRange(alpha, bravo, off, far);
        await db.SaveChangesAsync();
        return new Env { Pg = pg, N = n, Other = other, Actors = actors, Alpha = alpha.Id, Bravo = bravo.Id, Off = off.Id, OtherService = far.Id };
    }

    // ================= GetAsync =================
    // Input: actor key (+ access scope) -> Expected: the service of branch N is returned, or MSG06 (403).
    public static TheoryData<string, string, bool, bool, int, bool> GetCases => new()
    {
        // caseName, actor, scope.Owner, scope.Assigned (branch list = [N] or [Other] below), scopeBranch (0 = N, 1 = other), allowed
        { "UT-SPG-01 Quản trị viên (không thuộc chi nhánh)", "admin", false, false, 0, true },
        { "UT-SPG-02 Chủ phòng khám là thành viên", "owner", false, false, 0, true },
        { "UT-SPG-03 Bác sĩ là thành viên", "dentist", false, false, 0, true },
        { "UT-SPG-04 Trợ lý là thành viên (đọc được)", "assistant", false, false, 0, true },
        { "UT-SPG-05 Bác sĩ thành viên nhưng không được khám (đọc vẫn được)", "dentistNoFlag", false, false, 0, true },
        { "UT-SPG-06 Bác sĩ chi nhánh khác", "otherDentist", false, false, 0, false },
        { "UT-SPG-07 Bác sĩ chưa có hồ sơ nhân viên", "outsider", false, false, 0, false },
        { "UT-SPG-08 Tài khoản nhân viên đã khóa", "inactive", false, false, 0, false },
        { "UT-SPG-09 Tài khoản khách hàng", "customer", false, false, 0, false },
        { "UT-SPG-10 Tài khoản không tồn tại", "ghost", false, false, 0, false },
        { "UT-SPG-11 Phạm vi được giao không chứa chi nhánh", "dentist", false, true, 1, false },
        { "UT-SPG-12 Phạm vi được giao có chi nhánh", "dentist", false, true, 0, true },
        { "UT-SPG-13 Phạm vi được giao nhưng là Owner thì bỏ qua", "dentist", true, true, 1, true }
    };

    [Theory, MemberData(nameof(GetCases))]
    public async Task Get_authorizes_by_role_membership_and_scope(string caseName, string actor, bool owner, bool assigned, int scopeBranch, bool allowed)
    {
        await using var env = await NewEnv();
        var access = new Scope(owner, assigned, scopeBranch == 0 ? env.N : env.Other);
        var call = () => env.Svc(access).GetAsync(env.Actor(actor), env.Alpha, CancellationToken.None);
        if (allowed)
        {
            var dto = await call();
            Assert.Equal((env.Alpha, env.N, "A-alpha", 100000L, 1, "lần"), (dto.ServiceId, dto.BranchId, dto.Name, dto.ReferenceUnitPriceVnd, dto.PriceVersion, dto.Unit));
            return;
        }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", error.Message);
    }

    [Fact]
    public async Task Get_UT_SPG_14_unknown_service_is_not_found()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().GetAsync(env.Actor("admin"), 987654321, CancellationToken.None));
        Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
    }

    // ================= ListAsync =================
    [Theory]
    [InlineData("UT-SPL-01 Trợ lý không được xem bảng giá", "assistant")]
    [InlineData("UT-SPL-02 Khách hàng", "customer")]
    [InlineData("UT-SPL-03 Tài khoản đã khóa", "inactive")]
    [InlineData("UT-SPL-04 Tài khoản không tồn tại", "ghost")]
    [InlineData("UT-SPL-05 Bác sĩ chưa có hồ sơ nhân viên", "outsider")]
    public async Task List_rejects_actors_who_cannot_see_prices(string caseName, string actor)
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().ListAsync(env.Actor(actor), CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    // Input: actor + scope -> Expected: names (of this test's two branches) in branch/name order; inactive services are never listed.
    public static TheoryData<string, string, bool, bool, int, string> ListCases => new()
    {
        { "UT-SPL-06 Quản trị viên thấy mọi chi nhánh", "admin", false, false, 0, "A-alpha,B-bravo,A-other" },
        { "UT-SPL-07 Chủ phòng khám chỉ thấy chi nhánh mình", "owner", false, false, 0, "A-alpha,B-bravo" },
        { "UT-SPL-08 Bác sĩ chỉ thấy chi nhánh mình", "dentist", false, false, 0, "A-alpha,B-bravo" },
        { "UT-SPL-09 Bác sĩ chi nhánh khác", "otherDentist", false, false, 0, "A-other" },
        { "UT-SPL-10 Bác sĩ không được khám: danh sách rỗng", "dentistNoFlag", false, false, 0, "" },
        { "UT-SPL-11 Chủ phòng khám dù không được khám vẫn thấy", "ownerNoFlag", false, false, 0, "A-alpha,B-bravo" },
        { "UT-SPL-12 Quản trị viên bị giới hạn theo phạm vi được giao", "admin", false, true, 0, "A-alpha,B-bravo" },
        { "UT-SPL-13 Phạm vi được giao là chi nhánh khác", "dentist", false, true, 1, "" },
        { "UT-SPL-14 Owner bỏ qua phạm vi được giao", "admin", true, true, 1, "A-alpha,B-bravo,A-other" }
    };

    [Theory, MemberData(nameof(ListCases))]
    public async Task List_returns_visible_active_services(string caseName, string actor, bool owner, bool assigned, int scopeBranch, string expected)
    {
        await using var env = await NewEnv();
        var access = new Scope(owner, assigned, scopeBranch == 0 ? env.N : env.Other);
        var rows = await env.Svc(access).ListAsync(env.Actor(actor), CancellationToken.None);
        var mine = rows.Where(x => x.BranchId == env.N || x.BranchId == env.Other).Select(x => x.Name).ToArray();
        Assert.Equal(expected == "" ? [] : expected.Split(','), mine);
        Assert.DoesNotContain(rows, x => x.ServiceId == env.Off);
    }

    // ================= SaveAsync =================
    private static SaveServicePriceRequest Req(int version = 1, long? price = 250000, string? unit = null, Guid? key = null) => new(version, price, key ?? Guid.NewGuid(), unit);

    public static TheoryData<string, string, bool> SaveAuthorization => new()
    {
        { "UT-SPS-01 Quản trị viên", "admin", true },
        { "UT-SPS-02 Chủ phòng khám", "owner", true },
        { "UT-SPS-03 Chủ phòng khám không được khám (vẫn sửa được)", "ownerNoFlag", true },
        { "UT-SPS-04 Bác sĩ được khám", "dentist", true },
        { "UT-SPS-05 Bác sĩ không được khám", "dentistNoFlag", false },
        { "UT-SPS-06 Trợ lý", "assistant", false },
        { "UT-SPS-07 Bác sĩ chi nhánh khác", "otherDentist", false },
        { "UT-SPS-08 Bác sĩ chưa có hồ sơ nhân viên", "outsider", false },
        { "UT-SPS-09 Tài khoản đã khóa", "inactive", false },
        { "UT-SPS-10 Khách hàng", "customer", false },
        { "UT-SPS-11 Tài khoản không tồn tại", "ghost", false }
    };

    [Theory, MemberData(nameof(SaveAuthorization))]
    public async Task Save_authorizes_the_actor(string caseName, string actor, bool allowed)
    {
        await using var env = await NewEnv();
        var call = () => env.Svc().SaveAsync(env.Actor(actor), env.Alpha, Req(), CancellationToken.None);
        if (allowed) { Assert.Equal(250000L, (await call()).ReferenceUnitPriceVnd); return; }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
        Assert.Equal(1, (await env.Db.Set<SchedulingService>().AsNoTracking().SingleAsync(x => x.Id == env.Alpha)).PriceVersion);
    }

    [Fact]
    public async Task Save_UT_SPS_12_scope_outside_the_branch_is_forbidden()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc(new Scope(false, true, env.Other)).SaveAsync(env.Actor("dentist"), env.Alpha, Req(), CancellationToken.None));
        Assert.Equal((403, "MSG06"), (error.Status, error.Response.Code));
    }

    [Fact]
    public async Task Save_UT_SPS_13_unknown_service_is_not_found()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SaveAsync(env.Actor("admin"), 987654321, Req(), CancellationToken.None));
        Assert.Equal((404, "MSG113"), (error.Status, error.Response.Code));
    }

    // Input: request values -> Expected: BusinessException (checked before any data is read).
    [Theory]
    [InlineData("UT-SPS-14 RequestKey rỗng", true, 250000L, 1)]
    [InlineData("UT-SPS-15 Giá âm", false, -1L, 1)]
    [InlineData("UT-SPS-16 ExpectedVersion bằng 0", false, 250000L, 0)]
    [InlineData("UT-SPS-17 ExpectedVersion âm", false, 250000L, -3)]
    public async Task Save_rejects_malformed_request(string caseName, bool emptyKey, long price, int version)
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, new SaveServicePriceRequest(version, price, emptyKey ? Guid.Empty : Guid.NewGuid()), CancellationToken.None));
        Assert.Equal((400, "MSG04"), (error.Status, error.Response.Code));
    }

    [Theory]
    [InlineData("UT-SPS-18 Giá 0 (biên dưới)", 0L)]
    [InlineData("UT-SPS-19 Giá rất lớn", 9_000_000_000L)]
    public async Task Save_accepts_boundary_prices(string caseName, long price)
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(price: price), CancellationToken.None);
        Assert.Equal((price, 2), (dto.ReferenceUnitPriceVnd, dto.PriceVersion));
    }

    [Fact]
    public async Task Save_UT_SPS_20_null_price_clears_the_price()
    {
        await using var env = await NewEnv();
        var dto = await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(price: null), CancellationToken.None);
        Assert.Null(dto.ReferenceUnitPriceVnd);
        Assert.Equal(2, dto.PriceVersion);
    }

    [Theory]
    [InlineData("UT-SPS-21 Đơn vị lần", "lần", true)]
    [InlineData("UT-SPS-22 Đơn vị răng", "răng", true)]
    [InlineData("UT-SPS-23 Đơn vị hàm", "hàm", true)]
    [InlineData("UT-SPS-24 Đơn vị gói", "gói", true)]
    [InlineData("UT-SPS-25 Đơn vị g", "g", true)]
    [InlineData("UT-SPS-26 Đơn vị ml", "ml", true)]
    [InlineData("UT-SPS-27 Đơn vị lạ", "kg", false)]
    [InlineData("UT-SPS-28 Đơn vị rỗng", "", false)]
    [InlineData("UT-SPS-29 Sai hoa thường", "ML", false)]
    public async Task Save_validates_unit(string caseName, string unit, bool valid)
    {
        await using var env = await NewEnv();
        var call = () => env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(unit: unit), CancellationToken.None);
        if (valid) { Assert.Equal(unit, (await call()).Unit); return; }
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal((400, "MSG04"), (error.Status, error.Response.Code));
        var stored = await env.Db.Set<SchedulingService>().AsNoTracking().SingleAsync(x => x.Id == env.Alpha);
        Assert.Equal(("lần", 1, (long?)100000), (stored.Unit, stored.PriceVersion, stored.ReferenceUnitPriceVnd));
    }

    [Fact]
    public async Task Save_UT_SPS_30_null_unit_keeps_the_current_unit()
    {
        await using var env = await NewEnv();
        await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(unit: "răng"), CancellationToken.None);
        var dto = await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(version: 2, price: 300000, unit: null), CancellationToken.None);
        Assert.Equal(("răng", 3, 300000L), (dto.Unit, dto.PriceVersion, dto.ReferenceUnitPriceVnd));
    }

    [Fact]
    public async Task Save_UT_SPS_31_stale_version_conflicts()
    {
        await using var env = await NewEnv();
        var error = await CreateTestKit.ErrorOf(() => env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, Req(version: 2), CancellationToken.None));
        Assert.Equal((409, "MSG111"), (error.Status, error.Response.Code));
        Assert.Equal(1, (await env.Db.Set<SchedulingService>().AsNoTracking().SingleAsync(x => x.Id == env.Alpha)).PriceVersion);
    }

    [Fact]
    public async Task Save_UT_SPS_32_same_key_same_request_replays_without_bumping_the_version_again()
    {
        await using var env = await NewEnv();
        var request = Req();
        var first = await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, request, CancellationToken.None);
        var again = await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, request, CancellationToken.None);
        Assert.Equal(first, again);
        Assert.Equal(2, (await env.Db.Set<SchedulingService>().AsNoTracking().SingleAsync(x => x.Id == env.Alpha)).PriceVersion);
    }

    [Fact]
    public async Task Save_UT_SPS_33_same_key_with_different_request_or_actor_conflicts()
    {
        await using var env = await NewEnv();
        var request = Req();
        await env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, request, CancellationToken.None);
        var otherRequest = await CreateTestKit.ErrorOf(() => env.Svc().SaveAsync(env.Actor("admin"), env.Alpha, request with { ReferenceUnitPriceVnd = 1 }, CancellationToken.None));
        var otherActor = await CreateTestKit.ErrorOf(() => env.Svc().SaveAsync(env.Actor("owner"), env.Alpha, request, CancellationToken.None));
        Assert.Equal([(409, "MSG111"), (409, "MSG111")], [(otherRequest.Status, otherRequest.Response.Code), (otherActor.Status, otherActor.Response.Code)]);
    }
}
