# S26 — Notifications & Appointment Reminders (mobile patient app)

Bằng chứng tích hợp mobile cho S26 theo spec [S26-Notifications-Reminders.md](../specs/S26-Notifications-Reminders.md). Triển khai theo hướng contract-first: UI và repo đã sẵn sàng, chờ backend TV5 expose endpoint thật.

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

## Việc còn lại (chờ backend TV5)

- Backend expose 5 endpoint ở trên.
- Khi backend sẵn sàng, set `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=true` — UI chạy không đổi source.
- Sau khi verify, có thể xoá `notification.mock.ts` và adapter (giữ lại tạm để test offline).
