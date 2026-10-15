/**
 * S01 — Sign In types.
 *
 * Bám sát SignInRequest / SignInResponse / CurrentUser trong
 * backend/src/DCMS.Application/Features/Authentication/Contracts.cs.
 * Giữ EffectiveRoles, HasVerifiedPatientLink để sau này UC-36 (Patient link)
 * có thể kiểm tra quyền mà không cần đổi shape.
 */

export interface SignInRequest {
  userName: string;
  password: string;
}

export interface CurrentUser {
  id: number;
  userName: string;
  fullName: string;
  accountType: 'Staff' | 'Public' | string;
  roles: string[];
  effectiveRoles: string[];
  permissions: string[];
  hasVerifiedPatientLink: boolean;
}

export interface SignInResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export interface SessionSnapshot {
  accessToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export type SignInFeedback =
  | { kind: 'idle' }
  | { kind: 'submitting' }
  | { kind: 'success' }
  | { kind: 'error'; code: 'MSG02' | 'MSG03' | 'MSG04' | 'MSG06' | 'MSG07' | 'MSG09' | 'MSG144' | 'NETWORK'; message: string };

/** Mã System Messages dùng chung từ đặc tả v1.2. Tham chiếu docs/specs/... */
export const SYSTEM_MESSAGE_CODES = {
  MSG02: 'MSG02',
  MSG03: 'MSG03',
  MSG04: 'MSG04',
  MSG06: 'MSG06',
  MSG07: 'MSG07',
  MSG09: 'MSG09',
  MSG144: 'MSG144',
} as const;
