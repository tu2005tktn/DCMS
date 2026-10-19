import {
  formatMessage,
  maxLengthMessage,
  recoveryFields,
  requiredMessage,
} from './recovery.messages';
import type { RecoveryPolicy } from './recovery.api';

export const ACCOUNT_MAX_LENGTH = 64;
export const PROOF_MIN_LENGTH = 4;
export const PROOF_MAX_LENGTH = 64;
export const PASSWORD_MAX_LENGTH = 256;

/**
 * [PROPOSED] Resend cooldown shown by the countdown.
 *
 * The API deliberately returns a neutral body for /resend so the response cannot reveal whether an
 * account exists, which means the client cannot learn the exact remaining wait. The countdown is
 * therefore driven from the configured cooldown; a resend that is too early is simply refused
 * server-side with the same neutral answer. Must be confirmed with the clinic before Go-Live.
 */
export const RESEND_COOLDOWN_SECONDS = 120;

export type FieldErrors = Partial<Record<string, string>>;

export function validateAccount(identifier: string, maxLength = ACCOUNT_MAX_LENGTH): FieldErrors {
  const value = identifier.trim();
  if (!value) return { identifier: requiredMessage(recoveryFields.account) };
  if (value.length > maxLength)
    return { identifier: maxLengthMessage(recoveryFields.account, maxLength) };
  return {};
}

export function validateProof(proof: string): FieldErrors {
  const value = proof.trim();
  if (!value) return { proof: requiredMessage(recoveryFields.proof) };
  if (value.length < PROOF_MIN_LENGTH || value.length > PROOF_MAX_LENGTH)
    return { proof: formatMessage(recoveryFields.proof) };
  return {};
}

export function validatePasswordPair(newPassword: string, confirmPassword: string): FieldErrors {
  const errors: FieldErrors = {};
  if (!newPassword) errors.newPassword = requiredMessage(recoveryFields.newPassword);
  else if (newPassword.length > PASSWORD_MAX_LENGTH)
    errors.newPassword = maxLengthMessage(recoveryFields.newPassword, PASSWORD_MAX_LENGTH);
  if (!confirmPassword) errors.confirmPassword = requiredMessage(recoveryFields.confirmPassword);
  if (newPassword && confirmPassword && newPassword !== confirmPassword)
    errors.confirmPassword = formatMessage(recoveryFields.confirmPassword);
  return errors;
}

/** Hints rendered from the shared S02 policy so the client never invents its own rules. */
export function passwordHints(policy: RecoveryPolicy): string[] {
  const hints = [`Tối thiểu ${policy.requiredLength} ký tự`];
  if (policy.requireDigit) hints.push('Có chữ số');
  if (policy.requireLowercase) hints.push('Có chữ thường');
  if (policy.requireUppercase) hints.push('Có chữ in hoa');
  if (policy.requireNonAlphanumeric) hints.push('Có ký tự đặc biệt');
  return hints;
}

/** Maps a policy code returned alongside MSG109 to the specific rule that failed. */
export function policyViolations(policy: RecoveryPolicy, password: string): string[] {
  const violations: string[] = [];
  if (password.length < policy.requiredLength)
    violations.push(`Cần tối thiểu ${policy.requiredLength} ký tự.`);
  if (policy.requireDigit && !/\d/.test(password)) violations.push('Cần có chữ số.');
  if (policy.requireLowercase && !/[a-z]/.test(password)) violations.push('Cần có chữ thường.');
  if (policy.requireUppercase && !/[A-Z]/.test(password)) violations.push('Cần có chữ in hoa.');
  if (policy.requireNonAlphanumeric && !/[^A-Za-z0-9]/.test(password))
    violations.push('Cần có ký tự đặc biệt.');
  return violations;
}
