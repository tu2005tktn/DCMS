# Đơn thuốc của bệnh nhân trên web và ứng dụng

## Phạm vi

- Web bệnh nhân và ứng dụng Expo giữ chức năng đặt, xem, đổi và hủy lịch hẹn hiện có. Trang chủ và menu phải có lối vào rõ ràng tới danh sách lịch hẹn.
- Thêm danh sách và chi tiết đơn thuốc S23 do nha sĩ lưu trong hồ sơ bệnh nhân. Chỉ đọc: ngày kê, chi nhánh, chẩn đoán, lời dặn, bác sĩ, tên/hàm lượng/dạng/đơn vị/số lượng/hướng dẫn từng thuốc và nhãn thuốc phòng khám hoặc mua ngoài. Không cho bệnh nhân sửa đơn, không hiện giá hay dữ liệu quản trị. Đơn theo lượt khám S19 chưa được gộp vì hiện chỉ lưu từng dòng thuốc riêng, không có đơn hoàn chỉnh.
- Chưa liên kết hoặc liên kết chưa VERIFIED: màn hình chỉ giải thích cần xác minh hồ sơ và dẫn đến trang liên kết. Không lấy đơn thuốc bằng số điện thoại, mã bệnh nhân nhập tay, hoặc lịch đặt hộ. Thu hồi liên kết phải chặn lần đọc tiếp theo và xóa nội dung cũ khỏi giao diện.

## API

- `GET /api/me/prescriptions?page=1&pageSize=20` trả `{items,total,page,pageSize}` sắp xếp ngày kê giảm dần, chỉ đơn của `PatientId` trong liên kết VERIFIED của tài khoản Public hiện hành. Page size cố định 20; không nhận patientId/accountId từ client.
- `GET /api/me/prescriptions/{id}` trả chi tiết cùng các dòng thuốc đã lưu snapshot. Đơn khác bệnh nhân hoặc không tồn tại trả 404; chưa có liên kết VERIFIED trả 403; chưa đăng nhập trả 401. Mọi request kiểm tra link trên database, không chỉ tin claim/token cũ.
- DTO danh sách không chứa dòng thuốc; DTO chi tiết có `requiresExternalPurchase`, `items[].isExternalPurchase`. Không trả mã nội bộ, giá, ID nhân viên hoặc thông tin bệnh nhân khác.

## Giao diện và nghiệm thu

- Web: `/customer/prescriptions` trong menu và trang chủ; danh sách 20 đơn/trang, mở `/customer/prescriptions/:id`, loading/empty/error/retry rõ ràng, 401 hết phiên và 403 dẫn tới liên kết hồ sơ. Lịch hẹn hiện có vẫn ở `/customer/appointments` và có lối vào từ trang chủ/menu.
- Mobile: từ trang tài khoản Public có nút Lịch hẹn của tôi và Đơn thuốc của tôi; trang đơn có danh sách và chi tiết, tải lại, tải thêm, trạng thái chưa xác minh và lỗi. Dùng token phiên Public hiện có. Lịch hẹn hiện có giữ quyền và thao tác đặt/đổi/hủy.
- Kiểm thử API không liên kết/đã xác minh/đã thu hồi, cross-patient ID và phân trang; build .NET/web/mobile, UI desktop/mobile và hồi quy lịch hẹn.
