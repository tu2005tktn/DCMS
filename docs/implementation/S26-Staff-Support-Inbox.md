# S26 — Staff Support Inbox (Web)

> **Trạng thái**: UI mock đầy đủ, chờ backend TV5. Build web (`web/`), Playwright 4/4 pass.

Phạm vi: module web cho staff **xem và xử lý** các yêu cầu hỗ trợ (đổi/huỷ/xác nhận) do booker & attendee gửi lên. Bám sát đặc tả S26 (`docs/specs/S26-Notifications-Reminders.md`) và màn hình mobile S12 (`mobile/src/app/(home)/staff/requests`).

Tài liệu này mô tả phần UI web — backend TV4/TV5 đã được đặc tả trong S26. Web chỉ là lớp hiển thị + gọi command theo hợp đồng mock DTO chung (`mobile/src/features/appointments/appointment.types.ts`).

## Phạm vi đợt này

- Trang **Hộp thư hỗ trợ** (`/support-inbox`) cho staff với quyền `appointments.support.handle` (sẽ dùng chung policy nhóm "staff clinic ops"; placeholder tới khi backend chốt permission key).
- Lọc theo trạng thái (Cần xử lý / Chờ staff / Đang xem xét / Đã đóng), theo chi nhánh mà staff phụ trách.
- Drawer chi tiết yêu cầu:
  - Thông tin appointment liên quan (mã, chi nhánh, ghế, nha sĩ, thời gian).
  - Thông tin requester (booker/attendee, accountId/ẩn danh, SĐT).
  - Lịch sử thay đổi `history` của appointment.
  - Cờ quyền `canResolve`, `canEdit`, `branchMatch`, `ownershipMatch` (do server tính, mock hiện trả `true` cho mọi staff).
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
| Không viết lại logic booking/change | UI web chỉ gọi `appointmentMockRepo.staffChange`; backend sẽ thay thế bằng HTTP thật. |
| Notification không chứa chẩn đoán (GBR-081) | Web chỉ hiển thị `notificationCode` (MSG31-38) làm text, không render body lâm sàng. |

## Cấu trúc thư mục

```
web/src/features/support-inbox/
├── types.ts                # DTO + enum, bám sát mobile/.../appointment.types.ts
├── supportInbox.mock.ts    # mock repo cho staff (list/detail/commands)
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
| `POST /api/support-requests/{id}/change-appointment` | `StaffChangeCommand` (Change/Cancel) | `StaffChangeResult` |
| `POST /api/support-requests/{id}/resolve` | `StaffResolveCommand` | `StaffResolveResult` |
| `POST /api/support-requests/{id}/reject` | `StaffResolveCommand` với `decision='Reject'` | `StaffResolveResult` |

Cho tới khi backend có, UI sử dụng `supportInbox.mock.ts` (in-memory) để staff thấy luồng đầy đủ.

## Dữ liệu mock (chỉ Development)

Mock tạo 6 yêu cầu mẫu thuộc branch 1 (mặc định staff phụ trách):
- 2 `Change` Pending.
- 1 `Cancel` InReview.
- 1 `ConfirmToAttend` Pending.
- 1 `Change` Resolved.
- 1 `Cancel` Withdrawn (chỉ để staff thấy lịch sử; UI không cho phép resolve).

Mỗi dòng gắn với `AppointmentDto` tối thiểu: `id`, `code`, `version`, `status`, `startAt`, `endAt`, `branchId`, `branchName`, `dentistName`, `attendeeName`, `booker`.

## Kiểm thử thủ công (Playwright + tay)

1. `cd web; npm run dev`, đăng nhập `assistant.demo` (tài khoản Staff) → thấy menu "Hộp thư hỗ trợ".
2. Mở `/support-inbox` → thấy 6 yêu cầu mock, lọc "Chờ staff" → 3 dòng.
3. Bấm vào `Change` Pending → drawer hiển thị lịch + lý do; bấm "Đổi sang khung mới" → điền `newStartAt` + lý do → command `staffChange` → trả `ok=true` → bật nút Resolve.
4. Resolve với lý do → danh sách tải lại; dòng chuyển `Resolved`.
5. Mở yêu cầu `Cancel` đang `Withdrawn` → không thấy nút Resolve/Reject; chỉ thấy lịch sử.
6. Đăng nhập `patient.demo` (tài khoản Public) → menu Staff ẩn; gõ thẳng URL `/support-inbox` → trang "Không có quyền".
7. Mô phỏng lỗi `STALE_VERSION`: trong mock, tăng `version` của request lên khi staff đang mở drawer → bấm Resolve → toast "Phiên làm việc đã thay đổi, vui lòng tải lại." + danh sách refresh.

## Phối hợp với mobile

- `mobile/src/app/(home)/staff/requests/index.tsx` và `[id].tsx` đã hiện thực cùng DTO và command; web dùng chung `SupportRequestDto`/`StaffChangeCommand`/`StaffResolveResult` từ `appointment.types.ts` (khi chuyển sang chia sẻ package, copy sang `web/src/features/support-inbox/types.ts` giữ nguyên shape).
- Khi backend TV5 triển khai, cả hai phía chỉ cần thay implementation bên trong repo (mock → HTTP) — UI không đổi.

## Traceability

- UC: UC-05, UC-06, UC-34 (booking portion).
- Screens: S12 (mobile) ↔ S26-Staff (web, mới).
- GBR: 007, 012, 013, 014, 015, 031, 073, 075, 077, 078, 081, 123.
- MSG: 31-38, 94, 111, 144.
- AT: AT16 (phần xử lý hỗ trợ).
