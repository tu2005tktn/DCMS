# Đặt buổi hẹn kế tiếp theo kế hoạch điều trị — 03/11/2026

Tab **Lịch hẹn** của hồ sơ bệnh nhân lấy hạng mục trong **KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ** có ngày dự kiến gần nhất mà chưa thực hiện để bác sĩ đặt buổi hẹn điều trị tiếp theo. Lịch hẹn phải nằm trong thời gian bác sĩ phụ trách có lịch làm. Giao diện ở [PATIENT-APPOINTMENTS-TAB.md](PATIENT-APPOINTMENTS-TAB.md).

## Vì sao đi qua chỉ định hẹn tái khám
`POST /api/appointments` chỉ nhận dịch vụ **Khám** khi `kind ≠ FollowUp`; dịch vụ điều trị bắt buộc `kind = "FollowUp"` kèm `followUpId` (`AppointmentService`). Vì vậy mỗi hạng mục được hẹn có một `FollowUpInstruction` (đã có trong [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md)) với vòng đời `NeedsBooking → Booked → Completed`. Tiếp tục dùng endpoint và bảng lịch hẹn hiện có; trường xác nhận đồng ý và hai cột bổ sung ngày 07/11/2026 được mô tả bên dưới.

### Xác nhận bệnh nhân đồng ý và lịch bác sĩ — bổ sung 07/11/2026

- Drawer đặt lịch theo kế hoạch có checkbox **Đã có sự đồng ý của bệnh nhân** ở trước bước xác nhận, mặc định bỏ chọn mỗi lần mở/hạng mục mới. Chưa tích thì nút Xác nhận đặt lịch bị khóa. Lỗi mạng/validation giữ checkbox và bản nhập để thử lại.
- `POST /api/appointments` bổ sung trường tùy chọn `patientConsentConfirmed: boolean = false`. Mọi lịch mới có chỉ định nguồn dossier/item (kể cả nguồn bước) phải gửi `true`; server xác định nguồn từ chỉ định, không dựa vào tên dịch vụ hoặc nội dung lý do. Thiếu/false trả HTTP 400 với mã `APPOINTMENT_PATIENT_CONSENT_REQUIRED`, field `patientConsentConfirmed`, không ghi lịch/đặt chỗ/receipt/outbox.
- Khi lưu lịch hợp lệ, server ghi `Appointment.PatientConsentConfirmedAt` (UTC) và `PatientConsentConfirmedBy` (account ID) từ phiên hiện hành. Đây là xác nhận của người đặt rằng bệnh nhân đồng ý đặt lịch, không thay thế đồng thuận điều trị/chữ ký hoặc check-in/ConfirmedToAttend.
- Thêm migration cộng hai cột nullable vào `APPOINTMENT`; dữ liệu cũ giữ null, không tự xác nhận thay bệnh nhân. Đặt khám thường và chỉ định không có nguồn hạng mục giữ hợp đồng hiện hành. Việc đổi lịch giữ quy trình hiện có; thông tin này thể hiện lần xác nhận khi tạo lịch.
- Fingerprint idempotency phải phân biệt khi tích/bỏ đồng ý, đồng thời giữ tương thích receipt của request cũ không có trường mới. Replay lịch đã tạo không ghi lại thời điểm/tài khoản đồng ý hoặc tạo lịch thứ hai.
- Trang **Lịch làm việc** hiển thị thẻ lịch hẹn theo tuần cho bác sĩ được chọn: giờ bắt đầu/kết thúc UTC+7, chi nhánh, bệnh nhân, dịch vụ, trạng thái lịch và trạng thái ghi nhận đồng ý. Có đổi tuần, tải lại, trạng thái tải/trống/lỗi và cập nhật khi quay lại cửa sổ. Lịch không sửa các khoảng làm việc hoặc giữ chỗ thêm.
- Lịch theo kế hoạch cũ chưa có metadata hiển thị **Chưa ghi nhận**, lịch có metadata hiển thị **Đã đồng ý**, lịch khác hiển thị dấu gạch. Chỉ có link chi tiết lịch khi được cấp `appointments.read`.
- API danh sách và quyền truy cập được mô tả tại `S09-UC03.md`; tiếp tục giữ phần sửa kiểm trạng thái hạng mục, không thêm lại ràng buộc đặt khác dịch vụ/hạng mục đã được người dùng loại khỏi phạm vi.
- Kiểm chứng: chưa tích không gửi lệnh, checkbox gửi đúng/reset/giữ khi lỗi; API thiếu đồng ý không ghi dở, ghi đúng actor/time, replay; danh sách bác sĩ có lịch vừa tạo/đổi/hủy, giới hạn vai trò/nhân viên/chi nhánh, metadata cũ null. Kiểm tra migration và model snapshot trên schema cách ly.

