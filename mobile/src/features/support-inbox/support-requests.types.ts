/**
 * S26 — Staff support inbox DTO (mobile).
 *
 * Cùng shape với `web/src/features/support-inbox/types.ts` và backend `/api/support-requests`.
 * Quyền (`permission`) do server tính theo quyền, chi nhánh và quyền sở hữu; UI chỉ dùng để ẩn/hiện nút.
 */

export type SupportRequestStatus = 'Pending' | 'InReview' | 'Resolved' | 'Rejected' | 'Withdrawn';
export type SupportRequestKind = 'Change' | 'Cancel' | 'ConfirmToAttend' | 'WithdrawAttend' | 'General';
export type AppointmentStatus = 'Pending' | 'Confirmed' | 'Rescheduled' | 'Completed' | 'Cancelled' | 'NoShow';
export type ChangeReason = 'PatientRequest' | 'DentistUnavailable' | 'ChairUnavailable' | 'ScheduleReduced' | 'PatientNoShow' | 'Other';

export interface StaffPermission {
  canResolve: boolean;
  canEdit: boolean;
  branchMatch: boolean;
  ownershipMatch: boolean;
}

export interface AppointmentHistoryEntry {
  at: string;
  actor: string;
  actorUserId?: number;
  action: string;
  notes?: string;
}

export interface AppointmentSummary {
  id: string;
  code: string;
  version: number;
  status: AppointmentStatus;
  startAt: string;
  endAt: string;
  branchId: number;
  branchName: string;
  chairId: number | null;
  dentistId: number;
  dentistName: string;
  serviceId: number;
  serviceName: string;
  durationMinutes: number;
}

export interface SupportRequestDto {
  id: string;
  appointmentId: string;
  appointmentCode: string;
  kind: SupportRequestKind;
  status: SupportRequestStatus;
  expectedVersion: number;
  requestKey: string;
  reason: string | null;
  proposedChange?: { newStartAt?: string; newDentistId?: number; newServiceId?: number; reason: ChangeReason };
  requester: { accountId: number | null; name: string; phone: string | null; isAnonymous: boolean };
  permission: StaffPermission;
  appointmentActionCommitted: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SupportRequestDetail extends SupportRequestDto {
  appointment: AppointmentSummary;
  history: AppointmentHistoryEntry[];
  decisionReason: string | null;
  decidedAt: string | null;
  deciderName: string | null;
}

export type SupportRequestPage = { items: SupportRequestDto[]; total: number; page: number; pageSize: number };

export interface StaffChangeCommand {
  requestKey: string;
  expectedVersion: number;
  reason: ChangeReason;
  notes?: string;
  newStartAt?: string;
}

export interface StaffResolveCommand {
  requestKey: string;
  expectedVersion: number;
  decision: 'Resolve' | 'Reject';
  notes?: string;
  decisionReason?: string;
}

export interface StaffVerifyRequesterCommand {
  requestKey: string;
  expectedVersion: number;
  notes?: string;
}

export type CommandErrorCode = 'FORBIDDEN' | 'NOT_FOUND' | 'STALE_VERSION' | 'BAD_REQUEST' | 'NETWORK' | 'SERVER' | 'UNAUTHORIZED';

export type CommandResult = { ok: true; request: SupportRequestDetail | null } | { ok: false; errorCode: CommandErrorCode; errorMessage: string };

export const statusLabels: Record<SupportRequestStatus, string> = {
  Pending: 'Chờ staff', InReview: 'Đang xem xét', Resolved: 'Đã xử lý', Rejected: 'Bị từ chối', Withdrawn: 'Đã rút',
};

export const kindLabels: Record<SupportRequestKind, string> = {
  Change: 'Yêu cầu đổi lịch', Cancel: 'Yêu cầu huỷ lịch', ConfirmToAttend: 'Xác nhận tham dự',
  WithdrawAttend: 'Rút xác nhận tham dự', General: 'Hỏi chung',
};

export const reasonLabels: Record<ChangeReason, string> = {
  PatientRequest: 'Bệnh nhân yêu cầu', DentistUnavailable: 'Nha sĩ bận', ChairUnavailable: 'Ghế bận',
  ScheduleReduced: 'Giảm giờ làm', PatientNoShow: 'Không đến', Other: 'Khác',
};

export const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  Pending: 'Chờ xác minh', Confirmed: 'Đã xác minh', Rescheduled: 'Đã đổi lịch',
  Completed: 'Đã khám', Cancelled: 'Đã huỷ', NoShow: 'Không đến',
};
