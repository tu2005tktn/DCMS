# Thống kê công việc/dịch vụ

Ngày: 06/11/2026 (gom theo phiếu điều trị cập nhật cùng ngày). Màn hình `/work-statistics` ("Thống kê công việc/dịch vụ", nhóm Quản trị) liệt kê các dòng "Quá trình điều trị/dịch vụ" trong phần Điều trị và thanh toán của **mọi bệnh nhân**, **gom theo từng phiếu (hồ sơ) điều trị**, để xem nhanh công việc đã làm, chi phí và công nợ. Chỉ đọc, không ghi dữ liệu.

## Nguồn dữ liệu

- Hoạt động lâm sàng đã ghi nhận (`ClinicalActivity`, trạng thái `Posted`) thuộc hồ sơ điều trị (`DossierId` khác rỗng). Hoạt động không thuộc hồ sơ điều trị (khám đơn lẻ) không có trong thống kê này.
- Giá trị hiệu lực của dòng là bản **sau chỉnh sửa trên phiếu**: áp các sự kiện `SheetActivityEdited`/`SheetActivityDeleted` theo thứ tự phiên bản hồ sơ. Dòng đã xóa trên phiếu **không** có trong thống kê; phiếu không còn dòng nào thì không hiện.
- Tiền do **khoản thu** (`FinanceCharge`) quyết định, không tính lại từ đơn giá: Chi phí = tổng khoản thu của dòng, Đã trả = tổng `PaymentAllocation` của phiếu thu có trạng thái `Posted` (người dùng thấy là "Đã xác nhận"). Nhờ vậy số liệu luôn khớp màn Thanh toán; dòng chưa lập khoản thu (chưa tính phí) không bị coi là còn nợ.

## Bố cục: mỗi phiếu điều trị một nhóm

Các cột **Ngày, MSKH, Khách hàng, Lịch hẹn tiếp theo, Bác sĩ & phụ tá** chỉ hiện **một lần cho cả phiếu** (ô gộp theo số dòng của phiếu). Mỗi dịch vụ của phiếu là một dòng riêng với **Điều trị/dịch vụ, Chi phí, Đã trả, Còn lại**. Không có cột Ghi chú và không hiện răng/vùng.

| Cột | Nguồn |
|---|---|
| Ngày | ngày tạo phiếu điều trị (`TreatmentDossier.StartedOn`) |
| MSKH | `PatientProfile.PatientCode` |
| Khách hàng | `PatientProfile.FullName`, là liên kết mở hồ sơ bệnh nhân `/patients/{id}` |
| Lịch hẹn tiếp theo | lịch hẹn của bệnh nhân có trạng thái `Confirmed`, `StartAt` ≥ hiện tại, gần nhất; trống nếu không có |
| Bác sĩ & phụ tá | bác sĩ phụ trách phiếu; dưới đó là các phụ tá khác nhau xuất hiện trong các dòng của phiếu (nếu có) |
| Điều trị/dịch vụ | tên dịch vụ hiệu lực của dòng, chỉ tên dịch vụ |
| Chi phí | tổng `FinanceCharge.AmountVnd` của khoản thu gắn với dòng (`ActivityId`), tức đúng số tiền màn Thanh toán đang thu; dòng **chưa có khoản thu** có Chi phí 0 và nhãn "Chưa lập khoản thu" |
| Đã trả | tổng phân bổ của phiếu thu đã ghi sổ (`Posted`) vào các khoản thu của dòng |
| Còn lại | Chi phí − Đã trả |

## API

`GET /api/work-statistics?from=&to=&branchId=&q=&page=&pageSize=` — quyền `finance.work-statistics.read` (cha `finance.read`, cập nhật 07/11/2026). Menu/route dùng cùng quyền; dữ liệu chỉ gồm chi nhánh được cấp và chi nhánh yêu cầu ngoài phạm vi trả 403.

- `from`/`to`: lọc theo **ngày tạo phiếu** (`yyyy-MM-dd`, gồm cả hai đầu); `from > to` → MSG04.
- `branchId`: chi nhánh của phiếu; không truyền = mọi chi nhánh được cấp quyền.
- `q`: tìm theo MSKH, tên khách hàng hoặc tên dịch vụ của phiếu (không phân biệt hoa thường), tối đa 160 ký tự; phiếu khớp thì hiện đủ các dòng.
- `pageSize` ∈ {20, 50, 100}; **phân trang theo phiếu**, sắp ngày tạo giảm dần rồi `CreatedAt` giảm dần rồi id; trong phiếu, các dòng theo ngày thực hiện tăng dần.
- Trả `{ items: [{ dossierId, dossierCode, startedOn, patientId, patientCode, patientName, nextAppointmentAt, dentistName, assistantNames, costVnd, paidVnd, dueVnd, lines: [{ activityId, serviceName, costVnd, paidVnd, dueVnd }] }], total, page, pageSize, totals: { costVnd, paidVnd, dueVnd } }`; `total` là số phiếu; `totals` tính trên toàn bộ tập đã lọc.

