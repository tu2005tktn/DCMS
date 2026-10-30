# S58 — Danh mục tham chiếu (UC-51, đợt 2)

Nguồn: DCMS_Screen_Specifications_v2.5 "S58 Thiết lập hệ thống chi tiết" §2, §3, §6 (Chẩn đoán, Nhóm khách hàng, Danh mục phụ), §10 seed; DCMS_Use_Cases_Business_Rules_Messages_v2.5 "UC51 chi tiết" SET-01–06, §9.1 (`/api/settings/catalogs/{kind}`, `/api/reference-options`), §9.3; GBR-178, GBR-182; SET-AT-01/02/03/04/09/32. Đợt 1: [S58-GENERAL-SETTINGS.md](S58-GENERAL-SETTINGS.md).

## Phạm vi

Một engine danh mục dùng chung cho các kind đơn giản trong allowlist v1:

| kind | Nhãn | Trường riêng |
|---|---|---|
| `diagnoses` | Chẩn đoán/tên bệnh | `code` = mã ICD tùy chọn ≤40 (chỉ tham khảo, không xác nhận hợp lệ lâm sàng) |
| `patient-sources` | Nguồn khách hàng | — |
| `occupations` | Nghề nghiệp | — |
| `intake-history` | Tiền sử (lời khai hành chính) | — |
| `care-types` | Loại chăm sóc khách hàng | — |
| `expense-categories` | Loại chi | `reserved` chỉ đọc |
| `customer-groups` | Nhóm khách hàng | `code` bắt buộc; `description` là ghi chú. Chỉ phân loại: không giá thẻ/giá trị/điểm/tích điểm/thời hạn thẻ, không tự giảm phí (DCMS-127, SET-AT-19). Thành viên = hồ sơ bệnh nhân có `CustomerGroupId` trỏ tới nhóm |

Các kind còn lại trong allowlist (`service-categories`, `services`, `providers`, `material-categories`, `materials`, `lab-work-types`, `medicines`) có hợp đồng typed riêng ở các đợt sau; gọi engine này với kind khác trả 400 MSG04 `field = kind`. Không hard-delete, không export Excel cho các kind ở đây.

Consumer: S05/S06 chọn nguồn/nghề/nhóm/tiền sử từ [đợt 3](S58-PATIENT-CATALOG-FIELDS.md). Chưa nối: S19/S23 chọn chẩn đoán qua module này (S19 hiện dùng danh mục chẩn đoán của DCMS-121), S51 loại chi, S60 loại CSKH. Projection `reference-options` đã sẵn cho các consumer đó.

## Dữ liệu

`REFERENCE_ENTRY`: `Id` (uuid), `Kind` (≤32), `Code?` (≤40, lưu chữ hoa sau trim), `Name` (1–160, trim + Unicode FormC, giữ dấu), `NormalizedName` (chữ hoa của Name), `Description?` (≤1000), `IsReserved`, `IsActive`, `Version`, `CreatedAt/CreatedBy`, `UpdatedAt/UpdatedBy`. Unique `(Kind, NormalizedName)` và `(Kind, Code)` khi Code có giá trị, tính cả mục đã ngừng dùng (SET-03: không gộp, không tái sử dụng tên trùng).

- `code`: bắt buộc với mọi kind trừ `diagnoses`; mẫu `^[A-Z0-9][A-Z0-9._-]*$` sau khi chuyển chữ hoa.
- Lệnh ghi dùng chung receipt/audit `SYSTEM_CATALOG_CHANGE` (ResourceId = Id entry); `BeforeJson` = `{ reason, before }`, `ResultJson` = DTO sau lưu. Người tạo/sửa do server ghi.

Seed (migration, chạy một lần, có ID cố định; chỉ các tên đã nêu cụ thể trong đặc tả, không tự sáng tác tên còn thiếu manifest — Q01):

- `intake-history`: Chảy máu lâu, Phản ứng thuốc, Thấp khớp, Huyết áp cao, Huyết áp thấp, Tiểu đường, Bệnh tim, Bệnh gan, Bệnh phổi (mã `TS01`–`TS09`).
- `expense-categories`: Dụng cụ - Vật liệu (`CHI-VATLIEU`), Labo (`CHI-LABO`), Trả lương (`CHI-LUONG`) là `reserved`; Văn phòng phẩm (`CHI-VPP`).
- `patient-sources`: Khác (`KHAC`).
- 34 chẩn đoán, 10 nguồn còn lại và 13 nghề nghiệp chưa có danh sách tên trong tài liệu: phòng khám nhập qua UI; không seed.

## Quyền

Không có grant store tường minh, nên `catalog.{kind}.read/create/update/deactivate` chỉ thuộc Staff active có role gán ClinicOwner (cùng nguyên tắc đợt 1). `capabilities` trả thêm section `catalogs` cho Owner.

`GET /api/reference-options` (UC-51 A5) không cần quyền settings; chỉ trả mục đang dùng `{id, code, name, version}` theo vai trò nghiệp vụ của consumer:

- `diagnoses`: Dentist hiệu lực.
- `patient-sources`, `occupations`, `intake-history`, `customer-groups`: ClinicAssistant hoặc Dentist hiệu lực (quyền đọc bệnh nhân).
- `care-types`: ClinicAssistant hoặc Dentist hiệu lực.
- `expense-categories`: ClinicOwner.

## API

