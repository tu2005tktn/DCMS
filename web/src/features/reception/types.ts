import { ApiError } from '../../services/api';
import type { AppointmentOptions } from '../appointments/types';

export const purposeLabels: Record<string, string> = { InitialExamination: 'Khám lần đầu', TreatmentContinuation: 'Tiếp tục điều trị',
  PostoperativeOrWarrantyReview: 'Kiểm tra sau điều trị / bảo hành', PeriodicOrNewProblem: 'Khám định kỳ / vấn đề mới' };
export const visitLabels: Record<string, string> = { Queued: 'Chờ khám', InProgress: 'Đang khám', Completed: 'Hoàn tất', Cancelled: 'Đã hủy' };
export type VisitSource = { dossierId: string | null; itemId: string | null; stepId: string | null; activityId: string | null };
export const emptySource: VisitSource = { dossierId: null, itemId: null, stepId: null, activityId: null };
export type ReceptionOptions = { calendar: AppointmentOptions; canCheckIn: boolean; canReorder: boolean; canDecidePriority: boolean };
export type ReceptionAppointment = { appointmentId: string; code: string; branchId: number; startAt: string; endAt: string; status: string;
  version: number; dentistId: number; dentistName: string; patientId: number | null; patientCode: string | null; bookerName: string;
  bookerPhone: string | null; bookerEmail: string | null; attendeeName: string; attendeeBirthDate: string | null; attendeeRelation: string; noShowEligible: boolean };
export type Visit = { appointmentId: string; appointmentCode: string; appointmentStatus: string; appointmentVersion: number; visitId: string;
  visitCode: string; visitStatus: string; version: number; branchId: number; patientId: number; patientCode: string; patientName: string;
  dentistId: number; dentistName: string; appointmentAt: string; actualArrivalAt: string; enteredAt: string; purpose: string; source: VisitSource;
  sourceReviewPending: boolean; blockers: string[]; queue: { position: number; order: number; waitingMinutes: number };
  clinicalPriority: string | null; priorityDentistId: number | null; additionalConcern: string | null; reviewReason: string | null; note: string | null; arrivalReason: string | null; sourceLinks: SourceOption[] };
export type ReceptionView = { date: string; timeZone: string; serverNow: string; notArrived: ReceptionAppointment[]; queue: Visit[] };
export type ReceptionDetail = { appointment: ReceptionAppointment; visit: Visit | null };
export type SourceOption = { id: string; kind: 'Dossier' | 'Item' | 'Step' | 'Activity'; parentId: string | null; label: string; blocker: string | null; version: number };
export type VisitHistory = { id: string; action: string; actorAccountId: number; actorName: string; decidingDentistId: number | null; reason: string; occurredAt: string };
export const clinicTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour12: false });
export const clinicInput = (date = new Date()) => new Date(date.getTime() + 7 * 3600_000).toISOString().slice(0, 16);
export const fromClinicInput = (value: string) => new Date(`${value}:00+07:00`).toISOString();
export function failureText(error: unknown) {
  if (!(error instanceof ApiError)) return (error as Error).message;
  const prefix = error.status === 0 ? 'Mất kết nối' : error.status === 401 ? 'Phiên đăng nhập đã hết hạn' : error.status === 403 ? 'Thiếu quyền'
    : error.status === 409 ? 'Dữ liệu đã thay đổi' : error.status >= 500 ? 'Chưa thể lưu' : 'Kiểm tra thông tin';
  return `${prefix} · ${error.code}: ${error.message}`;
}
