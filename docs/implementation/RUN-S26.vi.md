# Hướng dẫn chạy & test S26 — Notifications & Appointment Reminders

> Bám sát `docs/specs/S26-Notifications-Reminders.md` và `docs/implementation/S26-Notifications-Reminders.md`.

## 0. Yêu cầu môi trường

- ✅ .NET 10 SDK (`dotnet --list-sdks` phải có 10.x)
- ✅ Node.js 22 + npm
- ✅ Docker Desktop với Linux engine **đang chạy** (trên Windows mở app Docker Desktop, đợi icon xanh)
- ✅ PostgreSQL container chạy qua docker compose

## 1. Khởi động lần đầu (PowerShell, từ `D:\SEP\SE\DCMS\DCMS-clone`)

```powershell
# 1.1 Khởi động Docker Desktop (nếu chưa) — mở app, đợi icon xanh ~30-60s
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# 1.2 Khởi động PostgreSQL
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
# Phải thấy STATUS = "Up" / "healthy"

# 1.3 Restore + build backend
$dotnet = 'dotnet'   # đã có global
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore

# 1.4 Migration + seed demo accounts
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo

# 1.5 Lấy mật khẩu demo
notepad .env    # tìm dòng DEV_SEED_PASSWORD=<giá-trị>
```

## 2. Chạy app (2 terminal song song)

### Terminal A — API
```powershell
cd D:\SEP\SE\DCMS\DCMS-clone
& .\infrastructure\scripts\start-api.ps1
# API chạy ở http://localhost:5080
# Health: Invoke-RestMethod http://localhost:5080/health
```

### Terminal B — Web (staff)
```powershell
cd D:\SEP\SE\DCMS\DCMS-clone\web
npm.cmd install
npm.cmd run build
npm.cmd run dev
# Web ở http://localhost:5173
```

### Terminal C — Mobile (patient app — Expo)
```powershell
cd D:\SEP\SE\DCMS\DCMS-clone\mobile
npm.cmd install
# Bật mock S26 (mặc định — backend TV5 chưa sẵn sàng)
$env:EXPO_PUBLIC_USE_REAL_NOTIFICATION_API = "false"
npm.cmd start
# Mở Expo Go trên điện thoại, quét QR.
# Hoặc nhấn 'w' để mở web preview tại http://localhost:8081
```

## 3. Test 2 scenario S26

### SC1 — Patient app: xem notification + nhắc lịch (mock)

1. Mở mobile, đăng ký tài khoản mới qua màn S02 (SĐT Việt Nam 10 số, mật khẩu theo policy).
2. Đăng nhập bằng tài khoản vừa tạo.
3. Vào **S15/S11** → đặt lịch khám thật qua API:
   - Chọn chi nhánh, nha sĩ, dịch vụ, khung giờ.
   - Submit → nhận mã lịch (vd: `AP-20261016-001`).
4. Vào tab **Thông báo** ở home:
   - Thấy notification "Đặt lịch thành công" + badge **MOCK** ở góc phải header.
   - Thấy notification "Nhắc lịch 24h" (mock seed cho accountId=1).
5. Tap notification unread → chấm xanh biến mất; mở deep link `/appointments/{id}` → server (mock) check quyền lại → navigate tới chi tiết.
6. Test **đổi lịch** trong mobile → vào lại thông báo → reminder cũ `Superseded`, reminder mới theo version mới.
7. Test **huỷ lịch** → notification "Lịch khám đã bị huỷ" xuất hiện, reminder bị vô hiệu.

> Tiêu chí nghiệm thu kiểm tra:
> - [x] S26 list **chỉ** trả về notification của account hiện tại.
> - [x] Read notification không đổi `Appointment.status`.
> - [x] Notification KHÔNG chứa chẩn đoán.
> - [x] External fail (`hasExternalFailure=true`) → badge "Gửi thất bại (đã có nhân viên xử lý)".

### SC2 — Staff web: Hộp thư hỗ trợ (mock UI)

