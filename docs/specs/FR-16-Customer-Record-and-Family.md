# III. Functional Requirements

## 16. Customer: Medical Record and Family Members

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "16.1" to "16.4"; the screen and the drawer of 16.2 are frames "16.2" and "16.3", the window 16.3 is frame "16.4").

### 16.1 Medical Record

This screen (`/customer/treatment-records`, navigation item "Hồ sơ điều trị", title "Hồ sơ bệnh án") allows the customer to see their own medical record that the clinic has linked to the account: basic information, examinations, treatment and payments, images and prescriptions. Everything is read-only.

When the account is not yet linked to a patient record the screen shows "Hãy xác minh liên kết hồ sơ để xem bệnh án." (see the screen "Liên kết hồ sơ"). A loading spinner is shown until the data is loaded; on error the message is shown with the button "Thử lại".

| Field Name | Description |
|---|---|
| **Header card** | |
| Họ tên | Read-only. Full name of the patient. |
| Mã bệnh nhân | Read-only. |
| **Tab "Thông tin cơ bản"** | |
| Mã bệnh nhân, Họ tên, Ngày sinh, Giới tính, Số điện thoại, Địa chỉ, Người giám hộ | Read-only. "—" when empty. |
| **Tab "Khám"** | |
| Visit list | Read-only. One item for each visit: date and branch, "Bác sĩ [name] · [status]", and "Chẩn đoán: …" when there is one. Empty: "Chưa có lượt khám". |
| Link "Xem kết quả khám" | Opens the result under the item: Triệu chứng, Chẩn đoán, Kết luận, Lời dặn, Tái khám, Hướng xử trí, Chuyển khám (only when there is one), each finding ("Răng n" or the region: condition · diagnoses) and each investigation result (name: result · interpretation). |
| **Tab "Điều trị & Thanh toán"** | |
| Card of a treatment sheet | Title "[code] · [diagnosis]", a tag "Đang điều trị" or "Đã kết thúc", and "Bắt đầu dd/mm/yyyy". Empty: "Chưa có hồ sơ điều trị đã xác nhận". |
| Hạng mục bác sĩ đã làm | Read-only. Service name · region, date · quantity unit, and "Kết quả: …". Empty: "Chưa có hạng mục đã ghi nhận". |
| Kế hoạch đã đồng ý | Read-only. Service name · region · quantity unit, with a tag of the progress. Empty: "Chưa có hạng mục đã đồng ý". |
| Thanh toán đã ghi | Read-only. Date · method, and the amount in đ. Empty: "Chưa có khoản thanh toán trên phiếu điều trị". |
| **Tab "Hình ảnh"** | |
| Image list | Read-only. File name · date, with the button "Xem ảnh" that opens the image in a window. Only images that the clinic has published are shown. Empty: "Chưa có hình ảnh đã công bố". Error "Không mở được hình ảnh." |
| **Tab "Đơn thuốc"** | |
| Prescription list | Read-only. "dd/mm/yyyy · Bác sĩ [name]", the diagnosis, and each medicine: name · quantity unit · instructions. Empty: "Chưa có đơn thuốc". |

### 16.2 My Family Members

This screen (`/customer/family-members`, navigation item "Người thân của tôi") allows the customer to keep a list of family members and to book or see the record for them.

The drawer ("Thêm người thân" or "Sửa người thân") allows the customer to enter the information of a family member. The browser asks "Bỏ các thay đổi chưa lưu?" when the drawer is closed with unsaved changes.

| Field Name | Description |
|---|---|
| **Header** | |
| Button "Thêm người thân" | Opens the drawer below. |
| **Search** | |
| Tìm theo họ tên | Text, up to 100 characters, clearable. The search runs when the user presses Enter or clicks "Tìm kiếm". |
| Alert (error) | Read-only. The error message, with the button "Thử lại". |
| **Table** (cards on a phone) | |
| Họ tên | Read-only. |
| Quan hệ | Read-only. Bố, Mẹ, Vợ/Chồng, Con, Anh/Chị/Em, Ông/Bà or Khác. |
| Ngày sinh | Read-only. dd/mm/yyyy or "Chưa cung cấp". |
| Giới tính | Read-only. "Chưa cung cấp" when empty. |
| Số điện thoại liên hệ | Read-only. "Chưa cung cấp" when empty. |
| Link "Đặt lịch" | Opens the booking wizard (2.3) with this family member already chosen. |
| Button "Xem hồ sơ" | Opens the window 16.3. |
| Button "Sửa" | Opens the drawer below with the member. |
| Button "Xóa" | Asks "Bạn có muốn xóa người thân này khỏi danh sách không?" (Đồng ý / Không đồng ý). Sending again after an error does not delete twice. |
| Empty | "Chưa có người thân phù hợp. Bạn có thể thêm người thân mới." ("Chưa tải được danh sách." when loading failed). |
| Paging | 20 members per page. |
| **Drawer "Thêm người thân" / "Sửa người thân"** | |
| Alert (error) | Read-only. The error message. |
| Xem phiên bản mới | Button, shown when the member was changed elsewhere (MSG111). Shows "Thông tin mới nhất" (Họ tên, Quan hệ, Ngày sinh, Giới tính, Số điện thoại liên hệ) with the buttons "Dùng thông tin mới" and "Giữ bản nhập để lưu lại". |
| Họ tên | Required, up to 100 characters. Messages "Nhập họ tên người thân." and "Họ tên tối đa 100 ký tự." |
| Quan hệ | Required select, placeholder "Chọn quan hệ". Message "Chọn quan hệ." |
| Ngày sinh (không bắt buộc) | Date, cannot be after today. Message "Ngày sinh không hợp lệ hoặc sau ngày hiện tại." |
| Giới tính | Required select, placeholder "Chọn giới tính". Message "Chọn giới tính." |
| Số điện thoại liên hệ | Required, 10 digits of a Vietnamese mobile number, placeholder "Ví dụ: 0912345678". Messages "Nhập số điện thoại liên hệ." and "Nhập số di động Việt Nam gồm 10 chữ số." |
| Buttons "Hủy", "Lưu" | Close the drawer, or save. |

