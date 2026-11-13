# Màn hình doanh thu — 06/11/2026

> **Đã gỡ ngày 13/11/2026.** Phân hệ tài chính đã được xóa khỏi backend, web và database theo [CARE-LAB-FINANCE-STORAGE.vi.md](CARE-LAB-FINANCE-STORAGE.vi.md). Tài liệu này chỉ còn giá trị tham khảo khi đặc tả lại chức năng; không mô tả hành vi hiện tại.

## Mục đích và nguồn

- Màn **Doanh thu** cho người có `finance.revenue.read` (cha `finance.read`, cập nhật 07/11/2026) xem tiền thực nhận theo ngày Việt Nam và chi nhánh được cấp quyền. Mặc định tháng hiện tại; có hôm nay, tuần, tháng, khoảng tùy chọn và tất cả. Không cho chọn chi nhánh ngoài phạm vi tài khoản.
- Nguồn 1: `PAYMENT_RECEIPT` theo lượt khám, ledger dossier hoặc tiền nhận trước, chỉ `State=Posted`; ngày thực nhận là `ConfirmedAt` (tiền mặt cũng được post ngay). Nguồn 2: `TREATMENT_DOSSIER_PAYMENT` của form TRẢ TIỀN; tiền mặt theo `PaidOn`, chuyển khoản PayOS đã xác nhận theo `CreatedAt`. Hai nguồn là hai loại chứng từ riêng, không nhân đôi từ các dòng phân bổ.
- `PAYMENT_RECEIPT` Transfer `Pending` và `TREATMENT_PAYOS_ORDER` Pending/Creating/Review không tính doanh thu. Tổng đang chờ chỉ gồm receipt Pending và PayOS order Pending, không xem Review là tiền đã thu.
- Số trên màn là **tiền đã thu**, không phải doanh thu dồn tích/kế toán, lợi nhuận hoặc giá vốn. Vật liệu trên bill không tạo doanh thu riêng. Không cộng chi phí, kế hoạch điều trị hay đơn thuốc vào tiền đã thu.

## Giao diện

- Thẻ tổng: đã thu, tiền mặt, chuyển khoản, phương thức khác, khoản đang chờ; biểu đồ theo ngày; bảng chứng từ với ngày/giờ, chi nhánh, bệnh nhân, nguồn, phương thức, số tiền và đường dẫn về màn gốc. Nhãn màn hình là **Tiền thực thu** để phân biệt với doanh thu dịch vụ ở màn Tài chính.
- Bộ lọc thời gian và chi nhánh; tổng số tính trên toàn bộ tập đã lọc, bảng phân trang. Ngày hiển thị theo Asia/Ho_Chi_Minh. Trạng thái trống/lỗi/tải lại rõ ràng.

## API

`GET /api/finance/revenue?from=yyyy-MM-dd&to=yyyy-MM-dd&branchId=&page=1&pageSize=20` trả `{from,to,totals:{receivedVnd,cashVnd,transferVnd,otherVnd,pendingVnd,byMethod},daily:[{date,cashVnd,transferVnd,otherVnd}],total,items:[{id,source,sourceId,patientId,patientName,branchId,branchName,receivedAt,paymentDate,method,amountVnd}]}`. `from/to` là ngày bao gồm hai đầu theo giờ Việt Nam. Giới hạn tối đa 366 ngày cho một truy vấn để kiểm soát tải; không có ngày thì mặc định tháng hiện tại. `pageSize` tối đa 100. API chỉ đọc, không thay đổi chứng từ.
