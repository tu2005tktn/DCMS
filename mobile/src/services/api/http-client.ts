/**
 * HTTP client + error model.
 *
 * `request(path, options)` — anonymous (cho /api/auth/sign-in, /api/auth/policy).
 * `authedRequest(path, token, options)` — gắn Authorization: Bearer.
 *
 * ApiError giữ status để các tầng trên dịch sang mã System Messages (MSG06/07/09/144).
 * KHÔNG log body request/response vì có thể chứa token.
 */

import { env } from '@/config/env';
import { ApiError, type ServerMessage } from './api-error';

function buildUrl(path: string): string {
  if (!path.startsWith('/') || path.startsWith('//')) {
    throw new Error('API path phải bắt đầu bằng một dấu / và không bắt đầu bằng //.');
  }
  return env.apiUrl + path;
}

function buildHeaders(extra?: HeadersInit, accessToken?: string): HeadersInit {
  const headers: Record<string, string> = {
    Accept: 'application/json',
  };
  if (extra) {
    if (extra instanceof Headers) {
      extra.forEach((value, key) => {
        headers[key] = value;
      });
    } else if (Array.isArray(extra)) {
      for (const [key, value] of extra) {
        headers[key] = value;
      }
    } else {
      Object.assign(headers, extra as Record<string, string>);
    }
  }
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  }
  return headers;
}

async function send(path: string, options: RequestInit, accessToken?: string): Promise<Response> {
  const response = await fetch(buildUrl(path), {
    ...options,
    headers: buildHeaders(options.headers, accessToken),
  });
  if (!response.ok) {
    // Keep the server's System Message (code + text) when present; status stays available for S01 mapping.
    const body: unknown = await response.json().catch(() => null);
    const message = body && typeof body === 'object' && 'message' in body && typeof body.message === 'string'
      ? body.message : `Yêu cầu API thất bại (${response.status}).`;
    const code = body && typeof body === 'object' && 'code' in body && typeof body.code === 'string'
      ? body.code : undefined;
    throw new ApiError(message, response.status, code);
  }
  return response;
}

const fallbackMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

/**
 * Sends a request to the API and returns the parsed body.
 *
 * A failed answer carries the stable message code from the server so the screen can show the exact
 * wording (MSG108, MSG109, ...) instead of a generic string.
 */
export async function requestJson<T>(path: string, options: RequestInit = {}): Promise<T> {
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

export async function request(path: string, options: RequestInit = {}): Promise<Response> {
  return send(path, options);
}

export async function authedRequest(
  path: string,
  accessToken: string,
  options: RequestInit = {},
): Promise<Response> {
  return send(path, options, accessToken);
}
