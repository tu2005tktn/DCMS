/**
 * Client-side i18n resources for UC-02 account recovery (S03).
 *
 * The message codes and wording are taken from the v1.2 specification and must stay byte-identical
 * to the values the API returns, so a message can be re-rendered from either side. The backend is
 * the source of truth for text; this module exists so the UI never invents its own wording.
 */
export const recoveryMessages = {
  /** Always shown after a recovery request, whether or not the account exists. */
  MSG12:
    'Nếu thông tin tài khoản hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi qua kênh liên hệ đã đăng ký.',
  MSG13: 'Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.',
  MSG108:
    'Mã hoặc liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu xác minh lại.',
  MSG144: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.',
} as const;

/** Field names for the placeholder messages MSG02 and MSG04. */
export const recoveryFields = {
  account: 'Tài khoản',
  proof: 'Mã xác minh',
  newPassword: 'Mật khẩu mới',
  confirmPassword: 'Xác nhận mật khẩu',
} as const;

export const requiredMessage = (fieldName: string) => `Vui lòng nhập ${fieldName}.`;
export const formatMessage = (fieldName: string) => `${fieldName} không đúng định dạng.`;
export const maxLengthMessage = (fieldName: string, maxLength: number) =>
  `${fieldName} không được vượt quá ${maxLength} ký tự.`;

/** Shown when the user cannot reach the verification channel (alternative flow A1). */
export const channelHelpText =
  'Nếu bạn không truy cập được kênh liên hệ đã đăng ký, hãy liên hệ quản trị viên phòng khám để được hỗ trợ trực tiếp.';
