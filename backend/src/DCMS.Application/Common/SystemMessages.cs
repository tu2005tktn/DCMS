namespace DCMS.Application.Common;

// Exact Vietnamese wording from the v1.2 specification, section 3.
public sealed record SystemMessage(string Code, string Message)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }
}

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
        "MSG31" => "Không thể tự đổi hoặc hủy lịch này. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG32" => "Vui lòng liên hệ phòng khám, cung cấp mã lịch hẹn và xác minh kênh liên hệ đã dùng khi đặt lịch.",
        "MSG33" => "Đã gửi yêu cầu hỗ trợ. Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công.",
        "MSG34" => "Không thể đổi sang khung giờ đã chọn. Lịch hẹn cũ của bạn vẫn được giữ nguyên.",
        "MSG35" => $"Đã đổi lịch hẹn sang {value}. Mọi thông báo nhắc lịch sẽ theo giờ hẹn mới.",
        "MSG37" => $"Đã hủy lịch hẹn {value}.",
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
        "MSG11" => "Tạo tài khoản thành công. Bạn có thể đặt lịch cho mình hoặc người thân.",
        "MSG14" => "Đã cập nhật tài khoản thành công.",
        "MSG15" => "Đã khóa tài khoản. Quyền truy cập từ các phiên hiện tại đã bị vô hiệu hóa.",
        "MSG16" => "Đã mở khóa tài khoản thành công.",
        "MSG17" => "Bạn có thể yêu cầu liên kết hồ sơ sau khi đến khám và được tiếp nhận lần đầu tại phòng khám.",
        "MSG18" => "Đã gửi yêu cầu liên kết hồ sơ. Phòng khám sẽ xác minh thông tin trước khi cấp quyền truy cập.",
        "MSG19" => "Tài khoản của bạn đã được liên kết với hồ sơ của bạn.",
        "MSG20" => "Yêu cầu liên kết chưa được chấp thuận hoặc liên kết đã bị thu hồi. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG12" => "Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.",
        "MSG13" => "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
        "MSG108" => "Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.",
        "MSG109" => "Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.",
        "MSG102" => "Bạn chưa có quyền xem thông tin này. Vui lòng liên hệ phòng khám để được hỗ trợ.",
        "MSG42" => "Lịch hẹn không đủ điều kiện tiếp nhận trong ngày hiện tại. Vui lòng kiểm tra hoặc xếp lịch hợp lệ.",
        "MSG43" => "Thông tin người đến khám không khớp hồ sơ hoặc lịch đã chọn. Vui lòng đối chiếu lại.",
        "MSG44" => "Có hồ sơ có thể trùng. Vui lòng đối chiếu trước khi tạo mới.",
        "MSG47" => "Lịch hẹn hoặc nguồn lực không còn hợp lệ để tiếp nhận. Vui lòng kiểm tra và xếp lại lịch.",
        "MSG114" => "Bệnh nhân đã được check-in. Mở lượt khám hiện có.",
        "MSG237" => "Đã tiếp nhận; bác sĩ cần xác nhận nguồn và nội dung chuyên môn.",
        "MSG111" => "Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.",
        "MSG127" => "Chỉ định quay lại đã có lịch hẹn đang hiệu lực. Vui lòng kiểm tra lịch hiện tại.",
        "MSG113" => "Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.",
        "MSG115" => "Không tìm thấy hồ sơ bệnh nhân phù hợp.",
        "MSG116" => "Đã cập nhật thông tin hành chính của bệnh nhân.",
        "MSG122" => "Vui lòng ghi lý do trước khi mở lại hồ sơ điều trị.",
        "MSG203" => "Không thể đánh dấu hạng mục chưa thực hiện là đã hoàn tất.",
        "MSG205" => "Danh mục đã được sử dụng. Bạn có thể ngừng áp dụng cho giao dịch mới.",
        "MSG229" => "Các bước điều trị hoặc thời gian dự kiến chưa hợp lệ. Vui lòng kiểm tra kế hoạch.",
        "MSG230" => "Điều kiện để thực hiện bước điều trị chưa đáp ứng. Vui lòng kiểm tra lại.",
        "MSG231" => "Đã lưu chỉ định quay lại. Chưa có lịch hẹn được đặt.",
        "MSG233" => "Thay đổi kế hoạch ảnh hưởng buổi tiếp theo hoặc lịch đã đặt. Vui lòng xem và xử lý các mục liên quan.",
        "MSG234" => "Bước điều trị đã đặt lịch nay bị chặn. Vui lòng kiểm tra lịch liên quan với phòng khám.",
        "MSG235" => "Chưa có khung giờ phù hợp. Chỉ định quay lại vẫn được giữ để phòng khám xử lý.",
        "MSG236" => "Mẫu điều trị đã thay đổi hoặc ngừng sử dụng. Vui lòng kiểm tra lại phiên bản.",
        "MSG241" => "Đã lưu thay đổi tạm dừng bước điều trị.",
        "MSG239" => $"Thông tin cần bác sĩ kiểm tra: {field}. Vui lòng ghi đánh giá và hướng xử trí.",
        "MSG240" => "Nội dung đồng ý chưa khớp phương án hiện tại. Vui lòng tư vấn và ghi nhận lại quyết định trước khi thực hiện.",
        "MSG243" => "Còn hạng mục chưa được xử lý khi kết thúc kế hoạch. Vui lòng ghi kết quả và lý do cho phần chưa hoàn tất.",
        "MSG244" => "Vui lòng ghi căn cứ bảo hành hoặc miễn phí và phạm vi hạng mục được áp dụng.",
        "MSG245" => "Phương án đã thay đổi. Vui lòng kiểm tra khoản đã ghi nhận, công nợ và số dư trước khi điều chỉnh; hệ thống không tự hoàn tiền.",
        "MSG249" => "Đã ghi nhận quyết định của bệnh nhân. Hạng mục chưa đồng ý sẽ không được thực hiện; có thể kết thúc lượt tư vấn.",
        "MSG252" => "Vui lòng ghi mức giá hoặc giảm trừ đã thỏa thuận, lý do và hạng mục áp dụng. Khoản đã chốt cần điều chỉnh theo đúng quyền.",
        "MSG255" => $"{field} chưa được phòng khám xác nhận hoặc cấu hình. Chưa tạo dịch vụ, lịch hẹn hay thông báo gửi ra ngoài.",
        "MSG256" => "Phân bổ thanh toán chưa khớp số tiền nhận hoặc vượt số dư có thể sử dụng. Vui lòng kiểm tra từng dòng và phần tiền ứng trước khi xác nhận.",
        "MSG257" => "Giá dịch vụ không được âm. Giảm giá, voucher hoặc khấu trừ vui lòng ghi nhận bằng chương trình khuyến mãi hoặc giảm trừ có lý do.",
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
