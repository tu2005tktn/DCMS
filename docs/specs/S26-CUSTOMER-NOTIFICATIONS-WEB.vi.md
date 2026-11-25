# S26 — Thông báo trong ứng dụng trên web bệnh nhân

Bổ sung cho [S26-Notifications-Reminders.md](S26-Notifications-Reminders.md). Mobile đã hiển thị thông báo S26; web khách hàng (`/customer/*`) dùng cùng API để bệnh nhân đăng nhập bằng trình duyệt cũng thấy nhắc lịch và kết quả xử lý yêu cầu. Không thêm bảng hay endpoint mới.

## Phạm vi

- Chỉ tài khoản **Public** trên cổng `/customer`. Thông báo S26 chỉ được tạo cho người đặt lịch và tài khoản có liên kết hồ sơ VERIFIED; nhân viên nhận việc qua mục cần xử lý (`STAFF_HANDLING_ITEM`) và Hộp thư hỗ trợ, nên web nhân viên không có chuông thông báo.
- Không hiển thị chẩn đoán hay nội dung lâm sàng ngoài `title`/`body` server trả (GBR-081).

## Giao diện

- Header cổng bệnh nhân có nút chuông, cạnh tên tài khoản, với số chưa đọc (`GET /api/notifications/unread-count`; quá 99 hiển thị `99+`). Nhãn trợ năng: "Thông báo, N chưa đọc".
- Bấm chuông mở ngăn kéo "Thông báo" (bên phải; toàn màn hình khi ≤ 576px) gồm:
  - Bộ lọc "Tất cả" / "Chưa đọc" → `GET /api/notifications?limit=30&unreadOnly=`; "Xem thêm" dùng `cursor` = `nextCursor`.
  - Mỗi dòng: tiêu đề, nội dung, thời gian (Asia/Ho_Chi_Minh), chấm "chưa đọc", nhãn "Gửi email/push thất bại" khi `hasExternalFailure` (lịch vẫn còn hiệu lực).
  - "Đánh dấu tất cả đã đọc" → `POST /api/notifications/mark-read` với tối đa 100 id chưa đọc đang hiển thị và `expectedAccountId` = id tài khoản phiên.
  - Trạng thái tải, rỗng ("Chưa có thông báo"), lỗi kèm "Thử lại".
- Bấm một thông báo: đánh dấu đã đọc (`POST /api/notifications/{id}/mark-read`), rồi nếu có `deepLink` thì hỏi lại quyền `POST /api/notifications/check-deep-link`. Được phép thì điều hướng theo bảng dưới và đóng ngăn kéo; không được phép thì hiển thị `reason` của server, không điều hướng.

| `deepLink` từ server | Trang web |
| --- | --- |
| `/appointments` | `/customer/appointments` |
| `/appointments/{code}` | `/customer/appointments/{code}` |
| `/prescriptions`, `/prescriptions/{id}` | `/customer/prescriptions`, `/customer/prescriptions/{id}` |
| `/book-appointment` | `/customer/book` |
| `/visit-results/{id}`, `/medical-records/{id}` | `/customer/treatment-records` (web chưa có trang chi tiết riêng) |
| Khác | Không điều hướng; chỉ đánh dấu đã đọc |

## Cập nhật thời gian thực

- Kết nối SignalR `/hubs/notifications` bằng token phiên khách; sự kiện `notificationCreated` chỉ là tín hiệu làm mới: tải lại số chưa đọc và danh sách đang mở qua API (không tin payload). Mất kết nối thì vẫn làm mới số chưa đọc mỗi 60 giây và khi tab được focus lại.
- Đăng xuất, 401 hoặc đổi tài khoản: ngắt kết nối và xoá danh sách khỏi bộ nhớ.

## Nghiệm thu

- Đặt/đổi/huỷ lịch hoặc nhân viên xử lý yêu cầu → chuông tăng số, danh sách có dòng mới; bấm mở đúng trang lịch hẹn.
- Thông báo cho lịch mà tài khoản không còn quyền xem → không điều hướng, hiện lý do.
- Tài khoản khác không thấy thông báo của nhau; 401 → về trang đăng nhập.
