# III. Functional Requirements

## 5. Staff Reception

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "5.1" and "5.2").

### 5.1 Reception List

This screen (`/reception`, title "Tiếp nhận hôm nay" or "Tiếp nhận dd/mm/yyyy") allows staff to:

- See the appointments of a day and check the arriving patients in.
- See the past appointments that nobody has processed, record or undo a no-show, and move a missed appointment to another time.
- See the waiting queue and the visits of the day.
- Open a visit, or book a walk-in appointment.

| Field Name | Description |
|---|---|
| **Header** | |
| Button "Tải lại" | Reloads the data. |
| Button "Xếp lịch khách vãng lai" | Opens the booking drawer. Shown to users who can create appointments and check patients in. |
| **Filters** | |
| Chi nhánh | Shown only when the user has several branches. |
| Ngày tiếp nhận | Date of the list; the title shows "hôm nay" for today. Disabled on the tab "Quá giờ chưa xử lý". |
| Bác sĩ | Filter by dentist. |
| Trạng thái | Filter by status. Hidden on the tab "Quá giờ chưa xử lý". |
| Tìm người đến khám | Search by name, appointment code or patient code ("Tên, mã lịch hoặc mã bệnh nhân"). |
| **Tab "Lịch hẹn"** | |
| Table | Columns Lịch, Người khám ("Chưa gắn hồ sơ" when no record is linked), Người đặt, Bác sĩ, Ghế, Trạng thái, Thao tác. |
| Row order | By work still to do, then by start time (earliest first) inside each group: (1) "Trễ hẹn" and "Chờ xử lý"; (2) upcoming "Đã xác nhận"; (3) "Đã tiếp nhận" and "Đang khám"; (4) "Hoàn thành", "Khách không tới" and "Đã hủy", shown dimmed. The order applies across pages. |
| Action "Tiếp nhận" | Opens Check-in (5.2). |
| Action "Xem điều kiện tiếp nhận" | Opens 5.2 to show why the appointment cannot be checked in yet. |
| Action "Mở lượt khám" | Opens the visit of a checked-in appointment. |
| Trạng thái (labels) | A confirmed appointment shows "Trễ hẹn" after its start time and "Chờ xử lý" after its end time. |
| Action "Khách không tới" | Shown for a confirmed appointment whose start time has passed, to users with `appointments.no-show`. Opens the dialog of 3.5. |
| Action "Hoàn tác không tới" | Shown on the day the no-show was recorded. Opens the dialog of 3.5. |
| Action "Đổi lịch" | Shown for a late appointment of today or a no-show recorded today, to users with `appointments.reschedule`. Opens the drawer of 3.5. |
| **Tab "Hàng chờ và lượt khám"** | |
| Table | Columns Bệnh nhân, Giờ đến, Chờ, Bác sĩ, Lý do đến, Trạng thái lượt (Chờ khám, Đang khám, Hoàn tất, Đã hủy), Thao tác. |
| Action "Mở lượt khám" | Opens the visit. |
| **Tab "Quá giờ chưa xử lý (N)"** | N is the number of confirmed appointments of every date whose end time has passed and that have no visit, within the branch and dentist scope of the user. Newest first. |
| Table | Columns Ngày giờ, Người khám, Người đặt, Liên hệ, Bác sĩ, Trạng thái, Thao tác ("Khách không tới"). Empty: "Không còn lịch quá giờ cần xử lý." |
| **Messages** | |
| Error | "Chưa tải được dữ liệu. Vui lòng tải lại." |
| No branch | "Bạn chưa có chi nhánh được phép tiếp nhận." |

### 5.2 Check-in

This screen (`/reception/appointments/:appointmentId`, title "Tiếp nhận bệnh nhân") allows staff to match the arriving person with the appointment and a patient record, and to open the visit.

| Field Name | Description |
|---|---|
| **Header** | |
| Button "Về danh sách tiếp nhận" | Returns to 5.1. |
| **Appointment information** | |
| Người khám trên lịch | Read-only. |
| Ngày sinh trên lịch | Read-only; "Chưa có thông tin" when unknown. |
| Người đặt | Read-only; "Chưa có liên hệ" when unknown. |
| Lịch hẹn, Chi nhánh, Bác sĩ / ghế | Read-only. |
| **Result** | |
| Region "Kết quả tiếp nhận" | Shows a load error with "Tải lại lịch". |
| Alert "Đã tiếp nhận — mở lượt khám hiện có" | Status "Chờ khám"; buttons "Mở lượt khám" and "Xem hàng chờ". |
| Alert "Bạn chỉ có quyền xem lượt được phân công." | Shown to users who may see only their own visits. |
| Alert "Lịch chưa đủ điều kiện tiếp nhận" | Shown when the appointment cannot be checked in. |
| **Patient record** | |
| Hồ sơ bệnh nhân | Search by name, code or phone ("Tìm tên, mã hoặc số điện thoại"). Message "Vui lòng chọn và đối chiếu hồ sơ bệnh nhân." until one is chosen. |
| Button "Tạo hồ sơ qua chức năng hồ sơ bệnh nhân" | Opens patient creation (4.2). |
| Thông tin cần đối chiếu | Read-only: Mã / họ tên, Ngày sinh ("Chưa biết"), Điện thoại ("Chưa có"). |
| Checkbox "Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn" | Required before check-in. |
| **Visit information** | |
| Lý do đến | Required: Khám mới, Điều trị tiếp, Kiểm tra sau điều trị / bảo hành, Tái khám định kỳ / vấn đề khác. |
| Đợt điều trị cũ (nếu có) | Optional; placeholder "Chưa có đợt điều trị để chọn". The doctor confirms the source later. |
| Ghi chú thêm | Optional. |
| **Arrival time** | |
| Text | "Giờ đến mặc định là thời điểm hệ thống tiếp nhận." |
| Nhập giờ đến hồi cứu | Opens Giờ đến thực tế (giờ Việt Nam) and Lý do hồi cứu. |
| **Actions** | |
| Button "Xác nhận Check-in" | Creates the visit and puts the patient in the queue. |
| Dialog "Bỏ thông tin tiếp nhận chưa lưu?" | Shown when leaving with unsaved data. Buttons "Bỏ thay đổi" and "Tiếp tục nhập". |
