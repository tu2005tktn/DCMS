import { env } from '@/config/env';
import { ApiError } from './api-error';

// Supply authorization headers explicitly when a session contract exists.
export async function request(path: string, options: RequestInit = {}): Promise<Response> {
  if (!path.startsWith('/') || path.startsWith('//')) throw new Error('Use an API-relative path starting with one slash.');
  const response = await fetch(env.apiUrl + path, options);
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null);
    const message = body && typeof body === 'object' && 'message' in body && typeof body.message === 'string'
      ? body.message : 'Yêu cầu API thất bại.';
    const code = body && typeof body === 'object' && 'code' in body && typeof body.code === 'string'
      ? body.code : undefined;
    throw new ApiError(message, response.status, code);
  }
  return response;
}
