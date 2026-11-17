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
| `StorageStamp` (cột nền dùng chung) + `Version` | Giống `VISIT` về mặt thiết kế (nên chỉ giữ `Version`), nhưng `StorageStamp` nằm ở lớp nền dùng chung, và `Version` của hoạt động chỉ tăng ở các bước nghiệp vụ chứ không mỗi lần lưu. Gộp đòi hỏi đổi cơ chế chống ghi đè của mọi loại bản ghi còn lại. **Chưa quyết định**, ghi lại để người dùng cân nhắc. |
| `DossierId`, `StepId` (suy ra được từ `ItemId`) | Giữ: dùng làm khóa truy vấn trong nhiều dịch vụ, chỉ mục `(PatientId, DossierId, StepId)` và thống kê; bỏ sẽ phải thêm join ở mọi nơi. |
| `IsPlanTransfer` | Giữ: là điều kiện của chỉ mục duy nhất một lần chuyển kế hoạch cho mỗi `(ItemId, PlanVersion)`. |
| `FeeStatus`, `DiscountSnapshotVnd`, `PriceSnapshotVnd`, `ChargingBasis(Evidence)` | Giữ: ảnh chụp tài chính tại thời điểm ghi nhận, không tính lại được từ giá hiện hành (TREATMENT-LEDGER-INTEGRATION). |
| `PostedAt`, `CreatedAt`, `ElapsedMinutes`, `RecordedBy` | Giữ: được dùng trong hợp đồng API, kiểm tra nghiệp vụ và thống kê. |
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

## Giai đoạn 2: xóa đánh giá lâm sàng và ghi chú chăm sóc (18/11/2026)

Người dùng yêu cầu xóa hai loại bản ghi `ClinicalReview` (S63, đánh giá lâm sàng) và `ClinicalCareNote` (ghi chú chăm sóc) vì không có giao diện web hay mobile nào dùng; API chỉ được gọi từ integration test. Database phát triển không có dòng nào thuộc hai loại này. Bảng còn hai loại bản ghi: `ClinicalActivity` và `FollowUpInstruction`. `CLINICAL_ACTIVITY` giảm từ **66 xuống 50 cột**; `CARE_TASK` bỏ thêm 1 cột. Không thêm bảng.

### Cột bị xóa khỏi `CLINICAL_ACTIVITY` (16)

| Cột | Thuộc loại bản ghi |
| --- | --- |
| `Complaint`, `Findings`, `Coverage`, `CoveredComponent`, `Exclusions`, `Policy`, `PolicyVersion`, `Basis`, `NextAction`, `DentistAccountId`, `SourceItemId` | Chỉ `ClinicalReview` |
| `Content`, `Response`, `ActorId` | Chỉ `ClinicalCareNote` |
| `RecordedAt` | `ClinicalReview` và `ClinicalCareNote` |
| `ReviewId` | `ClinicalActivity` (liên kết tới đánh giá) và `ClinicalCareNote` |

Các cột dùng chung với loại còn lại (`VisitId`, `PatientId`, `BranchId`, `Outcome`, `Version`, `SourceActivityId`) được giữ. Bỏ kèm hai chỉ mục chỉ phục vụ đánh giá/ghi chú: `(VisitId, RecordedAt)` và `(SourceActivityId, SourceItemId)`.

### `CARE_TASK.ReviewId`

Công việc chăm sóc từng cho phép tham chiếu một đánh giá lâm sàng. Web S60 luôn gửi `reviewId: null`; DB phát triển không có dòng nào có giá trị. Cột bị xóa; `CreateCareTaskRequest` và `CareTaskDto` bỏ trường `reviewId` (System.Text.Json bỏ qua trường thừa nếu client cũ vẫn gửi).

### Hành vi thay đổi

- Gỡ route `GET/POST /api/visits/{visitId}/reviews`, `GET /api/reviews/{id}`, `POST /api/visits/{visitId}/care-notes` và quy tắc phân quyền màn hình tương ứng.
- `ActivityInput` và `ActivityDto` bỏ `reviewId`. Hoạt động trong lượt khám với `chargingBasis = Free` phải có hạng mục kế hoạch, giá 0 và căn cứ rõ ràng (trước đây có thể thay bằng một đánh giá "Covered").
- Lệnh lặp lại (`CLINICAL_COMMAND_RECEIPT`) cũ của `ReviewCreate`, `CareNoteCreate` được giữ nguyên như dữ liệu lịch sử.

### Chuyển đổi dữ liệu

- `Up`: xóa các dòng `ProcessKind IN ('ClinicalReview', 'ClinicalCareNote')`, bỏ hai chỉ mục, xóa 16 cột của `CLINICAL_ACTIVITY` và `CARE_TASK.ReviewId`.
- `Down`: thêm lại các cột và chỉ mục với kiểu cũ; **không khôi phục được** các dòng đánh giá/ghi chú đã xóa hay giá trị `ReviewId`.

### Chưa làm

`FollowUpInstruction` (hướng dẫn tái khám) chưa bị xóa: nó gắn với `APPOINTMENT.FollowUpId`, luồng đặt lịch tiếp theo (`NextVisitBookingDrawer`), tiếp nhận, thông báo, công việc chăm sóc và quét đến hạn điều trị. Đây là thay đổi lớn hơn hẳn và trùng các file lịch hẹn đang được rút gọn song song, nên cần quyết định riêng.

Migration: `SimplifyClinicalActivityTable` (giai đoạn 1) và `RemoveClinicalReviewsAndCareNotes` (giai đoạn 2).
