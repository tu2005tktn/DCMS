# III. Functional Requirements

## 21. Access Groups ("Phân quyền truy cập")

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "21.1" to "21.6").

### 21.1 Access Groups: tab "Nhóm quyền"

This page (/settings/access-groups, card "Phân quyền truy cập" in 20.1) allows the Clinic Owner to define dynamic access groups: which screens and actions a group may use. A group is then assigned to staff members (21.2). The page needs the permission to view access groups; the buttons that change data need the permission to manage groups.

| Field Name | Description |
|---|---|
| **Header** | |
| ← Quay lại / Breadcrumb | Button and read-only path "Thiết lập / Bảo mật và lịch sử / Phân quyền truy cập". Returns to 20.1. |
| Segmented control | Switches between "Phân quyền truy cập" and "Lịch sử thiết lập". |
| Description | Read-only. Explains that groups are set per screen and action and then assigned to staff with their business branches. |
| Tải lại phân quyền | Button. Reloads groups, staff, grant options and capabilities. |
| Thêm nhóm quyền | Button. Opens the dialog 21.3 in add mode. Shown only with the permission to manage groups. |
| Lịch sử phân quyền | Button. Opens the drawer 21.6. Shown only with the permission to view the settings history. |
| Alert (error) | Read-only. The error message; when the data cannot be loaded the button "Thử lại phân quyền" reloads it. |
| Tabs | "Nhóm quyền" and "Gán cho nhân viên" (21.2). |
| **Group table** | |
| Tên phân quyền | Read-only. Name of the group with its code below. |
| Vai trò nền | Read-only. The base role of the group: Trợ lý phòng khám, Bác sĩ or Quản trị viên. |
| Nhân viên | Read-only. Number of staff members assigned to the group. |
| Trạng thái | Read-only tag. "Đang dùng" or "Ngừng dùng". |
| Thiết lập | Button. Opens the dialog 21.3 for the group (read-only when the user may not manage or the group is the user's own). |
| Ngừng dùng / Khôi phục | Button. Opens the dialog 21.5. Not shown for the user's own group or without the permission to manage. "Ngừng dùng" is disabled while the group still has staff: "Chuyển nhân viên sang nhóm khác trước khi ngừng nhóm." |

### 21.2 Access Groups: tab "Gán cho nhân viên"

This tab shows the staff members and the access group and business branches assigned to each. The base role of a staff member decides which groups can be assigned.

| Field Name | Description |
|---|---|
| Nhân viên | Read-only. Name, with the tag "Đã khóa" when the account is locked. |
| Vai trò | Read-only. Roles of the staff member. |
| Nhóm quyền | Read-only. Name of the assigned group, or "Theo quyền vai trò hiện hành" when none is assigned. |
| Chi nhánh nghiệp vụ | Read-only. Branches in which the staff member works, or "Chưa có". |
| Gán nhóm quyền | Button. Opens the dialog 21.4. Shown only with the permission to assign access to staff. Disabled for the user's own row and for a Clinic Owner. |

### 21.3 Access Group Dialog

This dialog opens from "Thêm nhóm quyền" (add) or "Thiết lập" (edit or view) in 21.1. Title: "Thiết lập phân quyền". When editing, the first save shows a preview of the effect and the user saves again to confirm.

| Field Name | Description |
|---|---|
| Tên phân quyền | Text, required, up to 160 characters. |
| Mã phân quyền | Text, required, up to 40 characters. |
| Vai trò nền | Select, required. Trợ lý phòng khám, Bác sĩ, Quản trị viên. Disabled when the group already has staff. Changing it removes the grants that do not belong to the new role. |
| Mô tả | Textarea, up to 1000 characters. |
| Alert (chỉ xem) | Read-only. "Bạn đang thuộc nhóm này. Thay đổi cần được thực hiện bởi người quản lý khác." or "Bạn có quyền xem nhóm phân quyền." The whole form is read-only. |
| Tìm quyền | Text. Filters the grant list by name or key. Parents of a match stay visible. |
| Danh sách quyền | Tree of checkboxes. Each screen is a parent and its actions are children. Checking a child also checks its parents; unchecking a parent unchecks its children. A grant outside the base role is disabled with "· Theo vai trò chuyên môn khác". A user who is not the Clinic Owner can only grant permissions that the user has. |
| Lý do thay đổi phân quyền | Textarea, up to 1000 characters. Required when editing an existing group. |
| Ảnh hưởng | Read-only. Shown after the preview: the number of affected staff members, the grants added and the grants removed. |
| Alert (error) | Read-only. Error message. When the group was changed by someone else (MSG111) the button "Đọc lại phiên bản nhóm, giữ bản nhập" loads the new version and keeps the input. |
| Hủy | Button. Closes the dialog. With unsaved changes it asks "Bỏ thay đổi phân quyền chưa lưu?" (Bỏ thay đổi / Tiếp tục sửa). |
| Xem trước thay đổi / Lưu phân quyền / Đóng | Primary button. When editing: first "Xem trước thay đổi", then "Lưu phân quyền". When adding: "Lưu phân quyền". When the form is read-only: "Đóng". Name, code and the reason (when editing) must be filled; otherwise MSG02. |

### 21.4 Assign Access Group Dialog

This dialog opens from "Gán nhóm quyền" in 21.2. Title: "Gán nhóm quyền · [tên nhân viên]".

| Field Name | Description |
|---|---|
| Nhóm quyền | Select, required. Only active groups whose base role matches the staff member's role, and (for a user who is not the Clinic Owner) whose grants the user also has. |
| Alert (thông tin) | Read-only. "Chưa có nhóm đang dùng phù hợp với vai trò nhân viên. Tạo nhóm phù hợp trước khi gán." when the list is empty. |
| Chi nhánh nghiệp vụ | Multi-select, required. Only branches the staff member joined and the user may manage. |
| Lý do gán nhóm | Textarea, required, up to 1000 characters. |
| Alert (error) | Read-only. Error message. For MSG111 the button "Đọc lại phiên bản nhân viên và nhóm" reloads both. |
| Hủy | Button. Closes the dialog. |
| Lưu nhóm cho nhân viên | Button. Saves the assignment. Missing group, branch or reason gives MSG02. Disabled without the permission to assign. |

### 21.5 Change Group State Dialog

This dialog opens from "Ngừng dùng" or "Khôi phục" in 21.1. Title: "Ngừng dùng nhóm quyền" or "Khôi phục nhóm quyền".

| Field Name | Description |
|---|---|
| Group name | Read-only. Name of the group with the reminder that a group with staff must have its staff moved first. |
| Lý do đổi trạng thái | Textarea, required, up to 1000 characters. |
| Alert (error) | Read-only. Error message. For MSG111 the button "Đọc lại trạng thái nhóm" reloads the group. |
| Hủy | Button. Closes the dialog. |
| Xác nhận trạng thái | Button. Switches the group between active and inactive. Disabled without the permission to manage. |

### 21.6 Access Change History Drawer

This drawer opens from "Lịch sử phân quyền" in 21.1. It shows the latest 100 changes of access groups in the user's permitted scope.

| Field Name | Description |
|---|---|
| Alert (error) | Read-only. Error message with the button "Tải lại lịch sử". |
| Thời gian | Read-only. Date and time of the change. |
| Người thực hiện | Read-only. Name of the staff member, or "#id" when not found. |
| Thao tác | Read-only. "Thiết lập nhóm", "Gán nhóm" or "Đổi trạng thái". |
| Lý do | Read-only. The reason entered. |
| Empty | "Chưa có thay đổi phân quyền." |

