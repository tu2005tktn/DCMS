# DCMS — Bàn giao cho AI tiếp quản

> Cập nhật: 03/11/2026. Đây là tài liệu khởi động nhanh cho Codex, Claude Code hoặc agent khác làm việc trong thư mục này. Đọc `AGENTS.md` trước khi thực hiện bất kỳ thay đổi nào.

## 1. Dự án này là gì

DCMS là hệ thống quản lý phòng khám nha khoa. Web hiện phục vụ hai nhóm chính:

- Nhân viên phòng khám: bệnh nhân, lịch hẹn, khám, điều trị, thanh toán, danh mục hệ thống.
- Khách hàng: tài khoản, lịch hẹn, Dashboard, người thân và đặt lịch hộ.

Stack hiện dùng:

- API: .NET 10, ASP.NET Core, EF Core/Npgsql 10, PostgreSQL.
- Web: React 19, TypeScript, Vite, Ant Design.
- Kiến trúc backend: `Domain` → `Application` → `Infrastructure` → `Api`.

## 2. Đọc theo thứ tự này

1. `AGENTS.md` — quy tắc bắt buộc khi làm việc trong repo.
2. `README.md` và `docs/SETUP.vi.md` — cài đặt, chạy local, kiểm tra service.
3. `docs/specs/README.md` — bộ đặc tả DCMS v2.5 là nguồn nghiệp vụ chính.
4. Đặc tả của feature đang sửa trong `docs/specs/`.
5. Tệp evidence cùng tên trong `docs/implementation/` để biết phần nào đã được kiểm chứng.

Các tài liệu kiến trúc cũ có thể chưa phản ánh toàn bộ migration mới. Khi cần biết schema hay endpoint hiện hành, kiểm tra source và migration hiện có trước khi kết luận.

## 3. Quy tắc không được bỏ qua

- Cập nhật tài liệu trong `docs/` trước khi sửa feature, API, dữ liệu hoặc UI.
- Không đưa secret, `.env`, dữ liệu bệnh nhân, file upload, database dump hay model weight vào Git.
- Trước mọi lệnh Git hoặc GitHub, kể cả `git status`, `git diff`, `git fetch`, phải mô tả lệnh và nhận đồng ý rõ ràng của người dùng cho chính lệnh đó.
- Không tự đưa thay đổi không liên quan vào commit; không tự push `main`.
- Chỉ áp migration vào `dcms_dev` sau khi đã giải thích tác động và nhận đồng ý riêng. Không seed hoặc tạo dữ liệu thử trong database đang vận hành.
- Khi có thay đổi UI ở `web/`, giữ React + Ant Design và CSS theo khu vực feature. Khi có thay đổi API, giữ Clean Architecture và feature folders hiện có.
- Trước khi sửa trong workspace, giả định có thể đang tồn tại thay đổi chưa commit của người dùng. Chỉ kiểm tra Git sau khi có phê duyệt theo quy tắc trên.

## 4. Chạy local và kiểm tra

Từ thư mục gốc dự án:

```powershell
docker compose --env-file .env -f infrastructure/docker/compose.yaml up -d
& .\infrastructure\scripts\start-api.ps1
```

Từ cửa sổ PowerShell khác:

```powershell
cd web
npm.cmd install
npm.cmd run dev
```

Endpoint local thường dùng:

- API health: `http://localhost:5080/health`
- Web: `http://127.0.0.1:5173`
- OpenAPI: `http://localhost:5080/openapi/v1.json`

Lệnh build chuẩn:

```powershell
$dotnet = if (Test-Path .\.tools\dotnet\dotnet.exe) { '.\.tools\dotnet\dotnet.exe' } else { 'dotnet' }
& $dotnet restore backend/DCMS.sln
& $dotnet build backend/DCMS.sln --no-restore
Push-Location web
try { npm.cmd run build } finally { Pop-Location }
```

Chỉ chạy test khi người dùng yêu cầu kiểm chứng hoặc khi task cần chứng minh thay đổi. Integration test phải dùng database/schema cách ly, không dùng `dcms_dev`.

## 5. Bản đồ feature quan trọng

