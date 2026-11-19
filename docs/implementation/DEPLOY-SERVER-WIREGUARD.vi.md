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
| `infrastructure/docker/compose.prod.yaml` | Stack production: postgres, api, web, clamav (tùy chọn), cùng các job `migrate`/`seed`; image lấy từ `ghcr.io` |
| `.github/workflows/deploy-server.yml` | Build image trên GitHub, đẩy lên `ghcr.io`, server kéo về và chạy |
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

Cài đặt ban đầu ở trên build tại chỗ. Từ 19/11/2026 các lần cập nhật đi qua pipeline (mục CI/CD bên dưới): image build sẵn trên GitHub, server chỉ `pull` rồi `up -d`.

## Tự động deploy khi merge vào `main` (CI/CD) — build trên GitHub, server chỉ kéo image (19/11/2026)

Server 3,8 GB RAM từng treo hai lần khi tự build image (17–18/11/2026). Từ 19/11/2026 server **không build nữa**:

```
push/merge vào main
  └─ job build  (runner của GitHub, ubuntu-latest)
       build image api + web → push lên GitHub Container Registry:
       ghcr.io/tu2005tktn/dcms-api:<commit>, :latest
       ghcr.io/tu2005tktn/dcms-web:<commit>, :latest
  └─ job deploy (self-hosted runner dcms-server trên máy chủ, nhãn dcms-prod; chỉ chạy khi build thành công)
       docker login ghcr.io bằng GITHUB_TOKEN của chính lần chạy (chỉ quyền đọc package)
       → gỡ ClamAV nếu đang tắt → pull api, web → up -d --wait postgres → job migrate
       → up -d --remove-orphans → chờ /health → docker logout → dọn image cũ
```

- Workflow: `.github/workflows/deploy-server.yml`, chạy khi push vào `main` hoặc chạy tay (`workflow_dispatch`). Không chạy cho pull request.
- Image gắn tag bằng SHA commit đầy đủ (`DCMS_TAG`), nên bản đang chạy luôn khớp đúng commit. Quay lại bản cũ: chạy tay workflow trên commit cũ, hoặc trên server đặt `DCMS_TAG=<sha cũ>` rồi `docker compose ... pull api web && docker compose ... up -d`.
- `compose.prod.yaml`: `api`, `web`, `migrate`, `seed` dùng `${DCMS_REGISTRY:-ghcr.io/tu2005tktn}/dcms-api|dcms-web:${DCMS_TAG:-latest}`. Khối `build:` vẫn giữ để có thể build tay khi cần (`docker compose ... build`), nhưng pipeline không dùng.
- Đăng nhập registry: job build dùng `GITHUB_TOKEN` với quyền `packages: write`; job deploy dùng `GITHUB_TOKEN` với quyền `packages: read` rồi `docker logout` cuối job. Không cần lưu thêm secret hay mật khẩu nào trên server hoặc GitHub. Image chỉ chứa mã ứng dụng (đã công khai trong repo); mật khẩu, dữ liệu bệnh nhân và model AI nằm ở `/opt/dcms/.env` và volume trên server, không nằm trong image.
- Build dùng cache của GitHub Actions (`type=gha`), lần sau nhanh hơn.
- Self-hosted runner chạy dưới user `github-runner` (thuộc nhóm `docker`) dưới dạng systemd service, tự kết nối ra GitHub nên server không mở cổng. Compose chạy trong thư mục checkout của runner với project name `dcms` và `--env-file /opt/dcms/.env`. `.env` thuộc nhóm `github-runner`, quyền 640.
- Chỉ một lần deploy chạy tại một thời điểm (`concurrency: deploy-server`).
- Build lỗi thì job deploy không chạy và container cũ vẫn chạy. Pull hoặc migration lỗi cũng dừng trước khi khởi động lại API.
- Nếu runner offline quá lâu, GitHub có thể xóa đăng ký runner (log: "The runner registration has been deleted from the server"). Khi đó tạo token mới ở *Settings → Actions → Runners → New self-hosted runner* và chạy lại `config.sh --replace` trên server.
- Repo đang **public**: chỉ workflow này nhắm nhãn `dcms-prod`; không thêm workflow `pull_request` dùng runner này. Nên bật *Settings → Actions → General → Require approval for all external contributors*.

