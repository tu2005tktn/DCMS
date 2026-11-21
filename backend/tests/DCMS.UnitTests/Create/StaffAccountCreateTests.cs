using DCMS.Application.Features.Accounts;
using DCMS.Infrastructure.Features.Accounts;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>AccountService.CreateAsync (create a staff account). Each row = one input set and the exact message the code throws.</summary>
public sealed class StaffAccountCreateTests
{
    private static CreateStaffRequest Valid(string userName = "nv.an", string password = "Abcdef1!", string fullName = "Nguyễn Văn An",
        string employeeCode = "NV001", string? email = "an@dcms.vn", string? phone = "0901234567", string[]? roles = null) =>
        new(userName, password, fullName, employeeCode, email, phone, roles ?? ["ClinicAssistant"]);

    public static TheoryData<string, CreateStaffRequest, int, string> Rejected => new()
    {
        { "UT-ACC-01 Tên đăng nhập để trống", Valid(userName: ""), 400, "Vui lòng nhập Tên đăng nhập." },
        { "UT-ACC-02 Tên đăng nhập có khoảng trắng", Valid(userName: "nv an"), 400, "Tên đăng nhập không đúng định dạng." },
        { "UT-ACC-03 Họ tên để trống", Valid(fullName: ""), 400, "Vui lòng nhập Họ tên." },
        { "UT-ACC-04 Họ tên 161 ký tự", Valid(fullName: new string('A', 161)), 400, "Họ tên không được vượt quá 160 ký tự." },
        { "UT-ACC-05 Email và số điện thoại đều trống", Valid(email: "", phone: ""), 400, "Vui lòng nhập Email hoặc số điện thoại." },
        { "UT-ACC-06 Email sai định dạng", Valid(email: "an-at-dcms"), 400, "Email không đúng định dạng." },
        { "UT-ACC-07 Số điện thoại có chữ", Valid(phone: "09abc"), 400, "Số điện thoại không đúng định dạng." },
        { "UT-ACC-08 Danh sách vai trò rỗng", Valid(roles: []), 403, "Bạn không có quyền thực hiện thao tác này." },
        { "UT-ACC-09 Mã nhân viên để trống", Valid(employeeCode: ""), 400, "Mã nhân viên không đúng định dạng." },
        { "UT-ACC-10 Mật khẩu để trống", Valid(password: ""), 400, "Mật khẩu không đúng định dạng." },
        { "UT-ACC-11 Mật khẩu 257 ký tự", Valid(password: new string('a', 257)), 400, "Mật khẩu không đúng định dạng." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Create_rejects_invalid_input(string caseName, CreateStaffRequest request, int status, string message)
    {
        using var db = CreateTestKit.NewDb();
        var users = CreateTestKit.NewUserManager(db);
        var service = new AccountService(db, users, new CreateTestKit.AllowAll());

        var error = await CreateTestKit.ErrorOf(() => service.CreateAsync(1, request, default));

        Assert.Equal(status, error.Status);
        Assert.Equal(message, error.Message);
    }

}
