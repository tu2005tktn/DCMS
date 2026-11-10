# Đăng nhập bằng email hoặc tên đăng nhập — 09/11/2026

Đặc tả được cập nhật trước implementation tại `docs/specs/PATIENT-LOGIN-IDENTIFIER.md`; UC01-UC31 dẫn tới contract này.

- Cổng bệnh nhân dùng nhãn **Email hoặc tên đăng nhập**, bỏ regex điện thoại, inputmode numeric và maxlength 10. Chỉ bắt buộc định danh không trắng. Username dạng số điện thoại vẫn dùng được.
- API sign-in giữ trường `userName`; tra username Identity trước, rồi email chuẩn hóa cho tài khoản Public. Không còn giới hạn 64 ký tự ở login API. Giữ EmailIndex unique, mật khẩu, IsActive, lockout, session/token/audit và phạm vi hồ sơ.
- Form nhân viên bỏ maxlength 64. Không mở thêm email fallback cho Staff. Không thay đổi đăng ký/quản trị tài khoản hoặc thêm migration.
- Sửa các locator login trong test portal/appointments/family-record. Fixture responsive cũ chọn một dropdown dịch vụ đã được thay bằng dịch vụ Khám cố định; đã sửa fixture để chọn đúng hai dropdown chi nhánh/nha sĩ hiện hành.

Xác minh:

- .NET restore/build thành công, 0 lỗi; vẫn có warning Pkcs vốn có.
- 30 HTTP/PostgreSQL tests qua: LoginIdentifierApiTests, RegistrationApiTests và các ca AccountApiTests về đăng nhập, khóa/thu hồi phiên, token/phiên hết hạn. Bao gồm case insensitive/trim, username 129 ký tự, email trên 64 ký tự, số điện thoại; sai mật khẩu/không tồn tại/trắng/khóa/inactive/tài khoản không có mật khẩu không tạo phiên; email Staff không trở thành fallback.
- npm install và web build thành công; còn warning bundle vốn có.
- 27 browser mock tests qua: login-identifier, customer-portal, customer-ui-responsive, session-renewal. Kiểm input không cắt chuỗi, gửi đúng định danh, lỗi/trống/thử lại, từ chối Staff tại cổng bệnh nhân, Google, refresh/session và reflow 320/390/768/1440px.
- `git diff --check` qua; main API/database 5080 healthy và `/customer/sign-in` ở web 5173 trả HTTP 200 sau khởi động lại. API tests dùng schema riêng, browser tests dùng dữ liệu tổng hợp/mock; không thay hồ sơ bệnh nhân thật.

Log/TRX nằm ở `.artifacts/runtime-20261109/login-identifier-*`. Không lưu thông tin đăng nhập thật vào tài liệu hoặc Git.
