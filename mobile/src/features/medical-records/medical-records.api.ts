import { env } from '@/config/env';
import { ApiError } from '@/services/api/api-error';
import type { OwnTreatmentSummaryItem, SystemMessage, VisitPage, VisitScope } from './medical-records.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

export class RecordApiError extends ApiError {
  constructor(message: string, status: number, public readonly code: string) { super(message, status); }
}

// Mỗi request đều mang access token của phiên hiện tại; không lưu link file công khai.
async function call<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (init.body) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  let response: Response;
  try { response = await fetch(env.apiUrl + path, { ...init, headers, cache: 'no-store' }); }
  catch { throw new RecordApiError(connectionMessage, 0, 'MSG144'); }
  const body = await response.json().catch(() => null) as unknown;
  if (!response.ok) {
    const error = body as Partial<SystemMessage> | null;
    throw new RecordApiError(error?.message ?? connectionMessage, response.status, error?.code ?? 'MSG144');
  }
  return body as T;
}

// S41 luôn gọi API thật (không đi qua công tắc mock của màn lịch hẹn) để không bao giờ hiển thị dữ liệu giả đã lưu.
export const medicalRecordsApi = {
  treatmentSummary: (token: string) => call<OwnTreatmentSummaryItem[]>('/api/patients/me/treatment-summary', token),
  visits: (token: string, scope: VisitScope) =>
    call<VisitPage>(`/api/me/appointments?scope=${scope}&page=1&pageSize=50`, token),
};
