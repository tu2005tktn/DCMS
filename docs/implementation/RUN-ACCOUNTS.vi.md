# Chạy thử UC-01 và UC-31

Yêu cầu Git, .NET 10 SDK, Node.js 22/npm và Docker Desktop Linux engine. Xem [hướng dẫn cài công cụ](../SETUP.vi.md). Các lệnh dưới đây chạy bằng PowerShell từ thư mục gốc repository.

## Cấu hình, migration và demo

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
if (-not (Test-Path web/.env)) { Copy-Item web/.env.example web/.env }
# Sửa POSTGRES_PASSWORD riêng trước lần đầu khởi tạo database.
notepad .env
& .\infrastructure\scripts\initialize-development.ps1
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo
```

Đợi PostgreSQL healthy trước lệnh cuối. `initialize-development.ps1` chỉ thêm khóa JWT và mật khẩu demo còn thiếu, giữ giá trị đã có. `-Migrate -SeedDemo` áp dụng migration còn thiếu rồi thêm demo account chưa có và kết thúc; không khởi động HTTP server. Seed chỉ được chạy ở Development, không ghi đè mật khẩu/roles/states của account tồn tại. Không reset hoặc xóa volume.

Các account demo: `admin.demo`, `dentist.demo`, `owner.demo`, `assistant.demo`, `dual.demo` (Dentist + Assistant), `guest.demo` (Public/Guest). Mật khẩu của account mới seed nằm ở **`DEV_SEED_PASSWORD` trong `.env` máy bạn**; mở file bằng editor để xem, không đưa mật khẩu vào source hoặc gửi file cho team. `guest.demo` là dữ liệu giả Development, không đại diện việc đã xác minh contact ngoài thực tế và không có Patient hoặc Verified link. Những account mới qua UI có mật khẩu ban đầu do Admin nhập.

## Mở ứng dụng

Terminal A tại thư mục gốc:

```powershell
& .\infrastructure\scripts\start-api.ps1
```

Terminal B:

```powershell
Set-Location web
npm.cmd install
npm.cmd run build
npm.cmd run dev
```

Mở **http://localhost:5173/sign-in**. API ở **http://localhost:5080**, trang health được giữ ở **http://localhost:5173/health**. Các API mới dưới `/api/auth` và `/api/accounts`; OpenAPI Development ở `/openapi/v1.json`.

## Thử từng chức năng

1. Đăng nhập `admin.demo` bằng mật khẩu riêng trong `.env`. Trang tài khoản của tôi hiển thị vai trò được cấp, vai trò hiệu lực và nút cập nhật quyền. Sai mật khẩu phải hiện đúng MSG09.
2. Mở **Quản lý tài khoản**, tìm theo username/họ tên/email; chọn page size và chuyển trang. Tìm chuỗi không có kết quả để kiểm tra empty state; nút Tải lại gọi API thật.
3. Chọn **Tạo nhân viên**. Nhập username duy nhất, mã nhân viên, fullname, mật khẩu theo policy được hiển thị và một/nhiều vai trò. Lưu thành công nhận MSG14. Dùng lại username hoặc mã nhân viên phải nhận MSG139.
4. Chọn **Sửa** ở account Staff, đổi fullname/contact hoặc tập vai trò, nhập lý do và xác nhận. Public account chỉ có khóa/mở khóa/audit. Không có form tạo Patient hoặc phê duyệt liên kết hồ sơ.
5. Chọn **Khóa**, nhập lý do và xác nhận. MSG15 thông báo thu hồi các phiên hiện tại. Chọn **Mở khóa** với lý do, nhận MSG16; đăng nhập mới được phép, token cũ vẫn không dùng được.
6. Chọn **Audit** để xem actor/time/target, lý do và JSON trước/sau. Timestamp hiển thị theo múi giờ Việt Nam, database lưu UTC. Không có mật khẩu, hash hoặc token trong audit.
7. Chọn **Đăng xuất** để server vô hiệu hóa phiên rồi quay về đăng nhập. Reload trang cũng cần đăng nhập lại vì web giữ JWT trong bộ nhớ, chưa có persistent login/refresh token.
8. Thử `owner.demo`: thấy Clinic Owner và Dentist ở vai trò hiệu lực, không có menu quản lý tài khoản. `dual.demo` có hai vai trò độc lập; `guest.demo` là Public/Guest, không có Patient hoặc quyền bệnh án.

Để thử phiên cũ: mở hai cửa sổ trình duyệt riêng (ví dụ thường và ẩn danh). Admin tạo một account thử có Admin + Dentist; đăng nhập account thử ở cửa sổ thứ hai. Từ Admin ở cửa sổ thứ nhất, thu hồi Admin; cửa sổ thứ hai gọi tải lại danh sách sẽ nhận MSG06. Khóa account đó thì request riêng tư tiếp theo nhận MSG07. Đừng khóa/thu hồi account Admin duy nhất nếu không còn quản trị viên khác để khôi phục quyền.

Để thử conflict: hai cửa sổ Admin cùng mở form Sửa một account. Lưu ở cửa sổ A rồi lưu form vẫn đang mở ở B. B phải nhận MSG111; hủy form, tải lại dữ liệu và kiểm tra thay đổi trước khi sửa tiếp.

## Thử khôi phục tài khoản (UC-02 / S03)

Luồng có 4 bước và chạy giống nhau trên web lẫn mobile. Mở **http://localhost:5173/recover** hoặc bấm **Khôi phục tài khoản** ở trang đăng nhập.

1. Nhập tên đăng nhập (ví dụ `dentist.demo`) rồi **Gửi yêu cầu**. Phản hồi luôn là MSG12 với cùng một nội dung bất kể tài khoản có tồn tại hay không.
2. Nhập mã xác minh rồi **Xác minh**. Mã sai/hết hạn/đã dùng đều nhận MSG108 và không đổi mật khẩu.
3. Nhập mật khẩu mới theo gợi ý policy (gợi ý đọc từ `/api/auth/policy`) rồi **Đặt lại mật khẩu**. Mật khẩu không đạt policy nhận MSG109.
4. Thành công nhận MSG13, các phiên đăng nhập cũ bị thu hồi, rồi chuyển sang trang đăng nhập.

Nút **Gửi lại mã** có đếm ngược. API `/resend` trả lời trung lập nên giao diện tự ước lượng thời gian chờ; gửi lại quá sớm vẫn bị từ chối phía server.

### Đọc mã xác minh khi chưa có kênh gửi thật

Chưa cấu hình provider gửi mail/SMS thật, nên ở **Development** adapter giữ mã đã phát trong bộ nhớ và cho đọc lại qua endpoint bắt buộc đăng nhập:

```powershell
# đăng nhập lấy token, rồi thay {accountId}
Invoke-RestMethod http://localhost:5080/api/dev/recovery/proof/{accountId} `
  -Headers @{ Authorization = "Bearer $token" }
