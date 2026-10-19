# S26 — Notifications & Appointment Reminders (mobile patient app)

Bằng chứng tích hợp S26 theo spec [S26-Notifications-Reminders.md](../specs/S26-Notifications-Reminders.md). Backend TV5 và mobile HTTP adapter đã được nối thật; mock chỉ còn là lựa chọn offline khi đặt `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=false`.

## Backend đã triển khai

- EF schema/migration cho `IN_APP_NOTIFICATION`, `NOTIFICATION_DELIVERY`,
  `APPOINTMENT_REMINDER` và `STAFF_HANDLING_ITEM`.
- `AppointmentOutboxEvent` được booking/change/cancel ghi trong transaction hiện có;
  background dispatcher chỉ xử lý event sau commit.
- Dedupe theo unique `DedupeKey`; chạy dispatcher lặp không tạo notification/reminder trùng.
- Đổi/hủy supersede reminder cũ; reminder mới giữ appointment version và recheck
  version/status/account trước khi gửi.
- External delivery có tối đa 3 lần theo các mốc 1/5/15 phút. Sau lần cuối,
  delivery là `Failed`, notification phản ánh `hasExternalFailure` và tạo staff
  handling item; Appointment không rollback.
- Deep link nạp lại Appointment và kiểm ownership/patient link hiện tại; mark-read
  không đổi Appointment lifecycle.

## Phạm vi đã code (mobile patient app)

| File | Vai trò |
| --- | --- | 
| `mobile/src/features/appointments/notification.types.ts` | DTO + enum + envelope chuẩn hoá giữa UI ↔ backend. |
| `mobile/src/features/appointments/notification.api.ts` | HTTP client gọi endpoint TV5 (`/api/notifications`, `/mark-read`, `/unread-count`, `/check-deep-link`). |
| `mobile/src/features/appointments/notification.repo.ts` | Adapter: chuyển đổi mock ↔ real theo `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API`. |
| `mobile/src/features/appointments/notification.mock.ts` | Mock repo hiện có — giữ nguyên để chạy UI khi backend chưa sẵn sàng. |
| `mobile/src/app/(home)/notifications.tsx` | S26 list screen — đã chuyển sang dùng `notificationRepo`, truyền token từ SessionProvider. |
| `mobile/src/config/env.ts` | Thêm `useRealNotificationApi` cờ. |
| `mobile/.env.example` | Document flag `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API`. |

## Endpoint contract chốt với backend TV5

```
GET    /api/notifications?unreadOnly=&limit=&cursor=
       → 200 { items: NotificationDto[], nextCursor: string | null }

POST   /api/notifications/{id}/mark-read
       Body: { expectedAccountId: number }
       → 200 { ok: true } | 409 MSG111 (stale version)

POST   /api/notifications/mark-read
       Body: MarkReadRequest  (tối đa 100 ids)
       → 200 MarkReadResult

GET    /api/notifications/unread-count
       → 200 { count: number }

POST   /api/notifications/check-deep-link
       Body: { deepLink: string }
       → 200 { allowed: boolean, reason?: string }
```

## Nguyên tắc đã áp dụng (từ spec S26)

- Notification **KHÔNG** chứa chẩn đoán (GBR-081) — `NotificationDto.body` chỉ giữ thông tin lịch.
- In-app authoritative; external push tùy adapter/cấu hình (mock có 60% fail attempt 1 để demo retry).
- Retry max 3 lần, backoff 1/5/15 phút — `NotificationAttempt.attemptCount`, `nextRetryAt`.
- External fail **KHÔNG** rollback Appointment — chỉ tạo staff handling item (`hasExternalFailure=true`).
- Read notification/deep link **KHÔNG** đổi lifecycle lịch.
- Deep link permission check ở **server-side** (UI gọi `/check-deep-link` trước khi navigate).
- Idempotency theo `dedupeKey` — dispatch lặp không tạo trùng row.
- S26 list chỉ trả về notification của account hiện tại (`accountId` filter ở repo + backend).

## Cách chạy

```powershell
cd mobile
# Mock (mặc định, không cần backend):
npm.cmd start

# Real API (khi backend TV5 sẵn sàng):
$env:EXPO_PUBLIC_USE_REAL_NOTIFICATION_API = "true"
npm.cmd start
```

## Kiểm thử tay

1. Mở app, đăng nhập account bất kỳ (mock user `accountId=1`).
2. Vào tab **Thông báo** → thấy 3 notification seed (booked, reminder 24h, support request update).
3. Tap một notification unread → marker chấm xanh biến mất, body vẫn còn.
4. Tap notification có `deepLink` → navigate sang detail appointment (mock chấp nhận vì accountId khớp).
5. Sửa `dedupeKey` trong seed để có notification của account 2 → đăng nhập account khác (mock chưa hỗ trợ đổi user, dùng test API giả lập).
6. Bật `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=true` + chạy backend → đảm bảo mọi case (list/markRead/deepLink) đều dùng API thật.

## Ghi chú vận hành

- API thật là mặc định. Mock vẫn được giữ để demo offline có chủ đích.
- External email/push chỉ được tạo khi caller chọn kênh và adapter đã cấu hình; không
  có adapter sẽ retry hữu hạn rồi tạo handling item, không giả trạng thái delivered.
