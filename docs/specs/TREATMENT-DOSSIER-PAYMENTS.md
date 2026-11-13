# Thanh toán theo phiếu điều trị — 03/11/2026

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

Cập nhật lưu trữ 12/11/2026 theo [hợp đồng rút gọn database](DATABASE-SIMPLIFICATION.vi.md). Đơn PayOS lưu các dòng được chọn trong `Lines` JSONB của `TREATMENT_PAYOS_ORDER`; ID hoạt động và số tiền tiếp tục dùng để đối chiếu kết quả thanh toán.

## Bỏ hàng chờ Thanh toán độc lập (09/11/2026)

Không còn menu **Thanh toán** và trang `/payments` có tiêu đề **Danh sách cần thu**. Nhân viên mở bệnh nhân và dùng tab **Điều trị & Thanh toán** để thu theo phiếu. URL `/payments` cũ chuyển về danh sách bệnh nhân. Màn chi tiết thanh toán lượt khám `/visits/{visitId}/payments` và API thu tiền vẫn được giữ cho quy trình khám/cận lâm sàng, nhưng không còn điều hướng về hàng chờ cũ. Các liên kết từ màn dịch vụ và in phiếu thu dẫn về nơi có ngữ cảnh phù hợp.

## Nhận trước bằng chuyển khoản PayOS

Trong **NHẬN TRƯỚC...**, nhân viên chọn tiền mặt hoặc chuyển khoản. Tiền mặt tạo khoản nhận trước `Posted` ngay. Chuyển khoản tạo khoản nhận trước và phiếu thu `Pending`, lệnh PayOS riêng có QR hiển thị trong cùng hộp thoại. Bệnh nhân không bị chuyển sang trang PayOS. Một lệnh gồm đúng một khoản nhận trước, có mã yêu cầu duy nhất, số tiền cố định, hạn 30 phút và liên kết tới bệnh nhân/chi nhánh/phiếu điều trị. Mở lại hộp thoại phải thấy lệnh còn hiệu lực. Client hỏi trạng thái định kỳ; webhook ký hợp lệ và server polling cùng dùng một thao tác chốt có khóa/idempotency. Chỉ khi PayOS báo `PAID`, đúng order code, payment link, VND và số tiền đã trả bằng số tiền yêu cầu, server chuyển phiếu thu sang `Posted` và cộng số dư khả dụng. Lệnh hết hạn/hủy/lỗi không tăng số dư; dữ liệu sai vào `Review` để đối soát. Không tạo `TREATMENT_DOSSIER_PAYMENT` cho cọc vì chưa gắn vào việc đã làm. API: `POST /api/dossiers/{id}/advance-payos-orders`, `GET /api/dossiers/{id}/advance-payos-orders/active`, `GET /api/dossiers/{id}/advance-payos-orders/{orderId}`. Webhook `/api/payments/payos/webhook` định tuyến cả lệnh thanh toán dịch vụ và lệnh nhận trước.

## Luồng chính trong hồ sơ bệnh nhân (09/11/2026)

Điểm vào nghiệp vụ là **Bệnh nhân → Điều trị & Thanh toán** của đúng phiếu điều trị. Nhân viên thấy nợ của các việc đã làm, lịch sử thu tiền và số dư nhận trước ngay tại đây. Nút **TRẢ TIỀN...** thu từng việc đã làm bằng tiền mặt hoặc chuyển khoản PayOS; mỗi lần thu là một giao dịch riêng và chỉ giao dịch đã xác nhận mới giảm nợ. Bill và QR chuyển khoản nằm trong hộp thoại của hồ sơ, không chuyển sang PayOS. Nếu chưa có việc đã làm hoặc cần thu cho giai đoạn chưa thực hiện, nhân viên chọn **NHẬN TRƯỚC...**, nhập loại cọc/ứng trước và số tiền. Tiền mặt được ghi nhận ngay; các phương thức chưa xác nhận chỉ là Pending và không được coi là đã thu hay số dư khả dụng. Số dư nhận trước được hiển thị riêng, không tự trừ nợ hoặc tính doanh thu; khi phân bổ vào dịch vụ đã thực hiện phải có dòng hóa đơn nguồn và thao tác phân bổ minh bạch. Lịch sử thu và nhận trước đều truy cập từ phiếu. Màn Tài chính dùng để tổng hợp và quản lý chứng từ nhiều bệnh nhân, không phải điểm bắt đầu bắt buộc của nhân viên trong hồ sơ.

