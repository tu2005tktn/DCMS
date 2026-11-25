# S26 — Staff Support Inbox (Web)

> **Trạng thái**: UI đã dùng API thật TV5, có EF migration và kiểm thử tích hợp.

Phạm vi: module web cho staff **xem và xử lý** các yêu cầu hỗ trợ (đổi/huỷ/xác nhận) do booker & attendee gửi lên. Bám sát đặc tả S26 (`docs/specs/S26-Notifications-Reminders.md`) và màn hình mobile S12 (`mobile/src/app/(home)/staff/requests`).

Tài liệu này mô tả UI web và API TV5. Web gọi `supportInbox.api.ts`; mock cũ chỉ còn làm fixture tham khảo/offline và không nằm trên production path.

## Phạm vi đợt này

- Trang **Hộp thư hỗ trợ** (`/support-inbox`) cho staff với quyền `appointments.support.handle` (sẽ dùng chung policy nhóm "staff clinic ops"; placeholder tới khi backend chốt permission key).
- Lọc theo trạng thái (Cần xử lý / Chờ staff / Đang xem xét / Đã đóng), theo chi nhánh mà staff phụ trách.
- Drawer chi tiết yêu cầu:
  - Thông tin appointment liên quan (mã, chi nhánh, ghế, nha sĩ, thời gian).
  - Thông tin requester (booker/attendee, accountId/ẩn danh, SĐT).
  - Lịch sử thay đổi `history` của appointment.
  - Cờ quyền `canResolve`, `canEdit`, `branchMatch`, `ownershipMatch` (do server tính theo quyền, chi nhánh và quyền sở hữu).
- Command TV4/TV5 (theo hợp đồng mock DTO):
  - **Xác minh requester** (`verifyRequester`).
  - **Đổi lịch** với lý do (`staffChange`).
  - **Huỷ lịch** với lý do (`staffChange`).
  - **Resolve** yêu cầu (`staffResolve`) — chỉ khi booking action cần thiết đã commit.
  - **Reject** yêu cầu với lý do (`staffResolve`).
- Trạng thái UI: `loading`, `empty`, `denied` (không phải staff), `error` (kèm nút thử lại), `ready`.
- Lỗi server `STALE_VERSION` (MSG111) → tải lại danh sách; `FORBIDDEN` (MSG06) → đóng drawer + cảnh báo; `NOT_FOUND` (MSG01) → bỏ chọn dòng; `NETWORK`/`SERVER` → giữ form, cho retry.

## Không thuộc phạm vi

- Toàn bộ hộp thư S30 (care/clinical).
- Tạo support case từ phía booker/attendee (mobile đã có ở S12).
- Gửi notification thật (do TV5; web chỉ hiển thị notificationCode trả về từ command).
- Auto-resolve khi booking action fail (đã được chốt trong S26: Resolve chỉ commit khi mutation thật sự thành công; nếu lỗi thì request vẫn `Pending`).

## Quy tắc nghiệp vụ áp dụng (lấy từ S26)

| Quy tắc | Áp dụng trong web |
| --- | --- |
| `Resolved` chỉ commit khi booking action cần thiết đã thành công (GBR-073) | Nút Resolve chỉ bật khi `kind ∈ {Change, Cancel}` **đã** chạy `staffChange` thành công; với `ConfirmToAttend`/`WithdrawAttend`/`General` thì Resolve chỉ cần `expectedVersion` hợp lệ. |
| Không báo xử lý thành công nếu mutation thất bại (GBR-081) | UI gọi command, đọc `result.ok`; chỉ thông báo thành công khi `ok === true`. Nếu `ok === false` với `errorCode === 'STALE_VERSION'` thì gợi ý tải lại. |
| Withdrawn không được resolve (GBR-075) | Ẩn nút Resolve/Reject khi `status === 'Withdrawn'`. |
| `expectedVersion` conflict (GBR-014) | Mọi command đều gửi `expectedVersion` lấy từ danh sách; server trả MSG111 → toast và tải lại. |
| Quyền staff + branch + ownership (UC-34 booking) | Nút hành động disable khi `canResolve === false` hoặc `branchMatch === false`; tooltip giải thích. |
| Không viết lại logic booking/change | UI web gọi `POST /api/support-requests/{id}/change-appointment`; đổi/hủy lịch do server thực hiện trong transaction. |
| Notification không chứa chẩn đoán (GBR-081) | Web chỉ hiển thị `notificationCode` (MSG31-38) làm text, không render body lâm sàng. |

## Cấu trúc thư mục

```
web/src/features/support-inbox/
├── types.ts                # DTO + enum, bám sát mobile/.../appointment.types.ts
└── SupportInboxPage.tsx    # trang /support-inbox
```

`App.tsx` thêm:
- import `SupportInboxPage`.
- route `<Route path="support-inbox" element={<Protected permission="appointments.support.handle"><SupportInboxPage /></Protected>} />`.
- menu item "Hộp thư hỗ trợ" chỉ hiện với tài khoản Staff (accountType !== 'Public').

## API contract (UI ↔ backend TV5, khi có)

| Method & path | Mô tả | Trả về |
| --- | --- | --- |
| `GET /api/support-requests?statuses=&branchId=&page=&pageSize=` | Hàng đợi cho staff | `{ items: SupportRequestDto[], total, page, pageSize }` |
| `GET /api/support-requests/{id}` | Chi tiết yêu cầu + appointment | `SupportRequestDetail` (mở rộng từ `SupportRequestDto` thêm `appointment` đầy đủ) |
| `POST /api/support-requests/{id}/verify-requester` | `{ requestKey, expectedVersion, notes? }` | `SupportRequestDto` |
| `POST /api/support-requests/{id}/change-appointment` | `StaffChangeCommand` (Change/Cancel) | `StaffChangeResult` gồm appointment và request/version mới |
| `POST /api/support-requests/{id}/resolve` | `StaffResolveCommand` | `StaffResolveResult` |
| `POST /api/support-requests/{id}/reject` | `StaffResolveCommand` với `decision='Reject'` | `StaffResolveResult` |