| Route | Hợp đồng |
|---|---|
| `GET /api/settings/catalogs/{kind}?q=&status=Active\|Inactive\|All&page=&pageSize=` | `q` ≤100 tìm theo tên/mã; trang 20/50/100; sort tên chuẩn hóa rồi Id. `{ items: [ReferenceEntryDto], total, page, pageSize }` |
| `GET /api/settings/catalogs/{kind}/{id}` | `ReferenceEntryDto`; khác kind hoặc không có 404 MSG113 |
| `POST /api/settings/catalogs/{kind}` | `{ requestKey, entry: { code?, name, description? } }` → 201 DTO |
| `PUT /api/settings/catalogs/{kind}/{id}` | `{ expectedVersion, requestKey, reason, entry }` → DTO |
| `POST /api/settings/catalogs/{kind}/{id}/state-preview` | `{ active }` → `{ entry, allowed, blockers: [string], impact: string }`; không ghi |
| `POST /api/settings/catalogs/{kind}/{id}/state` | `{ expectedVersion, requestKey, active, reason }` → DTO |
| `GET /api/settings/catalogs/{kind}/{id}/history` | Mới nhất trước: `[{ occurredAt, actorName, command, reason?, before?, after }]` |
| `GET /api/reference-options?kind=&search=` | Tối đa 50 mục active (`diagnoses`: 500, vì màn khám tải cả danh sách một lần), sort tên: `[{ id, code, name, version }]`. Từ 30/10/2026 là nguồn chọn chẩn đoán khi khám ([gộp settings](S58-SETTINGS-MERGE.vi.md)) |

`ReferenceEntryDto`: `{ id, kind, code, name, description, reserved, active, version, createdAt, createdByName, updatedAt, updatedByName, patientCount? }`.

`patientCount` (DCMS-127) chỉ có với các kind gắn vào hồ sơ bệnh nhân (`customer-groups`, `patient-sources`, `occupations`, `intake-history`); với các kind khác là `null`. Đây là số hồ sơ **chưa ngừng sử dụng** đang trỏ tới mục, đếm trực tiếp từ `PATIENT_PROFILE` tại thời điểm đọc, không lưu cột đếm riêng. Chỉ trả số tổng hợp, không trả danh sách bệnh nhân.

`entry` chỉ nhận ba trường trên (không mass assignment: actor, kind, reserved, active không lấy từ body). `reason` 1–500 bắt buộc khi sửa và đổi trạng thái.

## Quy tắc

- SET-01: ngừng dùng chỉ ẩn khỏi `reference-options` và lựa chọn mới; bản ghi vẫn đọc được. Khôi phục kiểm lại trùng tên/mã.
- Mục `reserved` không ngừng dùng được: 409 `SETTINGS_RESERVED_ENTRY` ("Mục này được hệ thống dùng cho chứng từ liên kết nên không thể ngừng dùng."). Vẫn đổi được tên/mô tả.
- SET-02: requestKey bắt buộc; cùng actor + lệnh + payload trả kết quả cũ; khác → 409 MSG111. Version lệch → 409 MSG111. Kiểm quyền trước replay.
- Trùng: 409 MSG201 kèm `field` — mã: "Mã danh mục đã tồn tại. Vui lòng chọn mã khác."; tên: "Tên đã tồn tại trong danh mục. Vui lòng nhập tên khác."
- Sửa không có thay đổi: 400 `SETTINGS_NO_CHANGE`.
- Đổi trạng thái sang đúng trạng thái hiện tại: 409 MSG111.
- Preview trạng thái: `blockers` rỗng trừ mục reserved; `impact` mô tả "Mục sẽ không còn được chọn cho giao dịch mới; dữ liệu đã ghi giữ nguyên." Với kind có `patientCount > 0`, impact nêu thêm "Đang gắn với N hồ sơ bệnh nhân; các hồ sơ này giữ nguyên giá trị". Ngừng dùng nhóm/nguồn đang có thành viên **không bị chặn** (SET-01): hồ sơ cũ vẫn hiển thị mục kèm nhãn "(ngừng dùng)" và giữ giá trị khi sửa trường khác.

## UI web

Tab **Danh mục** trong `/system-settings` (Owner). Bên trái chọn loại danh mục; bên phải: tìm kiếm, lọc Đang dùng/Ngừng dùng/Tất cả, bảng Mã (Mã ICD với chẩn đoán)/Tên/Mô tả/Trạng thái/Thao tác, phân trang 20/50/100. Thêm/Sửa bằng Modal (Sửa có ô Lý do bắt buộc); Ngừng dùng/Khôi phục bằng Modal hiển thị tên, tác động, lý do. Lịch sử thay đổi mở trong Drawer. Đang gửi khóa submit; lỗi giữ bản nhập, trùng đánh dấu ô; MSG111 cho "Tải lại bản mới". Tách loading/lỗi/không có kết quả. Mục reserved có Tag "Hệ thống" và không có nút Ngừng dùng. Với Nhóm khách hàng, Nguồn, Nghề nghiệp, Tiền sử: bảng có cột "Số hồ sơ" lấy từ `patientCount`.

## Nghiệm thu

SET-AT-01 (Owner/403), 02 (tạo/sửa có dấu, tìm/lọc/phân trang), 03/04 (stale, retry cùng/khác payload), 32 (seed không trùng, reserved), reference-options chỉ trả active và đúng vai trò. DCMS-127: số hồ sơ theo nhóm đúng với liên kết thật (tăng/giảm khi gắn/bỏ, không đếm hồ sơ đã ngừng sử dụng); SET-AT-19 gắn nhóm không đổi khoản phí, giảm trừ hay phiếu thu. Integration test trên schema tạm; web build.

Tích hợp workspace 53d3e2e: các API `/api/settings/*` của module trong tài liệu này dùng tiền tố `/api/system-settings/settings/*`; API options dùng `/api/system-settings/reference-options`. Các API `/api/settings/*` hiện hữu tiếp tục dành cho module phân quyền/danh mục vận hành riêng. Xem [ghi chú tích hợp](../implementation/MAIN-INTEGRATION-20261027-53d3e2e.vi.md).
