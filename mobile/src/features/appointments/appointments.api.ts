import { env } from '@/config/env';
import { AppointmentApiError } from './appointments.errors';
import type {
  AppointmentChangeResult, AppointmentDetail, AppointmentPage, AppointmentQuery, AvailabilitySlot, CancelCommand,
  RescheduleCommand, SupportCommand, SupportRequest, SupportResult, SystemMessage,
} from './appointments.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

export { AppointmentApiError };

export type AppointmentsApi = {
  list: (token: string, query: AppointmentQuery) => Promise<AppointmentPage>;
  detail: (token: string, code: string) => Promise<AppointmentDetail>;
  availability: (token: string, detail: AppointmentDetail, date: string) => Promise<AvailabilitySlot[]>;
  reschedule: (token: string, code: string, command: RescheduleCommand) => Promise<AppointmentChangeResult>;
  cancel: (token: string, code: string, command: CancelCommand) => Promise<AppointmentChangeResult>;
  requestSupport: (token: string, code: string, command: SupportCommand) => Promise<SupportResult>;
  withdrawSupport: (token: string, id: number, expectedVersion: number) => Promise<SupportRequest>;
};

async function call<T>(path: string, token: string | null, init: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (init.body) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;
  let response: Response;
  try { response = await fetch(env.apiUrl + path, { ...init, headers }); }
  catch { throw new AppointmentApiError(connectionMessage, 0, 'MSG144'); }
  const body = await response.json().catch(() => null) as unknown;
  if (!response.ok) {
    const error = body as Partial<SystemMessage> | null;
    throw new AppointmentApiError(error?.message ?? connectionMessage, response.status, error?.code ?? 'MSG144');
  }
  return body as T;
}

const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) });

// Real endpoints from the contract (§4). Reads belong to TV3; commands to this task; availability to Tư (S09).
const httpAppointmentsApi: AppointmentsApi = {
  list: (token, q) => {
    const query = new URLSearchParams({ scope: q.scope, status: q.status, page: String(q.page), pageSize: String(q.pageSize) });
    if (q.from) query.set('from', q.from);
    if (q.to) query.set('to', q.to);
    return call<AppointmentPage>(`/api/me/appointments?${query}`, token);
  },
  detail: (token, code) => call<AppointmentDetail>(`/api/me/appointments/${encodeURIComponent(code)}`, token),
  availability: async (token, detail, date) => {
    const query = new URLSearchParams({ date });
    return (await call<{ slots: AvailabilitySlot[] }>(`/api/appointments/${encodeURIComponent(detail.code)}/availability?${query}`, token)).slots;
  },
  reschedule: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/reschedule`, token, post(command)),
  cancel: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/cancel`, token, post(command)),
  requestSupport: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/support-requests`, token, post(command)),
  withdrawSupport: async (token, id, expectedVersion) =>
    (await call<{ request: SupportRequest }>(`/api/support-requests/${id}/withdraw`, token, post({ expectedVersion }))).request,
};

// S14/S16 always talk to the real API; there is no local data source.
export const appointmentsApi: AppointmentsApi = httpAppointmentsApi;
