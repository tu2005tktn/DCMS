/**
 * Client-side messages for UC-02 account recovery (S03).
 * The wording is fixed by the v1.2 specification and matches what the API returns.
 */
export const recoveryMessages = {
  MSG12:
    'Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.',
  MSG13: 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.',
  MSG108: 'Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.',
  MSG144: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.',
} as const;

export const recoveryFields = {
  account: 'Tài khoản',
  proof: 'Mã xác minh',
  newPassword: 'Mật khẩu mới',
  confirmPassword: 'Xác nhận mật khẩu',
} as const;

export const channelHelpText =
  'Nếu bạn không truy cập được kênh liên hệ đã đăng ký, hãy liên hệ quản trị viên phòng khám để được hỗ trợ trực tiếp.';

/**
 * [PROPOSED] Resend cooldown shown by the countdown.
 * The API answer for /resend is neutral, so the client cannot learn the exact remaining wait.
 */
export const RESEND_COOLDOWN_SECONDS = 120;
