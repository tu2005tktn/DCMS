namespace DCMS.Application.Common;

// Exact Vietnamese wording from the v1.2 specification, section 3.
public sealed record SystemMessage(string Code, string Message);

public static class SystemMessages
{
    public static SystemMessage Get(string code, string field = "Thông tin", int maxLength = 0, string? value = null) => new(code, code switch
    {
        "MSG22" => "Không tìm thấy khung giờ phù hợp. Vui lòng đổi ngày, bác sĩ hoặc dịch vụ.",
        "MSG23" => "Vui lòng nhập thông tin người khám thực tế.",
        "MSG24" => "Lịch đặt công khai phải bắt đầu sau ít nhất 60 phút. Vui lòng chọn giờ khác.",
        "MSG25" => "Khung giờ bạn chọn không còn khả dụng. Vui lòng chọn khung giờ khác.",
        "MSG26" => "Không còn khoảng thời gian liên tiếp đủ cho lịch hẹn. Vui lòng chọn giờ khác.",
        "MSG27" => "Thời gian này trùng với lịch hẹn đã có. Vui lòng chọn giờ khác.",
        "MSG28" => "Khung giờ này đã hết chỗ. Vui lòng chọn giờ khác.",
        "MSG29" => "Đặt lịch thành công. Vui lòng lưu mã lịch hẹn để tra cứu.",
        "MSG30" => "Yêu cầu đặt lịch này đã được xử lý. Hệ thống trả lại lịch hẹn đã tạo.",
        "MSG40" => "Không còn khung giờ phù hợp trong ngày. Vui lòng chọn ngày khác.",
        "MSG94" => "Lịch hẹn đã được xác nhận nhưng thông báo chưa gửi được. Nhân viên sẽ kiểm tra.",
        "MSG38" => "Thay đổi này ảnh hưởng đến các lịch hẹn đã xác nhận. Vui lòng xử lý các lịch liên quan trước khi cập nhật.",
        "MSG39" => "Đã cập nhật lịch làm việc. Hệ thống sẽ hiển thị các khung giờ đặt lịch phù hợp.",
        "MSG110" => "Giờ làm việc hoặc giờ nghỉ chưa hợp lệ. Vui lòng kiểm tra thời gian bắt đầu, kết thúc và các khoảng bị trùng.",
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
        "MSG11" => "Tạo tài khoản thành công. Bạn có thể đặt lịch cho mình hoặc người thân.",
        "MSG14" => "Đã cập nhật tài khoản thành công.",
        "MSG15" => "Đã khóa tài khoản. Quyền truy cập từ các phiên hiện tại đã bị vô hiệu hóa.",
        "MSG16" => "Đã mở khóa tài khoản thành công.",
        "MSG17" => "Bạn có thể yêu cầu liên kết hồ sơ sau khi đến khám và được tiếp nhận lần đầu tại phòng khám.",
        "MSG18" => "Đã gửi yêu cầu liên kết hồ sơ. Phòng khám sẽ xác minh thông tin trước khi cấp quyền truy cập.",
        "MSG19" => "Tài khoản của bạn đã được liên kết với hồ sơ của bạn.",
        "MSG20" => "Yêu cầu liên kết chưa được chấp thuận hoặc liên kết đã bị thu hồi. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG109" => "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.",
        "MSG111" => "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.",
        "MSG113" => "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.",
        "MSG115" => "Không tìm thấy hồ sơ bệnh nhân phù hợp.",
        "MSG116" => "Đã cập nhật thông tin hành chính của bệnh nhân.",
        "MSG139" => "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.",
        "MSG142" => "Tài khoản hoặc hồ sơ đã có liên kết tự thân đang hiệu lực. Vui lòng liên hệ phòng khám để kiểm tra.",
        "MSG143" => "Chưa đủ căn cứ xác minh tài khoản với hồ sơ tự thân. Vui lòng bổ sung hoặc đối chiếu thông tin nhận diện.",
        "MSG144" => "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.",
        // v2.0 operational messages; {entity} uses `field`, {code} uses `value`.
        "MSG201" => $"Mã {field} đã tồn tại. Vui lòng chọn mã khác.",
        "MSG202" => "Bản ghi đã ngừng sử dụng. Vui lòng khôi phục hoặc chọn bản ghi hợp lệ.",
        "MSG222" => $"Đã ghi nhận {field} với mã {value}.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    });
}

public sealed class BusinessException(int status, SystemMessage response) : Exception(response.Message)
{
    public int Status { get; } = status;
    public SystemMessage Response { get; } = response;
}
