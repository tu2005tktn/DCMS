# III. Functional Requirements

## 2. Customer Online Booking

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "2.1" to "2.9").

### 2.1 Customer Shell

This layout (`/customer`) wraps every customer screen. It allows the customer to move between the customer functions and to sign out.

| Field Name | Description |
|---|---|
| Brand "DCMS Cổng bệnh nhân" | Opens the customer home. |
| Avatar and full name | Initials of the last two words of the name and the full name of the signed-in customer. |
| Button "Đăng xuất" | Ends the session and returns to Customer Sign-in. |
| Navigation "Điều hướng khách hàng" | Trang chủ, Đặt lịch, Lịch hẹn của tôi, Đơn thuốc của tôi, Người thân của tôi, Hồ sơ điều trị, Liên kết hồ sơ. The current item is highlighted. |
| Content | Displays the selected customer screen. |

### 2.2 Customer Home

This screen (`/customer/home`) allows the customer to see the nearest appointment and to open the customer functions.

| Field Name | Description |
|---|---|
| Card "Lịch hẹn gần nhất" | Shows the next upcoming appointment with its time (Vietnam time), current status and the button "Xem lịch hẹn". The latest appointment that has already started and was not cancelled (for example "Vắng mặt" or "Chờ phòng khám xác nhận") stays visible with its status: in a row "Lịch vừa qua" under the upcoming one, or as the main appointment with the link "Đặt lịch mới" when nothing is upcoming. Link "Xem tất cả" opens the appointment list. Shows "Đang tải lịch hẹn…" while loading and a retry button on error. |
| Empty state | "Bạn chưa có lịch hẹn sắp tới." with the button "Đặt lịch ngay". |
| Card "Đặt lịch khám" | Button "Bắt đầu đặt lịch" opens the booking wizard. |
| Quick access "Truy cập nhanh" | Five shortcuts: Người thân của tôi, Lịch hẹn của tôi, Đơn thuốc của tôi, Liên kết hồ sơ của tôi, Hồ sơ điều trị, each with a one-line description. |

### 2.3 Booking, step 1: Người khám

This screen (`/customer/book`, title "Đặt lịch khám") is step 1 of the five-step wizard. It allows the customer to:

- Choose whether the customer or a relative will be examined.
- Choose a saved family member when booking for a relative.

The same wizard header is shown in the five steps: the link "Quay lại" (to the customer home, or to Customer Sign-in when not signed in), the text "Bước n/5" with the name of the step, the progress bar "Tiến độ đặt lịch" and the steps Người khám, Chọn lịch, Liên hệ, Xem lại, Hoàn tất. While the data is loading: "Đang tải dữ liệu đặt lịch".

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert (error) | Read-only. The error message. When the session has expired, with the link "Đăng nhập". |
| **Bạn đặt lịch cho ai?** | "Chúng tôi sẽ dùng lựa chọn này để hiển thị đúng thông tin người khám." |
| Option "Bản thân" | Button, "Tôi là người đến khám". The examinee is the customer; the name is filled in from the account. |
| Option "Người thân" | Button, "Tôi đặt lịch cho một người khác". |
| Người thân đã lưu | Select with search (the search runs 0.3 seconds after typing; the list loads 20 at a time while scrolling). Shown for "Người thân". Placeholder "Chọn người thân đã lưu". Each option: name · relationship · date of birth. Empty: "Không có người thân phù hợp". Note "Danh sách được quản lý tại mục Người thân của tôi." |
| Status | "Đang tải người thân đã chọn…" when the page was opened with a chosen family member. If that member was deleted: "Người thân đã xóa hoặc không còn khả dụng. Hãy chọn lại người thân đã lưu." with the button "Thử lại". |
| Button "Tiếp tục" | Moves to step 2. Disabled while loading, and for "Người thân" until a family member is chosen. |

### 2.4 Booking, step 2: Chọn lịch

This step ("Chọn nơi khám và khung giờ") allows the customer to:

- Choose the branch, the dentist and the date.
- See the free time slots and choose one.
- Add a reason for the visit.

