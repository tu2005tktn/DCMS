# Rút gọn bảng `APPOINTMENT` (18/11/2026)

Yêu cầu người dùng ngày 18/11/2026: bỏ các cột thừa của `APPOINTMENT` (tồn dư từ table-splitting `RESOURCE_RESERVATION` và các cột không hiển thị/không dùng trên giao diện). Phương án đã chốt là **Phương án B**: giữ các cột khóa, tìm kiếm, lập lịch và trạng thái ở top-level; gom 5 trường khai báo đặt lịch phụ vào cột JSONB `BookingInfo`; dùng `Version` làm khóa đồng thời duy nhất. Bảng giảm từ **40 xuống 22 cột**. Không thêm bảng.

## Cột sau khi rút gọn (22 cột)

| Nhóm | Cột | Kiểu / Ràng buộc | Ghi chú |
| --- | --- | --- | --- |
| Khóa & Mã | `Id` | `uuid`, PK, `NOT NULL` | Hợp nhất `Id` và `AppointmentId` |
| | `Code` | `varchar(40)`, `UNIQUE`, `NOT NULL` | Mã lịch hẹn (`AP-...`) |
| Tài nguyên & Lịch | `BranchId` | `int`, FK `SCHEDULING_BRANCH(Id)`, `NOT NULL` | |
| | `DentistId` | `int`, FK `USER_ACCOUNT(EmployeeId)`, `NOT NULL` | |
| | `ChairId` | `int`, `NOT NULL` | |
| | `ServiceId` | `int?`, FK `SCHEDULING_SERVICE(Id, BranchId)` | |
| | `StartAt`, `EndAt` | `timestamptz`, `NOT NULL` | `CK_APPOINTMENT_RANGE`: `"StartAt" < "EndAt"` |
| Liên kết hồ sơ | `PatientId` | `int?`, FK `PATIENT_PROFILE(Id)` | |
| | `BookerAccountId` | `int?`, FK `USER_ACCOUNT(Id)` | |
| | `FamilyMemberId` | `uuid?`, FK `CUSTOMER_FAMILY_MEMBER(Id)` | |
| | `FollowUpId` | `uuid?` | Unique filter khi `Status IN ('Confirmed','CheckedIn')` |
| Tìm kiếm & Phân loại | `AttendeeName` | `varchar(100)`, `NOT NULL` | Giữ top-level phục vụ tìm kiếm `ILike` tại Tiếp nhận |
| | `Kind` | `varchar(16)`, `NOT NULL` | `Standard`, `WalkIn`, `FollowUp` |
| | `Source` | `varchar(24)`, `NOT NULL` | `Phone`, `Zalo`, `Facebook`, `InPerson`, `App` |
| | `Reason` | `varchar(1000)?` | Lý do khám |
| Trạng thái & Đồng thời | `Status` | `varchar(24)`, `NOT NULL` | `CK_APPOINTMENT_Status`: `Confirmed`, `CheckedIn`, `InProgress`, `Completed`, `Cancelled`, `NoShow` |
| | `CreatedAt` | `timestamptz`, `NOT NULL` | |
| | `Version` | `int`, `NOT NULL` | Khóa đồng thời duy nhất |
| JSONB | `BookingInfo` | `jsonb`, `NOT NULL` | `BookerName`, `BookerContact`, `AttendeeBirthDate`, `AttendeeRelation`, `ExceptionReason` |
| | `SupportRequests` | `jsonb`, `NOT NULL` | Yêu cầu hỗ trợ đổi/hủy dưới 12h |
| | `Reminders` | `jsonb`, `NOT NULL` | Lịch nhắc hẹn |

## Cấu trúc `BookingInfo` (JSONB)

```json
{
  "BookerName": "string",
  "BookerContact": "string | null",
  "AttendeeBirthDate": "YYYY-MM-DD | null",
  "AttendeeRelation": "Self | Relative",
  "ExceptionReason": "string | null"
}
```

`BookerContact` hợp nhất SĐT hoặc email người đặt thành một trường duy nhất; API đọc vẫn suy ra `BookerPhone` / `BookerEmail` (hoặc bản che `BookerPhoneMasked` / `BookerEmailMasked`) dựa trên việc chuỗi có chứa `@` hay không.

## Cột bị xóa (19 cột) và cách thay

