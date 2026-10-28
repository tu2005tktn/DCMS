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
const REFRESH_KEY = 'dcms.web.refresh-token';

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
function readRefreshToken(): string | null {
  try { return globalThis.sessionStorage?.getItem(REFRESH_KEY) ?? null; } catch { return null; }
}
let refreshToken = readRefreshToken();
let sessionGeneration = 0;
let refreshing: { generation: number; promise: Promise<void> } | undefined;
let sessionLost: ((error: ApiError) => void) | undefined;

/** Cập nhật accessToken (lưu cả vào memory + sessionStorage). AuthContext gọi khi sign-in. */
export const setAccessToken = (token: string | null) => {
  sessionGeneration++;
  accessToken = token;
  writeAccessToken(token);
  if (!token) {
    refreshToken = null;
    try { globalThis.sessionStorage?.removeItem(REFRESH_KEY); } catch { /* Memory state still works. */ }
  }
};

export function setSessionTokens(session: { accessToken: string; refreshToken?: string }, renewal = false) {
  if (renewal) { accessToken = session.accessToken; writeAccessToken(accessToken); }
  else setAccessToken(session.accessToken);
  refreshToken = session.refreshToken ?? null;
  try {
    if (refreshToken) globalThis.sessionStorage?.setItem(REFRESH_KEY, refreshToken);
    else globalThis.sessionStorage?.removeItem(REFRESH_KEY);
  } catch { /* Memory state still works. */ }
}

function expiresSoon(token: string) {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number };
    return typeof payload.exp === 'number' && payload.exp * 1000 <= Date.now() + 60_000;
  } catch { return false; }
}

async function renewSession() {
  if (refreshing?.generation === sessionGeneration) { await refreshing.promise; return; }
  if (!accessToken || !refreshToken || !expiresSoon(accessToken)) return;
  const generation = sessionGeneration;
  const token = refreshToken;
  const renewal = (async () => {
    // Refresh is anonymous and must not recursively enter the authenticated client.
    const result = await api<{ accessToken: string; refreshToken: string }>('/api/auth/refresh', {
      method: 'POST', body: JSON.stringify({ refreshToken: token }),
    }, false).catch(error => {
      if (generation === sessionGeneration && error instanceof ApiError && error.status === 401) {
        setAccessToken(null); sessionLost?.(error);
      }
      throw error;
    });
    if (generation === sessionGeneration) setSessionTokens(result, true);
  })();
  refreshing = { generation, promise: renewal };
  try { await renewal; } finally { if (refreshing?.promise === renewal) refreshing = undefined; }
}

export const onSessionLost = (handler: typeof sessionLost) => { sessionLost = handler; };

export async function api<T>(path: string, init: RequestInit = {}, authenticated = true): Promise<T> {
  const generation = sessionGeneration;
  if (authenticated) {
    await renewSession();
    // A sign-out/account switch while renewal was pending must cancel the old action.
    if (generation !== sessionGeneration) throw new ApiError(401, 'MSG07', 'Phiên đăng nhập đã thay đổi. Vui lòng thử lại thao tác.');
  }
  const requestToken = accessToken;
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
    if (response.status === 401 && authenticated && requestToken === accessToken) {
      // S26+ — token invalid/expired → xoá khỏi storage, gọi sessionLost callback.
      setAccessToken(null);
      sessionLost?.(error);
    }
    throw error;
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}
