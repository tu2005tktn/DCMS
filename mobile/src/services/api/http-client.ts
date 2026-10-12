import { env } from '@/config/env';
import { ApiError, type ServerMessage } from './api-error';

const fallbackMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

/**
 * Sends a request to the API and returns the parsed body.
 *
 * A failed answer carries the stable message code from the server so the screen can show the exact
 * wording (MSG108, MSG109, ...) instead of a generic string.
 */
export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  if (!path.startsWith('/') || path.startsWith('//')) throw new Error('Use an API-relative path starting with one slash.');
  let response: Response;
  try {
    response = await fetch(env.apiUrl + path, options);
  } catch {
    throw new ApiError(fallbackMessage, 0, 'MSG144');
  }
  const body = (await response.json().catch(() => null)) as ServerMessage | T | null;
  if (!response.ok) {
    const server = body && typeof body === 'object' && 'message' in body ? (body as ServerMessage) : null;
    throw new ApiError(server?.message ?? fallbackMessage, response.status, server?.code ?? 'MSG144');
  }
  return body as T;
}
