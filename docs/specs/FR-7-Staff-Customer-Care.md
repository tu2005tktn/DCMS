# III. Functional Requirements

## 7. Staff Customer Care

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "7.1" to "7.3").

### 7.1 Customer Care List

This screen (`/customer-care`, title "Chăm sóc khách hàng") is for clinic assistants and the clinic owner. It allows staff to:

- Find care tasks by period, owner, type, status or customer.
- Open a task, or create a new one.
- Export the list to Excel.

The clinic owner can read every branch but does not write.

| Field Name | Description |
|---|---|
| **Header** | |
| Button "TẠO MỚI" | Opens 7.3. Shown to users who can manage care tasks. |
| **Filters** | |
| Chi nhánh | "Tất cả chi nhánh" or one branch; only the clinic owner with several branches. |
| Xem theo | Xem theo ngày, tuần or tháng. |
| Kỳ trước, Từ ngày, Đến ngày, Kỳ sau | Move the period, or set the dates. "Từ ngày phải trước hoặc bằng đến ngày." or "Khoảng ngày tối đa 366 ngày." when the range is wrong. |
| Người phụ trách | Filter by owner. |
| Trạng thái | Cần xử lý (Mở + Đang xử lý), Mở, Đang xử lý, Đã xong, Đã hủy. |
| Khách hàng | Search by code, name or phone ("Khách hàng: mã, tên hoặc SĐT"), up to 100 characters, clearable. |
| **List** | |
| Text "Tìm thấy n phiếu." | Number of tasks found. |
| Buttons "[Excel]", "Tải lại" | Export the filtered list; reload. |
| Table | Columns Thời gian, Chi nhánh (only when several branches are shown), Khách hàng (name, code or "Chưa có hồ sơ" for a missed booking without a patient record, phone), Loại chăm sóc, Nội dung, Người phụ trách ("Chưa giao"), Trạng thái, Liên hệ gần nhất (result and time, or "Chưa liên hệ"). |
| Row | Clicking a row opens 7.2. |
| Paging | Page size 20, 50 or 100. |
| **Messages** | |
| No branch | "Bạn chưa thuộc chi nhánh nào để chăm sóc khách hàng." |
| Empty | "Không có phiếu chăm sóc phù hợp" |
| Error | The error text with "Thử lại". |
| Access | "Chỉ Trợ lý và Chủ phòng khám dùng màn Chăm sóc khách hàng." |

### 7.2 Care Task Drawer

This drawer (title: patient code, or "Chưa có hồ sơ", and name) allows staff to read a task, to record a contact with the customer and to close the task.

| Field Name | Description |
|---|---|
| **Header** | |
| Button "Sửa" | Opens 7.3 for this task. |
| Button "Đánh dấu đã xong" | Sets the status to "Đã xong". |
| **Task** | |
| Chi nhánh, Khách hàng, SĐT | Read-only. |
| Thời gian chăm sóc | Read-only. |
| Loại chăm sóc | Read-only. |
| Người phụ trách | Read-only; "Chưa giao" when empty. |
| Trạng thái | Read-only, with the version number. |
| Nội dung | Read-only. |
| **Ghi nhận liên hệ** | |
| Kênh | Required: Điện thoại, Trực tiếp, Khác. |
| Kết quả | Required: Đã liên lạc được, Không nghe máy, Khách từ chối, Khác. |
| Bước tiếp theo | Optional, up to 1000 characters. |
| Button "Lưu liên hệ" | Saves the contact. It does not send a message or book an appointment. |
| **Lịch sử liên hệ** | |
| Timeline | Result, channel, time, user and next step. Empty text "Chưa có lần liên hệ nào". |
| **Message** | |
| Conflict | "Phiếu vừa thay đổi ở nơi khác. Dữ liệu mới đã được tải lại, hãy kiểm tra rồi làm lại." |

### 7.3 Care Task Form

This dialog ("Thêm chăm sóc" or "Sửa chăm sóc") allows staff to create or edit a task.

| Field Name | Description |
|---|---|
| Chi nhánh | Required when the user has several branches; not editable on an existing task. |
| Ngày, Giờ, Phút | Required. |
| Khách hàng | Required for a new task; search by code, name or phone ("Không có khách phù hợp" when none). Read-only when editing. |
| Người phụ trách | Optional; "Chưa giao". |
| Loại chăm sóc | Required for a new task; read-only when editing. The button "Quản lý loại chăm sóc" opens the type catalogue, for users who may manage it. |
| Nội dung | Required, up to 4000 characters. |
| Trạng thái | Only when editing: Mở, Đang xử lý, Đã xong, Đã hủy. |
| Buttons "Hủy", "Lưu" | Close or save. Closing with typed content asks "Bỏ nội dung đang nhập?" (buttons "Bỏ và đóng", "Ở lại"). |
| Conflict | "Phiếu vừa được người khác cập nhật. Đóng hộp thoại, tải lại danh sách rồi sửa lại." |
