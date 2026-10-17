import { recoveryFields, channelHelpText } from './recovery.messages';
import type { RecoveryPolicy } from './recovery.api';

export const ACCOUNT_MAX_LENGTH = 64;
export const PROOF_MIN_LENGTH = 4;
export const PROOF_MAX_LENGTH = 24;
export const PASSWORD_MAX_LENGTH = 256;

export type FieldErrors = Partial<Record<string, string>>;

export function validateAccount(identifier: string): FieldErrors {
  const value = identifier.trim();
  if (!value) return { identifier: `Vui lòng nhập ${recoveryFields.account}.` };
  if (value.length > ACCOUNT_MAX_LENGTH)
    return { identifier: `${recoveryFields.account} không được vượt quá ${ACCOUNT_MAX_LENGTH} ký tự.` };
  return {};
}

export function validateProof(proof: string): FieldErrors {
  const value = proof.trim();
  if (!value) return { proof: `Vui lòng nhập ${recoveryFields.proof}.` };
  if (value.length < PROOF_MIN_LENGTH || value.length > PROOF_MAX_LENGTH)
    return { proof: `${recoveryFields.proof} không đúng định dạng.` };
  return {};
}

export function validatePasswordPair(newPassword: string, confirmPassword: string): FieldErrors {
  const errors: FieldErrors = {};
  if (!newPassword) errors.newPassword = `Vui lòng nhập ${recoveryFields.newPassword}.`;
  else if (newPassword.length > PASSWORD_MAX_LENGTH)
    errors.newPassword = `${recoveryFields.newPassword} không được vượt quá ${PASSWORD_MAX_LENGTH} ký tự.`;
  if (!confirmPassword) errors.confirmPassword = `Vui lòng nhập ${recoveryFields.confirmPassword}.`;
  if (newPassword && confirmPassword && newPassword !== confirmPassword)
    errors.confirmPassword = `${recoveryFields.confirmPassword} không đúng định dạng.`;
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

export { channelHelpText };
