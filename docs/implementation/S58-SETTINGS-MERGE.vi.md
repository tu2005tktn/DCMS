# Gộp `/api/settings` vào `/api/system-settings` — kết quả và bằng chứng

Người thực hiện: Ngô Minh Trí, theo quyết định của Tư ngày 30/10/2026. Đặc tả: [S58-SETTINGS-MERGE.vi.md](../specs/S58-SETTINGS-MERGE.vi.md).

## Thay đổi

| Hạng mục | Nội dung |
| --- | --- |
| Route | Bỏ `/api/settings/general*`, `/api/settings/catalogs/*` và `/api/reference-options`.<br>Chuyển sang dưới `/api/system-settings`:<br>• nhóm quyền: `access-groups/*`, `staff/{id}/access-group`;<br>• quyền theo section: `access-capabilities`;<br>• lịch sử thay đổi thiết lập: `settings/audit`. |
| Service | `ISettingsService` chỉ còn nhóm quyền, gán nhóm, quyền theo section và lịch sử thay đổi. Phần general/catalog/options trùng lặp đã bỏ. `ISettingsAccess` không đổi. |
| Danh mục | `ReferenceCatalogService` kiểm grant `catalog.{kind}.*`; Owner vẫn có đủ quyền. `customer-groups` chỉ Owner. |
| Khám | Kiểm `diagnosisReferences` theo `REFERENCE_ENTRY`. Màn khám lấy chẩn đoán qua `/api/system-settings/reference-options?kind=diagnoses`, trả `version`, tối đa 500 mục. |
| Migration | `20261030090000_MergeOperationalReferenceEntries`: chỉ `INSERT … WHERE NOT EXISTS … ON CONFLICT DO NOTHING`, giữ `Id` và `Version`. Không có DROP, UPDATE hay DELETE. Down không làm gì. |
| Bảng legacy | `SETTINGS_VERSION` và `OPERATIONAL_REFERENCE_ENTRY` giữ nguyên dữ liệu; không còn API ghi vào. |
| Test của Tư | `SettingsApiTests` và `MainSettingsCompatibilityTests` chuyển sang route và contract mới, cùng các ý kiểm như trước.<br>Thêm kiểm: route cũ trả 404; dữ liệu cũ được chép cùng `Id`/`Version`; nhân viên chỉ có grant danh mục vẫn bị 403 sau khi thu hồi. |

## Bằng chứng

| Loại | Kết quả |
| --- | --- |
| API + PostgreSQL thật | Nhóm settings, khám, trường hồ sơ, thiết lập chung: 92/92. Toàn bộ suite: xem mục cuối |
| Migration trên schema mới | 33/33 chẩn đoán và 3/3 loại chi của bảng cũ có trong `REFERENCE_ENTRY` cùng `Id`; 0 dòng bị bỏ qua |
| Browser thật (API thật, không mock) | 3/3, không lỗi JS:<br>1. Route cũ trả 404; nhóm quyền chạy ở route mới.<br>2. Owner tạo chẩn đoán ở `/system-settings`.<br>3. Bác sĩ chọn được chẩn đoán đó ở màn khám; phiếu lưu snapshot `{id, version, name}` |
| Build | Backend 0 lỗi; web production build OK |

## Còn mở

- Shell web `/system-settings` hiển thị nhóm theo role (`/api/system-settings/settings/capabilities`). Nhân viên chỉ có grant một danh mục gọi API được nhưng chưa thấy tab tương ứng. Phần này thuộc DCMS-132 (SET-AT-01).
- Chưa có màn web cho nhóm quyền (`/api/system-settings/access-groups`), trước và sau khi gộp đều vậy.
