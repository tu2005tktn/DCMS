# Chạy thử S03 cho tài khoản khách hàng (Web và Mobile)

Phạm vi: quên mật khẩu bằng **email đã đăng ký** cho tài khoản Public. Không có bước xác minh email riêng hoặc SMS. Màn hình và API khôi phục của nhân viên giữ nguyên.

## Cấu hình gửi mail Development

1. Tạo `.env` từ `.env.example` nếu chưa có. Không đưa `.env`, mã xác minh hoặc mật khẩu vào Git.
2. Điền `SMTP_HOST=127.0.0.1`, `SMTP_PORT=1025`, `SMTP_ENABLE_SSL=false`, `SMTP_FROM_ADDRESS=no-reply@dcms.local` trong `.env`. Để trống username/password cho Mailpit cục bộ.
3. Từ thư mục gốc chạy `docker compose --env-file .env -f infrastructure/docker/compose.yaml --profile mail up -d mailpit`. Hộp thư xem tại `http://localhost:8025`.
4. Kiểm tra đúng connection string trước khi áp dụng các migration còn chờ bằng `infrastructure/scripts/start-api.ps1 -Migrate`. Chạy lại API bằng `infrastructure/scripts/start-api.ps1`. Nếu PowerShell chặn script, dùng `powershell -ExecutionPolicy Bypass -File .\infrastructure\scripts\start-api.ps1` trong phiên thử nghiệm. Chính sách Public mới không cần migration hay sửa hàng dữ liệu cũ.
5. Chạy Web ở `web` bằng `npm.cmd run dev`, hoặc Mobile theo hướng dẫn hiện tại của dự án. Với Mobile trên thiết bị khác máy chủ, cấu hình API base URL trỏ về IP truy cập được của máy chạy API; hộp thư Mailpit vẫn mở trên máy phát triển.

Nếu chưa cấu hình SMTP, yêu cầu **khôi phục** vẫn trả MSG12 trung lập cho email đúng định dạng, kể cả khi không có tài khoản hoặc provider lỗi; không lấy MSG12 làm bằng chứng mail đã đến. Endpoint đọc mã development cũ chỉ dành cho Admin, không phải đường đi của khách hàng.

## Kiểm tra luồng

1. Đăng ký tài khoản Public với họ tên, số điện thoại và email chưa dùng. Email được lưu làm địa chỉ nhận mã khôi phục; không cần vào màn hình xác minh riêng.
2. Ở `/customer/sign-in` bấm **Quên mật khẩu**. Mobile có lối vào tương ứng ở màn hình đăng nhập. Nhập email đã đăng ký; kiểm tra hộp thư Mailpit, nhập mã và mật khẩu mới theo `/api/auth/policy`.
3. Thành công trở về đăng nhập. Mật khẩu cũ, access token và refresh token cũ phải bị từ chối; mật khẩu mới đăng nhập được. Khóa tài khoản vẫn cần Admin mở khóa. Không có Patient/link nào được tạo hay xác minh.
4. Thử email đã đăng ký nhưng `EmailConfirmed=false` và email không tồn tại: cả hai đều nhận MSG12 như nhau, nhưng chỉ địa chỉ đã đăng ký nhận mã. Thử mã sai/hết hạn/đã dùng và gửi lại quá sớm: server từ chối hoặc áp dụng giới hạn dù UI còn hiển thị mã cũ.

Route khách hàng: `POST /api/customer/recovery/{request,resend,verify,reset}` (không cần đăng nhập). Các endpoint xác minh email đã triển khai trước đó không còn là điều kiện cho luồng này. Cấu hình giới hạn nằm trong `Recovery` của API; giá trị hiện tại là [PROPOSED] cho môi trường Development.

Kiểm thử tự động dùng PostgreSQL tạm cách ly. Không chạy kiểm thử tích hợp trên database dự án đang dùng và không seed tài khoản thử vào đó.
