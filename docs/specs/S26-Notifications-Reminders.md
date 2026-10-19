# S26 Notifications & Appointment Reminders

Chủ màn hình: **Tư Nguyễn Văn** (theo ràng buộc spec chung). Quyết định người dùng ghi nhận theo yêu cầu 12/10/2026, nội dung đặc tả tham chiếu v2.0 ngày 15/10/2026.

Nguồn: `DCMS_Screen_Specifications_v2.0.docx` (S26) và `DCMS_Use_Cases_Business_Rules_Messages_v2.0.docx` (UC-05/06, UC-34 booking portion; GBR-007/012–015/031/073/075/077/078/081/123; MSG31–38/94/111/144; AT16).

## Phạm vi

S26 đặc tả hai mảng nghiệp vụ gắn với luồng đặt lịch:

1. **Support request cho appointment (TV4 ↔ TV5).**
   - S12 (patient mobile): xem code/lifecycle, booker/attendee riêng, branch/chair/staff/start/end, lịch sử và trạng thái `Pending`/`Resolved`/`Rejected`/`Withdrawn`; submit/withdraw qua API đọc TV3.
   - Staff (web): xác minh requester; đổi/hủy appointment kèm lý do qua command TV4 theo quyền/status/version; không viết lại logic booking/change.
   - Command resolve/reject support request (TV5): kiểm quyền, request phải `Pending` và `expectedVersion` khớp; `Resolved` chỉ set sau khi booking action cần thiết đã commit. Không báo xử lý thành công nếu mutation thất bại. Requester `Withdrawn` không được resolve lại.
   - Cờ `ConfirmedToAttend` độc lập với Appointment lifecycle; thay đổi flag không check-in/complete Visit.
   - Hiển thị lịch bị ảnh hưởng khi giảm giờ làm để staff xử lý; Tư vẫn chặn schedule publish cho đến khi hợp lệ.

2. **Notification & reminder cho appointment.**
   - S26 chỉ phần thông báo lịch hẹn của account hiện tại; mở deep link phải kiểm quyền lại.
   - Tạo notification khi có booker account; anonymous booking vẫn nhận mã lịch trên UI.
   - Schema in-app notification, outbox/dispatcher và reminder thuộc luồng đặt lịch; chốt hợp đồng event/transaction hook sớm với TV2/TV4.
   - Booking/change/cancel cập nhật lịch, vô hiệu reminder cũ và lập reminder đúng lịch hiện tại; defaults 24h/2h, recheck `currentVersion`/`status`/preference trước khi gửi.
   - Retry tối đa 3 lần, backoff 1/5/15 phút theo v2.0; in-app authoritative, external push tùy adapter/cấu hình.
   - Lỗi external có staff handling item phù hợp, **không giả báo đã gửi** và **không rollback Appointment**.
   - Notification không chứa chẩn đoán; không xây toàn bộ support inbox S30 hoặc toàn bộ module care.

## Đối chiếu UC/GBR/MSG

| S26 | UC/GBR/MSG | Hành vi |
| --- | --- | --- |
| S26 list (patient app) | UC-05/06; GBR-007/012/013 | Lấy notification của account hiện tại; phân trang + filter `unread`/`all`; đọc lại sau khi mark. |
| S26 detail + deep link | UC-05/06; AT16; MSG94/111 | Mở deep link phải kiểm quyền lại server-side; link stale/version mismatch → MSG111. |
| Reminder 24h/2h | GBR-014/015/031/077/078 | Lập reminder sau khi booking commit; vô hiệu reminder cũ khi đổi/hủy; recheck `currentVersion`/`status`/preference trước khi gửi. |
| Retry dispatch | AT16; MSG144 | Tối đa 3 lần, backoff 1/5/15 phút; external lỗi tạo staff handling item, không rollback. |
| Support request resolve/reject | UC-34 (booking portion); GBR-073/075/081/123; MSG31–38 | Quyền staff, `Pending` + `expectedVersion`; chỉ `Resolved` sau booking action commit. |
| Cờ `ConfirmedToAttend` | GBR-031/077 | Độc lập với Appointment lifecycle; đổi flag không check-in/complete Visit. |
| Lịch bị ảnh hưởng khi giảm giờ làm | GBR-123; MSG31–38 | Hiển thị staff xử lý; publish schedule bị chặn cho tới khi hợp lệ. |

## Quyết định cấu hình (chốt cho S12/S26)