### 16.3 Family Member Record

This window ("Hồ sơ của [name]") opens from "Xem hồ sơ". It allows the customer to see the appointments booked for the family member and, when the clinic has granted access, the record of that person. The family member does not need a DCMS account. The window refreshes itself when it gets the focus and every 15 seconds.

| Field Name | Description |
|---|---|
| **Notices** | |
| Notice "Người thân không cần tài khoản DCMS" | Shown while no access is granted: the record is shown by default after the clinic has received the right family member from an appointment booked by the customer. |
| Alert (error) | Read-only. The error message, with the button "Thử lại". |
| **Access status** | |
| Tag | Read-only. Chưa có hồ sơ liên kết, Chờ phòng khám xác minh, Đã được cấp quyền, Bị từ chối, Đã thu hồi, Đã rút yêu cầu or Đã hết hạn. |
| Tải lại trạng thái | Button. |
| Clinic note | Read-only. The public note of the clinic, when there is one. |
| Text | For a pending request: "Yêu cầu đã gửi. Khi người thân đến khám, vui lòng phối hợp với phòng khám để đối chiếu danh tính và xác nhận phạm vi chia sẻ." With no access: "Hồ sơ khám sẽ tự hiển thị sau khi người thân được tiếp nhận. Nếu đã có hồ sơ từ trước, bạn có thể yêu cầu phòng khám liên kết bên dưới." |
| **Liên kết hồ sơ đã có tại phòng khám** | A collapsible block, shown only when a request can be sent. |
| Mã hồ sơ người thân (nếu biết) | Text, up to 40 characters. |
| Thông tin giúp phòng khám đối chiếu | Text, up to 1,000 characters, with a counter. |
| Yêu cầu quyền xem hồ sơ | Button. Sends the request. |
| Rút yêu cầu / Chấm dứt quyền truy cập | Button (red), shown for a pending or granted access. Asks "Rút yêu cầu xác minh?" or "Chấm dứt quyền xem hồ sơ?" (Xác nhận / Hủy). |
| **Record header** | Shown only when access is granted. |
| Bệnh nhân, Ngày sinh, Chi nhánh, Có hiệu lực đến | Read-only. When the access expires, the content is hidden and the status is loaded again. |
| **Tabs** | |
| Lịch đặt hộ | Always shown. Each appointment: the code (a link to Appointment Detail), time · status (Đã đặt, Đã đến khám, Chờ khám, Đang khám, Đã hoàn thành, Đã hủy, Không đến), branch · dentist. Empty: "Chưa có lịch đặt hộ được liên kết với người thân này." |
| Lịch sử khám | Only when the scope "Lịch sử khám" is granted. One collapsible item per visit (date · dentist): Chi nhánh, Triệu chứng, Chẩn đoán, Kết luận, Lời dặn. |
| Điều trị | Only when the scope "Điều trị" is granted. Per sheet: "[code] · date", the diagnosis, and each service with its progress (Chưa thực hiện, Đang thực hiện, Đã hoàn thành, Một phần, Đã hủy, Đã dừng) and the estimated dates. |
| Đơn thuốc | Only when the scope "Đơn thuốc" is granted. Per prescription: "Đơn ngày …", the diagnosis, each medicine (name, strength, quantity, route, days, morning/noon/afternoon/evening dose, meal timing, instructions) and "Lời dặn". |
| Ảnh và tài liệu | Only when the scope "Ảnh và tài liệu" is granted. File name, date, size in KB and the button "Tải tài liệu". Error "Không thể tải tài liệu. Quyền truy cập có thể đã thay đổi, vui lòng tải lại." |
| Empty (other tabs) | "Chưa có nội dung chính thức trong phạm vi đã cấp." Each tab has 10 items per page. |
| **Lịch sử yêu cầu** | A collapsible block with each earlier request: its status, the time and the public note. |
| Button "Đóng" | Closes the window. |
