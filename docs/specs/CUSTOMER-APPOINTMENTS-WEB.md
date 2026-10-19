# Lịch hẹn của tôi trên web — S14/S16, UC-05/06

Ngày 18/10/2026. Dùng hợp đồng [UC-06](UC06-APPOINTMENT-CHANGES.md) và [S14/S16](../implementation/UC05-06-S14-S16.md).

## Điều hướng và dữ liệu

- Khách hàng đăng nhập vào `/customer/appointments`: Sắp tới/Lịch sử, lọc trạng thái/từ ngày/đến ngày theo UTC+7, phân trang 20 dòng. Gọi `GET /api/me/appointments` với token của cổng khách hàng. Quyền xem do server quyết định: người đặt hoặc hồ sơ tự thân VERIFIED; đặt hộ không cấp quyền hồ sơ người thân.
- Mở `/customer/appointments/:code` gọi `GET /api/me/appointments/{code}`. Hiện mã/trạng thái, giờ hẹn UTC+7, thời lượng, nha sĩ/chi nhánh/dịch vụ, người khám/người đặt và contact đã che, lịch sử đổi/hủy, điều kiện tự phục vụ và liên hệ phòng khám.
- Thêm đường vào từ trang chủ, điều hướng khách hàng và kết quả đặt lịch có đăng nhập. Không đưa token staff vào request khách hàng. Route được CustomerGuard bảo vệ; client không nhận PatientId hoặc AccountId để mở rộng quyền.
- Loading, không có lịch, lỗi kết nối, không có quyền/không tồn tại là các trạng thái riêng. Mọi request 401 xóa phiên khách hàng; 403/404 giữ phiên nhưng xóa nội dung lịch không còn quyền đọc.

## Đổi và hủy

- Nút đổi/hủy xuất hiện khi trạng thái Confirmed và chưa bắt đầu:
  + Từ 12 giờ trở lên: Tự phục vụ trực tiếp. Đổi lịch: chọn nha sĩ/ngày → `GET /api/appointments/{code}/availability?date` → chọn slot → nhập lý do → xem lại → xác nhận `POST /api/appointments/{code}/reschedule` (áp dụng ngay). Hủy lịch: nhập lý do → xem lại → xác nhận `POST /api/appointments/{code}/cancel`.
  + Dưới 12 giờ: Trải nghiệm đổi lịch trên UI hoàn toàn tương tự (chọn nha sĩ cùng chi nhánh, chọn ngày, chọn slot trống từ availability, nhập lý do, xem lại). Tuy nhiên, hệ thống không duyệt đổi lịch ngay mà gửi yêu cầu hỗ trợ tới nhân viên phòng khám qua `POST /api/appointments/{code}/support-requests` với loại `Change`, khung giờ đề xuất `newStartAt`, `dentistId`, `chairId` và lý do. Lịch hẹn cũ giữ nguyên khung giờ ban đầu; yêu cầu được ghi nhận ở trạng thái `Pending`.
  + Chỉ sau khi nhân viên phòng khám (Staff S12) xác minh và duyệt (`change-appointment` + `resolve`), lịch hẹn mới chính thức được đổi sang giờ mới. Nếu nhân viên từ chối (`reject`), lịch hẹn vẫn giữ nguyên giờ cũ.
  + Trong lúc có yêu cầu đổi lịch `Pending`, UI hiển thị thông tin yêu cầu đang chờ nhân viên xử lý và nút "Rút yêu cầu", đồng thời chặn gửi thêm yêu cầu đổi lịch mới.
  + Hủy lịch dưới 12 giờ hoặc lịch đã bắt đầu: giải thích quy tắc và hiển thị liên hệ phòng khám.
- Payload đổi lịch lấy expectedVersion từ chi tiết, scheduleVersion/assistantScheduleVersion và chairId/newStart từ slot. Không cho người dùng tự nhập duration.
- Chặn gửi đồng thời. requestKey theo fingerprint của action/code/version/nội dung; giữ key khi retry lỗi mạng/5xx với cùng nội dung; không giữ key khi server từ chối 4xx. Đổi nội dung tạo key mới. Thành công dùng response đã lưu, tăng version theo server, cập nhật trạng thái/giờ/lịch sử/yêu cầu hỗ trợ.
- MSG34 giữ giờ cũ và lý do đã nhập, tải lại slot, bỏ chọn slot vừa bị chiếm, yêu cầu xem lại. MSG111/MSG31 giữ form để người dùng biết lỗi và yêu cầu tải lại trước khi gửi tiếp. Lỗi 403/404 xóa chi tiết ngay; 401 về đăng nhập. Backend theo hợp đồng UC06 có thể trả 503 khi hủy follow-up chưa có adapter; web không báo thành công và không cập nhật giả.
- Sau hủy hoặc đổi thành công, danh sách tải lại từ API. Thông báo thành công dùng MSG35/MSG37 (hoặc MSG33 cho yêu cầu hỗ trợ dưới 12h) và lịch sử đọc từ dữ liệu thật.

## Nghiệm thu

1. Web với API/PostgreSQL thật: tài khoản synthetic đặt lịch, đăng nhập khách hàng, mở S14/S16, đổi slot qua review/confirm rồi hủy qua review/confirm; xác minh version/history, slot giải phóng và danh sách Lịch sử.
2. Kiểm thử UI xác định cho mốc dưới 12 giờ, validation lý do/ngày, slot trống/lỗi, MSG34, MSG111, 401, 403/404 và retry cùng requestKey. Không dùng mock để kết luận mutation đã lưu database.
3. Desktop và màn hình nhỏ: không tràn toàn trang; bảng có scroll nội bộ, form/slot chọn được bằng nhãn truy cập.
