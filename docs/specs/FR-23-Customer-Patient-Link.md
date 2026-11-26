# III. Functional Requirements

## 23. Customer: Link My Record

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frame "23.1").

### 23.1 Link My Record

This screen (`/customer/patient-link`, title "Liên kết hồ sơ của tôi") allows the customer to ask the clinic to link the account to the patient record of the customer. The clinic checks the identity before it grants the right to see the record. Booking for a family member does not create a link.

| Field Name | Description |
|---|---|
| **Header** | |
| Hồ sơ của tôi | Read-only label above the title. |
| Description | Read-only. "Phòng khám xác minh danh tính trước khi cấp quyền xem hồ sơ. Đặt lịch cho người thân không tạo liên kết." |
| **Card "Trạng thái hồ sơ của bạn"** | |
| Tải lại | Button. Loads the status again. |
| Tag | Read-only. "Chưa liên kết" while the account is not linked to a patient record. |
| Text | Read-only. "Phòng khám cần xác minh danh tính trước khi bạn xem hồ sơ của mình." and "Chỉ gửi yêu cầu sau khi đã đến khám và được tiếp nhận tại phòng khám." |
| **Card "Gửi yêu cầu liên kết"** | |
| Họ tên | Required, up to 160 characters. Messages "Vui lòng nhập họ tên." and "Họ tên không được vượt quá 160 ký tự." |
| Ngày sinh (không bắt buộc) | Date, cannot be after today. |
| Mã hồ sơ (nếu biết) | Text, up to 40 characters, letters, digits, "_" and "-" only. Messages "Mã hồ sơ không được vượt quá 40 ký tự." and "Mã hồ sơ không đúng định dạng." |
| Thông tin đối chiếu | Text area, up to 1,000 characters, with a counter. Message "Không được vượt quá 1000 ký tự." |
| Xác nhận | Checkbox, required: "Đây là hồ sơ của chính tôi, không phải của người thân." Message "Vui lòng xác nhận đây là hồ sơ của chính bạn." |
| Gửi yêu cầu | Button. Sends the request to the clinic. |
