# Đề xuất tích hợp doanh thu, công nợ, nhận trước và hoàn tiền — 09/11/2026

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

## Điều chỉnh điểm vào theo phiếu điều trị

Nhân viên bắt đầu từ hồ sơ bệnh nhân, tab **Điều trị & Thanh toán**. Tại mỗi phiếu, khu vực cuối quá trình điều trị hiển thị số dư nhận trước của chính phiếu, nút **TRẢ TIỀN...**, **NHẬN TRƯỚC...** và **LỊCH SỬ**. Thu tiền cho việc đã làm vẫn dùng API dossier và bill PayOS tại chỗ; nhận tiền mặt trước dùng API `POST /api/finance/advances` với `patientId`, `branchId`, `dossierId` lấy từ phiếu, không nhập mã thủ công. Lịch sử hiển thị riêng giao dịch đã thu cho từng việc và các khoản nhận trước, số đã phân bổ, số còn lại. Nhận trước chưa phân bổ không làm giảm nợ dịch vụ hay tăng doanh thu thực hiện. Việc phân bổ khoản nhận trước vào dịch vụ, hoàn tiền và đối chiếu hóa đơn vẫn cần hoàn thiện trong chính luồng này; không được coi các thao tác đó là đã giải quyết chỉ vì có màn Tài chính riêng.

Nhận trước còn hỗ trợ **chuyển khoản PayOS** tại chính hộp thoại này. Server tạo phiếu thu Pending, trả QR và bill, tự đối chiếu PayOS bằng webhook/polling rồi chuyển Posted một lần khi đúng số tiền. Trong thời gian chờ, số dư khả dụng là 0; lệnh hủy/hết hạn/lỗi liên kết hủy phiếu Pending; lệnh sai dữ liệu cần đối soát. Xem hợp đồng chi tiết tại [TREATMENT-DOSSIER-PAYMENTS.md](TREATMENT-DOSSIER-PAYMENTS.md).

Trạng thái: đang triển khai; phần đã hiện thực và phần còn thiếu được ghi ở mục 8. Phạm vi không gồm thuế.

## 1. Hiện trạng đã kiểm tra trong mã nguồn

- `FINANCE_CHARGE` lưu nghĩa vụ cho dịch vụ đã phát sinh từ lượt khám/phiếu điều trị; `PAYMENT_RECEIPT` (`Pending`, `Posted`, `Cancelled`) và `PAYMENT_ALLOCATION` lưu tiền thu và phân bổ cho charge. `FINANCIAL_ACCOUNT` chứa version để kiểm soát ghi đồng thời. `FINANCE_AUDIT` và `FINANCE_COMMAND_RECEIPT` hỗ trợ lịch sử/idempotency.
- Luồng `TREATMENT_DOSSIER_PAYMENT`/`_LINE` ghi tiền trực tiếp cho hoạt động đã thực hiện, gồm chuyển khoản PayOS. Nó độc lập với `PAYMENT_RECEIPT`; báo cáo hiện tại gộp hai nguồn.
- `GET /api/finance/revenue` và màn hình Doanh thu hiện là báo cáo **tiền đã thu**, không phải doanh thu dịch vụ. Chỉ số `receivedVnd` hiện cộng Cash và Transfer, chưa có hoàn tiền, nhận trước, POS/ví điện tử.
- Dossier có kế hoạch và các hoạt động lâm sàng với giá/giảm giá snapshot. Phải lấy giá giai đoạn từ kế hoạch duyệt và hoạt động thực tế; không chia đều tổng lộ trình.
- Chưa có thực thể hóa đơn độc lập, tiền nhận trước, hoàn tiền hoặc ngày đến hạn của nghĩa vụ. Vì vậy không đổi tên phiếu thu thành hóa đơn và không suy diễn số dư nhận trước từ `PAYMENT_ALLOCATION`.

## 2. Nguồn sự thật và mốc ghi nhận

