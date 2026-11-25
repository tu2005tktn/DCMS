import { env } from '@/config/env';
import { ApiError } from '@/services/api/api-error';
import type { AccessOverview, FamilyAccess, FamilyAppointment, FamilyMember, Page, RecordHeader, SaveFamilyMember } from './family-members.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

// Carries the server's `field` so form errors land on the right input (FAMILY_MEMBER_VALIDATION).
export class FamilyApiError extends ApiError {
  constructor(message: string, status: number, code: string, public readonly field?: string) { super(message, status, code); }
}

async function call<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', Authorization: `Bearer ${token}` };
  if (init.body) headers['Content-Type'] = 'application/json';
  let response: Response;
  try { response = await fetch(`${env.apiUrl}/api/me/family-members${path}`, { ...init, headers, cache: 'no-store' }); }
  catch { throw new FamilyApiError(connectionMessage, 0, 'MSG49'); }
  const body = await response.json().catch(() => null) as { code?: string; message?: string; field?: string } | null;
  if (!response.ok) throw new FamilyApiError(body?.message ?? connectionMessage, response.status, body?.code ?? 'MSG49', body?.field);
  return body as T;
}
const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) });

export const familyApi = {
  list: (token: string, keyword: string, page: number) =>
    call<Page<FamilyMember>>(`?${new URLSearchParams({ keyword, page: String(page), pageSize: '20' })}`, token),
  get: (token: string, id: string) => call<FamilyMember>(`/${encodeURIComponent(id)}`, token),
  create: (token: string, body: SaveFamilyMember, requestKey: string) => call<FamilyMember>('', token, post({ ...body, requestKey })),
  update: (token: string, id: string, body: SaveFamilyMember, expectedVersion: number, requestKey: string) =>
    call<FamilyMember>(`/${encodeURIComponent(id)}`, token, { method: 'PUT', body: JSON.stringify({ ...body, expectedVersion, requestKey }) }),
  remove: (token: string, id: string, expectedVersion: number, requestKey: string) =>
    call<{ id: string; version: number; deleted: boolean }>(`/${encodeURIComponent(id)}`, token,
      { method: 'DELETE', body: JSON.stringify({ expectedVersion, requestKey }) }),

  appointments: (token: string, id: string, page: number) =>
    call<Page<FamilyAppointment>>(`/${encodeURIComponent(id)}/appointments?page=${page}&pageSize=10`, token),
  access: (token: string, id: string) => call<AccessOverview>(`/${encodeURIComponent(id)}/record-access`, token),
  requestAccess: (token: string, id: string, patientCode: string | null, matchingInformation: string | null, requestKey: string) =>
    call<FamilyAccess>(`/${encodeURIComponent(id)}/record-access/requests`, token, post({ patientCode, matchingInformation, requestKey })),
  withdrawAccess: (token: string, id: string, requestId: string, expectedVersion: number, requestKey: string) =>
    call<FamilyAccess>(`/${encodeURIComponent(id)}/record-access/requests/${encodeURIComponent(requestId)}/withdraw`, token, post({ expectedVersion, requestKey })),
  header: (token: string, id: string) => call<RecordHeader>(`/${encodeURIComponent(id)}/record`, token),
  section: <T>(token: string, id: string, section: string, page: number) =>
    call<Page<T>>(`/${encodeURIComponent(id)}/record/${section}?page=${page}&pageSize=10`, token),
  documentUrl: (id: string, documentId: string) =>
    `${env.apiUrl}/api/me/family-members/${encodeURIComponent(id)}/record/documents/${encodeURIComponent(documentId)}/content`,
};

// Same idempotency rule as the web: resending an unchanged command reuses its key; any change gets a new key.
export function keyFor(ref: { current: { text: string; key: string } | null }, payload: unknown, newKey: () => string) {
  const text = JSON.stringify(payload);
  if (ref.current?.text !== text) ref.current = { text, key: newKey() };
  return ref.current.key;
}
