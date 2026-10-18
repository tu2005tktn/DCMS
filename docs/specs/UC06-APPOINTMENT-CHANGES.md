# UC-06 — API đổi và hủy lịch hẹn

Ngày: 18/10/2026. Nguồn: UC-06/S12/S16 và GBR-012/013/014/015/025/031/075/076/081 trong bộ đặc tả v2.0; hợp đồng mobile tại `docs/implementation/UC05-06-S14-S16.md`.

## Hợp đồng và quyền

- `POST /api/appointments/{code}/reschedule`: `{expectedVersion, requestKey, newStart, chairId, scheduleVersion, assistantScheduleVersion?, reason, requesterVerification?}`.
- `POST /api/appointments/{code}/cancel`: `{expectedVersion, requestKey, reason, requesterVerification?}`.
- `GET /api/appointments/{code}/availability?date=yyyy-MM-dd`: HTTP 200 `AvailabilityResult`, cùng quyền sở hữu/phạm vi và điều kiện trạng thái/thời gian với command. Chỉ date đến từ client; branch/dentist/service/assistant/duration/attendee/excluded reservation lấy từ lịch đã được kiểm quyền. Đây là tra cứu, không giữ slot. Dùng cho S16 thay vì availability công khai theo catalog.
- Thành công HTTP 200: `{appointment: AppointmentDetail, notification: {code, message}}`; MSG35 có giờ mới theo UTC+7, MSG37 có mã lịch. Chi tiết trả lịch sử đã lưu; staff nhận `access: STAFF`.
- AppointmentDetail bổ sung `assistantId` (nullable). HTTP adapter S16 dùng endpoint availability theo code để nhận đúng `assistantScheduleVersion` và kiểm tra lịch của trợ lý được giữ nguyên.
- Không đăng nhập: HTTP 401 MSG32; bearer không hợp lệ: HTTP 401 MSG07. Không tồn tại hoặc ngoài phạm vi: HTTP 404 MSG102, không tiết lộ mã lịch có tồn tại.
- Public account đang hoạt động được đổi/hủy lịch do mình đặt (kể cả cho người thân), hoặc lịch có PatientId thuộc liên kết tự thân VERIFIED hiện còn hiệu lực. Không suy ra quyền từ số điện thoại hoặc PatientId client gửi.
- Clinic Assistant đang hoạt động và là thành viên chi nhánh được dùng cùng command; bắt buộc `requesterVerification` 1–500 ký tự mô tả việc xác minh người yêu cầu. Các vai trò staff khác không được quyền đổi/hủy theo endpoint này.
- Public: chỉ `Confirmed`, chưa tới giờ hẹn, còn ít nhất 12 giờ (đúng 12 giờ hợp lệ). Staff đã xác minh có thể xử lý dưới 12 giờ, nhưng vẫn chỉ lịch `Confirmed` chưa bắt đầu. Sai trạng thái/thời gian: HTTP 409 MSG31.
- Lý do sau trim phải 1–500 ký tự: HTTP 400 MSG02/MSG03. RequestKey phải UUID khác rỗng, expectedVersion dương; các trường slot hợp lệ: HTTP 400 MSG04.

## Transaction và dữ liệu

Một transaction ReadCommitted dùng cùng advisory lock S09 (90403) với đặt lịch và công bố lịch làm việc. Đọc lại quyền, trạng thái, thời gian và version sau khi lấy lock. `TimeProvider` cấp thời gian kiểm tra; không dùng thời gian từ client.

- Đổi lịch giữ chi nhánh, nha sĩ, trợ lý, dịch vụ, thời lượng và liên kết follow-up. Slot mới phải có lead time >=60 phút; kiểm tra tài nguyên trên toàn thời lượng bằng `IAvailabilityService.CheckInTransactionAsync`, loại trừ đúng reservation hiện tại. Duration lấy từ bản ghi lịch (15–480 phút theo booking), không lấy từ client và không đổi theo duration catalog hiện tại. Lead time tự kiểm ở command; kiểm tra tài nguyên dùng duration gốc để hỗ trợ cả lịch do quầy đặt. GET slot theo code cũng dùng duration này và loại trừ reservation cũ; không thay đổi hợp đồng availability công khai S15. Lịch làm việc lệch version trả MSG111; slot không hợp lệ/bị chiếm trả HTTP 409 MSG34. Thất bại không nhả hoặc sửa slot cũ.
- Hủy đặt cả Appointment và ResourceReservation sang `Cancelled`, giữ khoảng cũ làm lịch sử, tăng version, lưu CancelledAt/By/Reason. Không xóa lịch.
- Mỗi thành công ghi một `APPOINTMENT_CHANGE` có actor SELF/STAFF, old/new interval và chair, reason, requesterVerification, requestKey, thời gian; tăng version một lần. Bảng dùng FK giữ lịch sử.
- Receipt dùng khóa ghép `(ActorAccountId, RequestKey)`; fingerprint gồm action, code và toàn bộ command chuẩn hóa. Cùng actor/key/nội dung trả nguyên kết quả đã lưu, không tăng version, ghi history hoặc event lần nữa. Key bị dùng lại với nội dung/action khác trả HTTP 409 MSG111. Quyền được kiểm tra lại trước replay, kể cả khi liên kết đã bị thu hồi.
- Cùng transaction ghi một `APPOINTMENT_OUTBOX` event `appointment.rescheduled` hoặc `appointment.cancelled`, gồm mã/ID, version, trạng thái, chi nhánh, giờ cũ/mới; không đưa lý do tự do, contact hoặc chẩn đoán vào payload. Dispatcher/reminder do TV5 cung cấp; commit không phụ thuộc gửi thông báo.
- Follow-up: đổi lịch giữ nguyên ID; hủy phải gọi port transactional `IFollowUpAppointmentChangePort.MarkNeedsBookingAsync` trước commit. Module follow-up chưa có trong checkout: adapter mặc định trả HTTP 503 MSG144 cho hủy lịch có FollowUpId, rollback toàn bộ; không giả vờ chuyển chỉ định sang NeedsBooking. Adapter tương lai phải dùng cùng DbContext/transaction, không gọi mạng ngoài. Chi tiết của lịch đã hủy thể hiện NeedsBooking khi adapter hoàn thành.
- Lỗi lưu DB rollback mọi thay đổi; không trả thành công. EF concurrency failure trả MSG111; lỗi persistence bất ngờ trả MSG144 qua error middleware.

## Phạm vi tích hợp

Đây là hai command đổi/hủy. API tạo/rút/xử lý yêu cầu hỗ trợ, cùng-visit extension, notification dispatcher và UI staff S12 là các phần riêng. S14/S16 hiện có thể gọi HTTP client bằng `EXPO_PUBLIC_APPOINTMENTS_MOCK=false`; không chuyển cấu hình mặc định khi support-request API chưa sẵn sàng.

## Kiểm thử bắt buộc

HTTP + PostgreSQL trong schema riêng: đổi thành công và giữ duration/link; hủy và nhả slot; replay; key reuse; stale appointment/schedule; slot conflict giữ lịch cũ; ownership/verified link/revocation; anonymous/staff scope/verification; đúng 12 giờ và dưới 12 giờ; trạng thái đã bắt đầu; validation; cạnh tranh cùng version/slot; history/outbox đúng một lần; follow-up adapter rollback khi chưa có module.
