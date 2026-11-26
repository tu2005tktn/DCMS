# III. Functional Requirements

## 3. Staff Appointments

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "3.1" to "3.5").

### 3.1 Appointment Calendar

This screen (`/appointments`) allows staff to see the appointments of a branch by day or by week and to book a new one. A dentist sees only their own column when the account is limited to its own schedule.

| Field Name | Description |
|---|---|
| Button "Đặt lịch" | Opens the booking drawer (3.3). Requires the permission to create appointments. |
| Mode "Ngày" / "Tuần" | Day view with one column per dentist, or week view for one dentist. |
| Buttons "Hôm nay", "‹ Trước", "Sau ›" | Go to today, or move one day (or week) back or forward. |
| Date label | The weekday and date, or the first and last day of the week. |
| Select "Chi nhánh" | Shown only to users who can pick a branch (more than one branch). |
| Select "Nha sĩ" | "Tất cả nha sĩ" in day view; one dentist in week view. |
| Select "Trạng thái" | "Tất cả trạng thái" or one status: Đã xác nhận, Đã tiếp nhận, Đang khám, Hoàn thành, Đã hủy, Khách không tới. The filter uses the stored status; "Trễ hẹn" and "Chờ xử lý" are display labels of a confirmed appointment (see 3.5). |
| Button "Tải lại" | Reloads the calendar. |
| Legend | Giờ làm, Nghỉ / block, Ngoài lịch. |
| Calendar grid | One column per dentist (per day in week view) with a time scale. A cell is working time, blocked time (the reason is shown, default "Nghỉ") or "Ngoài lịch làm việc". Clicking a working cell opens the booking drawer for that dentist and time. |
| Appointment block | Time range, attendee name, code, source, service, chair and a status tag (see 3.5 for the labels after the start time). Clicking it opens the dialog "Thông tin lịch hẹn" with the link "Mở chi tiết đầy đủ". |
| Messages | "Bạn chưa thuộc chi nhánh nào có lịch hẹn."; "Không có lịch hẹn phù hợp với bộ lọc đã chọn."; "Chi nhánh chưa có nha sĩ trong phạm vi của bạn."; on error "Chưa tải được lịch — đây không phải là lịch trống." with "Thử lại". |

### 3.2 Appointment Detail

This screen (`/appointments/:id`, and the same content in the dialog "Thông tin lịch hẹn") shows one appointment and its actions.

| Field Name | Description |
|---|---|
| Link "← Lịch hẹn" | Returns to the calendar. |
| Button "Tải lại" | Reloads the appointment. |
| Content | Trạng thái, Thời gian, Chi nhánh / ghế, Nha sĩ, Dịch vụ, Lý do, Người khám (Bản thân or Người thân), Người đặt, Loại / nguồn, Lý do ngoại lệ, Phiên bản. |
| Buttons "Chỉnh sửa lịch", "Hủy lịch", "Khách không tới", "Đặt lịch thay thế" | Shown according to the status (for example a confirmed appointment) and the permission of the user. They open 3.4 and 3.5, or the replacement booking. |
| Card "Yêu cầu đổi / hủy của lịch hẹn" | Only for staff who handle support requests. Table with Yêu cầu (code, opens the request), Loại, Trạng thái, Lý do; empty text "Lịch hẹn này chưa có yêu cầu hỗ trợ." |

### 3.3 Staff Booking Drawer

This drawer ("Đặt lịch") allows staff to book an appointment for a customer or patient. It has two steps: the form, then "Xem lại trước khi xác nhận".

