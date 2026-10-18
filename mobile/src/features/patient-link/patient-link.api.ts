import { env } from '@/config/env';
import { ApiError } from '@/services/api/api-error';
import type { OwnPatientRecord, OwnRecordChange, OwnRecordSummary, SelfLinkChange, UpdateOwnRecord, SelfLinkOverview, SelfLinkRequest, SubmitLinkRequest, SystemMessage } from './patient-link.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

export class LinkApiError extends ApiError {
  constructor(message: string, status: number, public readonly code: string) { super(message, status); }
}

// Every S07 operation uses the access token from the shared S01 session.
async function call<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (init.body) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  let response: Response;
  try { response = await fetch(env.apiUrl + path, { ...init, headers }); }
  catch { throw new LinkApiError(connectionMessage, 0, 'MSG144'); }
  const body = await response.json().catch(() => null) as unknown;
  if (!response.ok) {
    const error = body as Partial<SystemMessage> | null;
    throw new LinkApiError(error?.message ?? connectionMessage, response.status, error?.code ?? 'MSG144');
  }
  return body as T;
}

export const patientLinkApi = {
  overview: (token: string) => call<SelfLinkOverview>('/api/patient-links/me', token),
  submit: (token: string, request: SubmitLinkRequest) =>
    call<SelfLinkChange>('/api/patient-links/me/requests', token, { method: 'POST', body: JSON.stringify(request) }),
  withdraw: (token: string, id: number, version: number) =>
    call<SelfLinkRequest>(`/api/patient-links/me/requests/${id}/withdraw`, token, { method: 'POST', body: JSON.stringify({ version }) }),
  ownRecord: (token: string) => call<OwnRecordSummary>('/api/patient-links/me/record', token),
  myRecord: (token: string) => call<OwnPatientRecord>('/api/patients/me', token),
  updateMyRecord: (token: string, request: UpdateOwnRecord) =>
    call<OwnRecordChange>('/api/patients/me', token, { method: 'PUT', body: JSON.stringify(request) }),
};
