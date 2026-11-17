# Rút gọn bảng `CLINICAL_ACTIVITY` (18/11/2026)

Tác vụ tự động theo cùng cách rút gọn `VISIT` ([VISIT-TABLE-SIMPLIFICATION.vi.md](VISIT-TABLE-SIMPLIFICATION.vi.md)): bỏ cột thừa, cột chỉ lặp lại thông tin đã có, giữ nguyên hợp đồng API/web/mobile. Không thêm bảng. Bảng giảm từ **67 xuống 66 cột** (số cột thực tế của bảng dùng chung nhiều loại bản ghi).

## Hiện trạng

`CLINICAL_ACTIVITY` là một bảng duy nhất (TPH, cột phân loại `ProcessKind`) chứa bốn loại bản ghi: `ClinicalActivity` (hoạt động điều trị), `ClinicalReview` (đánh giá), `ClinicalCareNote` (ghi chú chăm sóc) và `FollowUpInstruction` (hướng dẫn tái khám). Hợp của các cột là 67; nhiều cột dùng chung tên giữa các loại (`PatientId`, `BranchId`, `VisitId`, `Outcome`, `Version`, `RecordedAt`...), nên số cột của riêng `ClinicalActivity` là 32 trên tổng 67.

Rà soát từng cột của `ClinicalActivity`: toàn bộ 32 cột đều có EF model, đều được ghi hoặc đọc (hoạt động, thống kê công việc, ghi nhận doanh thu theo `PerformedAt`, khoản phí điều trị, hồ sơ bệnh án trên mobile S41). Không có cột mồ côi ngoài model, vì bảng được tạo mới bởi migration `ConsolidateTreatmentStorage` (khác `VISIT`, vốn nâng cấp từ schema cũ).

## Cột bị xóa

| Cột | Lý do | Cách thay |
| --- | --- | --- |
| `SourceKind` (`Visit` / `Dossier`) | Lặp lại thông tin đã có ở `VisitId`. Hoạt động từ phiếu điều trị luôn có `VisitId = NULL` (dịch vụ chặn ghi nhận khi `VisitId` có giá trị); hoạt động trong lượt khám luôn có `VisitId`. | `SourceKind` tính khi đọc: `VisitId IS NULL` → `Dossier`, ngược lại `Visit`. DTO `ActivityDto.sourceKind` giữ nguyên giá trị. |

## Đã xem xét và giữ lại

| Cột / ý tưởng | Quyết định |
| --- | --- |
| `StorageStamp` (cột nền của cả 4 loại) + `Version` | Giống `VISIT` về mặt thiết kế (nên chỉ giữ `Version`), nhưng `StorageStamp` nằm ở lớp nền dùng chung; `ClinicalCareNote` không có `Version`, và `Version` của hoạt động chỉ tăng ở các bước nghiệp vụ chứ không mỗi lần lưu. Gộp đòi hỏi đổi cơ chế chống ghi đè của cả bốn loại bản ghi. **Chưa quyết định**, ghi lại để người dùng cân nhắc. |
| `DossierId`, `StepId` (suy ra được từ `ItemId`) | Giữ: dùng làm khóa truy vấn trong nhiều dịch vụ, chỉ mục `(PatientId, DossierId, StepId)` và thống kê; bỏ sẽ phải thêm join ở mọi nơi. |
| `IsPlanTransfer` | Giữ: là điều kiện của chỉ mục duy nhất một lần chuyển kế hoạch cho mỗi `(ItemId, PlanVersion)`. |
| `FeeStatus`, `DiscountSnapshotVnd`, `PriceSnapshotVnd`, `ChargingBasis(Evidence)` | Giữ: ảnh chụp tài chính tại thời điểm ghi nhận, không tính lại được từ giá hiện hành (TREATMENT-LEDGER-INTEGRATION). |
| `PostedAt`, `CreatedAt`, `ElapsedMinutes`, `RecordedBy`, `ReviewId` | Giữ: được dùng trong hợp đồng API, kiểm tra nghiệp vụ và thống kê. |
| Đổi tên `CreatedAt` ↔ `RecordedAt` giữa các loại bản ghi | Không đổi: ảnh hưởng chéo bốn loại và hợp đồng API. |

## Chuyển đổi dữ liệu

- `Up`: `DROP COLUMN "SourceKind"`. Không cần chuyển đổi vì giá trị suy ra được từ `VisitId`.
- `Down`: thêm lại `SourceKind varchar(16)` (nullable như các loại bản ghi khác, theo model cũ) rồi đặt `Dossier` hoặc `Visit` cho mọi dòng `ProcessKind = 'ClinicalActivity'` theo `VisitId`.

## Hợp đồng API

Không đổi. `ActivityDto.sourceKind` vẫn trả `Visit` hoặc `Dossier`; web và mobile không cần sửa.

## Phụ thuộc đã kiểm tra

`MATERIAL_USAGE` (FK `ActivityId`), khoản phí điều trị, thống kê công việc, doanh thu theo `PerformedAt` và hồ sơ bệnh án mobile S41 không đọc `SourceKind` từ cơ sở dữ liệu; chỉ `ActivityDto` và logic ghi nhận (`ClinicalWorkService`) dùng giá trị này.

## Migration

`20261117183321_SimplifyClinicalActivityTable`.
