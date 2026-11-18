# Deploy DCMS lên server nội bộ qua WireGuard

Tài liệu mô tả cách triển khai DCMS (API + web + PostgreSQL) lên một máy chủ Debian/Ubuntu chỉ truy cập được qua WireGuard. Không có thay đổi schema hay hành vi nghiệp vụ; chỉ thêm đóng gói container.

## Mô hình triển khai

```
Trình duyệt (máy trong VPN)
        │ http://<IP server>
        ▼
  web (nginx, cổng 80)  ── phục vụ SPA đã build từ `web/`
        │  /api/*, /hubs/* (WebSocket SignalR), /health
        ▼
  api (ASP.NET Core .NET 10, cổng 8080 nội bộ, ASPNETCORE_ENVIRONMENT=Production)
        │
        ├── postgres (postgres:17-alpine, chỉ trong mạng Docker)
        └── clamav (tùy chọn, profile `antivirus`; mặc định tắt)
```

- Web và API dùng **cùng origin**: SPA được build với `VITE_API_BASE_URL=""`, nên mọi request đi tới `/api`, `/hubs`, `/health` trên chính host và được nginx chuyển tới API. Không cần CORS cho web.
- `Cors__AllowedOrigins__0` vẫn được đặt bằng URL công khai của web, vì PayOS dùng giá trị này làm URL quay lại.
- Chỉ cổng 80 (web) mở ra ngoài. PostgreSQL, API và ClamAV (nếu bật) không publish cổng.
- Dữ liệu bền vững nằm trong Docker volume: `dcms_postgres_data`, `dcms_clinical_documents`, `dcms_branch_assets`, `dcms_clamav_data`.

## Tệp liên quan

| Tệp | Vai trò |
|---|---|
| `backend/Dockerfile` | Build và publish `DCMS.Api` (SDK 10.0 → runtime aspnet 10.0) |
| `web/Dockerfile` | Build SPA bằng Node rồi phục vụ bằng nginx |
| `web/nginx.conf` | SPA fallback + reverse proxy `/api`, `/hubs`, `/health` |
| `infrastructure/docker/compose.prod.yaml` | Stack production: postgres, api, web, clamav (tùy chọn), cùng các job `migrate`/`seed` |
| `infrastructure/docker/.env.production.example` | Mẫu biến môi trường production |

## Biến môi trường (`.env` trên server, không commit)

Bắt buộc: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY` (≥ 32 byte), `PUBLIC_WEB_URL`.
Tùy chọn: `DEV_SEED_PASSWORD` (chỉ khi seed dữ liệu demo), `GOOGLE_WEB_CLIENT_ID`, nhóm `SMTP_*`, nhóm `PAYOS_*`, `WEB_PORT` (mặc định 80).

Web chỉ cần `VITE_API_BASE_URL` lúc build (để rỗng = cùng origin); Google Client ID được web lấy từ API qua `Google:WebClientId`.

## Migration và dữ liệu demo

`Program.cs` chỉ cho phép `--migrate`, `--seed-demo`, `--seed-clinic-catalog` khi môi trường là Development. Vì vậy API chạy lâu dài ở **Production**, còn migration/seed chạy như job một lần (profile `tools`) với `ASPNETCORE_ENVIRONMENT=Development`, dùng cùng image và cùng database rồi thoát ngay:

```bash
docker compose --env-file .env -f infrastructure/docker/compose.prod.yaml --profile tools run --rm migrate
docker compose --env-file .env -f infrastructure/docker/compose.prod.yaml --profile tools run --rm seed
```

`seed` chạy `--seed-demo` rồi `--seed-clinic-catalog`; chỉ dùng cho server demo, cần `DEV_SEED_PASSWORD`.

## Quy trình deploy

1. Server: cài Docker Engine + Compose plugin.
2. Máy dev: đóng gói các tệp được Git theo dõi (`git ls-files`) thành tar, chép sang server vào `/opt/dcms` (giữ nguyên `.env` có sẵn trên server).
3. Server: tạo `/opt/dcms/.env` từ `infrastructure/docker/.env.production.example` (lần đầu), sinh mật khẩu DB và `JWT_SIGNING_KEY` ngẫu nhiên, `chmod 600`.
4. `docker compose ... up -d --build postgres`, chạy job `migrate` (và `seed` nếu cần), rồi `up -d --build api web`.
5. Kiểm tra: `curl http://<IP server>/health` trả về trạng thái healthy; mở `http://<IP server>` từ máy trong VPN.

Cập nhật phiên bản mới: lặp lại bước 2, chạy lại `migrate`, rồi `up -d --build api web`.

## Tự động deploy khi merge vào `main` (CI/CD)

