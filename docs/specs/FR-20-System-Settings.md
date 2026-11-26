# III. Functional Requirements

## 20. System Settings ("Thiết lập hệ thống")

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "20.1" to "20.4").

Note: this group covers the overview and the section "Thiết lập chung". The catalog sections (diagnoses, medicines, suppliers, materials, labo, customer groups), branches, access groups and the settings history are separate pages opened from the cards of 20.1.

### 20.1 System Settings Overview

This screen (/settings, menu "Thiết lập hệ thống", group "Quản trị") is the entry of all clinic settings. It allows the Clinic Owner (or a user with the permission to view settings) to find a settings section and open it. Only the sections the user may see are shown.

| Field Name | Description |
|---|---|
| Title | Read-only. "Thiết lập hệ thống". |
| Tìm mục thiết lập… | Text, clearable. Filters the cards by name, ignoring case and accents. When nothing matches: "Không có mục phù hợp." |
| Nhóm Phòng khám | Cards: "Thiết lập chung", "Chi nhánh & Ghế điều trị", "Nhóm khách hàng". |
| Nhóm Danh mục | Cards: "Chẩn đoán / Tên bệnh", "Thuốc / Dược phẩm". |
| Nhóm Vật liệu và labo | Cards: "Công ty cung cấp vật liệu", "Loại dụng cụ / Vật liệu", "Công ty labo", "Loại công việc labo". |
| Nhóm Bảo mật và lịch sử | Cards: "Phân quyền truy cập", "Lịch sử thiết lập". |
| Card | Button. Shows an icon, the name and a short description of the section. Opens the section page (20.2 for "Thiết lập chung"). |
| Alert (error) | Read-only. The error message with the button "Thử lại" when the permissions of the screen cannot be loaded. |
| No permission | "Bạn không có quyền thực hiện thao tác này (403 MSG06)." with the link "Về trang chủ". |

### 20.2 General Settings (/settings/general)

This page opens from the card "Thiết lập chung" in 20.1. It allows the Clinic Owner to set the staff idle sign-out time, the required fields of a patient record, the patient code rules and the receipt print options. Every save is previewed, needs a reason and creates a new version that can be restored later. Without the permission to manage, the whole form is disabled.

