# Chạy demo DCMS trên máy local (runbook)

Hướng dẫn khởi động, demo và kiểm thử dự án trên Windows/PowerShell. Đây là bản gộp ngắn cho
mục đích demo; chi tiết tính năng nằm ở [RUN-ACCOUNTS.vi.md](RUN-ACCOUNTS.vi.md), cài công cụ ở
[../SETUP.vi.md](../SETUP.vi.md), và bằng chứng UC-02 ở [../../docs/s03-account-recovery-evidence.md](../../docs/s03-account-recovery-evidence.md).

## Trạng thái hiện tại của repo

- Dự án chỉ có **môi trường Development**: `.env` (không commit) đã cấu hình sẵn DB, JWT và mật khẩu demo.
- Docker Desktop trên máy này nằm ở `%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe` (không phải `C:\Program Files\Docker`).
- .NET SDK local: `.tools\dotnet\dotnet.exe`; `web/node_modules` đã cài sẵn; file `web/.env` (không commit) trỏ API về `http://localhost:5080`.
- Ngày 13/10/2026 đã vá 6 lỗi chặn chạy/thử nghiệm UC-02 (xem "Lỗi đã sửa" bên dưới). Các thay đổi đang trong working tree, **chưa commit**: `backend/src/DCMS.Api/Features/Authentication/DevelopmentRecoveryEndpoints.cs`, `backend/src/DCMS.Api/appsettings.Development.json` (giới hạn rate dev), `backend/src/DCMS.Infrastructure/DependencyInjection.cs`, `backend/src/DCMS.Infrastructure/Features/Authentication/AccountRecoveryService.cs`, `backend/src/DCMS.Infrastructure/Features/Authentication/DevelopmentContactDeliveryAdapter.cs`, `backend/tests/DCMS.IntegrationTests/RecoveryApiTests.cs`, `web/tests/recovery.spec.ts`, `docs/README.md`, `docs/implementation/RUN-ACCOUNTS.vi.md`.

## Khởi động nhanh (mỗi lần demo)

Có thể dùng 3 terminal ở thư mục gốc `DCMS`.

**Terminal 1 — PostgreSQL:**

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps   # đợi (healthy)
```

**Terminal 2 — API (chỉ cần từ terminal đã import env; script tự đọc `.env`):**

```powershell
# Lần đầu hoặc khi có migration mới; script migrate + seed rồi tự thoát:
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo

# Chạy API:
& .\infrastructure\scripts\start-api.ps1
```

**Terminal 3 — web:**

```powershell
Set-Location web
npm.cmd run dev
```

Kiểm tra:

```powershell
Invoke-RestMethod http://localhost:5080/health     # {"api":"healthy","database":"healthy"}
Start-Process http://localhost:5173/sign-in        # màn hình đăng nhập
```

> Nếu web chưa từng build: `npm.cmd install` và `npm.cmd run build` trước `run dev` (build sạch, chỉ còn cảnh báo chunk > 500 kB đã biết).

## Mail, migration và seed

- `start-api.ps1 -Migrate -SeedDemo` apply migration còn thiếu rồi seed các account demo còn thiếu, **sau đó thoát** (không giữ server).
- Seed chạy ở Development, không ghi đẻ mật khẩu/vai trò/state của account đã tồn tại, không xóa dữ liệu.
- Không dùng `down -v` nếu muốn giữ dữ liệu demo.

## Tài khoản demo

| Tài khoản | Vai trò hiệu lực | Ghi chú |
| --- | --- | --- |
| `admin.demo` | Admin | menu Quản lý tài khoản |
| `owner.demo` | Clinic Owner + Dentist | không có menu quản trị |
| `dentist.demo` | Dentist | |
| `assistant.demo` | Clinic Assistant | |
| `dual.demo` | Dentist + Assistant | |
| `guest.demo` | Public/Guest | không có Patient |

Mật khẩu chung nằm ở biến **`DEV_SEED_PASSWORD` trong `.env`** ở thư mục gốc: mở file bằng editor để xem, **không** chép vào source, chat hoặc ảnh chụp màn hình.

## Kịch bản demo đề xuất (10 phút)

1. Đăng nhập `admin.demo` → xem "Tài khoản của tôi": vai trò được cấp / vai trò hiệu lực.
2. **Quản lý tài khoản**: tìm kiếm, phân trang, tạo nhân viên mới (MSG14), sửa vai trò, khóa/mở khóa (MSG15/MSG16), xem audit.
3. **Khôi phục tài khoản (UC-02)**: mở http://localhost:5173/recover → nhập username → MSG12 neutral → đọc mã → xác minh → đặt mật khẩu mới (MSG13) → đăng nhập bằng mật khẩu mới; mật khẩu cũ bị từ chối (MSG09).
4. (Tùy chọn) Mở 2 cửa sổ trình duyệt để thử lock/thu hồi vai trò có hiệu lực ngay với token đang dùng.

### Đọc mã xác minh khi chưa có kênh gửi thật

Chưa cấu hình provider mail/SMS. Ở Development adapter giữ mã trong **bộ nhớ tiến trình** và cho đọc lại qua endpoint yêu cầu đăng nhập:

```powershell
# 1. Đăng nhập admin, giữ token
$login = Invoke-RestMethod http://localhost:5080/api/auth/sign-in -Method Post `
    -ContentType 'application/json' `
    -Body (@{ username='admin.demo'; password='<DEV_SEED_PASSWORD>' } | ConvertTo-Json)
# 2. Gửi yêu cầu khôi phục cho account mục tiêu
Invoke-RestMethod http://localhost:5080/api/auth/recovery/request -Method Post `
    -ContentType 'application/json' -Body (@{ identifier='dentist.demo' } | ConvertTo-Json)
