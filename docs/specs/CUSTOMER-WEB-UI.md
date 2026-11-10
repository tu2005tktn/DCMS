# Giao diện cổng bệnh nhân Web — bản cải thiện 19/10/2026

## Người thân — 01/11/2026
Thêm route, nav và Dashboard shortcut Người thân của tôi; CRUD tên/quan hệ/ngày sinh và chọn nhanh trong booking theo CUSTOMER-FAMILY-MEMBERS.md. Giữ hệ màu/Ant Design, mobile cards và desktop table. Không đổi Patient/check-in/liên kết, nguồn slot hoặc quy tắc đặt lịch.

## Dashboard theo chức năng hiện có — 01/11/2026

- Trang `/customer/home` đổi bố cục thành lời chào, lịch hẹn gần nhất, thẻ đặt lịch và lối vào Lịch hẹn/Liên kết hồ sơ. Không thay backend, route, quyền hoặc quy trình đặt/đổi/hủy lịch.
- Dùng lại `GET /api/me/appointments?scope=UPCOMING&status=ALL&page=1&pageSize=20` như màn danh sách. Hiển thị phần tử đầu tiên theo thứ tự sắp tới do server trả về; không tự xác định quyền hoặc sắp lại trạng thái. Chỉ hiển thị các trường sẵn có của AppointmentCard: mã, thời gian, thời lượng, nha sĩ, người khám và trạng thái. Chi nhánh/dịch vụ không có trong DTO danh sách thì không suy đoán.
- Nút Xem lịch hẹn mở đúng `/customer/appointments/:code`; nút Xem tất cả mở danh sách hiện có. Không đưa thao tác đổi/hủy trực tiếp lên Dashboard.
- Có loading, không có lịch, lỗi tải và thử lại; 401 dùng cơ chế expire hiện có. Lỗi tải không hiển thị như không có lịch. Nút đặt lịch và liên kết hồ sơ vẫn dùng được khi tải danh sách lỗi.
- Chưa có nguồn thông báo riêng trong cổng hiện tại: không thêm panel thông báo, số đếm, nhắc lịch hoặc nội dung giả. Không thêm AI, sơ đồ răng, lịch sử điều trị, đơn thuốc hay bệnh án chi tiết. Liên kết hồ sơ vẫn là màn sẵn có, không tự biến thành quyền đọc bệnh án đầy đủ.
- CSS giới hạn trong Dashboard bệnh nhân; giữ đăng nhập/đăng ký/khôi phục, cổng nhân viên và các trang bệnh nhân khác. Responsive 390/768/1440; vùng bấm >=44px, focus rõ. Kiểm mock với danh sách/empty/lỗi/retry/401 và điều hướng; không tạo dữ liệu thử trong database.

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

### Rà soát UX bằng `ui-ux-pro-max` (19/10/2026)

- Giữ hệ màu xanh DCMS, Inter và bố cục đặt lịch đang dùng. Các gợi ý quảng bá như carousel lời chứng thực không áp dụng cho cổng bệnh nhân thực hiện giao dịch; không thêm nội dung giả hoặc đổi API.
- Ở bước chọn lịch, ngay cạnh ô ngày hiển thị rõ ngày đã chọn theo `ngày/tháng/năm` và ghi “Giờ Việt Nam”. Ô chọn ngày vẫn giữ thao tác lịch quen thuộc; phần tóm tắt và giờ hẹn dùng cùng định dạng địa phương.
- Ở bước thông tin người đặt/người khám, khi bấm tiếp tục mà dữ liệu chưa hợp lệ, hiển thị một vùng tóm tắt lỗi ở đầu bước, chuyển focus tới vùng này, có liên kết về từng trường lỗi và vẫn giữ lỗi ngay cạnh trường. Khi người dùng sửa trường, lỗi tương ứng được xóa; dữ liệu nhập không bị mất.
- Trên biểu mẫu đăng nhập và đăng ký bệnh nhân, lỗi kiểm tra nhiều trường cũng được tóm tắt ở đầu biểu mẫu, có liên kết tới trường cần sửa và chuyển focus tới tóm tắt sau khi gửi. Lỗi từng trường của Ant Design và dữ liệu đã nhập được giữ nguyên. Điều này không áp dụng cho cổng nhân viên.
- Trên màn hình nhỏ, tiến trình đặt lịch vẫn nêu rõ bước hiện tại trong tổng số 5 bước và có thanh tiến độ nhìn thấy được. Mọi trạng thái lỗi và tiến độ phải có chữ, không chỉ dựa vào màu. Kiểm tra bàn phím và bố cục tại 320, 390, 768 và 1440 px.

- Dùng xanh `#175CD3`, chữ `#17233D`, nền `#F4F7FB`, nền nhấn `#EAF2FF`, thành công `#15803D`, lỗi `#B42318`. Nhãn trường, nút chính, trạng thái chọn, focus, thông báo và khoảng cách nhất quán. Modal Ant Design cần class riêng vì được render ngoài cây `.customer-portal`.
- Ở khoảng 390, 768 và 1440 px: không tràn ngang, điều hướng và hành động chính còn nhìn thấy/dùng được. Focus rõ; thao tác bàn phím, phóng to 200%, reduced motion và vùng bấm khoảng 44 px. Không chỉ dùng màu để biểu thị trạng thái.
- Playwright dùng API giả lập để xác nhận người chưa đăng nhập không vào được `/customer/book`, tài khoản Public đã đăng nhập đặt lịch được, cùng các ca slot rỗng/bị tranh, lỗi mạng, lịch hẹn, đổi/hủy/yêu cầu hỗ trợ và liên kết hồ sơ. Build Web và kiểm tra hồi quy `/sign-in` nhân viên. Không tạo dữ liệu thử trong database dự án.

