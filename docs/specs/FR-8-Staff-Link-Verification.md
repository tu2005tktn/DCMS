# III. Functional Requirements

## 8. Staff Link Verification

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "8.1" to "8.4").

### 8.1 Link Verification Queue

This screen (`/link-verification`, title "Xác minh liên kết hồ sơ") allows a clinic assistant to check requests that link a customer account to a patient record, before the account may see that record. A link is never verified from the phone number alone. It has two tabs, "Hồ sơ của bản thân" and "Hồ sơ người thân".

| Field Name | Description |
|---|---|
| **Tab "Hồ sơ của bản thân"** | |
| Trạng thái | Chờ xác minh (default), Đã xác minh, Từ chối, Đã thu hồi, Đã rút, Tất cả. |
| Từ ngày, Đến ngày | Date range. "Khoảng ngày không đúng định dạng." when the start is after the end. |
| Từ khóa | Request code, account, name, e-mail or phone; up to 100 characters. |
| Buttons "Tìm kiếm", "Đặt lại", "Tải lại" | Apply the filters, clear them, reload. |
| Table | Columns Mã yêu cầu, Tài khoản (user name and full name), Họ tên khai báo, Thời gian gửi, Trạng thái, Người xử lý (name and time, or "—"). |
| Row | Clicking a row opens 8.2. |
| Paging | Shows "n yêu cầu"; page size 10, 20 or 50. |
| Messages | Empty "Không có yêu cầu phù hợp với bộ lọc."; error "Chưa tải được dữ liệu." with "Thử lại". |
| **Tab "Hồ sơ người thân"** | |
| Trạng thái quyền người thân | "Tất cả" or one access status. |
| Tìm yêu cầu người thân | Search by name, account or request code; button "Tìm kiếm". |
| Button "Tải lại" | Reloads the list. |
| Table | Columns Mã yêu cầu, Người yêu cầu, Người thân, Gửi lúc, Trạng thái, Thao tác with the button "Đối chiếu" (opens 8.4). |
| Messages | Empty "Không có yêu cầu phù hợp."; error "Chưa tải được yêu cầu." with "Thử lại". |

### 8.2 Review Drawer

This drawer (title: request code and status, or "Đối chiếu yêu cầu") allows staff to compare the account with the declared data, choose the patient record and verify, reject or revoke the link. It has two tabs, "Đối chiếu" and "Lịch sử".

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert | Error of the last action, with "Tải lại" when the data is outdated. |
| Alert "Tài khoản đã có liên kết tự thân đang hiệu lực (<mã>). Không thể xác minh thêm." | The link cannot be verified. |
| Alert "Tài khoản đang bị khóa." | Read-only warning. |
| **Tài khoản** | |
| Tên đăng nhập, Họ tên tài khoản | Read-only. |
| Email, Số điện thoại | Read-only, with the confirmation state. |
| Tạo lúc | Read-only. |
| **Thông tin tự khai** | |
| Họ tên, Ngày sinh, Mã hồ sơ | Read-only; "—" when empty. |
| Thông tin đối chiếu | Read-only text from the customer. |
| Gửi lúc | Read-only. |
| **Hồ sơ đã liên kết** | |
| Mã hồ sơ, Họ tên, Ngày sinh, SĐT | Read-only. Shown when a record is already linked. |
| **Tra cứu hồ sơ** | |
| Search | Code, name or phone, button "Tra cứu". Empty text "Không tìm thấy hồ sơ phù hợp." |
| Table | Mã hồ sơ, Họ tên, Ngày sinh, SĐT, Đối chiếu (what matches or differs). |
| Alert | "Hồ sơ đã chọn lệch … so với thông tin tự khai — không thể xác minh." The user should check the documents again, or reject the request with a reason. |
| **Decision** | |
| Kết quả đối chiếu (phương pháp / bằng chứng) | Required to verify; up to 1000 characters. Example "đối chiếu CCCD tại quầy lễ tân, khớp họ tên và ngày sinh". |
| Button "Xác minh" | Verifies after the confirmation "Xác nhận đúng hồ sơ tự thân?". Disabled when the record differs or the account already has a link. |
| Button "Từ chối" | Opens 8.3. |
| Button "Thu hồi liên kết" | For a verified link; opens 8.3. |
| Kết quả xử lý | Read-only: Người xác minh / từ chối, Người thu hồi, Rút yêu cầu, Kết quả đối chiếu (nội bộ), Lý do quyết định (nội bộ), Nội dung gửi người dùng. |
| **Tab "Lịch sử"** | |
| Timeline | Gửi yêu cầu, Rút yêu cầu, Xác minh, Từ chối, Thu hồi, Đọc hồ sơ tự thân. Empty text "Chưa có lịch sử." |

### 8.3 Reject or Revoke

Dialog "Từ chối yêu cầu liên kết" or "Thu hồi liên kết".

| Field Name | Description |
|---|---|
| Lý do (nội bộ) | Required. |
| Nội dung gửi người dùng (tùy chọn) | Must not contain other people's records or internal notes. |
| Button "Xác nhận" | Saves the decision. |

### 8.4 Family Record Drawer

This drawer ("Xác minh quyền xem hồ sơ người thân") allows staff to grant, reject or revoke a customer's access to the record of a relative. It has two tabs, "Đối chiếu và quyết định" and "Lịch sử xử lý".

| Field Name | Description |
|---|---|
| **Request** | |
| Người yêu cầu, Người thân (with the relationship) | Read-only. |
| Ngày sinh, Mã hồ sơ khai báo | Read-only; "Chưa cung cấp" when no code was given. |
| Thông tin đối chiếu | Read-only. |
| Hồ sơ đã cấp, Phạm vi, Hết hạn | Read-only. Shown once access is granted. |
| **Record lookup** | |
| Tra cứu hồ sơ người thân | Search, button "Tra cứu hồ sơ". Table Mã hồ sơ, Họ tên, Ngày sinh. |
| Alert | "Họ tên, ngày sinh hoặc mã hồ sơ không khớp thông tin người thân. Không thể cấp quyền cho hồ sơ này." |
| **Grant access** | |
| Chi nhánh được chia sẻ | Required. |
| Phạm vi được xem | Required, at least one scope. |
| Có hiệu lực đến ngày | Required, from today and within 364 days. |
| Căn cứ chia sẻ | Required: Sự đồng ý của bệnh nhân, or Căn cứ đại diện đã được phòng khám kiểm tra. |
| Kết quả đối chiếu và bằng chứng đã kiểm tra | Required, up to 1000 characters. |
| Button "Cấp quyền xem hồ sơ" | Grants access. Disabled when the record does not match. |
| **Reject or revoke** | |
| Lý do từ chối / thu hồi | Required, up to 500 characters. |
| Nội dung gửi người yêu cầu (không bắt buộc) | Up to 500 characters. |
| Button "Từ chối yêu cầu" / "Thu hồi quyền" | Rejects a pending request, or revokes granted access. |
| **Tab "Lịch sử xử lý"** | |
| Timeline | Time, user, action and reason. |
| Error | Alert with "Tải lại yêu cầu". |