# 3. Đọc mã (thay {accountId}; tìm id qua /api/accounts?search=... khi đăng nhập admin)
Invoke-RestMethod "http://localhost:5080/api/dev/recovery/proof/{accountId}" `
    -Headers @{ Authorization = "Bearer $($login.accessToken)" }
# 4. Xác minh và đặt lại mật khẩu
$verify = Invoke-RestMethod http://localhost:5080/api/auth/recovery/verify -Method Post `
    -ContentType 'application/json' -Body (@{ identifier='dentist.demo'; proof='<mã vừa đọc>' } | ConvertTo-Json)
Invoke-RestMethod http://localhost:5080/api/auth/recovery/reset -Method Post `
    -ContentType 'application/json' -Body (@{ identifier='dentist.demo'; resetGrant=$verify.resetGrant; newPassword='<mật khẩu mới>'; confirmPassword='<mật khẩu mới>' } | ConvertTo-Json)
```

Mã xác minh **không** được ghi vào database, outbox, log hay audit. Endpoint chỉ tồn tại ở Development.

## Reset dữ liệu demo về ban đầu

- Reset DB dev (xóa toàn bộ schema và chạy lại): chỉ khi cố ý — dừng container, `docker compose --env-file .env -f infrastructure/docker/compose.yaml down -v`, `up -d`, rồi `start-api.ps1 -Migrate -SeedDemo`.
- Chỉ reset mật khẩu account demo: đổi lại bằng `DEV_SEED_PASSWORD` qua luồng khôi phục phía trên.

## Chạy kiểm thử tự động

```powershell
# Backend: unit + integration (schema riêng, không sửa dữ liệu development)
& .\infrastructure\scripts\test-accounts.ps1

# Browser (cần API + Vite đang chạy, demo đã seed; cài Chromium một lần)
Push-Location web; npx.cmd playwright install chromium; Pop-Location
& .\infrastructure\scripts\test-accounts.ps1 -Browser
```

Kết quả đợt 13/10/2026 (sau khi vá): unit **30/30**, integration **33/33** (2 lần chạy liên tiếp), backend build 0 warning/0 error, web build thành công, OpenAPI: `http://localhost:5080/openapi/v1.json`.

Browser tests: `accounts.spec.ts` **4/4 xanh**; `recovery.spec.ts` đã sửa lỗi cú pháp chặn toàn bộ spec (`=> 'POST'` thành `=== 'POST'`) và tăng cách ly (mỗi test hoàn tất luồng để token được tiêu thụ, đổi qua `dual.demo` cho test URL, mock kèm CORS headers, đợi UI settle sau lỗi). Test "wrong proof, weak password and mismatch" **còn flaky**: khi nút đang ở trạng thái in-flight (guard double-submit) Ant Design thêm icon loading vào accessible name, nên locator `exact` có thể không khớp; nguyên nhân đã xác định, cần ổn định bằng product fix (aria-label cố định hoặc data-testid) cho team web. Các luồng chức năng đã được chứng minh xanh bằng 33 integration test và chạy tay qua API/web.

