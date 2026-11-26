# III. Functional Requirements

## 9. Dentist: Patients

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "9.2" to "9.9").

A dentist uses the patient screens to read the record and to work on the examination, treatment and prescriptions of a patient. A dentist cannot create, edit or archive a patient record.

### 9.1 Patient List

The screen `/patients` is the same as 4.1 (Patient List), with one difference: the button "Thêm hồ sơ" and the button "Thêm hồ sơ mới" of the empty list are not shown, because only users who can manage patients see them.

### 9.2 Patient Profile

This screen (`/patients/:id`, title "HỒ SƠ KHÁCH HÀNG") allows a dentist to read one patient and to open the tabs of that patient. A tab appears only when the user has the permission to read it.

| Field Name | Description |
|---|---|
| **Header** | |
| Back arrow and "Đóng hồ sơ khách hàng" | Return to the patient list. |
| Banner | Read-only. Full name, patient code, age ("Chưa rõ tuổi" when unknown) and status "Đang sử dụng" or "Ngừng sử dụng". Button "Tải lại" reloads the record. |
| Alert "Hồ sơ đã ngừng sử dụng" | Read-only. Shown for an archived record. |
| Tabs | Thông tin cơ bản, Khám, Điều trị & Thanh toán, Hình ảnh, Lịch hẹn, Đơn thuốc. |
| **Tab "Thông tin cơ bản"** | |
| Mã bệnh nhân, Họ tên, Ngày sinh, Giới tính | Read-only; "Chưa rõ" when unknown. |
| Số điện thoại, Địa chỉ | Read-only. |
| Ngày lập hồ sơ, Nguồn, Phân nhóm khách hàng, Nghề nghiệp | Read-only. |
| Tiền sử (bệnh nhân khai) | Read-only. |
| Ghi chú hành chính | Read-only. |
| **Người giám hộ** | |
| Họ tên, Quan hệ, Số điện thoại | Read-only. "Không có người giám hộ." when empty. |
| **Liên kết tài khoản ứng dụng** | |
| Text | Read-only. "Đã liên kết với tài khoản …" or "Chưa có tài khoản nào được xác minh liên kết với hồ sơ này." |
| **History** | |
| Button "Xem lịch sử thay đổi" / "Ẩn lịch sử thay đổi" | Shows who changed what and when, the reason and each changed field (before → after). "Chưa có lịch sử thay đổi." when empty. |

The buttons "Sửa thông tin", "Ngừng sử dụng" and "Khôi phục hồ sơ" are not shown to a dentist. The tab "Lịch hẹn" is the same as in 4.3.

### 9.3 Tab "Khám"

This tab (title "Lịch sử khám") allows a dentist to see the visits of the patient and the examination form of each visit.

| Field Name | Description |
|---|---|
| **Filters** | |
| Tìm phiếu khám | Search by appointment, dentist or diagnosis ("Lịch hẹn, bác sĩ hoặc chẩn đoán"). |
| Trạng thái phiếu khám | "Tất cả phiếu", Chưa lập phiếu, Bản nháp, Đã hoàn tất phiếu khám, Đã kết thúc lượt khám. |
| Button "Tải lại phiếu khám" | Reloads the list. The list also reloads when the browser tab regains focus. |
| Link "Mở hàng chờ tiếp nhận" | Opens the reception list (5.1). |
| **List** | |
| Table | Columns Nội dung, Ngày đến / lịch hẹn, Bác sĩ / chi nhánh, Phiếu khám (Chưa lập, Bản nháp, Đã hoàn tất khám, with the version), Chẩn đoán / hướng xử trí, Lượt đến / kết thúc, Thao tác. |
| Button "Xem" | Opens 9.4. Shown when the visit has an examination form. |
| Link "Mở khám" / "Xem phiếu khám" | Opens the visit; "Mở khám" for a visit still in the queue. |
| Paging | 10 per page. |
| **Messages** | |
| Empty | "Chưa có lượt khám trong các chi nhánh được cấp quyền." |
| Limit | "Đang hiển thị 100 lượt gần nhất trong quyền truy cập." |
| Error | The error text with "Thử lại". |

### 9.4 Examination Content

This drawer ("Nội dung khám · <mã lịch>") allows a dentist to read the examination form of one visit.

