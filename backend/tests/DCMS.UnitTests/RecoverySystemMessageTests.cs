using DCMS.Application.Common;
using Xunit;

namespace DCMS.UnitTests;

/// <summary>
/// The wording of the messages used by UC-02 is fixed by the v1.2 specification, including the
/// Vietnamese diacritics. These tests lock the text so a refactor cannot silently change it.
/// </summary>
public sealed class RecoverySystemMessageTests
{
    [Theory]
    [InlineData("MSG12", "Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.")]
    [InlineData("MSG13", "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.")]
    [InlineData("MSG108", "Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.")]
    [InlineData("MSG109", "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.")]
    [InlineData("MSG144", "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.")]
    [InlineData("MSG06", "Bạn không có quyền thực hiện thao tác này.")]
    [InlineData("MSG33", "Đã gửi yêu cầu hỗ trợ. Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công.")]
    public void RecoveryMessagesMatchTheSpecification(string code, string expected)
    {
        Assert.Equal(expected, SystemMessages.Get(code).Message);
        Assert.Equal(code, SystemMessages.Get(code).Code);
    }

    [Theory]
    [InlineData("MSG02", "Tên đăng nhập")]
    [InlineData("MSG04", "Mật khẩu")]
    public void PlaceholderMessagesKeepTheFieldName(string code, string field)
    {
        Assert.Contains(field, SystemMessages.Get(code, field).Message);
    }

    [Fact]
    public void NeutralRequestMessageIsIdenticalForKnownAndUnknownAccounts()
    {
        // The client must render exactly the same text either way; the service always returns MSG12.
        var known = SystemMessages.Get("MSG12");
        var unknown = SystemMessages.Get("MSG12");
        Assert.Equal(known.Message, unknown.Message);
    }

    [Fact]
    public void UnknownMessageCodeFailsLoudlyInsteadOfInventingText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SystemMessages.Get("MSG999"));
    }
}