API tái sử dụng: `GET /api/finance/advances?branchId&patientId`, `POST /api/finance/advances` với `dossierId` cố định theo phiếu và `GET /api/finance/plans/{dossierId}/settlement`. Giao diện không cho sửa mã bệnh nhân, chi nhánh hoặc phiếu trong yêu cầu nhận trước. Quyền xem là `finance.read`; tạo nhận trước và thanh toán là `finance.payments.create`. Lỗi API được giữ trên hộp thoại để nhân viên sửa và thử lại; gửi lại cùng request key không tạo bản ghi thứ hai.

Bill xem trước khi chọn chuyển khoản, danh mục dịch vụ/vật liệu trong ngày và QR được mô tả tại [TREATMENT-TRANSFER-BILL.md](TREATMENT-TRANSFER-BILL.md).

Ghi nhận tiền khách trả cho các việc đã làm trong **QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ** của một phiếu điều trị (dossier). Thay thế nguồn "ledger DCMS-101" cho nút **TRẢ TIỀN...** của phiếu; module Finance theo lượt khám (`/api/finance/visits/...`) và các endpoint `/api/dossiers/{id}/finance`, `/collections` không đổi và không được dùng bởi form này.

## Quy tắc
- Chỉ các hoạt động của phiếu có `State = Posted`, chưa bị xóa khỏi phiếu (`SheetDeleted`) mới xuất hiện. Giá trị hiệu lực là giá trị đang hiển thị ở bảng quá trình (đã tính sửa trên phiếu).
- Mỗi dòng: **Chi phí** = `round(Quantity × PriceSnapshotVnd)`; **Giảm giá** = `DiscountSnapshotVnd`; **Đã trả** = tổng các dòng thanh toán đã lưu cho hoạt động; **Còn nợ** = `max(0, Chi phí − Giảm giá − Đã trả)`.
- Số tiền mỗi dòng là số nguyên VND, `0 ≤ số tiền ≤ Còn nợ`. Dòng bằng 0 không được lưu. **Tổng tiền thanh toán** = tổng các dòng và phải > 0.
- Hoạt động có `ChargingBasis = Included` có Còn nợ 0 và không nhập được.
- Ngày thanh toán là ngày (giờ Việt Nam), không ở tương lai.
- `Cash` (Tiền mặt) qua `POST /payments` tính vào Đã trả ngay khi lưu. `Transfer` mới phải đi qua lệnh PayOS và chỉ tính vào Đã trả khi PayOS xác nhận; xem [bill chuyển khoản](TREATMENT-TRANSFER-BILL.md). Dữ liệu chuyển khoản cũ đã lưu vẫn được giữ nguyên.
- Người thu là một nhân viên đang hoạt động thuộc chi nhánh của phiếu; mặc định là nhân viên của tài khoản đang đăng nhập.
- Ghi chú tối đa 1000 ký tự.
- Một thanh toán đã lưu là bất biến (không sửa/xóa trong đợt này). Hoàn tiền/hủy là yêu cầu riêng.
- Phiếu **Đã đóng** vẫn thu được nợ còn lại. Phiếu đã xóa trả 404.
- **Đã trả của phiếu** = tổng mọi dòng thanh toán của phiếu. **Còn nợ của phiếu** hiển thị trên phiếu = tổng Còn nợ các dòng việc đã làm (không phải Thành tiền kế hoạch − Đã trả), như đã quy định ở [PATIENT-TREATMENT-PAYMENT-UI.md](PATIENT-TREATMENT-PAYMENT-UI.md).
- Sửa chi phí/giảm giá của việc đã làm hoặc xóa việc đã có thanh toán bị chặn nếu làm Chi phí − Giảm giá thấp hơn số đã trả của việc đó (409/MSG111). Thay đổi này chỉ chặn ở mức dịch vụ, không đổi schema thanh toán.