## Giao diện

Menu "Thống kê công việc/dịch vụ" (nhóm Quản trị) hiện khi có `finance.work-statistics.read`. Bộ lọc: khoảng ngày, chi nhánh, ô tìm kiếm. Bảng 9 cột (Ngày, MSKH, Khách hàng, Lịch hẹn tiếp theo, Điều trị/dịch vụ, Bác sĩ & phụ tá, Chi phí, Đã trả, Còn lại), tiền căn phải định dạng VND, hàng tổng cộng phía dưới theo bộ lọc, phân trang 20/50/100. Trạng thái: đang tải, trống ("Chưa có công việc nào"), lỗi kèm "Thử lại".

## Giới hạn

Không xuất Excel, không gom theo bác sĩ/dịch vụ, không sửa dữ liệu từ màn này. Dữ liệu tổng hợp trong bộ nhớ ở quy mô một phòng khám.

## Thanh tổng hợp và lọc nhanh theo tuần/tháng (06/11/2026)

- Phía trên bảng có **thanh tổng hợp**: bên trái "Tổng cộng: {Chi phí}  Đã trả: {Đã trả}  Còn lại: {Còn lại}" (theo toàn bộ kết quả lọc, không chỉ trang hiện tại), bên phải "Tìm thấy {N} công việc/dịch vụ." với N là **số dòng dịch vụ** trong toàn bộ kết quả lọc. API trả thêm `lineCount`.
- Bộ lọc nhanh **Tuần này** và **Tháng này** cạnh ô khoảng ngày: đặt `from`/`to` theo ngày tạo phiếu, giờ Việt Nam; tuần tính từ thứ Hai đến Chủ nhật chứa hôm nay, tháng từ ngày 1 đến ngày cuối tháng. Bấm lại nút đang chọn hoặc sửa tay hai ô ngày thì bỏ chọn nhanh; nút "Tất cả" xóa khoảng ngày.
- Hàng "Tổng cộng" cuối bảng được thay bằng thanh tổng hợp ở trên.
- Chưa có tải file Excel.

## Một ô chọn khoảng thời gian (06/11/2026)

Hai nút Tuần này/Tháng này được thay bằng **một ô chọn "Thời gian"** (dropdown) với các mục: **Tất cả** (mặc định, không giới hạn ngày), **Hôm nay**, **Tuần này** (thứ Hai–Chủ nhật), **Tháng này**, **Năm này** (01/01–31/12) và **Tùy chọn**. Mục có sẵn đặt `from`/`to` theo ngày tạo phiếu, giờ Việt Nam. Chỉ khi chọn **Tùy chọn** mới hiện hai ô "Từ ngày"/"Đến ngày" để nhập tay; đổi sang mục khác thì ẩn hai ô này. Các quy tắc còn lại (thanh tổng hợp, API) giữ nguyên.

## Sửa nguồn tiền: theo phiếu điều trị (06/11/2026)

Nút "Trả tiền" trong Quá trình điều trị/dịch vụ ghi vào thanh toán của hồ sơ điều trị (`DossierPayment`/`DossierPaymentLine`), không phải phiếu thu của màn Thanh toán, nên mục "Nguồn dữ liệu" về `FinanceCharge`/`PaymentAllocation` ở trên **không còn áp dụng**. Số tiền của mỗi dòng tính đúng như bảng trong phiếu điều trị:

- Giá trị dòng = `Quantity × PriceSnapshotVnd` (làm tròn), giảm giá = `min(giá trị, DiscountSnapshotVnd)`.
- **Chi phí** = giá trị − giảm giá; dòng `ChargingBasis = Included` (đã gồm trong gói) có Chi phí 0.
- **Đã trả** = tổng `DossierPaymentLine.AmountVnd` của dòng; **Còn lại** = Chi phí − Đã trả (không âm).
- Không còn nhãn "Chưa lập khoản thu"; mọi dòng đều có Chi phí theo giá trên phiếu. API bỏ `hasCharge`.
