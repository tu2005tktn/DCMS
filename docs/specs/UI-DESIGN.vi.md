# Đặc tả thiết kế UI DCMS (web)

Phạm vi: giao diện web React/Ant Design hiện tại (`web/`) — trang đăng nhập (`/sign-in`), khôi phục tài khoản (`/recover`), trạng thái hệ thống (`/health`) và workspace sau đăng nhập (`/`, `/accounts`). Tài liệu này mô tả ngôn ngữ thiết kế và bố cục mục tiêu; việc thay đổi chỉ ở tầng trình bày, không thay đổi hành vi, route, phân quyền, API contract và nội dung thông báo MSG.

## Mục tiêu

- Thay giao diện mặc định thô (header trắng + menu ngang, card bo nhỏ, trang đăng nhập trống trơn) bằng bố cục dashboard chuyên nghiệp: sidebar điều hướng, header có danh tính người dùng, không gian nội dung có nhịp.
- Duy trì toàn bộ hành vi nghiệp vụ: state machine khôi phục 4 bước, form rules, thông báo lỗi/thành công, bảng dữ liệu động, audit drawer.
- Nhất quán một design system duy nhất: token màu, typography, bán kính, bóng đổ, khoảng cách dùng chung cho cả 4 trang.

## Design tokens

| Nhóm | Token | Giá trị | Ghi chú |
| --- | --- | --- | --- |
| Màu chủ đạo | `--dcms-primary` | `#1677ff` → gradient `#2563eb → #1e40af` | Đồng bộ `colorPrimary` AntD; nút chính dùng gradient |
| Màu nền | `--dcms-bg` | `#f4f6fa` | Nền nội dung workspace |
| Màu thương hiệu | `--dcms-navy` | `#0c1e3a` / gradient `#0b1b33 → #12335e` | Sidebar, panel thương hiệu trang auth |
| Màu chữ | chính `#17233d`, phụ `#5a6b85`, mờ `#8494ad` | | Tương phản đạt AA trên nền sáng |
| Viền | `#e4e9f2` | | Card, input, ngăn cách bảng |
| Thành công | `#16a34a` | trạng thái Hoạt động |
| Cảnh báo/khoá | `#dc2626` | trạng thái Đã khóa |
| Bán kính | `--dcms-radius` | 12px card, 10px control, 999px tag/pill | Bo góc lớn hơn mặc định |
| Bóng đổ | card `0 1px 2px rgba(12,30,58,.04), 0 12px 32px -12px rgba(12,30,58,.12)` | đổ xuống, không viền đậm |
| Typography | Inter (Google Fonts), cỡ nội dung 14px, tiêu đề trang 22px/700 | nạp qua `index.html` |

## Bố cục

### 1. Trang xác thực (sign-in, recover)

- Nền gradient nhẹ + họa tiết chấm mờ; thẻ nội dung dạng 2 cột: **panel thương hiệu** (trái, nền navy gradient, logo, tên hệ thống, 3 gói tính năng) và **khung form** (phải, nền trắng, bo 16px).
- Màn hình < 960px: panel thương hiệu ẩn, chỉ giữ khung form.
- Tiến trình khôi phục (4 bước) giữ nguyên cấu trúc `ol.recovery-step` + `aria-*`, chỉ đổi style thanh tiến trình (tròn hơn, có nhãn bước hiện tại).
- Luồng và thông báo trung lập (MSG12, MSG13, trạng thái cooldown) không đổi.

### 2. Workspace (đã đăng nhập)

- `Layout.Sider` 248px, cố định bên trái: logo DCMS + chữ "Phòng khám nha khoa", menu dọc (icon + nhãn) theo quyền `accounts.manage`, chân sider là thẻ người dùng (avatar viết tắt, họ tên, vai trò).
- `Layout.Header` 64px nền trắng: tiêu đề trang hiện tại, nút "Cập nhật quyền" khi ở trang chủ (chuyển vào menu người dùng), chuông thông báo (giao diện), menu dropdown người dùng (thông tin, đăng xuất).
- `Layout.Content`: nền xám nhạt, `page-header` gồm tiêu đề + mô tả + hành động chính; nội dung giới hạn chiều rộng 1200px, canh giữa.
- Responsive: < 992px sidebar thu thành thanh icon; < 768px sidebar ẩn, mở bằng nút menu ở header.

### 3. Trạng thái hệ thống (/health)

- Dùng chung khung auth-shell: panel thương hiệu + thẻ trạng thái. API và PostgreSQL hiển thị bằng `Tag` có chấm màu, có hành động kiểm tra lại.

## Quy ước thành phần

- **Card**: nền trắng, bo 12px, bóng mềm, không viền đậm; `head` phân cách bằng đường `#eef1f6`.
- **Button**: cao 36–40px, bo 10px; `primary` gradient, `hover` sáng lên; nhấn `danger` chỉ khi hành động phá hủy (khóa/mở khóa).
- **Table**: header chữ hoa nhẹ/màu phụ, dòng `hover` nền `#f7f9fc`, cột trạng thái dùng badge có chấm; giữ `scroll={{ x }}` và pagination.
- **Avatar/danh tính**: avatar viết tắt tên, nền gradient chủ đạo.
- **Tag vai trò**: pill xanh nhạt viền xanh.
- **Empty/lỗi**: giữ nguyên thông điệp tiếng Việt hiện có; chỉ đổi khung hiển thị.
- **Chuyển động**: fade-in 200–280ms cho đổi bước form; tôn trọng `prefers-reduced-motion`.

## Truy cập (a11y) và phi địa phương hoá

- Giữ mọi `aria-label`/`role` hiện có (tiến trình khôi phục, nút đóng).
- Đảm bảo tương phản chữ/nền ≥ 4.5:1; trạng thái không chỉ truyền tải bằng màu (kèm chữ "Hoạt động"/"Đã khóa").
- Toàn bộ nhãn giao diện giữ tiếng việt như hiện tại; không sinh chuỗi mới ngoài nhãn điều hướng/mô tả trang.

## Kiểm chứng

- `npm run build` trong `web/` (tsc + vite) phải pass.
- Kiểm tra tay: đăng nhập → sidebar + header hiển thị đúng vai trò; trang `/accounts` với quyền, chặn 403 không quyền; luồng recover 4 bước nguyên vẹn; `/health` đọc đúng API/database; coi màn hình 375px không vỡ bố cục.