1. `service_date` là ngày thực hiện hoạt động; doanh thu quản trị phát sinh khi hoạt động được xác nhận/posted, theo giá trừ giảm giá được chốt. Sửa giá, hủy hoạt động sau khi chốt phải tạo bút toán điều chỉnh có dấu vết, không sửa lịch sử tiền thu.
2. `invoice_date` là ngày phát hành hóa đơn nội bộ, tách khỏi `service_date`. Hóa đơn có nhiều dòng, mỗi dòng tham chiếu duy nhất tới nguồn charge/hoạt động; không lập trùng dòng nguồn trên các hóa đơn còn hiệu lực.
3. `payment_date`/`refund_date` là thời điểm giao dịch thực sự `Posted`/hoàn thành; `created_at` là thời điểm lập bản ghi. `due_date` của hóa đơn/dòng công nợ do nhân viên nhập hoặc theo quy tắc cấu hình, không suy ra từ ngày thu.
4. Giá trị dịch vụ = tổng gross của dịch vụ đã thực hiện. Doanh thu dịch vụ = gross − discount ± điều chỉnh hợp lệ. Tiền thực thu = tổng giao dịch thu thành công, gồm tiền nhận trước, tính đúng một lần. Tiền thu ròng = thực thu − hoàn thành công. Công nợ = nghĩa vụ đã phát hành − phân bổ hợp lệ, có xét bút toán điều chỉnh và hoàn tiền làm khôi phục nghĩa vụ khi thích hợp. Tiền nhận trước chưa phân bổ = thu trước − phân bổ − hoàn từ số dư. Tiền đã phân bổ cho giai đoạn chưa thực hiện phải hiển thị riêng và không tính doanh thu thực hiện.
5. Trạng thái hóa đơn tính từ số dư và hoàn tiền: Unpaid, PartiallyPaid, Paid, PartiallyRefunded, Refunded; lưu trạng thái dẫn xuất hoặc cập nhật trong cùng transaction. Hoàn tiền không tự động hủy doanh thu/hoá đơn.

## 3. Tích hợp dữ liệu đề xuất

Giữ `FINANCE_CHARGE`, `PAYMENT_RECEIPT`, `PAYMENT_ALLOCATION`, `TREATMENT_DOSSIER_PAYMENT`, kế hoạch điều trị và PayOS hiện có. Thêm cấu trúc tối thiểu:

| Bảng | Mục đích và ràng buộc chính |
| --- | --- |
| `FINANCE_INVOICE` | Id, code duy nhất theo chi nhánh, patient/branch, invoice/due/created dates, status, version; FK bệnh nhân/chi nhánh; không xóa hóa đơn đã phát hành. |
| `FINANCE_INVOICE_LINE` | Id, invoice FK, charge/source FK, gross/discount/net snapshot, service date; unique active source để chặn lập trùng. |
| `FINANCE_ADVANCE` | Id, patient/branch, optional dossier/plan FK, `Deposit` hoặc `ServiceAdvance`, original payment reference, amount, received date, remaining balance/version; không tạo receipt thứ hai lúc sử dụng. |
| `FINANCE_ADVANCE_ALLOCATION` | Advance FK, invoice line/charge FK, amount, actor/time, request key; unique request key; tổng phân bổ ≤ số dư. |
| `FINANCE_REFUND` | Id/code, original payment FK (hoặc nguồn thanh toán dossier), invoice/advance FK, patient/branch, amount, method, reason, requester/approver, requested/approved/completed dates, state/version; tổng hoàn thành ≤ phần có thể hoàn. |
| `FINANCE_ADJUSTMENT` | Id, source charge/invoice line FK, signed amount, reason, actor/time, request key; chỉ ghi thêm, dùng cho hủy/sửa dịch vụ đã chốt. |