- In-app notification là nguồn authoritative; external push (email/SMS) là phương thức phụ thuộc adapter/cấu hình. Khi external fail, in-app vẫn đúng và tạo staff handling item; không bao giờ báo "đã gửi" nếu mutation thất bại.
- Reminder mặc định `24h` và `2h` trước `Appointment.StartAt`; tự recheck `currentVersion`/`status`/preference trước khi dispatch. Đổi/hủy làm reminder cũ `Superseded`; lập reminder mới theo lịch hiện tại.
- Retry tối đa 3 lần với khoảng chờ `1/5/15` phút. Sau lần cuối thất bại, tạo staff handling item loại `ExternalDeliveryFailed` chứa `notificationId`, `attemptCount`, `lastError`, `appointmentId`; không rollback Appointment.
- Support request `Resolved` chỉ commit sau khi booking action cần thiết đã thành công; dùng `expectedVersion` của request và `currentVersion` của appointment. Nếu mutation thất bại, request vẫn `Pending` và trả lỗi tương ứng (MSG111 stale, MSG06 quyền, MSG144 dịch vụ).
- `ConfirmedToAttend` là cờ boolean thuộc Appointment; thay đổi cờ không tạo Visit mới, không đổi `Appointment.Status` (giữ `Confirmed`/`Completed`/...).
- S26 list mặc định 20/trang, tối đa 100; sort `createdAt desc`, ID tie-breaker; chỉ lấy notification của account hiện tại, không lẫn của booking anonymous khác.
- Deep link từ notification: server kiểm quyền lại với Appointment hiện tại; quyền không được suy ra chỉ vì account từng nhận notification.
- Notification **không bao giờ** chứa chẩn đoán, kết quả xét nghiệm hay thông tin lâm sàng. Chỉ thông tin lịch (mã, branch/chair/staff, thời gian, trạng thái, lý do đổi/hủy nếu có).

## Persistence và transaction hook

- Booking/change/cancel ghi `AppointmentOutboxEvent` trong cùng transaction với
  Appointment. Event chỉ được dispatcher nhận sau commit.
- `NOTIFICATION` có unique `DedupeKey`; dispatch lại cùng event không tạo row mới.
- `APPOINTMENT_REMINDER` có appointment version và due time; đổi/hủy supersede mọi
  reminder Pending/Retry của version cũ trước khi tạo lịch 24h/2h mới.
- Trước dispatch, worker nạp lại Appointment và preference; chỉ gửi khi version,
  status, account ownership và due time vẫn hợp lệ.
- External adapter update attempt/outbox độc lập; lỗi external không rollback giao
  dịch booking đã commit.

## Phối hợp & hợp đồng

- **TV2 (booking)**: phát event `AppointmentBooked`/`Changed`/`Cancelled` sau commit. Outbox/dispatcher của S26 subscribe để tạo notification + reminder.
- **TV4 (support request + shared change API)**: sở hữu schema/submit/read/withdraw support request; shared change API cho staff đổi/hủy appointment kèm lý do. Trả về `expectedVersion` mới sau commit.
- **TV5 (resolve/reject + notification)**: sở hữu command resolve/reject, notification + reminder service, event-dispatch contract. Không xử lý no-show job, callback, check-in.
- **Tư (hạ tầng)**: schema in-app notification, outbox/dispatcher, reminder scheduler. Lưu credentials, thông tin bệnh nhân, model weights ngoài Git; dùng env/`.env.example`.

## Ranh giới & ngoài phạm vi

- Không xây toàn bộ support inbox S30.
- Không xây toàn bộ module care/clinical.
- Không xử lý no-show job, callback, check-in (thuộc TV khác).
- Không verify email/SMS ở S26; S03 recovery là feature riêng.
- Không tự rollback Appointment khi external delivery lỗi.

## Phối hợp UI states

- S26 list có `loading` / `empty` / `validation` / `connection` / `access-denied` / `stale-version` phân biệt rõ; retry có giới hạn; giữ dữ liệu form khi lỗi phù hợp.
- Staff web: hiển thị support request `Pending` với lịch liên quan, nút `Resolve` / `Reject` chỉ bật khi quyền + `expectedVersion` hợp lệ; lỗi server không đóng drawer.
- Patient app (S12 + S26): mark-as-read tối đa 100 sự kiện mới nhất; deep link reload entity rồi kiểm quyền lại; không tự đổi lifecycle lịch khi read notification.

## Nghiệm thu

- Staff xử lý support request từ app; hai phía (staff + patient) đọc lại đúng kết quả/lịch sử; conflict không chuyển request thành `Resolved` sai.
- Trạng thái request và Appointment tách biệt; flag `ConfirmedToAttend` không tạo Visit.
- Notification được tạo **sau** khi booking commit; dispatch lặp không tạo trùng thông báo (idempotency theo `notificationId`).
- Đổi/hủy không gửi reminder theo giờ cũ; không gửi cho account ngoài quyền.
- Lỗi mail/push vẫn giữ booking và hiển thị trạng thái thất bại thực; retry có giới hạn (3 lần, 1/5/15 phút); sau 3 lần có staff handling item.
- Read notification/deep link không làm đổi lifecycle lịch.
- Notification không chứa chẩn đoán.

## Traceability

- UC: UC-05, UC-06, UC-34 (booking portion).
- Screens: S12, S26.
- GBR: 007, 012, 013, 014, 015, 031, 073, 075, 077, 078, 081, 123.
- MSG: 31–38, 94, 111, 144.
- AT: AT16 (phần xử lý hỗ trợ và delivery failure).

## Handoff

- **TV4**: schema/submit/read/withdraw support request + shared change API.
- **TV5**: resolve/reject support request, notification + reminder service, event-dispatch contract.
- **Tư (infrastructure)**: schema in-app notification, outbox/dispatcher, reminder scheduler; chốt hợp đồng event/transaction hook sớm với TV2/TV4.
- **PR & kiểm thử nghiệp vụ** thuộc UI + API thật + dữ liệu/migration. Mock DTO chung dùng để làm song song; không đợi task khác hoàn tất mới bắt đầu UI.