| Field Name | Description |
|---|---|
| Header | Read-only. Dentist, branch and arrival time. |
| Lý do khám / triệu chứng | Read-only; "Chưa ghi" when empty. |
| Tiền sử, Dị ứng, Thuốc đang dùng | Read-only; "Chưa ghi" when empty. |
| Kết quả khám | Read-only. One line per tooth or region, with the surfaces and the finding. "Chưa ghi kết quả theo răng / vùng" when empty. |
| Chẩn đoán / kết luận | Read-only. |
| Dặn dò / lời khuyên | Read-only. |
| Hướng xử trí | Read-only. |
| Nhu cầu quay lại | Read-only. Shown when recorded. |
| Nơi / lý do chuyển khám | Read-only. Shown when recorded. |
| Ghi chú | Read-only. |
| Link "Mở phiếu khám gốc" | Opens the visit. |

### 9.5 Tab "Điều trị & Thanh toán"

This tab (title "DS PHIẾU ĐIỀU TRỊ/DỊCH VỤ") allows a dentist to:

- List the treatment sheets of the patient and open one.
- Write the treatment plan and the work done inside the open sheet.
- Change the status of a sheet, delete an empty sheet, print it, or create a new one.

The tab is for users with the role Dentist and the permission to read treatment. Others see "Bạn chưa được cấp quyền xem hồ sơ điều trị." Each button below is disabled when the user lacks its permission, and all editing is disabled when the sheet is closed (not editable).

| Field Name | Description |
|---|---|
| **Header** | |
| Patient line | Read-only. "<Họ tên> (MSKH: <mã>, <n> tuổi)", or "chưa rõ tuổi". |
| Alert | Error of loading the list, with "Thử lại". |
| **Sheet card (one per sheet)** | |
| Ngày lập phiếu | Read-only. Creation time when the card is open, otherwise "Phiếu <mã>". |
| Status button | "Chưa xong", "Hoàn tất" or "Đã ngừng". Menu "Chưa xong" / "Hoàn tất" leads to 9.6. "Chưa xong" does nothing on a sheet that is already open. |
| Icon button "Xóa phiếu" | Opens the delete dialog of 9.6. |
| Icon button "In phiếu đang mở" | Prints the open sheet; disabled until the sheet is loaded. |
| Button "+" / "−" | Opens or collapses the sheet. Only one sheet is open at a time; the newest is open by default. |
| Alert | Error of changing the status, with "Tải lại". |
| **Open sheet** | See 9.5.1. |
| **Paging** | |
| Buttons "Xem thêm phiếu", "Trang trước" | Page through the sheets, 20 per page. |
| **Create a sheet** | |
| Chi nhánh | Required. Choose among the branches of the dentist; fixed when the tab is opened from a visit, and chosen automatically when there is only one. |
| Button "+ TẠO PHIẾU MỚI" | Creates an empty sheet and opens it. Asks "Bỏ các thay đổi chưa lưu để tạo phiếu mới?" when the open sheet has unsaved changes. |
| Alert | Error of creating, with "Thử lại". |
| **Messages** | |
| Empty | "Chưa có phiếu điều trị. Bác sĩ có thể tạo phiếu mới cho bệnh nhân này." |

#### 9.5.1 Open Treatment Sheet

The body of an open sheet, shown inside the card. It has the sections KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ, QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ, HÌNH ẢNH and GHI CHÚ.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert | Error of the last action, with "Đọc lại phiên bản" when the sheet was changed elsewhere. |
| Alert (copy) | Error of copying a plan row, with "Thử lại sao chép" or "Đọc lại phiên bản sao chép". |
| Link "Mở nguồn khám" | Opens the visit the sheet was created from. Shown when there is one. |
| **KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ** | |
| Button "Kế hoạch điều trị" | Opens 9.10 to add the rows of a plan template. Disabled while a row is being edited. |
| Tổng dự toán, Giảm giá, Thành tiền | Read-only. Total of the agreed rows, approved discounts, and the amount to pay. |
| Table | Columns Ngày, Răng, Điều trị/dịch vụ, Thời gian dự kiến (phút), Ghi chú, Bác sĩ & phụ tá, Chi phí, Giảm giá. Rows are sorted by date, then by order; rows without a date come last. A draft row shows the tag "Nháp" and the button "Sửa nháp". |
| Button "+" (column header) | Adds a plan row (9.5.2). |
| Menu "⋯" of a row | "Thực hiện" (moves the row to the work done, 9.5.3), "Hoàn tất" / "Bỏ hoàn tất", "Sao chép", "Sửa", "Xóa". "Xóa" asks "Bạn có muốn xóa dòng này không?" ("Đồng ý", "Không đồng ý"). |
| Empty row | "Chưa có hạng mục. Nhấn + để thêm." |
| **QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ** | |
| Table | Columns Ngày, Răng, Điều trị/dịch vụ, Ghi chú, Bác sĩ & phụ tá, Labo, Chi phí, Giảm giá. Cost shows the fee state (Chưa ghi nhận phí, Đã ghi nhận phí, Đã bao gồm, Miễn phí, Chưa yêu cầu phí, Chờ quy tắc phí gói, Chờ điều chỉnh phí) and "Phí labo" when there is one. |
| Icon "Chọn vật liệu dùng" | In the Ghi chú cell. Opens 9.12. |
| Icon "Phiếu labo" | In the Labo cell. Opens 9.11. |
| Buttons "✎", "×" | Edit a row (9.5.3), or delete it after the confirmation "Bạn có muốn xóa dòng này không?". |
| Button "+ Thêm việc đã làm" | Adds a row of work done (9.5.3). |
| Button "Trả tiền" | Opens the payment dialog. Shown to the clinic owner only, so a dentist does not see it. |
| Paging | Shown when there are more than 20 rows. |
| Empty row | "Phiếu chưa có việc điều trị." |
| **HÌNH ẢNH** | |
| Image gallery | Images of this sheet. Shown when the user may read documents; the upload button needs the permission to upload. |
| **GHI CHÚ** | |
| Ghi chú hồ sơ | Up to 4000 characters. |
| Button "Lưu ghi chú" | Saves the note; disabled while the note is unchanged. The browser warns when leaving with an unsaved note. |

