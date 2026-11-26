# III. Functional Requirements

## 14. Dentist: Lab Work Management

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "14.1" and "14.2").

### 14.1 Lab Work List

This screen (`/lab-orders`, menu "Quản lý công việc labo", group "Chuyên môn") allows a dentist to:

- See all the lab orders (prosthetic work sent to a dental laboratory) and how many are in each status.
- Filter and search the lab orders.
- Open a lab order to see its detail.

The screen needs the permission "Xem màn Quản lý công việc labo" (`treatment.lab.read`). The lab order itself is created from a treatment sheet (see 9.11).

| Field Name | Description |
|---|---|
| **Status tiles** | |
| Chưa gửi / Đã gửi xưởng / Đã nhận về / Quá hạn hẹn / Đã giao bệnh nhân / Làm lại | Six buttons, each with the number of lab orders. Click one to filter the list by that status; click it again to remove the filter. The tile "Quá hạn hẹn" is shown in a warning style. |
| **Filters** | |
| Tìm phiếu labo | Search box, clearable, up to 160 characters, placeholder "Mã phiếu, MSKH, tên bệnh nhân, công việc". Runs when the user presses Enter or clicks the search icon, and when the box is cleared. |
| Chi nhánh | Select, clearable, placeholder "Tất cả chi nhánh". Shown only to users who can pick a branch and who have more than one branch. Changing it clears "Công ty labo" and "Bác sĩ". |
| Công ty labo | Select, clearable. The laboratories that appear in the result. |
| Bác sĩ | Select, clearable. The dentists that appear in the result. |
| Hẹn từ / đến | Two dates for the due date of the lab order. "Hẹn từ" cannot be later than "đến". |
| Đặt lại | Button. Clears all the filters. Disabled when no filter is applied. |
| Alert (error) | Read-only. The error message with the button "Thử lại". |
| **Table** | |
| Mã phiếu | Read-only. Code of the lab order or "—". |
| Bệnh nhân | Read-only. Name, and the patient code under it. |
| Công việc | Read-only. Name of the work, and under it "SL n" and the material. |
| Công ty labo | Read-only. Name or "—". |
| Hẹn | Read-only. Due date, with "(quá hạn)" in a warning style when it is overdue. |
| Phí / Đã trả | Read-only. "Fee / Paid", in ₫. |
| Trạng thái | Read-only tag: Chưa gửi, Đã gửi xưởng, Đã nhận về, Đã giao bệnh nhân, Làm lại. |
| Row | Click (or press Enter) to open the detail 14.2. |
| Empty | "Chưa có phiếu labo nào"; with filters applied "Không có phiếu labo phù hợp bộ lọc." |
| Paging | Page size 20 (default), 50 or 100. |

### 14.2 Lab Order Detail

This panel opens on the right when a row is clicked. It allows a dentist to:

- See the detail of the lab order.
- Move it to the next status, with a note.
- Open the full lab order to edit it.
- See the history of its statuses.

| Field Name | Description |
|---|---|
| Title | "Phiếu labo" and the code. |
| Tags | Read-only. The status, and "Quá hạn hẹn" when overdue. |
| Bệnh nhân | Read-only. Name and patient code. |
| Công việc | Read-only. Name, "SL n" and the material. |
| Răng | Read-only. FDI codes or "—". |
| Công ty labo | Read-only. |
| Bác sĩ | Read-only. |
| Ngày lập | Read-only. |
| Hẹn | Read-only. |
| Đã gửi | Read-only. Date sent to the laboratory. |
| Đã nhận về | Read-only. Date received back. |
| Đã giao | Read-only. Date delivered to the patient. |
| Phí / Đã trả | Read-only. In ₫. |
| Ghi chú | Read-only. |
| Sửa phiếu / Xem phiếu đầy đủ | Button. "Sửa phiếu" for a user with the permission to manage lab orders (`treatment.lab.manage`), otherwise "Xem phiếu đầy đủ". Opens the window of 9.11. After saving, the panel closes and the list is reloaded. |
| **Change status** | Shown only to a user with the permission "Đổi trạng thái phiếu labo" (`treatment.lab.track`). |
| Ghi chú đổi trạng thái | Text, optional, up to 300 characters, placeholder "Ghi chú (không bắt buộc)". |
| Status buttons | The buttons depend on the current status: Chưa gửi → "Gửi xưởng"; Đã gửi xưởng → "Đã nhận về" and "Yêu cầu làm lại"; Đã nhận về → "Giao bệnh nhân" and "Yêu cầu làm lại"; Đã giao bệnh nhân → "Yêu cầu làm lại"; Làm lại → "Gửi xưởng". |
| Alert (error) | Read-only. The error message. When another user has changed the order (conflict) the list is reloaded. |
| **Nhật ký trạng thái** | |
| History | Read-only list, one line per change: the new status, the time, the person and the note. |