| Field Name | Description |
|---|---|
| **Intro** | "Chọn nơi khám và ngày phù hợp để xem các giờ còn trống." |
| **Thông tin lịch khám** | |
| Chi nhánh | Required select, placeholder "Chọn chi nhánh". Changing it clears the dentist. When no branch accepts bookings: "Chưa có chi nhánh nhận đặt lịch". |
| Dịch vụ | Read-only. "Khám · n phút" (the examination service of the branch). If none: "Chưa có dịch vụ Khám khả dụng tại chi nhánh này". |
| Bác sĩ | Required select, placeholder "Chọn bác sĩ". The first option is "Bất kỳ bác sĩ (ghế trống)", then the dentists of the branch. |
| Ngày khám | Required date, default tomorrow. Hint "Ngày đã chọn: dd/mm/yyyy · Giờ Việt Nam". |
| **Chọn khung giờ khám (Giờ Việt Nam)** | |
| Slots | Buttons "hh:mm–hh:mm" grouped under "Buổi sáng" and "Buổi chiều". One slot can be chosen. "Đang tìm khung giờ" while searching. |
| Empty | Before the choices are made: "Chọn chi nhánh, bác sĩ và ngày để xem khung giờ khám."; with no slot: "Không có khung giờ phù hợp. Hãy chọn ngày hoặc bác sĩ khác." |
| **Lịch đã chọn** | |
| Summary | Read-only. Chi nhánh, Dịch vụ, Bác sĩ, Ngày khám, Giờ khám ("Chưa chọn" or "Chưa chọn giờ" until chosen) and the note "Thông tin này chưa phải lịch hẹn đã xác nhận." |
| **Lý do khám** | |
| Lý do khám | Optional text, up to 1,000 characters. A collapsible block: "Thêm lý do khám (không bắt buộc)", or "Lý do khám đã nhập (có thể sửa)" once there is text. |
| Buttons "Quay lại", "Tiếp tục" | Back to step 1, or on to step 3. "Tiếp tục" is disabled until a slot is chosen. |

### 2.5 Booking, step 3: Liên hệ

This step ("Thông tin người đặt và người khám") allows the customer to confirm who books and who is examined. "Phòng khám dùng thông tin liên hệ để trao đổi về lịch hẹn."

| Field Name | Description |
|---|---|
| **Notices** | |
| Error summary | Read-only. "Kiểm tra thông tin trước khi tiếp tục" with a link for each wrong field. |
| Lịch đang đặt | Read-only. The time, the branch and the dentist of the chosen slot. |
| **Người đặt lịch** | "Người phòng khám sẽ liên hệ khi cần." |
| Họ tên người đặt | Required, 1 to 100 characters, filled in from the account. Message "Nhập họ tên người đặt (1–100 ký tự)." |
| Số điện thoại Việt Nam hoặc email | Required, filled in from the account. A phone number of 10 digits starting with 0, or an email. Message "Nhập số điện thoại Việt Nam hoặc email hợp lệ." |
| **Người đến khám** | "Thông tin của bạn." or "Thông tin người thân sẽ đến khám." |
| Họ tên người khám | Required, 1 to 100 characters. Read-only when a saved family member was chosen. Message "Nhập họ tên người khám (1–100 ký tự)." |
| Ngày sinh người khám (không bắt buộc) | Date. Shown only for "Người thân". Cannot be after today. Read-only when a saved family member was chosen. Message "Ngày sinh không hợp lệ hoặc sau ngày hiện tại." |
| Buttons "Quay lại chọn lịch", "Xem lại" | Back to step 2, or on to step 4 after the check of the fields. |

### 2.6 Booking, step 4: Xem lại và xác nhận

This step shows the whole request before it is sent. "Kiểm tra thông tin trước khi gửi yêu cầu đặt lịch."

| Field Name | Description |
|---|---|
| Alert (error) | Read-only. The refusal message with its code (for example when the slot is no longer free). When the refusal is MSG24, MSG25, MSG26, MSG27, MSG28 or MSG111, the chosen slot is cleared, the slots are loaded again and the wizard returns to step 2. When the session has expired: "Phiên khách hàng đã hết hạn. Vui lòng đăng nhập lại trước khi xác nhận lịch." with the link "Đăng nhập". |
| **Lịch khám** | |
| Chi nhánh | Read-only. |
| Dịch vụ | Read-only. |
| Bác sĩ | Read-only. "Bất kỳ bác sĩ (ghế trống)" is replaced by the dentist of the chosen slot. |
| Thời gian | Read-only. Date and time. |
| **Người đặt và người khám** | |
| Người đặt | Read-only. Name · phone or email. |
| Người khám | Read-only. Name, and "(Bản thân)" or "(Người thân)". |
| Ngày sinh người khám | Read-only. Shown for a relative with a date of birth. |
| Button "Sửa thông tin" | Returns to step 3. |
| Button "Xác nhận đặt lịch" | Creates the appointment and moves to step 5. Disabled while sending or after the session has expired. Sending again after an error does not create a second appointment. |

### 2.7 Booking, step 5: Hoàn tất