| Nhóm cột bị xóa | Cách thay |
| --- | --- |
| `AppointmentId`, `ReservationCode`, `ReservationStatus`, `AttendeeKey`, `AttendeeLabel` | Bỏ entity `ResourceReservation` và cơ chế table-splitting; dùng trực tiếp `Appointment` (`Id`, `Code`, `Status`, `PatientId`, `AttendeeName`) để kiểm tra chiếm chỗ bác sĩ, ghế và bệnh nhân. 5 khóa ngoại từ `VISIT`, `IN_APP_NOTIFICATION` (2 FK), `AUDIT_LOG`, `COMMAND_RECEIPT` trỏ thẳng về `APPOINTMENT("Id")`. |
| `StorageStamp` | `Version` trở thành khóa đồng thời duy nhất của `Appointment`. |
| `CancelledAt`, `CancelledBy`, `CancelReason` | Thông tin người hủy, thời điểm hủy và lý do hủy nằm trong `AUDIT_LOG` (`AppointmentChange`, `Action = 'CANCEL'`). |
| `AssistantId` | Bỏ lưu trợ lý trên dòng lịch hẹn (mọi form đặt lịch hẹn không cho chọn trợ lý); giữ nguyên `AssistantId` trong các truy vấn lịch làm việc (`AvailabilityQuery`, `AvailabilitySlot`, `ResourceCheck`) và phân hệ điều trị. |
| `ConfirmedToAttend` | Bỏ cột trạng thái xác nhận tham dự và endpoint `PATCH /api/appointments/{id}/attendance-confirmation`; bỏ phần xử lý `ConfirmToAttend` / `WithdrawAttend` khỏi hộp thư yêu cầu hỗ trợ. |
| `PatientConsentConfirmedAt`, `PatientConsentConfirmedBy` | API tạo lịch hẹn từ kế hoạch điều trị (`POST /api/appointments`) tiếp tục bắt buộc `patientConsentConfirmed = true`, không cần lưu thêm 2 cột dấu vết riêng trên `APPOINTMENT`. |
| `BookerName`, `BookerPhone`, `BookerEmail`, `AttendeeBirthDate`, `AttendeeRelation`, `ExceptionReason` | Chuyển vào `BookingInfo` JSONB (trong đó `BookerPhone` và `BookerEmail` gộp thành `BookerContact`). |

## Hợp đồng API

- `ScheduleAppointment` (`/api/working-schedules/appointments`): bỏ `patientConsentConfirmedAt`.
- `SupportAppointmentSummary` (`/api/support-requests/*`): bỏ `confirmedToAttend`.
- `CreateAppointmentRequest`, `MyAppointmentDetail`, `ReceptionAppointment`, `VisitResult`: bỏ `assistantId`.
- Bỏ endpoint `PATCH /api/appointments/{id:guid}/attendance-confirmation` và quy tắc quyền tương ứng.
- `StaffAppointmentDetail` và `ReceptionAppointment` giữ nguyên các trường `bookerName`, `bookerPhone` / `bookerPhoneMasked`, `bookerEmail` / `bookerEmailMasked`, `attendeeBirthDate`, `attendeeRelation`, `exceptionReason` được đọc từ `BookingInfo`.

## Migration

`SimplifyAppointmentTable`:
- `Up`: gỡ 5 FK trỏ vào `APPOINTMENT("AppointmentId")`, xóa dòng giữ chỗ mồ côi nếu có (`"Code" IS NULL`), chuẩn hóa `"Id" = "AppointmentId"`, bổ sung audit hủy còn thiếu nếu có, thêm và điền `"BookingInfo"` JSONB từ 6 cột cũ, siết `NOT NULL` cho các cột bắt buộc, gỡ `AK_APPOINTMENT_AppointmentId`, các FK/index cũ và 19 cột dư, thêm `CK_APPOINTMENT_RANGE` (`"StartAt" < "EndAt"`) và `CK_APPOINTMENT_Status`, và tạo lại 5 FK trỏ về `APPOINTMENT("Id")`.
- `Down`: khôi phục đầy đủ 19 cột (suy xuất lại từ `BookingInfo`, `Id`/`Code`/`Status`/`PatientId`/`AttendeeName` và `AUDIT_LOG` cho thông tin hủy), khôi phục `AK_APPOINTMENT_AppointmentId`, các FK, index và ràng buộc cũ trước khi gỡ `BookingInfo`.
