import { api } from '../../services/api';
import { uiPreview } from '../ui-preview/preview';
import { previewCareApi } from './preview';
import type {
  AttemptCommand, CareOptions, CareQuery, CareTaskDetail, CareTaskPage, EscalationCommand, StatusCommand,
} from './types';

// S25/S60 data access. Endpoints follow the [PROPOSED] DCMS-99 contract in docs/implementation/S25-S60-CARE-TASKS-UI.md;
// when Vinh finalises the API only this file changes.
export type CareApi = {
  options: () => Promise<CareOptions>;
  list: (query: CareQuery) => Promise<CareTaskPage>;
  detail: (taskId: string) => Promise<CareTaskDetail>;
  recordAttempt: (taskId: string, command: AttemptCommand) => Promise<CareTaskDetail>;
  changeStatus: (taskId: string, command: StatusCommand) => Promise<CareTaskDetail>;
  escalate: (taskId: string, command: EscalationCommand) => Promise<CareTaskDetail>;
};

const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) });
const id = (taskId: string) => encodeURIComponent(taskId);

const httpCareApi: CareApi = {
  options: () => api('/api/care-tasks/options'),
  list: q => {
    const query = new URLSearchParams({ page: String(q.page), pageSize: String(q.pageSize) });
    for (const key of ['from', 'to', 'ownerId', 'patient', 'type', 'status', 'branchId'] as const)
      if (q[key] !== undefined && q[key] !== '') query.set(key, String(q[key]));
    return api(`/api/care-tasks?${query}`);
  },
  detail: taskId => api(`/api/care-tasks/${id(taskId)}`),
  recordAttempt: async (taskId, c) => (await api<{ task: CareTaskDetail }>(`/api/care-tasks/${id(taskId)}/attempts`, post(c))).task,
  changeStatus: async (taskId, c) => (await api<{ task: CareTaskDetail }>(`/api/care-tasks/${id(taskId)}/status`, post(c))).task,
  escalate: async (taskId, c) => (await api<{ task: CareTaskDetail }>(`/api/care-tasks/${id(taskId)}/escalations`, post(c))).task,
};

export const careApi: CareApi = uiPreview ? previewCareApi : httpCareApi;
