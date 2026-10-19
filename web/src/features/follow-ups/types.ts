import type { SystemMessage } from '../../services/api';

// Contract: docs/specs/S24-FOLLOW-UP-INSTRUCTION-WEB.md (DTO từ backend DCMS-97 — Vinh).
// JSON camelCase; mọi trạng thái hiển thị lấy từ server, không suy từ state local.
export type FollowUpPurpose =
  | 'TreatmentContinuation' | 'PostoperativeReview' | 'WarrantyReview' | 'PeriodicRecall' | 'NewProblem';
export type FollowUpStatus = 'NeedsBooking' | 'Booked' | 'Completed' | 'Cancelled' | 'Superseded';
export type FollowUpSource = {
  dossierId: string | null; itemId: string | null; stepId: string | null;
  visitId: string | null; activityId: string | null;
};
export type FollowUpInstruction = {
  id: string; patientId: number; branchId: number; source: FollowUpSource;
  purpose: FollowUpPurpose; reason: string; guidance: string | null;
  earliestOn: string; targetOn: string; latestOn: string;
  durationMinutes: number; dentistId: number | null; capability: string | null;
  status: FollowUpStatus; overdue: boolean; activeAppointmentId: string | null;
  version: number; allowedActions: string[];
};
export type FollowUpChange = { instruction: FollowUpInstruction; notification: SystemMessage };

// Bối cảnh gửi sang S11 (BookingDrawer) — không tự chọn slot, không đổi trạng thái local.
export type FollowUpHandoff = {
  instructionId: string; version: number; patientId: number; patientLabel: string;
  branchId: number; dentistId: number | null; durationMinutes: number;
  earliestOn: string; targetOn: string; latestOn: string;
};

export const purposeLabels: Record<FollowUpPurpose, string> = {
  TreatmentContinuation: 'Tiếp tục điều trị',
  PostoperativeReview: 'Tái khám sau điều trị',
  WarrantyReview: 'Kiểm tra bảo hành',
  PeriodicRecall: 'Tái khám định kỳ',
  NewProblem: 'Vấn đề mới',
};
export const statusLabels: Record<FollowUpStatus, string> = {
  NeedsBooking: 'Chưa đặt lịch', Booked: 'Đã đặt lịch',
  Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Superseded: 'Đã thay thế',
};
export const statusColors: Record<FollowUpStatus, string> = {
  NeedsBooking: 'orange', Booked: 'green', Completed: 'blue', Cancelled: 'red', Superseded: 'default',
};

// Hợp đồng staff: 15–480 phút, bội số 15 (reservedMinutes tường minh, không suy từ estimated).
export const durationOptions = Array.from({ length: 32 }, (_, index) => ({
  value: (index + 1) * 15, label: `${(index + 1) * 15} phút`,
}));
