/**
 * S26 — Staff Support Inbox (Web).
 *
 * DTO bám sát `mobile/src/features/appointments/appointment.types.ts` để cả hai phía
 * (mobile staff + web staff) dùng chung hợp đồng khi backend TV5 sẵn sàng. Khi chuyển
 * sang chia sẻ package, các type này sẽ được thay bằng re-export từ shared.
 *
 * Mock DTO chung theo chuẩn bàn giao "Dùng mock DTO chung để làm song song, không đợi
 * task khác hoàn tất mới bắt đầu UI".
 *
 * Traceability:
 *   - S26: hộp thư staff, command TV4/TV5, lịch sử, conflict version.
 *   - UC-05, UC-06, UC-34 (booking portion).
 *   - GBR-007/012-015/031/073/075/077/078/081/123.
 *   - MSG31-38/94/111/144, AT16.
 */

// ============================================================================
// Domain enums
// ============================================================================

/** Trạng thái support request — tách biệt khỏi AppointmentStatus (GBR-007). */
export type SupportRequestStatus =
  | 'Pending'        // chờ staff xử lý
  | 'InReview'       // staff đang xử lý
  | 'Resolved'       // staff resolve sau khi booking action đã commit
  | 'Rejected'       // staff từ chối
  | 'Withdrawn';     // requester tự rút (sau đó KHÔNG được resolve lại — GBR-075)

/** Loại request — staff command TV4 dùng. */
export type SupportRequestKind =
  | 'Change'           // đổi lịch
  | 'Cancel'           // huỷ lịch
  | 'ConfirmToAttend'  // xác nhận tham dự
  | 'WithdrawAttend'   // rút xác nhận tham dự
  | 'General';         // hỏi chung

/** Trạng thái appointment (chỉ để hiển thị badge trên row). */
export type AppointmentStatus =
  | 'Pending' | 'Confirmed' | 'Rescheduled' | 'Completed' | 'Cancelled' | 'NoShow';

/** Lý do đổi/huỷ. */
export type ChangeReason =
  | 'PatientRequest' | 'DentistUnavailable' | 'ChairUnavailable'
  | 'ScheduleReduced' | 'PatientNoShow' | 'Other';

/** Vai trò người tham dự. */
export type AttendeeRelation = 'Self' | 'Relative';

// ============================================================================
// Labels & colors (dùng cho tag Ant Design)
// ============================================================================

export const statusLabels: Record<SupportRequestStatus, string> = {
  Pending: 'Chờ staff',
  InReview: 'Đang xem xét',
  Resolved: 'Đã xử lý',
  Rejected: 'Bị từ chối',
  Withdrawn: 'Đã rút',
};

export const statusColors: Record<SupportRequestStatus, string> = {
  Pending: 'gold',
  InReview: 'blue',
  Resolved: 'green',
  Rejected: 'red',
  Withdrawn: 'default',
};

export const kindLabels: Record<SupportRequestKind, string> = {
  Change: 'Yêu cầu đổi lịch',
  Cancel: 'Yêu cầu huỷ lịch',
  ConfirmToAttend: 'Xác nhận tham dự',
  WithdrawAttend: 'Rút xác nhận tham dự',
  General: 'Hỏi chung',
};

export const reasonLabels: Record<ChangeReason, string> = {
  PatientRequest: 'Bệnh nhân yêu cầu',
  DentistUnavailable: 'Nha sĩ bận',
  ChairUnavailable: 'Ghế bận',
  ScheduleReduced: 'Giảm giờ làm',
  PatientNoShow: 'Không đến',
  Other: 'Khác',
};

export const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  Pending: 'Chờ xác minh',
  Confirmed: 'Đã xác minh',
  Rescheduled: 'Đã đổi lịch',
  Completed: 'Đã khám',
  Cancelled: 'Đã huỷ',
  NoShow: 'Không đến',
};

// ============================================================================
// DTOs
// ============================================================================

/** Quyền của staff với request (server trả; mock trả `true`). */
export interface StaffPermission {
  canResolve: boolean;
  canEdit: boolean;
  branchMatch: boolean;
  ownershipMatch: boolean;
}

/** Một dòng lịch sử thay đổi của appointment. */
export interface AppointmentHistoryEntry {
  at: string;
  actor: 'System' | 'Staff' | 'Patient' | 'Booker';
  actorUserId?: number;
  action:
    | 'Created' | 'Rescheduled' | 'Cancelled' | 'Completed' | 'NoShow'
    | 'ConfirmedToAttend' | 'RequestSubmitted' | 'RequestResolved' | 'RequestRejected'
    | 'RequestWithdrawn' | 'ScheduleReduced';
  before?: { startAt?: string; status?: AppointmentStatus };
  after?: { startAt?: string; status?: AppointmentStatus; reason?: ChangeReason };
  notes?: string;
}

