# S60 — Chăm sóc khách hàng trên web: kết quả code và bằng chứng

Người thực hiện: Ngô Minh Trí. Ngày 09/11/2026, nhánh `TriNM`. Đặc tả: [S60-CUSTOMER-CARE-WEB.vi.md](../specs/S60-CUSTOMER-CARE-WEB.vi.md).

## Thay đổi

| Phần | Nội dung |
| --- | --- |
| Backend | `ICareTaskService`/`CareTaskService` thêm `OptionsAsync`, `PatientOptionsAsync`, `WorklistAsync`, `ExportAsync`<br>Thêm route `GET /api/care-tasks/options`, `/patient-options`, `/worklist`, `/export`, dùng quyền `care-tasks.read` hiện có<br>Không có bảng mới, không có migration |
| Sửa lỗi | `POST`/`PATCH /api/care-tasks` lưu `dueAt` dưới dạng UTC. Trước đây gửi giờ có `+07:00` thì API trả 500 |
| Web | Menu **Chăm sóc khách hàng** dưới **Hộp thư hỗ trợ**, chỉ cho vai trò Trợ lý. Route `/customer-care`<br>Danh sách: lọc chi nhánh, xem theo ngày/tuần/tháng, ‹ ›, người phụ trách, loại, trạng thái, khách hàng; "Tìm thấy N phiếu."; [Excel]; phân trang; TẠO MỚI<br>Hộp thoại **Thêm chăm sóc / Sửa chăm sóc**<br>Ngăn chi tiết: ghi nhận liên hệ, lịch sử, đánh dấu đã xong<br>Nút "…" mở bảng danh mục `care-types` của S58 khi có quyền |
| Tài liệu | Đặc tả S60 mới; cập nhật S22-S63 (đã có web S60) và S58-REFERENCE-CATALOGS (đã nối loại CSKH) |

Mã nguồn chính:
- [CareTaskService.cs](../../backend/src/DCMS.Infrastructure/Features/Treatment/CareTaskService.cs)
- [ClinicalWorkEndpoints.cs](../../backend/src/DCMS.Api/Features/Treatment/ClinicalWorkEndpoints.cs)
- `web/src/features/customer-care/`

## Bằng chứng

**API + PostgreSQL thật:** `CustomerCareWorklistApiTests` đạt 1/1. Test chỉ để local, không đưa lên Git. Nội dung kiểm:
- khách mới check-in (`Queued`) không được gợi ý; bắt đầu khám (`InProgress`) thì được gợi ý, có SĐT; tìm rỗng trả 400;
- `options` có chi nhánh, nhân viên kèm vai trò, loại danh mục và loại hệ thống;
- tạo phiếu với `dueAt +07:00`; gửi lại cùng `requestKey` không tạo trùng;
- lọc theo ngày, người phụ trách, loại, trạng thái `Pending`/`Done`, tìm theo SĐT; ký tự `%` được thoát;
- liên hệ gần nhất hiện trong danh sách; sửa bằng phiên bản cũ trả 409 MSG111; đánh dấu xong thành công;
- `pageSize` sai, khoảng ngày ngược, trạng thái lạ đều trả 400 MSG04;
- Excel `no-store`, có nội dung, SĐT, nhãn "Đã xong" và "Đã liên lạc được";
- chi nhánh không phải thành viên: 403 ở `worklist`, `export`, `patient-options` và không có trong `options`.

**Hồi quy:** `ClinicalWorkApiTests`, `AllScreensPermissionApiTests`, `ReferenceCatalogApiTests`, `TreatmentProgression*` và test mới: 46/47 đạt.
- Một test lỗi: `ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId`. Bước `POST /api/activities/{id}/post` trả 503.
- Chạy lại đúng test này trên HEAD gốc `67de397` (chưa có thay đổi S60): lỗi y hệt (503). Lỗi có từ trước, không do S60.

**Trình duyệt Chromium thật → API thật**, schema `dcms_test_demo`, dữ liệu giả. Kết quả:
- `dentist.demo` và `owner.demo` không thấy menu; vào thẳng `/customer-care` thì bị chặn.
- `assistant.demo` thấy "Chăm sóc khách hàng" ngay dưới "Hộp thư hỗ trợ".
- Hai loại "Hỏi thăm sau điều trị" và "Nhắc tái khám" được Chủ phòng khám tạo qua API danh mục thật.
- Tạo phiếu: ô khách gợi ý "BN000004 · … · khám 08/11/2026"; phiếu hiện trong danh sách với tên loại, SĐT, người phụ trách.
- Ghi nhận "Không nghe máy" kèm bước tiếp theo, có trong lịch sử.
- Đánh dấu đã xong thì nút Sửa biến mất, danh sách hiện "Đã xong" và liên hệ gần nhất.
- Lọc "Cần xử lý" không còn phiếu này. Tìm BN000004 đúng số phiếu.
- Xem theo tuần ra 09/11–15/11/2026 (thứ Hai–Chủ nhật). Tải Excel hợp lệ.

