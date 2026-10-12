import { isVietnameseMobile } from '@/utils/contact';
import type { CredentialPolicy, RegistrationErrors, RegistrationForm } from './registration.types';

export function validateRegistration(form: RegistrationForm, policy?: CredentialPolicy): RegistrationErrors {
  const errors: RegistrationErrors = {};
  const fullName = form.fullName.trim();
  if (!fullName) errors.fullName = 'Vui lòng nhập họ tên.';
  else if (fullName.length > 160) errors.fullName = 'Họ tên không được vượt quá 160 ký tự.';

  if (!form.phoneNumber.trim()) errors.phoneNumber = 'Vui lòng nhập số điện thoại.';
  else if (!isVietnameseMobile(form.phoneNumber)) {
    errors.phoneNumber = 'Nhập số di động Việt Nam gồm 10 chữ số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.';
  }

  const email = form.email.trim();
  if (!email) errors.email = 'Vui lòng nhập email.';
  else if (email.length > 254) errors.email = 'Email không được vượt quá 254 ký tự.';
  else if (!/^[^\s@]+@[^\s@]+$/u.test(email)) errors.email = 'Email không đúng định dạng.';

  if (!form.password) errors.password = 'Vui lòng nhập mật khẩu.';
  else if (form.password.length > (policy?.maximumLength ?? 256)) {
    errors.password = `Mật khẩu không được vượt quá ${policy?.maximumLength ?? 256} ký tự.`;
  } else if (policy && (form.password.length < policy.requiredLength
    || (policy.requireDigit && !/\p{Nd}/u.test(form.password))
    || (policy.requireLowercase && !/\p{Ll}/u.test(form.password))
    || (policy.requireUppercase && !/\p{Lu}/u.test(form.password))
    || (policy.requireNonAlphanumeric && !/[^\p{L}\p{N}]/u.test(form.password)))) {
    errors.password = 'Mật khẩu chưa đáp ứng chính sách hiển thị bên dưới.';
  }

  if (!form.confirmPassword) errors.confirmPassword = 'Vui lòng xác nhận mật khẩu.';
  else if (form.confirmPassword !== form.password) errors.confirmPassword = 'Xác nhận mật khẩu không khớp.';
  return errors;
}
