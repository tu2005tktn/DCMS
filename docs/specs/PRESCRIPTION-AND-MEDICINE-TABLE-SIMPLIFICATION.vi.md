# Rút gọn bảng `PRESCRIPTION` (33 -> 11 cột) và `CATALOG_MEDICINE` (22 -> 10 cột)

Ngày cập nhật: 18/11/2026  
Áp dụng sau quyết định phương án **A** nhằm loại bỏ hai nhánh TPH không còn route sử dụng trên giao diện (`ExternalPrescription`, `PrescriptionTemplate`) khỏi `PRESCRIPTION`, đồng thời gom các trường mô tả dược phẩm của `CATALOG_MEDICINE` vào JSONB `Spec`, bỏ các cột audit trùng `COMMAND_RECEIPT` (`SystemCatalogChange`) và giữ nguyên toàn bộ giao diện kê đơn S23 (`PatientPrescriptionsTab.tsx`, `CustomerPrescriptionsPage.tsx`) và quản lý thuốc (`MedicineManagementPage.tsx`, `SupplyCatalogPanel.tsx`).

## 1. Bảng `PRESCRIPTION` (`33 -> 11` cột)

Bảng `public."PRESCRIPTION"` chỉ còn thực thể `PatientPrescription` với **11 cột**:

| Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- |
| `Id` | `uuid` PK | Định danh đơn thuốc bệnh nhân |
| `PatientId` | `integer NOT NULL` FK -> `PATIENT_PROFILE.Id` (`Restrict`) | Bệnh nhân được kê đơn |
| `BranchId` | `integer NOT NULL` FK -> `SCHEDULING_BRANCH.Id` (`Restrict`) | Chi nhánh kê đơn |
| `PrescriberEmployeeId` | `integer NOT NULL` FK -> `EMPLOYEE_PROFILE.Id` (`Restrict`) | Bác sĩ kê đơn |
| `PrescribedOn` | `date NOT NULL` | Ngày kê đơn (`IX_PRESCRIPTION_PatientId_PrescribedOn`) |
| `Diagnosis` | `varchar(1000) NOT NULL` | Chẩn đoán chính |
| `OtherDiagnosis` | `varchar(1000)` nullable | Chẩn đoán khác |
| `Advice` | `varchar(4000)` nullable | Ghi chú / lời dặn |
| `Version` | `integer NOT NULL DEFAULT 1` | Concurrency token |
| `CreatedAt` | `timestamptz NOT NULL` | Thời điểm tạo đơn |
| `Items` | `jsonb NOT NULL DEFAULT '[]'::jsonb` | Danh sách `PatientPrescriptionItem` (giữ `IsExternalPurchase` trong JSONB để bảo toàn rollback trước `20261112083713_SimplifyScreenStorage`) |

### 22 cột được loại bỏ trên `PRESCRIPTION`
- Cột phân biệt TPH và cờ legacy: `PrescriptionKind`, `RequiresExternalPurchase`, `UpdatedAt`.
- Nhánh `ExternalPrescription` (13 cột): `VisitId`, `PrescribedByDoctorId`, `Revision`, `Status`, `GeneralAdvice`, `CorrectionReason`, `SupersedesId`, `FinalizedAt`, `FinalizedBy`, `VerificationCode`, `CreatedBy`, `UpdatedBy`, `VisitLines`. Các màn hình cũ `PrescriptionPage.tsx`, `PrescriptionTemplatesPage.tsx` (chưa từng được mount trong router `App.tsx`) cùng các endpoint `/api/prescriptions/visits/*` và `/api/prescriptions/templates/*` được gỡ bỏ; `OwnClinicalRecordService` trả `HasPrescription = false`; `FamilyRecordQueries` chuyển sang đọc trực tiếp `PatientPrescription` (có `Diagnosis`, `Advice`, `PrescribedOn`, `PrescriberName`, `Items`).
- Nhánh `PrescriptionTemplate` (6 cột): `Code`, `Name`, `DiagnosisKind`, `Description`, `IsActive`, `TemplateLines`.
- Bảng `AUDIT_LOG` (`PrescriptionAudit`) và `COMMAND_RECEIPT` (`PatientPrescriptionReceipt`) giữ nguyên cấu trúc cột để không phát sinh thay đổi ngoài hai bảng mục tiêu.

