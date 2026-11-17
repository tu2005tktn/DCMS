# Xóa hướng dẫn tái khám; `CLINICAL_ACTIVITY` chỉ còn hoạt động điều trị (18/11/2026)

Trạng thái: **spec đã duyệt theo yêu cầu người dùng, chưa triển khai**. Việc triển khai chờ phiên rút gọn `APPOINTMENT` ([APPOINTMENT-TABLE-SIMPLIFICATION.vi.md](APPOINTMENT-TABLE-SIMPLIFICATION.vi.md)) commit, vì phải sửa cùng các file lịch hẹn.

Yêu cầu người dùng ngày 18/11/2026: "Chỉ cần tạo lịch hẹn tiếp theo theo kế hoạch, xóa hướng dẫn tái khám" và "chỉ giữ các cột của hoạt động điều trị". Đây là giai đoạn 3 của [rút gọn CLINICAL_ACTIVITY](CLINICAL-ACTIVITY-TABLE-SIMPLIFICATION.vi.md). Không thêm bảng.

## Lý do

`FollowUpInstruction` (S24) không có màn hình riêng. Web chỉ tạo nó ngầm trong `NextVisitBookingDrawer` ngay trước khi đặt lịch, để lấy `followUpId` gắn vào lịch hẹn. Khung ngày, thời lượng và nha sĩ của lần hẹn đã có sẵn trên hạng mục kế hoạch (`TREATMENT_PLAN_ITEM.ReturnEarliestOn/ReturnTargetOn/ReturnLatestOn`, `ReservedMinutes`, `ResponsibleDentistId`), nên bản ghi trung gian là thừa.

## Luồng đặt lịch tiếp theo mới

1. `GET /api/patients/{patientId}/next-visit-candidates` giữ nguyên, nhưng `NextVisitCandidate` bỏ `followUpId`; trạng thái "đã đặt" tính từ lịch hẹn đang hiệu lực có `PlanItemId` bằng hạng mục.
2. Web gọi thẳng `POST /api/appointments` với `kind = "FollowUp"` và `planItemId = <ItemId>` (thay cho `followUpId`). Bỏ lời gọi `POST /api/follow-up-instructions`.
3. Server kiểm tra như trước, nhưng đọc từ hạng mục: hồ sơ đang mở, hạng mục `Agreed` và chưa xong; bệnh nhân/chi nhánh khớp; ngày hẹn trong khung kế hoạch (giữ quy tắc "trong vòng 4 ngày so với kế hoạch"); nha sĩ và thời lượng; sẵn sàng labo. Mỗi hạng mục chỉ có một lịch hẹn `Confirmed`/`CheckedIn` cùng lúc.
4. Đổi lịch, hủy, vắng mặt không còn chuyển trạng thái hướng dẫn (`Booked` ↔ `NeedsBooking`); hạng mục tự hiện lại trong danh sách "cần đặt lịch" khi không còn lịch hẹn hiệu lực.

Các mục đích khác của hướng dẫn (`PostoperativeReview`, `WarrantyReview`, `PeriodicRecall`, `NewProblem`) chỉ tạo được qua API, không có giao diện; bỏ cùng hướng dẫn. Lịch tái khám không theo kế hoạch đặt như lịch hẹn thường (`Standard`).

## Thay đổi schema

### `CLINICAL_ACTIVITY`: 50 → 31 cột

Bỏ 19 cột: 16 cột riêng của hướng dẫn (`SourceVisitId`, `SourceActivityId`, `SourceCompletionEventId`, `Purpose`, `Reason`, `Guidance`, `EarliestOn`, `TargetOn`, `LatestOn`, `DurationMinutes`, `DentistId`, `Capability`, `Status`, `ActiveAppointmentId`, `CreatedBy`, `UpdatedAt`) và 3 cột kỹ thuật của bảng nhiều loại:

| Cột | Cách thay |
| --- | --- |
| `ProcessKind` | Bảng chỉ còn một loại bản ghi; bỏ cột phân loại và mô hình TPH. Ràng buộc `CK_CLINICAL_ACTIVITY_VALUES` thành `"Quantity" > 0 AND "PriceSnapshotVnd" >= 0`; bỏ `CK_FOLLOW_UP_WINDOW`, `CK_FOLLOW_UP_DURATION`. |
| `RecordId` | Gộp vào `Id`. Dữ liệu hiện có `Id = RecordId` ở mọi hoạt động; migration kiểm tra lại điều này và dừng nếu khác. FK của `LAB_ORDER` (và các bảng tham chiếu `CLINICAL_ACTIVITY.Id`) giữ nguyên. |
| `StorageStamp` | `Version` thành khóa đồng thời duy nhất (như `VISIT`). Mọi thao tác ghi một hoạt động (lưu nháp, ghi nhận, sửa từ phiếu điều trị, cập nhật `FeeStatus` từ tài chính) phải tăng `Version` đúng 1. |