## Lỗi đã sửa đợt 13/10/2026 (working tree, chưa commit)

| Lỗi | Triệu chứng | Sửa ở |
| --- | --- | --- |
| API không khởi động được | `Body was inferred but the method does not allow inferred body parameters` | `DevelopmentRecoveryEndpoints.cs` (thêm `[FromServices]`; .NET 10 đưa `FromServicesAttribute` về namespace `Microsoft.AspNetCore.Mvc`) |
| Thiếu token provider của Identity | Reset mật khẩu trả 503 `NotSupportedException` | `DependencyInjection.cs` (thêm `.AddDefaultTokenProviders()`, đăng ký concrete adapter cho dev aid) |
| Chuỗi tiếng Việt mojibake trong lý do audit/thông báo UC-02 | `"YÃªu cáº§u..."`, test `ValidationUsesTheSpecifiedMessages` fail | `AccountRecoveryService.cs` (viết lại toàn bộ chuỗi đúng UTF-8) |
| Dev aid luôn 404 | Adapter scoped mất proof giữa các request | `DevelopmentContactDeliveryAdapter.cs` (lưu proof ở store tĩnh, chỉ Development) |
| Reset đồng thời trả 503/409 thay vì MSG108 | Npgsql execution strategy/EF bọc lỗi 40001 không được bắt; audit tái sử dụng change tracker hỏng | `AccountRecoveryService.cs` (một catch thống nhất cho mọi chuỗi concurrency + `ChangeTracker.Clear()` trước audit) |
| Test recovery dùng chung account demo, lệch thứ tự | 12/33 fail không ổn định | `RecoveryApiTests.cs` (mỗi test tự tạo account riêng + email đã xác minh; kỳ vọng message theo field; ép kiểu cột JSON sang text; chấp nhận 401 cho endpoint status ẩn) |
| Browser recovery spec không biên dịch | `=> 'POST'` ở `recovery.spec.ts:177` chặn toàn bộ file test | `recovery.spec.ts` (sửa thành `=== 'POST'`, tách account `dual.demo`, mock kèm CORS headers, test 8 hoàn tất luồng) |

## Triển khai (deploy) — trạng thái và việc cần chuẩn bị

Repo **chưa** có cấu hình production (không compose cho API, không pipeline, không `appsettings.Production.json`). Muốn chạy thật cần:

1. PostgreSQL production (container hoặc managed) + chuỗi kết nối qua biến môi trường/secret, không dùng `.env`.
2. Cấu hình API: `ASPNETCORE_ENVIRONMENT=Production`, `Jwt__SigningKey`, `ConnectionStrings__DefaultConnection`, `Cors:AllowedOrigins` (domain web thật), HTTPS/Kestrel hoặc reverse proxy.
3. Kênh khôi phục thật (S02): thay `DevelopmentContactDeliveryAdapter` bằng provider mail/SMS; endpoint `/api/dev/recovery/*` tự động biến mất ở Production.
4. Xác nhận các tham số `[PROPOSED]` trong `Recovery` section (thời gian sống mã, cooldown, số lần gửi/gõ lại, rate limit).
5. Chốt các quyết định `[PROPOSED]` trong [s03-account-recovery-evidence.md](../../docs/s03-account-recovery-evidence.md) §5 và bổ sung mô tả đăng ký kênh có xác minh trước khi bật UC-02 cho người dùng thật.

Không commit `.env`, private keys, ảnh chứa credential, model weights hay dump database.

## Xử lý sự cố thường gặp

- **Docker không kết nối**: mở Docker Desktop từ `%LOCALAPPDATA%\Programs\DockerDesktop\Docker Desktop.exe`, đợi engine sẵn sàng rồi thử lại.
- **Build báo "file is locked by DCMS.Api"**: API đang chạy giữ DLL. Dừng terminal API (Ctrl+C), build, chạy lại.
- **`/health` trả 503 `database: unavailable`**: xem container Postgres có `healthy` không; đợi thêm vài giây sau khi container vừa khởi động.
- **Script bị chặn execution policy**: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned` trong terminal đó.
- **Quên mật khẩu demo**: xem `DEV_SEED_PASSWORD` trong `.env`.
- **Repository bị khóa secret trong git**: kiểm tra `git status` sạch trước khi demo; không commit `.env`, `web/.env`, `.tools`, `test-results`.
