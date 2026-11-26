# III. Functional Requirements

## 6. Staff Support Inbox

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "6.1" to "6.3").

### 6.1 Support Inbox

This screen (`/support-inbox`, title "Hộp thư hỗ trợ") allows staff to see the requests that customers send about their appointments (change, cancel, confirm attendance, general question) and to open one for handling.

| Field Name | Description |
|---|---|
| **Filter** | |
| Tabs | Four filters over the five request statuses: "Cần xử lý" (Chờ staff and Đang xem xét, the default), "Chờ staff", "Đang xem xét", "Đã đóng" (Đã xử lý, Bị từ chối and Đã rút). The selected tab shows the number of requests. "Đang xem xét" is kept for later: no action sets it yet, so the tab is empty. |
| Button "Tải lại" | Reload icon. Reloads the list. The list also refreshes by itself when a request changes. |
| **List** | |
| Table | Columns Mã lịch, Loại yêu cầu (Yêu cầu đổi lịch, Yêu cầu huỷ lịch, Xác nhận tham dự, Rút xác nhận tham dự, Hỏi chung), Lý do, Người yêu cầu (with "(vãng lai)" and the phone), Trạng thái (Chờ staff, Đang xem xét, Đã xử lý, Bị từ chối, Đã rút), Cập nhật. |
| Row | Clicking a row opens 6.2. |
| Paging | Shows "n yêu cầu"; page size 10, 20 or 50. |
| **Messages** | |
| Empty | "Không có yêu cầu nào trong bộ lọc này." |
| Error | "Chưa tải được dữ liệu." with "Thử lại". |

### 6.2 Request Drawer

This drawer (title: appointment code and status) allows staff to review one request, to change or cancel the appointment when the request asks for it, and to resolve or reject the request. It has three tabs.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert | Read-only. Shows the result or the error of the last action. |
| Alert "Bạn không thuộc chi nhánh của lịch hẹn này — chỉ đọc." | The user may only view the request. |
| Alert "Yêu cầu đã được rút bởi người gửi…" | A withdrawn request cannot be resolved or rejected. |
| **Tab "Tổng quan": Yêu cầu** | |
| Mã yêu cầu, Loại, Trạng thái, Phiên bản | Read-only. |
| Người yêu cầu, SĐT liên hệ | Read-only; "(vãng lai)" for an anonymous requester. |
| Gửi lúc, Cập nhật | Read-only. |
| Xác minh requester | "Đã xác minh" with the time, or the tag "Chưa xác minh". |
| Lý do bệnh nhân, Khung giờ đề xuất | Read-only; "—" when empty. |
| **Tab "Tổng quan": Lịch hẹn** | |
| Mã lịch, Trạng thái lịch, Chi nhánh, Nha sĩ, Dịch vụ, Thời lượng | Read-only. |
| Bắt đầu, Kết thúc | Read-only. |
| Bệnh nhân, Booker | Read-only; "(người thân)" or "(vãng lai)" when it applies. |
| Xác nhận tham dự | Đã xác nhận, Đã từ chối or Chờ xác nhận. |
| Phiên bản lịch | Read-only. |
| **Tab "Tổng quan": Kết quả xử lý** | |
| Người xử lý, Lý do (nội bộ) | Read-only. Shown once the request is decided. |
| **Tab "Tổng quan": Đổi lịch / Huỷ lịch** | Shown for a change or cancel request when the user may edit. |
| Khung giờ mới | Required for a change request; date and time. |
| Lý do thay đổi | Required: Bệnh nhân yêu cầu, Nha sĩ bận, Ghế bận, Giảm giờ làm, Không đến, Khác. |
| Ghi chú nội bộ (tùy chọn) | Up to 500 characters. |
| **Tab "Lịch sử"** | |
| Timeline | Each entry: action, actor, time, and when present the appointment status, new time, reason and note. Empty text "Chưa có lịch sử." |
| **Tab "Xử lý"** | |
| Button "Xác minh người yêu cầu" | Marks the requester as checked against the booking data. |
| Button "Đổi lịch" / "Huỷ lịch" | Applies the change to the appointment; needs a verified requester. Becomes "Đã thay đổi lịch" afterwards. |
| Button "Xử lý yêu cầu" | Closes the request after a confirmation ("Xử lý yêu cầu?"). A change or cancel request must be applied first. |
| Button "Từ chối" | Opens 6.3. |

### 6.3 Reject Request

Dialog "Từ chối yêu cầu".

| Field Name | Description |
|---|---|
| Lý do từ chối | Required, up to 500 characters (internal). |
| Nội dung gửi người dùng (tùy chọn) | Up to 500 characters; must not contain other people's records or internal notes. |
| Buttons "Hủy", "Xác nhận từ chối" | Close or reject the request. |
