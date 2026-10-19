// Contract: docs/implementation/S25-S60-CARE-TASKS-UI.md §3 ([PROPOSED] for DCMS-99).
export type CareTaskStatus = 'Open' | 'InProgress' | 'Done' | 'Cancelled';
export type CareTaskType = 'FollowUpBooking' | 'NoShowCallback' | 'Aftercare' | 'Other';
export type CareFlag = 'NeedsBooking' | 'ScheduledBlocked' | 'MissedAppointment' | 'ClinicallyOverdue' | 'DeliveryFailure';
export type ContactChannel = 'Phone' | 'Zalo' | 'InPerson' | 'Other';
export type ContactResult = 'NotContacted' | 'NoAnswer' | 'Contacted' | 'Rebooked' | 'NoLongerInterested';
export type EscalationStatus = 'Pending' | 'Acknowledged' | 'Resolved';
export type CareAction = 'start' | 'done' | 'cancel' | 'recordAttempt' | 'rebook' | 'escalate';

export const statusLabels: Record<CareTaskStatus, string> = { Open: 'Mở', InProgress: 'Đang xử lý', Done: 'Đã xong', Cancelled: 'Đã hủy' };
export const statusColors: Record<CareTaskStatus, string> = { Open: 'blue', InProgress: 'geekblue', Done: 'green', Cancelled: 'default' };
export const typeLabels: Record<CareTaskType, string> = {
  FollowUpBooking: 'Hẹn tái khám', NoShowCallback: 'Gọi lại sau vắng mặt', Aftercare: 'Chăm sóc sau điều trị', Other: 'Khác',
};
// Each flag is shown on its own; they are never merged into a single "not done".
export const flagLabels: Record<CareFlag, string> = {
  NeedsBooking: 'Cần đặt lịch', ScheduledBlocked: 'Đã hẹn nhưng bị chặn', MissedAppointment: 'Lỡ hẹn',
  ClinicallyOverdue: 'Quá hạn lâm sàng', DeliveryFailure: 'Gửi nhắc thất bại',
};
export const flagColors: Record<CareFlag, string> = {
  NeedsBooking: 'gold', ScheduledBlocked: 'orange', MissedAppointment: 'volcano', ClinicallyOverdue: 'red', DeliveryFailure: 'magenta',
};
export const channelLabels: Record<ContactChannel, string> = { Phone: 'Điện thoại', Zalo: 'Zalo', InPerson: 'Trực tiếp', Other: 'Khác' };
export const resultLabels: Record<ContactResult, string> = {
  NotContacted: 'Chưa liên hệ được', NoAnswer: 'Không trả lời', Contacted: 'Đã liên hệ', Rebooked: 'Đã đặt lại lịch',
  NoLongerInterested: 'Không còn nhu cầu',
};
export const escalationLabels: Record<EscalationStatus, string> = { Pending: 'Đang chờ bác sĩ', Acknowledged: 'Bác sĩ đã tiếp nhận', Resolved: 'Đã xử lý' };
export const escalationColors: Record<EscalationStatus, string> = { Pending: 'gold', Acknowledged: 'blue', Resolved: 'green' };

export type CareTaskItem = {
  taskId: string; version: number; type: CareTaskType; status: CareTaskStatus; flags: CareFlag[]; title: string;
  patient: { id: number; patientCode: string; fullName: string; birthDate?: string | null };
  branchId: number; branchName: string; owner: { id: number; name: string } | null; dueAt: string;
};
export type CareTaskPage = { items: CareTaskItem[]; total: number; page: number; pageSize: number };
export type ContactAttempt = {
  id: string; attemptedAt: string; channel: ContactChannel; result: ContactResult; notes: string | null; reportedSymptoms: string | null;
  nextContactAt: string | null; appointmentCode: string | null; actorName: string; recordedAt: string;
};
export type Escalation = {
  id: string; clinicianName: string; reason: string; status: EscalationStatus; createdAt: string;
  acknowledgedAt: string | null; acknowledgedBy: string | null; resolvedAt: string | null; evidence: string | null;
};
export type CareTaskDetail = CareTaskItem & {
  content: string;
  source: {
    instruction?: { id: string; label: string; status: string } | null; activity?: { id: string; label: string } | null;
    appointment?: { id: string; code: string; startAt: string; status: string } | null;
  };
  aftercare?: { requesterDentist: string; sourceVisit: string; selectedDue: string; questions: string[] } | null;
  attempts: ContactAttempt[]; escalations: Escalation[]; allowedActions: CareAction[];
};
export type CareOptions = {
  branches: { id: number; name: string }[]; owners: { id: number; name: string }[];
  clinicians: { id: number; name: string; branchId: number }[]; types: CareTaskType[];
};
export type CareQuery = {
  from?: string; to?: string; ownerId?: number; patient?: string; type?: CareTaskType; status?: CareTaskStatus; branchId?: number;
  page: number; pageSize: number;
};
export type AttemptCommand = {
  expectedVersion: number; requestKey: string; attemptedAt: string; channel: ContactChannel; result: ContactResult;
  notes: string | null; reportedSymptoms: string | null; nextContactAt: string | null; appointmentId: string | null;
};
export type StatusCommand = { expectedVersion: number; requestKey: string; status: CareTaskStatus; reason: string | null };
export type EscalationCommand = { expectedVersion: number; requestKey: string; clinicianId: number; reason: string };

export const formatStamp = (iso: string) => new Date(iso).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' });
export const formatDay = (iso: string) => new Date(iso).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
export function newRequestKey() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.floor(Math.random() * 16); return (c === 'x' ? r : (r & 3) | 8).toString(16); });
}