| Field Name | Description |
|---|---|
| **Header** | |
| ← Quay lại | Button. Returns to 20.1. With unsaved changes it asks "Bỏ thay đổi chưa lưu?" (Bỏ thay đổi / Tiếp tục sửa). |
| Breadcrumb | Read-only. "Thiết lập / [group] / Thiết lập chung". "Thiết lập" returns to 20.1. |
| Segmented control | Switches between the sections of the same group (Phòng khám: Thiết lập chung, Chi nhánh & Ghế điều trị, Nhóm khách hàng). Shown only when the group has more than one section. |
| Phiên bản | Read-only. The version, or "Mặc định (chưa lưu lần nào)". |
| Hiệu lực từ | Read-only. Date and time the version took effect, or "—". |
| Người lưu | Read-only. Who saved the version, or "—". |
| Alert (error) | Read-only. The error message. For the conflict MSG111 (someone else saved first) the button "Tải phiên bản mới" reads the new version and keeps the user's input. |
| **Tự đăng xuất** | |
| Tự đăng xuất khi không thao tác sau | Select. 5, 10, 20, 30, 60, 90 or 120 minutes. Applies only to staff sessions on the web; the patient app has its own policy. Takes effect for staff sessions right after saving. |
| **Hồ sơ bắt buộc** | |
| Ngày lập hồ sơ, Giới tính, Ngày sinh, Địa chỉ, Số điện thoại, Phân nhóm khách hàng, Nguồn | Checkbox for each field. A checked field becomes required from the next time a patient record is saved. Existing records are not changed. |
| **Mã bệnh nhân** | |
| Chế độ cấp mã | Radio. "Tự động, cho nhập tay" (empty = the system issues a code, typed code is checked for duplicates); "Chỉ tự động" (the code field is locked); "Chỉ nhập tay" (the code is required, duplicates are rejected). |
| Tiền tố | Text, up to 12 letters without accents, digits or hyphens, may be empty. Shows an example of the automatic code. Applies only to new patients; issued codes do not change. |
| Số tiếp theo | Read-only. "Mã tự động kế tiếp", "Số đã cấp gần nhất" and "Số lớn nhất đang có". It does not reserve a code; existing codes, also typed ones, are skipped. |
| Đổi số tiếp theo | Button. Opens the dialog 20.4. Disabled without the permission to manage. |
| **Mẫu in phiếu thu** | |
| Tùy chọn hiển thị | Checkbox for each item: Ghi chú dùng nội bộ, Hiện mã phiếu thu, Địa chỉ bệnh nhân, Số điện thoại bệnh nhân, Phương thức thanh toán, Bác sĩ, trợ thủ, Tổng phí, Giảm giá, Đã thanh toán, Còn lại, Tổng công nợ của bệnh nhân, Dòng ký tên, Mặc định người nộp là bệnh nhân, Mặc định người thu là người thao tác, Ghi chú thanh toán. Applies from the next receipt print; issued receipts keep their amounts, numbers, payer and collector. |
| **Actions** | |
| Xem trước và lưu | Button. Opens the preview dialog 20.3. Enabled only when there are changes and the user may manage. |
| Hoàn tác | Button. Enabled when there are changes. Asks "Bỏ thay đổi chưa lưu?" and restores the saved values. |
| **Lịch sử phiên bản** | |
| Phiên bản | Read-only. Version number, with the tag "Khôi phục từ n" when it was made by a restore. |
| Thời điểm / Người lưu / Lý do | Read-only. |
| Thao tác | Tag "Hiện hành" for the current version; otherwise the button "Khôi phục", which opens 20.3 in restore mode. Disabled while there are unsaved changes or without the permission. |
| Empty | "Chưa có phiên bản nào được lưu; hệ thống đang dùng giá trị mặc định." |
| Paging | 20, 50 or 100 versions per page. |

### 20.3 Preview Dialog

This dialog opens from "Xem trước và lưu" (title "Xem trước thay đổi") or from "Khôi phục" (title "Khôi phục nội dung phiên bản n").

| Field Name | Description |
|---|---|
| Alert (cảnh báo) | Read-only. "Thiết lập vừa được người khác lưu..." with the button "Tải phiên bản mới" when the data is stale. The save is then disabled. |
| Alert (thông tin) | Read-only. "Không có thay đổi so với phiên bản hiện hành." The save is then disabled. |
| Bảng thay đổi | Read-only. Columns Mục, Trước, Sau for each changed setting. |
| Hồ sơ đang thiếu trường vừa bắt buộc | Read-only warning. Lists how many existing patient records miss each newly required field. |
| Cảnh báo | Read-only list of other warnings of the system. |
| Lý do | Textarea, required, up to 500 characters with a counter. Error: "Vui lòng nhập Lý do." |
| Hủy | Button. Closes the dialog. Disabled while saving. |
| Lưu | Button. Saves a new version (or restores the old content as a new version). On success the message "Đã lưu Thiết lập chung, phiên bản n." is shown, the form and the history are refreshed. The same content sent again uses the same request key, so it is not written twice. |

### 20.4 Change Next Patient Number Dialog

This dialog opens from "Đổi số tiếp theo" in 20.2. The number can only go up.

| Field Name | Description |
|---|---|
| Alert (error) | Read-only. The error message. |
| Số tiếp theo mới | Integer, 1 or more, required. Must be greater than the current number; otherwise "Chỉ được tăng: phải lớn hơn n." The current number is shown as help. |
| Xem trước | Button. Checks the new number and shows the next automatic code, the number of existing codes that will be skipped and any issue. Disabled when the number is empty or too low. |
| Alert (cảnh báo) | Read-only. "Số tiếp theo vừa thay đổi..." with the button "Đọc lại" when a new record was created or someone else changed it. |
| Lý do | Textarea, required, up to 500 characters. |
| Hủy | Button. Closes the dialog. |
| Lưu | Button. Enabled only after a successful preview that is not stale and a reason. On success the message "Mã tự động tiếp theo: [mã]." is shown. |

