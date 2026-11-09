# UC-06 — API đổi và hủy lịch hẹn

> Cập nhật nguồn 26/10/2026: [bộ đặc tả v2.5](README.md) là nguồn hiện hành. Nội dung và bằng chứng dưới đây giữ mốc triển khai ban đầu; chưa được nghiệm thu lại theo v2.5.
Theo hợp đồng S58/S21/S24 v2.3 ngày 24/10/2026, Appointment gắn `followUpId` chỉ chuyển instruction từ `NeedsBooking` sang `Booked` sau khi lịch và reservation commit. Reschedule giữ ID, kiểm lại cửa sổ, readiness, dentist, duration và toàn bộ nhân sự/ghế trong khoảng mới. Cancel/NoShow đưa instruction chưa hoàn tất về `NeedsBooking` trong cùng transaction; lỗi port làm rollback lịch. Không có slot không hủy instruction. Ba ghế là cấu hình chi nhánh thực tế, không là số hardcode trong engine; ghế và staff được giữ liên tục trong cả duration.

Ngày: 18/10/2026. Nguồn: UC-06/S12/S16 và GBR-012/013/014/015/025/031/075/076/081 trong bộ đặc tả v2.0; hợp đồng mobile tại `docs/implementation/UC05-06-S14-S16.md`.

## Hợp đồng và quyền

### Thao tác từ thẻ lịch S10 — 09/11/2026

Trên `/appointments`, bấm thẻ lịch mở drawer chi tiết tại chỗ có các thao tác dùng chung: chỉnh sửa, hủy, khách không tới và đặt lịch thay thế. Hiển thị theo grant/trạng thái/giờ, tải lại lưới và chi tiết sau commit, giữ nguyên bộ lọc/ngày. Mặc định tự chọn nha sĩ đang đăng nhập qua `options.currentDentistId` do server xác định; trợ lý không phải nha sĩ không bị tự chọn một bác sĩ khác. Chi tiết hợp đồng tại UC04-05-S10-S11, mục 4.

### Khách không tới và chèn lịch thay thế — 09/11/2026

- Dispatcher xử lý event NoShow bằng notification loại NoShow (không báo đặt lịch thành công), ngừng reminder của lịch cũ. Lịch thay thế có notification/nhắc hẹn riêng; không gửi thông tin khách mới cho người đặt lịch cũ.

