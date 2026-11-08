# Kiểu bảng dùng chung (08/11/2026)

Mọi bảng trong khu vực nhân viên (`.staff-content`) dùng chung một kiểu để dễ đọc:

- Viền ngoài đậm (1px, xám đậm `#98a2b3`), bo góc 8px; đường kẻ ngang giữa các hàng và đường kẻ dọc giữa các cột cùng dày 2px, cùng màu xám đậm `#98a2b3`.
- Hàng tiêu đề có nền xanh xám đậm `#d3deec`, đường kẻ dưới dày 2px, vạch phân cách mảnh mặc định của Ant Design được ẩn.
- Tiêu đề cột chữ đậm (700), màu tối `#1d2939`.
- Chữ trong các ô đậm vừa (500), màu tối `#1d2939`.
- Khi bảng chuyển sang dạng thẻ trên màn hẹp, giữ nguyên dạng thẻ của từng màn (viền thẻ riêng); kiểu này không đổi dữ liệu hay chức năng.

Quy tắc nằm ở `web/src/app/staffWorkspace.css` (cuối file), thay cho các viền riêng lẻ trước đó khi hai quy tắc trùng.
