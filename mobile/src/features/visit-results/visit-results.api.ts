import { env } from '@/config/env';
import { RecordApiError } from '@/features/medical-records/medical-records.api';
import type { SystemMessage } from '@/features/medical-records/medical-records.types';
import type { OwnDocumentPage, OwnVisitDetail, OwnVisitPage } from './visit-results.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

// Same contract as S41: bearer token of the current session on every request, no cache, server decides the patient.
async function call<T>(path: string, token: string): Promise<T> {
  let response: Response;
  try {
    response = await fetch(env.apiUrl + path, { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, cache: 'no-store' });
  } catch { throw new RecordApiError(connectionMessage, 0, 'MSG144'); }
  const body = await response.json().catch(() => null) as unknown;
  if (!response.ok) {
    const error = body as Partial<SystemMessage> | null;
    throw new RecordApiError(error?.message ?? connectionMessage, response.status, error?.code ?? 'MSG144');
  }
  return body as T;
}

export const pageSize = 20;

export const visitResultsApi = {
  visits: (token: string, page: number) => call<OwnVisitPage>(`/api/patients/me/visits?page=${page}&pageSize=${pageSize}`, token),
  visit: (token: string, visitId: string) => call<OwnVisitDetail>(`/api/patients/me/visits/${encodeURIComponent(visitId)}`, token),
  documents: (token: string, visitId: string, page: number) =>
    call<OwnDocumentPage>(`/api/patients/me/documents?visitId=${encodeURIComponent(visitId)}&page=${page}&pageSize=${pageSize}`, token),
};