- Lịch Confirmed đã tới giờ bắt đầu, chưa có Visit/check-in, có nút **Khách không tới** theo `appointments.no-show`. Có xác nhận và lý do bắt buộc. API `/api/appointments/{code}/no-show` kiểm lại grant/branch/phạm vi bác sĩ trong service, version, trạng thái và thời gian server. Nha sĩ chỉ đánh dấu lịch được phân công; Public không được gọi. Không có job tự đánh dấu và không áp dụng lịch đang tiếp nhận/khám.
- Commit giữ nguyên mã/Patient/thời gian lịch cũ, chuyển Appointment và reservation sang NoShow, tăng version, ghi history/receipt/outbox trong khóa 90403. Reservation NoShow không giữ tài nguyên. Chỉ định điều trị chưa hoàn tất trở về NeedsBooking.
- Lịch NoShow còn thời gian (`EndAt > now`) có **Đặt lịch thay thế** theo `appointments.create`. Dùng form đặt tại quầy, cố định chi nhánh/bác sĩ/ngày và loại WalkIn/InPerson theo khung nguồn. Không sao chép Patient, người đặt, liên hệ hoặc chỉ định điều trị cũ. Người dùng chọn/nhập khách khác; dịch vụ Khám theo contract đặt mới.
- Hộp thay thế tra cứu slot thực tế cho 1 phút để tìm lần bắt đầu còn hợp lệ gần nhất trong khoảng nguồn, rồi gợi ý thời lượng bằng min(thời lượng Khám, số phút còn lại từ slot đó tới EndAt). Người dùng có thể đổi số phút 1–480; chỉ hiển thị slot tương lai kết thúc không quá EndAt nguồn. Không đặt lùi giờ; dưới 60 phút vẫn cần lý do ngoại lệ hiện hành. Nếu hết giờ/không còn đủ slot thì hiển thị rõ, không tạo lịch giả hoặc tái sử dụng mã cũ.
- `POST /api/appointments` bổ sung tùy chọn `replacesAppointmentId: Guid?`. Chỉ Staff có appointments.create, phạm vi đọc lịch nguồn và membership phù hợp được dùng; cho phép nha sĩ đặt WalkIn thay thế lịch của mình dù đặt thường ngoài ngữ cảnh vẫn theo quy tắc cũ. Sau khóa giao dịch kiểm lại quyền và nguồn NoShow, không có Visit, còn giờ, đúng chi nhánh/bác sĩ, không kèm followUp/assistant, start > now và end <= nguồn.EndAt. Slot/schedule/version/tài nguyên vẫn kiểm bằng engine chung, không tin khung giờ client khai.
- Fingerprint chỉ thêm reference khi có trường để giữ retry cũ. Kiểm scope trước replay; retry cùng nội dung trả lịch đã commit dù khung nguồn đã hết giờ sau đó. Outbox của lịch mới lưu reference nguồn để truy vết, không đổi lịch/Patient cũ và không đưa thông tin khách mới vào history công khai của khách cũ. Không thêm bảng hoặc migration.
- Hai người chèn cùng giờ chỉ một lệnh giữ chỗ thành công; lỗi giữ NoShow và không ghi dở lịch mới. Hết giờ vẫn giữ lịch sử Khách không tới và có thể đặt lịch mới ở lịch chung. UI tải lại trạng thái sau đánh dấu/đặt thay, giữ form/key khi lỗi mạng, kiểm quyền riêng cho hai thao tác, hỗ trợ 390px.

### Phòng khám chủ động sửa/hủy — bổ sung 09/11/2026

- Nhân viên có `appointments.reschedule`/`appointments.cancel` chủ động chỉnh ngày/giờ, nha sĩ và ghế qua khung giờ hợp lệ, hoặc hủy lịch ngay từ tab Lịch hẹn của bệnh nhân và màn chi tiết lịch. Không cần có yêu cầu hỗ trợ của khách hàng trước đó. Thời lượng lưu trên lịch là thời lượng dùng để tìm và giữ tài nguyên khi đổi lịch.
- Dùng các command hiện có, bổ sung tùy chọn `clinicInitiated: boolean = false` cho reschedule/cancel. Chỉ Staff được gửi true. Với true, lý do thay đổi/hủy bắt buộc 1–500 ký tự; không bắt nhân viên khai việc xác minh một người yêu cầu không tồn tại. History ghi actor STAFF và ghi rõ phòng khám chủ động ở RequesterVerification, không giả lập sự đồng ý của bệnh nhân. Với false/thiếu trường giữ luồng xác minh người yêu cầu hiện hành và quy tắc Public 12 giờ.
- Server đọc lại quyền động từ DB ở service sau khóa giao dịch và trước replay, kiểm chi nhánh assignment/membership và cùng phạm vi đọc lịch. Nha sĩ chỉ thao tác lịch được phân công, chỉ chọn bác sĩ trong options của mình; trợ lý/nhân viên quản lý xem phạm vi rộng hơn theo quy tắc hiện có. Không cấp quyền chỉ vì hiện nút. Availability theo mã cũng kiểm cùng phạm vi, cho nhân viên được đọc lịch tra cứu slot.
- Chỉ lịch Confirmed chưa bắt đầu được sửa/hủy. CheckedIn/InProgress/Completed/Cancelled/NoShow và lịch đã tới giờ bị chặn; đổi lịch mới vẫn cần lead time 60 phút, bác sĩ/ghế/trợ lý liên tục và các kiểm chỉ định điều trị hiện có. Không đổi Patient, người đặt, chi nhánh, dịch vụ hoặc tạo lịch thay thế.
- Lịch cũ giữ nguyên tới khi commit thành công; hủy giải phóng reservation và đưa chỉ định chưa hoàn tất về NeedsBooking. History/receipt/outbox/version cùng transaction. Thêm dấu clinicInitiated vào fingerprint chỉ khi true để giữ replay của command cũ thiếu trường.
- Web: hai nút Chỉnh sửa lịch/Hủy lịch riêng theo grant và trạng thái, form hiển thị lịch hiện tại, ngày/bác sĩ/slot mới và lý do. Hủy có bước xác nhận rõ mã/người khám/giờ và lý do. Thành công tải lại danh sách, chi tiết và khối hạng mục cần hẹn. Không optimistic update lịch đã lưu. Lỗi mạng giữ form và key cùng nội dung; đổi nội dung dùng key mới. Stale cho tải lại phiên bản mà giữ bản nhập, bỏ slot để chọn lại; xung đột slot tải lại giờ trống. Đổi bộ lọc/đóng khi đang ghi được chặn.
- Không cần migration hoặc endpoint mới. Kiểm API HTTP và service trực tiếp: grant/branch/own doctor, Public không bật chế độ phòng khám, bỏ/thu hồi grant với token cũ, stale/replay/concurrent/conflict, trạng thái cấm, đổi/hủy theo kế hoạch và metadata/history/outbox. Browser kiểm thao tác từ hồ sơ và chi tiết, quyền đọc, form/retry/stale, hủy và cập nhật dữ liệu sau thành công.
- Form chỉnh sửa và xác nhận hủy nằm trong viewport 390px, không cắt nhãn/nút khi đổi kích thước. Kiểm cả bounds của dialog, không chỉ scrollWidth của trang.

