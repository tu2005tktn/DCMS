# Staff Management Workspace — UI được duyệt

Ngày 31/10/2026. Người dùng duyệt mẫu navy sidebar + header trắng + bảng Bệnh nhân. Chỉ sửa Web/tài liệu UI, không backend/API/payload/route/permission hoặc nghiệp vụ. Không thêm dashboard KPI, thông báo, tìm kiếm toàn cục hoặc selector chi nhánh chưa có nguồn.

Menu nhân viên chuyển từ ngang sang dọc, nhóm Vận hành/Chuyên môn/Quản trị; giữ nguyên từng điều kiện role/permission hiện có, không suy quyền từ nhóm menu. Giữ Tài khoản của tôi, đăng xuất, cảnh báo idle và tất cả đường dẫn hiện có. Header có breadcrumb hiện tại, initials/tên/vai trò từ auth hiện có. Màn hình nhỏ dùng drawer menu có nhãn, đóng bằng Escape/điều hướng; focus bàn phím rõ. Trang home vẫn là tài khoản, không đổi trang đích đăng nhập.

Bệnh nhân giữ nguyên filter/tìm kiếm/thêm/mở hồ sơ/phân trang và API; chỉ sắp lại thành heading + filter panel + table panel, nút/icon, typography, status có chữ. Không dữ liệu giả hoặc nhãn bản tham khảo trong giao diện thật. CSS staff riêng không tác động customer/sign-in/recovery. Hồ sơ bệnh nhân đang dùng focused shell giữ chế độ toàn màn hình hiện có, in không có sidebar/header. Responsive 390/768/1440, reduced motion, bàn phím.

Kiểm thử bằng API mock cho quyền/menu, deep link, mobile drawer, filter giữ query/paging, 403 URL trực tiếp, idle, lỗi logout; hồi quy patient treatment, booking settings và customer. Web build; không tạo dữ liệu trong database thật, không Git khi chưa được duyệt từng lệnh.

## Điều chỉnh theo người dùng: sidebar luôn hiện

Sidebar cố định, hiện cả trong hồ sơ bệnh nhân/phiếu điều trị và khi cuộn. Bỏ chế độ ẩn header/sidebar của focused record trên màn hình; giữ chế độ ẩn chrome khi in. Dưới 992px sidebar thu thành rail icon 64px với link có tên truy cập/tooltip, nội dung chừa 64px; nút mở drawer tên đầy đủ vẫn hoạt động. Không đổi quyền, route hoặc logic. Kiểm sidebar và khoảng chừa nội dung tại 390/768/1440, hồ sơ bệnh nhân, cuộn và in.

## Điều chỉnh theo người dùng: nút thu gọn sidebar

Từ 992px trở lên, sidebar có nút bật/tắt thu gọn ngay trong thanh (hàng dưới logo, có tên truy cập "Thu gọn menu"/"Mở rộng menu"; chỉ hiện icon, không có chữ). Icon các mục trong rail phải nằm giữa cột 64px. Thu gọn đưa sidebar về rail icon 64px như chế độ dưới 992px (ẩn tên mục, tiêu đề nhóm, chữ thương hiệu; link vẫn có `title`/`aria-label`), nội dung chừa 64px; mở rộng trả về 258px. Trạng thái nhớ trong `localStorage` (khóa `dcms.staff.sidebar.collapsed`), đọc/ghi trong try/catch và mặc định mở rộng nếu không đọc được. Dưới 992px giữ nguyên rail + drawer hiện có, nút này ẩn. Không đổi quyền, route, logic; không ảnh hưởng in (sidebar vẫn ẩn khi in).

## Vùng nội dung dàn kín (14/11/2026)

Mọi trang trong khu nhân viên dùng toàn bộ bề rộng vùng nội dung: bỏ giới hạn 1680px, lề ngoài 12px, tiêu đề trang 22px và khoảng cách dưới tiêu đề thu gọn. Không đổi dữ liệu, quyền hay luồng thao tác.

Trang Kế hoạch điều trị (danh sách mẫu): khoảng cách giữa các khối 12px, thẻ và ô bảng gọn hơn, nút trong bảng cao 32px để bảng hiển thị được nhiều dòng hơn.

Trang Kế hoạch điều trị: bấm "Mở" trên một dòng sẽ mở chi tiết kế hoạch trong hộp thoại giữa màn hình (rộng 1180px, cuộn trong hộp thoại) thay vì hiện thành khối phía dưới danh sách. Trong hộp thoại có Sửa tên, Xóa kế hoạch, sửa/xóa hạng mục và "+ Thêm hạng mục"; đóng hộp thoại hỏi xác nhận nếu còn thay đổi chưa lưu. Hộp thoại tạo/sửa tên mở chồng lên trên hộp thoại chi tiết.

Bảng trong khu nhân viên (màn hình từ 1001px): đường kẻ dọc giữa các cột dùng màu `#98a2b3`, 1px, để phân tách cột rõ ràng; dạng thẻ trên màn hình hẹp không đổi.