## Hạng mục kế tiếp
Trong các phiếu không bị xóa của bệnh nhân thuộc chi nhánh nha sĩ được phép, một hạng mục là **cần hẹn** khi thỏa tất cả:
- phiếu `Open`, kế hoạch `Committed`; hạng mục **hiệu lực**: `Decision = Agreed` hoặc `EntrySource = ClinicianEntry` (hạng mục nhập trực tiếp trên phiếu), không `SheetDeleted`/`HiddenFromSheet`;
- **chưa thực hiện**: `DisplayCompleted = false` và chưa có hoạt động `Posted` (chưa bị xóa khỏi phiếu) gắn `ItemId` đó;
- chưa có chỉ định `Booked` còn lịch hoạt động (hạng mục đã hẹn được trả về với trạng thái `Booked` để hiển thị, không tính là cần hẹn).

Sắp xếp: `EstimatedStart` tăng dần, hạng mục không có ngày xếp cuối, rồi `PlannedOrder`. Phần tử đầu tiên chưa `Booked` là **hạng mục kế tiếp**.
- **Bác sĩ phụ trách** = `ResponsibleDentistId` của hạng mục, nếu trống thì nha sĩ của phiếu.
- **Thời lượng mặc định** = `EstimatedMinutes` (mặc định 30) làm tròn lên bội số 15 phút, trong 15–480. Khi đặt hẹn cấp hạng mục, nhân viên được sửa thời lượng riêng của lần hẹn theo hợp đồng bên dưới.
- **Ngày muốn hẹn** = `max(EstimatedStart, hôm nay giờ Việt Nam)`.

## API (policy `TreatmentDentist`, người dùng phải là nha sĩ của chi nhánh)
`GET /api/patients/{patientId}/next-visit-candidates` → `{ items: [{ dossierId, dossierCode, itemId, serviceId, serviceName, region, estimatedStart, durationMinutes, dentistId, dentistName, branchId, status: "Ready"|"NeedsBooking"|"Booked", followUpId, appointment: { id, code, startAt } | null }] }` đã sắp xếp theo quy tắc trên.
- `Ready`: chưa có chỉ định; `NeedsBooking`: đã có chỉ định chưa gắn lịch (dùng lại, không tạo thêm); `Booked`: đã có lịch hoạt động.
- Bệnh nhân không có phiếu hoặc không thuộc phạm vi → `items` rỗng; bệnh nhân không tồn tại → 404/MSG113; không phải nha sĩ của chi nhánh → 403/MSG06.

Đặt hẹn dùng các endpoint hiện có, theo thứ tự:
1. Trước khi tạo, đọc lại `next-visit-candidates` để dùng chỉ định đã có của hạng mục (một lần đặt trước đó có thể đã tạo chỉ định nhưng thất bại ở bước đặt lịch; tạo thêm sẽ bị MSG233). Nếu hạng mục vẫn `Ready`: `POST /api/follow-up-instructions` với nguồn `{ dossierId, itemId }`, `purpose = TreatmentContinuation`, `earliestOn = hôm nay`, `targetOn = ngày muốn hẹn`, `latestOn = targetOn + 30 ngày`, `durationMinutes`, `dentistId`.
2. `GET /api/availability` cho đúng bác sĩ, dịch vụ, thời lượng để lấy khung giờ **nằm trong lịch làm của bác sĩ**.
3. `POST /api/appointments` với `kind = "FollowUp"`, `followUpId`, `serviceId`, `dentistId`, khung giờ đã chọn và `patientConsentConfirmed = true` sau khi đã ghi nhận bệnh nhân đồng ý đặt lịch.

## Quyền đặt lịch (thay đổi 03/11/2026)
Trước đây `POST /api/appointments` từ chối mọi tài khoản staff không có vai trò `ClinicAssistant` (403/MSG06), nên nha sĩ không đặt được buổi hẹn điều trị của chính mình. Nay **nha sĩ (vai trò hiệu lực `Dentist`) được tạo lịch khi và chỉ khi request có `followUpId`** (loại `FollowUp`); đặt lịch thường (Standard/WalkIn) vẫn chỉ dành cho `ClinicAssistant`. Các kiểm tra khác giữ nguyên: nha sĩ phải là thành viên hoạt động của chi nhánh, chỉ định phải đúng bệnh nhân/chi nhánh/khoảng ngày/thời lượng/nha sĩ (`FollowUpAppointmentPort.ValidateAsync`) và khung giờ phải nằm trong lịch làm của bác sĩ.

### Sửa thời lượng khi đặt hẹn cấp hạng mục — 09/11/2026