| Field Name | Description |
|---|---|
| Loại lịch | Thường, Vãng lai or Tái khám (a follow-up is opened only from an instruction). |
| Nguồn | Điện thoại, Zalo, Facebook, Trực tiếp or Ứng dụng. |
| Họ tên người đặt | Booker's name. |
| SĐT (10 số) hoặc email | Booker's contact. |
| Quan hệ với người đặt | Bản thân or Người thân. |
| Hồ sơ bệnh nhân có sẵn (tùy chọn) | Search by code, name or phone ("Gõ mã, tên hoặc SĐT"). |
| Họ tên người khám, Ngày sinh (nếu biết) | Attendee data. |
| Dịch vụ | Service; Lý do khám (tùy chọn). |
| Chi nhánh, Nha sĩ | Required. |
| Ngày | Date. |
| Thời lượng (phút) | Shown only in a replacement booking: typed, from 1 up to the minutes left in the missed slot. Other bookings have no such field and use the duration of the selected service (shown with the service name and in the review step). |
| Khung giờ hợp lệ | Free slots, each showing the chair DCMS assigned (staff do not choose a chair). Before the choices: "Chọn chi nhánh, nha sĩ, dịch vụ và ngày để xem khung giờ."; none left: "Không còn khung giờ đủ tài nguyên cho thời lượng này…" |
| Lý do ngoại lệ (bắt đầu trong vòng 60 phút) | Required when the appointment starts within 60 minutes. |
| Buttons "Hủy", "Xem lại" | Close the drawer or move to the review step. |
| Review | Loại / nguồn, Người đặt, Người khám, Chỉ định, Dịch vụ, Nha sĩ / ghế, Thời gian, Lý do ngoại lệ; buttons "Sửa thông tin" and "Xác nhận đặt lịch". |

### 3.4 Edit Appointment

This drawer ("Chỉnh sửa lịch hẹn") allows staff to move an appointment to another date, dentist or slot.

| Field Name | Description |
|---|---|
| Warning | "Lịch hẹn không còn ở trạng thái có thể thực hiện thao tác này." when the status no longer allows the action. |
| Summary | Mã lịch, Bệnh nhân, Lịch hiện tại, Chi nhánh, Bác sĩ, Thời lượng. |
| Ngày hẹn mới (giờ Việt Nam) | Date, not before today. |
| Bác sĩ mới | Dentists of the same branch. |
| Khung giờ mới | Free slots; none: "Không có khung giờ phù hợp. Chọn ngày hoặc bác sĩ khác." The chosen slot is shown as "Giờ mới đã chọn". |
| Lý do chỉnh sửa (bắt buộc) | Required, up to 500 characters. |
| Buttons "Đóng", "Lưu thay đổi" | "Lưu thay đổi" needs a slot and a reason. On a version conflict the button "Tải lại phiên bản" loads the latest data. |

### 3.5 Cancel, No-show, Undo and Move a Missed Appointment

Three confirmation dialogs. They are opened from the appointment detail; "Khách không tới" and "Hoàn tác không tới" are also offered in the Reception list (5.1).

| Field Name | Description |
|---|---|
| Dialog "Hủy lịch hẹn" | Warning "Lịch hẹn sẽ được hủy và khung giờ được giải phóng."; the same summary as 3.4; Lý do hủy lịch (bắt buộc); buttons "Giữ lịch hẹn" and "Xác nhận hủy lịch". |
| Dialog "Khách không tới" | The same summary; Lý do khách không tới (bắt buộc); buttons "Giữ nguyên" and "Xác nhận khách không tới". The reason defaults to "Khách không tới theo lịch hẹn." |
| Dialog "Hoàn tác khách không tới" | Opened by the button "Hoàn tác không tới". The same summary; Lý do hoàn tác (bắt buộc), default "Ghi nhận nhầm khách không tới."; buttons "Giữ nguyên" and "Xác nhận hoàn tác". The button is shown only on the clinic day the no-show was recorded, to users with the permission `appointments.no-show`. The appointment returns to Confirmed, the open callback task is cancelled and the customer is notified. Refused when another active appointment overlaps the same chair, dentist or patient (MSG120), after that day (MSG119), or when the treatment sheet was removed because the deposit was forfeited (MSG121). |
| Button "Đổi lịch" | Moves a missed appointment: a confirmed appointment of today whose start time has passed, or a no-show recorded today. Requires `appointments.reschedule`; also offered in the Reception list. Opens a drawer with Ngày hẹn mới (default today), Bác sĩ mới, the free future slots (no 60-minute lead time) and Lý do đổi lịch (required, default "Khách đến muộn, đổi sang giờ khác."). The appointment becomes Confirmed at the new time, the open callback task is cancelled, the history keeps the no-show and the customer is told the new time. Refused when the same person already has another open self-booked examination (MSG118) or the treatment sheet was removed (MSG121). |
| Status label of a confirmed appointment | Not stored. "Đã xác nhận" before the start time; "Trễ hẹn" from the start time until the end time; "Chờ xử lý" after the end time. "Khách không tới" can be recorded as soon as the start time has passed; there is no waiting period and no automatic no-show. |
