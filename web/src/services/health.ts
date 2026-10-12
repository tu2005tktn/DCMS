export type HealthData = { api: string; database: string };
export type HealthResult =
  | { kind: 'loading' }
  | { kind: 'response'; data: HealthData; httpStatus: number }
  | { kind: 'error'; message: string };

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export async function fetchHealth(): Promise<HealthResult> {
  try {
    const response = await fetch(`${apiBaseUrl.replace(/\/$/, '')}/health`, { cache: 'no-store' });
    const data: HealthData = await response.json();
    if (typeof data.api !== 'string' || typeof data.database !== 'string') {
      return { kind: 'error', message: 'Phản hồi health không hợp lệ.' };
    }
    return { kind: 'response', data, httpStatus: response.status };
  } catch (error) {
    return { kind: 'error', message: error instanceof Error ? error.message : 'Lỗi không xác định' };
  }
}
