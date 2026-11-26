# III. Functional Requirements

## 10. Dentist: Examination

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "10.1" to "10.6").

### 10.1 Examination Page

This screen (`/visits/:visitId`, title "Khám", subtitle "Phiếu khám · Kết quả, chẩn đoán và hướng xử trí") allows the dentist in charge of a visit to:

- Start the examination of a patient who waits in the queue.
- Record the examination form: symptoms, health facts, findings by tooth or region, diagnosis, advice and the next step.
- Order and review investigations (X-ray, lab, other).
- Look at the images and documents of the visit.
- Finish the examination form, amend it afterwards, and end the visit.

The screen needs the permission to read examinations. Which buttons work depends on the state of the visit and the permissions of the user (`allowedActions`); a user who cannot save sees the form read-only. A visit for treatment continuation does not show this screen: the user is sent to the treatment dossier chosen at reception, or sees "Lượt điều trị chưa được liên kết hồ sơ khi tiếp nhận…" with the link "Mở các hồ sơ điều trị".

| Field Name | Description |
|---|---|
| **Header** | |
| Link "Các phiếu khám" | Opens the tab "Khám" of the patient (9.3). |
| Link "Về hàng chờ" | Opens the reception list (5.1). |
| Button "Tải lại phiên bản" | Reloads the examination form. Typed content that is not saved stays. |
| **Notices** | |
| Alert "Đã lưu lịch sử khám vào hồ sơ bệnh nhân" | Read-only. Shown when the visit is finished and the form is confirmed, with the link "Xem lịch sử khám của bệnh nhân". |
| Alert | Read-only. Error of the last action. "Lượt đã thay đổi. Bản nhập vẫn còn; tải lại phiên bản trước khi lưu tiếp." when the visit changed elsewhere. |
| Prompt "Rời phiếu khám và bỏ thay đổi chưa lưu?" | Shown when the user follows a link with unsaved changes. The browser also warns when the page is closed. |
| **Patient** | |
| Title | Read-only. "<Mã bệnh nhân> · <Họ tên>". |
| Tuổi, Giới tính, Ngày sinh | Read-only; "Chưa rõ" when unknown, "Dưới 1 tuổi" for an infant. |
| Tags | Read-only. "Chưa lập phiếu", "Phiếu nháp" or "Đã hoàn tất khám"; "Phiên bản n"; "Lượt đến: <trạng thái>"; "Chờ kết quả cận lâm sàng" or "Chờ bác sĩ đọc kết quả" when a required investigation is open. |
| Lịch hẹn, Bác sĩ, Chi nhánh | Read-only. |
| Giờ đến, Mục đích | Read-only. |
| Thông tin tiếp nhận | Read-only; "Chưa có" when empty. |
| Hồ sơ cũ | Read-only; "Chưa chọn hồ sơ" when empty. A warning shows the reason when the old dossier cannot be used. |
| Button "Bắt đầu khám" | Moves the visit from "Chờ khám" to "Đang khám". Shown only for a visit in the queue. |
| **Tabs** | "Khám & sơ đồ răng" (10.2), "Cận lâm sàng" (10.3), "Ảnh & tài liệu" (10.4, shown to users who may read documents), "Tổng kết khám" (10.5), "Lịch sử khám" (10.6). |

### 10.2 Tab "Khám & sơ đồ răng"

This tab allows the dentist to record the examination. After the form is confirmed, the tab shows "Đã hoàn tất khám" and is read-only unless the dentist amends it.

| Field Name | Description |
|---|---|
| **Information** | |
| Lý do khám / triệu chứng | Required for finishing. Up to 4000 characters. |
| Tiền sử | Up to 4000 characters. Placeholder "Có thông tin thì ghi vào, không có thì để trống". Empty means "unknown". |
| Dị ứng | Same as Tiền sử. |
| Thuốc đang dùng | Same as Tiền sử. |
| **Sơ đồ răng (FDI)** | |
| Bộ răng hiển thị | Răng vĩnh viễn (default), Răng sữa, Răng hỗn hợp. The default follows the teeth already recorded. |
| Chế độ sơ đồ | Sơ đồ 2D, or Mô hình 3D. If the 3D model cannot load, "Không tải được mô hình 3D." with "Về sơ đồ 2D". |
| Tooth buttons | One button per tooth in FDI number, upper and lower jaw, patient's right and left. Click to select, click again to unselect. "○ Chưa có ghi nhận", "● Có kết quả", "◉ Đang chọn". Hover shows the recorded finding. Not recorded does not mean normal. |
| Alert | "Có n kết quả thuộc bộ răng đang ẩn…" with "Xem cả hai bộ răng". |
| Đang chọn | Read-only. The selected teeth or "Chưa chọn răng", with "Bỏ chọn". |
| Chẩn đoán cho răng đang chọn | Several values. Choose from the diagnosis catalogue of the branch, or type. "Không tải được danh mục chẩn đoán" with "Thử lại" when the catalogue fails. |
| Mô tả phát hiện / ghi chú bổ sung | Up to 2000 characters. |
| Bề mặt răng | Nhai / cắn, Ngoài, Trong, Gần, Xa. Several allowed. |
| Button "Áp dụng cho n răng" | Adds one finding per selected tooth. Needs a diagnosis or a description. A tooth that already has the same finding is skipped. |
| **Kết quả khám** | |
| Button "Thêm vùng khám" | Adds a finding for a region, for example "Toàn hàm". |
| Finding row | A tag "Răng <số>" or a text box "Vùng khám" (up to 200 characters), "Mô tả bổ sung" (up to 2000), "Chẩn đoán" (several), "Bề mặt" (for a tooth), and the button "Bỏ". |
| Ghi chú khám | Up to 8000 characters. |
| **Actions** | |
| Button "Lưu nháp" | Saves the form without confirming. Not shown while amending. |
| Button "Chẩn đoán & hướng xử trí" | Goes to the tab "Tổng kết khám". |