## Hợp đồng không thay đổi

- Quyết định Web ngày 19/10/2026: bỏ lối đặt lịch không đăng nhập ở `/access` và trang đăng nhập khách hàng. `/customer/book` yêu cầu phiên Public hợp lệ; truy cập trực tiếp khi chưa đăng nhập hoặc sau khi phiên hết hạn chuyển tới `/customer/sign-in` trước khi tải dữ liệu đặt lịch. Sau đăng nhập người dùng vào trang chủ rồi bấm **Bắt đầu đặt lịch**. Chỉ áp dụng Web bệnh nhân; API đặt lịch Guest và luồng Mobile không đổi.

- Đăng ký/đăng nhập/khôi phục tiếp tục dùng API auth hiện có; đặt lịch dùng `/api/availability/options`, `/api/availability`, `POST /api/appointments`; quản lý lịch và liên kết hồ sơ dùng các endpoint đang có. Không thêm endpoint hay trường request/response.
- Chi tiết quy tắc lịch và thông điệp lỗi theo [CUSTOMER-APPOINTMENTS-WEB.md](CUSTOMER-APPOINTMENTS-WEB.md); phần giao diện này chỉ thay cách trình bày, không đổi kết quả server.

## Thiết kế lại giao diện theo Figma — 10/11/2026

Áp dụng bản thiết kế Figma "DCMS - Staff Web UI" (trang Customer web redesign) vào cổng bệnh nhân. **Chỉ đổi giao diện Web** (`web/src`): không sửa backend, API, payload, quyền, route hiện có, quy tắc đặt/đổi/hủy lịch hay luồng 5 bước đã nêu ở trên.

- **Khung điều hướng:** gộp thanh đầu trang và thanh điều hướng thành một thanh ngang cố định: logo DCMS, các mục Trang chủ / Đặt lịch / Lịch hẹn / Đơn thuốc / Người thân / Hồ sơ, tài khoản (avatar viết tắt + Đăng xuất). Thêm mục **Đặt lịch** trỏ `/customer/book`; bỏ mục Đơn thuốc bị lặp. Tên truy cập (aria-label) và đích của các liên kết hiện có giữ nguyên.
- **Màn hình nhỏ:** không có giao diện mobile riêng; thanh điều hướng giữ nguyên, xuống hàng riêng và cuộn ngang khi cần (phạm vi thiết kế lại chỉ là Web desktop).
- **Token:** nền `#F9FAFB`, viền `#EAECF0`, bo góc thẻ 16px và nút/ô nhập 10px, chữ chính `#101828`; giữ xanh `#175CD3`, Inter, Ant Design.
- **Trang chủ:** thẻ "Lịch hẹn gần nhất" là điểm nhấn đầu trang (khối ngày/tháng lớn, trạng thái, nhóm nút); thẻ Đặt lịch khám và Truy cập nhanh dùng cùng bo góc và khoảng cách. Chỉ hiển thị dữ liệu hiện có của AppointmentCard; không thêm banner/trạng thái liên kết hồ sơ vì không có nguồn dữ liệu trên trang này.
- **Đặt lịch:** thanh bước và tóm tắt giữ nguyên nội dung; khung giờ là chip bo góc 10px, giờ đã chọn nền xanh chữ trắng (vẫn có `aria-pressed`); nhóm sáng/chiều nằm trong thẻ trắng, tóm tắt là thẻ nhấn bên phải.
- **Lịch hẹn, Người thân, Liên kết hồ sơ, Đơn thuốc:** thẻ/bảng, nhãn trạng thái và khoảng cách theo token mới; không đổi cột dữ liệu, bộ lọc hay thao tác.
- **Kiểm tra:** build Web, Playwright các spec cổng khách hàng (customer-portal, customer-dashboard, customer-ui-responsive, customer-appointments, family-members, booking-prefill) và xem lại ở 390/768/1440 px.
# Hồ sơ bệnh án bệnh nhân (10/11/2026)

Cổng bệnh nhân có một màn **Hồ sơ bệnh án** chỉ đọc, gom thông tin tương ứng các tab nhân viên: Thông tin cơ bản, Khám, Điều trị & Thanh toán, Hình ảnh, Đơn thuốc. Mọi yêu cầu dùng token khách hàng và các API `/api/patients/me/...` hoặc `/api/me/...`; server xác định patientId từ liên kết VERIFIED, không nhận patientId tùy ý từ URL. `GET /api/patients/me/treatment-summary` trả `payments` cho từng dossier từ `DossierPayment` đã ghi và `PaymentReceipt` ở trạng thái Posted, gồm ngày, phương thức, số tiền, nguồn; chỉ những khoản thuộc dossier của bệnh nhân đã xác minh. Không suy số còn nợ từ kế hoạch. Tài liệu hình ảnh dùng API documents và content có xác thực, không nhúng URL công khai. Thông tin chưa có API công khai hiển thị trạng thái chưa khả dụng, không lấy API nhân viên. Tài liệu, lượt khám, đơn thuốc và hồ sơ điều trị chỉ thuộc bệnh nhân đã liên kết; 401 kết thúc phiên, 403 yêu cầu xác minh liên kết. Các danh sách có tải lại và trạng thái trống.
