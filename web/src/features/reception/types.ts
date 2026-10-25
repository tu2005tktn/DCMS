import type { AppointmentOptions, AppointmentStatus } from '../appointments/types';

export const purposeLabels: Record<string, string> = {
  InitialExamination: 'Khám mới', TreatmentContinuation: 'Điều trị tiếp',
  PostoperativeOrWarrantyReview: 'Kiểm tra sau điều trị / bảo hành', PeriodicOrNewProblem: 'Tái khám định kỳ / vấn đề khác',
};
export const visitLabels: Record<string, string> = { Queued: 'Chờ khám', InProgress: 'Đang khám', Completed: 'Hoàn tất', Cancelled: 'Đã hủy' };
export type ReceptionOptions = { calendar: AppointmentOptions; canCheckIn: boolean; clinicDate: string; serverNow: string };
export type PageResult<T> = { items: T[]; total: number; page: number; pageSize: number; clinicDate: string; serverNow: string };
export type ReceptionAppointment = {
  appointmentId: string; code: string; branchId: number; branchName: string; dentistId: number; dentistName: string;
  chairId: number; chairName: string; assistantId: number | null; startAt: string; endAt: string;
  status: AppointmentStatus; version: number; patientId: number | null; patientCode: string | null; patientName: string | null;
  bookerName: string; bookerPhone: string | null; bookerEmail: string | null; attendeeName: string; attendeeBirthDate: string | null;
  attendeeRelation: string; visitId: string | null; canCheckIn: boolean; ineligibleReason: string | null;
};
export type VisitResult = {
  visitId: string; patientId: number; patientCode: string; patientName: string; appointmentId: string; appointmentCode: string;
  branchId: number; branchName: string; dentistId: number; dentistName: string; chairId: number; assistantId: number | null;
  appointmentAt: string; arrivalAt: string; recordedAt: string; status: string; appointmentStatus: string;
  visitVersion: number; appointmentVersion: number; purpose: string; note: string | null; arrivalReason: string | null;
  source: { followUpId: string | null; priorCourseId: string | null; label: string | null; blocker: string | null } | null;
  requiresClinicalSourceReview: boolean; waitingMinutes: number; replayed: boolean; openVisitPath: string;
};
export type ReceptionDetail = { appointment: ReceptionAppointment; visit: VisitResult | null; clinicDate: string; serverNow: string; canBackdate: boolean };
export type Course = { id: string; label: string; version: number };
export function arrivalClock(value: string) { return new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }); }