### 10.3 Tab "Cận lâm sàng"

This tab allows the dentist to order an investigation, to record its result and to read it. A new order needs a visit that the dentist can edit.

| Field Name | Description |
|---|---|
| **Header** | |
| Buttons "Tải lại chỉ định", "Thêm chỉ định" | Reload the orders; open 10.3a. The list also reloads by itself while a file is being scanned. |
| Alert | "Lượt khám đã kết thúc. Có thể đính chính hồ sơ cũ; cần tạo lượt khám mới để lập chỉ định mới.", or "Để bổ sung chỉ định, vào Tổng kết khám và bấm Đính chính kết quả.", or "Chỉ định bổ sung được lưu ngay…". |
| Empty | "Chưa có chỉ định. Tạo khi bác sĩ cần thêm kết quả để đánh giá." |
| **Order card** | |
| Title | Name, state tag (Chờ kết quả, Chờ bác sĩ đọc, Bác sĩ đã đọc, Đã hủy) and the tag "Cần để kết luận". |
| Information | Read-only. Group (Chụp phim răng hàm mặt, Chẩn đoán hình ảnh khác, Xét nghiệm, Giải phẫu bệnh / tế bào học, Khác), tooth or region, creator, time, "Lý do". |
| Result | Read-only. "Kết quả", who received it and when, the files with their scan state, "Nhận định", who read it and when, "Lý do hủy". |
| Button "Tải tệp kết quả" | Opens the upload drawer of the document screen. |
| Button "Nhận kết quả" | Opens 10.3b. |
| Button "Đọc kết quả" | Opens 10.3c. |
| Button "Hủy chỉ định" | Opens 10.3d. |

#### 10.3a Create Investigation

Dialog "Tạo chỉ định cận lâm sàng".

| Field Name | Description |
|---|---|
| Lý do bổ sung sau xác nhận khám | Required, up to 1000 characters. Only when ordering after the form is confirmed. |
| Nhóm chỉ định | Required. Chụp phim răng hàm mặt, Chẩn đoán hình ảnh khác, Xét nghiệm, Giải phẫu bệnh / tế bào học, Khác. |
| Tên chỉ định | Required, up to 160 characters. Suggestions for the chosen group (for example "Phim toàn cảnh (Panorama / OPG)"), or type another name. |
| Răng / vùng | Up to 200 characters. |
| Lý do chỉ định | Required, up to 1000 characters. |
| Checkbox "Cần bác sĩ đọc kết quả trước khi hoàn tất khám" | Checked by default. |
| Buttons "Đóng", "Tạo chỉ định" | Close, or save. |

#### 10.3b Receive Result

Dialog "Nhận kết quả cận lâm sàng".

| Field Name | Description |
|---|---|
| Nội dung kết quả | Up to 4000 characters. The result may also be a file uploaded to the order. |
| Alert | "Tệp đang kiểm tra hoặc bị từ chối chưa được dùng làm bằng chứng kết quả…" |
| Buttons "Đóng", "Lưu kết quả" | Close, or save. |

#### 10.3c Read Result

Dialog "Bác sĩ đọc kết quả".

| Field Name | Description |
|---|---|
| Result | Read-only. Name, result text ("Kết quả nằm trong các tệp đã nhận." when only files), and the result files. |
| Nhận định của bác sĩ | Optional, up to 4000 characters. |
| Buttons "Đóng", "Xác nhận đã đọc kết quả" | Close, or confirm. |

#### 10.3d Cancel Investigation

Dialog "Hủy chỉ định cận lâm sàng".

| Field Name | Description |
|---|---|
| Alert | "Nếu dịch vụ đã thực hiện, hủy chỉ định không tự xóa phí, khoản phải trả hoặc phiếu thu." |
| Lý do hủy | Required, up to 1000 characters. |
| Buttons "Đóng", "Xác nhận hủy chỉ định" | Close, or cancel the order. |

