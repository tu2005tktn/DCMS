/**
 * S01 — Sign In validation.
 *
 * Username 3–64 ký tự A-Z a-z 0-9 . _ -  (xem AccountService.UserNamePattern trong backend).
 * Password tối đa 256 ký tự; backend kiểm tra policy thật qua /api/auth/policy, nhưng
 * validate tối thiểu client để giảm round-trip khi rỗng.
 *
 * Không sinh thêm rule ngoài những gì backend đã ghi nhận (UC01-UC31.md, mục
 * "Cấu hình còn cần xác nhận" — username/field limits là validation hiện tại cần rà soát
 * trước deployment, không coi là luật cứng).
 */

import { SYSTEM_MESSAGE_CODES } from './sign-in.types';

const USERNAME_PATTERN = /^[A-Za-z0-9._-]{3,64}$/;
const MAX_PASSWORD = 256;

export interface SignInFormInput {
  userName: string;
  password: string;
}

export interface SignInValidationError {
  field: 'userName' | 'password' | 'form';
  code: keyof typeof SYSTEM_MESSAGE_CODES;
  message: string;
}

export function validateSignInForm(input: SignInFormInput): SignInValidationError | null {
  const userName = input.userName.trim();
  if (!userName) {
    return { field: 'userName', code: 'MSG02', message: 'Vui lòng nhập tên đăng nhập.' };
  }
  if (!USERNAME_PATTERN.test(userName)) {
    return { field: 'userName', code: 'MSG04', message: 'Tên đăng nhập không đúng định dạng.' };
  }
  const password = input.password;
  if (!password) {
    return { field: 'password', code: 'MSG02', message: 'Vui lòng nhập mật khẩu.' };
  }
  if (password.length > MAX_PASSWORD) {
    return { field: 'password', code: 'MSG03', message: 'Mật khẩu không được vượt quá 256 ký tự.' };
  }
  return null;
}

/** Validate đơn trường để hiển thị ngay khi người dùng rời ô. */
export function validateSignInField(field: 'userName' | 'password', value: string): SignInValidationError | null {
  if (field === 'userName') {
    const trimmed = value.trim();
    if (!trimmed) return { field, code: 'MSG02', message: 'Vui lòng nhập tên đăng nhập.' };
    if (!USERNAME_PATTERN.test(trimmed)) return { field, code: 'MSG04', message: 'Tên đăng nhập không đúng định dạng.' };
    return null;
  }
  if (!value) return { field, code: 'MSG02', message: 'Vui lòng nhập mật khẩu.' };
  if (value.length > MAX_PASSWORD) return { field, code: 'MSG03', message: 'Mật khẩu không được vượt quá 256 ký tự.' };
  return null;
}
