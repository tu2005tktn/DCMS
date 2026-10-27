# Giao diện cổng bệnh nhân Web — bản cải thiện 19/10/2026

## Phạm vi và nguyên tắc

- Cải thiện giao diện của `/access` và các trang `/customer/*` hiện có: đăng nhập, đăng ký, khôi phục, trang chủ, đặt lịch sau đăng nhập, danh sách và chi tiết lịch, đổi/hủy lịch, liên kết hồ sơ. Ưu tiên luồng đặt và quản lý lịch.
- Giữ nhận diện DCMS xanh dương, font Inter và Ant Design. Chỉ áp dụng token/CSS trong cổng bệnh nhân; trang `/sign-in` và khu vực nhân viên giữ nguyên.
- Không đổi quyền phía server, quy tắc nghiệp vụ đặt lịch, API, payload hoặc cách quản lý phiên. Web yêu cầu đăng nhập Public trước khi vào form đặt lịch. Đặt lịch cho người thân không cấp quyền đọc hồ sơ của họ. Giờ hẹn luôn được ghi rõ là giờ Việt Nam.

## Cấu trúc và trạng thái giao diện

| Luồng | Nội dung ưu tiên | Trạng thái và hành động |
| --- | --- | --- |
| Chọn cổng | Hai lựa chọn Khách hàng/Nhân viên dễ phân biệt; khách hàng đăng nhập hoặc đăng ký trước khi đặt lịch | Link có tên rõ, focus bàn phím và vùng bấm thuận tiện. |
| Tài khoản | Một hành động chính ở từng bước; nhãn, hướng dẫn, lỗi từng trường và đường quay lại | Đang gửi, lỗi mạng/validation, kết quả đăng ký; Google và khôi phục giữ nguyên hợp đồng hiện có. Khôi phục phản hồi chung, không lộ tài khoản. |
| Trang chủ | Đặt lịch khám là hành động chính; lịch hẹn và liên kết hồ sơ là lối vào kế tiếp | Điều hướng và đăng xuất luôn tìm được trên màn hình nhỏ. |
| Đặt lịch | Người khám → chi nhánh/dịch vụ/bác sĩ/ngày/slot → người đặt và người khám → xem lại → xác nhận | Hiển thị slot theo sáng/chiều, trạng thái chọn rõ, loading/không có slot/lỗi mạng riêng. Giữ dữ liệu khi slot bị tranh hoặc cần chọn lại; giữ `requestKey` theo hợp đồng hiện hành. Kết quả nhấn mạnh mã lịch, giờ, trạng thái Confirmed và bước tiếp theo. |
| Lịch hẹn | Danh sách ưu tiên giờ, người khám, bác sĩ, trạng thái; chi tiết ưu tiên lịch hiện tại và hành động hợp lệ | Desktop dùng bảng, màn hình nhỏ dùng thẻ. Trạng thái có chữ. >=12 giờ đổi/hủy trực tiếp; dưới 12 giờ gửi yêu cầu chờ nhân viên, lịch cũ vẫn hiệu lực. Pending có giờ đề xuất và đường rút yêu cầu. Không có quyền, stale, conflict, loading, empty và lỗi kết nối có nội dung/hướng xử lý riêng. |
| Liên kết hồ sơ | Trạng thái hiện tại, hành động phù hợp, sau đó mới tới biểu mẫu và lịch sử | Chỉ hiển thị tóm tắt hồ sơ của chính tài khoản VERIFIED; không ngụ ý đặt lịch hộ sẽ cấp quyền đọc hồ sơ. |

## Giao diện dùng chung và kiểm tra

### Mẫu B cho bước chọn lịch khám (19/10/2026)

