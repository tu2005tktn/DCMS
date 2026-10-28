# S12 — Staff Booking Exceptions and Resolution

> Cập nhật nguồn 26/10/2026: [bộ đặc tả v2.5](README.md) là nguồn hiện hành. Nội dung và bằng chứng dưới đây giữ mốc triển khai ban đầu; chưa được nghiệm thu lại theo v2.5.

Nguồn hiện hành: [Screen Specifications v2.5](DCMS_Screen_Specifications_v2.5.docx) và
[Use Cases / Business Rules / Messages v2.5](DCMS_Use_Cases_Business_Rules_Messages_v2.5.docx), ngày 26/10/2026. Hợp đồng bên dưới được viết theo v2.0 ngày 15/10/2026.
Tài liệu này chốt phạm vi TV5 cho S12 và hợp đồng tích hợp với API booking/change
do TV4 sở hữu.

## Phạm vi và ranh giới

- Staff xem hàng đợi và chi tiết support request thuộc chi nhánh được phép xử lý.
- Chi tiết luôn tách booker và attendee, đồng thời hiển thị mã/lifecycle của lịch,
  chi nhánh, ghế, staff, thời gian, lịch sử và trạng thái request.
- Staff xác minh requester rồi đổi/hủy lịch kèm lý do bằng shared appointment
  change command; S12 không sao chép logic giữ chỗ hay kiểm tra xung đột.
- TV5 sở hữu resolve/reject. TV4 tiếp tục sở hữu schema, submit/read/withdraw request
  và shared appointment change API.
- Không bao gồm S30 care inbox, no-show job, callback, check-in hay complete Visit.

## Trạng thái và bất biến

- Support request có trạng thái `Pending`, `Resolved`, `Rejected`, `Withdrawn`.
- Chỉ request `Pending` và đúng `expectedVersion` mới được verify, resolve hoặc reject.
- Request `Withdrawn` không được mở lại hay resolve.
- Với request `Change`/`Cancel`, `Resolved` chỉ được ghi sau khi appointment mutation
  đã commit thành công. Nếu mutation lỗi/conflict, request vẫn `Pending`.
- Chi tiết request trả cờ `appointmentActionCommitted` do server tính từ
  `AppointmentActionRequestKey` và `APPOINTMENT_CHANGE` tương ứng. Client không được
  suy đoán action của request hiện tại từ một thay đổi cũ trong lịch sử appointment.
- Command `change-appointment` thành công trả đồng thời appointment và request detail
  đã lưu (gồm version mới). UI dùng response này để bật Resolve, không phụ thuộc một
  GET nối tiếp; lỗi đọc lại không được làm UI kẹt ở version cũ sau khi mutation đã commit.
- Mọi command có `requestKey` để idempotent và `expectedVersion` để optimistic
  concurrency; response trả lại `id`, `version`, `status` sau khi lưu.
- `ConfirmedToAttend` là cờ độc lập trên Appointment. Thay đổi cờ không đổi lifecycle
  Appointment và không tạo/check-in/complete Visit.
- Quyền, branch và ownership được kiểm tra lại ở server; cờ quyền trong DTO chỉ phục
  vụ trình bày, không phải nguồn quyết định truy cập.

## API contract

| Method | Path | Mục đích |
| --- | --- | --- |
| `GET` | `/api/support-requests?statuses=&branchId=&page=&pageSize=` | Hàng đợi staff có phân trang. |
| `GET` | `/api/support-requests/{id}` | Chi tiết request, appointment và history. |
| `POST` | `/api/support-requests/{id}/verify-requester` | Xác minh requester với `requestKey`, `expectedVersion`, `notes`. |
| `POST` | `/api/support-requests/{id}/change-appointment` | Gọi shared change/cancel command; không tự resolve nếu command lỗi. |
| `POST` | `/api/support-requests/{id}/resolve` | Resolve request Pending sau khi action bắt buộc đã commit. |
| `POST` | `/api/support-requests/{id}/reject` | Reject request Pending; bắt buộc lý do. |
| `PATCH` | `/api/appointments/{id}/attendance-confirmation` | Đổi `confirmedToAttend` độc lập lifecycle. |

