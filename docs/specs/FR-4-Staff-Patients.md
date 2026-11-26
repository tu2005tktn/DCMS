# III. Functional Requirements

## 4. Staff Patients

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "4.1" to "4.4").

### 4.1 Patient List

This screen (`/patients`) allows staff to search patients and to open or create a patient record.

| Field Name | Description |
|---|---|
| Button "Thêm hồ sơ" | Opens the patient form drawer (4.2). Shown to users who can manage patients. |
| Từ khóa | Search by patient code, name (with or without diacritics) or phone. |
| Chi nhánh | "Tất cả chi nhánh" or one branch. |
| Ngày sinh | Filter by date of birth. |
| Checkbox "Gồm hồ sơ ngừng sử dụng" | Includes archived records. |
| Buttons "Tìm kiếm", "Đặt lại", "Tải lại" | Apply the filters, clear them, reload the list. |
| Table "Danh sách bệnh nhân" | Shows the number of records. Columns Mã bệnh nhân, Họ tên, Ngày sinh ("Chưa rõ" when unknown), Giới tính ("Chưa rõ"), Số điện thoại, Lần khám gần nhất ("Chưa có dữ liệu"), Trạng thái (Đang sử dụng or Ngừng sử dụng) and the link "Mở hồ sơ". Clicking a row opens the record. Page size 20, 50 or 100. |
| Messages | "Chưa tải được dữ liệu" with "Thử lại" on error; when the list is empty the button "Thêm hồ sơ mới". |

### 4.2 Patient Form

This drawer ("Thêm hồ sơ bệnh nhân", also used to edit a record) allows staff to:

- Create a patient record, or correct an existing one.
- Review the possible duplicate records before saving.
- Record the guardian of a patient who needs one.

Which fields are required follows the clinic's configuration of the patient record.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert "Có hồ sơ nghi trùng" | Read-only. Lists the possible duplicates (Mã, Họ tên, Ngày sinh, SĐT, Lý do, with "Ngừng sử dụng" for an archived one) that the user must review before saving. |
| Alert | Read-only. Shows the save error, the failure to load the clinic's patient settings, or the failure to load a catalogue, each with "Thử lại" when it can be retried. |
| **Thông tin chính** | |
| Mã bệnh nhân | Placeholder "Tự động". Up to 40 characters. Disabled when the clinic generates codes; required when the clinic requires manual codes ("Phòng khám yêu cầu nhập mã thủ công."). Read-only when editing. |
| Họ tên | Required. Message "Vui lòng nhập Họ tên." when empty; up to 100 characters. |
| Ngày sinh | Date, format dd/mm/yyyy. Leave empty if unknown ("Để trống nếu chưa rõ."), unless the clinic requires it. |
| Giới tính | Choice, "Chưa rõ" is the default. Message "Vui lòng nhập Giới tính." when the clinic requires it. |
| Số điện thoại | Up to 20 characters, valid format. A phone number may be shared with other people and is not used to merge records. |
| Địa chỉ | Up to 255 characters. Message "Vui lòng nhập Địa chỉ." when the clinic requires it. |
| **Thông tin bổ sung** | |
| Ngày lập hồ sơ | Date, not in the future. Button "Hôm nay" fills today's date. |
| Nguồn | Choice from the catalogue; "Chưa chọn" by default. Message "Vui lòng chọn Nguồn." when required. |
| Phân nhóm khách hàng | Choice from the catalogue; "Chưa chọn" by default. Message "Vui lòng chọn Phân nhóm khách hàng." when required. |
| Nghề nghiệp | Optional choice from the catalogue. |
| Tiền sử (bệnh nhân tự khai) | Multiple choice. Reference only; the dentist still records the health condition at the examination. |
| Tiền sử khác | Up to 1000 characters. |
| **Người giám hộ** | |
| Tên người giám hộ | Up to 160 characters. Requires Quan hệ với bệnh nhân when filled. |
| Quan hệ với bệnh nhân | Up to 40 characters, for example "Mẹ". Message "Vui lòng nhập quan hệ với bệnh nhân." when the name is filled and this is empty. |
| SĐT người giám hộ | Up to 20 characters, valid format. |
| Ghi chú hành chính | Up to 1000 characters. |
| **Actions** | |
| Button "Hủy" | Closes the drawer. With unsaved changes it asks "Bỏ thay đổi chưa lưu?" ("Thông tin vừa nhập sẽ không được lưu.", buttons "Bỏ thay đổi" and "Tiếp tục sửa"). |
| Button "Lưu" | Saves the record. |

### 4.3 Patient Profile

This screen (`/patients/:id`, title "HỒ SƠ KHÁCH HÀNG") shows one patient in tabs. Each tab appears only when the user has the permission to read it.

| Field Name | Description |
|---|---|
| Back arrow and "Đóng hồ sơ khách hàng" | Return to the list. |
| Banner | Full name, patient code, age ("Chưa rõ tuổi" when unknown), status Đang sử dụng or Ngừng sử dụng, and the button "Tải lại". |
| Alert "Hồ sơ đã ngừng sử dụng" | Shown for an archived record. |
| Tab "Thông tin cơ bản" | Buttons "Sửa thông tin" and "Ngừng sử dụng" (or "Khôi phục hồ sơ"). Fields: Mã bệnh nhân, Họ tên, Ngày sinh, Giới tính, Số điện thoại, Địa chỉ, Ngày lập hồ sơ, Nguồn, Phân nhóm khách hàng, Nghề nghiệp, Tiền sử (bệnh nhân khai), Ghi chú hành chính. |
| Section "Người giám hộ" | Họ tên, Quan hệ, Số điện thoại; "Không có người giám hộ." when empty. |
| Section "Liên kết tài khoản ứng dụng" | "Đã liên kết với tài khoản …" or "Chưa có tài khoản nào được xác minh liên kết với hồ sơ này." |
| Button "Xem lịch sử thay đổi" / "Ẩn lịch sử thay đổi" | Shows who changed what and when, with the reason and each changed field (before → after); "Chưa có lịch sử thay đổi." when empty. |
| Tab "Khám" | The patient's examinations. |
| Tab "Điều trị & Thanh toán" | The treatment dossiers and payments. |
| Tab "Hình ảnh" | The patient's image gallery. |
| Tab "Lịch hẹn" | Filter Tất cả, Sắp tới, Lịch sử; button "Tải lại lịch hẹn"; table Mã lịch hẹn, Thời gian, Trạng thái, Chi nhánh / ghế, Nha sĩ, Dịch vụ, Thao tác; empty text "Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn." |
| Tab "Đơn thuốc" | The patient's prescriptions. |

The contents of the tabs Khám, Điều trị & Thanh toán, Hình ảnh and Đơn thuốc are described with their own screens.

### 4.4 Archive and Restore

Dialog opened from the tab "Thông tin cơ bản".

| Field Name | Description |
|---|---|
| Title | "Ngừng sử dụng hồ sơ <mã> · <họ tên>" or "Khôi phục hồ sơ <mã> · <họ tên>". |
| Note | Archive: the record is not deleted, history and links are kept, it is hidden from the default search and cannot be edited until restored. Restore: "Hồ sơ sẽ dùng lại được cho tìm kiếm và chỉnh sửa." |
| Lý do | Required, up to 500 characters. |
| Buttons "Hủy", "Ngừng sử dụng" or "Khôi phục" | Close or confirm. |