## API (policy `TreatmentDentist`, người dùng phải là nha sĩ của chi nhánh phiếu)
- `GET /api/dossiers/{dossierId}/payments/context` → `{ paidVnd, lines: [{ activityId, performedAt, dentistName, serviceName, costVnd, discountVnd, paidVnd, dueVnd }], collectors: [{ id, name }], defaultCollectorId }`.
- `GET /api/dossiers/{dossierId}/payments` → danh sách mới nhất trước: `{ id, paidOn, method, collectorId, collectorName, note, totalVnd, createdAt, lines: [{ activityId, serviceName, amountVnd }] }`.
- `POST /api/dossiers/{dossierId}/payments` body `{ paidOn: "yyyy-MM-dd", method: "Cash", collectorId, note?, lines: [{ activityId, amountVnd }], requestKey }` → 200 với `DossierPaymentDto`. `method: "Transfer"` bị từ chối; dùng `POST /api/dossiers/{dossierId}/payos-orders`.
- Lỗi theo `{code,message}`: MSG04 dữ liệu không hợp lệ (số tiền ≤ 0, tổng 0, ngày tương lai, phương thức/người thu sai, hoạt động không thuộc phiếu); MSG111 số tiền vượt Còn nợ hiện tại hoặc `requestKey` bị dùng lại với nội dung khác; MSG06 không có quyền; MSG113 không tìm thấy phiếu.
- Idempotency: cùng `requestKey` + cùng nội dung trả lại kết quả đã lưu, không tạo thêm; khác nội dung → MSG111. Server khóa và kiểm tra lại Còn nợ trong cùng transaction nên hai lần lưu đồng thời không thể vượt Còn nợ.

## Dữ liệu
- `TREATMENT_DOSSIER_PAYMENT`: Id, DossierId, PatientId, BranchId, PaidOn (date), Method, CollectorEmployeeId, Note, TotalVnd, CreatedBy, CreatedAt.
- `TREATMENT_DOSSIER_PAYMENT_LINE`: PaymentId, ActivityId, AmountVnd (> 0); khóa (PaymentId, ActivityId).
- Idempotency dùng bảng receipt có sẵn `TREATMENT_PROGRESSION_RECEIPT` với `Command = "dossier-payment"`.
- Cần migration `DossierPayments`. Áp vào `dcms_dev` chỉ sau khi người dùng đồng ý riêng.

## Giao diện
Xem mục "Form Tạo thanh toán mới" trong [PATIENT-TREATMENT-PAYMENT-UI.md](PATIENT-TREATMENT-PAYMENT-UI.md).

## Bỏ dòng thông báo thiếu quyền Lập phiếu thu (07/11/2026)

Khu vực thanh toán của phiếu điều trị không còn hiện dòng "Chỉ xem: tài khoản chưa có quyền Lập phiếu thu." khi tài khoản thiếu `finance.payments.create`. Nút "Trả tiền" vẫn bị khóa hoặc ẩn theo quyền như cũ; quyền ở máy chủ không đổi.


## Thanh tiêu đề màn Thanh toán gọn hơn (08/11/2026)

Thanh tiêu đề "Danh sách cần thu" (tiêu đề, nút Tải lại thanh toán, liên kết Danh mục dịch vụ) căn trái, khoảng cách cố định 16px giữa các mục, không đệm thêm quanh từng mục, nút giữ đệm ngang 16px. Ô chọn chi nhánh nằm ngay dưới thanh, cách 12px.


## Bảng có đường viền (08/11/2026)

Các bảng trong nhóm màn Thanh toán/Doanh thu/Dịch vụ cận lâm sàng/Thống kê (vùng `.finance-page`) có viền ngoài và đường kẻ giữa các ô, tiêu đề cột nền nhạt. Dữ liệu không đổi.
