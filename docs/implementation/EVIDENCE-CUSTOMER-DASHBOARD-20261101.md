# Dashboard cổng bệnh nhân — 01/11/2026

Phạm vi: chỉ Web và tài liệu. Tách CustomerDashboard, dùng lại GET danh sách lịch sắp tới, hiển thị lịch đầu tiên đúng DTO hiện có và truy cập nhanh tới route đặt lịch/danh sách/liên kết hồ sơ. Không thay backend, API contract, quyền hoặc quy tắc lịch. Không tạo panel thông báo khi chưa có nguồn; không thêm AI, răng, đơn thuốc hoặc lịch sử điều trị.

Kiểm thử:
- Web `npm run build`: đạt trên bản cuối; còn warning SignalR annotation và kích thước chunk.
- Playwright customer-dashboard.spec.ts: 5/5 đạt (11.4s), API mock. Có lịch/đúng nguồn và route, empty, lỗi/retry, 401 và 403; không phát sinh command ghi.
- staff-workspace-ui.spec.ts: 5/5 đạt trong lượt hồi quy cùng Dashboard, kiểm UI nhân viên và customer sign-in không chịu ảnh hưởng. Lượt đó 2 ca Dashboard lỗi do fixture lỗi bị StrictMode tiêu thụ trước và assertion ngày lệ thuộc locale; đã sửa fixture/assertion và chạy lại 5 ca Dashboard đạt.
- Sau chỉnh độ tương phản thẻ đặt lịch, chạy lại ca chính Dashboard: 1/1 đạt, kiểm và chụp ở 390/768/1440. Đã xem ảnh desktop và mobile, bảng điều khiển không tràn ngang.
- Dữ liệu trong ảnh là dữ liệu tổng hợp từ mock, không ghi database. Chưa nghiệm thu tích hợp API đang chạy hoặc luồng đặt/đổi/hủy từ đầu đến cuối trong lượt này.

Không chạy Git, migration hoặc seed. Các sửa đổi customer có sẵn được giữ; CustomerPages chỉ thay phần Home bằng component Dashboard mới, customer.css không sửa trong lượt này.