/** Appointment rút gọn — gắn với request. */
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
  attendee: {
    patientId: number | null;
    name: string;
    birthDate: string | null;
    relation: AttendeeRelation;
  };
  booker: {
    accountId: number | null;
    name: string;
    phone: string | null;
    isAnonymous: boolean;
  };
  confirmedToAttend: 'Pending' | 'Yes' | 'No';
}

/** Support request — UI list/detail. */
export interface SupportRequestDto {
  id: string;                    // "SR-000123"
  appointmentId: string;
  appointmentCode: string;
  kind: SupportRequestKind;
  status: SupportRequestStatus;
  expectedVersion: number;       // conflict check khi resolve (GBR-014)
  requestKey: string;            // idempotency
  reason: string | null;         // lý do bệnh nhân
  proposedChange?: {
    newStartAt?: string;
    newDentistId?: number;
    newServiceId?: number;
    reason: ChangeReason;
  };
  requester: {
    accountId: number | null;
    name: string;
    phone: string | null;
    isAnonymous: boolean;
  };
  permission: StaffPermission;
  createdAt: string;
  updatedAt: string;
}

/** Support request + appointment chi tiết (cho drawer). */
export interface SupportRequestDetail extends SupportRequestDto {
  appointment: AppointmentSummary;
  history: AppointmentHistoryEntry[];
  decisionReason: string | null;
  decidedAt: string | null;
  deciderName: string | null;
}

// ============================================================================
// Page envelope
// ============================================================================

export type SupportRequestPage = {
  items: SupportRequestDto[];
  total: number;
  page: number;
  pageSize: number;
};

// ============================================================================
// Commands (TV4 + TV5) — bám sát mobile
// ============================================================================

/** Command chung cho change/cancel. */
export interface StaffChangeCommand {
  requestKey: string;
  expectedVersion: number;
  reason: ChangeReason;
  notes?: string;
  newStartAt?: string;
  newDentistId?: number;
  newServiceId?: number;
}

export interface StaffChangeResult {
  ok: boolean;
  appointment?: AppointmentSummary;
  notificationCode?: string;   // MSG37/94/144
  errorCode?: 'CONFLICT' | 'FORBIDDEN' | 'NOT_FOUND' | 'BAD_REQUEST' | 'STALE_VERSION' | 'NETWORK' | 'SERVER';
  errorMessage?: string;
}

/** Command resolve/reject. */
export interface StaffResolveCommand {
  requestKey: string;
  expectedVersion: number;
  decision: 'Resolve' | 'Reject';
  notes?: string;
  decisionReason?: string;     // bắt buộc khi Reject
}

export interface StaffResolveResult {
  ok: boolean;
  request?: SupportRequestDetail;
  notificationCode?: string;
  errorCode?: 'CONFLICT' | 'FORBIDDEN' | 'NOT_FOUND' | 'STALE_VERSION' | 'BAD_REQUEST' | 'NETWORK' | 'SERVER';
  errorMessage?: string;
}

/** Command xác minh requester. */
export interface StaffVerifyRequesterCommand {
  requestKey: string;
  expectedVersion: number;
  notes?: string;
}

// ============================================================================
// Filters
// ============================================================================

export type StatusFilterGroup = 'open' | 'pending' | 'inReview' | 'closed';

export const statusFilterOptions: { value: StatusFilterGroup; label: string; statuses: SupportRequestStatus[] }[] = [
  { value: 'open', label: 'Cần xử lý', statuses: ['Pending', 'InReview'] },
  { value: 'pending', label: 'Chờ staff', statuses: ['Pending'] },
  { value: 'inReview', label: 'Đang xem xét', statuses: ['InReview'] },
  { value: 'closed', label: 'Đã đóng', statuses: ['Resolved', 'Rejected', 'Withdrawn'] },
];

// ============================================================================
// Helpers
// ============================================================================

export const formatDateTime = (value: string | null | undefined): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(date);
};

export const formatDate = (value: string | null | undefined): string => {
  if (!value) return '—';
  // Giữ nguyên YYYY-MM-DD; không chuyển timezone vì đây là local date.
  return value.split('-').reverse().join('/');
};

export const formatTime = (value: string | null | undefined): string => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(date);
};