#### 9.5.2 Plan Row Editor

An editable row in the plan table.

| Field Name | Description |
|---|---|
| Ngày | Date of the planned work; optional. |
| Răng | Tooth numbers in FDI, separated by comma, for example "25, 26, 46"; up to 100 characters. Message "Nhập mã FDI, ví dụ 25, 26, 46." when a code is wrong. |
| Điều trị/dịch vụ | Required. Search among the services of the branch. Picking a service fills Chi phí from its reference price. |
| Thời gian dự kiến (phút) | Whole number from 1 to 480. Message "Nhập số phút nguyên từ 1 đến 480." |
| Ghi chú | Up to 1000 characters. |
| Bác sĩ | Required; default is the dentist of the sheet. |
| Phụ tá | Optional. |
| Chi phí | Required, VND, not negative. Disabled when editing an existing row without the permission to set prices. |
| Giảm giá | Amount, with the type "₫" or "%" and "Lý do giảm trừ" (up to 1000 characters). Disabled when editing an existing row without the permission to set prices. |
| Buttons "✓", "×" | Save the row, or close it ("Bỏ nội dung hàng chưa lưu?" when changed). "Đọc lại phiên bản" appears with an error. |

#### 9.5.3 Work-done Row Editor

The same row, used for "Thêm việc đã làm", "Thực hiện" and "✎". It differs from 9.5.2 in these fields.

| Field Name | Description |
|---|---|
| Ngày | Date of the work; the current day when moving a plan row to the work done. |
| Thời gian dự kiến | Not shown. |
| Ghi chú | Up to 1000 characters, with the icon "Chọn vật liệu dùng" (9.12). |
| Labo | "Ghi chú Labo" / "Công việc Labo", up to 1000 characters. |

### 9.6 Close, Reopen and Delete a Sheet

Dialogs opened from 9.5.

| Field Name | Description |
|---|---|
| **Dialog "Kết thúc hồ sơ điều trị" / "Mở lại hồ sơ điều trị"** | |
| Kết quả điều trị | Required when closing: Hoàn tất đúng thỏa thuận, Hoàn tất theo phạm vi đã điều chỉnh, Ngừng khi còn phần chưa làm. |
| Alert | "Còn n hạng mục chưa hoàn tất; cần xử lý trước khi đóng." |
| Xử lý: <tên dịch vụ> | Required for each unfinished item: Dừng phần còn lại, Hủy phần chưa thực hiện, Bỏ qua theo quyết định đã ghi nhận. |
| Lý do | Required when the outcome is not "Hoàn tất đúng thỏa thuận", and when reopening. |
| Buttons "Hủy", "Xác nhận kết thúc" / "Mở lại" | Close the dialog, or save. |
| **Dialog "Xóa phiếu <mã>?"** | |
| Note | Only an empty sheet, without plan or treatment data, can be deleted. A sheet with work needs the closing procedure. |
| Buttons "Hủy", "Xóa phiếu" | Close the dialog, or delete. |

### 9.7 Tab "Hình ảnh"

This tab allows a dentist to look at the images of the patient. Uploading is done inside a treatment sheet (9.5).

