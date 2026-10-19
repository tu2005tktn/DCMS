# S26+ · Realtime Notification & Booking Data Fix — PLAN

> Tác giả: assistant · Ngày: 2026-10-17 · Branch: `Trien`
> Phạm vi: 3 vấn đề người dùng báo + SignalR realtime.
> Phong cách: phased — dừng sau mỗi giai đoạn để bạn verify.

---

## 0. Bối cảnh thực tế (rất quan trọng)

Tôi đã đọc codebase và thấy **một số giả định ban đầu của bạn là sai**:

| Bạn nghĩ | Thực tế trong code |
|---|---|
| "Đặt lịch thành công" | `BookingScreen.tsx` đã gọi `bookingApi.create` (real `/api/appointments`). Backend `AppointmentEndpoints` đã có. Nhưng `AppointmentHubScreen` (màn "Lịch hẹn của tôi") đang đọc từ **`appointmentMockRepo`** (mock in-memory). Mock **không lưu** booking mới → reload = 3 lịch seed cứng. |
| "Thông báo biến mất khi reload" | `NotificationListScreen` đang dùng `notificationMockRepo` (vì `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=false`). Backend TV5 (notification API) **CHƯA TỒN TẠI** trong `DCMS.Api` — không có file `*Notification*.cs`, không có `MapGet("/api/notifications")`. Reload = re-seed từ 4 hardcode items. |
| "SignalR" | Backend chưa có `AddSignalR`, `MapHub`, package `Microsoft.AspNetCore.SignalR` chưa được thêm vào `.csproj`. Mobile chưa có `@microsoft/signalr`. Web staff inbox chỉ dùng mock, không có SignalR client. |

**Trạng thái hiện tại**: hub và notification realtime đã có. Staff support inbox dùng cùng hub, join group theo branch thật và nhận `supportRequestUpdated` để tự đồng bộ list/detail qua API mà không cần tải lại trang.

---

## 1. Phân kỳ (phased)

### Giai đoạn 1 — **Fix bug "đặt lịch xong nhưng list trống" + "thông báo biến mất"** (1 commit)

**Cách làm**:
- **Bug 1**: Tạo `appointmentLocalStore.ts` dùng `AsyncStorage`. Cập nhật `appointmentMockRepo`:
  - `createRequest` đã có → bổ sung `seed()` đọc từ AsyncStorage lúc mount, ghi lại sau mỗi mutation.
  - Trả lời trực tiếp câu hỏi của bạn: **không cần đợi backend**. Khi backend có, chuyển `USE_REAL_APPOINTMENT_API` env, mobile sẽ tự dùng API thật, mock sẽ được dùng làm offline cache.
- **Bug 2**: Backend TV5 thật sự chưa tồn tại (xác nhận ở §0). Hai lựa chọn:
  - (a) **Tôi tạo backend notification API tối thiểu** trong giai đoạn 2 (cùng với SignalR). Ở giai đoạn 1, giữ mock nhưng **persist qua AsyncStorage** + **tạo notification `AppointmentBooked` ngay trong `BookingScreen` khi booking thành công** (qua mock, dedupe theo `appt:{id}:booked`).
  - (b) Hoặc bạn xác nhận có backend TV5 ở branch khác → tôi sẽ rebase.

Tôi đề xuất (a): mobile persist notification + tạo inline, fix được trải nghiệm reload ngay. Giai đoạn 2 sẽ thay bằng real API.

**File thay đổi**:
- `mobile/src/services/storage/appointment-local-store.ts` (mới, ~60 LOC, AsyncStorage wrapper)
- `mobile/src/services/storage/notification-local-store.ts` (mới, ~60 LOC)
- `mobile/src/features/appointments/appointment.mock.ts` (sửa ~30 dòng, hydrate/persist)
- `mobile/src/features/appointments/notification.mock.ts` (sửa ~30 dòng)
- `mobile/src/features/appointments/BookingScreen.tsx` (sửa ~10 dòng, gọi `notificationMockRepo.create({kind:'AppointmentBooked', dedupeKey: 'appt:{id}:booked'})` ngay khi `result.kind==='ok'`)
- Có thể cần `mobile/src/services/storage/async-storage.ts` (helper) nếu chưa có.

**Test**:
- Đặt lịch → reload app → list có lịch mới ✅
- Đặt lịch → vào Notifications → thấy "Đặt lịch thành công" → reload → vẫn còn ✅

