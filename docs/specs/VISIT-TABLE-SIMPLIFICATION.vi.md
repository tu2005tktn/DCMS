# Rút gọn bảng `VISIT` (17/11/2026)

Yêu cầu người dùng ngày 17/11/2026: bỏ các cột thừa của `VISIT` và các dữ liệu không cần lưu trong phiếu khám, đưa bảng về dạng một người thiết kế bảng sẽ làm. Phương án đã chốt là "A": giữ `Version`, bỏ `StorageStamp`. Bảng giảm từ **32 xuống 16 cột**. Không thêm bảng.

## Cột sau khi rút gọn

| Nhóm | Cột | Ghi chú |
| --- | --- | --- |
| Khóa | `Id` | PK |
| | `AppointmentId` | FK `APPOINTMENT`, duy nhất |
| | `PatientId`, `BranchId`, `DentistId` | FK |
| | `RecordedBy` | FK `USER_ACCOUNT`, người tiếp nhận |
| Tiếp nhận | `ArrivalAt`, `Purpose`, `ArrivalReason`, `Note` | |
| Trạng thái | `Status` | `Queued`, `InProgress`, `ExaminationConfirmed`, `Completed`, `Cancelled` |
| Nội dung khám | `ExaminationContentJson`, `Investigations` | JSONB |
| Cập nhật phiếu khám | `UpdatedAt`, `UpdatedBy` | Đổi tên từ `ExaminationUpdatedAt`, `ExaminationUpdatedBy` |
| Chống ghi đè | `Version` | Khóa đồng thời duy nhất |

## Cột bị xóa và cách thay

| Cột | Cách thay |
| --- | --- |
| `AdditionalConcern`, `ClinicalPriority`, `ReviewReason`, `PriorityDentistId`, `IdentityNote`, `QueueOrder`, `Code`, `ActivityId`, `DossierId`, `ItemId`, `StepId` | Không có trong EF model, không được code nào đọc hoặc ghi; chỉ tồn tại trên database từng nâng cấp từ schema tiếp nhận cũ (database tạo mới từ migration không có). Xóa bằng `DROP COLUMN IF EXISTS`, kèm FK `FK_VISIT_EMPLOYEE_PROFILE_PriorityDentistId` và các index `IX_VISIT_Code`, `IX_VISIT_BranchId_QueueOrder`, `IX_VISIT_PriorityDentistId`. |
| `SourceJson`, `RequiresClinicalSourceReview` | Bỏ việc lễ tân chọn "đợt điều trị cũ" khi tiếp nhận. Lượt "Tiếp tục điều trị" mở tab Điều trị của bệnh nhân để bác sĩ chọn hồ sơ. Lịch tái khám vẫn nối với hướng dẫn tái khám qua `APPOINTMENT.FollowUpId`. Dữ liệu nguồn cũ được giữ trong `AFTER_JSON` của dòng audit `CheckIn` sẵn có. |
| `ExaminationConfirmed` | Gộp vào `Status = ExaminationConfirmed`. Phiếu khám được coi là đã xác nhận khi `Status` là `ExaminationConfirmed` hoặc `Completed`. |
| `StorageStamp` | `Version` trở thành khóa đồng thời của cả `Visit` và `VisitExamination` (hai entity dùng chung dòng). Mỗi lần lưu lượt khám hoặc phiếu khám, `Version` tăng đúng 1. |
| `RecordedAt` | Dùng `ArrivalAt`. Thời điểm ghi nhận thực tế của lần tiếp nhận hồi cứu vẫn có trong audit `CheckIn`. Bỏ ràng buộc `CK_VISIT_Arrival`. |

## Luồng trạng thái

`Queued` → (bắt đầu khám) → `InProgress` → (xác nhận phiếu) → `ExaminationConfirmed` → (hoàn tất lượt) → `Completed`. `Cancelled` giữ nguyên ý nghĩa.

| Thao tác | Trạng thái cho phép | Trạng thái sau |
| --- | --- | --- |
| Lưu nháp phiếu khám | `InProgress` | `InProgress` |
| Xác nhận phiếu khám | `InProgress` | `ExaminationConfirmed` |
| Sửa phiếu đã xác nhận (đính chính) | `ExaminationConfirmed`, `Completed` | Giữ nguyên |
| Hoàn tất lượt | `ExaminationConfirmed` | `Completed` |
| Chỉ định cận lâm sàng, kê đơn, tải tài liệu, hoạt động điều trị trong lượt | `InProgress`, `ExaminationConfirmed` (lượt đang mở) | Giữ nguyên |
| Bổ sung chỉ định sau khi xác nhận (cần lý do) | `ExaminationConfirmed` | Giữ nguyên |

Lịch hẹn (`APPOINTMENT.Status`) vẫn là `InProgress` trong suốt `InProgress` và `ExaminationConfirmed` của lượt khám.

## Phiếu khám: thôi lưu 3 trường suy ra

`ExaminationContentJson` không lưu `diagnoses`, `diagnosis`, `conclusion` nữa. Ba giá trị này được tính khi đọc:
- `diagnoses`: chẩn đoán chung (`unscopedDiagnoses`) cộng chẩn đoán từng răng/vùng (`findings[].diagnoses`), bỏ trùng.
- `diagnosis` và `conclusion`: `diagnoses` nối bằng `"; "`.

API, web và mobile vẫn nhận đủ ba trường như trước. Dữ liệu cũ đã lưu ba trường này vẫn đọc được; lần lưu tiếp theo sẽ bỏ chúng.

## Hợp đồng API

- `POST /api/reception/check-ins`: bỏ `priorCourseId`.
- `VisitResult`: bỏ `source`, `requiresClinicalSourceReview`, `recordedAt`.
- Bỏ `GET /api/reception/appointments/{appointmentId}/courses`.
- Bàn giao khám sang điều trị (`ClinicalHandoff`): bỏ `source`.
- `ExaminationDto.confirmed` và `VisitResult.status` giữ nguyên tên; `status` có thêm giá trị `ExaminationConfirmed`.
- Mọi `expectedVersion` / `expectedVisitVersion` giữ nguyên.

## Migration

`20261117154529_SimplifyVisitTable`: chuyển `Status` sang `ExaminationConfirmed` cho lượt `InProgress` có phiếu đã xác nhận; gỡ 3 trường suy ra khỏi `ExaminationContentJson`; đổi tên hai cột cập nhật; xóa 16 cột; cập nhật `CK_VISIT_Status`; bỏ `CK_VISIT_Arrival`. `Down` thêm lại 5 cột thuộc chuỗi migration (`ExaminationConfirmed`, `RecordedAt`, `RequiresClinicalSourceReview`, `SourceJson`, `StorageStamp`) với kiểu và ràng buộc cũ, khôi phục `ExaminationConfirmed` từ `Status`, `RecordedAt` từ `ArrivalAt` và ba trường chẩn đoán suy ra; không khôi phục nguồn tiếp nhận và không tạo lại 11 cột ngoài chuỗi migration.
