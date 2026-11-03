# Đặt buổi hẹn kế tiếp theo kế hoạch điều trị — 03/11/2026

Tab **Lịch hẹn** của hồ sơ bệnh nhân lấy hạng mục trong **KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ** có ngày dự kiến gần nhất mà chưa thực hiện để bác sĩ đặt buổi hẹn điều trị tiếp theo. Lịch hẹn phải nằm trong thời gian bác sĩ phụ trách có lịch làm. Giao diện ở [PATIENT-APPOINTMENTS-TAB.md](PATIENT-APPOINTMENTS-TAB.md).

## Vì sao đi qua chỉ định hẹn tái khám
`POST /api/appointments` chỉ nhận dịch vụ **Khám** khi `kind ≠ FollowUp`; dịch vụ điều trị bắt buộc `kind = "FollowUp"` kèm `followUpId` (`AppointmentService`). Vì vậy mỗi hạng mục được hẹn có một `FollowUpInstruction` (đã có trong [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md)) với vòng đời `NeedsBooking → Booked → Completed`. **Không đổi hợp đồng `POST /api/appointments`, không thêm bảng.**

## Hạng mục kế tiếp
Trong các phiếu không bị xóa của bệnh nhân thuộc chi nhánh nha sĩ được phép, một hạng mục là **cần hẹn** khi thỏa tất cả:
- phiếu `Open`, kế hoạch `Committed`; hạng mục **hiệu lực**: `Decision = Agreed` hoặc `EntrySource = ClinicianEntry` (hạng mục nhập trực tiếp trên phiếu), không `SheetDeleted`/`HiddenFromSheet`;
- **chưa thực hiện**: `DisplayCompleted = false` và chưa có hoạt động `Posted` (chưa bị xóa khỏi phiếu) gắn `ItemId` đó;
- chưa có chỉ định `Booked` còn lịch hoạt động (hạng mục đã hẹn được trả về với trạng thái `Booked` để hiển thị, không tính là cần hẹn).

Sắp xếp: `EstimatedStart` tăng dần, hạng mục không có ngày xếp cuối, rồi `PlannedOrder`. Phần tử đầu tiên chưa `Booked` là **hạng mục kế tiếp**.
- **Bác sĩ phụ trách** = `ResponsibleDentistId` của hạng mục, nếu trống thì nha sĩ của phiếu.
- **Thời lượng** = `EstimatedMinutes` (mặc định 30) làm tròn lên bội số 15 phút, trong 15–480.
- **Ngày muốn hẹn** = `max(EstimatedStart, hôm nay giờ Việt Nam)`.

## API (policy `TreatmentDentist`, người dùng phải là nha sĩ của chi nhánh)
`GET /api/patients/{patientId}/next-visit-candidates` → `{ items: [{ dossierId, dossierCode, itemId, serviceId, serviceName, region, estimatedStart, durationMinutes, dentistId, dentistName, branchId, status: "Ready"|"NeedsBooking"|"Booked", followUpId, appointment: { id, code, startAt } | null }] }` đã sắp xếp theo quy tắc trên.
- `Ready`: chưa có chỉ định; `NeedsBooking`: đã có chỉ định chưa gắn lịch (dùng lại, không tạo thêm); `Booked`: đã có lịch hoạt động.
- Bệnh nhân không có phiếu hoặc không thuộc phạm vi → `items` rỗng; bệnh nhân không tồn tại → 404/MSG113; không phải nha sĩ của chi nhánh → 403/MSG06.

Đặt hẹn dùng các endpoint hiện có, theo thứ tự:
1. Trước khi tạo, đọc lại `next-visit-candidates` để dùng chỉ định đã có của hạng mục (một lần đặt trước đó có thể đã tạo chỉ định nhưng thất bại ở bước đặt lịch; tạo thêm sẽ bị MSG233). Nếu hạng mục vẫn `Ready`: `POST /api/follow-up-instructions` với nguồn `{ dossierId, itemId }`, `purpose = TreatmentContinuation`, `earliestOn = hôm nay`, `targetOn = ngày muốn hẹn`, `latestOn = targetOn + 30 ngày`, `durationMinutes`, `dentistId`.
2. `GET /api/availability` cho đúng bác sĩ, dịch vụ, thời lượng để lấy khung giờ **nằm trong lịch làm của bác sĩ**.
3. `POST /api/appointments` với `kind = "FollowUp"`, `followUpId`, `serviceId`, `dentistId`, khung giờ đã chọn.

## Quyền đặt lịch (thay đổi 03/11/2026)
Trước đây `POST /api/appointments` từ chối mọi tài khoản staff không có vai trò `ClinicAssistant` (403/MSG06), nên nha sĩ không đặt được buổi hẹn điều trị của chính mình. Nay **nha sĩ (vai trò hiệu lực `Dentist`) được tạo lịch khi và chỉ khi request có `followUpId`** (loại `FollowUp`); đặt lịch thường (Standard/WalkIn) vẫn chỉ dành cho `ClinicAssistant`. Các kiểm tra khác giữ nguyên: nha sĩ phải là thành viên hoạt động của chi nhánh, chỉ định phải đúng bệnh nhân/chi nhánh/khoảng ngày/thời lượng/nha sĩ (`FollowUpAppointmentPort.ValidateAsync`) và khung giờ phải nằm trong lịch làm của bác sĩ.

## Điều kiện hạng mục hiệu lực (thay đổi)
`CreateFollowUpAsync` và `FollowUpAppointmentPort.CheckReadinessAsync` trước đây chỉ nhận hạng mục `Decision = Agreed`. Nay nhận cả hạng mục `EntrySource = ClinicianEntry` (đã được tính là hiệu lực trên phiếu và bảng giá); các điều kiện khác (phiếu `Open`, kế hoạch `Committed`, đúng `PlanVersion`) giữ nguyên.

## Lịch làm của bác sĩ
Khung giờ chỉ đến từ `GET /api/availability`, và `POST /api/appointments` kiểm lại trong giao dịch, nên không thể đặt ngoài lịch làm của bác sĩ. Khi ngày muốn hẹn không còn khung giờ, giao diện **tự chuyển sang ngày gần nhất có khung giờ** (quét tối đa 30 ngày, trong khoảng `latestOn` của chỉ định) mà không hiện thông báo gợi ý; ô ngày hiển thị ngày đã chọn. Chỉ khi không tìm được khung giờ nào mới hiện thông báo bác sĩ chưa có lịch làm còn trống.

## Vòng đời
- Hủy hoặc vắng mặt: chỉ định quay về `NeedsBooking`, hạng mục lại xuất hiện là cần hẹn.
- Đổi lịch giữ cùng chỉ định (kiểm lại khoảng ngày và nha sĩ).
- Khi bác sĩ thực hiện hạng mục (có hoạt động `Posted` gắn `ItemId`), hạng mục không còn là cần hẹn.
