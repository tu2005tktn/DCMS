# Staff management UI — 31/10/2026

## Bổ sung: sidebar luôn hiển thị

Theo yêu cầu tiếp theo, bỏ chế độ ẩn sidebar trong hồ sơ bệnh nhân và trên màn hình nhỏ. Desktop giữ menu đầy đủ; dưới 992px giữ thanh icon 64px, có tên truy cập và nút mở menu đầy đủ. Sidebar cố định khi cuộn; bản in tiếp tục ẩn menu. Điều chỉnh hàng nút phiếu trên mobile để không tràn ngang. Không đổi API, phân quyền hoặc logic nghiệp vụ.

- `npm run build`: đạt; còn cảnh báo SignalR annotation và kích thước chunk.
- Playwright `staff-workspace-ui.spec.ts` + `patient-treatment-ui.spec.ts`: **19/19 đạt**, Chrome/API mock, 2.3 phút. Kiểm sidebar luôn hiện, vị trí khi cuộn, menu mobile, hồ sơ/phiếu, responsive 390/768/1440 và hồi quy thao tác.
- Đã xem screenshot `staff-workspace-390.png` và `treatment-v25-390.png`; thanh icon hiện, bảng cuộn nội bộ. Không dùng dữ liệu bệnh nhân thật.
- Không thao tác Git hoặc database trong lượt sửa sidebar. Kết quả và giới hạn bên dưới thuộc lượt triển khai khung UI trước đó.

Thay khung Workspace bằng StaffWorkspace/sidebar navy/header trắng theo mẫu được duyệt, giữ route, quyền menu, auth/logout/idle. Trang Bệnh nhân tách filter/table panel, icon, màu trạng thái và tiêu đề; giữ nguyên API, query, handler và pagination. Không backend/migration/Git, không dùng database thật tạo dữ liệu thử. Không thêm KPI hoặc thao tác mới. Focused patient record và CSS in giữ chế độ cũ.

## Bằng chứng

- `npm run build`: đạt. Còn warning SignalR PURE annotation và chunk size hiện có.
- Playwright `staff-workspace-ui.spec.ts`, `patient-treatment-ui.spec.ts`, `booking-settings.spec.ts` với `--grep-invert 'new-visit patient selects'`: **20/20 đạt**, Chrome/API mock, 1.9 phút. Gồm 5 ca shell UI, 14 ca phiếu điều trị và 1 ca booking settings.
- Shell: kiểm permission và direct URL 403, filter query, mobile menu/Escape/điều hướng tài khoản, logout lỗi giữ trang, reduced motion, staff sign-in riêng và customer sign-in không có staff shell. Screenshot desktop/mobile ở 390/768/1440 trong `web/test-results/staff-workspace-*.png`; không tràn ngang toàn trang, bảng cuộn nội bộ.
- Phiếu: hồi quy chuyển thực hiện/sửa/xóa/hủy/retry, ghi chú/vật liệu/finance mock, giữ focused workspace và responsive.

## Giới hạn hồi quy

Trong lượt thăm dò, bài `booking-settings.spec.ts` thứ hai mở customer/book không đăng nhập bị chuyển về đăng nhập, và bài `customer-ui-responsive.spec.ts` tại 320px chờ select bác sĩ thứ ba không còn tồn tại trong luồng hiện tại. Đã dừng lượt thăm dò sau khi xác định fixture không khớp các thay đổi customer có sẵn. Không sửa code/test customer để làm các ca này pass; chưa xác nhận toàn bộ luồng booking customer. Lượt 20/20 thay bằng smoke test customer sign-in tại 390/768/1440 để kiểm CSS không lan. Không tuyên bố toàn bộ E2E dự án đạt.
