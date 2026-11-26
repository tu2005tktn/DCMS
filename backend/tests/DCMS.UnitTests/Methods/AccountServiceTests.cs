using DCMS.Application.Features.Accounts;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Authentication;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of AccountService (staff account administration): ListAsync, DetailAsync, CreateAsync, UpdateAsync, SetStateAsync, AuditAsync
/// and helpers (CheckUserNameAsync, CheckContactsAsync, RequireAdminAsync, ValidateRoles/Reason, Check). Runs on PostgreSQL.
/// The shared test schema keeps rows of other tests, so each test searches with its own unique tag.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountServiceTests
{
    private const string Password = "Abcdef1!";

    private sealed class Env : IAsyncDisposable
    {
        public required CreateTestKit.PostgresDb Pg;
        public required int Tag, Admin, NonAdmin;
        public DcmsDbContext Db => Pg.Db;
        public ValueTask DisposeAsync() => Pg.DisposeAsync();
        public AccountService Svc() => new(Db, CreateTestKit.NewRoleUserManager(Db), new CreateTestKit.AllowAll());
        public string Code(int i) => $"T{Tag}C{i}";
        public CreateStaffRequest Req(int i = 1, Func<CreateStaffRequest, CreateStaffRequest>? change = null)
        {
            var r = new CreateStaffRequest($"t{Tag}u{i}", Password, $"Nhân viên Tag{Tag} số {i}", Code(i), $"t{Tag}u{i}@dcms.vn", $"09{Tag % 10}{(1000000 + Tag * 10 + i) % 10000000:D7}", [AccountRoles.ClinicAssistant]);
            return change == null ? r : change(r);
        }
        public Task<AccountChange> Create(CreateStaffRequest r, int? actor = null) => Svc().CreateAsync(actor ?? Admin, r, default);
        public async Task<AccountDto> Make(int i, params string[] roles) =>
            (await Create(Req(i, r => roles.Length == 0 ? r : r with { Roles = roles }))).Account;
        public async Task Mutate(int id, Action<ApplicationUser> change) { Db.ChangeTracker.Clear(); var u = await Db.Users.SingleAsync(x => x.Id == id); change(u); await Db.SaveChangesAsync(); Db.ChangeTracker.Clear(); }
        public async Task<ApplicationUser> Reload(int id) { Db.ChangeTracker.Clear(); return await Db.Users.AsNoTracking().SingleAsync(x => x.Id == id); }
    }

    private static async Task<Env> NewEnv()
    {
        var pg = await CreateTestKit.NewPostgresDbAsync();
        var tag = CreateTestKit.NextId();
        foreach (var role in AccountRoles.All)
            if (!await pg.Db.Set<DCMS.Domain.Features.Settings.AccessGroup>().IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == role))
                pg.Db.Set<DCMS.Domain.Features.Settings.AccessGroup>().Add(new() { RecordKind = "IdentityRole", IdentityRoleId = Array.IndexOf(AccountRoles.Staff, role) >= 0 ? 100 + Array.IndexOf(AccountRoles.Staff, role) : 200 + Array.IndexOf(AccountRoles.All, role), Code = role, Name = role, BaseRole = role });
        await pg.Db.SaveChangesAsync();
        var users = CreateTestKit.NewRoleUserManager(pg.Db);
        async Task<int> Staff(string name, string role)
        {
            var u = new ApplicationUser { UserName = name, FullName = name, AccountType = "Staff", IsActive = true, AssignedRoles = [role] };
            Assert.True((await users.CreateAsync(u, Password)).Succeeded);
            pg.Db.Employees.Add(new EmployeeProfile { AccountId = u.Id, EmployeeCode = "AD" + u.Id, FullName = name });
            await pg.Db.SaveChangesAsync();
            return u.Id;
        }
        return new Env { Pg = pg, Tag = tag, Admin = await Staff($"adm{tag}", AccountRoles.Admin), NonAdmin = await Staff($"dent{tag}", AccountRoles.Dentist) };
    }

    private static async Task Rejects(Func<Task> call, int status, string? code = null, string? message = null)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal(status, error.Status);
        if (code != null) Assert.Equal(code, error.Response.Code);
        if (message != null) Assert.Equal(message, error.Message);
    }

    // ================= ListAsync =================
    private static Task<AccountPage> List(Env env, string? search = null, int page = 1, int size = 20, string? role = null, string? status = null, string? sort = null, string? dir = null) =>
        env.Svc().ListAsync(search, page, size, role, status, sort, dir, default);

    [Theory]
    [InlineData("UT-ACS-01 Trang 0", 0, 20, null, null, null, null, null)]
    [InlineData("UT-ACS-02 Cỡ trang 0", 1, 0, null, null, null, null, null)]
    [InlineData("UT-ACS-03 Cỡ trang 101", 1, 101, null, null, null, null, null)]
    [InlineData("UT-ACS-04 Trang vượt int.Max / cỡ trang", 2147483647, 100, null, null, null, null, null)]
    [InlineData("UT-ACS-05 Từ khóa 161 ký tự", 1, 20, "{161}", null, null, null, null)]
    [InlineData("UT-ACS-06 Vai trò không tồn tại", 1, 20, null, "Hacker", null, null, null)]
    [InlineData("UT-ACS-07 Trạng thái lạ", 1, 20, null, null, "Banned", null, null)]
    [InlineData("UT-ACS-08 Hướng sắp xếp lạ", 1, 20, null, null, null, null, "sideways")]
    [InlineData("UT-ACS-09 Cột sắp xếp lạ", 1, 20, null, null, null, "password", null)]
    public async Task List_rejects_invalid_arguments(string caseName, int page, int size, string? search, string? role, string? status, string? sort, string? dir)
    {
        await using var env = await NewEnv();
        await Rejects(() => List(env, search == "{161}" ? new string('a', 161) : search, page, size, role, status, sort, dir), 400, "MSG04");
    }

    [Fact]
    public async Task List_UT_ACS_10_boundary_arguments_are_accepted()
    {
        await using var env = await NewEnv();
        foreach (var page in new[] { await List(env, new string('a', 160), 1, 1), await List(env, null, 1, 100), await List(env, null, 21474836, 100) })
            Assert.NotNull(page);
    }

    [Fact]
    public async Task List_UT_ACS_11_search_matches_user_name_full_name_employee_code_email_phone_and_id()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1); var b = await env.Make(2);
        Assert.Equal(2, (await List(env, $"tag{env.Tag}")).Total);                         // full name, case-insensitive
        Assert.Equal([a.Id], (await List(env, $"T{env.Tag}U1")).Items.Select(x => x.Id)); // user name (also matches email prefix)
        Assert.Equal([b.Id], (await List(env, env.Code(2).ToLowerInvariant())).Items.Select(x => x.Id)); // employee code
        Assert.Equal([a.Id], (await List(env, a.Email!.ToUpperInvariant())).Items.Select(x => x.Id));
        Assert.Contains(b.Id, (await List(env, $"({b.Phone![..4]}) {b.Phone[4..]}")).Items.Select(x => x.Id));  // phone ignores spaces and brackets
        Assert.Equal([a.Id], (await List(env, a.Id.ToString())).Items.Where(x => x.Id == a.Id).Select(x => x.Id));
        Assert.Empty((await List(env, $"zzz-{env.Tag}-none")).Items);
    }

    [Fact]
    public async Task List_UT_ACS_12_filters_by_role_status_and_pages()
    {
        await using var env = await NewEnv();
        var assistant = await env.Make(1); var dentist = await env.Make(2, AccountRoles.Dentist); var locked = await env.Make(3, AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        await env.Svc().SetStateAsync(env.Admin, locked.Id, new AccountStateRequest(locked.Version, true, "Nghỉ việc"), default);
        string tag = $"tag{env.Tag}";
        Assert.Equal([dentist.Id, locked.Id], (await List(env, tag, role: AccountRoles.Dentist)).Items.Select(x => x.Id));
        Assert.Equal([locked.Id], (await List(env, tag, status: "Locked")).Items.Select(x => x.Id));
        Assert.Equal([assistant.Id, dentist.Id], (await List(env, tag, status: "Active")).Items.Select(x => x.Id));
        var page2 = await List(env, tag, page: 2, size: 1);
        Assert.Equal((3, 2, 1, 1), (page2.Total, page2.Page, page2.PageSize, page2.Items.Count));
        Assert.Equal([AccountRoles.ClinicAssistant, AccountRoles.Dentist], (await List(env, tag)).Items.Single(x => x.Id == locked.Id).Roles);
    }

    [Theory]
    [InlineData("UT-ACS-13 id", "id")]
    [InlineData("UT-ACS-14 employeeCode", "employeeCode")]
    [InlineData("UT-ACS-15 userName", "userName")]
    [InlineData("UT-ACS-16 fullName", "fullName")]
    [InlineData("UT-ACS-17 email", "email")]
    [InlineData("UT-ACS-18 phone", "phone")]
    [InlineData("UT-ACS-19 isActive", "isActive")]
    [InlineData("UT-ACS-20 updatedAt", "updatedAt")]
    public async Task List_sorts_by_every_supported_column_in_both_directions(string caseName, string column)
    {
        await using var env = await NewEnv();
        await env.Make(1); await env.Make(2); await env.Make(3);
        var asc = await List(env, $"tag{env.Tag}", sort: column, dir: "asc");
        var desc = await List(env, $"tag{env.Tag}", sort: column, dir: "desc");
        Assert.Equal(3, asc.Items.Count);
        Assert.Equal(asc.Items.Select(x => x.Id).Order(), asc.Items.Select(x => x.Id).Order());
        if (column is "id" or "employeeCode" or "userName" or "fullName" or "email" or "phone")
            Assert.Equal(asc.Items.Select(x => x.Id), desc.Items.Select(x => x.Id).Reverse());
        Assert.Equal(asc.Items.Select(x => x.Id).Order(), desc.Items.Select(x => x.Id).Order());
    }

    // ================= DetailAsync =================
    [Fact]
    public async Task Detail_UT_ACS_21_returns_roles_and_employee_code_or_404()
    {
        await using var env = await NewEnv();
        var made = await env.Make(1, AccountRoles.Dentist, AccountRoles.ClinicAssistant);
        var dto = await env.Svc().DetailAsync(made.Id, default);
        Assert.Equal((env.Code(1), "Staff", true, 1), (dto.EmployeeCode, dto.AccountType, dto.IsActive, dto.Version));
        Assert.Equal([AccountRoles.ClinicAssistant, AccountRoles.Dentist], dto.Roles);
        await Rejects(() => env.Svc().DetailAsync(987654321, default), 404, "MSG01");
    }

    // ================= CreateAsync =================
    [Fact]
    public async Task Create_UT_ACS_22_creates_the_account_employee_roles_and_audit()
    {
        await using var env = await NewEnv();
        var change = await env.Create(env.Req(1, r => r with { UserName = $"  t{env.Tag}u1  ", FullName = "  Nguyễn Văn An  ", EmployeeCode = $" t{env.Tag}c1 ", Roles = [AccountRoles.Dentist, AccountRoles.ClinicAssistant] }));
        Assert.Equal("MSG14", change.Notification.Code);
        var a = change.Account;
        Assert.Equal(($"t{env.Tag}u1", "Nguyễn Văn An", env.Code(1), "Staff", true), (a.UserName, a.FullName, a.EmployeeCode, a.AccountType, a.IsActive));
        Assert.Equal([AccountRoles.ClinicAssistant, AccountRoles.Dentist], a.Roles);
        var user = await env.Reload(a.Id);
        Assert.True(await CreateTestKit.NewRoleUserManager(env.Db).CheckPasswordAsync(user, Password));
        Assert.Equal(env.Code(1), (await env.Db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == a.Id)).EmployeeCode);
        var audit = await env.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == a.Id);
        Assert.Equal(("CreateStaff", env.Admin), (audit.Action, audit.ActorId));
    }

    [Fact]
    public async Task Create_UT_ACS_23_only_an_active_admin_may_create()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(env.Req(1), env.NonAdmin), 403, "MSG06");
        await Rejects(() => env.Create(env.Req(1), 987654321), 403, "MSG06");
        (await env.Db.Users.SingleAsync(x => x.Id == env.Admin)).IsActive = false; await env.Db.SaveChangesAsync();
        await Rejects(() => env.Create(env.Req(1)), 403, "MSG06");
        Assert.Equal(0, await env.Db.Users.CountAsync(x => x.UserName == $"t{env.Tag}u1"));
    }

    [Fact]
    public async Task Create_UT_ACS_24_roles_must_be_distinct_staff_roles()
    {
        await using var env = await NewEnv();
        foreach (var roles in new[] { new string[0], [AccountRoles.Patient], [AccountRoles.Guest], ["Hacker"], [AccountRoles.Dentist, AccountRoles.Dentist], AccountRoles.Staff.Append(AccountRoles.Dentist).ToArray() })
            await Rejects(() => env.Create(env.Req(1, r => r with { Roles = roles })), 403, "MSG06");
        await env.Create(env.Req(1, r => r with { Roles = AccountRoles.Staff }));           // all staff roles at once is the upper bound
    }

    [Fact]
    public async Task Create_UT_ACS_25_duplicates_conflict_per_field()
    {
        await using var env = await NewEnv();
        var first = (await env.Create(env.Req(1))).Account;
        await Rejects(() => env.Create(env.Req(2, r => r with { UserName = first.UserName.ToUpperInvariant() })), 409);   // user name, case-insensitive
        await Rejects(() => env.Create(env.Req(2, r => r with { EmployeeCode = first.EmployeeCode!.ToLowerInvariant() })), 409);
        await Rejects(() => env.Create(env.Req(2, r => r with { Email = first.Email!.ToUpperInvariant() })), 409);
        await Rejects(() => env.Create(env.Req(2, r => r with { Phone = $"{first.Phone![..4]} {first.Phone[4..]}" })), 409);   // normalized phone
        var messages = new[] { "userName", "employeeCode", "email", "phone" }.Select(DCMS.Application.Features.Accounts.StaffAccountConflicts.ForField).Select(x => x.Message).Distinct().Count();
        Assert.Equal(4, messages);                                                                                         // each field has its own message
        Assert.Equal(1, await env.Db.Users.CountAsync(x => x.FullName.Contains($"Tag{env.Tag}")));
    }

    [Theory]
    [InlineData("UT-ACS-26 Mật khẩu quá yếu (abc)", "abc", 400, "MSG109")]
    [InlineData("UT-ACS-27 Mật khẩu thiếu chữ số", "Abcdefg!", 400, "MSG109")]
    [InlineData("UT-ACS-28 Mật khẩu thiếu ký tự đặc biệt", "Abcdef12", 400, "MSG109")]
    public async Task Create_rejects_passwords_that_fail_the_identity_policy(string caseName, string password, int status, string code)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(env.Req(1, r => r with { Password = password })), status, code);
        Assert.Equal(0, await env.Db.Users.CountAsync(x => x.UserName == $"t{env.Tag}u1"));
    }

    [Theory]
    [InlineData("UT-ACS-29 Mã nhân viên có khoảng trắng", "NV 01")]
    [InlineData("UT-ACS-30 Mã nhân viên 41 ký tự", "{41}")]
    [InlineData("UT-ACS-31 Mã nhân viên có ký tự đặc biệt", "NV#01")]
    public async Task Create_rejects_malformed_employee_codes(string caseName, string code)
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(env.Req(1, r => r with { EmployeeCode = code == "{41}" ? new string('A', 41) : code })), 400, "MSG04");
    }

    [Fact]
    public async Task Create_UT_ACS_32_boundaries_user_name_3_to_64_chars_and_employee_code_40_chars()
    {
        await using var env = await NewEnv();
        await Rejects(() => env.Create(env.Req(1, r => r with { UserName = "ab" })), 400, "MSG04");
        await Rejects(() => env.Create(env.Req(1, r => r with { UserName = new string('a', 65) })), 400, "MSG04");
        await env.Create(env.Req(1, r => r with { UserName = $"a{env.Tag}" .PadRight(64, 'x') }));
        await env.Create(env.Req(2, r => r with { EmployeeCode = ("E" + env.Tag).PadRight(40, '9') }));
    }

    // ================= UpdateAsync =================
    private static UpdateStaffRequest Upd(AccountDto a, Func<UpdateStaffRequest, UpdateStaffRequest>? change = null)
    {
        var r = new UpdateStaffRequest(a.Version, a.UserName, a.FullName, a.Email, a.Phone, a.Roles, "Cập nhật thông tin");
        return change == null ? r : change(r);
    }

    [Fact]
    public async Task Update_UT_ACS_33_changes_details_roles_and_resets_contact_confirmation()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1, AccountRoles.ClinicAssistant);
        await env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version, false, "Giữ nguyên"), default);   // version 2
        await env.Mutate(a.Id, u => { u.EmailConfirmed = true; u.PhoneNumberConfirmed = true; });
        var current = await env.Svc().DetailAsync(a.Id, default);
        var newPhone = $"08{env.Tag % 10}{(2000000 + env.Tag) % 10000000:D7}";
        var result = await env.Svc().UpdateAsync(env.Admin, a.Id, Upd(current, r => r with { UserName = $"t{env.Tag}new", FullName = " Tên mới ", Email = $"t{env.Tag}new@dcms.vn", Phone = newPhone,
            Roles = [AccountRoles.Dentist], Reason = "  Đổi chức vụ  " }), default);
        var dto = result.Account;
        Assert.Equal("MSG14", result.Notification.Code);
        Assert.Equal(($"t{env.Tag}new", "Tên mới", current.Version + 1), (dto.UserName, dto.FullName, dto.Version));
        Assert.Equal([AccountRoles.Dentist], dto.Roles);
        var saved = await env.Reload(a.Id);
        Assert.Equal((false, false), (saved.EmailConfirmed, saved.PhoneNumberConfirmed));          // changed contacts are unverified
        Assert.Equal("Tên mới", (await env.Db.Employees.AsNoTracking().SingleAsync(x => x.AccountId == a.Id)).FullName);
        var audit = await env.Db.AccountAudits.AsNoTracking().Where(x => x.TargetId == a.Id && x.Action == "UpdateStaff").SingleAsync();
        Assert.Equal(("Đổi chức vụ", env.Admin), (audit.Reason, audit.ActorId));
        Assert.Contains("ClinicAssistant", audit.BeforeJson);
    }

    [Fact]
    public async Task Update_UT_ACS_34_keeping_the_same_contacts_keeps_their_confirmation()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1);
        await env.Mutate(a.Id, u => { u.EmailConfirmed = true; u.PhoneNumberConfirmed = true; });
        await env.Svc().UpdateAsync(env.Admin, a.Id, Upd(await env.Svc().DetailAsync(a.Id, default), r => r with { FullName = "Chỉ đổi tên" }), default);
        var saved = await env.Reload(a.Id);
        Assert.Equal((true, true), (saved.EmailConfirmed, saved.PhoneNumberConfirmed));
    }

    [Fact]
    public async Task Update_UT_ACS_35_guards()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1); var b = await env.Make(2);
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Reason = "" }), default), 400, "MSG02");
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Reason = new string('a', 501) }), default), 400, "MSG03");
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Roles = [] }), default), 403, "MSG06");
        await Rejects(() => env.Svc().UpdateAsync(env.NonAdmin, a.Id, Upd(a), default), 403, "MSG06");
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, 987654321, Upd(a), default), 404, "MSG01");
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Version = 9 }), default), 409, "MSG111");
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { UserName = b.UserName }), default), 409);
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Email = b.Email }), default), 409);
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Phone = b.Phone }), default), 409);
        Assert.Equal(a.UserName, (await env.Reload(a.Id)).UserName);
        await env.Svc().UpdateAsync(env.Admin, a.Id, Upd(a, r => r with { Reason = new string('a', 500) }), default);   // own contacts + 500-char reason are fine
    }

    [Fact]
    public async Task Update_UT_ACS_36_a_public_account_cannot_be_edited_here()
    {
        await using var env = await NewEnv();
        var publicUser = new ApplicationUser { UserName = $"pub{env.Tag}", FullName = "Khách", AccountType = "Public", IsActive = true, AssignedRoles = [AccountRoles.Patient], Email = $"pub{env.Tag}@dcms.vn" };
        Assert.True((await CreateTestKit.NewRoleUserManager(env.Db).CreateAsync(publicUser, Password)).Succeeded);
        var dto = await env.Svc().DetailAsync(publicUser.Id, default);
        await Rejects(() => env.Svc().UpdateAsync(env.Admin, publicUser.Id, Upd(dto, r => r with { Roles = [AccountRoles.Dentist] }), default), 403, "MSG06");
    }

    // ================= SetStateAsync =================
    [Fact]
    public async Task SetState_UT_ACS_37_lock_revokes_sessions_and_unlock_restores_access()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1);
        var user = await env.Reload(a.Id);
        var session = new AuthSession { Id = Guid.NewGuid(), AccountId = a.Id, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), LastSeenAt = DateTimeOffset.UtcNow, ClientKind = "StaffWeb" };
        session.RefreshTokens.Add(new AuthRefreshToken { Id = await env.Db.NextAccountEntryIdAsync(default), AccountId = a.Id, SessionId = session.Id, TokenHash = new string('a', 64), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), Status = "ACTIVE" });
        env.Db.ChangeTracker.Clear();
        var tracked = await env.Db.Users.SingleAsync(x => x.Id == a.Id); tracked.Sessions.Add(session); await env.Db.SaveChangesAsync();
        _ = user;
        var locked = await env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version, true, "  Vi phạm  "), default);
        Assert.Equal(("MSG15", false, a.Version + 1), (locked.Notification.Code, locked.Account.IsActive, locked.Account.Version));
        var saved = await env.Reload(a.Id);
        Assert.NotNull(saved.Sessions.Single().RevokedAt);
        Assert.Equal("REVOKED", saved.Sessions.Single().RefreshTokens.Single().Status);
        var audit = await env.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == a.Id && x.Action == "Lock");
        Assert.Equal("Vi phạm", audit.Reason);
        var unlocked = await env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(locked.Account.Version, false, "Đã xử lý"), default);
        Assert.Equal(("MSG16", true), (unlocked.Notification.Code, unlocked.Account.IsActive));
        Assert.Contains(env.Db.AccountAudits.Where(x => x.TargetId == a.Id), x => x.Action == "Unlock");
    }

    [Fact]
    public async Task SetState_UT_ACS_38_guards()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1);
        await Rejects(() => env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version, true, ""), default), 400, "MSG02");
        await Rejects(() => env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version, true, new string('a', 501)), default), 400, "MSG03");
        await Rejects(() => env.Svc().SetStateAsync(env.NonAdmin, a.Id, new AccountStateRequest(a.Version, true, "x"), default), 403, "MSG06");
        await Rejects(() => env.Svc().SetStateAsync(env.Admin, 987654321, new AccountStateRequest(1, true, "x"), default), 404, "MSG01");
        await Rejects(() => env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version + 1, true, "x"), default), 409, "MSG111");
        Assert.True((await env.Reload(a.Id)).IsActive);
    }

    // ================= AuditAsync =================
    [Fact]
    public async Task Audit_UT_ACS_39_returns_the_history_newest_first_with_actor_names()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1);
        await env.Svc().SetStateAsync(env.Admin, a.Id, new AccountStateRequest(a.Version, true, "Khóa thử"), default);
        var rows = await env.Svc().AuditAsync(a.Id, default);
        Assert.Equal(["Lock", "CreateStaff"], rows.Select(x => x.Action));
        Assert.Equal(($"adm{env.Tag}", env.Admin, a.Id, "Khóa thử"), (rows[0].ActorUserName, rows[0].ActorId, rows[0].TargetId, rows[0].Reason));
        using (var after = System.Text.Json.JsonDocument.Parse(rows[0].AfterJson)) Assert.False(after.RootElement.GetProperty("IsActive").GetBoolean());
        await Rejects(() => env.Svc().AuditAsync(987654321, default), 404, "MSG01");
        Assert.Empty(await env.Svc().AuditAsync(env.NonAdmin, default));
    }

    [Fact]
    public async Task Audit_UT_ACS_40_is_capped_at_100_rows()
    {
        await using var env = await NewEnv();
        var a = await env.Make(1);
        env.Db.AccountAudits.AddRange(Enumerable.Range(0, 105).Select(i => new AccountAudit { ActorId = env.Admin, TargetId = a.Id, Action = "Note", Reason = "r" + i, OccurredAt = DateTimeOffset.UtcNow, BeforeJson = "{}", AfterJson = "{}" }));
        await env.Db.SaveChangesAsync();
        Assert.Equal(100, (await env.Svc().AuditAsync(a.Id, default)).Count);
    }
}
