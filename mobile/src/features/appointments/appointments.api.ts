import { env } from '@/config/env';
import { AppointmentApiError } from './appointments.errors';
import { mockAppointmentsApi } from './appointments.mock';
import type {
  AppointmentChangeResult, AppointmentDetail, AppointmentPage, AppointmentQuery, AvailabilitySlot, CancelCommand,
  RescheduleCommand, RescheduleDentist, SupportCommand, SupportRequest, SupportResult, SystemMessage,
} from './appointments.types';

const connectionMessage = 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.';

export { AppointmentApiError };

export type AppointmentsApi = {
  list: (token: string, query: AppointmentQuery) => Promise<AppointmentPage>;
  detail: (token: string, code: string) => Promise<AppointmentDetail>;
  rescheduleDentists: (token: string, detail: AppointmentDetail) => Promise<RescheduleDentist[]>;
  availability: (token: string, detail: AppointmentDetail, date: string, dentistId: number) => Promise<AvailabilitySlot[]>;
  reschedule: (token: string, code: string, command: RescheduleCommand) => Promise<AppointmentChangeResult>;
  cancel: (token: string, code: string, command: CancelCommand) => Promise<AppointmentChangeResult>;
  requestSupport: (token: string, code: string, command: SupportCommand) => Promise<SupportResult>;
  withdrawSupport: (token: string, id: number | string, expectedVersion: number) => Promise<SupportRequest>;
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
  rescheduleDentists: async (token, detail) => {
    const query = new URLSearchParams({ branchId: String(detail.branchId) });
    const options = await call<{ employees: { id: number; branchId: number; name: string; canDentist: boolean }[] }>(
      `/api/availability/options?${query}`, token);
    return options.employees.filter(item => item.branchId === detail.branchId && item.canDentist
      && item.id !== detail.assistantId).map(({ id, branchId, name }) => ({ id, branchId, name }));
  },
  availability: async (token, detail, date, dentistId) => {
    const query = new URLSearchParams({ date, dentistId: String(dentistId) });
    return (await call<{ slots: AvailabilitySlot[] }>(`/api/appointments/${encodeURIComponent(detail.code)}/availability?${query}`, token)).slots;
  },
  reschedule: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/reschedule`, token, post(command)),
  cancel: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/cancel`, token, post(command)),
  requestSupport: (token, code, command) => call(`/api/appointments/${encodeURIComponent(code)}/support-requests`, token, post(command)),
  withdrawSupport: (token, id, expectedVersion) => call(`/api/support-requests/${id}/withdraw`, token, post({ expectedVersion })),
};

// Mock DTOs until TV2/TV3 ship the Appointment API; set EXPO_PUBLIC_APPOINTMENTS_MOCK=false to use the server.
export const usingMockAppointments = process.env.EXPO_PUBLIC_APPOINTMENTS_MOCK?.trim().toLowerCase() !== 'false';
export const appointmentsApi: AppointmentsApi = usingMockAppointments ? mockAppointmentsApi : httpAppointmentsApi;
