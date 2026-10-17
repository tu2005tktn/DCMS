/**
 * S01 — Sign In API client.
 *
 * Endpoint contract (đã đối chiếu trong docs/implementation/UC01-UC31.md, UC-01):
 *   POST /api/auth/sign-in
 *     Request:  { userName: string, password: string }
 *     Response: { accessToken: string, expiresAt: string (ISO-8601), user: CurrentUser }
 *   GET  /api/auth/me
 *     Headers: Authorization: Bearer <accessToken>
 *     Response: CurrentUser
 *   POST /api/auth/sign-out
 *     Headers: Authorization: Bearer <accessToken>
 *     Response: 204 No Content
 *
 * Phạm vi đợt này CHƯA có:
 *  - Refresh token endpoint (backend giới hạn: chưa có refresh token rotation).
 *  - "Liên hệ đã xác minh" (email/phone) làm định danh đăng nhập (API chỉ nhận username).
 *  - Rate-limit đăng nhập (backend chưa có).
 *  => Screen gọi API thật, session refresh dùng /me + focus/30s poller, mọi 401 tự đăng xuất.
 */

import { ApiError } from '@/services/api/api-error';
import { authedRequest, request } from '@/services/api/http-client';
import { tokenStorage } from '@/services/storage/token-storage';
import type {
  SignInRequest,
  SignInResponse,
  CurrentUser,
  SessionSnapshot,
} from './sign-in.types';

const SIGN_IN_PATH = '/api/auth/sign-in';
const ME_PATH = '/api/auth/me';
const SIGN_OUT_PATH = '/api/auth/sign-out';

async function parseJson<T>(response: Response): Promise<T> {
  // Phòng trường hợp backend trả body rỗng khi 204 hoặc 200 không có body.
  const text = await response.text();
  if (!text) throw new ApiError('Phản hồi API rỗng.', response.status);
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new ApiError('Phản hồi API không hợp lệ.', response.status);
  }
}

function translateUnauthorized(status: number): string {
  // Chỉ hiển thị mã lỗi + status, không bao giờ kèm token/header. Backend dùng MSG07 cho 401.
  if (status === 401) return 'MSG07';
  if (status === 403) return 'MSG06';
  if (status >= 500) return 'MSG144';
  return 'MSG09';
}

export const signInApi = {
  /**
   * Gọi POST /api/auth/sign-in. Không lưu token — để SessionProvider quyết định lưu hay không.
   * Trả về payload thô để session layer xử lý tiếp.
   */
  async signIn(payload: SignInRequest): Promise<SignInResponse> {
    const response = await request(SIGN_IN_PATH, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const body = await parseJson<SignInResponse>(response);
    if (!body.accessToken || !body.user) {
      throw new ApiError('Phản hồi đăng nhập thiếu token hoặc thông tin người dùng.', response.status);
    }
    return body;
  },

  /**
   * Gọi GET /api/auth/me với token hiện tại để xác nhận phiên còn hiệu lực.
   * Dùng cho hydrate lúc khởi động và refresh mỗi 30s / focus.
   */
  async me(accessToken: string): Promise<CurrentUser> {
    const response = await authedRequest(ME_PATH, accessToken, { method: 'GET' });
    return parseJson<CurrentUser>(response);
  },

  /**
   * Gọi POST /api/auth/sign-out, trả về true nếu backend xác nhận thu hồi phiên.
   * Lỗi 401 vẫn coi là "đã đăng xuất" vì token hết hạn — không hiển thị lỗi cho người dùng.
   */
  async signOut(accessToken: string): Promise<{ revoked: boolean; status: number }> {
    try {
      const response = await authedRequest(SIGN_OUT_PATH, accessToken, { method: 'POST' });
      return { revoked: response.status === 204, status: response.status };
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        return { revoked: false, status: 401 };
      }
      throw error;
    }
  },

  /**
   * Ghép dữ liệu thành SessionSnapshot để lưu vào state/secure store.
   * KHÔNG log payload, KHÔNG trả về token ra ngoài SessionProvider.
   */
  buildSnapshot(payload: SignInResponse): SessionSnapshot {
    return {
      accessToken: payload.accessToken,
      expiresAt: payload.expiresAt,
      user: payload.user,
    };
  },

  /** Mã lỗi thân thiện để hiển thị — không lộ chi tiết kỹ thuật. */
  translateStatusToCode: translateUnauthorized,
};

/** Tiện ích thử gọi /me qua token đã lưu mà không cần khởi tạo SessionProvider. */
export async function probeStoredSession(): Promise<SessionSnapshot | null> {
  let token: string | null;
  try {
    token = await tokenStorage.get();
  } catch {
    // Web chưa hỗ trợ SecureStore — coi như chưa có phiên.
    return null;
  }
  if (!token) return null;
  try {
    const user = await signInApi.me(token);
    return { accessToken: token, user, expiresAt: '' };
  } catch (error) {
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      await tokenStorage.clear().catch(() => undefined);
      return null;
    }
    throw error;
  }
}