> ✅ Web đã có UI staff inbox tại `/support-inbox` (route + menu). Tới khi backend TV5 triển khai, UI đọc/ghi qua `web/src/features/support-inbox/supportInbox.mock.ts` (in-memory) — DTO bám sát `mobile/.../appointment.types.ts`.
> Spec: [`S26-Staff-Support-Inbox.md`](../implementation/S26-Staff-Support-Inbox.md).

1. Mở web `http://localhost:5173`, đăng nhập `assistant.demo` / mật khẩu `DEV_SEED_PASSWORD`.
2. Menu staff hiện **Hộp thư hỗ trợ** (permission `appointments.support.handle` — cấp cho ClinicAssistant, Dentist, Admin).
3. Trang inbox mặc định "Cần xử lý" (Pending + InReview). Mock có sẵn 6 yêu cầu đa dạng: 2 `Change` Pending, 1 `Cancel` InReview, 1 `ConfirmToAttend` Pending, 1 `Change` Resolved, 1 `Cancel` Withdrawn.
4. Mở drawer yêu cầu `Change` Pending:
   - Tab **Tổng quan** thấy thông tin yêu cầu + lịch + lịch sử.
   - Tab **Xử lý**:
     - Chọn khung giờ mới + lý do → bấm **Đổi lịch** → command `staffChange` commit → button chuyển thành **Đã thay đổi lịch**, nút **Xử lý yêu cầu** bật.
     - Bấm **Xử lý yêu cầu** → xác nhận → đóng yêu cầu.
   - Bấm **Từ chối** → modal lý do → từ chối với lý do bắt buộc.
5. Mở yêu cầu `Withdrawn`: banner thông báo + nút Xử lý/Từ chối đều disabled.
6. Test phân quyền:
   - Đăng nhập `guest.demo` → menu Staff ẩn; gõ `/support-inbox` → trang 403.
   - Đăng nhập `admin.demo` / `dentist.demo` → có permission nhưng backend TV5 chưa có; UI dùng mock.

> Tiêu chí nghiệm thu:
> - [x] Resolve chỉ `ok=true` khi booking action cần thiết đã commit.
> - [x] Với Change/Cancel: nút Xử lý yêu cầu disabled cho tới khi staffChange xong.
> - [x] Withdrawn: ẩn resolve/reject; chỉ đọc lịch sử.
> - [x] Permission: chỉ staff có `appointments.support.handle` mới vào được.

## 4. Kiểm tra health

```powershell
# API health
Invoke-RestMethod http://localhost:5080/health
# Kỳ vọng: { api: "healthy", database: "healthy" }

# Web health
Invoke-RestMethod http://localhost:5173/health

# OpenAPI
Start-Process http://localhost:5080/openapi/v1.json
```

## 5. Troubleshooting

| Vấn đề | Xử lý |
| --- | --- |
| `docker compose ... failed to connect to docker API` | Mở Docker Desktop, đợi icon xanh, chạy lại |
| `Invoke-RestMethod http://localhost:5080/health` 5xx | Xem log API ở terminal A; `docker compose logs postgres` |
| `npm.cmd install` warning peer deps | Bỏ qua nếu app vẫn chạy; xoá `node_modules` + `package-lock.json` rồi `npm.cmd install` lại |
| Expo không quét được QR | Cùng Wi-Fi; set `EXPO_PUBLIC_API_URL=http://<LAN-IP>:5080` (vd 192.168.x.x) |
| Build backend bị khoá DLL | Dừng API bằng Ctrl+C, build lại |

## 6. Tắt project

```powershell
# Dừng API: Ctrl+C ở terminal A
# Dừng web: Ctrl+C ở terminal B
# Dừng mobile: Ctrl+C ở terminal C

# Giữ DB để test tiếp (không xoá data):
docker compose --env-file .env -f infrastructure/docker/compose.yaml stop

# Xoá hoàn toàn (mất data):
docker compose --env-file .env -f infrastructure/docker/compose.yaml down -v
```
