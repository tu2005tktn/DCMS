# III. Functional Requirements

## 11. Dentist: Working Schedule

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "11.1" and "11.2").

### 11.1 Working Schedule

This screen (`/working-schedules`, menu "Lịch làm việc", title "S09 · Lịch làm việc tuần") allows a dentist to:

- See the weekly working schedule.
- Set the weekly template, or an exception for one day (for example a day off).
- Check which appointments a change would affect, then publish the change.
- Preview the free time slots that the schedule gives.

The screen needs the permission to read working schedules. A dentist always sees their own schedule, and edits it when the permission to manage working schedules is granted; the schedule of a colleague is listed only in a branch where the dentist is allowed to manage schedules. Without the permission to manage, every field of the schedule is disabled and the buttons "Kiểm tra ảnh hưởng", "Lưu và công bố" and "Hủy thay đổi chưa lưu" are disabled.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert (error) | Read-only. Title by the kind of error (Không có quyền truy cập, Xung đột · Cần kiểm tra lại, Dữ liệu chưa hợp lệ, Lỗi kết nối hoặc dịch vụ) with the code and the message. Button "Tải lại" (asks to discard unsaved changes first). |
| Alert (success) | Read-only. The notification after publishing. |
| Prompt "Hủy các thay đổi chưa lưu?" | Shown when the user changes the filters with unsaved changes. "Hủy thay đổi chưa lưu để rời trang?" when following a link. The browser also warns when the page is closed. |
| **Filters** | |
| Chi nhánh | Shown only to users who can pick a branch and who have more than one branch. |
| Nhân viên / bác sĩ | Default is the user. Changing it loads that person's schedule. |
| Loại lịch | "Mẫu lặp hằng tuần" (default) or "Ngoại lệ ngày cụ thể". |
| Áp dụng từ ngày / Ngày ngoại lệ | Date, default today. |
| Empty | "Chưa có nhân viên được phân quyền" when there is nobody to show. |
| **Schedule status** | |
| Tag | "Đã công bố" or "Chưa công bố". |
| Text | Read-only. "Version n", "Có thay đổi chưa lưu", and for a published schedule the start date, the publisher and the time. |
| Card "Thay đổi đã công bố trong tương lai" | Read-only. Each future change: date, "Mẫu lặp" or "Ngoại lệ", version and reason. |
| **Editor** | |
| Lý do ngoại lệ | Only for an exception. Required, up to 500 characters, also for a day off. |
| Title | "Lịch làm việc hằng tuần" (7 days) or "Lịch làm việc trong ngày" (the chosen day only). Shows "n ngày làm việc". |
| Ngày | Read-only. Thứ 2 to Thứ 7 and Chủ nhật, with the tag "Có lịch" or "Ngày nghỉ". |
| Chi nhánh (of a day) | Choose "Không làm việc" or a branch of the person. Choosing a branch for a day off adds a work period 08:00–17:00; "Không làm việc" removes the day. |
| Period: Loại | Làm việc or Nghỉ. |
| Period: Bắt đầu, Kết thúc | In steps of 30 minutes. Searchable. |
| Period: Lý do | Up to 500 characters. Placeholder "Ghi chú (không bắt buộc)" for a work period, "Lý do nghỉ" otherwise. |
| Button bin | Removes the period. |
| Button "Thêm khoảng" | Adds a period 08:00–12:00 (Làm việc) to the day. Shown when the day has a branch. |
| Empty text | "Không có ca làm việc" for a day off. |
| **Actions** | |
| Button "Kiểm tra ảnh hưởng" | Lists the confirmed appointments the change would hit. |
| Button "Lưu và công bố" | Checks the impact, then publishes if no appointment is hit. |
| Button "Hủy thay đổi chưa lưu" | Restores the published schedule. Enabled when there is a change. |
| Validation | The periods must be valid and in steps of 30 minutes ("Giờ làm và nghỉ phải theo bước 30 phút."); an exception needs a reason ("Nhập lý do ngoại lệ, kể cả ngày không làm việc."); the date must not be before today ("Ngày áp dụng phải từ hôm nay theo giờ phòng khám."). |
| **Impact** | |
| Alert | "Có n lịch Confirmed bị ảnh hưởng. Giải quyết tại S12 trước khi công bố." or "Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra." |
| Table | Columns Mã lịch (a link to the appointment), Bắt đầu, Kết thúc, Người đến, Trạng thái. |

### 11.2 Preview Availability

The card "Preview availability" of 11.1 allows the dentist to see which slots the schedule gives, without holding any of them.

| Field Name | Description |
|---|---|
| Chế độ preview | "Nhân viên · bản đang sửa" (the draft being edited; default for users who may manage) or "Công khai · bản đã công bố" (what customers see). |
| Từ ngày, Đến ngày | From 1 to 31 days. "Chọn khoảng preview từ 1 đến 31 ngày." when wrong. |
| Chi nhánh preview | A branch of the person. |
| Dịch vụ | Services of the chosen branch. |
| Thời lượng phút | Whole number from 1 to 480. Disabled in the public mode, where the duration follows the service ("Thời lượng theo dịch vụ đã chọn."). |
| Trợ lý | Optional; another employee of the branch. |
| Button "Preview slot" | Disabled until a service is chosen and the person is a dentist of that branch. |
| Text | "Preview tối đa 31 ngày, không giữ chỗ. Mỗi slot dùng một ghế và cùng nhân sự trong toàn bộ khoảng." |
| Table | Columns Bắt đầu, Kết thúc, Ghế, Version lịch. |
| Empty | "Không có khung giờ đáp ứng tài nguyên". |
