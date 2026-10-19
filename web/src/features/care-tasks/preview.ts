import { ApiError } from '../../services/api';
import { previewDelay } from '../ui-preview/preview';
import type { CareApi } from './care.api';
import type { CareTaskDetail } from './types';

// UI PREVIEW ONLY (VITE_UI_PREVIEW=true). Illustrative tasks so the S25/S60 layout can be reviewed before DCMS-99 exists.
// Nothing is persisted and none of this is used when the flag is off. Delete this file once the real API is available.
const at = (days: number, hour = 9) => { const d = new Date(); d.setDate(d.getDate() + days); d.setHours(hour, 0, 0, 0); return d.toISOString(); };
const tasks: CareTaskDetail[] = [
  { taskId: 'ct-1', version: 1, type: 'FollowUpBooking', status: 'Open', flags: ['NeedsBooking'], title: 'Hẹn tái khám sau trám răng',
    patient: { id: 1, patientCode: 'BN-MINH-HOA-1', fullName: 'Bệnh nhân minh họa 1' }, branchId: 1, branchName: 'Chi nhánh minh họa',
    owner: { id: 4, name: 'Trợ lý minh họa' }, dueAt: at(0), content: 'Liên hệ đặt lịch tái khám theo chỉ dẫn của bác sĩ.',
    source: { instruction: { id: 'ins-1', label: 'Chỉ dẫn tái khám (minh họa)', status: 'NeedsBooking' } }, attempts: [], escalations: [],
    allowedActions: ['start', 'done', 'cancel', 'recordAttempt', 'rebook', 'escalate'] },
  { taskId: 'ct-2', version: 3, type: 'NoShowCallback', status: 'InProgress', flags: ['MissedAppointment', 'DeliveryFailure'], title: 'Gọi lại sau lỡ hẹn',
    patient: { id: 2, patientCode: 'BN-MINH-HOA-2', fullName: 'Bệnh nhân minh họa 2' }, branchId: 1, branchName: 'Chi nhánh minh họa',
    owner: null, dueAt: at(2), content: 'Bệnh nhân không đến lịch hẹn.',
    source: { appointment: { id: 'ap-1', code: 'AP-MINH-HOA', startAt: at(-1), status: 'NoShow' } },
    attempts: [{ id: 'a-1', attemptedAt: at(-1, 15), channel: 'Phone', result: 'NoAnswer', notes: 'Không bắt máy', reportedSymptoms: null,
      nextContactAt: at(1, 10), appointmentCode: null, actorName: 'Trợ lý minh họa', recordedAt: at(-1, 15) }],
    escalations: [], allowedActions: ['done', 'cancel', 'recordAttempt', 'rebook', 'escalate'] },
  { taskId: 'ct-3', version: 2, type: 'Aftercare', status: 'Open', flags: ['ClinicallyOverdue'], title: 'Hỏi thăm sau nhổ răng',
    patient: { id: 3, patientCode: 'BN-MINH-HOA-3', fullName: 'Bệnh nhân minh họa 3' }, branchId: 1, branchName: 'Chi nhánh minh họa',
    owner: { id: 4, name: 'Trợ lý minh họa' }, dueAt: at(-1), content: 'Hỏi thăm theo câu hỏi bác sĩ đã duyệt.',
    source: { activity: { id: 'act-1', label: 'Nhổ răng (minh họa)' } },
    aftercare: { requesterDentist: 'Nha sĩ minh họa', sourceVisit: 'Lượt khám (minh họa)', selectedDue: at(-1),
      questions: ['Còn chảy máu không?', 'Mức độ đau từ 0 đến 10?'] },
    attempts: [], escalations: [{ id: 'e-1', clinicianName: 'Nha sĩ minh họa', reason: 'Bệnh nhân báo sưng nhiều', status: 'Pending',
      createdAt: at(-1, 16), acknowledgedAt: null, acknowledgedBy: null, resolvedAt: null, evidence: null }],
    allowedActions: ['done', 'recordAttempt', 'escalate'] },
];
const keys = new Map<string, string>();
const copy = <T,>(v: T): T => JSON.parse(JSON.stringify(v)) as T;
function find(taskId: string) {
  const t = tasks.find(x => x.taskId === taskId);
  if (!t) throw new ApiError(404, 'MSG113', 'Không tìm thấy dữ liệu được yêu cầu. Vui lòng tải lại danh sách.');
  return t;
}
function guard(t: CareTaskDetail, expectedVersion: number, requestKey: string) {
  if (keys.has(requestKey)) return true;
  if (t.version !== expectedVersion) throw new ApiError(409, 'MSG111', 'Dữ liệu đã được cập nhật bởi thao tác khác. Vui lòng tải lại và kiểm tra trước khi xác nhận.');
  keys.set(requestKey, t.taskId);
  return false;
}

export const previewCareApi: CareApi = {
  async options() {
    await previewDelay();
    return { branches: [{ id: 1, name: 'Chi nhánh minh họa' }], owners: [{ id: 4, name: 'Trợ lý minh họa' }],
      clinicians: [{ id: 2, name: 'Nha sĩ minh họa', branchId: 1 }], types: ['FollowUpBooking', 'NoShowCallback', 'Aftercare', 'Other'] };
  },
  async list(q) {
    await previewDelay();
    const rows = tasks.filter(t => (!q.status || t.status === q.status) && (!q.type || t.type === q.type)
      && (!q.patient || `${t.patient.patientCode} ${t.patient.fullName}`.toLowerCase().includes(q.patient.toLowerCase()))
      && (!q.from || t.dueAt.slice(0, 10) >= q.from) && (!q.to || t.dueAt.slice(0, 10) <= q.to));
    return { items: copy(rows.slice((q.page - 1) * q.pageSize, q.page * q.pageSize)), total: rows.length, page: q.page, pageSize: q.pageSize };
  },
  async detail(taskId) { await previewDelay(); return copy(find(taskId)); },
  async recordAttempt(taskId, c) {
    await previewDelay();
    const t = find(taskId);
    if (!guard(t, c.expectedVersion, c.requestKey)) {
      t.attempts.unshift({ id: c.requestKey, attemptedAt: c.attemptedAt, channel: c.channel, result: c.result, notes: c.notes,
        reportedSymptoms: c.reportedSymptoms, nextContactAt: c.nextContactAt, appointmentCode: c.appointmentId ? 'AP-ĐÃ-ĐẶT' : null,
        actorName: 'Bạn (minh họa)', recordedAt: new Date().toISOString() });
      if (c.result === 'Rebooked') t.flags = t.flags.filter(f => f !== 'NeedsBooking');
      if (t.status === 'Open') t.status = 'InProgress';
      t.version += 1;
    }
    return copy(t);
  },
  async changeStatus(taskId, c) {
    await previewDelay();
    const t = find(taskId);
    if (!guard(t, c.expectedVersion, c.requestKey)) { t.status = c.status; t.version += 1;
      t.allowedActions = c.status === 'Done' || c.status === 'Cancelled' ? [] : t.allowedActions; }
    return copy(t);
  },
  async escalate(taskId, c) {
    await previewDelay();
    const t = find(taskId);
    if (!guard(t, c.expectedVersion, c.requestKey)) {
      t.escalations.unshift({ id: c.requestKey, clinicianName: 'Nha sĩ minh họa', reason: c.reason, status: 'Pending',
        createdAt: new Date().toISOString(), acknowledgedAt: null, acknowledgedBy: null, resolvedAt: null, evidence: null });
      t.version += 1;
    }
    return copy(t);
  },
};
