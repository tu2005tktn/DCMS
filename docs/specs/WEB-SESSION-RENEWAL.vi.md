# Gia hạn phiên nhân viên trên web

Ngày 28/10/2026. Sửa lỗi web chỉ giữ access token 15 phút và bỏ refresh token trong phản hồi đăng nhập, làm phiên bị kết thúc dù nhân viên đang thao tác.

- Đăng nhập nhân viên giữ access token và refresh token trong sessionStorage của tab; không ghi token vào log/file, không dùng localStorage. F5 giữ phiên, đóng tab xóa dữ liệu phía client như hiện tại.
- Trước request có xác thực, nếu access token còn dưới 60 giây thì gọi POST /api/auth/refresh với refresh token hiện tại (anonymous endpoint). Lưu cặp token mới sau rotation, rồi mới gửi request ban đầu. Các request song song chờ cùng một lần refresh, không gửi cùng refresh token nhiều lần.
- Poll /me hiện có 30 giây đi qua cơ chế này, giúp tab đang chạy được gia hạn. Không tự phát lại command ghi dữ liệu khi nhận 401, không tự đăng nhập lại bằng mật khẩu.
- Khi refresh hoặc request bị từ chối 401, kết thúc đúng phiên đang dùng; phản hồi cũ không được xóa/ghi đè phiên mới sau đổi tài khoản hoặc đăng xuất. Lỗi mạng/5xx báo kết nối, không xóa phiên. Token cũ không có refresh token cần đăng nhập lại một lần để nhận đủ cặp token.
- Giữ nguyên API/DTO/backend validation, JWT lifetime, rotation/reuse detection, thu hồi tài khoản/quyền. Backend hiện từ chối refresh nếu AuthSession đã hết hạn: tab bị treo/ngủ quá thời hạn vẫn phải đăng nhập lại. Đây không phải triển khai idle timeout S58; các giá trị S58 tự đăng xuất vẫn là cấu hình chưa có consumer idle riêng.
- Phạm vi đợt này là web nhân viên; không tuyên bố mobile/cổng khách hàng đã có gia hạn tương đương.

Kiểm tra: login/F5 giữ token pair, access gần hết hạn đổi token rồi /me thành công, concurrent request một refresh, mạng lỗi giữ phiên, refresh 401 kết thúc phiên, kết quả refresh/401 cũ không ghi đè phiên mới. Kiểm tra browser bằng API thật và build web.

## Kết quả triển khai

Web nhân viên đã lưu cặp token khi đăng nhập và gia hạn qua shared api client trước request. 5/5 browser regression (mock endpoint để kiểm race/error) đạt: concurrent rotation, F5, 503 giữ phiên, 401 xóa phiên và late refresh 200/401 không đổi phiên mới hoặc gửi command cũ. Browser/API thật đạt: UI login → giả lập đồng hồ trình duyệt gần access expiry → ba request /me đồng thời chỉ một refresh HTTP 200, refresh token đã rotate → /me thành công → F5 giữ đăng nhập → sign-out xóa cặp token. Chỉ session kiểm chứng riêng bị đăng xuất. Web production build đạt; không thay backend/schema.