Cập nhật 19/10/2026: [cấu hình slot và thời lượng](CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md) thay giới hạn thời lượng cũ bằng số nguyên 1–480 phút. Đổi lịch giữ thời lượng đã lưu và dùng bước slot hiện hành; nhận `bookingSettingsVersion` tùy chọn, token stale trả MSG111. Khám mới dùng catalog publicBookable; buổi tiếp tục điều trị phải giữ liên kết chỉ định/phác đồ khi module này được triển khai.

Các quy tắc nhân viên xác minh người yêu cầu ở hợp đồng ban đầu dưới đây áp dụng khi `clinicInitiated=false` hoặc thiếu trường. Chế độ chủ động của phòng khám ưu tiên theo mục bổ sung 09/11/2026 ở trên.

- `POST /api/appointments/{code}/reschedule`: `{expectedVersion, requestKey, dentistId, newStart, chairId, scheduleVersion, assistantScheduleVersion?, reason, requesterVerification?}` (dành cho tự phục vụ >= 12h hoặc Staff đã xác minh dưới 12h).
- `POST /api/appointments/{code}/cancel`: `{expectedVersion, requestKey, reason, requesterVerification?}`.
- `POST /api/appointments/{code}/support-requests`: `{requestKey, kind, reason, newStartAt?, dentistId?, chairId?}`. Dành cho khách hàng gửi yêu cầu hỗ trợ đổi (`Change`/`RESCHEDULE`) hoặc hủy (`Cancel`/`CANCEL`) lịch khi dưới 12 giờ. Tạo yêu cầu `Pending` có `ProposedStartAt` (đối với đổi lịch), không tự đổi/hủy trực tiếp; trả về HTTP 201 với `{request, notification: MSG33}`. `MSG33`: "Đã gửi yêu cầu hỗ trợ. Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công.".
- `POST /api/support-requests/{id}/withdraw`: `{expectedVersion}`. Người yêu cầu rút yêu cầu hỗ trợ đang `Pending` của mình.
- `GET /api/appointments/{code}/availability?date=yyyy-MM-dd&dentistId={id}`: HTTP 200 `AvailabilityResult`, kiểm quyền sở hữu/phạm vi, áp dụng cho lịch `Confirmed` chưa bắt đầu (cho phép tra cứu cả khi dưới 12 giờ để khách hàng chọn khung giờ đề xuất). Branch/service/assistant/duration/attendee/excluded reservation lấy từ lịch đã được kiểm quyền; `dentistId` phải là nha sĩ active tại cùng chi nhánh. Đây là tra cứu, không giữ slot. Web/mobile tái sử dụng `GET /api/availability/options` của màn đặt lịch để hiển thị các nha sĩ cùng chi nhánh, sau đó tải slot theo nha sĩ được chọn.
- Thành công HTTP 200: `{appointment: AppointmentDetail, notification: {code, message}}`; MSG35 có giờ mới theo UTC+7, MSG37 có mã lịch; yêu cầu hỗ trợ trả HTTP 201 kèm thông báo MSG33. UI Web và Mobile xử lý hiển thị banner thông báo thành công từ `notification.message`, reload dữ liệu lịch hẹn hiển thị thẻ yêu cầu hỗ trợ đang chờ duyệt, không hiển thị lỗi giả. Chi tiết trả lịch sử và danh sách supportRequests đã lưu; staff nhận `access: STAFF`.
- AppointmentDetail bổ sung `assistantId` (nullable) và `supportRequests` (danh sách yêu cầu hỗ trợ của lịch).
- Không đăng nhập: HTTP 401 MSG32; bearer không hợp lệ: HTTP 401 MSG07. Không tồn tại hoặc ngoài phạm vi: HTTP 404 MSG102, không tiết lộ mã lịch có tồn tại.
- Public account đang hoạt động được đổi/hủy lịch do mình đặt (kể cả cho người thân), hoặc lịch có PatientId thuộc liên kết tự thân VERIFIED hiện còn hiệu lực. Không suy ra quyền từ số điện thoại hoặc PatientId client gửi.
- Clinic Assistant đang hoạt động và là thành viên chi nhánh được dùng cùng command; bắt buộc `requesterVerification` 1–500 ký tự mô tả việc xác minh người yêu cầu. Các vai trò staff khác không được quyền đổi/hủy theo endpoint này.
- Public: Đổi/hủy trực tiếp chỉ áp dụng khi `Confirmed`, chưa tới giờ hẹn, còn ít nhất 12 giờ (đúng 12 giờ hợp lệ). Khi dưới 12 giờ (và chưa bắt đầu), khách hàng gửi yêu cầu hỗ trợ đổi lịch kèm khung giờ đề xuất để nhân viên phòng khám duyệt qua S12. Sai trạng thái/thời gian khi gọi reschedule trực tiếp: HTTP 409 MSG31.
- Lý do sau trim phải 1–500 ký tự: HTTP 400 MSG02/MSG03. RequestKey phải UUID khác rỗng, expectedVersion dương; các trường slot hợp lệ: HTTP 400 MSG04.

