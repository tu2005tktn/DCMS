using DCMS.Application.Features.Authentication;
using DCMS.Infrastructure.Features.Authentication;
using Xunit;

namespace DCMS.UnitTests.Create;

/// <summary>RegistrationService.RegisterAsync (create a customer account from the public register form).</summary>
public sealed class CustomerRegisterTests
{
    private static RegisterRequest Valid(string? fullName = "Lê Văn Nam", string? phone = "0987654321", string? email = "nam@gmail.com",
        string? password = "Abcdef1!", string? confirm = "Abcdef1!") => new(fullName, phone, email, password, confirm);

    public static TheoryData<string, RegisterRequest, string> Rejected => new()
    {
        { "UT-REG-01 Họ tên để trống", Valid(fullName: ""), "Vui lòng nhập Họ tên." },
        { "UT-REG-02 Họ tên 161 ký tự", Valid(fullName: new string('A', 161)), "Họ tên không được vượt quá 160 ký tự." },
        { "UT-REG-03 Số điện thoại để trống", Valid(phone: ""), "Vui lòng nhập Số điện thoại." },
        { "UT-REG-04 Số điện thoại không phải số di động Việt Nam", Valid(phone: "12345"), "Số điện thoại không đúng định dạng." },
        { "UT-REG-05 Email để trống", Valid(email: ""), "Vui lòng nhập Email." },
        { "UT-REG-06 Email 255 ký tự", Valid(email: new string('a', 250) + "@x.vn"), "Email không được vượt quá 254 ký tự." },
        { "UT-REG-07 Email thiếu ký tự @", Valid(email: "nam-gmail.com"), "Email không đúng định dạng." },
        { "UT-REG-08 Mật khẩu để trống", Valid(password: "", confirm: ""), "Vui lòng nhập Mật khẩu." },
        { "UT-REG-09 Mật khẩu 257 ký tự", Valid(password: new string('a', 257), confirm: new string('a', 257)), "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại." },
        { "UT-REG-10 Xác nhận mật khẩu để trống", Valid(confirm: ""), "Vui lòng nhập Xác nhận mật khẩu." },
        { "UT-REG-11 Xác nhận mật khẩu không khớp", Valid(confirm: "Abcdef1?"), "Xác nhận mật khẩu không đúng định dạng." },
    };

    [Theory, MemberData(nameof(Rejected))]
    public async Task Register_rejects_invalid_input(string caseName, RegisterRequest request, string message)
    {
        using var db = CreateTestKit.NewDb();
        var service = new RegistrationService(db, CreateTestKit.NewUserManager(db), null!);

        var error = await CreateTestKit.ErrorOf(() => service.RegisterAsync(request, default));

        Assert.Equal(400, error.Status);
        Assert.Equal(message, error.Message);
    }
}
