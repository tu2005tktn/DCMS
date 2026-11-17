# Giao diện khung nhân viên — đổi kiểu hiển thị (16/11/2026)

Đặc tả này **chỉ đổi giao diện** (màu, khoảng cách, bo góc, đổ bóng) của khung nhân viên (`StaffWorkspace`) và các thành phần chung trong vùng nội dung. **Giữ nguyên mọi thành phần gốc**: danh sách mục menu, điều kiện quyền, đường dẫn, nút "Đăng xuất", liên kết "Tài khoản của tôi", thu gọn sidebar, ngăn kéo menu trên điện thoại, liên kết "Đến nội dung chính", cảnh báo hết phiên, trang chủ `/`. Không đổi API, quyền, dữ liệu hay luồng nghiệp vụ.

## 1. Nguồn tham khảo

Chỉ lấy cảm hứng bố cục và màu sắc, không sao chép mã (cả hai repo dùng license MIT):

| Nguồn | Lấy gì |
| --- | --- |
| [condorheroblog/react-antd-admin](https://github.com/condorheroblog/react-antd-admin) | Sidebar nền trắng, mục đang chọn nền xanh nhạt; vùng nội dung nền xám nhạt với card trắng |
| [design-sparx/antd-multipurpose-dashboard](https://github.com/design-sparx/antd-multipurpose-dashboard) | Header trắng thoáng; card bo góc, đổ bóng nhẹ; tiêu đề trang đậm |

DCMS giữ antd 5, không thêm thư viện.

## 2. Thay đổi giao diện

### 2.1 Sidebar

- Nền trắng `#ffffff`, viền phải `1px #e4eaf2` (trước: nền xanh đậm `#102a43`).
- Logo và chữ "DCMS" màu `#175cd3`; dòng "Quản lý phòng khám" màu `#667085`.
- Tiêu đề nhóm (Vận hành, Chuyên môn, Quản trị): chữ 11px, đậm 600, in hoa, giãn chữ, màu `#98a2b3`.
- Mục thường: chữ `#344054`, icon `#667085`; di chuột: nền `#f2f4f7`.
- Mục đang chọn: nền `#e8f1ff`, chữ và icon `#175cd3`, đậm 600.
- Nút thu gọn và liên kết "Tài khoản của tôi" theo cùng bảng màu; đường kẻ trên liên kết tài khoản `#eaecf0`.
- Ngăn kéo menu trên điện thoại dùng cùng nền trắng.
- Kích thước, điểm ngắt (1100px, 991px, 600px) và hành vi thu gọn giữ nguyên.

### 2.2 Header

- Nền trắng, viền dưới `1px #e4eaf2`, đổ bóng rất nhẹ; cao tối thiểu 64px (trước 76px).
- Avatar nền `#e8f1ff`, chữ `#175cd3`. Nút "Đăng xuất" giữ nguyên vị trí, đổi sang kiểu viền mảnh, bo 8px.

### 2.3 Vùng nội dung

- Nền `#f5f7fa`; lề nội dung 20px trên màn rộng (giữ 12px dưới 1100px, 14px dưới 600px như cũ).
- Card: bo góc 10px, viền `#eaecf0`, đổ bóng `0 1px 2px rgba(16,24,40,.06)`.
- Tiêu đề trang (`h2` trong `.staff-content`): 22px, đậm 700, màu `#101828`.
- **Không đổi** [kiểu bảng dùng chung](TABLE-STYLE.md).

### 2.4 In

Quy tắc in giữ nguyên: ẩn sidebar, header, liên kết bỏ qua.

## 3. Đề xuất chưa làm (cần người dùng đồng ý riêng)

Các mục sau thêm chức năng mới nên **không** nằm trong lần đổi giao diện này:

- Thanh tab nhiều trang dưới header.
- Ô tìm trang trong header (`Ctrl+K`).
- Trang Tổng quan cho nhân viên (thẻ số liệu và lịch hẹn hôm nay, theo kiểu Healthcare Dashboard).
- Chuẩn hóa màn danh sách bằng ProTable.

## 4. Kiểm tra chấp nhận

1. Mọi mục menu, quyền, đường dẫn, nút Đăng xuất, "Tài khoản của tôi", thu gọn sidebar và ngăn kéo menu hoạt động như trước.
2. Sidebar nền trắng, mục đang chọn nền xanh nhạt; chữ đủ tương phản (tối thiểu 4.5:1).
3. Ở 1440px, 1000px, 390px: không tràn ngang, sidebar thu icon dưới 992px như cũ.
4. Bảng giữ kiểu theo TABLE-STYLE.
5. `npm run build` thành công.
