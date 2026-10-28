# S58 11 — kế hoạch và bằng chứng

Trạng thái: In Progress. Contract ở [đặc tả](../specs/S58-STAFF-WEB-IDLE.vi.md).

1. Mở rộng `AuthSession` bằng ClientKind/LastInteractiveActivityAt; migration
   backfill staff session từ CreatedAt để không reset idle về thời điểm deploy.
2. Dùng lại AuthenticationService/refresh chain: kiểm idle ở ValidateSession,
   Refresh và activity, revoke session + refresh nguyên tử khi expired.
3. Lưu policy typed/version/history/idempotency riêng trong lúc chờ DCMS-119;
   preview đếm staff web session từ PostgreSQL, không write.
4. UI owner chỉnh timeout và preview; staff web phát activity có throttle từ
   tương tác tin cậy, hiển thị warning và hết phiên. Không lưu draft bệnh án.
5. Test PostgreSQL thực, browser mock và browser thật ghi kết quả riêng.

## Bằng chứng

API contract triển khai:

```http
PUT /api/settings/staff-idle
Content-Type: application/json

{"expectedVersion":1,"requestKey":"ba4f4cae-14b1-47c4-97c0-7e196030dca9","reason":"Giới hạn phiên staff web","staffIdleTimeoutMinutes":5}
```

Response: `{"version":2,"effectiveAt":"...","staffIdleTimeoutMinutes":5}`.
`POST /api/settings/staff-idle/preview` nhận cùng timeout và trả
`affectedActiveSessions`/`evaluatedAt`. `GET /api/auth/session/idle` và
`POST /api/auth/session/activity` nhận `{expectedVersion,requestKey}` và trả
server time, mốc activity, deadline, timeout, `activityVersion`; không nhận
timestamp client. Touch vượt throttle ghi command receipt cùng session trong
transaction; request trong 30 giây chỉ ghi command receipt để retry giữ đúng
kết quả, không đổi interactive timestamp. Lệnh đổi policy có
expectedVersion/requestKey/audit nguyên tử riêng.

27/10/2026:

- Migration `S58StaffWebIdle` và `S58StaffActivityCommands` áp dụng PostgreSQL
  dev; backfill phiên cũ từ CreatedAt và activityVersion=1. `/health` trả
  API/database healthy.
- Schema PostgreSQL test cô lập: `StaffIdleApiTests` kiểm 5 phút, poll/refresh,
  hai client cùng session, activity theo giờ server, version/key/retry, policy giảm/tăng, không
  hồi sinh session revoked, Patient riêng và từ chối commit phiếu sau idle.
  Nhóm auth/billing regression: 42/42 pass; unit tests: 49/49 pass.
- Browser thật/API/PostgreSQL dev: owner mở màn settings và preview không lưu
  version; cùng run có luồng billing/reprint cũ. Playwright 5/5 pass.
- Browser mock riêng: warning 30 giây; mô phỏng 401 khi đang nhập phiếu,
  sau đăng nhập actor khác draft không xuất hiện. Mock không chứng minh server
  idle; bằng chứng server ở integration test PostgreSQL nêu trên.
- `dotnet build backend/DCMS.sln --no-restore`, `npm run build` pass. NuGet
  còn cảnh báo Pkcs 9.0.0 và nguồn audit package không truy cập được.

Vẫn In Progress: DCMS-119/128 chưa có trong checkout, nên settings đang là
resource typed singleton của ứng dụng một clinic, chưa nối action grant và
clinic scope chung. Toàn suite integration có các lỗi Google/recovery ngoài
phạm vi từ đợt trước; không tuyên bố suite tổng thể pass.
