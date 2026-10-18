# Web khách hàng — xem, đổi và hủy lịch

Hợp đồng: [CUSTOMER-APPOINTMENTS-WEB.md](../specs/CUSTOMER-APPOINTMENTS-WEB.md).

Triển khai S14/S16 cho cổng `/customer`, dùng API thật của UC-05/06. Không thay quyền staff. Support request, follow-up adapter và dispatcher giữ ranh giới đã ghi trong UC06.

## Đã thực hiện

- `/customer/appointments`: Sắp tới/Lịch sử, trạng thái, khoảng ngày UTC+7, phân trang, nhãn người đặt/người khám và đường vào chi tiết.
- `/customer/appointments/:code`: thông tin lịch, người đặt/người khám, eligibility, liên hệ phòng khám và timeline lịch sử thật.
- Hai dialog đổi/hủy có lý do, xem lại và xác nhận. Dialog đổi lịch tái sử dụng options của màn đặt lịch để chọn nha sĩ active cùng chi nhánh, rồi lấy slot có xác thực theo mã lịch + nha sĩ + ngày; phần xem lại hiển thị cả nha sĩ cũ/mới. Chỉ cập nhật chi tiết từ response thành công. Chặn double-submit; giữ requestKey khi retry mạng/5xx; MSG34 giữ nha sĩ/ngày/lý do và lấy lại slot; MSG111/MSG31 yêu cầu reload. 401 xóa phiên, 403/404 xóa dữ liệu lịch còn trong màn hình và giữ phiên khách hàng.
- Link từ trang chủ, thanh điều hướng khách hàng và kết quả đặt lịch đã đăng nhập. Lịch đã hủy nằm trong Lịch sử; quay về danh sách tải lại từ API.

## Kiểm chứng 18/10/2026

- `npm install` và `npm run build`: đạt. Chỉ có cảnh báo chunk Vite lớn hơn 500 kB. Thay đổi lockfile do npm được loại bỏ vì đầu task lockfile sạch.
- Playwright `customer-appointments.spec.ts`: **9/9 đạt**. 8 ca UI dùng mock để kiểm trạng thái lỗi/biên, 1 ca dùng toàn bộ API và PostgreSQL thật.
- Playwright regression `customer-portal.spec.ts`: **8/8 đạt**.
- Ca thật: đăng ký account synthetic qua API, tạo booking synthetic, đăng nhập bằng UI khách hàng, mở danh sách/chi tiết, đổi qua review/confirm (HTTP 200), hủy qua review/confirm (HTTP 200), đọc lại `Cancelled`/version 3/history 2, không còn ở Sắp tới và có trong Lịch sử. Không dùng lịch bệnh nhân thật.
- SQL trên database local sau ca UI thật: Appointment `Cancelled`, version 3; ResourceReservation `Cancelled`; APPOINTMENT_CHANGE 2, APPOINTMENT_CHANGE_RECEIPT 2. Backend UC06 đã có 48 test liên quan đạt trong task trước; không sửa backend trong task giao diện này.
- Đã kiểm bố cục chi tiết ở viewport 390×844 không tràn toàn trang; bảng danh sách có scroll nội bộ. Ảnh synthetic: `.artifacts/test-results/customer-appointments/cancelled-web.png` (ngoài Git).

## Cách thử

Mở `http://localhost:5173/customer/appointments`, đăng nhập tài khoản khách hàng. Chỉ có lịch đúng quyền của tài khoản xuất hiện; không hiển thị mã LH-DEMO. Có thể đặt lịch mới tại `/customer/book`, rồi chọn “Xem và quản lý lịch hẹn” trên kết quả. Dưới 12 giờ, web hiển thị thông tin liên hệ phòng khám; không giả lập gửi yêu cầu hỗ trợ. Phần hồ sơ cá nhân web đang được ưu tiên sau task lịch hẹn theo yêu cầu người dùng.