Còn lại 31 cột: `Id`, `PatientId`, `BranchId`, `VisitId`, `DossierId`, `ItemId`, `StepId`, `PlanVersion`, `IsPlanTransfer`, `ServiceId`, `ServiceNameSnapshot`, `Region`, `Unit`, `Quantity`, `LabWorkSnapshot`, `PerformedAt`, `ElapsedMinutes`, `PerformerId`, `AssistantId`, `RecordedBy`, `Outcome`, `NextSteps`, `ChargingBasis`, `ChargingBasisEvidence`, `PriceSnapshotVnd`, `DiscountSnapshotVnd`, `FeeStatus`, `State`, `Version`, `CreatedAt`, `PostedAt`. Tám cột không thuộc nhóm "chỉ của hoạt động" (`Id`, `PatientId`, `BranchId`, `DossierId`, `ItemId`, `StepId`, `PlanVersion`, `Version`) vẫn cần cho chính hoạt động điều trị.

### `APPOINTMENT.FollowUpId` → `APPOINTMENT.PlanItemId`

Đổi cột thành `PlanItemId uuid NULL`, FK `TREATMENT_PLAN_ITEM(Id)` `ON DELETE RESTRICT`, giữ chỉ mục duy nhất có lọc `Status IN ('Confirmed','CheckedIn')`. Chuyển dữ liệu: `PlanItemId` = `ItemId` của hướng dẫn mà lịch hẹn đang trỏ tới; lịch hẹn có hướng dẫn không gắn hạng mục chuyển `Kind` từ `FollowUp` sang `Standard` và `PlanItemId = NULL`. Phải phối hợp với spec rút gọn `APPOINTMENT` (bảng đích của spec đó còn giữ `FollowUpId`).

### `CARE_TASK.InstructionId`

Xóa. Công việc chăm sóc do sự kiện `followup.due` sinh ra vẫn giữ nội dung, chỉ mất liên kết.

## Code và API bị bỏ

- Route `/api/follow-up-instructions` (tạo, xem, sửa khung ngày, đổi trạng thái) và quy tắc phân quyền màn hình tương ứng.
- Entity `FollowUpInstruction`, `IFollowUpBookingPort`/`IFollowUpAppointmentChangePort` (`FollowUpAppointmentPort`) thay bằng cổng kiểm tra đặt lịch theo hạng mục kế hoạch.
- Bộ quét `TreatmentDueScanner` thôi phát `followup.due`; thông báo "có thể đặt lịch tái khám" và chiếu thông báo bệnh nhân khi hướng dẫn về `NeedsBooking` bị bỏ.
- Rà soát kế hoạch (`TreatmentRevisionService`) chỉ còn kiểm tra lịch hẹn theo hạng mục, không còn tác động lên hướng dẫn.
- Hợp đồng: `CreateAppointmentRequest.followUpId` → `planItemId`; DTO lịch hẹn của khách bỏ `followUp`; `CreateCareTaskRequest`/`CareTaskDto` bỏ `instructionId`; `BookingDrawer` bỏ nhánh `followUp` (không nơi nào dùng).
- Mobile: chi tiết lịch hẹn bỏ trạng thái hướng dẫn; kiểu đặt lịch bỏ `followUpId`.

## Migration dự kiến

Một migration `RemoveFollowUpInstructions`:
- `Up`: kiểm tra `Id = RecordId`; chuyển `APPOINTMENT.FollowUpId` sang `PlanItemId` như trên; xóa dòng `ProcessKind = 'FollowUpInstruction'`; bỏ chỉ mục, ràng buộc và 19 cột của `CLINICAL_ACTIVITY`; bỏ `CARE_TASK.InstructionId`.
- `Down`: thêm lại cột, ràng buộc, chỉ mục; `ProcessKind = 'ClinicalActivity'`, `RecordId = Id`, `StorageStamp = gen_random_uuid()`; `APPOINTMENT.FollowUpId` từ `PlanItemId` không khôi phục được vì hướng dẫn đã bị xóa (để `NULL`). Hướng dẫn tái khám đã xóa không khôi phục được.

Dữ liệu dev hiện có: 11 hoạt động, 4 hướng dẫn tái khám.

## Tài liệu cập nhật cùng lúc triển khai

S24 (`S24-FOLLOW-UP-INSTRUCTION-WEB.md`), `TREATMENT-PROGRESSION.vi.md`, `S22-S63-ACTUAL-REVIEW-CARE.md`, S60, bảng kiểm kê, ERD và spec rút gọn `APPOINTMENT`.
