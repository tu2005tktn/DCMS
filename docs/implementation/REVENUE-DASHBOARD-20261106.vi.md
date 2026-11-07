# Màn doanh thu và kiểm tra chạy local — 06/11/2026

- Thêm `GET /api/finance/revenue` và trang `/revenue`, dùng quyền `finance.read`. Báo cáo cộng chứng từ Finance `Posted` và thanh toán phiếu điều trị đã lưu, tách tiền mặt/chuyển khoản và khoản đang chờ, lọc theo ngày Việt Nam và chi nhánh.
- .NET build và web build đạt; 2/2 unit test chữ ký PayOS đạt; 2/2 Playwright ca form thu tiền và bill chuyển khoản đạt khi Vite chạy ở cổng 5173.
- PostgreSQL Docker local đang healthy ở cổng 5433. API và web đã khởi động; `GET /health` và trang web trả 200. API doanh thu không xác thực trả 401 đúng kỳ vọng.
- Database local có schema lệch migration: `--migrate` dừng tại `20261027120927_ClinicalServiceBilling` vì bảng `PAYMENT_RECEIPT` đã tồn tại, trong khi các bảng như `TREATMENT_PAYOS_ORDER`, `CLINIC_SETTINGS_VERSION` chưa tồn tại. Thử bằng tài khoản demo `owner.demo` trả 503 ở báo cáo; vì vậy chưa thể xác nhận màn doanh thu với dữ liệu thật trên DB này. Không sửa migration history hoặc xóa bảng dữ liệu tự động.
- Chưa tạo giao dịch PayOS thật hoặc xác nhận webhook/ngân hàng trong kiểm thử.
