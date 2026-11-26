# III. Functional Requirements

## 19. Booking Settings ("Cài đặt đặt lịch")

Wireframe: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frame "19.1").

### 19.1 Booking Settings

This screen (/booking-settings, menu "Cài đặt đặt lịch", group "Quản trị") allows a user to set, for one branch, the slot interval and the services that can be booked, with their durations. It is for staff with the permission "Xem cài đặt đặt lịch"; changing needs "Sửa cài đặt đặt lịch". Appointments that were already booked keep their duration.

| Field Name | Description |
|---|---|
| **Header** | |
| Title | Read-only. "Cài đặt đặt lịch". |
| Description | Read-only. Explains that the first examination / new course has only the service "Khám", and that treatment services are kept for the patient's treatment plan. |
| Alert (error) | Read-only. The error message, with the button "Thử lại" (or "Bỏ thay đổi và tải lại" when there are unsaved changes). The button is disabled without the permission to manage. |
| Alert (success) | Read-only. "Đã lưu cấu hình. Các lịch hẹn đã đặt giữ nguyên thời lượng." |
| Empty | "Bạn chưa có chi nhánh được phép cài đặt." when the user has no branch. |
| **Branch and slot** | |
| Chi nhánh cài đặt | Select. Shown only to users who can pick a branch and have more than one branch. Default is the first branch. Disabled while there are unsaved changes or while saving. |
| Bỏ thay đổi | Button. Shown only when there are unsaved changes. Reloads the settings and drops the changes. Disabled without the permission to manage. |
| Bước chia slot (phút) | Integer, 1 to 120. The step of the bookable times, counted from the start of each working period. Required. |
| **Dịch vụ khám / điều trị** | |
| Tên dịch vụ | Text, required, up to 160 characters. The name of the initial examination service is fixed as "Khám" and cannot be edited. |
| Thời lượng (phút) | Integer, 1 to 480. Required. |
| Hoạt động | Checkbox. An inactive service is not offered for booking. |
| Dùng cho | Read-only. "Khám lần đầu / đợt mới" for the examination service, "Điều trị theo phác đồ" for the others. |
| Thêm dịch vụ | Button. Adds a row with an empty name, 30 minutes, active. Disabled when there are 500 services or without the permission to manage. |
| Lưu cài đặt | Button. Saves the slot interval and the services of the branch. Enabled only when there are changes and all values are valid. Shows a loading state while saving. Retrying the same content uses the same request key; if someone else changed the settings first, the save is rejected and the user reloads. |
| Note | Read-only. "Thời lượng dịch vụ: 1–480 phút. Giờ nghỉ, lịch nha sĩ, ghế và các hẹn đã đặt vẫn được kiểm tra khi chọn giờ và xác nhận." |