## Bộ nhớ trên server

- Server có swap 4 GB (`/swapfile`, khai báo trong `/etc/fstab`, `vm.swappiness=10` trong `/etc/sysctl.d/99-swap.conf`), tạo ngày 19/11/2026.
- `backend/Dockerfile` và `web/Dockerfile` vẫn giữ giới hạn bộ nhớ khi build (MSBuild một tiến trình, GC heap 1 GB; Node heap 1 GB) để build tay trên máy nhỏ vẫn an toàn.

## Quét virus tệp tải lên (ClamAV) — mặc định tắt (18/11/2026)

ClamAV nạp cơ sở dữ liệu mẫu virus vào RAM (1,2–1,5 GB, lên ~2,5 GB khi cập nhật), là thành phần tốn RAM nhất trên server 3,8 GB và góp phần làm máy treo. Production mặc định chạy **không có ClamAV**:

- `CLINICAL_ANTIVIRUS=Disabled` (mặc định trong compose) đặt `ClinicalDocuments__Antivirus=Disabled` cho API: tệp hợp lệ chuyển Ready ngay, audit ghi `ScanSkipped`. Kiểm tra MIME/extension/chữ ký nội dung/kích thước vẫn áp dụng. Xem [S19/S20](../specs/S19-S20-DCMS-112.md#chế-độ-quét-virus-tùy-chọn-18112026).
- Service `clamav` chỉ chạy khi bật profile `antivirus`.
- Bật lại khi server đủ RAM (khuyến nghị 8 GB): trong `/opt/dcms/.env` đặt `CLINICAL_ANTIVIRUS=ClamAv` và thêm `antivirus` vào `COMPOSE_PROFILES` (ví dụ `public,antivirus`), rồi chạy lại deploy.
- Pipeline deploy tự dừng và xóa container `clamav` (trước bước build, để giải phóng RAM) khi `.env` không đặt `CLINICAL_ANTIVIRUS=ClamAv`. Tắt bằng tay: `docker compose ... --profile antivirus rm -sf clamav` (phải nêu profile, nếu không compose bỏ qua service `clamav`). Volume `dcms_clamav_data` có thể xóa để lấy lại đĩa.

## Public ra Internet (Cloudflare Tunnel)

- Domain `nhakhoangockhang.id.vn` dùng nameserver Cloudflare. Container `cloudflared` (profile `public`, bật bằng `COMPOSE_PROFILES=public` trong `.env`) giữ kết nối ra Cloudflare bằng `CLOUDFLARE_TUNNEL_TOKEN` trong `.env`; server không mở cổng ra Internet.
- Trong Cloudflare Zero Trust, tunnel `dcms` có Public Hostname `nhakhoangockhang.id.vn` (và `www`) trỏ tới `http://web:80`. Nginx phục vụ web và chuyển `/api`, `/hubs`, `/health` tới API như trong VPN.
- `PUBLIC_WEB_URL=https://nhakhoangockhang.id.vn` (dùng cho URL quay lại PayOS). Truy cập qua VPN bằng `http://192.168.10.251` vẫn hoạt động.

## Giới hạn hiện tại

- Trong VPN chỉ có HTTP; HTTPS công khai do Cloudflare đảm nhận.
- Server đang chứa dữ liệu demo (`seed`). Trước khi dùng dữ liệu bệnh nhân thật cần đổi mật khẩu các tài khoản demo hoặc dựng database mới không seed.
- Khi chưa cấu hình SMTP, Production không có adapter gửi email: xác nhận email/khôi phục mật khẩu sẽ báo lỗi gửi.
- ClamAV (khi bật) cần khoảng 1–1,5 GB RAM và tải chữ ký virus khi khởi động lần đầu; trong lúc đó tệp tải lên ở trạng thái PendingScan.
