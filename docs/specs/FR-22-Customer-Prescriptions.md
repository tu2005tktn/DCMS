# III. Functional Requirements

## 22. Customer: Prescriptions

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "22.1" for the list and "22.2" for the detail).

The screens show the prescriptions that the dentists of the clinic wrote for the customer. Everything is read-only. A prescription is shown only when the account is linked to a verified patient record (see the screen "Liên kết hồ sơ"). Medicines are always bought by the patient outside the clinic.

### 22.1 My Prescriptions

This screen (`/customer/prescriptions`, navigation item "Đơn thuốc của tôi", title "Đơn thuốc của tôi") allows the customer to see the list of their prescriptions. The newest prescription is shown first (up to 200 prescriptions).

The detail screen (`/customer/prescriptions/:id`) allows the customer to read one prescription in the form of a printed sheet. A prescription that does not belong to the linked record of the customer cannot be opened.

| Field Name | Description |
|---|---|
| **Prescription list** | One card for each prescription. The whole card is a link to the screen below. |
| Ngày kê đơn | Read-only. dd/mm/yyyy, shown in bold. |
| Số thuốc | Read-only. "[n] thuốc", the number of medicines in the prescription. |
| Chi nhánh | Read-only. Name of the branch where the prescription was written. |
| Bác sĩ | Read-only. "Bác sĩ [name]". |
| Chẩn đoán | Read-only. "Chẩn đoán: [diagnosis]". Not shown when the prescription has no diagnosis. |
| Tên thuốc | Read-only. Names of the medicines, separated by commas. |
| Xem đơn thuốc → | Link. Opens the screen below for this prescription. |
| **Loading** | |
| Loading | "Đang tải đơn thuốc…" with a spinner. |
| **Error** | |
| Alert (error) | Read-only. The error message, with the button "Thử lại" that loads the list again. |
| **Empty** | |
| Empty | "Chưa có đơn thuốc nào. Đơn chỉ hiển thị khi tài khoản đã liên kết hồ sơ được xác minh." The words "liên kết hồ sơ" are a link to the screen "Liên kết hồ sơ". |
| **Detail "Chi tiết đơn thuốc"** (`/customer/prescriptions/:id`) | |
| ← Đơn thuốc của tôi | Link. Returns to the list. |
| **Sheet header** | |
| Tiêu đề | Read-only. "ĐƠN THUỐC". |
| Tên phòng khám | Read-only. Name of the branch that wrote the prescription. |
| Số điện thoại, Email | Read-only. Contact of the branch, shown only when the branch has them. |
| **Section "BỆNH NHÂN"** | |
| Họ và tên | Read-only. |
| Mã BN | Read-only. Patient code. |
| Ngày sinh | Read-only. dd/mm/yyyy or "—". |
| Giới tính | Read-only. Nam, Nữ, Khác or "—". |
| Chẩn đoán | Read-only. "—" when empty. |
| Chẩn đoán khác | Read-only. "—" when empty. |
| **Section "CHỈ ĐỊNH"** | A numbered list, one line for each medicine. |
| Tên thuốc | Read-only. Medicine name, followed by " · [strength]" when there is a strength. |
| Số lượng | Read-only. Quantity and unit, for example "21 viên". |
| Hướng dẫn dùng | Read-only. Instructions of the dentist for this medicine. Not shown when empty. |
| **Section "GHI CHÚ/LỜI DẶN"** | |
| Ghi chú/Lời dặn | Read-only. The advice of the dentist, or "—". |
| **Section "NGÀY & BÁC SĨ KÊ ĐƠN"** | |
| Ngày | Read-only. Date of the prescription, dd/mm/yyyy. |
| Bác sĩ | Read-only. Name of the dentist. |
| **Notice** | |
| Alert (info) | Read-only. "Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ." |
| **Loading and errors** | |
| Loading | "Đang tải đơn thuốc…" with a spinner. |
| Not found | "Không tìm thấy đơn thuốc trong hồ sơ đã liên kết của bạn." Shown when the prescription does not exist or does not belong to the customer. |
| Alert (error) | Read-only. Other error message, with the button "Thử lại". |