**DỪNG LẠI ở đây để bạn verify** trước khi qua giai đoạn 2.

---

### Giai đoạn 2 — **Backend SignalR Hub + Notification API** (1-2 commits)

**Phạm vi backend** (1 commit lớn):
- Thêm package `Microsoft.AspNetCore.SignalR` vào `DCMS.Api.csproj`.
- Tạo `NotificationsHub` (`/hubs/notifications`) với:
  - JWT auth (dùng `OnTokenValidated` đã có — SignalR negotiate endpoint cũng qua JWT).
  - Mỗi connection thuộc group `account:{accountId}` + nếu staff role: `staff:branch:{branchId}`.
  - Client method (server→client): `notificationCreated`, `appointmentChanged`, `supportRequestUpdated`.
- Tạo `NotificationService` + `INotificationStore` (in-memory đầu tiên, dễ thay bằng DB) với methods:
  - `NotifyAccountCreated(accountId, kind, title, body, deepLink, dedupeKey)` — write store + push SignalR.
  - `NotifyStaffBranchChanged(branchId, ...)` — push SignalR cho group staff.
- Tạo `NotificationEndpoints` thật (`MapNotificationEndpoints`):
  - `GET /api/notifications?unreadOnly&limit&cursor` (real)
  - `POST /api/notifications/mark-read` (real, với expectedAccountId)
  - `POST /api/notifications/{id}/mark-read`
  - `GET /api/notifications/unread-count`
  - `POST /api/notifications/check-deep-link`
- Tạo **`/api/test/realtime/ping`** (chỉ Development) — endpoint test để fire SignalR event thủ công, demo không cần booking handler thật.
- Wire vào `Program.cs`: `builder.Services.AddSignalR(); ... app.MapHub<NotificationsHub>("/hubs/notifications");`
- Tích hợp vào flow booking thật (nếu có) bằng cách gọi `INotificationService.NotifyAccountCreated` trong `IAppointmentService.CreateAsync`. Nếu handler chưa sẵn sàng, dùng test ping.

**File thay đổi (backend)**:
- `backend/src/DCMS.Api/DCMS.Api.csproj` (+1 line)
- `backend/src/DCMS.Api/Program.cs` (+3 line)
- `backend/src/DCMS.Api/Features/Realtime/NotificationsHub.cs` (mới, ~80 LOC)
- `backend/src/DCMS.Api/Features/Realtime/INotificationService.cs` (mới, ~30 LOC)
- `backend/src/DCMS.Api/Features/Realtime/NotificationService.cs` (mới, ~120 LOC, in-memory store + SignalR push)
- `backend/src/DCMS.Api/Features/Realtime/INotificationStore.cs` (mới, ~20 LOC, abstraction)
- `backend/src/DCMS.Api/Features/Realtime/InMemoryNotificationStore.cs` (mới, ~150 LOC)
- `backend/src/DCMS.Api/Features/Realtime/RealtimeEndpoints.cs` (mới, ~30 LOC, test ping + future booking hooks)
- `backend/src/DCMS.Api/Features/Notifications/NotificationEndpoints.cs` (mới, ~150 LOC, real API thay thế mock)
- `backend/src/DCMS.Application/Features/Notifications/NotificationContracts.cs` (mới, ~50 LOC, DTOs)

**Persist**: InMemory store **KHÔNG persist** theo yêu cầu `persist-event` của bạn. Đề xuất: thêm interface `INotificationStore` để ngày mai thay bằng EF Core không phá chỗ khác. Bạn OK?

**Test backend (manual)**:
- `dotnet run` → POST `/api/test/realtime/ping` (với token) → bật SignalR client bất kỳ (wscat) → thấy event.
- `GET /api/notifications` trả JSON thật (không còn mock).

**DỪNG LẠI ở đây** để bạn verify backend.

---

### Giai đoạn 3 — **Mobile SignalR client + realtime UI** (1-2 commits)

**Phạm vi mobile**:
- Thêm package `@microsoft/signalr` (Expo không tự có — cần `expo install` hoặc cài thẳng, tương thích Expo SDK mới).
- Tạo `services/realtime/signalr-client.ts`:
  - Singleton connection tới `${API_BASE}/hubs/notifications?access_token={token}`.
  - Reconnect exponential backoff (built-in SignalR đã có, chỉ config).
  - `useRealtime()` hook + `RealtimeProvider` ở root layout, khởi tạo khi `state.status === 'authenticated'`, teardown khi logout.
