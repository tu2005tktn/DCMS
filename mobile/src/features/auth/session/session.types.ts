/**
 * Session types.
 *
 * Trạng thái phiên:
 *  - 'initializing': đang đọc SecureStore và gọi /me để hydrate. UI hiển thị splash.
 *  - 'anonymous': chưa đăng nhập. Routes trong (auth) cho phép.
 *  - 'authenticated': đã có CurrentUser + accessToken. Routes trong (account) cho phép.
 *  - 'error': lỗi mạng khi hydrate (không phải 401). UI cho phép thử lại.
 *
 * Backend đợt này CHƯA có refresh token; "refresh" nghĩa là gọi lại /me để phát hiện
 * account bị khóa / role bị thu hồi (kịch bản S04/DCMS-75).
 */

import type { CurrentUser } from '../sign-in/sign-in.types';

export type SessionState =
  | { status: 'initializing' }
  | { status: 'anonymous' }
  | {
      status: 'authenticated';
      user: CurrentUser;
      accessToken: string;
      expiresAt: string;
    }
  | { status: 'error'; message: string };

export interface SessionContextValue {
  state: SessionState;
  signIn: (snapshot: { accessToken: string; user: CurrentUser; expiresAt: string }) => Promise<void>;
  signOut: (reason?: 'user' | 'expired' | 'role-revoked') => Promise<void>;
  /** Gọi lại /me để đồng bộ role/status. Trả về true nếu còn hiệu lực. */
  refresh: () => Promise<boolean>;
}
