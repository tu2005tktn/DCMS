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

Recovery chưa triển khai: không có endpoint reset. Kênh xác minh, proof expiry và retry policy chưa được xác nhận trong đặc tả; không dùng thao tác quản lý account để bỏ qua xác minh hoặc phê duyệt liên kết bệnh án.

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

Browser test loading/error chủ động trì hoãn và giả lập một response 503 rồi bỏ giả lập để retry API thật; các luồng chức năng dùng API/database thật. Backend tests có Patient/link sentinel để kiểm chứng bảo toàn dữ liệu ở boundary, không kiểm thử UC Patient chưa triển khai. Chưa kiểm thử production deployment hoặc kênh khôi phục chưa có.
