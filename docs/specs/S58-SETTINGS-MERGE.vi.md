# S58 — Gộp `/api/settings` vào `/api/system-settings`

Ngày chốt: 30/10/2026. Người quyết định: Tư (chủ DCMS-119/121/128) đồng ý dùng `/api/system-settings` làm nguồn duy nhất. Người thực hiện: Ngô Minh Trí.

## 1. Vấn đề

Main đang có hai module thiết lập chạy song song.

| Phần | `/api/system-settings` (màn web `/system-settings`) | `/api/settings` (DCMS-119/121/128) |
| --- | --- | --- |
| Thiết lập chung | `CLINIC_SETTINGS_VERSION`. Đây là nguồn mà form bệnh nhân, tự đăng xuất (DCMS-129) và phiếu thu (DCMS-130) đọc | `SETTINGS_VERSION`, không consumer nào đọc |
| Danh mục phụ | `REFERENCE_ENTRY`, có UI quản lý | `OPERATIONAL_REFERENCE_ENTRY`, không có UI |
| Chọn chẩn đoán khi khám | — | `/api/reference-options` đọc `OPERATIONAL_REFERENCE_ENTRY` |
| Nhóm quyền, grant, gán nhóm | — | `ACCESS_GROUP`, `ACCESS_GROUP_GRANT`, `STAFF_ACCESS_ASSIGNMENT`; `ISettingsAccess` được khám, thu ngân, tiếp nhận… dùng |
| Lịch sử | `SYSTEM_CATALOG_CHANGE` | `SETTINGS_AUDIT` |

Hệ quả:
- Chẩn đoán Owner tạo ở `/system-settings` **không** xuất hiện khi bác sĩ khám.
- Bật trường bắt buộc qua `/api/settings/general` không có tác dụng với form bệnh nhân.

## 2. Kết quả sau khi gộp

- **Nguồn duy nhất:**
  - Thiết lập chung dùng `CLINIC_SETTINGS_VERSION`.
  - Mọi danh mục phụ, gồm `diagnoses`, `patient-sources`, `occupations`, `intake-history`, `care-types`, `expense-categories` và `customer-groups`, dùng `REFERENCE_ENTRY`.
- **Nhóm quyền giữ nguyên dữ liệu và logic:** chỉ đổi route sang `/api/system-settings`. `ISettingsAccess` và các grant không đổi, nên khám, thu ngân và tiếp nhận vẫn kiểm quyền như cũ.
- **Bảng cũ không bị xóa:** `SETTINGS_VERSION` và `OPERATIONAL_REFERENCE_ENTRY` giữ nguyên dữ liệu ở dạng legacy chỉ đọc. Không còn API nào ghi vào hai bảng này.

### Đổi route

| Cũ | Mới |
| --- | --- |
| `GET /api/settings/capabilities` | `GET /api/system-settings/access-capabilities` (nội dung giữ nguyên) |
| `GET/PUT /api/settings/general`, `POST .../preview`, `.../restore-preview`, `.../restore` | **Bỏ.** Dùng `/api/system-settings/settings/general` (đã có preview/history/restore) |
| `/api/settings/catalogs/{kind}` (list/get/create/update/state-preview/state) | **Bỏ.** Dùng `/api/system-settings/settings/catalogs/{kind}` |
| `GET /api/settings/audit` | `GET /api/system-settings/settings/audit` (nội dung giữ nguyên; DCMS-132 mở rộng) |
| `GET /api/reference-options?kind&context&branchId&search` | **Bỏ.** Dùng `GET /api/system-settings/reference-options?kind&search` |
| `/api/access-groups`, `/grant-definitions`, `/staff`, `/{id}`, `/{id}/preview`, `/{id}/state` | `/api/system-settings/access-groups/...` (cùng hậu tố) |
| `PUT /api/staff/{employeeId}/access-group` | `PUT /api/system-settings/staff/{employeeId}/access-group` |

Thay đổi ở `GET /api/system-settings/reference-options`:
- `ReferenceOption` thêm `version` vào cuối. Đây là thay đổi tương thích: client cũ bỏ qua trường mới.
- Màn khám lấy `{id, version, code, name}` để chụp snapshot chẩn đoán như trước.
- Quyền giữ nguyên: `diagnoses` dành cho nha sĩ (kể cả Owner kiêm nha sĩ); các danh mục hồ sơ dành cho trợ lý hoặc nha sĩ.

### Quyền quản lý danh mục

`/api/system-settings/settings/catalogs/{kind}` kiểm grant `catalog.{kind}.{read|create|update|deactivate}` qua `ISettingsAccess`, theo quy tắc trước đây của `/api/settings/catalogs`:
- Owner có đủ các grant này nên hành vi với Owner không đổi.
- Nhân viên được giao nhóm quyền có grant tương ứng thì quản lý được đúng danh mục đó.
- Quyền được kiểm trước khi trả lại kết quả của lần gửi lại, nên người đã bị thu hồi quyền nhận 403.
- `customer-groups` chưa có grant nên vẫn chỉ Owner.

### Khám (DCMS-112)

- Server kiểm `diagnosisReferences` bằng `REFERENCE_ENTRY` (`Kind = diagnoses`). Mục ngừng dùng trả MSG202; version, tên hoặc mã lệch trả MSG111.
- Snapshot đã lưu trên phiếu cũ không bị kiểm lại và giữ nguyên.

## 3. Chuyển dữ liệu (migration `MergeOperationalReferenceEntries`)

Migration chỉ chèn thêm dòng, không cập nhật hay xóa dòng nào.

1. Với mỗi dòng của `OPERATIONAL_REFERENCE_ENTRY`, chép sang `REFERENCE_ENTRY` và **giữ nguyên `Id` và `Version`**, để snapshot chẩn đoán trên phiếu khám vẫn khớp.
   - `Code` được trim và viết hoa, như quy tắc của `REFERENCE_ENTRY`.
   - `Reserved` chuyển thành `IsReserved`, `Active` thành `IsActive`.
   - `UpdatedAt` và `UpdatedBy` lấy bằng `CreatedAt` và `CreatedBy`.
2. Bỏ qua dòng nếu `REFERENCE_ENTRY` đã có cùng `Id`, cùng (`Kind`, `NormalizedName`) hoặc cùng (`Kind`, `Code`). Bản đang dùng trên màn `/system-settings` được giữ.
   - Dòng bị bỏ qua vẫn nằm nguyên trong bảng cũ.
   - Phiếu khám cũ trỏ tới dòng đó vẫn đọc được, vì snapshot nằm trên phiếu.
3. Down: không làm gì. Không xóa dòng đã chép vì có thể đã được tham chiếu.

## 4. Nghiệm thu

1. Chẩn đoán tạo ở `/system-settings` chọn được khi khám. Phiếu khám lưu snapshot `{id, version, name, code}`; ngừng dùng thì không chọn mới được (MSG202).
2. Chẩn đoán có sẵn trong bảng cũ vẫn chọn được sau migration, cùng `Id`. Phiếu đã lưu trước migration mở và đính chính được.
3. Các route cũ trả 404. Route mới của nhóm quyền và gán nhóm cho kết quả, quyền, retry và stale giống trước.
4. Khám, thu ngân và tiếp nhận vẫn kiểm grant như cũ: toàn bộ test tích hợp đạt.
5. Migration chạy hai lần không nhân dòng. Không có `DROP` hoặc `DELETE`.
