# Trang chủ mobile cho bệnh nhân (09/11/2026)

## Mục tiêu và phạm vi

Thay màn hình bản đồ dành cho tester ở điểm vào mobile bằng trang chủ dễ dùng trên điện thoại. Tài khoản Public thấy các việc thường dùng: đặt và xem lịch hẹn, theo dõi bệnh án đang điều trị, xem đơn thuốc, thông báo và tài khoản. Khách chưa đăng nhập thấy lời giới thiệu, đăng nhập và đăng ký. Tài khoản nhân viên vẫn có lối vào các màn nghiệp vụ hiện có, không hiển thị dữ liệu bệnh nhân của tài khoản Public.

## Nguồn dữ liệu và quyền

- Trạng thái liên kết: `GET /api/patient-links/me` qua `patientLinkApi.overview`; chỉ khi `VERIFIED` mới tải `GET /api/patients/me/treatment-summary` và hiển thị hồ sơ đang điều trị (`state=Open`). Thẻ dẫn tới màn hồ sơ S41. Không đặt chẩn đoán trong thông báo hoặc trên màn chưa xác minh.
- Lịch sắp tới: `GET /api/me/appointments?scope=UPCOMING&page=1&pageSize=50` qua `medicalRecordsApi.visits`. Thẻ dẫn tới danh sách lịch hẹn hiện có. Chỉ hiển thị lịch của tài khoản hiện tại theo API.
- Thông báo: `GET /api/notifications` qua `notificationRepo`, mặc định API thật. Danh sách S26 có bộ lọc tất cả/chưa đọc, tải lại, đánh dấu đã đọc và mở liên kết sau khi API kiểm tra quyền. Không dùng mock cục bộ trong luồng thật hoặc hiển thị nhãn kỹ thuật.
- 401 kết thúc phiên; 403 hồ sơ xoá nội dung lâm sàng khỏi màn, chuyển sang trạng thái cần xác minh. Lỗi tải từng phần có nút thử lại và không được biến thành trạng thái trống.

## Trải nghiệm

- Trang chủ có lời chào, tác vụ chính, tóm tắt hồ sơ hiện tại, lịch sắp tới, thông báo mới và thanh điều hướng dễ bấm. Các màn chi tiết cũ giữ làm nguồn sự thật.
- Trạng thái loading, chưa liên kết, chờ xác minh, không có hồ sơ/lịch/thông báo, lỗi mạng và tải lại phải tách biệt. Kéo xuống để đồng bộ lại.
- Bố cục cuộn dọc, các vùng chạm tối thiểu khoảng 44 px, màu và chữ tương phản. Không lộ thông tin lâm sàng trên màn khách/chưa xác minh.
- Bản release có thể chạy khi không có Metro. Trong môi trường test qua USB, API dùng `adb reverse tcp:5080 tcp:5080` với `EXPO_PUBLIC_API_URL=http://localhost:5080`; khi tháo USB cần cấu hình lại URL API phù hợp mạng.

## Nghiệm thu

Khách có thể đi tới đăng nhập/đăng ký. Public đã đăng nhập đi từ trang chủ tới bệnh án, lịch, đơn thuốc, thông báo và tài khoản trong một lần bấm; trạng thái hồ sơ/lịch/thông báo phản ánh API thật. Tài khoản chưa xác minh không thấy nội dung bệnh án. Thông báo đọc và mở liên kết đúng quyền. Build TypeScript và Android thành công, kiểm tra trên thiết bị Android.
