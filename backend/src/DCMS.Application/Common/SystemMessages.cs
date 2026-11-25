namespace DCMS.Application.Common;

// Exact Vietnamese wording from the v1.2 specification, section 3.
public sealed record SystemMessage(string Code, string Message)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? CurrentVersion { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object>? Blockers { get; init; }
}

public static class SystemMessages
{
    public static SystemMessage Get(string code, string field = "Thông tin", int maxLength = 0, string? value = null) => new(code, code switch
    {
        "MSG18" => "Vui lòng nhập thông tin người khám thực tế.",
        "MSG19" => "Lịch đặt công khai phải bắt đầu sau ít nhất 60 phút. Vui lòng chọn giờ khác.",
        "MSG20" => "Khung giờ bạn chọn không còn khả dụng. Vui lòng chọn khung giờ khác.",
        "MSG21" => "Không còn khoảng thời gian liên tiếp đủ cho lịch hẹn. Vui lòng chọn giờ khác.",
        "MSG22" => "Thời gian này trùng với lịch hẹn đã có. Vui lòng chọn giờ khác.",
        "MSG23" => "Khung giờ này đã hết chỗ. Vui lòng chọn giờ khác.",
        "MSG24" => "Đặt lịch thành công. Vui lòng lưu mã lịch hẹn để tra cứu.",
        "MSG25" => "Yêu cầu đặt lịch này đã được xử lý. Hệ thống trả lại lịch hẹn đã tạo.",
        "MSG26" => "Không thể tự đổi hoặc hủy lịch này. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG27" => "Vui lòng liên hệ phòng khám, cung cấp mã lịch hẹn và xác minh kênh liên hệ đã dùng khi đặt lịch.",
        "MSG28" => "Đã gửi yêu cầu hỗ trợ. Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công.",
        "MSG29" => "Không thể đổi sang khung giờ đã chọn. Lịch hẹn cũ của bạn vẫn được giữ nguyên.",
        "MSG30" => $"Đã đổi lịch hẹn sang {value}. Mọi thông báo nhắc lịch sẽ theo giờ hẹn mới.",
        "MSG31" => $"Đã hủy lịch hẹn {value}.",
        "MSG32" => "Thay đổi này ảnh hưởng đến các lịch hẹn đã xác nhận. Vui lòng xử lý các lịch liên quan trước khi cập nhật.",
        "MSG33" => "Đã cập nhật lịch làm việc. Hệ thống sẽ hiển thị các khung giờ đặt lịch phù hợp.",
        "MSG40" => "Giờ làm việc hoặc giờ nghỉ chưa hợp lệ. Vui lòng kiểm tra thời gian bắt đầu, kết thúc và các khoảng bị trùng.",
        "MSG01" => "Không tìm thấy kết quả phù hợp.",
        "MSG02" => $"Vui lòng nhập {field}.",
        "MSG03" => $"{field} không được vượt quá {maxLength} ký tự.",
        "MSG04" => $"{field} không đúng định dạng.",
        "MSG05" => "Bạn không có quyền thực hiện thao tác này.",
        "MSG06" => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        "MSG07" => "Thông tin đăng nhập không đúng hoặc tài khoản không thể sử dụng. Vui lòng kiểm tra và thử lại.",
        "MSG08" => "Tạo tài khoản thành công. Bạn có thể đặt lịch cho mình hoặc người thân.",
        "MSG11" => "Đã cập nhật tài khoản thành công.",
        "MSG12" => "Đã khóa tài khoản. Quyền truy cập từ các phiên hiện tại đã bị vô hiệu hóa.",
        "MSG13" => "Đã mở khóa tài khoản thành công.",
        "MSG14" => "Bạn có thể yêu cầu liên kết hồ sơ sau khi đến khám và được tiếp nhận lần đầu tại phòng khám.",
        "MSG15" => "Đã gửi yêu cầu liên kết hồ sơ. Phòng khám sẽ xác minh thông tin trước khi cấp quyền truy cập.",
        "MSG16" => "Tài khoản của bạn đã được liên kết với hồ sơ của bạn.",
        "MSG17" => "Yêu cầu liên kết chưa được chấp thuận hoặc liên kết đã bị thu hồi. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG09" => "Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.",
        "MSG10" => "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
        "MSG38" => "Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.",
        "MSG39" => "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.",
        "MSG37" => "Bạn chưa có quyền xem thông tin này. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG34" => "Lịch hẹn không đủ điều kiện tiếp nhận trong ngày hiện tại. Vui lòng kiểm tra hoặc xếp lịch hợp lệ.",
        "MSG35" => "Thông tin người đến khám không khớp hồ sơ hoặc lịch đã chọn. Vui lòng đối chiếu lại.",
        "MSG36" => "Lịch hẹn hoặc nguồn lực không còn hợp lệ để tiếp nhận. Vui lòng kiểm tra và xếp lại lịch.",
        "MSG41" => "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.",
        "MSG42" => "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.",
        "MSG43" => "Không tìm thấy hồ sơ bệnh nhân phù hợp.",
        "MSG44" => "Đã cập nhật thông tin hành chính của bệnh nhân.",
        "MSG45" => "Vui lòng ghi lý do trước khi mở lại hồ sơ điều trị.",
        "MSG52" => "Danh mục đã được sử dụng. Bạn có thể ngừng áp dụng cho giao dịch mới.",
        "MSG54" => "Điều kiện để thực hiện bước điều trị chưa đáp ứng. Vui lòng kiểm tra lại.",
        "MSG55" => "Thay đổi kế hoạch ảnh hưởng buổi tiếp theo hoặc lịch đã đặt. Vui lòng xem và xử lý các mục liên quan.",
        "MSG56" => $"Thông tin cần bác sĩ kiểm tra: {field}. Vui lòng ghi đánh giá và hướng xử trí.",
        "MSG57" => "Nội dung đồng ý chưa khớp phương án hiện tại. Vui lòng tư vấn và ghi nhận lại quyết định trước khi thực hiện.",
        "MSG58" => "Còn hạng mục chưa được xử lý khi kết thúc kế hoạch. Vui lòng ghi kết quả và lý do cho phần chưa hoàn tất.",
        "MSG59" => "Vui lòng ghi mức giá hoặc giảm trừ đã thỏa thuận, lý do và hạng mục áp dụng. Khoản đã chốt cần điều chỉnh theo đúng quyền.",
        "MSG60" => "Giá dịch vụ không được âm. Giảm giá, voucher hoặc khấu trừ vui lòng ghi nhận bằng chương trình khuyến mãi hoặc giảm trừ có lý do.",
        "MSG46" => "Thông tin tài khoản chưa thể sử dụng. Vui lòng kiểm tra hoặc dùng chức năng khôi phục tài khoản.",
        "MSG47" => "Tài khoản hoặc hồ sơ đã có liên kết tự thân đang hiệu lực. Vui lòng liên hệ phòng khám để kiểm tra.",
        "MSG48" => "Chưa đủ căn cứ xác minh tài khoản với hồ sơ tự thân. Vui lòng bổ sung hoặc đối chiếu thông tin nhận diện.",
        "MSG49" => "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.",
        // v2.0 operational messages; {entity} uses `field`, {code} uses `value`.
        "MSG50" => $"Mã {field} đã tồn tại. Vui lòng chọn mã khác.",
        "MSG51" => "Bản ghi đã ngừng sử dụng. Vui lòng khôi phục hoặc chọn bản ghi hợp lệ.",
        "MSG53" => $"Đã ghi nhận {field} với mã {value}.",
        // Payment and PayOS messages (MSG93-MSG116), docs/specs/SYSTEM-MESSAGE-CODES.md.
        "MSG93" => "Thông tin thu tiền không hợp lệ.",
        "MSG94" => "Số tiền không hợp lệ.",
        "MSG95" => "Số tiền vượt giới hạn.",
        "MSG96" => "Số tiền cọc không hợp lệ.",
        "MSG97" => "Khoản phân bổ cọc không hợp lệ.",
        "MSG98" => "Cần nhập lý do khách không đến.",
        "MSG99" => "Cần nhập lý do hủy phiếu.",
        "MSG100" => "Mã yêu cầu không hợp lệ.",
        "MSG101" => "Khoảng ngày hoặc chi nhánh không hợp lệ.",
        "MSG102" => "Dịch vụ hoặc số còn phải thu đã thay đổi. Vui lòng tải lại.",
        "MSG103" => "Dịch vụ hoặc số tiền cần thu đã thay đổi.",
        "MSG104" => "Cần ưu tiên đủ 30% phí labo trước khi thu cho dịch vụ khác.",
        "MSG105" => "Cần ưu tiên đủ 30% phí labo trước khi phân bổ cọc cho dịch vụ khác.",
        "MSG106" => "Số dư cọc không đủ. Vui lòng tải lại.",
        "MSG107" => "Phiếu đã thay đổi. Vui lòng tải lại.",
        "MSG108" => "Phiếu đã có điều trị hoặc labo, không thể xóa do khách không đến.",
        "MSG109" => "Phiếu không có số dư cọc để ghi nhận bỏ cọc.",
        "MSG110" => "Phiếu đã sử dụng cọc hoặc có giao dịch dịch vụ.",
        "MSG111" => "Phiếu còn QR chuyển khoản chưa đối soát.",
        "MSG112" => "Đã có mã QR chưa hết hạn. Hãy mở lại mã QR hiện tại.",
        "MSG113" => "Mã yêu cầu đã được dùng.",
        "MSG114" => "Mã yêu cầu đã được dùng cho một khoản thu khác.",
        "MSG118" => "Người này đã có một lịch hẹn chưa được tiếp nhận. Vui lòng hủy lịch cũ hoặc đợi đến khi lịch đó được tiếp nhận trước khi đặt lịch mới.",
        "MSG117" => "Không đủ ghế để đăng ký khung giờ này. Vui lòng chọn khung giờ khác hoặc liên hệ quản lý phòng khám.",
        "MSG115" => "Mã yêu cầu đã được dùng cho một khoản phân bổ khác.",
        "MSG116" => "Mã yêu cầu đã được dùng cho thao tác khác.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    });
}

public sealed class BusinessException(int status, SystemMessage response) : Exception(response.Message)
{
    public int Status { get; } = status;
    public SystemMessage Response { get; } = response;
}