- Trên server có GitHub Actions **self-hosted runner** tên `dcms-server`, nhãn `dcms-prod`, chạy dưới user `github-runner` (thuộc nhóm `docker`) dưới dạng systemd service. Runner tự kết nối ra GitHub nên server không cần mở cổng nào.
- Workflow `.github/workflows/deploy-server.yml` chạy khi có push vào `main` (gồm merge PR) hoặc chạy tay (`workflow_dispatch`). Job chỉ nhắm `runs-on: [self-hosted, dcms-prod]`, không chạy cho pull request.
- Các bước: checkout → build image `api` → build image `web` (lần lượt, không song song) → dọn build cache → `up -d --wait postgres` → job `migrate` → `up -d --remove-orphans` (mọi service mặc định cùng các profile trong `COMPOSE_PROFILES`) → chờ `/health` trả `healthy` (tối đa ~2 phút) → dọn image cũ.
- Compose chạy ngay trong thư mục checkout của runner, với project name cố định `dcms` và `--env-file /opt/dcms/.env`, nên dùng lại đúng container và volume đang có. `.env` thuộc nhóm `github-runner`, quyền 640.
- Chỉ một lần deploy chạy tại một thời điểm (`concurrency: deploy-server`); lần sau chờ lần trước xong.
- Build hỏng thì container cũ vẫn chạy vì `up` chỉ thực hiện sau khi build thành công. Migration lỗi cũng dừng trước khi khởi động lại API.
- Repo đang **public**: chỉ workflow này được nhắm vào nhãn `dcms-prod`; không thêm workflow `pull_request` dùng runner này. Nên bật *Settings → Actions → General → Require approval for all external contributors*.

## Build tiết kiệm bộ nhớ (18/11/2026)

Server có 3,8 GB RAM; ClamAV, PostgreSQL và API dùng khoảng 1,6–2,5 GB khi chạy. Ngày 17/11/2026 lần build song song API và web làm máy hết RAM và treo (SSH và web không phản hồi). Từ nay build chậm hơn nhưng ít tốn RAM:

- Workflow build từng image một (`api` rồi `web`) thay vì `build api web` song song, và đặt `COMPOSE_PARALLEL_LIMIT=1`.
- `backend/Dockerfile`: `dotnet restore`/`publish` chạy với `-maxcpucount:1` (một tiến trình MSBuild), `DOTNET_GCHeapHardLimit` 1 GB và tắt build server/node reuse.
- `web/Dockerfile`: `NODE_OPTIONS=--max-old-space-size=1024` cho `npm ci` và `vite build`.
- Sau khi build xong, dọn build cache cũ hơn 7 ngày (`docker builder prune`) để không đầy đĩa.
- Trên server nên có swap 4 GB (`/swapfile`, `vm.swappiness=10`) làm vùng đệm; tạo một lần bằng tay khi có quyền SSH.

Container đang chạy không bị dừng trong lúc build; chỉ bước `up` cuối cùng mới khởi động lại `api`/`web`.

## Quét virus tệp tải lên (ClamAV) — mặc định tắt (18/11/2026)

ClamAV nạp cơ sở dữ liệu mẫu virus vào RAM (1,2–1,5 GB, lên ~2,5 GB khi cập nhật), là thành phần tốn RAM nhất trên server 3,8 GB và góp phần làm máy treo. Production mặc định chạy **không có ClamAV**:

- `CLINICAL_ANTIVIRUS=Disabled` (mặc định trong compose) đặt `ClinicalDocuments__Antivirus=Disabled` cho API: tệp hợp lệ chuyển Ready ngay, audit ghi `ScanSkipped`. Kiểm tra MIME/extension/chữ ký nội dung/kích thước vẫn áp dụng. Xem [S19/S20](../specs/S19-S20-DCMS-112.md#chế-độ-quét-virus-tùy-chọn-18112026).
- Service `clamav` chỉ chạy khi bật profile `antivirus`.
- Bật lại khi server đủ RAM (khuyến nghị 8 GB): trong `/opt/dcms/.env` đặt `CLINICAL_ANTIVIRUS=ClamAv` và thêm `antivirus` vào `COMPOSE_PROFILES` (ví dụ `public,antivirus`), rồi chạy lại deploy.
- Pipeline deploy tự dừng và xóa container `clamav` (trước bước build, để giải phóng RAM) khi `.env` không đặt `CLINICAL_ANTIVIRUS=ClamAv`. Tắt bằng tay: `docker compose ... rm -sf clamav`. Volume `dcms_clamav_data` có thể xóa để lấy lại đĩa.

## Public ra Internet (Cloudflare Tunnel)

- Domain `nhakhoangockhang.id.vn` dùng nameserver Cloudflare. Container `cloudflared` (profile `public`, bật bằng `COMPOSE_PROFILES=public` trong `.env`) giữ kết nối ra Cloudflare bằng `CLOUDFLARE_TUNNEL_TOKEN` trong `.env`; server không mở cổng ra Internet.
- Trong Cloudflare Zero Trust, tunnel `dcms` có Public Hostname `nhakhoangockhang.id.vn` (và `www`) trỏ tới `http://web:80`. Nginx phục vụ web và chuyển `/api`, `/hubs`, `/health` tới API như trong VPN.
- `PUBLIC_WEB_URL=https://nhakhoangockhang.id.vn` (dùng cho URL quay lại PayOS). Truy cập qua VPN bằng `http://192.168.10.251` vẫn hoạt động.

## Giới hạn hiện tại

- Trong VPN chỉ có HTTP; HTTPS công khai do Cloudflare đảm nhận.
- Server đang chứa dữ liệu demo (`seed`). Trước khi dùng dữ liệu bệnh nhân thật cần đổi mật khẩu các tài khoản demo hoặc dựng database mới không seed.
- Khi chưa cấu hình SMTP, Production không có adapter gửi email: xác nhận email/khôi phục mật khẩu sẽ báo lỗi gửi.
- ClamAV (khi bật) cần khoảng 1–1,5 GB RAM và tải chữ ký virus khi khởi động lần đầu; trong lúc đó tệp tải lên ở trạng thái PendingScan.
