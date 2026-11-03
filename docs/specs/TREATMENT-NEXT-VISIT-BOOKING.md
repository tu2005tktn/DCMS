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

## Ngày dự kiến là mục tiêu, khoảng cho phép lấy từ kế hoạch (thay đổi 03/11/2026, lần 2)
Ngày hẹn phải bám ngày dự kiến trong kế hoạch. Không thêm bảng và không migration.

**Khoảng cho phép của hạng mục: tối đa 4 ngày mỗi phía** (`TreatmentItemRules.BookingToleranceDays = 4`, dùng chung cho backend và giao diện). Ngày hẹn chỉ được lệch tối đa 4 ngày sớm hơn hoặc 4 ngày trễ hơn ngày dự kiến; ngoài khoảng này **không đặt được** (cập nhật 03/11/2026, lần 3, thay cho cơ chế "ngoài khoảng thì ghi lý do").
1. `targetOn` = `ReturnTargetOn` nếu hạng mục có đủ `ReturnEarliestOn`, `ReturnTargetOn`, `ReturnLatestOn`, nếu không thì `EstimatedStart`. Hạng mục không có ngày nào thì **không có khoảng cho phép** (mọi ngày trong khoảng chỉ định đều hợp lệ, không hiện độ lệch).
2. Khoảng của kế hoạch: `[targetOn − 4, targetOn + 4]`. Nếu có bộ ba `Return*` thì thu hẹp theo đó (`max(ReturnEarliestOn, targetOn − 4)` đến `min(ReturnLatestOn, targetOn + 4)`), không bao giờ rộng hơn ±4 ngày.
3. **Khoảng đặt được** (`TreatmentItemRules.BookingWindow(item, hôm nay giờ Việt Nam)`): cắt phần đã qua. `from = max(khoảng.from, hôm nay)`. Nếu cả khoảng đã qua (`khoảng.to < hôm nay`, kế hoạch quá hạn hơn 4 ngày) thì coi như hạng mục được dời về hôm nay: `[hôm nay, hôm nay + 4]`. `target` hiển thị = `targetOn` kẹp vào `[from, to]`.

`GET /api/patients/{id}/next-visit-candidates` trả `targetOn` (ngày dự kiến của kế hoạch, dùng tính độ lệch) và `windowFrom`, `windowTo` (khoảng đặt được ở mục 3).

**Chọn khung giờ khi kế hoạch đã có ngày dự kiến.** Giao diện chỉ xét các ngày trong `[windowFrom, windowTo]` (tối đa 9 ngày, mỗi ngày một lần gọi `GET /api/availability` cho đúng bác sĩ; không quét ra ngoài khoảng). Các ngày còn khung giờ hiện thành nút chọn ngày, **sắp từ gần ngày dự kiến nhất ra xa** (cùng khoảng cách thì ngày sớm trước); ngày gần nhất được chọn sẵn và hiện các khung giờ của ngày đó theo thứ tự giờ. Mỗi nút ngày và dòng "Độ lệch so với ngày dự kiến" ghi: "Đúng ngày dự kiến", "Sớm N ngày", "Trễ N ngày" (N từ 1 đến 4). Ô chọn ngày chỉ nhận ngày trong `[windowFrom, windowTo]`. Nếu bác sĩ không có khung giờ nào trong khoảng thì hiện thông báo: bác sĩ chưa có lịch làm còn trống trong khoảng `windowFrom – windowTo`; cần liên hệ quản lý lịch làm việc hoặc đổi ngày dự kiến của hạng mục trong kế hoạch. Hạng mục không có ngày dự kiến giữ cách cũ: quét từ hôm nay tiến tới 30 ngày, lấy tối đa 5 ngày gần nhất.

**Máy chủ kiểm tra lại.** `FollowUpAppointmentPort.ValidateAsync`: với chỉ định của hạng mục (không gắn bước) có khoảng đặt được mà ngày hẹn (giờ Việt Nam) nằm ngoài `[windowFrom, windowTo]` thì từ chối `409 MSG258`. Không còn lý do ngoại lệ để vượt khoảng này; ngoại lệ "bắt đầu trong 60 phút" của lịch hẹn giữ nguyên. Đổi lịch (`ValidateRescheduleAsync`) giữ cách kiểm tra theo khoảng của chỉ định, không đổi trong lần này.

