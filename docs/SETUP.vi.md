# Hướng dẫn cài đặt và chạy DCMS cho team

Hướng dẫn này dành cho môi trường phát triển trên **Windows, dùng PowerShell**. Chạy các lệnh tại thư mục gốc của repository, trừ khi bước đó yêu cầu vào `web` hoặc `backend`. Mỗi thành viên dùng database và mật khẩu riêng trên máy mình.

Hiện repository có ASP.NET Core Web API (.NET 10), PostgreSQL 17 qua Docker và web React/TypeScript/Vite/Ant Design. Đã có đăng nhập/đăng xuất Identity + JWT và quản lý tài khoản nhân viên với phân quyền, khóa/mở khóa và audit. ERD 37 bảng là tài liệu thiết kế; mới áp dụng phần tài khoản/nhân viên/phiên, chưa có Patient hoặc bệnh án. `mobile` có bộ khung Expo và màn hình placeholder; team mobile xem [hướng dẫn riêng](../mobile/README.md). `ai-service` vẫn là phần dự kiến, chưa cần cài Python, YOLO hay tải model. Xem thêm [hướng dẫn chạy thử UC-01/UC-31](implementation/RUN-ACCOUNTS.vi.md).

## 1. Cài các công cụ cần thiết

| Công cụ | Phiên bản/cấu hình dùng cho dự án | Nơi cài chính thức |
| --- | --- | --- |
| Git | Git for Windows | [Tải Git](https://git-scm.com/install/windows) |
| .NET SDK | **10.0.x**, chọn SDK, không chỉ Runtime; chọn kiến trúc phù hợp máy | [Tải .NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| Node.js và npm | **Node.js 22.x**, npm đi kèm bộ cài | [Tải Node.js](https://nodejs.org/en/download) — chọn nhánh 22.x |
| Docker Desktop | Dùng Linux containers; bật WSL 2 backend | [Hướng dẫn Docker trên Windows](https://docs.docker.com/desktop/setup/install/windows-install/) |
| Trình soạn thảo | VS Code hoặc IDE quen dùng | Tùy chọn; có thể build/chạy bằng terminal |

Với Docker Desktop, làm theo yêu cầu Windows/WSL trong tài liệu chính thức ở trên. Nếu chưa có WSL, mở PowerShell **Run as administrator**, chạy `wsl --install`, rồi khởi động lại máy nếu được yêu cầu. Mở Docker Desktop, hoàn tất thiết lập và đợi engine chạy. Các lệnh dự án bên dưới dùng PowerShell thông thường.

Không cần cài PostgreSQL riêng: Compose sẽ tải image `postgres:17-alpine`. `psql`, pgAdmin/DBeaver và Postman là tùy chọn. Không cần cài React, TypeScript, Ant Design hay EF Core toàn cục; các dependency đã khai báo trong repository.

Sau khi cài công cụ, đóng terminal cũ, mở PowerShell mới và kiểm tra:

```powershell
git --version
dotnet --list-sdks
node --version
npm.cmd --version
docker version
docker compose version
```

Kết quả cần có SDK `10.0.xxx`, Node `v22.x.x`, và `docker version` có cả phần Client lẫn Server. Hướng dẫn dùng `npm.cmd` để tránh lỗi PowerShell chặn `npm.ps1`.

### Nếu muốn cài .NET 10 riêng cho repository

Sau khi clone và vào thư mục gốc, có thể dùng cách này thay cho cài SDK toàn máy:

```powershell
New-Item -ItemType Directory -Force .tools | Out-Null
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .tools/dotnet-install.ps1
& .\.tools\dotnet-install.ps1 -Channel 10.0 -InstallDir .tools/dotnet -NoPath
& .\.tools\dotnet\dotnet.exe --version
```

`.tools` được Git bỏ qua. Script chạy API sẽ ưu tiên SDK trong `.tools/dotnet` nếu thư mục này tồn tại. Nếu PowerShell chặn script, xem mục lỗi thường gặp.

## 2. Lấy mã nguồn

Lấy URL repository từ team, thay `<URL_REPOSITORY>` trong lệnh sau bằng URL thật:

```powershell
git clone <URL_REPOSITORY> DCMS
Set-Location DCMS
```

Nếu đã có checkout, chỉ cần vào thư mục đó, ví dụ:

```powershell
Set-Location C:\capstone\DMCS
```

Kiểm tra thư mục hiện tại có `README.md`, `.env.example`, `backend`, `web` và `infrastructure`.

## 3. Tạo cấu hình riêng trên máy

Chỉ sao chép khi file đích chưa tồn tại để giữ cấu hình đã tạo:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
if (-not (Test-Path web/.env)) { Copy-Item web/.env.example web/.env }
notepad .env
```

File `.env` ở thư mục gốc có các biến:

```dotenv
POSTGRES_DB=dcms_dev
POSTGRES_USER=dcms
POSTGRES_PASSWORD=replace-with-a-private-local-password
POSTGRES_PORT=5432
```

**Thay `POSTGRES_PASSWORD` bằng mật khẩu riêng trước khi chạy Docker.** Để tương thích với script đọc `.env` hiện tại, dùng chuỗi dài chỉ gồm chữ, số, dấu `-` hoặc `_`; không thêm dấu nháy, khoảng trắng hoặc ký tự đặc biệt. Mỗi biến một dòng, dạng `KEY=value`.

File `web/.env` có:

```dotenv
VITE_API_BASE_URL=http://localhost:5080
```

API lấy cấu hình database từ `.env` gốc qua `start-api.ps1`. Web lấy URL API từ `web/.env`, không từ `.env` gốc. Khi sửa `web/.env`, khởi động lại Vite; với bản build, cần build lại. Biến `VITE_*` được đưa vào mã web nên chỉ đặt cấu hình công khai, không đặt mật khẩu.

Không commit `.env`, mật khẩu, hồ sơ bệnh nhân, database dump hoặc model weights. Chỉ chia sẻ `.env.example` với các giá trị mẫu.

Sinh cấu hình xác thực Development trước khi chạy API:

```powershell
& .\infrastructure\scripts\initialize-development.ps1
```

Script thêm `JWT_SIGNING_KEY` và `DEV_SEED_PASSWORD` ngẫu nhiên vào `.env`, giữ các giá trị đã có và không in secret. Không dùng giá trị cố định chung cho team.

## 4. Khởi động PostgreSQL

Từ thư mục gốc, khi Docker Desktop đang chạy:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
docker compose --env-file .env -f infrastructure/docker/compose.yaml ps
```

Lần đầu cần Internet để tải image. Đợi dịch vụ `postgres` chuyển sang `healthy`; có thể chạy lại lệnh `ps` sau vài giây.

Thông tin kết nối cho công cụ quản lý database nếu cần:

| Thuộc tính | Giá trị |
| --- | --- |
| Host | `localhost` |
| Port | `POSTGRES_PORT` trong `.env`, mặc định `5432` |
| Database | `POSTGRES_DB` trong `.env` |
| Username | `POSTGRES_USER` trong `.env` |
| Password | Mật khẩu riêng đã đặt trong `.env` |

Database được lưu trong Docker named volume, vẫn còn sau khi dừng container. Các biến user/password/database chỉ khởi tạo database khi volume còn mới; sửa `.env` sau lần chạy đầu không tự đổi mật khẩu trong database đã có.

## 5. Restore và build backend

Tại thư mục gốc:

```powershell
$dotnet = if (Test-Path .\.tools\dotnet\dotnet.exe) {
    '.\.tools\dotnet\dotnet.exe'
} else {
    'dotnet'
}
& $dotnet --version
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore
```

Chỉ tiếp tục nếu restore thành công và build báo `Build succeeded`. Nếu phiên bản SDK được chọn không phải 10.x, dùng SDK local theo mục 1. Migration hiện có tạo Identity, nhân viên, phiên và audit; không cần tạo lại migration.

## 6. Chạy API — terminal A

Mở PowerShell tại thư mục gốc:

```powershell
& .\infrastructure\scripts\start-api.ps1 -Migrate -SeedDemo
& .\infrastructure\scripts\start-api.ps1
```

Lệnh đầu áp dụng migration còn thiếu và thêm dữ liệu demo chưa có, rồi kết thúc. Lệnh thứ hai chạy HTTP server; giữ terminal này mở. Script đọc `.env`, đặt connection string và JWT config trong process, chạy profile `http` ở Development. API ở `http://localhost:5080`. Seed không thay đổi account đã có và không xóa dữ liệu.

Nếu chạy `dotnet run` trực tiếp mà chưa đặt connection string, API sẽ báo thiếu cấu hình. Dùng script trên cho quy trình chạy local thông thường.

## 7. Cài và chạy web — terminal B

Mở PowerShell mới tại thư mục gốc:

```powershell
Set-Location web
npm.cmd install
npm.cmd run build
npm.cmd run dev
```

Chỉ chạy bước tiếp theo khi bước trước thành công. Giữ terminal này mở và vào **http://localhost:5173/sign-in**. Dùng `admin.demo` với mật khẩu `DEV_SEED_PASSWORD` trong `.env` riêng để thử quản lý tài khoản. Lần đầu cần Internet để tải package. `npm.cmd run build` kiểm tra TypeScript và tạo output trong `web/dist`; `npm.cmd run dev` chạy Vite cho phát triển.

Vite cấu hình `strictPort: true`; nếu cổng 5173 bận, web sẽ báo lỗi thay vì tự chuyển cổng.

## 8. Kiểm tra đã chạy thành công — terminal C

Trong PowerShell khác:

```powershell
Invoke-RestMethod http://localhost:5080/health | ConvertTo-Json
```

Kết quả mong đợi, HTTP 200:

```json
{
  "api": "healthy",
  "database": "healthy"
}
```

Trang **http://localhost:5173/health** hiển thị trạng thái API và database. Nếu database không kết nối được, endpoint trả HTTP 503 với `database: "unavailable"`; `Invoke-RestMethod` sẽ báo lỗi HTTP.

OpenAPI JSON ở **http://localhost:5080/openapi/v1.json** khi chạy Development. Repository chưa cấu hình trang Swagger UI tại `/swagger`.

Checklist để xác nhận máy mới đã sẵn sàng:

- [ ] Có SDK .NET 10, Node.js 22/npm, Git và Docker engine hoạt động.
- [ ] Đã tạo `.env`, đổi mật khẩu riêng và tạo `web/.env`.
- [ ] PostgreSQL báo healthy trong Compose.
- [ ] Backend restore/build thành công.
- [ ] Web install/build thành công.
- [ ] API `/health` trả HTTP 200, API và database đều healthy.
- [ ] Đăng nhập được ở cổng 5173; trang `/health` hiển thị kết nối thành công.

## 9. Chạy lại hằng ngày và dừng dịch vụ

Không cần sao chép lại `.env` hoặc cài lại công cụ mỗi ngày. Tại thư mục gốc, khởi động database rồi chạy API:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
& .\infrastructure\scripts\start-api.ps1
```

Ở terminal khác, vào `web` và chạy `npm.cmd run dev`. Sau khi lấy code mới, chạy lại restore/build backend; nếu dependency web thay đổi, chạy lại `npm.cmd install` và `npm.cmd run build`.

Để dừng API và Vite, nhấn `Ctrl+C` trong từng terminal. Dừng database từ thư mục gốc:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml down
```

Lệnh này giữ volume dữ liệu. **Không thêm `-v` nếu cần giữ database**: `down -v` xóa volume và dữ liệu local.

## 10. Lỗi thường gặp

| Lỗi/triệu chứng | Cách xử lý |
| --- | --- |
| `git`, `node`, `dotnet` hoặc `docker` không được nhận diện | Kiểm tra đã cài công cụ và mở terminal mới để nhận PATH. Với .NET local, gọi `.tools/dotnet/dotnet.exe`. |
| `NETSDK1045` hoặc SDK không hỗ trợ `net10.0` | Cài .NET **10 SDK**, kiểm tra `dotnet --list-sdks`, hoặc dùng SDK local. |
| PowerShell chặn `npm.ps1` | Dùng `npm.cmd` như các lệnh trong hướng dẫn. |
| PowerShell chặn `start-api.ps1`/`dotnet-install.ps1` | Nếu chính sách máy cho phép, chạy `Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned` rồi chạy lại script trong cùng terminal. Chỉ có hiệu lực cho terminal này; nếu bị Group Policy chặn, liên hệ người quản lý máy. |
| Docker không kết nối được daemon/engine | Mở Docker Desktop, đợi engine chạy, kiểm tra Linux containers và WSL 2 theo tài liệu Docker. Chạy `docker version` để xác nhận Server hoạt động. |
| PostgreSQL không healthy hoặc API báo database unavailable | Kiểm tra Compose `ps`, xem log bằng lệnh bên dưới; kiểm tra `.env`, cổng và mật khẩu. |
| Cổng 5432 đang được dùng | Đổi `POSTGRES_PORT` trong `.env`, ví dụ `5433`; chạy lại Compose `up -d` và khởi động lại API. Script dùng cùng giá trị port. |
| API không bind được cổng 5080 | Dừng phiên API cũ hoặc dịch vụ đang chiếm cổng sau khi xác định đúng process. |
| Vite báo cổng 5173 đang được dùng | Dừng phiên Vite cũ hoặc dịch vụ chiếm cổng rồi chạy lại web. Nếu chủ động đổi cổng web, cập nhật `Cors:AllowedOrigins` trong `backend/src/DCMS.Api/appsettings.Development.json` và khởi động lại API. |
| Web báo `Failed to fetch`/CORS | Kiểm tra API đang chạy, `web/.env` trỏ đúng cổng 5080 và truy cập web bằng `localhost:5173` hoặc `127.0.0.1:5173`, là hai origin hiện được cho phép. |
| Đổi mật khẩu `.env` nhưng vẫn bị lỗi xác thực PostgreSQL | Volume cũ còn dùng mật khẩu lúc khởi tạo. Dùng lại cấu hình khớp database, hoặc đổi mật khẩu bằng công cụ quản trị với tài khoản hiện có. Không xóa volume nếu còn dữ liệu cần giữ. |
| Restore/install không tải được package | Kiểm tra Internet, proxy/VPN của máy và quyền truy cập NuGet/npm registry, sau đó chạy lại. |

Lệnh xem log database từ thư mục gốc:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml logs --tail 100 postgres
```

Lệnh xác định process đang dùng các cổng dự án:

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5432,5080,5173 -ErrorAction SilentlyContinue |
    Select-Object LocalAddress,LocalPort,OwningProcess
```

Khi nhờ team hỗ trợ, gửi bước đang thực hiện, phiên bản công cụ và thông báo lỗi đã bỏ thông tin nhạy cảm. Không gửi nội dung `.env`, connection string có mật khẩu hoặc dữ liệu bệnh nhân.

## 11. EF Core khi team bắt đầu làm database

Local tool `dotnet-ef` phiên bản 10.0.0 đã khai báo trong `backend/.config/dotnet-tools.json`. Lần cài hiện tại dùng `start-api.ps1 -Migrate` để áp dụng migration có sẵn. Chỉ dùng EF tool để tạo migration tiếp theo khi thay đổi schema.

Trước khi chạy EF, cần đặt `ConnectionStrings__DefaultConnection` trong **chính terminal chạy lệnh EF**. Script `start-api.ps1` đặt biến cho phiên chạy API, không tự truyền sang terminal khác. Xem lệnh migrations trong [README gốc](../README.md#ef-core-migrations) và thống nhất migration với team trước khi tạo mới.
