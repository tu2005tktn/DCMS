# Đăng nhập bệnh nhân bằng email hoặc tên đăng nhập — 09/11/2026

## Hành vi và giao diện

- Cổng bệnh nhân `/customer/sign-in` có trường **Email hoặc tên đăng nhập**, nhận email đã lưu hoặc username của tài khoản Public, kể cả username là số điện thoại khi đăng ký. Không giới hạn 10 chữ số, không bắt định dạng điện thoại/email vì người dùng có thể nhập username.
- Trường định danh đăng nhập chỉ bắt buộc không trắng, trim trước khi tra cứu. Không đặt min/max length hay cắt chuỗi trên form login hoặc giới hạn 64 ký tự tại API. Form nhân viên cũng bỏ maxlength 64 của username. Quy tắc độ dài/định dạng khi đăng ký và quản trị tài khoản không đổi; mật khẩu vẫn giữ kiểm tra hiện hành.
- Giữ `autocomplete=username`, bàn phím text cho phép gõ email/tên. Lỗi nhập trống có tổng hợp lỗi tại cổng bệnh nhân; lỗi xác thực giữ dữ liệu định danh để thử lại. Cổng bệnh nhân tiếp tục từ chối lưu phiên của tài khoản Staff.

## API và dữ liệu

- `POST /api/auth/sign-in` giữ JSON `{ userName, password, deviceLabel? }`; `userName` là định danh nhập vào để tương thích client cũ. Response/session/token/audit hiện có không thay đổi, không migration.
- Trước tiên tra username bằng chuẩn hóa Identity (không phân biệt hoa/thường). Nếu không có username tương ứng, tra email chuẩn hóa Identity trên tài khoản `Public`. Giữ EmailIndex unique hiện có; chỉ chấp nhận đúng một tài khoản khớp, không lấy tùy tiện tài khoản đầu tiên nếu dữ liệu nhập cũ vi phạm tính duy nhất. Email của Staff không tự thành phương thức đăng nhập mới. Tài khoản Google chưa có mật khẩu vẫn phải dùng luồng Google.
- Sau khi tìm tài khoản, giữ kiểm tra mật khẩu, IsActive, lockout và tạo phiên/audit hiện có. Email chưa khớp, username không tồn tại, sai mật khẩu, tài khoản khóa/không hoạt động hoặc email mơ hồ đều không tạo phiên và trả thông báo xác thực chung hiện hành (`401 MSG09`). Không suy liên kết Patient từ email hay cấp thêm quyền hồ sơ.
- Không log mật khẩu/định danh request. Không tự tạo tài khoản, không dùng số điện thoại làm bằng chứng liên kết hồ sơ.

## Kiểm thử

API/PostgreSQL schema riêng: username/email/hoa thường/khoảng trắng, username và email trên 64 ký tự, số điện thoại làm username; sai mật khẩu/unknown/blank/khóa/inactive/tài khoản chưa có mật khẩu không tạo phiên; staff vẫn đăng nhập username và không được mở rộng email fallback.

Browser: gửi được email, username dài và số điện thoại nguyên vẹn; login input không maxlength hoặc regex điện thoại; trường trống bị chặn; API lỗi giữ định danh; cổng bệnh nhân từ chối Staff; form Staff không cắt username dài; build và kiểm thử portal/session hiện có.