## 2. Bảng `CATALOG_MEDICINE` (`22 -> 10` cột)

Bảng `public."CATALOG_MEDICINE"` giữ **10 cột**:

| Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- |
| `Id` | `uuid` PK | Định danh thuốc |
| `Code` | `varchar(40) NOT NULL` | Duy nhất qua `IX_CATALOG_MEDICINE_Code` |
| `Name` | `varchar(200) NOT NULL` | Duy nhất không phân biệt hoa thường qua `IX_CATALOG_MEDICINE_Name_Upper` (`UNIQUE (UPPER("Name"))`) |
| `BaseUnit` | `varchar(40) NOT NULL` | Đơn vị gốc |
| `ReferencePrice` | `bigint` nullable | Giá tham khảo (VND) |
| `PrescriptionEligible` | `boolean NOT NULL` | Cho phép kê đơn |
| `InternalStockTracked` | `boolean NOT NULL` | Thuốc phòng khám (`true`) / mua ngoài (`false`) |
| `IsActive` | `boolean NOT NULL` | Trạng thái đang dùng |
| `Version` | `integer NOT NULL DEFAULT 1` | Concurrency token |
| `Spec` | `jsonb NOT NULL DEFAULT '{}'::jsonb` | `CatalogMedicineSpec`: `ActiveIngredient`, `Strength`, `DosageForm`, `Packaging`, `Instructions` |

### 13 cột được loại bỏ trên `CATALOG_MEDICINE`
- 5 trường mô tả dược phẩm chuyển vào `Spec` JSONB: `ActiveIngredient`, `Strength`, `DosageForm`, `Packaging`, `Instructions`.
- 3 cột lâm sàng không dùng trên màn hình Quản lý thuốc/Export Excel: `Route`, `Contraindications`, `AllergyWarnings`.
- 4 cột audit (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`): lịch sử thay đổi đã lưu tại `COMMAND_RECEIPT` (`SystemCatalogChanges`).
- `NormalizedName`: thay bằng unique functional index `IX_CATALOG_MEDICINE_Name_Upper` và kiểm tra trong giao dịch dưới advisory lock `6462362060`.

## 3. Migration thực thi

- Migration `20261118041335_SimplifyPrescriptionAndMedicineTables`:
  - `Up`: Trên `PRESCRIPTION`, xóa các dòng `PrescriptionKind <> 'PatientPrescription'` (cùng `COMMAND_RECEIPT` tham chiếu nếu có), chuẩn hóa `Diagnosis`/`Version`/`CreatedAt` not null, xóa ràng buộc/index nhánh cũ, xóa 22 cột dư và set `NOT NULL` cho các cột bắt buộc. Trên `CATALOG_MEDICINE`, thêm `Spec` (`jsonb`), backfill 5 trường mô tả vào `Spec` và chuẩn hóa `Version >= 1`, xóa `IX_CATALOG_MEDICINE_NormalizedName`, xóa 13 cột dư và tạo `IX_CATALOG_MEDICINE_Name_Upper`.
  - `Down`: Khôi phục đầy đủ 13 cột cho `CATALOG_MEDICINE` (backfill `Spec` và `NormalizedName`, `Created*`/`Updated*` từ `USER_ACCOUNT` hoặc `1`) rồi xóa `Spec`; khôi phục đầy đủ 22 cột cho `PRESCRIPTION` (với default JSONB hợp lệ `'[]'::jsonb` cho `VisitLines`/`TemplateLines`, backfill `PrescriptionKind = 'PatientPrescription'`, `UpdatedAt = CreatedAt`, `RequiresExternalPurchase = true`) cùng các index/FK/check constraints phục vụ rollback trong `StorageSimplificationTests`.