| Nhu cầu | Điểm vào chính | Đặc tả |
| --- | --- | --- |
| Hồ sơ bệnh nhân và tab Điều trị & Thanh toán | `web/src/features/patients/PatientProfilePage.tsx`, `web/src/features/treatment/DossierList.tsx` | `docs/specs/S21-S43-TREATMENT-DOSSIER.md` |
| Phiếu điều trị, kế hoạch và quá trình điều trị | `web/src/features/treatment/TreatmentSheet.tsx` | `docs/specs/TREATMENT-PAYMENT-V25.md` |
| Kế hoạch điều trị dùng lại | `web/src/features/treatment/` và feature `Treatment` ở backend | `docs/specs/TREATMENT-ITEM-PLANS.md` |
| Ảnh trong phiếu điều trị | `web/src/features/treatment/` và feature Examination/Documents | `docs/specs/TREATMENT-SHEET-IMAGES.md` |
| Vật liệu trong ghi chú | `web/src/features/treatment/MaterialNotePicker.tsx` | `docs/specs/SYSTEM-SETTINGS-MATERIAL-CATALOG.md` |
| Cổng khách hàng | `web/src/features/customer-auth/`, `web/src/features/customer-booking/` | `docs/specs/CUSTOMER-WEB-UI.md`, `docs/specs/CUSTOMER-APPOINTMENTS-WEB.md` |
| Người thân khách hàng | `backend/src/DCMS.*/*FamilyMembers*`, `web/src/features/customer-family-members/` | `docs/specs/CUSTOMER-FAMILY-MEMBERS.md` |
| Thiết lập hệ thống/danh mục | `web/src/features/system-settings/`, feature SystemSettings ở backend | `docs/specs/S58-SYSTEM-SETTINGS.vi.md` |

## 6. Điều trị & Thanh toán: trạng thái gần đây

Luồng hiện hành dùng một dossier làm phiếu điều trị. Kế hoạch và quá trình điều trị là hai bảng nhập tại chỗ; không tạo appointment hoặc payment chỉ vì thêm kế hoạch.

- `DossierList.tsx` quản lý danh sách phiếu, tạo phiếu nhanh, mở/thu gọn thẻ phiếu và phần tổng quan đầu danh sách.
- `TreatmentSheet.tsx` quản lý hàng kế hoạch, hàng quá trình, ghi chú, ảnh và thông tin tài chính hiển thị trong từng phiếu.
- Dịch vụ tài chính hiện dùng `TreatmentFinancialSummary`. Khi chưa nối ledger, backend có adapter `UnavailableTreatmentFinancialSummaryPort`; không được coi dữ liệu dự toán là tiền đã thu.
- Menu hàng kế hoạch có Thực hiện, Hoàn tất/Bỏ hoàn tất, Sao chép, Sửa và Xóa. Sao chép lưu ngay một hàng mới, không mở editor, không tạo actual hay thay đổi hàng nguồn. Đặc tả: `docs/specs/TREATMENT-SHEET-COPY.md`.
- Hạng mục lấy từ kế hoạch dùng lại có snapshot danh mục/giá và không tự đổi theo catalog về sau.
- Ảnh phải được chọn từ file trên máy tại ô Hình ảnh của phiếu; upload gắn với dossier và hiện lại ở tab Hình ảnh. Đặc tả: `docs/specs/TREATMENT-SHEET-IMAGES.md`.

Yêu cầu gần nhất chưa triển khai tại thời điểm bàn giao:

1. Bỏ thanh tổng quan đầu danh sách phiếu đang hiện tổng dự toán, đã trả, còn nợ và số activity chờ ghi phí.
2. Bỏ dòng “Phí đã ghi nhận” trong phiếu.
3. “Đã trả” hiển thị số tiền đã trả, không có dữ liệu thì là `0`.
4. “Còn nợ” là số tiền cần thanh toán trừ số đã trả.

Trước khi code phần này, phải xác minh nghĩa của `PlannedVnd`, `PostedChargeVnd`, `ConfirmedAllocatedVnd` và `OutstandingVnd` trong `TreatmentFinancialSummary`. Không trình bày dự toán như khoản phải thu thực tế nếu contract tài chính không xác nhận điều đó.

## 7. Mốc bàn giao gần nhất

- Mốc commit gần nhất được ghi nhận trong cuộc trao đổi trước: `d8248e0 sao chép hạng mục điều trị`, đã push lên `origin/vinh`.
- Luôn xác minh nhánh, HEAD và thay đổi local bằng Git sau khi nhận phê duyệt của người dùng; tài liệu này không thay thế trạng thái Git thực tế.
- Các thay đổi local ngoài phần điều trị từng tồn tại quanh đăng ký, Google sign-in và cổng khách hàng. Không giả định chúng đã được commit hoặc xóa.

## 8. Cách bàn giao tiếp cho agent khác

Khi bắt đầu một task mới, agent nên báo ngắn:

1. Feature sẽ sửa và đặc tả sẽ cập nhật.
2. Các tệp dự định đọc/sửa.
3. Migration có cần thiết hay không.
4. Cách kiểm chứng phù hợp.

Trước commit, phải phân loại tệp theo đúng feature, kiểm tra staged diff, rồi xin phê duyệt từng lệnh Git theo `AGENTS.md`.
