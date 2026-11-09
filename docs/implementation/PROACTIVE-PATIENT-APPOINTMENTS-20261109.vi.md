# Chủ động chỉnh sửa và hủy lịch bệnh nhân — 09/11/2026

Đặc tả: `../specs/UC06-APPOINTMENT-CHANGES.md` và `../specs/PATIENT-APPOINTMENTS-TAB.md`, mục bổ sung 09/11/2026.

## Hành vi

- Trong hồ sơ bệnh nhân → tab **Lịch hẹn**, từng lịch Confirmed chưa bắt đầu có **Chỉnh sửa lịch** và/hoặc **Hủy lịch** theo hai grant riêng. Màn chi tiết lịch dùng lại cùng component.
- Chỉnh sửa ngày/giờ, nha sĩ trong cùng phạm vi và ghế từ slot thực tế. Giữ Patient, người đặt, chi nhánh, dịch vụ và thời lượng đã lưu. Lý do bắt buộc. Chỉ lưu sau khi kiểm lại tài nguyên; thành công tải lại danh sách và khối hạng mục cần hẹn.
- Hủy mở xác nhận có mã lịch, bệnh nhân, thời gian và lý do; có **Giữ lịch hẹn** để đóng mà không gửi command. Commit hủy giải phóng reservation, giữ lịch sử và trả chỉ định chưa hoàn tất về NeedsBooking.
- Các form gọi endpoint có sẵn với `clinicInitiated=true`. Server xác định Staff, yêu cầu quyền động và scope đọc/chi nhánh sau khóa transaction, trước replay; nha sĩ chỉ thao tác lịch được phân công. Public không bật được chế độ phòng khám. Luồng cũ thiếu/false vẫn yêu cầu xác minh người yêu cầu theo hợp đồng trước.
- Không tạo grant, bảng hoặc migration mới. Audit dùng actor thật và mô tả phòng khám chủ động, không giả lập yêu cầu/đồng ý của khách. Fingerprint giữ nguyên cho command cũ; chế độ true có dấu riêng. History/receipt/outbox vẫn commit cùng giao dịch.
- Lỗi mạng giữ nội dung và key; sửa nội dung tạo key mới. Stale tải lại bản hiện tại, giữ lý do/ngày đã nhập nhưng bỏ slot ngay khi bắt đầu tải. Xung đột slot tải lại giờ trống. Nút và đóng form bị khóa khi ghi. Drawer có tên truy cập rõ và giới hạn theo viewport, đã xem ảnh 390px.

## Kiểm chứng

- .NET 10 restore/build: đạt, 0 lỗi. Web production build đạt. Các cảnh báo Pkcs, tham số chưa dùng, SignalR annotation và bundle size đã có vẫn còn.
- API/PostgreSQL cách ly: **44/44** AppointmentChangeApiTests + TreatmentPlanBookingApiTests, gồm nha sĩ/trợ lý/Owner chủ động sửa/hủy, phạm vi bác sĩ, Public không giả chế độ Staff, dynamic grant bị thu hồi với token cũ kể cả replay/gọi service trực tiếp, transaction/history/outbox/slot và chỉ định điều trị. Hồi quy SupportNotificationApiTests: **4/4**.
- Playwright mock API: **20/20**, gồm sáu ca thao tác mới và 14 ca tab lịch/đặt lịch điều trị hiện có. Bao phủ quyền đọc, hủy có xác nhận, refresh danh sách/chi tiết, retry key, stale giữ bản nhập và chọn lại giờ.
- Browser với API/PostgreSQL thật: **1/1**. Chuẩn bị Patient và lịch bằng API trong schema riêng; đăng nhập qua UI, từ tab bệnh nhân đổi ngày/giờ rồi hủy, đọc lại version/giờ từ API và xác nhận giờ được giải phóng. 0 pageerror; screenshot 1440/390 cùng kiểm bounds dialog. Không dùng dữ liệu thật hoặc gọi hủy lịch trong schema chính để thử.
- Fixture `proactive_e2e_20261109_06f65879` chạy ở API 5087/web 5187; đã dừng dịch vụ thử và xóa đúng schema sau kiểm chứng. API chính 5080/database healthy, web 5173 trả HTTP 200.
- Bằng chứng local: `.artifacts/runtime-20261109/proactive-{restore,build,web-build,api,support,browser,live}.log`, `proactive-api.trx`, `proactive-support.trx`, `proactive-editor-{1440,390}.png`.

Không commit/push trong lượt triển khai này; giữ các thay đổi người thân chưa commit từ lượt trước và cấu hình agent local.
