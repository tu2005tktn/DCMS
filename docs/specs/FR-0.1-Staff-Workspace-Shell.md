# III. Functional Requirements

## 0. General

### 0.1 Staff Workspace Shell

Wireframe: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frame "0.1 Staff Workspace Shell (wireframe)")

This screen implements the navigation shell of the web application for staff (admin, clinic owner, dentist, clinic assistant). Every staff screen opens inside this shell. It allows the staff member to:

- Open the functions that the account is permitted to use from the side menu, grouped as Vận hành, Chuyên môn and Quản trị.
- See the current location and the signed-in user.
- Collapse the side menu, or open it as a drawer on a narrow screen.
- Sign out, and be warned before the session expires.

| Field Name | Description |
|---|---|
| **Side menu** | |
| Brand (DCMS, Quản lý phòng khám) | Opens "Tài khoản của tôi". |
| Group "Vận hành" | Lịch hẹn, Tiếp nhận và hàng chờ, Bệnh nhân, Lịch làm việc, Hộp thư hỗ trợ, Chăm sóc khách hàng. |
| Group "Chuyên môn" | Kế hoạch điều trị, Giá và duyệt điều trị, Dụng cụ - vật liệu, Quản lý công việc labo. |
| Group "Quản trị" | Quản lý thuốc, Thống kê công việc/dịch vụ, Thống kê tài chính, Quản lý tài khoản, Thiết lập hệ thống, Cài đặt đặt lịch, Xác minh liên kết hồ sơ. |
| Menu item | Opens the function. Shown only when the account has the permission for it (Thống kê tài chính: clinic owner only; Chăm sóc khách hàng: clinic assistant and clinic owner). A group with no visible item is hidden. The current function is highlighted. |
| Link "Tài khoản của tôi" | Opens the staff member's own account page; highlighted when it is the current page. |
| Button "Thu gọn menu" / "Mở rộng menu" | Collapses or expands the side menu; the choice is remembered in the browser. |
| **Header** | |
| Menu button (narrow screen) | Opens the side menu as the drawer "Điều hướng nhân viên"; the drawer closes after a function is chosen. |
| Breadcrumb | "Phòng khám / " followed by the title of the current function ("Tài khoản của tôi" on the home page). |
| Avatar | The initials of the last two words of the full name ("NV" when there is no name). |
| User label | Full name and the role labels of the account. |
| Button "Đăng xuất" | Ends the session and returns to the sign-in page. A failure message is shown in the content area. |
| **Content area** | |
| Skip link "Đến nội dung chính" | Moves the keyboard focus to the content area. |
| Session warning | Shown when 60 seconds or less remain before the session expires for inactivity: "Phiên staff sẽ hết hạn sau N giây nếu không tương tác."; at 0 it asks the user to sign in again. |
| Content | Displays the screen of the selected function. |
