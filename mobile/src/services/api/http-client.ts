import { env } from '@/config/env';
import { ApiError } from './api-error';

// Supply authorization headers explicitly when a session contract exists.
export async function request(path: string, options: RequestInit = {}): Promise<Response> {
  if (!path.startsWith('/') || path.startsWith('//')) throw new Error('Use an API-relative path starting with one slash.');
  const response = await fetch(env.apiUrl + path, options);
  if (!response.ok) throw new ApiError('Yêu cầu API thất bại.', response.status);
  return response;
}