- Trong **Đặt lịch theo kế hoạch điều trị**, trường **Thời lượng (phút)** cho nhập số nguyên từ 1 đến 480, mặc định từ candidate. Đóng/mở lại hoặc chuyển hạng mục khôi phục mặc định. Giá trị rỗng/không hợp lệ hiện lỗi, không tải khung giờ hay xác nhận đặt lịch.
- Đổi thời lượng hủy lựa chọn giờ và kết quả tìm cũ, rồi tìm lại các ngày/khung giờ trong đúng khoảng cho phép bằng thời lượng mới. Kết quả request cũ không được ghi đè kết quả mới; đang tải chưa được xác nhận. Thông tin liên hệ và xác nhận đồng ý đã nhập được giữ.
- `GET /api/availability` và `POST /api/appointments.durationMinutes` cùng dùng thời lượng đã sửa. Chỉ định mới vẫn giữ thời lượng gợi ý mặc định của candidate; chỉ định đã có được dùng lại. Không sửa `TreatmentItem.EstimatedMinutes`, thời lượng chỉ định hay danh mục dịch vụ khi đặt lịch. Appointment/reservation lưu chính xác khoảng bắt đầu–kết thúc theo thời lượng lần hẹn.
- Server chỉ cho phép lệch thời lượng chỉ định khi người đặt là nhân viên và chỉ định có nguồn dossier + item, không có StepId. Cờ cho phép được xác định từ phiên tại Application/Infrastructure, không nhận từ JSON của client. Chỉ định cấp bước hoặc nguồn khác và khách hàng Public vẫn kiểm thời lượng bằng chỉ định. Các kiểm tra quyền động, bác sĩ/chi nhánh, ngày cho phép, trạng thái hạng mục, đồng ý và xung đột tài nguyên tiếp tục áp dụng trong giao dịch.
- Không thêm endpoint, trường JSON, bảng hoặc migration. Thời lượng đã nằm trong fingerprint đặt lịch: retry nguyên lệnh trả receipt cũ; đổi số phút phải dùng requestKey mới. Web giữ key khi retry lỗi mạng cùng nội dung, đổi thời lượng tạo key đặt lịch mới.
- Kiểm chứng: browser đổi thời lượng và chọn lại giờ, bỏ kết quả cũ đến chậm, chặn giá trị rỗng, tái dùng chỉ định cũ; API lưu thời lượng mới nhưng không đổi kế hoạch/chỉ định, replay và giới hạn 1–480, tiếp tục khóa thời lượng chỉ định cấp bước/Public.

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

## Kiểm tra lại khi ghi lịch — sửa lỗi 07/11/2026

- Với chỉ định cấp hạng mục (không có `StepId`), tạo hoặc đổi lịch phải đọc lại hạng mục trong giao dịch lịch hẹn: phiếu `Open`, kế hoạch `Committed`, đúng phiên bản, hạng mục hiệu lực, chưa xóa/ẩn, chưa đánh dấu hoàn tất, `Progress != Completed`, chưa có hoạt động `Posted` còn hiệu lực gắn hạng mục. Dùng cùng tiêu chí với danh sách `next-visit-candidates`; hộp đặt lịch mở từ trước không được vượt kiểm tra này.
- Hoạt động bị xóa khỏi phiếu qua sự kiện `SheetActivityDeleted` không còn là chứng cứ đã thực hiện. Khi đọc sự kiện chỉnh/xóa phải dùng snapshot cuối theo phiên bản phiếu, giống lịch sử hoạt động hiện có.
- Phạm vi thu hẹp theo yêu cầu người dùng: không bổ sung ràng buộc dịch vụ/hạng mục khác trong lần sửa này; giữ hợp đồng lựa chọn dịch vụ và các port lịch hẹn hiện có.
- Sai trạng thái hạng mục trả `409 MSG230`. Giao dịch thất bại không tạo/cập nhật lịch, đặt chỗ, receipt, outbox hoặc chuyển chỉ định sang `Booked`. Người dùng tải lại kế hoạch/hạng mục trước khi đặt tiếp. Hủy lịch vẫn theo hợp đồng hiện có để giải phóng lịch không còn phù hợp.
- Chỉ định theo lượt khám/hoạt động không có nguồn hạng mục giữ hành vi hiện có. Chỉ định cấp bước tiếp tục kiểm tiền nhiệm, kết quả labo và trạng thái từng bước; hoạt động đã làm ở bước trước không chặn các bước tiếp theo của cùng hạng mục.
- Retry cùng request key của lịch đã tạo tiếp tục trả receipt đã lưu; không tạo lịch lần hai hoặc kiểm lại thành một lệnh tạo mới. Riêng bản sửa kiểm trạng thái không đổi hợp đồng hoặc dữ liệu; bổ sung đồng ý đặt lịch dùng trường tùy chọn và migration nullable ở phần đầu tài liệu.
- Kiểm thử bắt buộc trên schema cách ly: hoàn tất/xóa/ẩn hạng mục sau khi có chỉ định; hạng mục đã có hoạt động Posted; hoạt động đã xóa khỏi phiếu; đặt lịch, đổi lịch và retry. Kiểm tra trạng thái chỉ định và không có ghi dở khi từ chối.
