# Thanh toán theo phiếu điều trị — 03/11/2026

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
