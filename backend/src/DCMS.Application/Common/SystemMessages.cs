namespace DCMS.Application.Common;

// Exact Vietnamese wording from the v1.2 specification, section 3.
public sealed record SystemMessage(string Code, string Message);

public static class SystemMessages
{
    public static SystemMessage Get(string code, string field = "Thông tin", int maxLength = 0) => new(code, code switch
    {
        "MSG01" => "Không tìm thấy kết quả phù hợp.",
        "MSG02" => $"Vui lòng nhập {field}.",
        "MSG03" => $"{field} không được vượt quá {maxLength} ký tự.",
        "MSG04" => $"{field} không đúng định dạng.",
        "MSG06" => "Bạn không có quyền thực hiện thao tác này.",
        "MSG07" => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        "MSG09" => "Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng. Vui lòng kiểm tra và thử lại.",
        "MSG12" => "Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.",
        "MSG13" => "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
        "MSG14" => "Đã cập nhật tài khoản thành công.",
        "MSG15" => "Đã khóa tài khoản. Quyền truy cập từ các phiên hiện tại đã bị vô hiệu hóa.",
        "MSG16" => "Đã mở khóa tài khoản thành công.",
        "MSG108" => "Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.",
        "MSG109" => "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.",
        "MSG111" => "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.",
        "MSG139" => "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.",
        "MSG144" => "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    });
}

public sealed class BusinessException(int status, SystemMessage response) : Exception(response.Message)
{
    public int Status { get; } = status;
    public SystemMessage Response { get; } = response;
}
