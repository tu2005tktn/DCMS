export type SystemMessage = { code: string; message: string };
export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) { super(message); }
}
const base = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
let accessToken: string | null = null;
let sessionLost: ((error: ApiError) => void) | undefined;
export const setAccessToken = (token: string | null) => { accessToken = token; };
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
    const error = new ApiError(response.status, body.code ?? fallback.code, body.message ?? fallback.message);
    if (response.status === 401 && authenticated) sessionLost?.(error);
    throw error;
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}
