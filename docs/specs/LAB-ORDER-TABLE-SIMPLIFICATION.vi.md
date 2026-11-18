# Rút gọn bảng `LAB_ORDER` (36 -> 18 cột)

Ngày cập nhật: 18/11/2026  
Áp dụng sau quyết định phương án **A** nhằm loại bỏ các cột dư thừa suy diễn được từ `CLINICAL_ACTIVITY` / `TREATMENT_DOSSIER` và gom nhóm thông số kỹ thuật phục hình vào cột JSONB `Spec`, trong khi vẫn giữ nguyên 100% hợp đồng API (`LabOrderDto`, `SaveLabOrderRequest`, `LabWorkItem`), giao diện web (`LabWorkPage.tsx`, `LabOrderModal.tsx`) và toàn bộ luồng nghiệp vụ labo/thanh toán.

## 1. Cấu trúc mới (18 cột)

Bảng `public."LAB_ORDER"` giữ đúng **18 cột**:

| Nhóm | Cột | Kiểu / Ghi chú |
| --- | --- | --- |
| Định danh & liên kết | `Id`, `ActivityId`, `Code` | `ActivityId` duy nhất (`FK` cascade tới `CLINICAL_ACTIVITY.Id`); `Code` duy nhất theo chi nhánh được kiểm tra ở tầng ứng dụng qua `CLINICAL_ACTIVITY` -> `TREATMENT_DOSSIER` (`IX_LAB_ORDER_Code` partial non-unique `WHERE "Code" IS NOT NULL`). |
| Đối tác & chuyên môn | `DentistId`, `LabProviderId`, `LabWorkTypeId`, `WorkName` | Giữ nguyên liên kết danh mục và bác sĩ chỉ định. |
| Mốc chính & sản lượng | `OrderedOn`, `DueOn`, `DeliveredOn`, `Quantity` | `CK_LAB_ORDER_QUANTITY` (`"Quantity" BETWEEN 1 AND 999`). |
| Tài chính & ghi chú | `TotalFeeVnd`, `PaidVnd`, `Note` | `CK_LAB_ORDER_FEES` (`"TotalFeeVnd" >= 0 AND "PaidVnd" >= 0 AND "PaidVnd" <= "TotalFeeVnd"`). |
| Trạng thái & đồng thời | `Status`, `Version` | `Version` (default `1`) là concurrency token thay thế `StorageStamp`, tự tăng trên mỗi cập nhật; `IX_LAB_ORDER_Status_DueOn`. |
| Dữ liệu JSONB | `Spec`, `StatusLogs` | `Spec` lưu 11 thông số kỹ thuật phục hình & mốc trung gian; `StatusLogs` lưu lịch sử chuyển trạng thái append-only. |

### Cấu trúc `Spec` (`jsonb`, `LabOrderSpec`)

Đóng gói 11 trường trước đây nằm rải rác thành các cột rời:
- Đường hoàn tất: `MarginOuter`, `MarginInner`, `MarginSide`
- Phục hình & vị trí: `BridgeShapes` (`string[]`), `IsChild` (`bool`), `Teeth` (`string[]`), `Shade` (`Dictionary<string, string>`)
- Vật liệu & bảo hành: `Material`, `Warranty`
- Mốc trung gian gửi/nhận xưởng: `SentOn`, `ReceivedOn` (`DateOnly?`)

Ánh xạ EF Core dùng `HasConversion(JsonConv<LabOrderSpec>(), JsonCompare<LabOrderSpec>())` cùng `model.Ignore<LabOrderSpec>()` do `Shade` dùng `Dictionary<string, string>`.

## 2. 8 cột được loại bỏ

| Cột bỏ | Lý do / Nơi suy diễn |
| --- | --- |
| `DossierId` | Lấy qua `ActivityId` -> `CLINICAL_ACTIVITY.DossierId` |
| `PatientId` | Lấy qua `ActivityId` -> `CLINICAL_ACTIVITY` -> `TREATMENT_DOSSIER.PatientId` |
| `BranchId` | Lấy qua `ActivityId` -> `CLINICAL_ACTIVITY` -> `TREATMENT_DOSSIER.BranchId` |
| `StorageStamp` | Thay bằng `Version` làm concurrency token; xóa trực tiếp (`ExecuteDeleteAsync`) kiểm tra `Id` + `Version` |
| `CreatedAt`, `CreatedBy` | Đã có trên `CLINICAL_ACTIVITY` và mục khởi tạo đầu tiên trong `StatusLogs` |
| `UpdatedAt`, `UpdatedBy` | Theo dõi thay đổi trạng thái trong `StatusLogs`; không cần cột audit tách rời trên `LAB_ORDER` |

## 3. Migration thực thi

- Migration `20261118032811_SimplifyLabOrderTable`:
  - `Up`: Thêm cột `Spec` (`jsonb`, default `'{}'::jsonb`), chuẩn hóa `Version` (`defaultValue: 1`, `GREATEST("Version", 1)`), backfill toàn bộ 11 trường (`MarginOuter`, `MarginInner`, `MarginSide`, `BridgeShapes`, `IsChild`, `Teeth`, `Shade`, `Material`, `Warranty`, `SentOn`, `ReceivedOn`) vào `Spec`, xóa các index cũ (`IX_LAB_ORDER_BranchId_Code`, `IX_LAB_ORDER_DossierId`, `IX_LAB_ORDER_BranchId_Status_DueOn`), xóa 19 cột dư thừa/đã đóng gói, và tạo `IX_LAB_ORDER_Code`, `IX_LAB_ORDER_Status_DueOn`.
  - `Down`: Tạo lại đầy đủ 19 cột (gồm `StorageStamp` default `gen_random_uuid()`, `DossierId`, `PatientId`, `BranchId`, audit columns, `Teeth`, `BridgeShapes`, `Shade`, v.v.), backfill ngược từ `Spec`, `CLINICAL_ACTIVITY`, `TREATMENT_DOSSIER`, `StatusLogs`, xóa cột `Spec` rồi khôi phục index cũ (`IX_LAB_ORDER_BranchId_Code`, `IX_LAB_ORDER_BranchId_Status_DueOn`, `IX_LAB_ORDER_DossierId`), bảo đảm khả năng rollback cho `CareLabStorageTests`.
