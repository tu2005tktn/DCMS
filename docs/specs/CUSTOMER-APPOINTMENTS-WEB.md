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
  + Hủy lịch dưới 12 giờ nhưng chưa bắt đầu: gửi yêu cầu hỗ trợ `Cancel` để nhân viên xác minh và xử lý; lịch cũ giữ nguyên khi yêu cầu đang chờ. Lịch đã bắt đầu không được tự đổi/hủy hay gửi yêu cầu đổi slot; hiển thị liên hệ phòng khám.
- Payload đổi lịch lấy expectedVersion từ chi tiết, scheduleVersion/assistantScheduleVersion và chairId/newStart từ slot. Không cho người dùng tự nhập duration.
- Chặn gửi đồng thời. requestKey theo fingerprint của action/code/version/nội dung; giữ key khi retry lỗi mạng/5xx với cùng nội dung; không giữ key khi server từ chối 4xx. Đổi nội dung tạo key mới. Thành công dùng response đã lưu, tăng version theo server, cập nhật trạng thái/giờ/lịch sử/yêu cầu hỗ trợ.
- MSG34 giữ giờ cũ và lý do đã nhập, tải lại slot, bỏ chọn slot vừa bị chiếm, yêu cầu xem lại. MSG111/MSG31 giữ form để người dùng biết lỗi và yêu cầu tải lại trước khi gửi tiếp. Lỗi 403/404 xóa chi tiết ngay; 401 về đăng nhập. Backend theo hợp đồng UC06 có thể trả 503 khi hủy follow-up chưa có adapter; web không báo thành công và không cập nhật giả.
- Sau hủy hoặc đổi thành công, danh sách tải lại từ API. Thông báo thành công dùng MSG35/MSG37 (hoặc MSG33 cho yêu cầu hỗ trợ dưới 12h) và lịch sử đọc từ dữ liệu thật.

### Giao diện yêu cầu đổi lịch trên Web (19/10/2026)

- Trong chi tiết lịch `Confirmed`, khối thao tác hiển thị rõ **giờ hẹn hiện tại** và một hành động chính phù hợp: `Đổi lịch` nếu còn ít nhất 12 giờ, `Yêu cầu đổi lịch` nếu dưới 12 giờ nhưng chưa bắt đầu. Trường hợp dưới 12 giờ phải giải thích lịch cũ vẫn còn hiệu lực cho đến khi nhân viên xử lý. Nếu có yêu cầu `Pending`, thay hành động gửi mới bằng trạng thái đang chờ, giờ đề xuất và nút rút yêu cầu.
- Hộp thoại đổi lịch theo hai bước có tên rõ ràng: **Chọn lịch mới** (bác sĩ, ngày, các giờ trống nhóm sáng/chiều, lý do) và **Kiểm tra yêu cầu** (giờ cũ, giờ đề xuất, bác sĩ, lý do). Chỉ cho sang bước kiểm tra khi đã chọn slot và nhập lý do hợp lệ; thời gian hiển thị theo `Asia/Ho_Chi_Minh`.
- Khi gửi yêu cầu dưới 12 giờ, nút ghi `Gửi yêu cầu đổi lịch`; thông báo MSG33 và thẻ `Đang chờ xử lý` xuất hiện sau khi đọc lại API. Không trình bày giờ đề xuất như lịch đã được đổi. Từ 12 giờ trở lên, nút ghi `Xác nhận đổi lịch`; kết quả lấy từ response đổi trực tiếp.
- Trạng thái tải slot, không còn slot, lỗi tải, MSG34 và stale MSG111/MSG31 phải nêu bước tiếp theo. Giữ bác sĩ, ngày và lý do khi có lỗi thích hợp; slot bị tranh phải chọn lại. Hộp thoại dùng nhãn form, trạng thái chọn có chữ/viền, focus bàn phím rõ và bố cục không tràn ngang ở màn hình nhỏ.

## Nghiệm thu

### Điều kiện chạy trên database development

Luồng yêu cầu đổi/hủy dưới 12 giờ cần migration
`20261018173750_S12S26BookingExceptionsNotifications` để có các bảng
`APPOINTMENT_SUPPORT_REQUEST`, `APPOINTMENT_SUPPORT_HISTORY` và
`SUPPORT_REQUEST_RECEIPT`. Luồng đổi nha sĩ cần thêm migration
`20261018180727_AppointmentRescheduleDentist`. Trước khi kiểm thử với API thật,
kiểm tra `__EFMigrationsHistory` và áp dụng các migration còn thiếu bằng
`infrastructure/scripts/start-api.ps1 -Migrate` trên database development;
không dùng `-SeedDemo` để kiểm tra lỗi này. Nếu schema chưa cập nhật, API không
thể đọc chi tiết/yêu cầu hỗ trợ dù bảng `APPOINTMENT` đã có lịch.

1. Web với API/PostgreSQL thật: tài khoản synthetic đặt lịch, đăng nhập khách hàng, mở S14/S16, đổi slot qua review/confirm rồi hủy qua review/confirm; xác minh version/history, slot giải phóng và danh sách Lịch sử.
2. Kiểm thử UI xác định cho mốc dưới 12 giờ, validation lý do/ngày, slot trống/lỗi, MSG34, MSG111, 401, 403/404 và retry cùng requestKey. Không dùng mock để kết luận mutation đã lưu database.
3. Desktop và màn hình nhỏ: không tràn toàn trang; bảng có scroll nội bộ, form/slot chọn được bằng nhãn truy cập.