**Build:** backend build đạt; `npm run build` của web đạt.

## Cập nhật 09/11/2026 (sau khi merge `main` có quản lý chi nhánh)

| Phần | Nội dung |
| --- | --- |
| Phạm vi đọc | **Trợ lý / Nha sĩ:** chỉ chi nhánh mình là thành viên. **Chủ phòng khám:** mọi chi nhánh đang hoạt động, theo S58-BRANCHES §4<br>`worklist`/`export` nhận `branchId` tùy chọn; bỏ trống thì gồm mọi chi nhánh đọc được. Chi tiết phiếu (`GET /{id}`) cũng đọc theo phạm vi này |
| Phạm vi ghi | Giữ nguyên: Nha sĩ/Trợ lý là thành viên chi nhánh. Chủ phòng khám luôn có vai trò hiệu lực Nha sĩ, nên ghi được ở chi nhánh mình là thành viên |
| Web | Menu hiện cho Trợ lý **và Chủ phòng khám**<br>Chủ phòng khám có ô "Chi nhánh", mặc định "Tất cả chi nhánh"; nhân viên khác không có ô này<br>Thêm cột "Chi nhánh" khi xem nhiều chi nhánh. Hộp thoại tạo có ô chi nhánh khi người tạo thuộc từ 2 chi nhánh |
| Bảng | Chữ 13px, bảng gọn, bố cục cố định, nội dung hiện đầy đủ. Bảng kéo hết bề ngang, vì khung trang căn giữa phần tử con |

Bằng chứng:
- **API + PostgreSQL thật:** `CustomerCareWorklistApiTests` 2/2. Ca mới kiểm:
  - Trợ lý không thấy phiếu chi nhánh B; lọc hoặc mở phiếu chi nhánh B trả 403;
  - Chủ phòng khám thấy cả hai chi nhánh, lọc được chi nhánh B, mở được chi tiết, xuất Excel được;
  - ở chi nhánh không phải thành viên, Chủ phòng khám tạo, sửa, ghi liên hệ đều trả 403.
- **Hồi quy:** `AllScreensPermissionApiTests`, `S58ExportAuditApiTests`, `MainSettingsCompatibilityTests`, `CustomerCareWorklistApiTests` 45/45. `BranchIsolationTests`, `OwnerAllBranchesTests`, `ClinicalWorkApiTests` đạt, trừ một test lỗi từ trước (mục "Còn mở").
- **Trình duyệt Chromium thật → API thật**, schema `dcms_test_demo`, 2 chi nhánh. Kết quả:
  - `assistant.b.demo` tạo một phiếu giả ở chi nhánh B;
  - `assistant.demo` không có ô chi nhánh, không thấy phiếu chi nhánh B; API trả 403;
  - `assistant.b.demo` chỉ thấy phiếu chi nhánh B;
  - `owner.demo` có ô "Tất cả chi nhánh", bảng có cột "Chi nhánh", chọn chi nhánh B thì còn 1 phiếu và mở chi tiết không có nút ghi;
  - `dentist.demo` không thấy menu;
  - ở màn 1440px, bảng rộng 1078px, không cuộn ngang, chữ ô 13px.
- **Sửa khi merge:**
  - Khôi phục 2 định nghĩa quyền `catalog.services.export` và `catalog.materials.export` (DCMS-132). Chúng bị mất khi giải xung đột `SettingsGrants.cs` bằng bản của `main`; `AllScreensPermissionApiTests` phát hiện.
  - Trả lại import `PrescriptionTemplatesPage` trong `App.tsx`, vì `main` đã dùng trang này.

## Còn mở

- **Test review/activity trả 503 từ trước:** `ReviewSaveDoesNotPostActivityAndActivityRetryUsesOneId` lỗi cả trên HEAD gốc. Chuyển người phụ trách S22/S63.
- **Chưa làm trên màn này:** chuyển bác sĩ (escalation) và chọn lịch hẹn cho kết quả "Đã đặt lại lịch". API đã có.
- **Danh mục loại CSKH chưa seed:** đặc tả S58 không cho tự đặt tên seed. Chủ phòng khám thêm trong Thiết lập hệ thống, hoặc cấp quyền `catalog.care-types.*` cho Trợ lý qua Phân quyền truy cập.
