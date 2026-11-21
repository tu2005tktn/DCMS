# Rút gọn bảng `TREATMENT_PLAN_ITEM` theo Phương án A (18/11/2026)

Yêu cầu người dùng ngày 18/11/2026: kế hoạch điều trị nằm ngay trên phiếu điều trị (`TreatmentSheet`), bảng `TREATMENT_PLAN_ITEM` đang dư quá nhiều cột so với giao diện thực tế. Phương án đã chốt là **Phương án A**: giữ đúng **18 cột** trên `TREATMENT_PLAN_ITEM` (giảm từ **65 xuống 18 cột**, bỏ `47` cột dư), giữ nguyên chuẩn 1NF (`32` bảng trong model EF / `33` bảng vật lý trong schema `public`).

## Cấu trúc bảng sau khi rút gọn (18 cột)

| Nhóm | Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| Định danh & thứ tự | `Id` | `uuid`, PK, `NOT NULL` | Khóa chính hạng mục |
| | `DossierId` | `uuid`, FK `TREATMENT_DOSSIER(Id)`, `NOT NULL` | Phiếu điều trị sở hữu hạng mục |
| | `PlannedOrder` | `int`, `NOT NULL` | Thứ tự hiển thị trên phiếu (index với `DossierId`) |
| Dịch vụ & vị trí | `ServiceId` | `int`, `NOT NULL` | Mã dịch vụ danh mục |
| | `ServiceName` | `varchar(160)`, `NOT NULL` | Tên dịch vụ hiển thị trên phiếu |
| | `Region` | `varchar(80)?` | Răng / vùng điều trị |
| | `Unit` | `varchar(40)`, `NOT NULL` | Đơn vị tính |
| | `Quantity` | `numeric(12,3)`, `NOT NULL`, `> 0` | Số lượng |
| Thời gian & phụ trách | `EstimatedStart` | `date?` | Ngày dự kiến |
| | `EstimatedMinutes` | `int?` | Thời lượng dự kiến (phút) |
| | `Note` | `varchar(1000)?` | Ghi chú hạng mục |
| | `ResponsibleDentistId` | `int?` | Nha sĩ phụ trách dòng |
| | `AssistantId` | `int?` | Phụ tá phụ trách dòng |
| Giá & giảm trừ | `AgreedUnitPriceVnd` | `bigint`, `NOT NULL`, `>= 0` | Đơn giá thỏa thuận (VND) |
| | `DiscountType` | `varchar(16)?` | `Fixed` hoặc `Percent` |
| | `DiscountValue` | `numeric(18,3)?` | Giá trị giảm giá |
| | `DiscountReason` | `varchar(1000)?` | Lý do giảm trừ |
| Trạng thái | `DisplayCompleted` | `bool`, `NOT NULL` | Đánh dấu hoàn tất hiển thị dòng kế hoạch |

## Liên kết với `CLINICAL_ACTIVITY`

- `CLINICAL_ACTIVITY."ItemId"` chuyển thành `uuid?` với khóa ngoại vật lý `FK_CLINICAL_ACTIVITY_TREATMENT_PLAN_ITEM_ItemId` (`REFERENCES "TREATMENT_PLAN_ITEM"("Id") ON DELETE SET NULL`) và partial unique index `(ItemId, PlanVersion) WHERE "IsPlanTransfer" = TRUE AND "ItemId" IS NOT NULL`.
- Ghi nhận hoạt động trực tiếp không xuất phát từ hạng mục kế hoạch (`+ Thêm việc đã làm`) lưu `CLINICAL_ACTIVITY."ItemId" = NULL`, không còn chèn dòng ẩn tổng hợp (`HiddenFromSheet`) vào `TREATMENT_PLAN_ITEM`.
- Xóa dòng hạng mục trên phiếu (`DELETE /api/dossiers/{dossierId}/sheet-items/{itemId}`) sẽ xóa vật lý dòng trong `TREATMENT_PLAN_ITEM`, đồng thời đặt `CLINICAL_ACTIVITY."ItemId" = NULL`, `IsPlanTransfer = FALSE` trên các hoạt động đã ghi và giữ nguyên `ServiceNameSnapshot`.

## 47 cột bị xóa