| Field Name | Description |
|---|---|
| Title "Đặt lịch thành công" | "Lịch khám của bạn đã được xác nhận. Hãy lưu mã này để tiện liên hệ phòng khám." |
| Mã lịch hẹn | Read-only. The appointment code. |
| Trạng thái | Read-only. "Đã xác nhận". |
| Thời gian | Read-only. Start and end time. |
| Button "Xem và quản lý lịch hẹn" | Opens Appointment Detail (2.9). |
| Link "Về trang chủ" | Opens the customer home. |

### 2.8 My Appointments

This screen (`/customer/appointments`, navigation item "Lịch hẹn của tôi") allows the customer to list and filter the appointments that were booked by them or for them, and to open the detail of one appointment (2.9). All times are shown in Vietnam time (UTC+7).

| Field Name | Description |
|---|---|
| Tải lại | Button. Loads the list again. |
| Đặt lịch mới | Link-button. Opens the booking wizard (2.3). |
| Tabs "Sắp tới", "Lịch sử" | Switch between upcoming appointments and past ones. Switching returns to page 1. |
| Trạng thái | Select, default "Tất cả". Options: Đã xác nhận, Đã tiếp nhận, Đang khám, Hoàn thành, Đã hủy, Vắng mặt. |
| Từ ngày | Date. |
| Đến ngày | Date. Message "Đến ngày phải từ ngày bắt đầu trở đi." when it is earlier than "Từ ngày". |
| Áp dụng | Button. Applies the filters and returns to page 1. |
| Đặt lại | Button. Clears the filters. |
| Alert (error) | Read-only. The error message, with the button "Thử lại". |
| **Table** (cards on a phone) | 20 appointments per page. |
| Mã lịch | Read-only. Appointment code, a link to the detail. |
| Thời gian | Read-only. dd/mm/yyyy hh:mm and the duration in minutes. |
| Nha sĩ | Read-only. |
| Người khám | Read-only. Name of the attendee, then "Bản thân" or "Người thân", then "Lịch tôi đặt" (booked by the customer) or "Lịch của tôi". |
| Trạng thái | Read-only tag. Đã xác nhận, Đã tiếp nhận, Đang khám, Hoàn thành, Đã hủy or Vắng mặt. A confirmed appointment shows "Đã đến giờ hẹn" from its start time and "Chờ phòng khám xác nhận" after its end time (display only). |
| Chi tiết | Link. Opens the detail of the appointment. |
| Loading | "Đang tải lịch hẹn…". |
| Empty | "Không có lịch hẹn phù hợp." with the link "Đặt lịch khám mới". |
### 2.9 Appointment Detail

This screen (`/customer/appointments/:code`, title "Chi tiết lịch hẹn") allows the customer to view one appointment and to change or cancel it. When the appointment starts in less than 12 hours, the customer can only send a request to the clinic. All times are shown in Vietnam time (UTC+7).

