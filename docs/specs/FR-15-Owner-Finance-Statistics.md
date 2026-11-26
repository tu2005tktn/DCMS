# III. Functional Requirements

## 15. Owner: Finance Statistics

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "15.1", "15.2" and "15.3").

### 15.1 Finance Statistics

This screen (`/finance/statistics`, menu "Thống kê tài chính", group "Quản trị") allows the clinic owner to:

- See six finance figures of the clinic for a period: amount to collect, money really received, money received in advance, money still to collect, cost of materials and profit.
- Compare the six figures on a bar chart and see their share on a ring chart.
- See the collection progress and the profit margin.
- Open the list behind each figure.

The menu item and the screen are for the role ClinicOwner only. The sheets are filtered by the date the treatment sheet was opened. The cost of materials is the total of the purchases in the chosen dates for the whole clinic, because purchases are not split by branch. A profit is counted only when the treatment sheet has been fully allocated; the total of purchases is subtracted once.

| Field Name | Description |
|---|---|
| **Filters** | |
| Từ ngày mở phiếu | Date (DD/MM/YYYY). Optional. If it is later than "Đến ngày mở phiếu", the latter is cleared. |
| Đến ngày mở phiếu | Date (DD/MM/YYYY). Optional. Cannot be earlier than "Từ ngày mở phiếu". |
| Chi nhánh | Select, clearable, placeholder "Tất cả chi nhánh". Shown only to users who can pick a branch and who have more than one branch. |
| Tải lại | Button. Loads the figures again. |
| Alert (error) | Read-only. The error message, or "Không tải được thống kê.". |
| **Six figure cards** | Each card is clickable and opens the list of 15.2. A card shows the name, a tag with the number of items, the amount, and a bar showing the amount against the largest figure. |
| Số tiền cần thu dịch vụ | Read-only. Total amount of the services. Tag "n phiếu". |
| Tiền thực thu | Read-only. Money really received. Tag "n giao dịch". |
| Tiền nhận trước | Read-only. Money received in advance (deposits). Tag "n biến động". |
| Tiền còn cần thu | Read-only. Money still to collect. Tag "n phiếu nợ". |
| Tiền dụng cụ vật liệu | Read-only. Total value of the purchases. Tag "n lần nhập". |
| Lãi | Read-only. Profit; shown in red when it is negative. Tag "n phiếu hoàn tất". |
| **Chart "Biểu đồ so sánh 6 chỉ số tài chính"** | |
| Bar chart | Read-only. One bar for each figure, with the amount in a short form (k, tr, tỷ) above it, and the name and the full amount below it. Click a bar to open the list. |
| **Card "Tỷ trọng dòng tiền & Hiệu suất thu"** | |
| Ring chart | Read-only. The share of each of the six figures. Click a part to open the list. In the centre: "Thực thu", the amount and "Đạt n%". |
| Legend | Read-only. The six short names (Cần thu DV, Thực thu, Nhận trước, Còn cần thu, Vật liệu, Lãi ròng) with their share in %. Click one to open the list. |
| Tiến độ thu dịch vụ (Đã thu / Cần thu) | Read-only. A percentage and a progress bar. |
| Tỷ suất lợi nhuận trên thực thu | Read-only. Profit over money received, in % (red when negative), with a progress bar. |

### 15.2 Detail Lists

Each of the six cards, bars and ring parts opens a window with a table of 20 rows per page. The link "Mở phiếu" on a row opens the treatment sheet in the screen "Thống kê công việc/dịch vụ" (12.1).

| Field Name | Description |
|---|---|
| **Chi tiết số tiền cần thu dịch vụ** | |
| Columns | Phiếu, Bệnh nhân (name and patient code), Ngày lập, Trạng thái (Chưa xong, Hoàn tất, Đã ngừng, Hủy phiếu), Tổng cần thu, Đã trả, Còn thu, link "Mở phiếu". |
| **Danh sách các lần thực thu** | |
| Columns | Thời gian thu, Phiếu, Bệnh nhân, Hình thức (a tag; a different colour for a deposit and for a forfeited deposit), Nội dung, Số tiền, link "Mở phiếu" (only when the sheet can be opened). |
| **Danh sách các lần nhận trước & phân bổ cọc** | |
| Columns | Thời gian, Phiếu, Bệnh nhân, Loại giao dịch, Nội dung, Biến động cọc (+ in green for an increase, in orange for a decrease), link "Mở phiếu". |
| **Phiếu điều trị còn cần thu** | |
| Columns | Phiếu, Bệnh nhân, Ngày lập, Tổng phí, Đã trả, Còn thu, link "Mở phiếu". |
| Khách không đến, bỏ cọc | Button on a row, shown only when the deposit of that sheet can be forfeited. Opens the dialog 15.3. |
| **Danh sách các lần nhập dụng cụ vật liệu** | |
| Columns | Ngày nhập, Vật liệu / Dụng cụ, Nhóm, Nhà cung cấp (or "—"), Số lượng (with the unit), Đơn giá, Thành tiền, Đã trả NCC. |
| **Chi tiết các khoản tính lãi** | |
| Columns | Ngày, Phiếu / Khoản mục, Bệnh nhân / Phạm vi, Thu dịch vụ, Chi phí (Labo / Vật liệu), Lãi ròng (green or red), link "Mở phiếu". |

### 15.3 Forfeit a Deposit (customer did not come)

This dialog ("Xóa phiếu do khách không đến") allows the owner to record that a customer did not come and keeps the deposit.

| Field Name | Description |
|---|---|
| Notice | Read-only. "Phiếu [code] sẽ được ẩn khỏi hồ sơ. Số dư cọc được ghi nhận là khoản khách bỏ cọc và không còn dùng để thanh toán." |
| Lý do khách không đến | Required text, up to 500 characters, placeholder "Lý do khách không đến". |
| Alert (error) | Read-only. The error message. |
| Ghi nhận bỏ cọc và xóa phiếu | Button (red). Disabled while the reason is empty. After success the dialog closes and the figures are reloaded. |
| Cancel (X) | Closes the dialog and clears the reason. |