Khoảng của chỉ định (`FollowUpInstruction.EarliestOn..LatestOn`) cũng là rào chắn ở máy chủ. Khi hộp thoại tạo chỉ định mới: `earliestOn = windowFrom`, `targetOn = target`, `latestOn = windowTo`; hạng mục không có ngày dự kiến: `earliestOn = hôm nay`, `targetOn = hôm nay`, `latestOn = hôm nay + 30 ngày`. Hạng mục đã có chỉ định cũ (khoảng rộng hơn) dùng giao của khoảng đặt được và khoảng chỉ định.

### Cập nhật ngày dự kiến theo lịch hẹn (có xác nhận)
Sau khi đặt thành công, nếu ngày hẹn khác ngày dự kiến thì hộp thoại hỏi bác sĩ có cập nhật ngày dự kiến không (mặc định có, không tự đổi âm thầm).
- `GET /api/dossiers/{dossierId}/plan/items/{itemId}/planned-date-preview?plannedOn=YYYY-MM-DD` (policy `DentistPolicy`, nha sĩ của chi nhánh) trả `{ dossierVersion, items: [{ itemId, serviceName, region, currentPlannedOn, proposedPlannedOn, isBookedItem }] }`. Dòng đầu là chính hạng mục vừa hẹn. Rỗng khi không có gì thay đổi (ngày hẹn bằng `targetOn` của hạng mục).
- Quy tắc đề xuất: `shift = plannedOn − targetOn của hạng mục` (ngày). Hạng mục vừa hẹn đổi sang `plannedOn`. Chỉ khi `shift > 0` (dời trễ) mới đề xuất dời các **hạng mục đứng sau** trong kế hoạch (`PlannedOrder` lớn hơn, cùng phiếu, hiệu lực, chưa thực hiện, chưa xóa/ẩn, có ngày dự kiến, và chưa có lịch hẹn hoạt động) cùng `shift` ngày để giữ khoảng cách. Hẹn sớm hơn không kéo các hạng mục sau lên sớm.
- `POST /api/dossiers/{dossierId}/plan/planned-dates` thân `{ expectedVersion, changes: [{ itemId, plannedOn }], requestKey }` (tối đa 50 dòng), trả `TreatmentChange`. Trong một giao dịch: kiểm `dossier.Version`, phiếu `Open`, mỗi `itemId` thuộc phiếu và chưa hoàn thành; đặt `EstimatedStart = plannedOn`; nếu hạng mục có bộ ba `Return*` thì dời cả ba cùng số ngày chênh (`plannedOn − targetOn cũ`), nếu có `EstimatedEnd` thì dời cùng số ngày; tăng `dossier.Version`; ghi sự kiện `ItemPlannedDatesChanged` (trước/sau); idempotent theo `requestKey`. Không đổi phiên bản kế hoạch (`PlanVersion`) vì chỉ chỉnh lịch dự kiến, giống `schedule-note`. Phiên bản lệch → 409 MSG111.
- Giao diện: bước 2 của hộp thoại liệt kê hạng mục, ngày hiện tại, ngày đề xuất, từng dòng có ô chọn (mặc định chọn hết); nút **Cập nhật ngày dự kiến** và **Giữ nguyên**. Đóng hộp thoại ở bước này tương đương Giữ nguyên. Lỗi khi cập nhật hiện thông báo nhưng lịch hẹn đã đặt vẫn giữ.

## Vòng đời
- Hủy hoặc vắng mặt: chỉ định quay về `NeedsBooking`, hạng mục lại xuất hiện là cần hẹn; **ngày dự kiến của hạng mục giữ nguyên** (chỉ bước cập nhật có xác nhận ở trên mới đổi ngày dự kiến). Nếu trước đó bác sĩ đã dời ngày dự kiến theo lịch hẹn thì ngày mới được giữ, không tự hoàn về ngày cũ.
- Đổi lịch giữ cùng chỉ định (kiểm lại khoảng ngày và nha sĩ).
- Khi bác sĩ thực hiện hạng mục (có hoạt động `Posted` gắn `ItemId`), hạng mục không còn là cần hẹn.