## Transaction và dữ liệu

Một transaction ReadCommitted dùng cùng advisory lock S09 (90403) với đặt lịch và công bố lịch làm việc. Đọc lại quyền, trạng thái, thời gian và version sau khi lấy lock. `TimeProvider` cấp thời gian kiểm tra; không dùng thời gian từ client.

- Đổi lịch giữ chi nhánh, trợ lý, dịch vụ, thời lượng và liên kết follow-up; người dùng được chọn một nha sĩ active khác trong cùng chi nhánh như luồng đặt lịch. Slot mới phải có lead time >=60 phút; kiểm tra nha sĩ và toàn bộ tài nguyên trên toàn thời lượng bằng `IAvailabilityService.CheckInTransactionAsync`, loại trừ đúng reservation hiện tại. Duration lấy từ bản ghi lịch (15–480 phút theo booking), không lấy từ client và không đổi theo duration catalog hiện tại. Lead time tự kiểm ở command; kiểm tra tài nguyên dùng duration gốc để hỗ trợ cả lịch do quầy đặt. GET slot theo code cũng dùng duration này và loại trừ reservation cũ. Khi commit, `Appointment` và `ResourceReservation` cùng cập nhật nha sĩ, ghế và khoảng giờ; event outbox phản ánh nha sĩ hiện tại. Lịch làm việc lệch version trả MSG111; nha sĩ/slot không hợp lệ hoặc bị chiếm trả HTTP 409 MSG34. Thất bại không nhả hoặc sửa slot cũ.
- Hủy đặt cả Appointment và ResourceReservation sang `Cancelled`, giữ khoảng cũ làm lịch sử, tăng version, lưu CancelledAt/By/Reason. Không xóa lịch.
- Mỗi thành công ghi một `APPOINTMENT_CHANGE` có actor SELF/STAFF, old/new interval và chair, reason, requesterVerification, requestKey, thời gian; tăng version một lần. Bảng dùng FK giữ lịch sử.
- Receipt dùng khóa ghép `(ActorAccountId, RequestKey)`; fingerprint gồm action, code và toàn bộ command chuẩn hóa. Cùng actor/key/nội dung trả nguyên kết quả đã lưu, không tăng version, ghi history hoặc event lần nữa. Key bị dùng lại với nội dung/action khác trả HTTP 409 MSG111. Quyền được kiểm tra lại trước replay, kể cả khi liên kết đã bị thu hồi.
- Cùng transaction ghi một `APPOINTMENT_OUTBOX` event `appointment.rescheduled` hoặc `appointment.cancelled`, gồm mã/ID, version, trạng thái, chi nhánh, giờ cũ/mới; không đưa lý do tự do, contact hoặc chẩn đoán vào payload. Dispatcher/reminder do TV5 cung cấp; commit không phụ thuộc gửi thông báo.
- Follow-up: đổi lịch giữ nguyên ID và kiểm lại cửa sổ/readiness; hủy/NoShow gọi port transactional `IFollowUpAppointmentChangePort.MarkNeedsBookingAsync` trước commit. Port dùng cùng DbContext/transaction, không gọi mạng ngoài. ID follow-up không tồn tại hoặc thay đổi bất nhất làm rollback Appointment; không giả vờ chuyển chỉ định sang NeedsBooking.