UI production sử dụng các endpoint trên. Mỗi thao tác phát sinh `requestKey` riêng;
server kiểm `expectedVersion`, quyền và membership chi nhánh lại trước mutation.
UI lấy `appointmentActionCommitted` từ request detail để bật Resolve; không quét lịch
sử appointment để đoán. Response change được áp dụng trực tiếp nên một GET đọc lại
không còn là điều kiện để nút Resolve hoạt động. Request key của verify/change/resolve/
reject được giữ khi retry lỗi mạng hoặc 5xx và chỉ thay khi command hoàn tất hoặc stale.

## Realtime (SignalR)

- Dùng hub sẵn có `/hubs/notifications`; web client dùng `@microsoft/signalr` và token trong session hiện tại.
- Backend resolve branch thật từ `SchedulingMember -> Employee.AccountId`, không dùng account id giả làm branch id.
- `SupportRequestService` phát `supportRequestUpdated` sau commit tới `staff:branch:{branchId}` cho create/verify/change/resolve/reject/withdraw.
- `SupportInboxPage` coi event là tín hiệu reload danh sách hiện tại. `RequestDrawer` nhận một `refreshToken` riêng để reload detail khi request đang mở bị thay đổi từ tab/account khác.
- Client reconnect tự động và reload một lần khi kết nối được khôi phục; unsubscribe/stop connection khi staff logout hoặc component unmount.
- Không cập nhật lạc quan lifecycle từ payload SignalR; mọi nội dung hiển thị vẫn lấy lại qua API có kiểm quyền.

## Persistence và transaction

- `APPOINTMENT_SUPPORT_REQUEST`, `APPOINTMENT_SUPPORT_HISTORY` và
  `SUPPORT_REQUEST_RECEIPT` giữ lifecycle, audit và idempotency.
- Đổi/hủy gọi `IAppointmentChangeService`; support service không sao chép logic
  availability/reservation. Resolve kiểm `AppointmentChange.RequestKey` đã commit.
- Resolve/reject ghi support history và appointment outbox trong transaction; notification
  cho requester được dispatcher tạo sau commit.
- `ConfirmedToAttend` là cột riêng trên Appointment; command không tạo Visit và không
  thay đổi `Appointment.Status`.

## Kiểm thử thủ công

1. Chạy backend và `cd web; npm run dev`, đăng nhập tài khoản nhân viên có quyền hộp thư hỗ trợ → thấy menu "Hộp thư hỗ trợ".
2. Từ web khách hàng hoặc mobile, gửi yêu cầu đổi/huỷ cho một lịch hẹn (dưới mốc tự phục vụ) → yêu cầu xuất hiện ở `/support-inbox`, lọc "Chờ staff".
3. Mở yêu cầu `Change` → xác minh người yêu cầu → "Đổi sang khung mới" chọn `newStartAt` + lý do → server trả `ok=true` → bật nút xử lý.
4. Xử lý → danh sách tải lại; dòng chuyển `Resolved`; người yêu cầu nhận thông báo S26.
5. Người yêu cầu rút một yêu cầu khác → staff mở thấy `Withdrawn`, không có nút xử lý/từ chối.
6. Đăng nhập tài khoản Public → menu nhân viên ẩn; mở thẳng `/support-inbox` → "Không có quyền".
7. Mở cùng yêu cầu ở hai tab, xử lý ở tab 1 rồi thao tác ở tab 2 → server trả 409 (MSG111) → thông báo và tải lại.

## Phối hợp với mobile

- `mobile/src/app/(home)/staff/requests/index.tsx` và `[id].tsx` gọi cùng API với web qua `mobile/src/features/support-inbox/support-requests.api.ts` (`GET /api/support-requests`, `GET /api/support-requests/{id}`, `POST .../verify-requester`, `.../change-appointment`, `.../resolve`, `.../reject`) bằng token phiên staff. Không còn dùng `appointmentMockRepo`, không gán cứng chi nhánh: server lọc theo chi nhánh của staff và trả cờ `permission`.
- Mobile dùng cùng shape DTO với `web/src/features/support-inbox/types.ts` (`SupportRequestDetail`, `StaffChangeCommand`, `StaffResolveCommand`). `requestKey` là UUID giữ nguyên khi thử lại cùng nội dung sau lỗi mạng/5xx.
- Đổi lịch trên mobile: chọn ngày (21 ngày tới) và khung giờ trống từ `GET /api/availability` theo chi nhánh/nha sĩ/dịch vụ/thời lượng của lịch hẹn; mặc định chọn giờ bệnh nhân đề xuất (`proposedChange.newStartAt`). Server vẫn kiểm tra lại khung giờ khi `change-appointment`. Từ chối bắt buộc nhập lý do (`decisionReason`) như web.
- Màn "Lịch bị ảnh hưởng" trên mobile đã gỡ khỏi menu staff vì backend chưa có endpoint preview lịch bị ảnh hưởng theo tuần; staff dùng màn lịch làm việc trên web.

## Traceability

- UC: UC-05, UC-06, UC-34 (booking portion).
- Screens: S12 (mobile) ↔ S26-Staff (web, mới).
- GBR: 007, 012, 013, 014, 015, 031, 073, 075, 077, 078, 081, 123.
- MSG: 31-38, 94, 111, 144.
- AT: AT16 (phần xử lý hỗ trợ).