| Nhóm | Cột bị xóa | Cách thay thế |
| --- | --- | --- |
| Bước & đóng gói | `Steps` (`jsonb`), `StorageStamp` | Bỏ tiến trình bước `TREATMENT_STEP` và khóa chống ghi đè riêng từng hạng mục; dùng `TREATMENT_DOSSIER."Version"` làm khóa đồng thời khi chỉnh sửa phiếu. |
| Cờ ẩn/nguồn phiếu | `EntrySource`, `EntryRecordedBy`, `EntryRecordedAt`, `SheetDeleted`, `HiddenFromSheet` | Mọi dòng trong bảng đều là dòng hiển thị hợp lệ trên phiếu; hoạt động lẻ không tạo dòng `HiddenFromSheet`, xóa dòng là xóa vật lý. |
| Chẩn đoán & vật liệu dòng | `SourceDiagnosisId`, `Material` | Không có UI nhập liệu trên phiếu (`TreatmentSheet`). |
| Thời lượng & cửa sổ hẹn V23 (`10` cột) | `EstimatedMinMinutes`, `EstimatedMaxMinutes`, `ReservedMinutes`, `ShortReservationAcknowledgedBy`, `ShortReservationAcknowledgedAt`, `EstimatedSessions`, `EstimatedEnd`, `ReturnEarliestOn`, `ReturnTargetOn`, `ReturnLatestOn` | Giữ `EstimatedMinutes` và `EstimatedStart`; đặt lịch hẹn tiếp theo dùng trực tiếp `EstimatedStart` làm mốc mục tiêu (±4 ngày). |
| Tham chiếu & duyệt giảm giá (`7` cột) | `ReferenceUnitPriceVnd`, `ReferencePriceVersion`, `DiscountQualifyingItemId`, `DiscountStatus`, `DiscountBasisHash`, `DiscountDecidedBy`, `DiscountDecidedAt` | Áp dụng giảm giá trực tiếp khi bác sĩ nhập trên phiếu; `CommercialTreatmentPage` giữ quản lý bảng giá tham chiếu `/api/service-prices`. |
| Tư vấn, đồng thuận & quyết định (`10` cột) | `Decision`, `Progress`, `Counseling`, `DecisionMaker`, `RepresentativeBasis`, `DecidedAt`, `DecidedBy`, `DecisionPlanVersion`, `ConsentDocumentId`, `ConsentPlanVersion` | Gỡ quy trình `/plan/draft`, `/plan/commit`, `/plan/items/{itemId}/decision`; phiên bản kế hoạch đếm từ lịch sử sự kiện `PlanCommitted` trong `AUDIT_LOG`. |
| Bảo hành (`11` cột) | `WarrantyPolicy`, `WarrantyPolicyVersion`, `WarrantyScope`, `WarrantyConfirmedAt`, `WarrantyEndsOn`, `WarrantyConfirmedBy`, `WarrantyComponent`, `WarrantyExclusions`, `WarrantyActivatedOn`, `WarrantyExplanation`, `ExpectedServiceLifeMonths` | Gỡ API `/items/{itemId}/warranty/confirm` do không có màn hình sử dụng. |

## API và giao diện được gỡ bỏ

- Gỡ các endpoint `/api/dossiers/{dossierId}/steps*`, `/api/dossiers/{dossierId}/plan/from-template`, `/api/treatment-templates` (nhóm mẫu bước S58; giữ nguyên nhóm `/api/treatment-templates/item-plans` cho `ApplyItemPlanModal` / `ItemPlansPage`), `/api/dossiers/{dossierId}/items/{itemId}/warranty/confirm`, `/api/dossiers/{dossierId}/plan/draft`, `/api/dossiers/{dossierId}/plan/commit`, `/api/dossiers/{dossierId}/plan/items/{itemId}/decision` và `/api/commercial/dossiers*`.
- Gỡ `TreatmentDueScanner`, nhánh kiểm tra step trong `CareTaskIngestionService`, `progressionTypes.ts` và khối duyệt thương mại hồ sơ trong `CommercialTreatmentPage.tsx`.

## Migration `20261118013045_SimplifyTreatmentPlanItemTable`

- `Up`: đổi `CLINICAL_ACTIVITY."ItemId"` thành `nullable`; bảo toàn `ServiceNameSnapshot`, đặt `ItemId = NULL`, `IsPlanTransfer = FALSE` cho hoạt động trỏ tới `00000000-...` hoặc hạng mục bị ẩn/xóa/từ chối/hoãn; xóa khỏi `TREATMENT_PLAN_ITEM` các dòng `SheetDeleted`, `HiddenFromSheet`, `Refused`, `Deferred`; xóa giảm giá chưa duyệt (`DiscountStatus IS DISTINCT FROM 'Approved'`); xóa constraint `CK_TREATMENT_ITEM_DURATION_V23`, gỡ `47` cột dư và tạo `FK_CLINICAL_ACTIVITY_TREATMENT_PLAN_ITEM_ItemId` (`ON DELETE SET NULL`).
- `Down`: gỡ khóa ngoại `FK_CLINICAL_ACTIVITY_TREATMENT_PLAN_ITEM_ItemId`, thêm lại `47` cột với giá trị mặc định hợp lệ (`Steps = '[]'::jsonb`, `StorageStamp = gen_random_uuid()`, `Decision = 'Agreed'`, `DecisionPlanVersion = 1`, `Progress` suy từ `DisplayCompleted`, `DiscountStatus = 'Approved'` khi có giảm giá), đưa `CLINICAL_ACTIVITY."ItemId"` về `NOT NULL` và khôi phục `CK_TREATMENT_ITEM_DURATION_V23`.