### 10.4 Tab "Ảnh & tài liệu"

This tab lists the images and documents of this visit; the visit filter is fixed. It is the same list as the document screen of the patient. Uploading adds a file to the visit. The button to mark a document as reviewed is shown to users who may review documents, while the visit is in progress and the dentist can edit or hand off.

### 10.5 Tab "Tổng kết khám"

This tab (title "Chẩn đoán và hướng xử trí") allows the dentist to conclude the examination, to finish the form and to move to the next step.

| Field Name | Description |
|---|---|
| **Diagnoses** | |
| Chẩn đoán đã ghi khi khám | Read-only. Each diagnosis recorded at a tooth or region, with the link "Sửa tại răng <số>". "Chưa có chẩn đoán gắn răng/vùng." when empty. |
| Chẩn đoán bổ sung / không gắn răng | Several values; up to 30 diagnoses and 4000 characters ("Tối đa 30 chẩn đoán, tổng nội dung tối đa 4000 ký tự."). A normal case chooses "Chưa phát hiện bất thường". |
| Alert "Kết luận bổ sung từ phiếu cũ" | Read-only. Shown for an old form that has a conclusion the diagnoses do not cover. |
| **Next step** | |
| Dặn dò / lời khuyên | Required, up to 4000 characters. |
| Hướng xử trí | Required: Tư vấn, chưa điều trị; Theo dõi / cần quay lại; Chuyển khám; Hoãn quyết định điều trị; Tiếp tục điều trị. |
| Nhu cầu quay lại | Required when the choice is "Theo dõi / cần quay lại". Up to 2000 characters. It records the need and does not book an appointment. |
| Nơi / lý do chuyển khám | Required when the choice is "Chuyển khám". Up to 1000 characters. |
| Lý do đính chính | Required while amending, up to 1000 characters. |
| **Notices** | |
| Alert "Chưa thể hoàn tất khám" | Lists what is missing; each item leads to its field: Lý do khám / triệu chứng; at least one finding (or the diagnosis "Chưa phát hiện bất thường"); the region and the condition of each finding; Chẩn đoán; Dặn dò; Hướng xử trí; Nhu cầu quay lại; Nơi / lý do chuyển khám; the required investigations that are not received and read. |
| Text | "Phiếu khám đã hoàn tất. Kết thúc lượt khám trước khi chuyển sang phiếu điều trị." |
| **Actions** | |
| Button "Kê đơn trong hồ sơ bệnh nhân" | Opens the tab "Đơn thuốc" of the patient (9.8). Needs the permission to manage prescriptions. |
| Button "Lưu nháp" | Saves without confirming. |
| Button "Hoàn tất khám" | Confirms the form once it is complete. While amending it is "Lưu đính chính". |
| Button "Hủy đính chính" | Drops the amendment. |
| Button "Đính chính kết quả" | Reopens a confirmed form for correction with a reason. Needs the permission to amend. |
| Button "Lập kế hoạch điều trị" | Opens the tab "Điều trị & Thanh toán" (9.5) with this visit as the source. Shown when the visit can be handed off. |
| Button "Đơn thuốc mua ngoài" | Opens the prescription of the visit. Shown for a confirmed form to users who manage examinations. |
| Button "Kết thúc lượt khám" | Opens 10.5a. |

#### 10.5a End the Visit

Dialog "Kết thúc lượt khám".

| Field Name | Description |
|---|---|
| Note | "Xác nhận kết quả, dặn dò và công việc của lượt khám này. Chưa thực hiện điều trị không cản kết thúc khám. Kế hoạch và các buổi điều trị sẽ được theo dõi riêng, công nợ hiện có vẫn được giữ." |
| Checkbox "Đã kiểm tra công việc và nhu cầu quay lại của lượt này" | Required to confirm. |
| Alert | Error of ending the visit. |
| Buttons "Hủy", "Xác nhận kết thúc lượt" | Close, or end the visit. |

### 10.6 Tab "Lịch sử khám"

This tab allows the dentist to read earlier versions of this form and earlier visits of the patient.

| Field Name | Description |
|---|---|
| **Các phiên bản phiếu hiện tại** | |
| Version card | "Phiên bản <n> · Đã xác nhận / Nháp", time, diagnoses, and "Lý do đính chính" when amended. The fold "Xem toàn bộ nội dung phiên bản" shows symptoms, health facts, findings, advice, next step, return need, referral and notes ("Chưa ghi" when empty). |
| Empty | "Chưa có phiên bản đã lưu." |
| **Các lần khám trước** | |
| Visit card | Arrival time, "Đã xác nhận" or "Nháp", diagnoses ("Chưa chẩn đoán"), the findings by tooth or region, and the link "Xem phiếu khám". |
| Empty | "Chưa có kết quả khám trước trong chi nhánh." |
