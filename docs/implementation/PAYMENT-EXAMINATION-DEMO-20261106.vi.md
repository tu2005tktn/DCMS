# Ca khám tổng hợp để thử thanh toán — 06/11/2026

- Chỉ tạo trong database local `dcms_schema_check_20261106`; không sửa `dcms_dev`. Bệnh nhân demo `BN-DEMO-0001` có một lượt khám đã hoàn thành, chẩn đoán sâu răng nhẹ, phí khám 100.000 VND đã ghi sổ nhưng chưa thu. Màn thanh toán phải hiện tổng 100.000, đã thu 0, còn 100.000.
- Ca thử được dựng idempotent bằng SQL có guard tên database và mã lịch `AP-PAYMENT-DEMO-20261106`. Do đã qua giờ nhận lịch trong ngày, không thể dùng luồng đặt lịch/tiếp nhận thật ngay lúc này; chỉ dùng ca này để thử giao diện và API thanh toán, không dùng làm bằng chứng quy trình lâm sàng.
- Sau khi tạo, đọc lại qua `GET /api/visits/{visitId}/examination` và `GET /api/finance/visits/{visitId}`. Không tự tạo thanh toán, để người dùng thử tiền mặt/chuyển khoản và xác nhận theo ý muốn.
- Đã tạo và xác minh: `VisitId=9e7d59b0-6d7c-4f7c-8d16-000000000002`, bệnh nhân `BN-DEMO-0001` / Nguyễn Văn An. Cả `owner.demo` và `dentist.demo` đọc được phiếu khám Completed và bill tổng 100.000đ, đã thu 0đ, còn 100.000đ, một khoản phí. Mở `/visits/9e7d59b0-6d7c-4f7c-8d16-000000000002/payments` để thử.
- Màn thanh toán lượt khám hiện dùng Finance Cash/Transfer Pending; QR PayOS nằm ở thanh toán phiếu điều trị và không phát sinh từ ca khám này.

## Bổ sung ca điều trị đúng màn thanh toán của hồ sơ

Theo phản hồi ngày 07/11/2026, ca thử cần hiện ở tab **Điều trị & Thanh toán** của `BN-DEMO-0001`. Tạo một phiếu điều trị riêng bằng API và một việc đã làm có phí còn nợ; không tự tạo khoản thanh toán hoặc lệnh PayOS. Kiểm tra lại `GET /api/patients/1/dossiers` và `GET /api/dossiers/{id}/payments/context`, rồi cung cấp link hồ sơ mở sẵn tab điều trị. Dữ liệu thử chỉ nằm trong database local demo.

## Quyền của nút thanh toán

`dentist.demo` chỉ có `finance.read`, nên nút **TRẢ TIỀN** bị khóa và API tạo thanh toán trả 403. Đây là phân quyền dự kiến: `finance.payments.create` chỉ cấp được cho vai trò Clinic Assistant hoặc Clinic Owner, không cấp cho Dentist thuần. `dual.demo` và `owner.demo` có quyền tạo thanh toán, cùng đọc được phiếu demo `2e4ae863-5959-4567-8fbf-a8a22b6b9f79` với một dịch vụ còn nợ 200.000 VND. Giao diện cần nêu rõ lý do khóa nút để tránh hiểu là lỗi thanh toán.
