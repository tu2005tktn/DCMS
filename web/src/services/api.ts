/**
 * Web HTTP client + session storage.
 *
 * S26+ — lưu accessToken vào sessionStorage để giữ session qua F5. Khi đóng tab,
 * sessionStorage tự xoá → bắt buộc đăng nhập lại (khớp đặc tả S01 mà RUN-ACCOUNTS.vi.md
 * đã ghi: "đóng tab cần đăng nhập lại").
 *
 * `request(path, options)` — anonymous (cho /api/auth/sign-in, /api/auth/policy).
 * `authedRequest(path, token, options)` — gắn Authorization: Bearer.
 *
 * ApiError giữ status để các tầng trên dịch sang mã System Messages (MSG06/07/09/144).
 * KHÔNG log body request/response vì có thể chứa token.
 */

export type SystemMessage = { code: string; message: string; field?: string };

export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string, public field?: string) { super(message); }
}

const base = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');

// S26+ — sessionStorage hydrate. Tách key để không va chạm với các mục khác trong tương lai.
const SESSION_KEY = 'dcms.web.access-token';

/** Đọc accessToken từ sessionStorage. Trả null nếu không có hoặc storage lỗi. */
export function readAccessToken(): string | null {
  try {
    return globalThis.sessionStorage?.getItem(SESSION_KEY) ?? null;
  } catch {
    return null;
  }
}

/** Lưu accessToken vào sessionStorage. */
function writeAccessToken(token: string | null): void {
  try {
    if (token) globalThis.sessionStorage?.setItem(SESSION_KEY, token);
    else globalThis.sessionStorage?.removeItem(SESSION_KEY);
  } catch {
    // sessionStorage có thể bị tắt (private mode / quota) — bỏ qua, app vẫn chạy trong session hiện tại.
  }
}

let accessToken: string | null = readAccessToken();
let sessionLost: ((error: ApiError) => void) | undefined;

/** Cập nhật accessToken (lưu cả vào memory + sessionStorage). AuthContext gọi khi sign-in. */
export const setAccessToken = (token: string | null) => {
  accessToken = token;
  writeAccessToken(token);
};

export const onSessionLost = (handler: typeof sessionLost) => { sessionLost = handler; };

export async function api<T>(path: string, init: RequestInit = {}, authenticated = true): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body) headers.set('Content-Type', 'application/json');
  if (authenticated && accessToken) headers.set('Authorization', `Bearer ${accessToken}`);
  let response: Response;
  try { response = await fetch(base + path, { ...init, headers, cache: 'no-store' }); }
  catch { throw new ApiError(0, 'MSG144', 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.'); }
  if (!response.ok) {
    const fallback = { code: 'MSG144', message: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.' };
    const body = await response.json().catch(() => fallback) as SystemMessage;
    const error = new ApiError(response.status, body.code ?? fallback.code, body.message ?? fallback.message, body.field);
    if (response.status === 401 && authenticated) {
      // S26+ — token invalid/expired → xoá khỏi storage, gọi sessionLost callback.
      setAccessToken(null);
      sessionLost?.(error);
    }
    throw error;
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}
