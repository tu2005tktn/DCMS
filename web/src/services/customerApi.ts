import { ApiError, type SystemMessage } from './api';

const base = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');
const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

// Customer requests carry their own token. Never use the staff client's module-level token.
export async function customerApi<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
  if (import.meta.env.DEV && globalThis.location?.pathname.startsWith('/__preview/clinical'))
    throw new ApiError(404, 'MSG113', 'Chế độ xem thử không kết nối API khách hàng.');
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body) headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);

  let response: Response;
  try { response = await fetch(base + path, { ...init, headers, cache: 'no-store' }); }
  catch { throw new ApiError(0, 'MSG144', connectionMessage); }

  if (!response.ok) {
    const body = await response.json().catch(() => null) as Partial<SystemMessage> | null;
    throw new ApiError(response.status, body?.code ?? 'MSG144', body?.message ?? connectionMessage);
  }
  return response.status === 204 ? undefined as T : await response.json() as T;
}