```

Mã xác minh **không** được ghi vào database, outbox, log hay audit. Endpoint này chỉ tồn tại ở Development và không được map ở Production.

> Account demo tạo **trước** thay đổi này không có email đã xác minh vì seeder bỏ qua account đã có. Hãy dùng account vừa seed hoặc cập nhật contact đã xác minh cho account trước.

### Cấu hình kênh và giới hạn khôi phục

Khóa nằm trong `backend/src/DCMS.Api/appsettings.Development.json`, mặc định `[PROPOSED]`:

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `TokenLifetimeMinutes` | 30 | Thời gian sống của mã xác minh / reset grant |
| `ResendCooldownMinutes` | 2 | Khoảng cách tối thiểu giữa hai lần gửi |
| `MaxResends` | 3 | Số lần gửi lại tối đa trong một phiên |
| `MaxVerifyAttempts` | 5 | Số lần nhập sai trước khi chặn |
| `RateLimitPerAccountPerHour` | 5 | Số request tối đa mỗi account mỗi giờ |
| `RateLimitPerIpPerHour` | 20 | Số request tối đa mỗi IP mỗi giờ |
| `Channel` | `Email` | Kênh gửi: `Email`, `Phone` hoặc `Link` |
| `CodeLength` | 6 | Độ dài mã số |
| `VerificationBaseUrl` | rỗng | Base URL cho kênh `Link`; link chỉ mang id mờ, không mang mã |

Giá trị không hợp lệ làm API dừng khởi động (validate on start) thay vì chạy với cấu hình sai.

### Mobile

```powershell
Set-Location mobile
npm.cmd install
npm.cmd run typecheck
npx.cmd expo start
```

Cùng 4 bước, cùng hành vi. Deep link `/(auth)/reset-password` là điểm vào cho kênh `Link`, chỉ mang id mờ.

## Lặp lại kiểm tra tự động

PostgreSQL phải đang chạy. Backend tests tạo schema riêng, không sửa dữ liệu development:

```powershell
& .\infrastructure\scripts\test-accounts.ps1
```

Browser tests cần API, Vite đang chạy và demo đã seed. Cài Chromium một lần rồi chạy:

```powershell
Push-Location web
npx.cmd playwright install chromium
Pop-Location
& .\infrastructure\scripts\test-accounts.ps1 -Browser
```

Script truyền mật khẩu demo qua biến môi trường của process test rồi xóa biến đó. Test tắt trace/video/screenshot để tránh tạo artifact chứa credential. Browser tests tạo account `e2e.*` giả, giữ lại trong development cùng audit; không xóa account có sẵn.

Build backend và kiểm tra health:

```powershell
. .\infrastructure\scripts\import-development-env.ps1
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore
Invoke-RestMethod http://localhost:5080/health
```

Nếu build bị khóa DLL trên Windows, dừng terminal API bằng Ctrl+C, build rồi chạy lại API. Nếu script bị chặn, dùng `Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned` trong terminal đó nếu chính sách máy cho phép.

## EF migrations sau này

Chưa cần tạo lại migration hiện có. Khi thêm schema mới, dùng cùng cấu hình đã import:

```powershell
. .\infrastructure\scripts\import-development-env.ps1
Push-Location backend
try {
    & $dotnet tool restore
    & $dotnet ef migrations add <MigrationName> --project src/DCMS.Infrastructure --startup-project src/DCMS.Api --output-dir Persistence/Migrations
} finally { Pop-Location }
```

Trong ví dụ này, `$dotnet` từ helper là đường dẫn tuyệt đối cho SDK local, hoặc lệnh global. Review migration trước khi áp dụng bằng `start-api.ps1 -Migrate`. Không rollback migration bằng lệnh xóa bảng nếu đang giữ dữ liệu.

## Kết quả thực tế đợt triển khai 11–12/10/2026

| Kiểm tra đã chạy | Kết quả |
| --- | --- |
| .NET 10 restore và build solution | Thành công, 0 warning, 0 error |
| Migration `AccountsAndSessions` trên development | Đã áp dụng; trước migration schema public không có bảng; migration Up chỉ tạo bảng/index/FK mới |
| Chạy lại `-Migrate -SeedDemo` | Thành công; không tạo lại hoặc đổi account có sẵn; test seed lặp lại kiểm tra hash/version/state/audit giữ nguyên |
| EF `migrations has-pending-model-changes` | Không có thay đổi model chưa được migration |
| PostgreSQL Compose / API `/health` | Container healthy; HTTP 200, API/database đều healthy |
| Backend integration tests | **15 passed, 0 failed, 0 skipped**, PostgreSQL thật trong schema riêng |
| Chromium browser tests | **4 passed**: route/login/logout; CRUD/role/lock/unlock/search/empty/audit; Owner thiếu quyền Admin; loading/error/retry |
| `npm install`, TypeScript + Vite build | Thành công; còn cảnh báo bundle lớn hơn 500 KB (khoảng 1.14 MB trước gzip), chưa tối ưu code splitting |
| `npm audit --audit-level=high` | 0 vulnerabilities sau khi cập nhật Vite cùng nhánh 6 lên 6.4.4 |
| Kiểm tra secret và script | Không có secret local trong file text Git-visible; `.env`, `.tools`, test-results bị ignore; PowerShell scripts parse thành công |
| Kiểm tra giao diện bằng ảnh | Đã xem trang đăng nhập và quản lý tài khoản desktop/mobile; bảng tài khoản cuộn ngang trên màn hình nhỏ |

### Bổ sung đợt 12–13/10/2026 (UC-02 / S03)

| Kiểm tra đã chạy | Kết quả |
| --- | --- |
| `.NET 10` restore và build solution | Thành công, 0 warning, 0 error |
| Migration `AccountRecovery` | Đã sinh file; chỉ tạo thêm `RECOVERY_TOKEN`, `RECOVERY_AUDIT`, `RECOVERY_OUTBOX` và index, không drop/xóa gì. **Chưa áp dụng lên database nào tại máy thực hiện** |
| Backend unit tests (`DCMS.UnitTests`) | **30 passed**, 0 failed, không cần database |
| Backend integration tests (`RecoveryApiTests`) | Biên dịch được, **chưa chạy** — máy thực hiện không có Docker/PostgreSQL |
| Web `npm run build` (tsc + Vite) | Thành công |
| Mobile `npm run typecheck` | Thành công |
| Web browser tests khôi phục | Đã viết `web/tests/recovery.spec.ts`, **chưa chạy** — cần API, Vite, PostgreSQL và `E2E_DEMO_PASSWORD` |
| Chạy thử Expo trên thiết bị | **Chưa chạy** — cần thiết bị/máy ảo |

Tiêu chí nghiệm thu 1–7 hiện được **chứng minh bằng test đã viết, chưa bằng lần chạy xanh**. Xem chi tiết map test trong [evidence S03](../s03-account-recovery-evidence.md). Các quyết định `[PROPOSED]` cần chốt cũng liệt kê ở đó.

Đã rà soát bảo mật và vá: `/recovery/status` và `/recovery/resend` ban đầu để lộ tài khoản có tồn tại; audit của luồng Admin ban đầu ghi thiếu actor. Chi tiết trong commit history nhánh `tuan`.

Browser test loading/error chủ động trì hoãn và giả lập một response 503 rồi bỏ giả lập để retry API thật; các luồng chức năng dùng API/database thật. Backend tests có Patient/link sentinel để kiểm chứng bảo toàn dữ liệu ở boundary, không kiểm thử UC Patient chưa triển khai. Chưa kiểm thử production deployment hoặc kênh khôi phục chưa có.
