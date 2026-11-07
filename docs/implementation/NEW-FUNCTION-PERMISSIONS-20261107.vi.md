# Bổ sung chức năng vào phân quyền — 07/11/2026

Đặc tả: [phân quyền toàn màn hình](../specs/ALL-SCREENS-DYNAMIC-PERMISSIONS.vi.md#bổ-sung-chức-năng-ngày-07112026).

## Thay đổi

- Thêm 10 grant vào metadata server của cây checkbox trong Thiết lập hệ thống → Phân quyền: doanh thu, thống kê công việc/dịch vụ, xem/thêm/sửa/xóa lần nhập hàng, tồn kho, lịch sử xuất dùng, tạo QR PayOS và quản lý phiếu labo.
- API và dịch vụ kiểm quyền riêng cho các chức năng trên. Web áp dụng cùng quyền cho menu, route, tab kho và nút ghi; giữ cơ chế cha/con, giới hạn vai trò, preview/version/audit và thu hồi quyền hiện có.
- Nhóm hạn chế đang gán phải được cấp rõ ràng quyền mới. Không sửa nhóm, role hay assignment của nhân viên trong database đang dùng; không cần migration. Nhân viên chưa gán nhóm giữ quyền theo vai trò hiện hành, gồm việc Admin chưa gán nhóm không tự nhận quyền báo cáo tài chính/kho.
- Thống kê công việc lọc theo chi nhánh được cấp khi không truyền bộ lọc; chi nhánh ngoài phạm vi trả 403.
- Form sửa vật liệu trên hoạt động điều trị dùng API tối thiểu theo dossier/activity, không cần quyền xem toàn bộ lịch sử xuất dùng. Picker tồn khả dụng giữ quyền đọc danh mục. Tạo QR tách khỏi thu tiền mặt; đọc lệnh, webhook và đối soát giữ contract cũ. Inventory ghi baseline webhook và dịch vụ tiếp tục xác minh HMAC.

## Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| .NET 10 restore/build solution | Qua, 0 lỗi |
| Unit tests | 87/87 qua |
| API integration/PostgreSQL | 88/88 qua trên schema `dcms_test_*` cách ly |
| Browser API mock | 79 ca riêng biệt qua: 45 ca phân quyền + 34 ca hồi quy nhập hàng, thống kê, điều trị/thanh toán/labo |
| Web npm install/build | Qua |
| PostgreSQL `pg_isready` | Nhận kết nối |
| HTTP `/health` | `api=healthy`, `database=healthy` |

API gồm kiểm inventory endpoint/role-cha, metadata grant-options, quyền đọc riêng, chặn ghi, cấp/thu hồi cùng token, giữ phạm vi chi nhánh, hồi quy nhập hàng/tồn kho và labo. Các ca thử cấp quyền ghi mới dùng input synthetic không hợp lệ hoặc ID không tồn tại để dừng trước thao tác với nhà cung cấp.

Lượt UI đầu có 78/79 qua, một ca mở trực tiếp `/materials` không thấy nội dung trong thời gian chờ; chạy riêng qua, sau đó chạy lại cả 45 ca phân quyền qua đủ. Các ca UI dùng mock API; không coi đó là kiểm chứng browser với API thật. 34 ca hồi quy còn lại đã qua trong lượt đầu.

Không gửi giao dịch PayOS thật. Khóa PayOS được loại khỏi môi trường tiến trình kiểm thử; không sửa `.env`. API phục vụ health chỉ chạy tạm và đã dừng. Bằng chứng TRX ở `.artifacts/permissions-20261107/api-final.trx` và `unit.trx`.

Build còn cảnh báo dependency `System.Security.Cryptography.Pkcs` 9.0.0 (NU1902/NU1903), hai tham số không dùng và kích thước bundle/annotation SignalR. Không thay package trong phạm vi này. Không thực hiện thao tác Git.