`PAYMENT_RECEIPT` cần cho phép mục đích `Invoice`/`Advance` và các phương thức Cash, Transfer, POS, EWallet, Other theo danh mục cấu hình. Phân bổ hóa đơn cần một liên kết tới invoice line; có thể mở rộng `PAYMENT_ALLOCATION` nếu nguồn là `PAYMENT_RECEIPT`, còn nguồn `TREATMENT_DOSSIER_PAYMENT` cần adapter/đối chiếu rõ ràng. Không di chuyển dữ liệu cũ sang bảng mới bằng suy đoán. Migration phải backfill được quan hệ xác định chắc chắn và gắn cờ các bản ghi không xác định để đối soát.

Tất cả lệnh ghi dùng transaction, idempotency key, kiểm tra version và khóa theo patient + branch (hoặc khóa PostgreSQL tương đương). FK/unique/check constraint ở DB và kiểm tra số dư lại trong transaction. Webhook PayOS chỉ hoàn tất một giao dịch gốc; replay không tăng tiền thu. Quyền tách xem báo cáo, thu tiền, duyệt hoàn tiền, đối soát.

## 4. Hợp đồng API dự kiến

- `GET /api/finance/overview?from&to&branchId&doctorId&patientId&serviceId`: gross, discount, recognizedRevenue, collected, refunded, netCollected, outstanding, unallocatedAdvance, advanceForUnperformedServices; mỗi chỉ số kèm mốc thời gian và nguồn dữ liệu.
- `GET/POST /api/finance/invoices`, `GET /api/finance/invoices/{id}`; tạo từ charge/hoạt động đủ điều kiện, ghi `invoiceDate` và `dueDate` rõ ràng.
- `POST /api/finance/invoices/{id}/payments`: một giao dịch/một phương thức, cho phép nhiều lần; `POST /api/finance/invoices/{id}/allocations/advance` sử dụng số dư nhận trước mà không tạo giao dịch thu mới.
- `GET/POST /api/finance/advances`, `GET /api/finance/advances/{id}/history`; tạo nhận tiền trước qua phương thức thu thật, gắn bệnh nhân và tùy chọn lộ trình.
- `GET /api/finance/receivables`, `GET /api/finance/receivables/{patientId}/history`: số dư, ngày phát sinh, hạn thanh toán, các lần phân bổ/điều chỉnh.
- `GET/POST /api/finance/refunds`, `POST /api/finance/refunds/{id}/approve`, `POST /api/finance/refunds/{id}/complete` (hoặc xác nhận từ cổng thanh toán), `POST /api/finance/refunds/{id}/reject`; duyệt và thực hiện là hai trạng thái riêng.
- `GET /api/finance/reports/{recognized-revenue|collections|receivables|advances|refunds}` với lọc thời gian/chi nhánh/bác sĩ/bệnh nhân/dịch vụ, phân trang và tổng toàn bộ tập lọc. API `/revenue` cũ cần giữ tương thích, đổi nhãn UI thành “Tiền thực thu” trước khi có dashboard mới.

Lỗi dùng hợp đồng `{code,message}` hiện có: 400 dữ liệu sai; 403 ngoài quyền/chi nhánh; 404 không thấy; 409 version, trùng nguồn, vượt số dư hoặc hoàn vượt hạn mức. Không trả thành công khi cổng thanh toán còn Pending.

## 5. Giao diện và luồng

Giữ màn Thanh toán theo lượt khám/phiếu điều trị và bill PayOS trong trang bệnh nhân. Thêm nhóm Tài chính: dashboard tách doanh thu và tiền thu; danh sách/chi tiết hóa đơn; form thu nhiều lần và lịch sử; tiền nhận trước; công nợ; thanh toán theo lộ trình; yêu cầu/duyệt/thực hiện hoàn tiền; các báo cáo doanh thu, thu theo phương thức, công nợ, nhận trước, hoàn tiền. Tái sử dụng bảng, bộ lọc, phân trang, trạng thái tải/lỗi và kiểm quyền của web hiện tại. Mỗi màn cho phép truy về chứng từ nguồn.

## 6. Kiểm thử bắt buộc trước khi bàn giao

