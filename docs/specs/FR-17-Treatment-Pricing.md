# III. Functional Requirements

## 17. Treatment Pricing ("Giá và duyệt điều trị")

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "17.1" and "17.2").

### 17.1 Reference Price List

This screen (`/commercial/treatment`, menu "Giá và duyệt điều trị", group "Chuyên môn") allows a user to:

- See the reference price of every treatment service in the user's branch scope.
- Open the dialog 17.2 to change the reference price and the unit of a service.

The screen is for users with the permission "Xem bảng giá điều trị" (`treatment.prices.read`) or "Xem duyệt thương mại điều trị" (`treatment.commercial.read`). Changing a price needs "Thiết lập giá điều trị" (`treatment.prices.manage`). A change of the reference price does not change the agreed price of an existing treatment plan: old plans keep their snapshot.

Note: in the current code this screen only manages reference prices. The approval of the agreed price and the discount of a plan item is done inside the treatment sheet (see 9.5), not on this screen.

| Field Name | Description |
|---|---|
| **Header** | |
| Title | "Giá tham chiếu điều trị". |
| Description | Read-only. States who can update the reference prices (dentists of the branch, Admin, clinic owner). |
| Alert (error) | Read-only. The error message when the list cannot be loaded or a price cannot be saved, with the button "Thử lại" that reloads the list. |
| Alert (success) | Read-only. "Đã cập nhật giá tham chiếu. Kế hoạch cũ giữ nguyên snapshot." |
| **Giá tham chiếu dịch vụ** | |
| Note | Read-only. "Thay đổi catalog không sửa giá đã chốt". |
| Loading | A skeleton is shown while the list is loading. |
| Tên dịch vụ | Read-only. Name of the treatment service. |
| Chi nhánh | Read-only. Branch of the service, shown as "Chi nhánh #id". |
| Giá tham chiếu | Read-only. Price per unit in ₫, or "Chưa có giá tham chiếu" when the service has no price. |
| Cập nhật giá | Button. Opens the dialog 17.2 for the service. Disabled without the permission `treatment.prices.manage`. |
| Empty | "Chưa có dịch vụ trong phạm vi chi nhánh." |

### 17.2 Reference Price Dialog

This dialog opens from "Cập nhật giá" in 17.1. Its title is "Giá tham chiếu · [tên dịch vụ]".

| Field Name | Description |
|---|---|
| Đơn vị dịch vụ | Select. One of: lần, răng, hàm, gói, g, ml. The default is the current unit, or "lần" when the service has none. |
| Giá tham chiếu mỗi đơn vị (VND) | Number, integer, 0 or more. Can be left empty to remove the reference price. |
| Hủy | Button. Closes the dialog without saving. Disabled while the system is saving. |
| Lưu giá | Button. Saves the unit and the price. Shows a loading state while saving. On success the dialog closes, the success alert of 17.1 is shown and the list is reloaded. On failure the error is shown in the alert of 17.1. |
| Concurrency | The save sends the version of the price that was read. If someone else changed the price first, the save is rejected and the user must reload. Each opening of the dialog uses a new request key, so a retry of the same save does not create a duplicate. |
