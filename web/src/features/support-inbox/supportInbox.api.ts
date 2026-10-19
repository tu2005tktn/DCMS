import { api, ApiError } from '../../services/api';
import type {
  StaffChangeCommand,
  StaffChangeResult,
  StaffResolveCommand,
  StaffResolveResult,
  StaffVerifyRequesterCommand,
  SupportRequestDetail,
  SupportRequestPage,
  SupportRequestStatus,
} from './types';

function commandFailure(error: unknown): Pick<StaffChangeResult, 'ok' | 'errorCode' | 'errorMessage'> {
  if (!(error instanceof ApiError)) return { ok: false, errorCode: 'SERVER', errorMessage: 'Không thể hoàn tất thao tác.' };
  const errorCode = error.status === 403 ? 'FORBIDDEN'
    : error.status === 404 ? 'NOT_FOUND'
      : error.status === 409 ? 'STALE_VERSION'
        : error.status === 0 ? 'NETWORK'
          : error.status >= 500 ? 'SERVER' : 'BAD_REQUEST';
  return { ok: false, errorCode, errorMessage: error.message };
}

export const supportInboxApi = {
  async listRequestsForStaff(query: {
    statuses: SupportRequestStatus[];
    branchId?: number;
    page: number;
    pageSize: number;
  }): Promise<SupportRequestPage> {
    const params = new URLSearchParams({
      statuses: query.statuses.join(','),
      page: String(query.page),
      pageSize: String(query.pageSize),
    });
    if (query.branchId) params.set('branchId', String(query.branchId));
    return api<SupportRequestPage>(`/api/support-requests?${params}`);
  },

  getRequest(id: string): Promise<SupportRequestDetail> {
    return api<SupportRequestDetail>(`/api/support-requests/${encodeURIComponent(id)}`);
  },

  verifyRequester(id: string, command: StaffVerifyRequesterCommand): Promise<SupportRequestDetail> {
    return api<SupportRequestDetail>(`/api/support-requests/${encodeURIComponent(id)}/verify-requester`, {
      method: 'POST', body: JSON.stringify(command),
    });
  },

  async staffChange(id: string, command: StaffChangeCommand): Promise<StaffChangeResult> {
    try {
      return await api<StaffChangeResult>(`/api/support-requests/${encodeURIComponent(id)}/change-appointment`, {
        method: 'POST', body: JSON.stringify(command),
      });
    } catch (error) { return commandFailure(error); }
  },

  async resolveRequest(id: string, command: StaffResolveCommand): Promise<StaffResolveResult> {
    try {
      const action = command.decision === 'Reject' ? 'reject' : 'resolve';
      return await api<StaffResolveResult>(`/api/support-requests/${encodeURIComponent(id)}/${action}`, {
        method: 'POST', body: JSON.stringify(command),
      });
    } catch (error) { return commandFailure(error) as StaffResolveResult; }
  },
};