| Field Name | Description |
|---|---|
| ← Lịch hẹn của tôi | Link. Returns to the list. |
| Tải lại lịch hẹn | Button. Loads the appointment again. |
| Alert (success) | Read-only. The result of the last action, for example the message after a change, a request or a withdrawal ("Đã rút yêu cầu hỗ trợ."). |
| Alert (error) | Read-only. The error message. The button "Thử lại" is not shown when the appointment is not found or the customer has no access (403, 404). |
| **Card with the appointment code** | |
| Mã lịch, Trạng thái | Read-only. The code is the card title; the status is a tag, with the same time-based labels as the list. |
| Alert (warning) | Read-only. Shown for a confirmed appointment whose start time has passed: "Đã đến giờ hẹn của bạn." or, after the end time, "Lịch hẹn đã quá giờ và đang chờ phòng khám xác nhận.", followed by "Nếu bạn đã đến khám, vui lòng liên hệ phòng khám theo số [phone]." |
| Thời gian bắt đầu | Read-only. dd/mm/yyyy hh:mm. |
| Kết thúc | Read-only. |
| Thời lượng | Read-only. "[n] phút". |
| Nha sĩ | Read-only. |
| Chi nhánh | Read-only. |
| Dịch vụ | Read-only. |
| Người khám | Read-only. Name, followed by "(người thân)" when it is a family member. |
| Người đặt | Read-only. Name of the person who booked. |
| Liên hệ người đặt | Read-only. Contact of the booker, partly hidden. "—" when empty. |
| Nguồn quyền | Read-only. "Lịch tôi đặt" or "Lịch của tôi". |
| **Card "Đổi hoặc hủy lịch"** | |
| Giờ hẹn hiện tại | Read-only. Time, dentist and branch of the appointment. |
| Đổi lịch, Hủy lịch | Buttons, shown when the status is "Đã xác nhận" and the appointment starts in at least 12 hours. They open the dialog "Đổi lịch hẹn" or "Hủy lịch hẹn". The clinic checks again when the customer confirms. |
| Yêu cầu đổi lịch, Yêu cầu hủy lịch | Buttons, shown instead when the status is "Đã xác nhận" and less than 12 hours remain. They open the dialog to send a request. The text explains that the old appointment is kept while the clinic checks. |
| Yêu cầu đang chờ xử lý | Read-only block, shown instead of the buttons while a request is pending: the proposed time (when there is one) and the link "Xem và rút yêu cầu" (goes to the card "Yêu cầu hỗ trợ"). The current appointment stays valid. |
| Alert (info) | Read-only. Shown when the customer cannot change the appointment: "Còn dưới 12 giờ…", "Lịch hẹn đã bắt đầu hoặc đã được tiếp nhận…", "Lịch hẹn không còn ở trạng thái Đã xác nhận…" or "Bạn không có quyền tự đổi hoặc hủy lịch này." |
| Cần hỗ trợ? | Read-only. Phone number of the clinic (a call link) and the working hours. |
| **Card "Yêu cầu hỗ trợ"** | Shown only when the appointment has requests. One item for each request. |
| Loại yêu cầu | Read-only. "Đổi lịch hẹn" or "Hủy lịch". |
| Trạng thái | Read-only tag. Đang chờ xử lý, Đã duyệt, Đã từ chối or Đã rút. |
| Gửi lúc | Read-only. |
| Khung giờ đề xuất | Read-only. Shown for a change request. |
| Lý do | Read-only. |
| Phản hồi | Read-only. The public answer of the clinic, when there is one. |
| Rút yêu cầu | Button, shown for a pending request. Withdraws the request. |
| **Card "Lịch sử thay đổi"** | |
| History item | Read-only. "Đổi lịch", "Hủy lịch", "Ghi nhận vắng mặt" or "Đính chính vắng mặt", the time, "Phòng khám" or "Tự phục vụ", the old time (and "→" the new time), and "Lý do: …". |
| Empty | "Chưa có thay đổi lịch hẹn." |
| **Dialog "Đổi lịch hẹn" / "Yêu cầu đổi lịch"** | Two steps: "1 Chọn lịch mới" and "2 Kiểm tra yêu cầu". |
| Alert (error) | Read-only. The error message. When the appointment was changed elsewhere (MSG111 or MSG31) the button "Tải lại lịch hẹn" is shown and the fields are locked. |
| Giờ hẹn hiện tại | Read-only. Time, dentist and duration. |
| Bác sĩ mới | Select of the dentists of the branch. Default is the current dentist. |
| Ngày hẹn mới (giờ Việt Nam) | Date, cannot be before today. |
| Giờ còn trống | Buttons "hh:mm–hh:mm" grouped by "Buổi sáng" and "Buổi chiều". Required: one slot must be chosen. Loading: "Đang tìm giờ trống". Error: the message with the button "Tải lại khung giờ". Empty: "Không có khung giờ phù hợp trong ngày này. Vui lòng chọn ngày khác." |
| Lý do | Required text area, up to 500 characters, with a counter. Messages "Vui lòng nhập Lý do." and "Lý do không được vượt quá 500 ký tự." Note "Không ghi thông tin bệnh lý trong lý do." |
| Đóng, Xem lại | Buttons. "Xem lại" goes to step 2 and is enabled only when a slot is chosen. |
| **Step 2 "Kiểm tra trước khi gửi"** | |
| Mã lịch, Giờ hẹn hiện tại, Bác sĩ đề xuất, Giờ đề xuất, Thời lượng, Lý do | Read-only. |
| Alert (info) | Read-only. For a request (less than 12 hours): the request waits for the clinic and the current appointment stays valid. |
| Quay lại chỉnh sửa | Button. Returns to step 1. |
| Xác nhận đổi lịch / Gửi yêu cầu đổi lịch | Button. Changes the appointment, or sends the request when less than 12 hours remain. Sending again after an error does not create a second change. |
| **Dialog "Hủy lịch hẹn"** | One step. |
| Giờ hẹn hiện tại | Read-only. |
| Lý do | Required text area, up to 500 characters, with the note "Không ghi thông tin bệnh lý trong lý do.". |
| Đóng, Xem lại | Buttons. |
| Review | Mã lịch, Giờ hẹn hiện tại, Thời lượng, Lý do. Alert (warning) "Sau khi xác nhận, lịch hẹn này sẽ bị hủy." For a request: the request waits for the clinic and the current appointment stays valid. |
| Xác nhận hủy lịch / Gửi yêu cầu hủy lịch | Button (red for a direct cancellation). Cancels the appointment, or sends the request when less than 12 hours remain. |
