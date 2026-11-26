# III. Functional Requirements

## 12. Dentist: Work/Service Statistics

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frame "12.1").

### 12.1 Work/Service Statistics

This screen (`/work-statistics`, menu "Thống kê công việc/dịch vụ", group "Quản trị") allows a dentist to:

- See the treatment sheets that have services done, with the cost, the amount paid and the amount remaining of each sheet, and the totals.
- Open one sheet to see its services, the people who worked on it and the sums.
- Filter by period and branch, and search by customer code, customer name or service.
- Open the patient record or the treatment sheet of a customer.

The screen needs the permission "Xem thống kê công việc / dịch vụ" (`finance.work-statistics.read`), given by default to ClinicOwner, Admin, Dentist and ClinicAssistant. The data is limited to the branches of the user. The screen is read-only: no value can be edited here.

| Field Name | Description |
|---|---|
| **Filters** | |
| Thời gian | Select, default "Tất cả". Options: Tất cả, Hôm nay, Tuần này (Monday to Sunday), Tháng này, Năm này, Tùy chọn. The range is in clinic time and is applied to the start date of the treatment sheet. |
| Từ ngày | Date. Shown only for "Tùy chọn". Cannot be later than "Đến ngày". |
| Đến ngày | Date. Shown only for "Tùy chọn". Cannot be earlier than "Từ ngày". |
| Chi nhánh | Select, clearable, placeholder "Tất cả chi nhánh". Shown only to users who can pick a branch and who have more than one branch. |
| Tìm công việc | Search box, clearable, placeholder "Tìm MSKH, khách hàng, dịch vụ". The search runs 0.3 seconds after the user stops typing. |
| **Summary** | |
| Tổng cộng | Read-only. Total cost of all the sheets found, in ₫. |
| Đã trả | Read-only. Total amount paid, in ₫. |
| Còn lại | Read-only. Total amount remaining, in ₫. |
| Count | Read-only. "Tìm thấy n công việc/dịch vụ." (n is the number of services). |
| **Notices** | |
| Alert (info) | Read-only. Shown when the screen was opened for one treatment sheet: "Đang xem phiếu [code]." with the button "Xem tất cả phiếu"; when that sheet has no service done yet: "Phiếu này chưa có dịch vụ đã thực hiện nên chưa có trong thống kê công việc." with the link "Mở phiếu điều trị". Changing a filter or the search leaves this view. |
| Alert (error) | Read-only. The error message, or "Không tải được thống kê.", with the button "Thử lại". |
| **Table** | One row for each treatment sheet. Click a row (or press Enter) to open or close its detail; only one detail is open at a time. |
| Mã phiếu | Read-only. Code of the treatment sheet. |
| Ngày | Read-only. Start date of the treatment sheet. |
| MSKH | Read-only. Customer code. |
| Khách hàng | Read-only. Customer name. |
| Bác sĩ | Read-only. |
| Chi phí | Read-only. Cost of the sheet, in ₫. |
| Đã trả | Read-only. Amount paid for the sheet, in ₫. |
| Còn lại | Read-only. Amount remaining for the sheet, in ₫. |
| Empty | "Chưa có công việc nào." |
| Paging | "Có n phiếu điều trị." Page size 20 (default), 50 or 100. Changing a filter returns to page 1; changing the page closes the open detail. |
| **Detail of a sheet** | Shown under the row that is open. |
| Customer | Link "[MSKH] - [name]". Opens the patient record. |
| Mã phiếu, Trạng thái | Read-only. The code and a tag: Đang điều trị, Hoàn thành, Đã dừng or Hủy phiếu. |
| Mở phiếu điều trị | Link. Opens the treatment sheet in the patient record. |
| Người tạo | Read-only. "—" when empty. |
| Bác sĩ | Read-only. "—" when empty. |
| Phụ tá | Read-only. Names of the assistants, separated by commas. Shown only when there are assistants. |
| Ngày tạo phiếu | Read-only. dd/mm/yyyy. |
| Lịch hẹn tiếp theo | Read-only. Date and time of the next appointment, or "—". |
| **Services table** | One row for each service done. |
| Điều trị/dịch vụ | Read-only. Name of the service. |
| Ngày thực hiện | Read-only. dd/mm/yyyy. |
| Số lượng | Read-only. |
| Đơn giá | Read-only. In ₫. |
| Giảm giá | Read-only. In ₫; empty when there is no discount. |
| Thành tiền | Read-only. In ₫, in bold. |
| Đã trả | Read-only. In ₫. |
| Còn lại | Read-only. In ₫. |
| Ghi chú | Read-only. Note of the sheet, or "Không có ghi chú". |
| Tổng tiền dịch vụ (n) | Read-only. Sum before the discount of the n services, in ₫. |
| Giảm giá | Read-only. Total discount, in ₫. |
| Khách cần trả | Read-only. In ₫, in bold. |
| Khách đã trả | Read-only. In ₫, in bold. |
| Còn lại | Read-only. In ₫. |
