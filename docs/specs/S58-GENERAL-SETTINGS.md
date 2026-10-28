# S58 — Thiết lập chung (UC-51, đợt 1)

Nguồn: DCMS_Screen_Specifications_v2.5 mục "S58 Thiết lập hệ thống chi tiết" (bổ sung 27/10/2026) §2, §3, §5 và DCMS_Use_Cases_Business_Rules_Messages_v2.5 "UC51 chi tiết" SET-01–08, §9.1–9.3; GBR-176/177/183/184; SET-AT-05/06/07/08/26/31. Phần Dụng cụ/Vật liệu giữ nguyên theo [SYSTEM-SETTINGS-MATERIAL-CATALOG.md](SYSTEM-SETTINGS-MATERIAL-CATALOG.md).

## Phạm vi đợt này

Có: nhóm **Thiết lập chung** gồm bốn khối Tự đăng xuất, Hồ sơ bắt buộc, Mã bệnh nhân, Mẫu in phiếu thu; version bất biến, preview, lịch sử, khôi phục bằng version mới; áp dụng Hồ sơ bắt buộc và Mã bệnh nhân vào lưu hồ sơ bệnh nhân (S05/S06, Hồ sơ của tôi).

Chưa có (ghi rõ trên UI, không giả lập):

- Tự đăng xuất chỉ lưu giá trị. Kiểm idle theo phiên (`lastInteractiveActivityAt`, `POST /api/auth/session/activity`, thu hồi refresh) là hạng mục riêng vì đổi hợp đồng phiên đăng nhập.
- Mẫu in chỉ lưu toggle. Chưa có module phiếu thu/in nên không có preview phiếu, print event hoặc bộ đếm số phiếu theo chi nhánh.
- Sửa next sequence mã bệnh nhân (lệnh riêng, chỉ tiến lên) chưa làm; sequence `patient_code_seq` hiện có tiếp tục được dùng.
- ~~Ba trường bắt buộc `profileDate`, `customerGroup`, `source` chưa có trên hồ sơ bệnh nhân~~ — đã bật được từ [đợt 3](S58-PATIENT-CATALOG-FIELDS.md).
- Các nhóm khác của S58 (dịch vụ, nhóm quyền, chi nhánh/ghế, export) và `GET /api/settings/audit` chung. Danh mục tham chiếu ở [đợt 2](S58-REFERENCE-CATALOGS.md).

## Quyền

Không có hệ thống grant tường minh trong code hiện tại, nên `settings.general.read`, `settings.general.manage` và `settings.audit.read` chỉ thuộc Staff active có role gán **ClinicOwner** (AT90: non-owner không có grant bị chặn). Admin, Dentist, ClinicAssistant không suy ra quyền này. `/api/auth/me` trả thêm permission `settings.general.manage` cho Owner. Mọi API kiểm lại tài khoản/role tại server ở từng request.

Đọc chính sách form bệnh nhân (`GET /api/patients/form-policy`) là projection nghiệp vụ theo quyền đọc bệnh nhân hiện có, không cần quyền settings (UC-51 A5).

## Dữ liệu

`CLINIC_SETTINGS_VERSION`: `Version` (PK, tăng 1), `EffectiveAt` (giờ server tại commit), `ActorId`, `Reason` (1–500), `RestoredFromVersion?`, `ContentJson` (jsonb, nội dung typed bên dưới). Bản ghi không bao giờ sửa/xóa; version hiện hành là version lớn nhất. Chưa có bản ghi nào thì hệ thống dùng mặc định với `version = 0` (không seed bằng GET).

Mặc định (giữ hành vi đang chạy): `staffIdleTimeoutMinutes = 20`; mọi `requiredFields` = false; `patientCode = { mode: AutoWithManualOverride, prefix: "BN" }` (thuật toán hiện hành `BN` + sequence 6 chữ số); `receiptPrint`: bật `showReceiptNumber`, `paymentMethod`, `totalFee`, `discount`, `previouslyPaid`, `remaining`, `paymentNote`, tắt phần còn lại.

Receipt/audit lệnh ghi dùng chung `SYSTEM_CATALOG_CHANGE` (requestKey, actor, command, resource cố định của Thiết lập chung, fingerprint, before/result JSON, thời điểm) trong cùng transaction với version mới.

## Nội dung và validation

