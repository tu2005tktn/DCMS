# DCMS-111 Tiếp nhận và bàn giao Visit

Chốt contract: 25/10/2026. Kiểm chứng hoàn tất: **26/10/2026**. Nhánh triển khai: `feat/dcms-111-reception`, nền `7da445f`. Contract: [S17/S18](../specs/S17-S18-RECEPTION.md), [DCMS-110](../specs/CLINIC-FLOW-DCMS-110.vi.md).

## Phạm vi đợt triển khai

Backend Visit/check-in/queue dùng PostgreSQL và mô hình chung; giao diện staff S17/S18; xếp lịch nhanh dùng S11 hiện có; trang đọc đúng Visit cho bàn giao DCMS-112. Không bổ sung chẩn đoán/điều trị/thu tiền hoặc tự xác minh app. Hai file Word v2.4 giữ nguyên bản nguồn.

## Kế hoạch kiểm chứng

Khôi phục/build backend với .NET 10 và build web sau npm install. Kiểm API qua TestServer với PostgreSQL schema riêng, gồm migration, idempotency/concurrency, dữ liệu đúng người, quyền, ngày +07, hồi cứu, nguồn điều trị và không tác dụng phụ. Chạy UI thật trên API/PostgreSQL trong môi trường riêng; kiểm same Visit ID và hàng chờ một dòng. Mock chỉ kiểm trạng thái lỗi giao diện nếu cần, không thay bằng chứng tích hợp.

Kiểm tra read-only database local cho thấy còn migration reception cũ sau lần hoàn tác code. Migration mới phải nâng cấp tại chỗ và bảo toàn dữ liệu; kiểm cả schema sạch và schema legacy có lượt/audit/receipt trước khi cân nhắc áp dụng local.

## Kết quả

| Kiểm tra | Kết quả |
| --- | --- |
| .NET 10 restore/build solution | Pass; còn cảnh báo NU1902/NU1903 từ `System.Security.Cryptography.Pkcs` 9.0.0 có sẵn |
| npm install / web production build | Pass; còn cảnh báo bundle lớn và annotation SignalR có sẵn |
| Reception API/PostgreSQL | **28/28 pass**, gồm cùng key/khác key/hai staff, retry khi Visit InProgress/Completed, nguồn sai Patient, rollback khi DB lỗi, race cancel/check-in, ghế/nhân sự/attendee xung đột, và nâng cấp/rollback schema legacy |
| Browser/API/PostgreSQL thật | **5/5 pass**: một dòng chờ và đúng Visit cho bác sĩ; phải xác nhận danh tính; không ghép bằng điện thoại/giữ nháp sau lỗi; chặn lịch ngày khác; walk-in qua S11 rồi check-in |
| Full backend regression | Unit **43/43 pass**; integration **198/200 pass**. Hai lỗi auth bên dưới đã tái hiện trên baseline sạch |
| Database local | Migration `20261025163452_ReceptionVisits` áp dụng thành công. Fingerprint số lượng và ID Patient, Appointment, Visit, audit, receipt và lịch sử nguồn trước/sau khớp; 15 Patient/19 Appointment giữ nguyên, Visit local trước đó là 0 |
| Runtime local | `/health` API/database healthy tại `http://localhost:5080/health`; staff web `http://localhost:5173/reception` trả HTTP 200; options có ba ghế và query hàng chờ được phân quyền chạy được |

Hai regression có sẵn: `GoogleApiTests.ExistingLocalEmailCreatesSeparateGoogleAccount` mong 200 nhưng nhận 409; `RegistrationApiTests.RepeatedAndConcurrentRequestsCreateOneAccount` nhận hai 409 thay vì một thành công. Chạy riêng cả hai trên worktree baseline sạch `7da445f` đều thất bại cùng cách; không thay assertion hoặc mở lại quy tắc email để làm xanh DCMS-111.

Artifact kiểm chứng local, nằm ngoài Git:

```text
.tools/dcms111/results/dcms111_net10.0_20261026000657.trx
.tools/dcms111/regression/dcms111-final_net10.0_20261026000928.trx
.tools/dcms111/baseline/baseline-auth_net10.0_20261025235331.trx
.tools/dcms111/browser-results.json
.tools/dcms111/browser-results/reception.integration-real-1e7b4-t-opens-the-identical-Visit/visit-handoff.png
.tools/dcms111/local-before.txt
.tools/dcms111/local-after.txt
```

Ảnh trang bàn giao đã kiểm tra: đúng Patient/Appointment/bác sĩ/giờ đến, trạng thái Chờ khám, và thông báo phần khám chưa tích hợp. Không có mock API trong năm ca browser trên. Test backend dùng clock cố định trong vòng đời JWT để kiểm cùng ngày sát nửa đêm; browser dùng giờ API thật. Fixture browser có grid một phút và giờ mở 00:00–24:00 trong schema riêng để chạy sát ranh giới ngày, không đổi cấu hình phòng khám local.

## Chạy lại

Từ root, dot-source `infrastructure/scripts/import-development-env.ps1` rồi dùng `$dotnet` chạy `test backend/tests/DCMS.IntegrationTests/DCMS.IntegrationTests.csproj --filter FullyQualifiedName~ReceptionApiTests`. Test tạo schema riêng, không xóa dữ liệu local.

Để chạy browser, ở terminal test riêng:

```powershell
. .\infrastructure\scripts\prepare-reception-e2e.ps1
& $dotnet run --no-launch-profile --project backend/src/DCMS.Api --no-build -- --urls http://127.0.0.1:5082
```

Ở terminal web riêng, đặt `VITE_API_BASE_URL=http://127.0.0.1:5082` rồi chạy `npm.cmd run dev -- --host 127.0.0.1 --port 8083`. Ở terminal kiểm thử, import env để lấy mật khẩu demo vào `E2E_DEMO_PASSWORD` mà không in ra, đặt `E2E_API_URL=http://127.0.0.1:5082`, `E2E_BASE_URL=http://127.0.0.1:8083`, rồi từ `web` chạy `npx.cmd playwright test --config playwright.reception.config.ts`. Chỉ chạy bộ này trên fixture riêng do script chuẩn bị.

## Ranh giới bàn giao

`/visits/:visitId` và `GET /api/visits/{visitId}` đã đọc lượt thật, kiểm đúng chi nhánh/phân công. **Chưa có Start Visit, lưu nháp khám, chẩn đoán hay file lâm sàng của DCMS-112**; chưa đánh dấu toàn luồng check-in → khám → điều trị đã đạt. DCMS-112 tiếp tục trên chính Visit này.

Port `IReceptionSourceReader` dành cho module điều trị; adapter hiện tại không dựng danh mục đợt giả và từ chối ID đợt gửi thủ công. Thiếu nguồn tùy chọn vẫn tiếp nhận/cần bác sĩ review. FollowUpId có sẵn trên lịch được server mang theo; snapshot nguồn legacy và blocker được giữ khi nâng cấp. Chưa nghiệm thu việc chọn đợt thật từ module điều trị chưa tồn tại.

Hồi cứu chỉ dành cho ClinicAssistant có thêm claim Identity `permission=reception.backdate`; mặc định không cấp đại trà, không tự cấp cho Admin hoặc người quản lý lịch. Không có endpoint tự cấp quyền mới. Cơ chế cấu hình claim dùng provisioning được ủy quyền của hệ thống hiện có.

Không sửa lịch hẹn/hồ sơ local để tạo bằng chứng; các thao tác E2E ghi synthetic data trong schema riêng. Không đưa token, mật khẩu, ảnh bệnh nhân hay log phiên đăng nhập vào Git.
