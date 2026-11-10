# Giao diện khách hàng trên mobile (10/11/2026)

## Mục tiêu

Tài khoản `Public` sử dụng ứng dụng như cổng khách hàng nha khoa: hiểu ngay việc cần làm, đặt lịch theo từng bước, quản lý lịch đã đặt, theo dõi bệnh án/kết quả khám/đơn thuốc sau khi liên kết hồ sơ được xác minh, và đọc thông báo lịch hẹn. Khách chưa đăng nhập có lối vào đăng nhập/đăng ký và đặt lịch Guest như luồng mobile hiện hành. Nhân viên vẫn vào màn nghiệp vụ riêng.

## Điều hướng và giao diện

- Dùng cùng ngôn ngữ thị giác với cổng khách hàng web: xanh `#175CD3`, chữ `#17233D`, nền `#F4F7FB`, thẻ trắng, trạng thái chọn rõ, vùng chạm ít nhất 44 px. Các màn có tiêu đề ngắn, mô tả hành động và lỗi/tải/trống khác biệt. Không hiển thị mã S01/S02/S26, `mock`, `API` hoặc nhãn triển khai cho khách hàng.
- Màn đăng nhập: trường `Số điện thoại hoặc tên đăng nhập` (backend nhận identifier hiện hành), mật khẩu, tạo tài khoản, quên mật khẩu và Google theo cấu hình hiện có. Đặt lịch Guest vẫn có lối vào. Không sửa hợp đồng xác thực.
- Màn đăng ký: nêu rõ số điện thoại 10 chữ số dùng để đăng nhập; form nhóm theo thông tin liên hệ/mật khẩu, lỗi gần trường, nút đăng ký rõ ràng. Không sửa quy tắc policy và submit.
- Màn đặt lịch: tiến trình ba bước Chọn lịch → Thông tin → Xác nhận; nhãn chọn dễ đọc, thời gian ghi rõ giờ Việt Nam, tóm tắt trước khi gửi và trạng thái thành công có mã lịch. Nếu chưa tải được danh mục, chỉ hiển thị lỗi và nút tải lại, không hiển thị form rỗng gây nhầm. Giữ nguyên dữ liệu gửi, tính hợp lệ, idempotency và hành vi slot hết chỗ.
- Màn lịch hẹn: tiêu đề, tab Sắp tới/Lịch sử, hành động đặt lịch nổi bật, lọc trạng thái/ngày ở vùng mở rộng; thẻ lịch dễ đọc, tiếp tục phân trang và dẫn tới chi tiết. Trạng thái lỗi không bị trình bày như danh sách trống.
- Các màn bệnh án, kết quả khám, đơn thuốc và thông báo dùng cùng màu, thẻ và typography; dữ liệu y tế chỉ qua API có quyền. Không đưa chẩn đoán vào thông báo. Các màn chi tiết hiện hành vẫn là nguồn sự thật.

## Kiểm tra

Typecheck mobile và build Android. Trên điện thoại: khách thấy lối vào đăng nhập/đăng ký/đặt lịch, người dùng Public vào trang chủ và các mục bằng một lần bấm; đặt lịch thử qua ba bước, mở lịch hẹn; liên kết chưa xác minh không lộ bệnh án; thông báo đọc qua API. Thử trạng thái API lỗi và danh sách trống. Không tạo hồ sơ hoặc đơn thuốc giả trong database.
# Giao diện đăng nhập (10/11/2026)

Màn đăng nhập mobile dùng bố cục nhận diện DCMS xanh và trắng, tiêu đề ngắn, biểu tượng răng, thẻ form dễ đọc, khoảng cách và vùng chạm phù hợp điện thoại. Giữ nguyên xác thực số điện thoại/tên đăng nhập và mật khẩu, Google, đăng ký, quên mật khẩu, đặt lịch không cần tài khoản; không thay đổi API hoặc logic kiểm tra lỗi.

## Điểm vào ứng dụng khi chưa đăng nhập (10/11/2026)

Sau khi khôi phục phiên, tài khoản đã xác thực vào trang chủ tương ứng. Khi chưa đăng nhập, route gốc và route Trang chủ chuyển thẳng đến Đăng nhập, không hiển thị màn chào/menu khách. Màn Đăng nhập liên kết tới Đăng ký và Quên mật khẩu; màn Đăng ký quay về Đăng nhập. Bỏ liên kết quay về màn chào và nút đặt lịch khách khỏi giao diện đăng nhập; không xóa API đặt lịch khách hoặc thay đổi xác thực. Đăng xuất cũng trở về Đăng nhập.