```json
{
  "staffIdleTimeoutMinutes": 20,
  "requiredFields": { "profileDate": false, "gender": false, "birthDate": true, "address": false,
                      "phone": false, "customerGroup": false, "source": false },
  "patientCode": { "mode": "AutoWithManualOverride", "prefix": "BN" },
  "receiptPrint": { "internalUseNote": false, "showReceiptNumber": true, "patientAddress": false, "patientPhone": false,
                    "paymentMethod": true, "dentistAssistant": false, "totalFee": true, "discount": true,
                    "previouslyPaid": true, "remaining": true, "patientTotalOutstanding": false, "signatureLines": false,
                    "defaultPayerToPatient": false, "defaultCollectorToActor": false, "paymentNote": true }
}
```

- `staffIdleTimeoutMinutes` ∈ {5, 10, 20, 30, 60, 90, 120}; không có lựa chọn tắt.
- `patientCode.mode` ∈ `AutoWithManualOverride`, `AutoOnly`, `ManualOnly`. `prefix` 0–12 ký tự `A–Z`, `0–9`, `-`; server trim và chuyển chữ hoa. Mã sinh = prefix + sequence ≥ 6 chữ số, luôn ≤ 40 ký tự.
- `settings` và bốn object con bắt buộc; toggle thiếu được hiểu là `false`. Giá trị sai enum/range hoặc object thiếu trả 400 MSG04 kèm `field` (ví dụ `patientCode.prefix`).
- (Đợt 1) Bật `requiredFields.profileDate|customerGroup|source` trả 400 `SETTINGS_FIELD_UNAVAILABLE`. Từ đợt 3 các trường này có trên hồ sơ nên được bật; mã lỗi được giữ cho trường chưa hỗ trợ trong tương lai.
- `reason` bắt buộc 1–500 ký tự cho lưu và khôi phục (MSG02/MSG03).

## API

Tất cả trả lỗi dạng `SystemMessage {code,message,field?}`.

| Route | Hợp đồng |
|---|---|
| `GET /api/settings/capabilities` | Staff đăng nhập. `{ sections: [{ key, canManage }] }` chỉ gồm nhóm được phép: `general` (Owner), `materials` (Dentist hiệu lực, theo module Dụng cụ/Vật liệu). Không trả dữ liệu từng nhóm. |
| `GET /api/settings/general` | Owner. `{ version, effectiveAt?, actorName?, reason?, settings }`. |
| `POST /api/settings/general/preview` | Owner. `{ expectedVersion, settings }` → `{ currentVersion, stale, changes: [{ field, label, before, after }], warnings: [string], missingPatients: [{ field, label, count }] }`. Không ghi gì, không cấp mã. `missingPatients` chỉ cho trường bắt buộc vừa bật, đếm hồ sơ chưa ngừng sử dụng đang thiếu. |
| `PUT /api/settings/general` | Owner. `{ expectedVersion, requestKey, reason, settings }` → DTO như GET. Version lệch 409 MSG111; payload không đổi so với hiện hành 400 `SETTINGS_NO_CHANGE` ("Thiết lập không có thay đổi để lưu."). |
| `GET /api/settings/general/history?page=&pageSize=` | Owner. Trang 20/50/100, mới nhất trước: `{ items: [{ version, effectiveAt, actorName, reason, restoredFromVersion?, settings }], total, page, pageSize }`. |
| `POST /api/settings/general/restore-preview` | Owner. `{ sourceVersion, expectedVersion }` → cùng shape preview, so nội dung version nguồn với hiện hành. |
| `POST /api/settings/general/restore` | Owner. `{ sourceVersion, expectedVersion, requestKey, reason }` → version mới có `restoredFromVersion`; validate lại theo ràng buộc hiện hành; không lùi sequence/mã đã cấp. Version nguồn không tồn tại 404 MSG113. |
| `GET /api/patients/form-policy` | Quyền đọc bệnh nhân. `{ version, requiredFields, patientCode: { mode, prefix } }`. |

Lệnh ghi (SET-02): requestKey bắt buộc; cùng actor + lệnh + payload trả kết quả đã commit; khác actor/lệnh/payload 409 MSG111. Quyền kiểm trước khi replay. Lệnh ghi giữ khóa advisory độc quyền của Thiết lập chung; preview không giữ khóa.

