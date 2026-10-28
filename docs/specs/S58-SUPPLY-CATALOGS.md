# S58 — Công ty cung cấp/labo, Loại công việc labo, Thuốc (UC-51, đợt 4)

Nguồn: DCMS_Screen_Specifications_v2.5 "S58 Thiết lập hệ thống chi tiết" §2 (danh sách, Excel), §3 (grant), §6 (Công ty vật liệu, Công ty labo, Loại công việc labo, Thuốc), §6 Đơn vị; UC-51 chi tiết SET-01–06, §9.1, §9.3 (MSG257); SET-AT-15/17/18/30. Đợt trước: [Thiết lập chung](S58-GENERAL-SETTINGS.md), [Danh mục tham chiếu](S58-REFERENCE-CATALOGS.md), [Trường hồ sơ](S58-PATIENT-CATALOG-FIELDS.md).

## Phạm vi

CRUD typed (không dùng chung bảng REFERENCE_ENTRY), version, audit, deactivate/restore, không hard-delete:

### Công ty (Provider) — một bảng cho cả hai tab

| Trường | Ràng buộc |
|---|---|
| `code` | Bắt buộc ≤40, chữ hoa sau trim, unique |
| `name` | Bắt buộc 1–160, unique (chuẩn hóa) |
| `type` | `Supplier`, `Lab`, `Both` |
| `address` | ≤1000 |
| `phone` | ≤20, mẫu số điện thoại như hồ sơ bệnh nhân |
| `email` | ≤254, định dạng email |

Tab "Công ty cung cấp vật liệu" lọc `type ∈ {Supplier, Both}`; tab "Công ty labo" lọc `{Lab, Both}`. Một công ty Both là một ID, không nhân đôi. Đổi loại làm mất vai trò Lab khi còn Loại công việc labo (kể cả ngừng dùng) dùng công ty đó làm mặc định → 409 MSG205 kèm số mục. Không tạo công nợ khi lưu.

### Loại công việc labo (LabWorkType)

`code` (bắt buộc ≤40, unique), `name` (bắt buộc ≤160, unique), `providerId?` (công ty Lab/Both đang dùng; giữ được công ty đã gắn dù sau đó ngừng dùng), `unit` ∈ `Jaw` (hàm), `HalfJaw` (bán hàm), `Tooth` (răng), `Piece` (cái), `Set` (bộ), `referencePrice?` (số nguyên VND ≥0), `warranty?` (≤1000, chỉ là mô tả). Yêu cầu labo (S44/S45, chưa có) sẽ chụp giá/đơn vị/bảo hành tại lúc tạo.

### Thuốc (Medicine)

`code` (bắt buộc ≤40, unique), `name` (bắt buộc 1–200, unique), `activeIngredient?` ≤200, `strength?` ≤200, `dosageForm?` ≤200, `baseUnit` (bắt buộc ≤40, ví dụ viên), `packaging?` ≤160 (nhãn đóng gói, ví dụ hộp 10 vỉ × 10 viên), `instructions?` ≤1000 (lời dặn mặc định cho dòng đơn mới), `referencePrice?` (số nguyên VND ≥0), `prescriptionEligible`, `internalStockTracked` (độc lập). Không lô/FEFO; chưa có module kho nên chưa khóa đổi đơn vị gốc theo movement.

## Quy tắc chung

