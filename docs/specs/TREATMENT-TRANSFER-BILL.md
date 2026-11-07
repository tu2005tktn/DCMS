# Bill chuyển khoản PayOS cho phiếu điều trị — 06/11/2026

## Luồng

- Form **TRẢ TIỀN** vẫn cho nhập số tiền theo từng việc đã làm, thu một phần và chọn Tiền mặt/Chuyển khoản. Tiền mặt tiếp tục ghi nhận ngay. Chọn Chuyển khoản rồi bấm **TẠO QR VÀ XEM BILL** tạo một yêu cầu thanh toán PayOS; không ghi vào Đã trả lúc này.
- Một kênh PayOS và tài khoản nhận chung cho các chi nhánh. Các khóa `PAYOS_CLIENT_ID`, `PAYOS_API_KEY`, `PAYOS_CHECKSUM_KEY`, `PAYOS_WEB_BASE_URL` ở `.env` chỉ được nạp vào backend. `PAYOS_WEB_BASE_URL` là URL web để tạo `returnUrl` và `cancelUrl` theo yêu cầu PayOS, không dùng làm chứng cứ đã thu. Web không nhận khóa API.
- Bill nằm ngay trong DCMS, không chuyển đến trang checkout PayOS. QR được render từ chuỗi `qrCode` do PayOS trả về, kèm ngân hàng, số tài khoản, chủ tài khoản, mã lệnh và tổng tiền. Quay lại giữ form. Đóng/mở lại phiếu có thể lấy lệnh đang chờ.
- Bill liệt kê tất cả dịch vụ đã thực hiện trong ngày được chọn theo UTC+7, gồm chi phí, giảm giá, đã trả và tiền phân bổ lần này. Dịch vụ ngày khác được phân bổ hiển thị riêng. Vật liệu thực tế xuất dùng gắn với từng activity gồm tên, số lượng, đơn vị; chỉ là thông tin, không cộng thêm giá hoặc tiết lộ giá vốn.
- Một dossier chỉ có một lệnh PayOS đang chờ hoặc cần đối soát. Cùng request key và payload trả cùng lệnh; cùng payload khi mở lại cũng trả lệnh cũ. Payload khác bị 409. Lệnh lỗi tạo QR, hủy hoặc hết hạn không tạo payment. Khi còn lệnh chờ, thao tác thu tiền mặt trên dossier bị chặn để tránh ghi đè số dư.

## Xác nhận và đối soát

- Backend nhận webhook PayOS tại `POST /api/payments/payos/webhook` không yêu cầu JWT nhưng bắt buộc xác thực HMAC-SHA256 bằng checksum key. So khớp order code, payment link ID, số tiền, VND và trạng thái thành công. Không tin dữ liệu URL return/cancel, ảnh chụp QR hoặc thao tác từ trình duyệt.
- Bill gọi `GET .../payos-orders/{orderId}` định kỳ; backend hỏi trạng thái link từ PayOS. Vì vậy bill tự cập nhật khi đang mở, ngay cả khi webhook chưa được cấu hình. Webhook cho phép tự xác nhận khi người dùng đã đóng bill; URL webhook phải truy cập được qua HTTPS và đăng ký trong PayOS.
- Máy chủ cũng đối soát lệnh Pending với PayOS định kỳ để tự xác nhận khi bill đã đóng hoặc webhook chưa tới; chỉ trạng thái PayOS đủ tiền mới tạo payment.
- Khi PayOS xác nhận đủ tiền, backend khóa giao dịch, kiểm tra lại dư của từng activity, tạo một `DossierPayment` phương thức `Transfer` với ngày thu thực tế theo UTC+7 và phân bổ một lần; cùng transaction đánh dấu lệnh `Paid`. Webhook/poll lặp không tạo tiền hai lần. Nếu dư đã đổi, chuyển lệnh sang `Review` và không ghi vượt dư.
- Lệnh chờ không tính vào Đã trả hoặc doanh thu tiền đã thu. Trạng thái `Paid` mới được tính. `Review` cần quy trình xử lý riêng; không tự sửa số tiền hoặc chuyển bệnh nhân.

## API và dữ liệu

- `GET /api/dossiers/{id}/payments/context` bổ sung `dossierCode`, `patientName`, `materials: [{name,quantity,unit}]` cho mỗi dòng. Chỉ activity đã Posted và chưa xóa khỏi phiếu; không trả giá vốn.
- `POST /api/dossiers/{id}/payos-orders` nhận `{paidOn,collectorId,note,lines:[{activityId,amountVnd}],requestKey}`. `GET /api/dossiers/{id}/payos-orders/active` và `GET /api/dossiers/{id}/payos-orders/{orderId}` trả trạng thái, QR, tài khoản nhận và phân bổ. Quyền tạo `finance.payments.create`, đọc `finance.read`, vẫn cần quyền nha sĩ với dossier đúng chi nhánh.
- `GET /api/dossiers/{id}/payos-orders/active` trả JSON literal `null` với `Content-Type: application/json` khi không có lệnh đang hoạt động; không trả 200 với body rỗng. Modal thanh toán phải mở bình thường trong trường hợp này.
- Khi PayOS không kết nối được, `POST /api/dossiers/{id}/payos-orders` trả lỗi JSON có mã `PAYOS_UNAVAILABLE` và HTTP 503 để giao diện hiện nguyên nhân; lỗi ghi log của môi trường không được làm đứt phản hồi API. Chạy API trên Windows bằng console/debug logging, không phụ thuộc quyền ghi Windows Event Log.
- `PAYOS_WEB_BASE_URL` là tùy chọn: nếu không khai báo, backend lấy origin đầu tiên trong `Cors:AllowedOrigins` làm URL quay lại cho PayOS. Biến này không phải endpoint API PayOS và không quyết định kết nối tới PayOS; môi trường chạy backend cần có quyền kết nối HTTPS ra `api-merchant.payos.vn:443`.
- Bảng `TREATMENT_PAYOS_ORDER` và `TREATMENT_PAYOS_ORDER_LINE` giữ lệnh, phân bổ, khóa idempotency, trạng thái, QR và liên kết payment đã xác nhận. Migration cộng thêm, không sửa payment cũ.
- `POST /api/dossiers/{id}/payments` vẫn dùng cho tiền mặt trong giao diện này; thanh toán chuyển khoản mới phải qua lệnh PayOS. Luồng thu ngân theo lượt khám là phạm vi riêng, chưa chuyển sang PayOS trong thay đổi này.

## Kiểm tra

- Một/nhiều dịch vụ trong ngày, dịch vụ ngày khác, vật liệu, không có vật liệu, thu một phần; QR số tiền khớp phân bổ và không chứa khóa hoặc tên bệnh nhân.
- Idempotency và lệnh chờ, webhook chữ ký sai, sai số tiền/link, replay webhook/poll, thay đổi dư trước khi thanh toán, hủy/hết hạn, mất kết nối PayOS, đóng/mở lại bill.
- Build .NET/web, browser test luồng bill và tiền mặt; API/DB cần schema thử nghiệm riêng. Không tạo giao dịch PayOS thật trong kiểm thử tự động.