Staff list/detail trả `permission.canResolve`, `permission.canEdit`, `branchMatch`,
`ownershipMatch`, nhưng mỗi mutation vẫn xác thực token và kiểm lại toàn bộ điều kiện.
Các lỗi phải phân biệt: `MSG06` access denied, `MSG111` stale/conflict, lỗi validation
MSG31–38 tương ứng, và `MSG144` lỗi dịch vụ/kết nối.

`change-appointment` trả `{ok, appointment, request, notificationCode}`; `request`
là bản đọc lại sau save với `expectedVersion` mới và
`appointmentActionCommitted=true`. Replay cùng `requestKey` trả đúng response này.

## Giao dịch resolve

1. Nạp request có concurrency lock và kiểm `Pending` + `expectedVersion`.
2. Kiểm quyền staff/branch và requester verification nếu action yêu cầu.
3. Với Change/Cancel, kiểm appointment history có action tương ứng gắn request này
   và mutation đã commit; không suy luận thành công chỉ từ payload client.
4. Ghi decision/history/outbox trong cùng transaction của support request.
5. Sau commit, dispatcher tạo notification idempotent cho booker account (nếu có).

## Đồng bộ thời gian thực cho staff inbox

- Staff web kết nối SignalR tại `/hubs/notifications` bằng access token hiện tại. Sau khi xác thực, server chỉ đưa connection vào các group `staff:branch:{branchId}` mà account staff đang là scheduling member còn hiệu lực.
- Server phát `supportRequestUpdated` **sau khi transaction đã commit** cho các sự kiện: tạo request, xác minh requester, booking action của request đã commit, resolve, reject và requester withdraw. Payload tối thiểu gồm `requestId`, `appointmentId`, `branchId`, `status`, `version`, `action` và `updatedAt`; không chứa dữ liệu chẩn đoán.
- Staff inbox nhận event đúng branch thì tự đọc lại danh sách theo bộ lọc hiện tại. Nếu drawer đang mở đúng `requestId`, drawer cũng đọc lại detail; event cũ hoặc trùng được bỏ qua theo `version`.
- Mất kết nối SignalR không làm hỏng command và không hiển thị sai là command thất bại. Client tự reconnect với backoff, đồng bộ lại bằng API sau reconnect và vẫn giữ nút tải lại thủ công làm phương án dự phòng.
- Event SignalR chỉ là tín hiệu invalidation; API đọc TV3 và kiểm quyền server vẫn là nguồn dữ liệu authoritative.

## UI states và nghiệm thu

- Loading, empty, validation, connection, access denied và stale conflict có trạng
  thái riêng; lỗi phù hợp không làm mất form đang nhập.
- Xác minh thành công cập nhật ngay version trong drawer. Nếu request đã được xác minh
  từ lần mở trước, UI hiển thị trạng thái đã xác minh và cho phép tiếp tục action.
- Mỗi command giữ nguyên `requestKey` khi retry lỗi mạng/5xx; stale/conflict tải lại
  request và tạo key mới. Resolve/Reject lỗi không được đóng nhầm dialog hoặc hiện success.
- Staff và requester đọc lại cùng trạng thái/lịch sử sau resolve/reject.
- Conflict không làm request thành `Resolved`; mutation thất bại không hiện success.
- Thay đổi `ConfirmedToAttend` không tạo Visit và không đổi Appointment status.
- Khi giảm giờ làm, staff thấy danh sách lịch bị ảnh hưởng; schedule publish vẫn do
  luồng S09 chặn cho tới khi dữ liệu hợp lệ.

## Calendar detail entry (19 October 2026)

- Clicking an S10 appointment opens `/appointments/{id}` directly, with the authorized staff detail for that exact appointment. A refresh preserves this route while the staff session is restored.
- The detail page shows appointment information and, for `appointments.support.handle`, its own support requests with pagination. Opening a request reuses the existing S12 command drawer on the same page; changes refresh both appointment information and request list.
- `GET /api/support-requests` accepts optional GUID `appointmentId`, applied before count/pagination alongside existing status and staff branch restrictions. No unrelated request is returned. An appointment with no matching requests shows an empty state; failed reads have a retry state.
- Other appointment readers see the appointment detail without support commands. Check-in remains outside the implemented support workflow. Navigating or viewing does not create a request or change appointment lifecycle.

## Traceability

UC-05/06 và booking portion UC-34; S12/S26;
GBR-007/012–015/031/073/075/077/078/081/123;
MSG31–38/94/111/144; AT16 phần xử lý hỗ trợ.
