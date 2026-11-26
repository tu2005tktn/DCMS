using DCMS.Application.Features.Authentication;
using DCMS.Domain.Features.Accounts;
using DCMS.Domain.Features.Settings;
using DCMS.Infrastructure.Features.Accounts;
using DCMS.Infrastructure.Features.Authentication;
using DCMS.Infrastructure.Persistence;
using DCMS.UnitTests.Create;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DCMS.UnitTests.Methods;

/// <summary>
/// Method-level tests of RegistrationService.RegisterAsync (public customer sign-up) and its helper Ensure.
/// UT-REG-01..11 (basic field validation) are in Create/CustomerRegisterTests; this file adds the remaining branches:
/// null/blank inputs, phone and email formats, trimming, Guest role missing, duplicates, Identity password rules and the stored result.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationServiceTests
{
    private const string Password = "Abcdef1!";

    private static RegisterRequest Valid(string? fullName = "Lê Văn Nam", string? phone = "0987654321", string? email = "nam@gmail.com",
        string? password = Password, string? confirm = Password) => new(fullName, phone, email, password, confirm);

    private static string PhoneOf(int tag, int i) => $"09{(tag * 10 + i) % 100000000:D8}";

    private static RegistrationService NewService(DcmsDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddIdentityCore<ApplicationUser>(options =>
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._")
            .AddRoles<IdentityRole<int>>().AddUserStore<AccountUserStore>().AddRoleStore<AccountRoleStore>();
        var provider = services.BuildServiceProvider();
        return new RegistrationService(db, provider.GetRequiredService<UserManager<ApplicationUser>>(), provider.GetRequiredService<RoleManager<IdentityRole<int>>>());
    }

    private static async Task EnsureGuestRoleAsync(DcmsDbContext db)
    {
        if (await db.Set<AccessGroup>().IgnoreQueryFilters().AnyAsync(x => x.RecordKind == "IdentityRole" && x.Name == AccountRoles.Guest)) return;
        db.Set<AccessGroup>().Add(new() { RecordKind = "IdentityRole", IdentityRoleId = 200 + Array.IndexOf(AccountRoles.All, AccountRoles.Guest), Code = AccountRoles.Guest, Name = AccountRoles.Guest, BaseRole = AccountRoles.Guest });
        await db.SaveChangesAsync();
    }

    private static async Task Rejects(Func<Task> call, int status, string code, string message)
    {
        var error = await CreateTestKit.ErrorOf(call);
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Response.Code);
        Assert.Equal(message, error.Message);
    }

    // ================= Validation before any database access =================
    public static TheoryData<string, RegisterRequest, string, string> Invalid => new()
    {
        { "UT-REG-12 Họ tên null", Valid(fullName: null), "MSG02", "Vui lòng nhập Họ tên." },
        { "UT-REG-13 Họ tên chỉ có khoảng trắng", Valid(fullName: "   "), "MSG02", "Vui lòng nhập Họ tên." },
        { "UT-REG-14 Số điện thoại null", Valid(phone: null), "MSG02", "Vui lòng nhập Số điện thoại." },
        { "UT-REG-15 Số điện thoại chỉ có khoảng trắng", Valid(phone: "   "), "MSG02", "Vui lòng nhập Số điện thoại." },
        { "UT-REG-16 Số điện thoại 9 số", Valid(phone: "098765432"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-17 Số điện thoại 11 số", Valid(phone: "09876543210"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-18 Đầu số 02 (điện thoại bàn)", Valid(phone: "0287654321"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-19 Đầu số 04", Valid(phone: "0487654321"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-20 Đầu +84", Valid(phone: "+84987654321"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-21 Số điện thoại có chữ", Valid(phone: "09876a4321"), "MSG04", "Số điện thoại không đúng định dạng." },
        { "UT-REG-22 Email null", Valid(email: null), "MSG02", "Vui lòng nhập Email." },
        { "UT-REG-23 Email chỉ có khoảng trắng", Valid(email: "  "), "MSG02", "Vui lòng nhập Email." },
        { "UT-REG-24 Email dạng 'Tên <địa chỉ>'", Valid(email: "Nam <nam@gmail.com>"), "MSG04", "Email không đúng định dạng." },
        { "UT-REG-25 Email có khoảng trắng bên trong", Valid(email: "na m@gmail.com"), "MSG04", "Email không đúng định dạng." },
        { "UT-REG-26 Mật khẩu null", Valid(password: null, confirm: null), "MSG02", "Vui lòng nhập Mật khẩu." },
        { "UT-REG-27 Xác nhận mật khẩu null", Valid(confirm: null), "MSG02", "Vui lòng nhập Xác nhận mật khẩu." },
        { "UT-REG-28 Xác nhận khác hoa/thường", Valid(confirm: "abcdef1!"), "MSG04", "Xác nhận mật khẩu không đúng định dạng." },
    };

    [Theory, MemberData(nameof(Invalid))]
    public async Task Register_rejects_invalid_input_before_touching_the_database(string caseName, RegisterRequest request, string code, string message)
    {
        using var db = CreateTestKit.NewDb();
        await Rejects(() => new RegistrationService(db, CreateTestKit.NewUserManager(db), null!).RegisterAsync(request, default), 400, code, message);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Register_UT_REG_29_password_of_exactly_256_characters_passes_the_length_check_but_not_the_password_policy()
    {
        // 256 is the upper bound of the explicit check; the Identity policy (needs digit/symbol) then decides.
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var longPassword = new string('a', 256);
        await Rejects(() => NewService(pg.Db).RegisterAsync(Valid(phone: PhoneOf(CreateTestKit.NextId(), 1), email: $"r{CreateTestKit.NextId()}@dcms.vn", password: longPassword, confirm: longPassword), default),
            400, "MSG109", "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.");
    }

    // ================= Guest role missing =================
    [Fact]
    public async Task Register_UT_REG_30_guest_role_missing_returns_503_MSG144_and_creates_nothing()
    {
        using var db = CreateTestKit.NewDb();   // fresh database without the Guest role
        await Rejects(() => NewService(db).RegisterAsync(Valid(), default), 503, "MSG144", "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.");
        Assert.Empty(db.Users);
    }

    // ================= Success =================
    [Fact]
    public async Task Register_UT_REG_31_creates_a_public_account_with_the_guest_role_and_an_audit_row()
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var tag = CreateTestKit.NextId();
        var phone = PhoneOf(tag, 1);

        var response = await NewService(pg.Db).RegisterAsync(Valid(fullName: "  Lê Văn Nam  ", phone: $" {phone} ", email: $"  Nam{tag}@Gmail.com  "), default);

        Assert.Equal(phone, response.PhoneNumber);
        Assert.Equal(("MSG11", "Tạo tài khoản thành công. Bạn có thể đặt lịch cho mình hoặc người thân."), (response.Notification.Code, response.Notification.Message));
        pg.Db.ChangeTracker.Clear();
        var user = await pg.Db.Users.AsNoTracking().SingleAsync(x => x.Id == response.AccountId);
        Assert.Equal(phone, user.UserName);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.Equal($"Nam{tag}@Gmail.com", user.Email);          // trimmed, case kept
        Assert.False(user.EmailConfirmed);
        Assert.Equal("Lê Văn Nam", user.FullName);
        Assert.Equal("Public", user.AccountType);
        Assert.True(user.IsActive);
        Assert.Equal([AccountRoles.Guest], user.AssignedRoles);
        Assert.True(await CreateTestKit.NewRoleUserManager(pg.Db).CheckPasswordAsync(user, Password));
        var audit = await pg.Db.AccountAudits.AsNoTracking().SingleAsync(x => x.TargetId == user.Id);
        Assert.Equal((user.Id, "SelfRegister", "Đăng ký tài khoản công khai"), (audit.ActorId, audit.Action, audit.Reason));
        using var after = System.Text.Json.JsonDocument.Parse(audit.AfterJson);
        Assert.Equal("Public", after.RootElement.GetProperty("accountType").GetString());
        Assert.Equal("Guest", after.RootElement.GetProperty("role").GetString());
        Assert.False(after.RootElement.GetProperty("phoneNumberConfirmed").GetBoolean());
    }

    // ================= Duplicates =================
    [Fact]
    public async Task Register_UT_REG_32_duplicate_phone_returns_409_MSG139()
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var tag = CreateTestKit.NextId();
        var service = NewService(pg.Db);
        await service.RegisterAsync(Valid(phone: PhoneOf(tag, 1), email: $"a{tag}@dcms.vn"), default);

        pg.Db.ChangeTracker.Clear();
        await Rejects(() => service.RegisterAsync(Valid(phone: PhoneOf(tag, 1), email: $"b{tag}@dcms.vn"), default), 409, "MSG139",
            "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.");
        Assert.Equal(1, await pg.Db.Users.CountAsync(x => x.Email == $"b{tag}@dcms.vn" || x.Email == $"a{tag}@dcms.vn"));
    }

    [Fact]
    public async Task Register_UT_REG_33_duplicate_email_ignoring_case_returns_409_MSG139()
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var tag = CreateTestKit.NextId();
        var service = NewService(pg.Db);
        await service.RegisterAsync(Valid(phone: PhoneOf(tag, 1), email: $"same{tag}@dcms.vn"), default);

        pg.Db.ChangeTracker.Clear();
        await Rejects(() => service.RegisterAsync(Valid(phone: PhoneOf(tag, 2), email: $"SAME{tag}@DCMS.VN"), default), 409, "MSG139",
            "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.");
        Assert.False(await pg.Db.Users.AnyAsync(x => x.UserName == PhoneOf(tag, 2)));
    }

    [Fact]
    public async Task Register_UT_REG_34_phone_already_used_as_a_staff_user_name_returns_409_MSG139()
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var tag = CreateTestKit.NextId();
        var phone = PhoneOf(tag, 1);
        pg.Db.Users.Add(new ApplicationUser { UserName = phone, FullName = "Nhân viên", AccountType = "Staff", IsActive = true });
        await pg.Db.SaveChangesAsync();

        await Rejects(() => NewService(pg.Db).RegisterAsync(Valid(phone: phone, email: $"x{tag}@dcms.vn"), default), 409, "MSG139",
            "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.");
    }

    // ================= Ensure(IdentityResult): Identity password policy =================
    [Theory]
    [InlineData("UT-REG-35 Mật khẩu 5 ký tự", "Ab1!x")]
    [InlineData("UT-REG-36 Thiếu chữ số", "Abcdefg!")]
    [InlineData("UT-REG-37 Thiếu chữ hoa", "abcdef1!")]
    [InlineData("UT-REG-38 Thiếu chữ thường", "ABCDEF1!")]
    [InlineData("UT-REG-39 Thiếu ký tự đặc biệt", "Abcdef12")]
    public async Task Register_rejects_passwords_that_break_the_identity_policy_with_MSG109(string caseName, string password)
    {
        await using var pg = await CreateTestKit.NewPostgresDbAsync();
        await EnsureGuestRoleAsync(pg.Db);
        var tag = CreateTestKit.NextId();
        var phone = PhoneOf(tag, 1);

        await Rejects(() => NewService(pg.Db).RegisterAsync(Valid(phone: phone, email: $"p{tag}@dcms.vn", password: password, confirm: password), default), 400, "MSG109",
            "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.");
        pg.Db.ChangeTracker.Clear();
        Assert.False(await pg.Db.Users.AnyAsync(x => x.UserName == phone));
    }
}