- Tạo `RealtimeToast` component: Banner top-of-screen hiển thị khi nhận `notificationCreated`, có nút "Mở" (deep link) và "Đóng".
- Wire `notificationRepo`:
  - Khi nhận `notificationCreated` từ SignalR → prepend vào local store (không cần reload API).
  - Khi mở app lần đầu → gọi `GET /api/notifications` (real) thay vì mock.
- Chuyển `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=true` làm mặc định (mock vẫn còn làm fallback nếu mất mạng).
- Bật `EXPO_PUBLIC_USE_REAL_APPOINTMENT_API` (nếu backend `AppointmentService` đã trả `listForAccount`) — sẽ trao đổi với bạn ở giai đoạn 2.

**File thay đổi (mobile)**:
- `mobile/package.json` (+1 dep)
- `mobile/src/services/realtime/signalr-client.ts` (mới, ~120 LOC)
- `mobile/src/features/realtime/RealtimeProvider.tsx` (mới, ~80 LOC)
- `mobile/src/features/realtime/RealtimeToast.tsx` (mới, ~150 LOC, animated banner)
- `mobile/src/features/appointments/notification.repo.ts` (sửa ~20 dòng, prepend từ SignalR)
- `mobile/src/app/_layout.tsx` (+1 dòng wrap RealtimeProvider)
- `mobile/src/services/config/env.ts` (thêm `EXPO_PUBLIC_API_HUB_BASE`)

**Test**:
- 2 thiết bị: (1) mobile đăng nhập user A; (2) curl/Postman gọi `/api/test/realtime/ping` với account A → mobile A thấy toast popup.
- Mobile B (staff role) → gọi ping staff branch → mobile B thấy toast khác.

---

### Giai đoạn 4 (optional, nếu còn thời gian) — **Web staff realtime** (1 commit)

- Web staff inbox (`web/src/features/support-inbox/`) thêm `@microsoft/signalr` client.
- Khi staff đang xem inbox, nhận toast mỗi khi user book/change/cancel/resolve.
- Tương tự mobile, có reconnect + persist last-seen cursor.

**File thay đổi (web)**:
- `web/package.json` (+1 dep)
- `web/src/features/realtime/realtime-client.ts` (mới, ~100 LOC)
- `web/src/features/realtime/RealtimeProvider.tsx` (mới, ~80 LOC)
- `web/src/features/realtime/StaffInboxToast.tsx` (mới, ~120 LOC)
- `web/src/app/App.tsx` (+1 wrap)

---

## 2. Tổng commit dự kiến

| # | Commit | Nội dung | Kích thước |
|---|---|---|---|
| 1 | `fix(mobile/S12+S26): persist mock data via AsyncStorage` | Giai đoạn 1 | ~5 file, +200 LOC |
| 2 | `feat(backend): SignalR + notification API` | Giai đoạn 2 | ~9 file mới, +700 LOC |
| 3 | `feat(mobile): realtime SignalR client + toast` | Giai đoạn 3 | ~6 file, +500 LOC |
| 4 | `feat(web/staff): realtime inbox notifications` | Giai đoạn 4 (optional) | ~4 file, +400 LOC |

---

## 3. Câu hỏi cần bạn xác nhận TRƯỚC khi tôi bắt đầu code

1. **Bug 2 (notification)**: Bạn muốn tôi **tự tạo backend notification API trong giai đoạn 2** (đường tắt), hay **bạn đã có TV5 ở branch khác** muốn tôi rebase vào? Nếu rebase, cho tôi xem file/branch.
2. **Persist SignalR event**: Tôi đề xuất dùng **in-memory store** trước, có `INotificationStore` interface để sau này thay bằng EF Core. OK không? Hay bạn muốn persist vào DB ngay từ đầu (thêm ~200 LOC Entity + migration)?
3. **Giai đoạn 4 (web staff realtime)**: Có cần làm luôn không, hay để mobile xong rồi tính?
4. **Trigger SignalR**: Bạn OK với test endpoint `/api/test/realtime/ping` (chỉ Dev env) để demo, thay vì phải chờ handler booking thật tích hợp?
5. **Target branch**: Toàn bộ push lên `Trien` như 2 commit trước, hay tách branch `feat/s26-realtime`?

Nếu OK tất cả, tôi bắt đầu **Giai đoạn 1 ngay**.
