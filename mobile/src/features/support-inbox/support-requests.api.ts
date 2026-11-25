/**
 * S26 — Staff support inbox HTTP client (mobile). Cùng endpoint với web `supportInbox.api.ts`.
 * Không log body vì có thể chứa thông tin cá nhân của người yêu cầu.
 */

import { ApiError } from '@/services/api/api-error';
import { authedRequest } from '@/services/api/http-client';
import type {
  CommandErrorCode,
  CommandResult,
  StaffChangeCommand,
  StaffResolveCommand,
  StaffVerifyRequesterCommand,
  SupportRequestDetail,
  SupportRequestPage,
  SupportRequestStatus,
} from './support-requests.types';

export type AvailabilitySlot = { start: string; end: string; chairId: number; dentistId: number };

const base = '/api/support-requests';
const json = (body: unknown): RequestInit => ({
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
});

function failure(error: unknown): Extract<CommandResult, { ok: false }> {
  if (!(error instanceof ApiError)) {
    return { ok: false, errorCode: 'NETWORK', errorMessage: 'Không thể kết nối máy chủ. Vui lòng thử lại.' };
  }
  const errorCode: CommandErrorCode = error.status === 401 ? 'UNAUTHORIZED'
    : error.status === 403 ? 'FORBIDDEN'
      : error.status === 404 ? 'NOT_FOUND'
        : error.status === 409 ? 'STALE_VERSION'
          : error.status >= 500 ? 'SERVER' : 'BAD_REQUEST';
  return { ok: false, errorCode, errorMessage: error.message };
}

/** Command trả về chi tiết yêu cầu mới (verify) hoặc `{ ok, request }` (change/resolve/reject). */
async function command(path: string, body: unknown, token: string): Promise<CommandResult> {
  try {
    const response = await authedRequest(path, token, json(body));
    const value = (await response.json().catch(() => null)) as
      | (SupportRequestDetail & { ok?: undefined })
      | { ok: boolean; request?: SupportRequestDetail; errorCode?: string | null; errorMessage?: string | null }
      | null;
    if (value && 'ok' in value && value.ok === false) {
      const known: CommandErrorCode[] = ['FORBIDDEN', 'NOT_FOUND', 'STALE_VERSION', 'BAD_REQUEST', 'SERVER'];
      const errorCode = known.find(code => code === value.errorCode) ?? 'BAD_REQUEST';
      return { ok: false, errorCode, errorMessage: value.errorMessage ?? 'Không thể hoàn tất thao tác.' };
    }
    if (value && 'ok' in value && typeof value.ok === 'boolean') return { ok: true, request: value.request ?? null };
    return { ok: true, request: (value as SupportRequestDetail | null) ?? null };
  } catch (error) {
    return failure(error);
  }
}

export const supportRequestsApi = {
  async list(query: { statuses: SupportRequestStatus[]; page: number; pageSize: number }, token: string): Promise<SupportRequestPage> {
    const params = new URLSearchParams({
      statuses: query.statuses.join(','),
      page: String(query.page),
      pageSize: String(query.pageSize),
    });
    const response = await authedRequest(`${base}?${params}`, token, { method: 'GET' });
    return (await response.json()) as SupportRequestPage;
  },

  async get(id: string, token: string): Promise<SupportRequestDetail> {
    const response = await authedRequest(`${base}/${encodeURIComponent(id)}`, token, { method: 'GET' });
    return (await response.json()) as SupportRequestDetail;
  },

  /** Free slots for the appointment's branch/dentist/service on one clinic date (YYYY-MM-DD). */
  async availability(query: { branchId: number; dentistId: number; serviceId: number | null; date: string; durationMinutes: number },
    token: string): Promise<AvailabilitySlot[]> {
    const params = new URLSearchParams({ branchId: String(query.branchId), dentistId: String(query.dentistId),
      date: query.date, durationMinutes: String(query.durationMinutes) });
    if (query.serviceId) params.set('serviceId', String(query.serviceId));
    const response = await authedRequest(`/api/availability?${params}`, token, { method: 'GET' });
    return ((await response.json()) as { slots: AvailabilitySlot[] }).slots;
  },

  verifyRequester: (id: string, body: StaffVerifyRequesterCommand, token: string) =>
    command(`${base}/${encodeURIComponent(id)}/verify-requester`, body, token),

  changeAppointment: (id: string, body: StaffChangeCommand, token: string) =>
    command(`${base}/${encodeURIComponent(id)}/change-appointment`, body, token),

  decide: (id: string, body: StaffResolveCommand, token: string) =>
    command(`${base}/${encodeURIComponent(id)}/${body.decision === 'Reject' ? 'reject' : 'resolve'}`, body, token),
};