| Field Name | Description |
|---|---|
| Image grid | Thumbnails of the images with the file name, 20 per page. An image still being scanned or rejected shows its state, and the reason when rejected. |
| Button "Thử lại" | Reloads an image or the list after an error. |
| Empty text | "Chưa có hình ảnh." |

### 9.8 Tab "Đơn thuốc"

This tab allows a dentist to list the prescriptions of the patient, to read one and to write a new one. Prescriptions are always for medicines that the patient buys outside the clinic.

| Field Name | Description |
|---|---|
| Alert (error) | Read-only. The error message when the list cannot be loaded. |
| Loading | A spinner until the list is loaded. |
| Button "+ Kê đơn thuốc" | Opens 9.9 to write a prescription. Shown only with the permission to manage prescriptions; disabled until the branches and dentists are loaded. |
| **Table** | 8 prescriptions per page, the newest first. |
| Ngày kê | Read-only. dd/mm/yyyy. |
| Chẩn đoán | Read-only. The diagnoses and "Chẩn đoán khác" separated by " · ", or "—". |
| Thuốc | Read-only. Names of the medicines, separated by commas. |
| Bác sĩ kê đơn | Read-only. |
| Xem | Button. Opens 9.9 to read the prescription. |
| Sửa | Button. Opens 9.9 to edit the prescription. Shown only with the permission to manage prescriptions. |
| Empty | "Chưa có đơn thuốc. Bấm Kê đơn thuốc để bắt đầu." ("Chưa có đơn thuốc." without the permission to manage). |
| Message | After saving: "Đã lưu đơn thuốc". |

### 9.9 Prescription Sheet

This dialog (sheet "ĐƠN THUỐC") allows a dentist to read, write or edit a prescription. The dialog is closed only with the buttons, not by clicking outside it.

| Field Name | Description |
|---|---|
| **Header** | |
| Chi nhánh kê đơn | Select, shown when the dentist works in more than one branch. Changing the branch reloads the medicines and the dentists of the branch. Otherwise the branch name is shown as text. |
| Số điện thoại, Email | Read-only. Contact of the branch, shown when it has them. |
| Alert (error) | Read-only. "Bác sĩ chưa được phân công chi nhánh để kê đơn." or the save error. |
| **BỆNH NHÂN** | |
| Họ và tên, Mã BN, Ngày sinh, Giới tính | Read-only. Taken from the patient record. |
| Chẩn đoán | Multi-value select, placeholder "Chọn hoặc nhập chẩn đoán". The options are the diagnoses of the catalogue ("name (code)"); the dentist can also type a new value (a semicolon separates values). Saved as one text, values separated by "; ". |
| Chẩn đoán khác | Text, up to 1,000 characters. |
| **CHỈ ĐỊNH** | A numbered list of medicine lines, at least 1 and at most 30. |
| Tên thuốc | Required select with search, placeholder "Tên thuốc". The options are the active medicines of the branch ("name · strength"), up to 50, sorted by name. Message "Chọn thuốc". |
| SL | Required whole number from 1 to 10,000. Messages "Nhập số lượng" and "1–10.000". |
| Đơn vị thuốc | Required text typed by the dentist, up to 40 characters, placeholder "Đơn vị thuốc (tự nhập), ví dụ: viên, tuýp". Message "Nhập đơn vị thuốc". It is cleared when another medicine is chosen. |
| Hướng dẫn sử dụng | Text, up to 1,000 characters. When the dentist chooses a medicine and the field is empty, the default instructions of the medicine are filled in. |
| ✕ | Button. Removes the line. Disabled when only one line is left. |
| + THÊM | Button. Adds an empty line. Disabled at 30 lines. |
| **GHI CHÚ/LỜI DẶN** | |
| Ghi chú, lời dặn | Text area, up to 4,000 characters. |
| **NGÀY & BÁC SĨ KÊ ĐƠN** | |
| Ngày | Required date, dd/mm/yyyy, default today, cannot be after today. |
| Bác sĩ | Required select of the dentists of the chosen branch. Default is the signed-in dentist. When the branch changes and the dentist does not work there, the first dentist of the branch is chosen. |
| **Actions** | |
| Hủy, Lưu đơn thuốc | Buttons. Close the dialog, or save the new or edited prescription. Editing a prescription that was changed elsewhere is refused (MSG111). |
| Đóng | Button. The only action when reading a prescription. |

### 9.10 Choose a Treatment Plan