Tích hợp S24 theo [TREATMENT-PROGRESSION.vi.md](TREATMENT-PROGRESSION.vi.md) sẽ thay adapter mặc định bằng cổng dùng chung DbContext/transaction. Đổi lịch follow-up phải kiểm cửa sổ và readiness mới, vẫn giữ FollowUpId; hủy/NoShow đưa instruction chưa hoàn tất về NeedsBooking và giữ history. Nếu cổng lỗi thì toàn bộ thay đổi Appointment rollback, không báo thành công.
- Lỗi lưu DB rollback mọi thay đổi; không trả thành công. EF concurrency failure trả MSG111; lỗi persistence bất ngờ trả MSG144 qua error middleware.

## Phạm vi tích hợp

Đây là hai command đổi/hủy. API tạo/rút/xử lý yêu cầu hỗ trợ, cùng-visit extension, notification dispatcher và UI staff S12 là các phần riêng. S14/S16 hiện có thể gọi HTTP client bằng `EXPO_PUBLIC_APPOINTMENTS_MOCK=false`; không chuyển cấu hình mặc định khi support-request API chưa sẵn sàng.

## Kiểm thử bắt buộc

HTTP + PostgreSQL trong schema riêng: đổi thành công và giữ duration/link; hủy và nhả slot; replay; key reuse; stale appointment/schedule; slot conflict giữ lịch cũ; ownership/verified link/revocation; anonymous/staff scope/verification; đúng 12 giờ và dưới 12 giờ; trạng thái đã bắt đầu; validation; cạnh tranh cùng version/slot; history/outbox đúng một lần; follow-up adapter rollback khi chưa có module.
