import type { SystemMessage } from '../../services/api';

// Contract: docs/implementation/UC04-05-S10-S11.md §3.
export type AppointmentStatus = 'Confirmed' | 'CheckedIn' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow';
export const statusLabels: Record<AppointmentStatus, string> = {
  Confirmed: 'Đã xác nhận', CheckedIn: 'Đã tiếp nhận', InProgress: 'Đang khám', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', NoShow: 'Vắng mặt',
};
export const statusColors: Record<AppointmentStatus, string> = {
  Confirmed: 'green', CheckedIn: 'blue', InProgress: 'geekblue', Completed: 'default', Cancelled: 'red', NoShow: 'orange',
};
export const kindLabels: Record<string, string> = { Standard: 'Thường', WalkIn: 'Vãng lai', FollowUp: 'Tái khám' };
export const sourceLabels: Record<string, string> = { Phone: 'Điện thoại', Zalo: 'Zalo', Facebook: 'Facebook', InPerson: 'Trực tiếp', App: 'Ứng dụng' };

export type CalendarOption = { id: number; name: string };
export type CalendarDentist = { id: number; branchId: number; name: string };
export type CalendarChair = { id: number; branchId: number; name: string };
export type CalendarService = { id: number; branchId: number; name: string; publicDurationMinutes: number };
export type AppointmentOptions = {
  branches: CalendarOption[]; dentists: CalendarDentist[]; chairs: CalendarChair[]; services: CalendarService[]; ownScheduleOnly: boolean;
};
export type AppointmentListItem = {
  id: string; code: string; branchId: number; branchName: string; startAt: string; endAt: string; durationMinutes: number;
  dentistId: number; dentistName: string; chairId: number; chairName: string; serviceName: string | null; attendeeName: string;
  attendeeRelation: string; kind: string; source: string; status: AppointmentStatus; version: number;
};
export type CalendarPeriod = { startMinute: number; endMinute: number; kind: 'Work' | 'Break' | 'Block'; reason: string | null };
export type CalendarDay = { dentistId: number; date: string; periods: CalendarPeriod[] };
export type CalendarView = { branchId: number; from: string; to: string; appointments: AppointmentListItem[]; days: CalendarDay[]; ownScheduleOnly: boolean };
export type StaffAppointmentDetail = {
  appointment: AppointmentListItem; bookerName: string; bookerPhoneMasked: string | null; bookerEmailMasked: string | null;
  bookedFromApp: boolean; reason: string | null; exceptionReason: string | null; createdAt: string;
};
export type AvailabilitySlot = {
  start: string; end: string; chairId: number; dentistId: number; assistantId: number | null; scheduleVersion: number; assistantScheduleVersion: number | null; bookingSettingsVersion?: number;
};
export type AppointmentResult = { appointmentId: string; code: string; version: number; status: string; startAt: string; endAt: string; notification: SystemMessage };

// Clinic time is UTC+7 without daylight saving; computed manually so the browser time zone does not matter.
const OFFSET = 7 * 3_600_000;
const pad = (n: number) => String(n).padStart(2, '0');
export const clinicToday = () => new Date(Date.now() + OFFSET).toISOString().slice(0, 10);
export const addDays = (date: string, days: number) => new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10);
export const mondayOf = (date: string) => addDays(date, -((new Date(`${date}T00:00:00Z`).getUTCDay() + 6) % 7));
export const localDate = (iso: string) => new Date(Date.parse(iso) + OFFSET).toISOString().slice(0, 10);
export const localMinute = (iso: string) => { const d = new Date(Date.parse(iso) + OFFSET); return d.getUTCHours() * 60 + d.getUTCMinutes(); };
export const clock = (minute: number) => `${pad(Math.floor(minute / 60) % 24)}:${pad(minute % 60)}`;
export const formatClock = (iso: string) => clock(localMinute(iso));
export const formatDate = (date: string) => date.split('-').reverse().join('/');
export const toInstant = (date: string, minute: number) => new Date(Date.parse(`${date}T00:00:00Z`) - OFFSET + minute * 60_000).toISOString();
const weekdays = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
export const weekday = (date: string) => weekdays[new Date(`${date}T00:00:00Z`).getUTCDay()];

export function newRequestKey() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = Math.floor(Math.random() * 16); return (c === 'x' ? r : (r & 3) | 8).toString(16); });
}
