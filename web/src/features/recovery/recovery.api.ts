import { api, type SystemMessage } from '../../services/api';

/**
 * UC-02 account recovery API client.
 *
 * Every self-service call returns the same neutral body for an existing and a non-existing
 * account, so nothing here may branch on "account found" - the screen cannot tell them apart and
 * must not try to.
 */
export type RecoveryPolicy = {
  requiredLength: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
  sessionMinutes: number;
};

/** Marks a step of the recovery flow. */
export type RecoveryStepId = 'request' | 'verify' | 'reset' | 'done';

const base = (customer: boolean) => customer ? '/api/customer/recovery' : '/api/auth/recovery';

export async function requestRecovery(identifier: string, customer = false): Promise<SystemMessage> {
  return api<SystemMessage>(
    `${base(customer)}/request`,
    { method: 'POST', body: JSON.stringify({ identifier }) },
    false,
  );
}

export async function resendRecovery(identifier: string, customer = false): Promise<SystemMessage> {
  return api<SystemMessage>(
    `${base(customer)}/resend`,
    { method: 'POST', body: JSON.stringify({ identifier }) },
    false,
  );
}

/** Returns the reset grant on success, or throws an ApiError carrying MSG108. */
export async function verifyRecovery(identifier: string, proof: string, customer = false): Promise<string> {
  const response = await api<{ resetGrant: string }>(
    `${base(customer)}/verify`,
    { method: 'POST', body: JSON.stringify({ identifier, proof }) },
    false,
  );
  return response.resetGrant;
}

/** Throws an ApiError carrying MSG108 (proof rejected) or MSG109 (password policy). */
export async function resetPassword(
  identifier: string,
  resetGrant: string,
  newPassword: string,
  confirmPassword: string,
  customer = false,
): Promise<SystemMessage> {
  return api<SystemMessage>(
    `${base(customer)}/reset`,
    {
      method: 'POST',
      body: JSON.stringify({ identifier, resetGrant, newPassword, confirmPassword }),
    },
    false,
  );
}

/**
 * The shared S02 password policy. Reused as-is: the screen never hard-codes password rules.
 */
export async function fetchRecoveryPolicy(): Promise<RecoveryPolicy> {
  return api<RecoveryPolicy>('/api/auth/policy', {}, false);
}
