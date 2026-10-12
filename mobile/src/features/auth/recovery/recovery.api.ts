import { request } from '@/services/api/http-client';
import type { ServerMessage } from '@/services/api/api-error';

export type RecoveryPolicy = {
  requiredLength: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
  sessionMinutes: number;
};

export type RecoveryStepId = 'request' | 'verify' | 'reset' | 'done';

type RequestOptions = { body?: string };

const json = (options: RequestOptions = {}): RequestInit => ({
  method: 'POST',
  headers: options.body ? { 'Content-Type': 'application/json' } : undefined,
  body: options.body,
});

/** Always answers with the same neutral MSG12 body, matching an account or not. */
export async function requestRecovery(identifier: string): Promise<ServerMessage> {
  return request<ServerMessage>('/api/auth/recovery/request', json({ body: JSON.stringify({ identifier }) }));
}

export async function resendRecovery(identifier: string): Promise<ServerMessage> {
  return request<ServerMessage>('/api/auth/recovery/resend', json({ body: JSON.stringify({ identifier }) }));
}

/** Returns the reset grant, or throws an ApiError carrying MSG108. */
export async function verifyRecovery(identifier: string, proof: string): Promise<string> {
  const response = await request<{ resetGrant: string }>(
    '/api/auth/recovery/verify',
    json({ body: JSON.stringify({ identifier, proof }) }),
  );
  return response.resetGrant;
}

/** Throws an ApiError carrying MSG108 (proof) or MSG109 (password policy). */
export async function resetPassword(
  identifier: string,
  resetGrant: string,
  newPassword: string,
  confirmPassword: string,
): Promise<ServerMessage> {
  return request<ServerMessage>(
    '/api/auth/recovery/reset',
    json({ body: JSON.stringify({ identifier, resetGrant, newPassword, confirmPassword }) }),
  );
}

/** The shared S02 password policy; its hints are shown before the password field. */
export async function fetchRecoveryPolicy(): Promise<RecoveryPolicy> {
  return request<RecoveryPolicy>('/api/auth/policy');
}
