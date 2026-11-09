import { api, apiBlob } from '../../services/api';
import type { CareOptions, CarePatient, CareTask, CareTaskDetail, CareWorklistPage, ContactAttempt, WorklistFilter } from './types';

const base = '/api/care-tasks';
function query(filter: WorklistFilter, extra: Record<string, string> = {}) {
  const params = new URLSearchParams({ from: filter.from, to: filter.to, ...extra });
  if (filter.branchId) params.set('branchId', String(filter.branchId));
  if (filter.ownerAccountId) params.set('ownerAccountId', String(filter.ownerAccountId));
  if (filter.type) params.set('type', filter.type);
  if (filter.status) params.set('status', filter.status);
  if (filter.search?.trim()) params.set('search', filter.search.trim());
  return params.toString();
}

export const customerCareApi = {
  options: () => api<CareOptions>(`${base}/options`),
  patients: (branchId: number, search: string) =>
    api<CarePatient[]>(`${base}/patient-options?${new URLSearchParams({ branchId: String(branchId), search })}`),
  worklist: (filter: WorklistFilter, page: number, pageSize: number) =>
    api<CareWorklistPage>(`${base}/worklist?${query(filter, { page: String(page), pageSize: String(pageSize) })}`),
  export: (filter: WorklistFilter) => apiBlob(`${base}/export?${query(filter)}`),
  detail: (id: string) => api<CareTaskDetail>(`${base}/${id}`),
  create: (body: { patientId: number; branchId: number; type: string; content: string; ownerAccountId: number | null; dueAt: string; requestKey: string }) =>
    api<CareTask>(base, { method: 'POST', body: JSON.stringify({ ...body, instructionId: null, appointmentId: null, reviewId: null }) }),
  update: (id: string, body: { expectedVersion: number; status: string; ownerAccountId: number | null; dueAt: string; content: string; requestKey: string }) =>
    api<CareTask>(`${base}/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),
  contact: (id: string, body: { channel: string; result: string; nextAction: string | null; expectedTaskVersion: number; requestKey: string }) =>
    api<ContactAttempt>(`${base}/${id}/contact-attempts`, { method: 'POST', body: JSON.stringify({ ...body, rebookedAppointmentId: null }) }),
};
