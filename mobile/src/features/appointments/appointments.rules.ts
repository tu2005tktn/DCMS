import type { AppointmentStatus, IneligibleReason, SupportRequestKind, SupportRequestStatus } from './appointments.types';

// Clinic time is Asia/Ho_Chi_Minh (UTC+7, no daylight saving); computed manually so it does not depend on Intl time zones.
const OFFSET_MS = 7 * 3_600_000;
const pad = (value: number) => String(value).padStart(2, '0');
const local = (iso: string) => new Date(new Date(iso).getTime() + OFFSET_MS);

export const formatDate = (iso: string) => { const d = local(iso); return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`; };
export const formatClock = (iso: string) => { const d = local(iso); return `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`; };
export const formatDateTime = (iso: string | null) => iso ? `${formatClock(iso)} ${formatDate(iso)}` : '—';
const weekdays = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'];
export const formatWeekday = (iso: string) => weekdays[local(iso).getUTCDay()];

// Clinic-local calendar date "yyyy-MM-dd" for an instant, and the instant at local midnight of such a date.
export const clinicDate = (instant: Date) => new Date(instant.getTime() + OFFSET_MS).toISOString().slice(0, 10);
export const clinicMidnight = (date: string) => new Date(Date.parse(`${date}T00:00:00Z`) - OFFSET_MS);
export const addDays = (date: string, days: number) => new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10);

export const statusLabels: Record<AppointmentStatus, string> = {
  Confirmed: 'Đã xác nhận', CheckedIn: 'Đã tiếp nhận', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', NoShow: 'Vắng mặt',
};
export const statusColors: Record<AppointmentStatus, [string, string]> = {
  Confirmed: ['#067647', '#ECFDF3'], CheckedIn: ['#175CD3', '#EFF8FF'], InProgress: ['#175CD3', '#EFF8FF'],
  Completed: ['#344054', '#F2F4F7'], Cancelled: ['#B42318', '#FEF3F2'], NoShow: ['#B54708', '#FFFAEB'],
};
export const supportStatusLabels: Record<SupportRequestStatus, string> = {
  Pending: 'Đang chờ xử lý', Resolved: 'Đã xử lý', Rejected: 'Không chấp thuận', Withdrawn: 'Đã rút',
};
export const supportKindLabels: Record<SupportRequestKind, string> = { RESCHEDULE: 'Đổi lịch', CANCEL: 'Hủy lịch', OTHER: 'Khác' };
export const ineligibleLabels: Record<IneligibleReason, string> = {
  WITHIN_12H: 'Còn dưới 12 giờ trước giờ hẹn nên không thể tự đổi hoặc hủy.',
  NOT_CONFIRMED: 'Lịch hẹn không còn ở trạng thái Đã xác nhận.',
  STARTED: 'Lịch hẹn đã bắt đầu hoặc đã được tiếp nhận.',
  NOT_OWNER: 'Tài khoản này không có quyền tự đổi hoặc hủy lịch hẹn.',
};

// GBR-012: exactly 12 hours still qualifies. Display only; the server decides on submission.
export const SELF_SERVICE_HOURS = 12;
export const hoursUntil = (iso: string, now = Date.now()) => (Date.parse(iso) - now) / 3_600_000;
export function remainingLabel(hours: number) {
  if (hours <= 0) return 'Đã đến giờ hẹn';
  const total = Math.floor(hours * 60);
  const days = Math.floor(total / 1440);
  const h = Math.floor((total % 1440) / 60);
  const m = total % 60;
  return ['Còn', days && `${days} ngày`, (days || h) && `${h} giờ`, !days && `${m} phút`].filter(Boolean).join(' ');
}

// DD-MM-YYYY input → yyyy-MM-dd, null when empty, undefined when invalid.
export function parseInputDate(value: string): string | null | undefined {
  const text = value.trim();
  if (!text) return null;
  const match = /^(\d{2})-(\d{2})-(\d{4})$/.exec(text);
  if (!match) return undefined;
  const iso = `${match[3]}-${match[2]}-${match[1]}`;
  const date = new Date(`${iso}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === iso ? iso : undefined;
}
export function formatDateInput(value: string) {
  const digits = value.replace(/\D/g, '').slice(0, 8);
  return [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4)].filter(Boolean).join('-');
}

// Reason 1–500 characters for reschedule, cancel and support requests (S16 #6).
export const REASON_MAX = 500;
export function reasonError(value: string) {
  const text = value.trim();
  if (!text) return 'Vui lòng nhập Lý do.';
  if (text.length > REASON_MAX) return `Lý do không được vượt quá ${REASON_MAX} ký tự.`;
  return undefined;
}

// One key per confirmed command so a retry after a lost response is not applied twice.
export function newRequestKey() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.floor(Math.random() * 16);
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}
