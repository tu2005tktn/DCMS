# III. Functional Requirements

## 13. Dentist: Treatment Plans (Plan Templates)

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "13.1", "13.2" and "13.3").

### 13.1 Treatment Plan List

This screen (`/treatment-templates`, menu "Kế hoạch điều trị", group "Chuyên môn") allows a dentist to:

- See the list of treatment plans of a branch.
- Create a new treatment plan.
- Open a treatment plan to see and edit its items.

A treatment plan is a ready-made list of items (service, teeth, time, cost) that the dentist later applies to a patient's treatment sheet (see 9.10). The screen is for users with the role Dentist and the permission "Xem mẫu điều trị" (`treatment.templates.read`); any other user sees "Bạn không có quyền quản lý kế hoạch điều trị." The permission "Tạo, sửa và phát hành mẫu điều trị" (`treatment.templates.manage`) is needed to create, edit and delete; without it every button of creation and editing is disabled.

| Field Name | Description |
|---|---|
| **Header** | |
| Chi nhánh | Select. Shown only to users who can pick a branch and who have more than one branch. Default is the first branch. Changing it with unsaved changes asks "Bỏ các thay đổi chưa lưu?". |
| Alert (error) | Read-only. The error message, with the button "Thử lại" when the list cannot be loaded. |
| **Danh sách kế hoạch** | |
| Tạo kế hoạch | Button. Opens the dialog 13.3. Disabled without the permission to manage, or while the system is busy. |
| Tìm tên kế hoạch | Text, up to 160 characters, clearable. The search runs when the user presses Enter or clicks "Tìm kiếm". |
| Tìm kiếm | Button. |
| Tên kế hoạch | Read-only. |
| Số hạng mục | Read-only. |
| Tổng chi phí | Read-only. Total cost of the items, in ₫. |
| Thao tác | Button "Mở". Opens the plan (13.2). |
| Empty | "Chưa có kế hoạch điều trị." ("Chưa tải được danh sách." when loading failed). |
| Paging | 20 plans per page. |

### 13.2 Treatment Plan Items

This window opens from "Mở" in 13.1. It allows a dentist to:

- See the items of the plan with the item count and the total cost.
- Add, edit and delete an item.
- Rename or delete the plan.

| Field Name | Description |
|---|---|
| **Header** | |
| Title | The name of the plan. |
| Summary | Read-only. "n hạng mục · Tổng chi phí: … ₫". |
| Sửa tên | Button. Opens the dialog 13.3 in rename mode. Disabled while an item is being edited. |
| Xóa kế hoạch | Button. Asks "Bạn có muốn xóa kế hoạch điều trị này không?" (Đồng ý / Không đồng ý). |
| Alert (error) | Read-only. Code and message. For the conflict MSG111 (someone else changed the plan) the text "Bản nhập vẫn được giữ. Đọc phiên bản mới để đối chiếu trước khi lưu lại." and the button "Xem phiên bản mới". |
| Alert "Phiên bản mới" | Read-only. Shows the new version (name, version, number of items, total) and each item. Button "Giữ bản nhập để lưu lại". |
| **Items table** | |
| Ngày | Read-only. Planned date or "—". |
| Răng | Read-only. FDI codes or "—". |
| Điều trị/dịch vụ | Read-only. |
| Thời gian dự kiến (phút) | Read-only. |
| Ghi chú | Read-only. |
| Bác sĩ & phụ tá | Read-only. The dentist, and "Phụ tá: name". |
| Chi phí | Read-only. In ₫. |
| Thao tác | Buttons "Sửa" and "Xóa" (asks "Bạn có muốn xóa hạng mục này không?"). Disabled while another item is being edited. |
| Empty | "Chưa có hạng mục. Bấm Thêm hạng mục để nhập." |
| **Item editor (one row)** | |
| Ngày dự kiến | Date. Optional. |
| Răng | Text, up to 160 characters, placeholder "25, 26, 46". FDI codes separated by commas, up to 52 codes. Error "Nhập mã răng FDI, ví dụ 25, 26, 46." |
| Điều trị/dịch vụ | Required select with search, placeholder "Chọn dịch vụ". Choosing a service fills the cost with its reference price when there is one. A stopped service is shown with "(đã ngừng)". Error "Chọn điều trị/dịch vụ." |
| Thời gian dự kiến (phút) | Number, optional, a whole number from 1 to 480. |
| Ghi chú | Text, up to 1,000 characters. |
| Bác sĩ | Select with search, optional, clearable, placeholder "Bác sĩ (không bắt buộc)". |
| Phụ tá | Select with search, optional, clearable. |
| Chi phí | Number in ₫, a whole number from 0 to 999999999999. |
| ✓ (Đồng ý) | Saves the item. The messages of the invalid fields are listed under the buttons. |
| × (Hủy sửa hạng mục) | Cancels. Asks "Bỏ các thay đổi chưa lưu?" when there are changes. |
| + Thêm hạng mục | Button. Adds an empty editor row. Disabled while editing, or when the plan already has 100 items. |

### 13.3 Create or Rename a Plan

This dialog ("Tạo kế hoạch điều trị" or "Sửa tên kế hoạch") allows a dentist to enter the name of a plan.

| Field Name | Description |
|---|---|
| Tên kế hoạch | Required text, up to 160 characters. |
| Alert (error) | Read-only. The error message; for MSG111 the button "Xem phiên bản mới". |
| Hủy | Button. Closes the dialog (asks "Bỏ các thay đổi chưa lưu?" when there are changes). |
| Lưu | Button. Disabled when the name is empty or longer than 160 characters. After saving a new plan, its window (13.2) opens. |

The browser also warns when the page is closed, or a link is followed, with unsaved changes.