Kiểm thử theo bảng quyết toán với từng mốc ngày: trả đủ; trả thiếu rồi trả nợ hôm sau; ba phương thức cho một hóa đơn; đặt cọc rồi phân bổ; trả trước toàn lộ trình 4 giai đoạn không chia đều; trả nhiều đợt; hủy giai đoạn và hoàn; hoàn một phần/toàn bộ; giao dịch thất bại/hủy; hai nhân viên thu hoặc phân bổ đồng thời; replay request/webhook. Mỗi ca đối chiếu doanh thu thực hiện, thực thu, tiền thu ròng, công nợ, số dư nhận trước, lịch sử và trạng thái hóa đơn. Chạy unit/integration với PostgreSQL thật và build backend/web; đối soát riêng bản ghi lịch sử trước khi bật báo cáo mới.

## 7. Trình tự triển khai

1. Chốt quy tắc dữ liệu lịch sử và phân loại hai nguồn thu hiện hữu; migration an toàn và API đọc tổng hợp.
2. Hóa đơn, thu nhiều lần, công nợ và báo cáo theo đúng ngày.
3. Nhận trước, phân bổ theo lộ trình và màn liên quan.
4. Hoàn tiền, phê duyệt, đối soát và báo cáo; hồi quy mọi luồng thanh toán hiện có.

Điểm cần xác nhận trước migration dữ liệu thật: `TREATMENT_DOSSIER_PAYMENT` và `PAYMENT_RECEIPT` có thể cùng biểu diễn một lần thu ở dữ liệu cũ hay không; những trường hợp không ghép chắc chắn phải nằm trong danh sách đối soát, không tự động cộng đôi hoặc xóa bản ghi.

## 8. Tình trạng triển khai 09/11/2026

- Đã thêm hóa đơn nội bộ và dòng hóa đơn tham chiếu duy nhất tới `FINANCE_CHARGE`; nhận trước, phân bổ nhận trước, hoàn tiền có bước yêu cầu/duyệt/hoàn tất, phương thức thanh toán bổ sung theo chi nhánh. Migration chỉ thêm bảng/cột và mở rộng ràng buộc phiếu thu ở chiều áp dụng; không chuyển hay xóa lịch sử cũ.
- Đã có API tổng quan, hóa đơn, thu nhiều lần, nhận trước, công nợ, đối chiếu kế hoạch, hoàn tiền và phương thức thanh toán. Số dư hóa đơn/công nợ tổng hợp phiếu thu, thanh toán dossier và phân bổ nhận trước; hoàn tiền hoàn tất khôi phục số dư nghĩa vụ tương ứng. Giao dịch Pending không tính đã thu.
- Đã có màn **Tài chính** cho các luồng trên; màn doanh thu cũ đổi nhãn **Tiền thực thu**. Ngày thu tiền mặt dossier theo `PaidOn`; PayOS theo thời điểm xác nhận lưu trong `CreatedAt`; ngày dịch vụ ưu tiên `ClinicalActivity.PerformedAt`.
- Chưa có bảng bút toán `FINANCE_ADJUSTMENT`, hủy/sửa giai đoạn sau khi ghi nhận doanh thu, API báo cáo riêng với bộ lọc bác sĩ/bệnh nhân/dịch vụ và phân trang toàn bộ nguồn; báo cáo trên web hiện chỉ là phần tổng quan và bảng nguồn. Chưa có quy trình đối soát tự động dữ liệu thanh toán lịch sử không xác định chắc chắn.
- Kiểm thử đã bao phủ thu nhiều lần, nhận trước và phân bổ, hoàn một phần, quyền phương thức bổ sung, thu đồng thời, ngày dịch vụ khác ngày thu, và nhận trước toàn bộ lộ trình 4 giai đoạn giá khác nhau tổng 30 triệu mà không ghi nhận doanh thu sớm. Còn cần bổ sung ca hủy giai đoạn, PayOS webhook replay trong cùng báo cáo, và kiểm thử giao diện end-to-end trước khi coi toàn bộ kế hoạch hoàn tất.
