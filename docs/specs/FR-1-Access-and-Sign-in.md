# III. Functional Requirements

## 1. Access, Sign-in and Recovery

Wireframes: https://www.figma.com/design/pqJcfUUOdMXZvXmx0gnxHi (frames "1.1" to "1.5").

### 1.1 Access Portal

This screen (`/access`) is the entry point of the web application. It allows the user to:

- Choose between the customer area and the staff area.
- Go to account creation for customers.

| Field Name | Description |
|---|---|
| **Portal options** | |
| Option "Khách hàng" | "Đặt lịch khám và xem lịch hẹn của bạn". Opens Customer Sign-in. |
| Option "Nhân viên" | "Vào khu vực làm việc của phòng khám". Opens Staff Sign-in. |
| **Account** | |
| Link "Tạo tài khoản khách hàng" | Opens Customer Register. |

### 1.2 Staff Sign-in

This screen (`/sign-in`) allows a staff member to:

- Sign in with a user name and password to open the staff workspace (a user who already has a session is sent to the workspace directly).
- Recover a forgotten account.
- Return to the Access Portal to choose another portal.

| Field Name | Description |
|---|---|
| **Header** | |
| Title "Đăng nhập" | Read-only. Followed by the text "Truy cập các chức năng theo vai trò được cấp cho tài khoản của bạn." |
| Link "Chọn cổng đăng nhập" | Returns to the Access Portal. |
| **Sign-in form** | |
| Alert | Read-only. Shows the sign-in error or the message that the session has expired. |
| Tên đăng nhập | Required. Message "Vui lòng nhập Tên đăng nhập." when empty. |
| Mật khẩu | Required, up to 256 characters, hidden. Message "Vui lòng nhập Mật khẩu." when empty. |
| Button "Đăng nhập" | Signs in and opens the workspace. |
| **Recovery** | |
| Link "Khôi phục tài khoản" | Opens Recover Account. Shown after "Quên mật khẩu?". |

### 1.3 Customer Sign-in

This screen (`/customer/sign-in`, title "Đăng nhập", "Tiếp tục đặt lịch và theo dõi các cuộc hẹn của bạn.") allows a customer to:

- Sign in with an e-mail or user name and a password.
- Sign in with a Google account.
- Go to account creation or to account recovery.

A customer who is already signed in is sent to the customer home.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert (error) | Read-only. The sign-in error, or the message of the session (for example when it has expired). |
| Error summary | Read-only. Lists the fields that are wrong after the button "Đăng nhập" is clicked; the focus moves to it. |
| **Sign-in form** | |
| Email hoặc tên đăng nhập | Required. Placeholder "Nhập email hoặc tên đăng nhập". Message "Vui lòng nhập email hoặc tên đăng nhập." |
| Mật khẩu | Required, hidden, with a button to show it. Message "Vui lòng nhập mật khẩu." |
| Link "Quên mật khẩu?" | Opens Recover Account (1.5). |
| Button "Đăng nhập" | Signs in and opens the customer home. |
| **Other options** | |
| Link "Tạo tài khoản" | Opens Customer Register (1.4). |
| Text "Hoặc đăng nhập bằng Google" | A separator. |
| Google button "Đăng nhập bằng Google" | Signs in with the Google account and opens the customer home. Messages: "Đăng nhập Google chưa được cấu hình trên máy chủ.", "Không tải được Google. Vui lòng kiểm tra kết nối.", "Google không trả về thông tin đăng nhập." |
| Link "Chọn lại cổng đăng nhập" | At the bottom. Opens the Access Portal (1.1). |

### 1.4 Customer Register

This screen (`/customer/register`, title "Tạo tài khoản", "Đăng ký bằng số di động Việt Nam để quản lý lịch hẹn thuận tiện hơn.") allows a new customer to create an account with a Vietnamese mobile number. No confirmation code is needed. After success the user is sent to Customer Sign-in.

| Field Name | Description |
|---|---|
| **Notices** | |
| Alert (error) | Read-only. The error message. For the duplicate phone number or e-mail (MSG139): "Số điện thoại hoặc email có thể đã được sử dụng. Hãy đăng nhập hoặc khôi phục tài khoản." with the links to Customer Sign-in and Recover Account. |
| Alert (success) | Read-only. The message of the system followed by "Đang chuyển tới đăng nhập..."; the form is disabled and the user is sent to sign-in after about 1 second. |
| Error summary | Read-only. Lists the wrong fields after the button "Đăng ký" is clicked. |
| **Account information** | |
| Họ tên | Required, up to 160 characters. Messages "Vui lòng nhập họ tên." and "Họ tên không được vượt quá 160 ký tự." |
| Số điện thoại | Required, 10 digits, example 0912345678. Messages "Vui lòng nhập số điện thoại." and "Nhập số di động Việt Nam gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08 hoặc 09." |
| Email | Required, up to 254 characters. Messages "Vui lòng nhập email.", "Email không đúng định dạng." and "Email không được vượt quá 254 ký tự." An e-mail error returned by the server is shown under this field. |
| **Password** | |
| Mật khẩu | Required, hidden. Must follow the password policy of the system. Messages "Vui lòng nhập mật khẩu." and "Mật khẩu chưa đáp ứng chính sách của hệ thống." A password error returned by the server is shown under this field. |
| Hint | Read-only. "Mật khẩu tối thiểu n ký tự" with the other requirements of the policy (chữ hoa, chữ thường, số, ký tự đặc biệt). |
| Xác nhận mật khẩu | Required, must equal "Mật khẩu". Messages "Vui lòng xác nhận mật khẩu." and "Xác nhận mật khẩu không khớp." |
| **Actions** | |
| Button "Đăng ký" | Creates the account. Disabled while sending or after success. |
| Note | "Không cần mã xác nhận khi đăng ký." |
| Link "Đăng nhập" | Under "Đã có tài khoản?". Opens Customer Sign-in. |

### 1.5 Recover Account

This screen (`/recover` for staff, `/customer/recover` for customers) allows the user to reset a forgotten password in four steps. For customers it shows "Bước n/4".

| Field Name | Description |
|---|---|
| **Step bar** | |
| Steps | Read-only: 1 Nhập tài khoản, 2 Xác minh mã, 3 Đặt mật khẩu mới, 4 Hoàn tất. |
| **Step 1: Nhập tài khoản** | |
| Tài khoản (Email for customers) | Required; a customer e-mail must be valid. |
| Button "Gửi yêu cầu" | Sends the verification code and moves to step 2. |
| **Step 2: Xác minh mã** | |
| Mã xác minh | Required. |
| Button "Gửi lại mã" | Available again after a countdown ("Gửi lại sau Ns"). |
| Button "Nhập tài khoản khác" | Returns to step 1. |
| Button "Xác minh" | Checks the code and moves to step 3. |
| **Step 3: Đặt mật khẩu mới** | |
| Mật khẩu mới, Xác nhận mật khẩu | Required, up to 256 characters, hidden; both must match. |
| Button "Đặt lại mật khẩu" | Saves the new password. |
| **Step 4: Hoàn tất** | |
| Message | Read-only. "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." and a note that earlier sessions were revoked. |
| Buttons "Về trang đăng nhập", "Khôi phục tài khoản khác" | Open Sign-in, or restart the recovery. |