- Áp dụng cho **bước 2/5** của `/customer/book`, chỉ sau khi tài khoản Public đăng nhập. Trên desktop, bộ lọc chi nhánh, dịch vụ, bác sĩ, ngày khám nằm trong cột trái; danh sách khung giờ là vùng chính ở cột phải. Trên màn hình nhỏ, bộ lọc gọn ở trên, khung giờ sáng/chiều ở ngay sau đó; tóm tắt lựa chọn và hành động tiếp tục theo sau. Các trường và bước khác giữ nguyên.
- Khung giờ nhóm theo buổi sáng/chiều, nút giờ có trạng thái chọn bằng chữ/viền/màu, vùng bấm tối thiểu khoảng 44 px. Ghi rõ “Giờ Việt Nam”. Chỉ hiển thị giờ API trả về; không đưa giờ mẫu trong thiết kế vào dữ liệu thật.
- Tóm tắt chỉ phản ánh chi nhánh, dịch vụ, bác sĩ, ngày và giờ **đã chọn**. Giờ chưa chọn được ghi rõ; không trình bày tóm tắt như lịch đã xác nhận. Lý do khám không bắt buộc vẫn nhập được trong bước này nhưng không đẩy danh sách giờ ra xa trên màn hình nhỏ.
- Giữ trạng thái tải, chưa đủ bộ lọc, không có slot và lỗi kết nối với hướng xử lý rõ. Khi slot bị tranh, giữ thông tin đã nhập, tải lại giờ và yêu cầu chọn giờ mới. Việc chọn giờ vẫn giữ nguyên requestKey/payload và kiểm tra server hiện có.
- Mockup là tham chiếu bố cục, không phải dữ liệu lịch thực và không mở thêm chức năng hoặc route. Chỉ thay giao diện Web bệnh nhân; không sửa backend, API, quyền hay màn hình nhân viên.

- Dùng xanh `#175CD3`, chữ `#17233D`, nền `#F4F7FB`, nền nhấn `#EAF2FF`, thành công `#15803D`, lỗi `#B42318`. Nhãn trường, nút chính, trạng thái chọn, focus, thông báo và khoảng cách nhất quán. Modal Ant Design cần class riêng vì được render ngoài cây `.customer-portal`.
- Ở khoảng 390, 768 và 1440 px: không tràn ngang, điều hướng và hành động chính còn nhìn thấy/dùng được. Focus rõ; thao tác bàn phím, phóng to 200%, reduced motion và vùng bấm khoảng 44 px. Không chỉ dùng màu để biểu thị trạng thái.
- Playwright dùng API giả lập để xác nhận người chưa đăng nhập không vào được `/customer/book`, tài khoản Public đã đăng nhập đặt lịch được, cùng các ca slot rỗng/bị tranh, lỗi mạng, lịch hẹn, đổi/hủy/yêu cầu hỗ trợ và liên kết hồ sơ. Build Web và kiểm tra hồi quy `/sign-in` nhân viên. Không tạo dữ liệu thử trong database dự án.

## Hợp đồng không thay đổi

- Quyết định Web ngày 19/10/2026: bỏ lối đặt lịch không đăng nhập ở `/access` và trang đăng nhập khách hàng. `/customer/book` yêu cầu phiên Public hợp lệ; truy cập trực tiếp khi chưa đăng nhập hoặc sau khi phiên hết hạn chuyển tới `/customer/sign-in` trước khi tải dữ liệu đặt lịch. Sau đăng nhập người dùng vào trang chủ rồi bấm **Bắt đầu đặt lịch**. Chỉ áp dụng Web bệnh nhân; API đặt lịch Guest và luồng Mobile không đổi.

- Đăng ký/đăng nhập/khôi phục tiếp tục dùng API auth hiện có; đặt lịch dùng `/api/availability/options`, `/api/availability`, `POST /api/appointments`; quản lý lịch và liên kết hồ sơ dùng các endpoint đang có. Không thêm endpoint hay trường request/response.
- Chi tiết quy tắc lịch và thông điệp lỗi theo [CUSTOMER-APPOINTMENTS-WEB.md](CUSTOMER-APPOINTMENTS-WEB.md); phần giao diện này chỉ thay cách trình bày, không đổi kết quả server.
