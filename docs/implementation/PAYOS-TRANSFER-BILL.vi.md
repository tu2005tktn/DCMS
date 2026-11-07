# PayOS chuyển khoản trong bill điều trị — 06/11/2026

## Cấu hình vận hành

1. Đặt `PAYOS_CLIENT_ID`, `PAYOS_API_KEY`, `PAYOS_CHECKSUM_KEY` trong `.env` ở root. `PAYOS_WEB_BASE_URL` chỉ cần khi muốn ghi đè URL quay lại (mặc định dùng `Cors:AllowedOrigins:0`); biến này không ảnh hưởng kết nối API PayOS. Không đưa khóa vào `web/.env` hoặc Git. Script `infrastructure/scripts/import-development-env.ps1` nạp các biến vào backend; khởi động API qua `start-api.ps1` như hướng dẫn README.
2. Áp migration `20261106153353_TreatmentPayOsOrders` lên cơ sở dữ liệu mục tiêu bằng quy trình migration hiện có. Không chạy migration trên dữ liệu thật khi chưa có kế hoạch sao lưu/triển khai phù hợp.
3. Để webhook hoạt động khi khách đóng bill, công bố API qua HTTPS và đăng ký `https://<api-host>/api/payments/payos/webhook` với kênh PayOS. Cấu hình 4 biến `.env` không tự đăng ký webhook. Worker backend vẫn hỏi PayOS mỗi 30 giây cho lệnh Pending khi API đang chạy, và bill hỏi trạng thái mỗi 3 giây.
4. Kiểm tra một giao dịch tiền nhỏ có kiểm soát trên kênh PayOS sau khi môi trường triển khai hoàn chỉnh: QR hiển thị ngay trong DCMS, trạng thái Pending trước khi chuyển, Paid sau khi PayOS báo đủ tiền, lịch sử phiếu chỉ tăng một lần. Không dùng dữ liệu bệnh nhân thật trong lần thử đầu.

## Đã kiểm tra trong workspace

- `dotnet restore backend/DCMS.sln` và `dotnet build backend/DCMS.sln --no-restore`: đạt.
- `npm.cmd install`, `npm.cmd run build`: đạt.
- `PayOsGatewayTests`: 2/2 đạt, gồm chữ ký tạo link và xác minh chữ ký webhook khi dữ liệu bị đổi.
- Playwright mock API: 2/2 ca tiền mặt và bill PayOS đạt; bill hiển thị dịch vụ/vật liệu, không điều hướng, trạng thái tự đổi sang Đã thanh toán khi API trả Paid.
- Không gọi API tạo link PayOS thật trong kiểm thử. Docker Desktop chưa chạy, cổng PostgreSQL theo `.env` không kết nối được, nên chưa chạy migration/integration/health trên môi trường này. Chưa thể xác nhận webhook thực từ PayOS hoặc quét QR bằng ngân hàng.

## Ranh giới dữ liệu

- Luồng phiếu điều trị chuyển khoản mới dùng PayOS. Phiếu thu thu ngân theo lượt khám vẫn có Cash/Transfer và quy tắc Pending/xác nhận hiện hữu. Dữ liệu chuyển khoản điều trị lịch sử được giữ nguyên.

## Kiểm tra kết nối 07/11/2026

- `PAYOS_WEB_BASE_URL` đã trở thành tùy chọn và được bỏ khỏi `.env` cục bộ; URL quay lại dùng `Cors:AllowedOrigins:0`. Biến này không phải nguyên nhân lỗi kết nối PayOS.
- Trong sandbox, kết nối TCP tới `api-merchant.payos.vn:443` bị chặn; cùng máy, kiểm tra ngoài sandbox trả thành công. Backend chạy ngoài sandbox đã gọi PayOS thật và tạo lệnh thử 50.000đ, trạng thái `Pending`, có QR và thông tin tài khoản nhận. API hỏi trạng thái và active order đều trả `Pending`; khoản 50.000đ vẫn chưa được ghi nhận thanh toán.
- Backend build và 2 unit test PayOS đạt; web build đạt. Chưa kiểm tra chuyển tiền thật hoặc webhook Paid vì chưa thực hiện giao dịch ngân hàng.
- Vật liệu trên bill không phải doanh thu vật liệu riêng và không có giá vốn. Trạng thái Review không tự ghi thanh toán; cần đối soát thủ công trước khi xử lý số tiền đã chuyển.

## Sửa lỗi build sau tích hợp PR #46 — 07/11/2026

- Mapping EF của `DossierPayOsOrderLine` phải kết thúc trước khi khai báo mapping `LabOrder`. Bổ sung dấu đóng `});` bị thiếu sau tích hợp để hai entity được cấu hình độc lập và backend biên dịch được.
- Bản sửa chỉ khôi phục cấu trúc khai báo model; giữ nguyên bảng, khóa, ràng buộc và API PayOS/labo hiện có, không thêm migration hoặc thay đổi dữ liệu.
- Kiểm tra yêu cầu: restore/build solution bằng .NET 10, chạy unit test (gồm model snapshot và PayOS) và các test model điều trị hiện có.
- Kết quả sau sửa: restore/build solution đạt (0 lỗi); toàn bộ 87 unit test đạt; 4 test trong `TreatmentModelTests` và `AppointmentModelTests` đạt. Các test model xác nhận snapshot không lệch mapping hiện tại và không kết nối database. Chưa chạy hai migration PayOS/đơn thuốc mới hoặc kiểm thử giao dịch PayOS thật trong lần sửa này.