## Áp dụng vào hồ sơ bệnh nhân (SET-07, SET-AT-05/06/07/08)

- `POST /api/patients` và `PUT /api/patients/{id}` nhận thêm `policyVersion?`. Lưu bệnh nhân giữ khóa advisory dùng chung (shared) với Thiết lập chung, đọc chính sách hiện hành trong transaction. Có `policyVersion` mà khác version hiện hành → 409 MSG111 với `field = "policyVersion"`; client tải lại chính sách và giữ bản nhập. Không gửi `policyVersion` (client cũ, ứng dụng bệnh nhân) thì validate theo chính sách hiện hành.
- Trường bắt buộc đang bật mà trống → 400 MSG02 với `field` là tên trường form (`birthDate`, `gender`, `address`, `phone`). Áp dụng cho tạo mới, sửa hành chính và "Hồ sơ của tôi"; không áp dụng cho đọc, ngừng/khôi phục hồ sơ, tiếp nhận hoặc khám. Không tự điền, không backfill.
- Mã bệnh nhân khi tạo: `AutoWithManualOverride` — trống thì sinh, có thì kiểm định dạng/unique (MSG201); `AutoOnly` — bỏ qua mã client, luôn sinh; `ManualOnly` — thiếu trả MSG02 `field = patientCode`, trùng MSG201. Đổi chế độ/prefix không đổi mã cũ. Sequence tăng nguyên tử và bỏ qua giá trị đã có người dùng.

## UI web

`/system-settings` hiển thị tab theo `capabilities`: **Thiết lập chung** (Owner) và các tab Dụng cụ/Vật liệu hiện có (Dentist). Menu "Thiết lập hệ thống" hiện khi có ít nhất một nhóm. Deep link `/system-settings?tab=general`.

Tab Thiết lập chung: bốn Card theo thứ tự Tự đăng xuất (Select), Hồ sơ bắt buộc (Checkbox; ba trường chưa có bị disable kèm chú thích), Mã bệnh nhân (Radio chế độ + Input prefix + ví dụ `PREFIX000001`), Mẫu in phiếu thu (Checkbox nhãn tiếng Việt). Mỗi khối chưa áp dụng ghi rõ "Đã lưu cấu hình; chưa áp dụng cho …". Đầu tab hiển thị version/hiệu lực/người lưu.

Luồng lưu: "Xem trước và lưu" → gọi preview → Modal liệt kê thay đổi, cảnh báo, số hồ sơ đang thiếu, ô Lý do bắt buộc → "Lưu" gửi PUT với requestKey giữ nguyên khi retry cùng payload. Không tự lưu khi tick. Đang gửi khóa submit. MSG111 giữ bản nhập, cho "Tải phiên bản mới". Rời tab khi có thay đổi chưa lưu phải xác nhận. Loading, lỗi tải, 403 tách biệt; lỗi không hiển thị như dữ liệu mặc định.

Lịch sử: bảng version/thời điểm/người lưu/lý do/khôi phục từ; nút "Khôi phục" → restore-preview → Modal lý do → restore.

Form bệnh nhân (S05/S06): tải `form-policy` khi mở; trường bắt buộc có dấu và rule; ô mã theo chế độ (AutoOnly disable, ManualOnly bắt buộc, gợi ý theo prefix); gửi `policyVersion`; nhận 409 `policyVersion` thì tải lại chính sách, giữ dữ liệu, báo "Thiết lập hồ sơ vừa thay đổi. Vui lòng kiểm tra lại các trường bắt buộc."

## Nghiệm thu đợt này

SET-AT-05, 06, 07, 08 (phần mode/prefix, GET không cấp số), 31 (restore tạo version mới), SET-02/03 retry/stale cho lệnh general, quyền Owner-only (AT90). Integration test trên schema PostgreSQL tạm; web build. Không tạo dữ liệu thử trong `dcms_dev`; migration vào `dcms_dev` cần người dùng duyệt riêng.

Tích hợp workspace 53d3e2e: các API `/api/settings/*` của module trong tài liệu này dùng tiền tố `/api/system-settings/settings/*`; API options dùng `/api/system-settings/reference-options`. Các API `/api/settings/*` hiện hữu tiếp tục dành cho module phân quyền/danh mục vận hành riêng. Xem [ghi chú tích hợp](../implementation/MAIN-INTEGRATION-20261027-53d3e2e.vi.md).