Dialog "Chọn kế hoạch điều trị", opened from the button "Kế hoạch điều trị" of 9.5.1. It allows a dentist to add all rows of a plan template to the sheet. The dentist of the sheet becomes the dentist of every added row.

| Field Name | Description |
|---|---|
| Note | "Các hạng mục sẽ được thêm vào cuối bảng hiện tại. Bác sĩ phụ trách sử dụng bác sĩ của phiếu." |
| Alert | Error of loading or applying, with "Tải lại nguồn" when the plan or the sheet was changed. |
| Chọn kế hoạch điều trị | Required. Search among the plans of the branch; each option shows the name and the number of rows. Paging 20 per page. |
| Preview | Name and "Tổng chi phí" of the plan, then a table of Ngày, Răng, Điều trị/dịch vụ, Thời gian dự kiến (phút), Ghi chú, Bác sĩ & phụ tá, Chi phí. "Kế hoạch chưa có hạng mục." when empty. |
| Buttons "Hủy", "Áp dụng" | Close, or add the rows. "Áp dụng" is disabled when no plan with rows is chosen. |

### 9.11 Lab Order

Dialog "PHIẾU LABO", opened from the icon "Phiếu labo" of a work-done row. It allows a dentist to record the lab work of that row. Without the permission to manage lab orders it is read-only.

| Field Name | Description |
|---|---|
| **Actions** | |
| Buttons "IN", "XÓA", "LƯU LẠI", "✕" | Print; delete the order after "Xóa phiếu labo này?" (only an existing order); save; close ("Phiếu labo có thay đổi chưa lưu. Đóng không lưu?"). |
| Alert | Error of loading with "Thử lại", or of saving. |
| **Header** | |
| Mã số | Up to 40 characters. |
| Ngày | Required. Message "Vui lòng nhập Ngày." |
| Hẹn, Giao | Date; not before Ngày. Messages "Hẹn không được trước Ngày." and "Giao không được trước Ngày." |
| **Order** | |
| Khách hàng | Read-only. Name, MSKH, age and gender. |
| Bác sĩ | Required; default is the dentist of the sheet. Message "Vui lòng chọn Bác sĩ." |
| Cty cung cấp | Optional choice of the lab. |
| Công việc labo | Required, up to 160 characters. A choice of work type fills the name, the lab, the fee and the warranty from the catalogue. Message "Vui lòng nhập Công việc labo." |
| Đường hoàn tất | Mặt ngoài, Mặt trong, Mặt bên; each Vai, Vát, Lưỡi dao or Mỏng dần. |
| Nhịp cầu răng | Hình yên ngựa, Hình bán yên ngựa, Hình trứng, Hình gờ T; several allowed. |
| Màu | Six shade zones, each up to 20 characters. |
| Răng | Choose teeth on the upper and the lower jaw in FDI numbers. Checkbox "Trẻ em" switches to milk teeth and clears the choice. |
| Ghi chú | Up to 1000 characters. |
| Chất liệu | Free text with suggestions (Sứ kim loại, Titanium, Zirconia, Emax, Nhựa, Khung kim loại). |
| Số lượng | Whole number from 1 to 999. |
| Tổng phí (₫), Đã trả (₫) | Not negative. Message "Đã trả không được lớn hơn Tổng phí." |
| Bảo hành | Up to 100 characters, for example "Trọn đời, 7 năm, 24 tháng...". |

### 9.12 Materials Used

Dialog "Vật liệu dùng", opened from the icon "Chọn vật liệu dùng". It allows a dentist to add the materials used to the note of the row. On a saved row they are also deducted from the stock.

| Field Name | Description |
|---|---|
| Note | "Chọn dụng cụ/vật liệu và số lượng để bổ sung vào ghi chú." |
| Alert | "Vui lòng thêm ít nhất một vật liệu.", the error of saving, or "Ghi chú sau khi thêm có n ký tự, vượt giới hạn 1.000…". |
| Tên dụng cụ/vật liệu | Required, one line per material. Search in the stock; each option shows the category, the stock and the unit. A material already chosen is disabled. "Kho chưa có vật liệu nào phù hợp." when empty; "Tải thêm vật liệu" and "Thử lại danh mục" when needed. |
| Số lượng | Required, positive, up to 3 decimals. Message "Nhập số lượng dương, tối đa 3 chữ số thập phân." |
| Button bin | Removes the line. |
| Button "Thêm vật liệu" | Adds a line. |
| Text | "Ghi chú hiện tại: n/1.000 ký tự." |
| Buttons "Hủy", "Lưu" | Close, or append "Vật liệu dùng: <tên> × <số lượng>; …" to the note. |
