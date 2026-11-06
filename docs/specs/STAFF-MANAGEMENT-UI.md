# Staff Management Workspace — UI được duyệt

Ngày 31/10/2026. Người dùng duyệt mẫu navy sidebar + header trắng + bảng Bệnh nhân. Chỉ sửa Web/tài liệu UI, không backend/API/payload/route/permission hoặc nghiệp vụ. Không thêm dashboard KPI, thông báo, tìm kiếm toàn cục hoặc selector chi nhánh chưa có nguồn.

Menu nhân viên chuyển từ ngang sang dọc, nhóm Vận hành/Chuyên môn/Quản trị; giữ nguyên từng điều kiện role/permission hiện có, không suy quyền từ nhóm menu. Giữ Tài khoản của tôi, đăng xuất, cảnh báo idle và tất cả đường dẫn hiện có. Header có breadcrumb hiện tại, initials/tên/vai trò từ auth hiện có. Màn hình nhỏ dùng drawer menu có nhãn, đóng bằng Escape/điều hướng; focus bàn phím rõ. Trang home vẫn là tài khoản, không đổi trang đích đăng nhập.

Bệnh nhân giữ nguyên filter/tìm kiếm/thêm/mở hồ sơ/phân trang và API; chỉ sắp lại thành heading + filter panel + table panel, nút/icon, typography, status có chữ. Không dữ liệu giả hoặc nhãn bản tham khảo trong giao diện thật. CSS staff riêng không tác động customer/sign-in/recovery. Hồ sơ bệnh nhân đang dùng focused shell giữ chế độ toàn màn hình hiện có, in không có sidebar/header. Responsive 390/768/1440, reduced motion, bàn phím.

Kiểm thử bằng API mock cho quyền/menu, deep link, mobile drawer, filter giữ query/paging, 403 URL trực tiếp, idle, lỗi logout; hồi quy patient treatment, booking settings và customer. Web build; không tạo dữ liệu trong database thật, không Git khi chưa được duyệt từng lệnh.

## Điều chỉnh theo người dùng: sidebar luôn hiện

Sidebar cố định, hiện cả trong hồ sơ bệnh nhân/phiếu điều trị và khi cuộn. Bỏ chế độ ẩn header/sidebar của focused record trên màn hình; giữ chế độ ẩn chrome khi in. Dưới 992px sidebar thu thành rail icon 64px với link có tên truy cập/tooltip, nội dung chừa 64px; nút mở drawer tên đầy đủ vẫn hoạt động. Không đổi quyền, route hoặc logic. Kiểm sidebar và khoảng chừa nội dung tại 390/768/1440, hồ sơ bệnh nhân, cuộn và in.