- SET-03: tên giữ dấu, trim + FormC; so trùng chữ hoa; code chữ hoa, mẫu `^[A-Z0-9][A-Z0-9._-]*$`. Trùng → 409 MSG201 kèm `field` (`code`/`name`), dùng chung câu chữ đợt 2. Unique tính cả mục đã ngừng dùng.
- SET-04: giá là số nguyên VND; `null` = chưa xác định, `0` = giá tham khảo 0. Giá âm → 400 **MSG257** ("Giá dịch vụ không được âm. Giảm giá, voucher hoặc khấu trừ vui lòng ghi nhận bằng chương trình khuyến mãi hoặc giảm trừ có lý do.") `field = referencePrice`. Giá > 1.000.000.000.000 → MSG04.
- SET-02: requestKey bắt buộc; replay cùng actor + lệnh + payload; khác → 409 MSG111; version lệch → MSG111. Sửa/đổi trạng thái bắt buộc `reason` 1–500. Sửa không đổi gì → `SETTINGS_NO_CHANGE`.
- Audit/receipt chung `SYSTEM_CATALOG_CHANGE` như đợt 2; xem lịch sử từng mục.
- Ngừng dùng chỉ ẩn khỏi lựa chọn mới. Ngừng công ty còn Loại công việc labo đang dùng làm mặc định: cho phép (loại công việc giữ liên kết), preview liệt kê số mục bị ảnh hưởng.

## Quyền

`providers.*`, `lab-work-types.*`, `medicines.*` (read/manage/export) chỉ thuộc Staff active có role gán ClinicOwner, như các đợt trước. `capabilities` trả thêm `supplies`.

Projection lựa chọn qua `GET /api/reference-options?kind=`: `suppliers` (Supplier/Both) và `lab-providers` (Lab/Both), `lab-work-types` — ClinicAssistant hoặc Dentist hiệu lực; `medicines` — Dentist hiệu lực, chỉ thuốc `prescriptionEligible`. Trả `{ id, code, name }` của mục đang dùng; không trả giá.

## API

Với `{resource}` ∈ `providers`, `lab-work-types`, `medicines` (route cụ thể, ưu tiên hơn `{kind}` của đợt 2):

| Route | Hợp đồng |
|---|---|
| `GET /api/settings/catalogs/{resource}?q=&status=&page=&pageSize=` (+ `type=Supplier\|Lab` cho providers, lọc kèm Both) | Trang typed DTO |
| `GET …/{id}` · `POST …` · `PUT …/{id}` | `{ requestKey, entry }` / `{ expectedVersion, requestKey, reason, entry }` |
| `POST …/{id}/state-preview` · `POST …/{id}/state` | như đợt 2 |
| `GET …/{id}/history` | như đợt 2 |
| `GET /api/settings/catalogs/medicines/export?q=&status=` | xlsx, cùng bộ lọc, không đổi dữ liệu. Mọi ô ghi dạng chuỗi nên ô bắt đầu `=`, `+`, `-`, `@` không thành công thức. |

DTO trả kèm `version`, `active`, `createdAt/ByName`, `updatedAt/ByName`; lab work type kèm `providerName`, `providerActive`.

## UI web

Tab **Vật tư, labo & thuốc** trong `/system-settings` (Owner) với 4 mục: Công ty cung cấp vật liệu, Công ty labo, Loại công việc labo, Thuốc. Mỗi mục: tìm kiếm, lọc trạng thái, bảng, phân trang 20/50/100, Thêm/Sửa (Drawer form, Lý do khi sửa), Ngừng dùng/Khôi phục có preview + lý do, Lịch sử. Thuốc có nút "Xuất Excel". Giá nhập số nguyên, hiển thị định dạng VND; để trống = chưa xác định.

## Nghiệm thu

SET-AT-15 (công ty Both một ID ở hai tab; đổi về Supplier khi còn loại công việc labo bị chặn), 17 (loại công việc không có công ty mặc định vẫn lưu), 18 (thuốc kê mua ngoài không cần theo dõi tồn), 30 (export đúng lọc, ô công thức an toàn), 12 phần giá âm/NULL/0, quyền Owner. Integration test trên schema tạm; web build.

Tích hợp workspace 53d3e2e: các API `/api/settings/*` của module trong tài liệu này dùng tiền tố `/api/system-settings/settings/*`; API options dùng `/api/system-settings/reference-options`. Các API `/api/settings/*` hiện hữu tiếp tục dành cho module phân quyền/danh mục vận hành riêng. Xem [ghi chú tích hợp](../implementation/MAIN-INTEGRATION-20261027-53d3e2e.vi.md).
