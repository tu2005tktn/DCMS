/**
 * Mock DTO chung cho S12 (Appointment lifecycle + support request) và S26 (Notification).
 *
 * Theo chuẩn bàn giao "Dùng mock DTO chung để làm song song, không đợi task khác hoàn thành
 * mới bắt đầu UI" — file này định nghĩa DTO mà cả UI TV5 và backend (TV2/TV4) sẽ dùng.
 *
 * Khi backend triển khai API thật, các DTO này được giữ nguyên shape để UI không phải đổi.
 * UI đọc qua `appointmentMockRepo` / `notificationMockRepo`; việc swap sang HTTP thật chỉ
 * cần đổi implementation bên trong repo (xem `*.mock.ts`).
 *
 * Traceability:
 *   - S12: appointment lifecycle, support request, schedule ảnh hưởng, change/cancel với lý do.
 *   - S26: notification list của account hiện tại, deep link có kiểm quyền lại.
 *   - UC-05 (đặt lịch Public), UC-06 (đổi/hủy) — booking portion UC-34.
 *   - GBR-007/012-015/031/073/075/077/078/081/123, MSG31-38/94/111/144, AT16.
 */

import type { BookingResult } from '@/features/appointments/booking.types';

// ============================================================================
// S12 — Appointment lifecycle
// ============================================================================

/** Trạng thái lịch hẹn. Tách biệt với support request (GBR-007) và ConfirmedToAttend (GBR-013). */
export type AppointmentStatus =
  | 'Pending'        // Vừa tạo, chờ staff xác minh requester
  | 'Confirmed'      // Đã xác minh, sẵn sàng diễn ra
  | 'Rescheduled'    // Đã đổi sang khung khác (cũ vẫn trong history)
  | 'Completed'      // Đã khám xong
  | 'Cancelled'      // Đã huỷ
  | 'NoShow';        // Khách không đến

/** Cờ xác nhận tham dự — độc lập với Appointment lifecycle (GBR-013/077). */
export type ConfirmedToAttend = 'Pending' | 'Yes' | 'No';

/** Phân loại vai trò trong một appointment (booker vs attendee). */
export type AttendeeRole = 'Booker' | 'Attendee' | 'Both';
/** Quan hệ người đặt với người khám. */
export type AttendeeRelation = 'Self' | 'Relative';

/** Nguồn tạo lịch — phân biệt account vs anonymous (để xử lý notification khác nhau). */
export type BookingSource = 'App' | 'WalkIn' | 'Phone' | 'System';

/** Lý do đổi/huỷ. */
export type ChangeReason =
  | 'PatientRequest'    // Bệnh nhân yêu cầu
  | 'DentistUnavailable'// Nha sĩ bận
  | 'ChairUnavailable'  // Ghế bận
  | 'ScheduleReduced'   // Giảm giờ làm (impacted appointments)
  | 'PatientNoShow'     // Không đến
  | 'Other';

/** Một dòng trong lịch sử thay đổi. */
export interface AppointmentHistoryEntry {
  at: string;                 // ISO 8601
  actor: 'System' | 'Staff' | 'Patient' | 'Booker';
  actorUserId?: number;       // null với System
  action:
    | 'Created' | 'Rescheduled' | 'Cancelled' | 'Completed' | 'NoShow'
    | 'ConfirmedToAttend' | 'RequestSubmitted' | 'RequestResolved' | 'RequestRejected'
    | 'RequestWithdrawn' | 'ScheduleReduced';
  before?: { startAt?: string; status?: AppointmentStatus };
  after?: { startAt?: string; status?: AppointmentStatus; reason?: ChangeReason };
  notes?: string;
}

/** Appointment — domain object. Mở rộng từ BookingResult để cover cả lifecycle. */
export interface AppointmentDto {
  id: string;                    // "AP-20261016-001"
  code: string;                  // mã ngắn hiển thị
  version: number;               // tăng mỗi mutation
  status: AppointmentStatus;
  startAt: string;               // ISO
  endAt: string;                 // ISO
  branchId: number;
  branchName: string;
  chairId: number | null;
  dentistId: number;
  dentistName: string;
  assistantId: number | null;
  serviceId: number;
  serviceName: string;
  durationMinutes: number;
  booker: {
    accountId: number | null;     // null nếu anonymous booking
    name: string;
    phone: string | null;
    email: string | null;
    isAnonymous: boolean;
  };
  attendee: {
    patientId: number | null;
    name: string;
    birthDate: string | null;
  };
  attendeeRelation: AttendeeRelation;
  source: BookingSource;
  reason: string | null;
  confirmedToAttend: ConfirmedToAttend;
  history: AppointmentHistoryEntry[];
}

// ============================================================================
// S12 — Support request (UC-05/06 phần hỗ trợ)
// ============================================================================

/** Trạng thái request — TÁCH BIỆT khỏi AppointmentStatus (GBR-007). */
export type SupportRequestStatus =
  | 'Pending'        // chờ staff xử lý
  | 'InReview'       // staff đang xử lý
  | 'Resolved'       // staff resolve sau khi booking action đã commit
  | 'Rejected'       // staff từ chối
  | 'Withdrawn';     // requester tự rút (sau đó KHÔNG được resolve lại — GBR-075)

/** Loại request — staff command TV4 dùng. */
export type SupportRequestKind =
  | 'Change'         // đổi lịch
  | 'Cancel'         // huỷ lịch
  | 'ConfirmToAttend'// xác nhận tham dự
  | 'WithdrawAttend' // rút xác nhận tham dự
  | 'General';       // hỏi chung

/** Quyền của staff với request. */
export interface StaffPermission {
  canResolve: boolean;        // Resolve/Reject command
  canEdit: boolean;           // đổi appointment trực tiếp
  branchMatch: boolean;       // staff thuộc branch của appointment
  ownershipMatch: boolean;    // staff là owner / được phân công
}

export interface SupportRequestDto {
  id: string;                  // "SR-000123"
  appointmentId: string;
  appointmentCode: string;     // copy để hiển thị
  kind: SupportRequestKind;
  status: SupportRequestStatus;
  expectedVersion: number;     // server check version khi resolve (GBR-014)
  requestKey: string;          // idempotency key
  reason: string | null;       // lý do bệnh nhân
  proposedChange?: {
    newStartAt?: string;
    newDentistId?: number;
    newServiceId?: number;
    reason: ChangeReason;
  };
  requester: {
    accountId: number | null;  // null nếu walk-in
    name: string;
    phone: string | null;
    isAnonymous: boolean;
  };
  history: AppointmentHistoryEntry[];  // dùng chung type
  createdAt: string;
  updatedAt: string;
}

// ============================================================================
// S12 — Staff commands (TV4)
// ============================================================================

/** Command chung cho change/cancel. */
export interface StaffChangeCommand {
  requestKey: string;          // idempotency (GBR-073)
  expectedVersion: number;     // conflict check (GBR-014)
  reason: ChangeReason;
  notes?: string;
}

export interface StaffChangeResult {
  ok: boolean;                 // mutation thành công thật sự
  appointment?: AppointmentDto;
  notificationCode?: string;   // MSG37/94/144
  errorCode?: 'CONFLICT' | 'FORBIDDEN' | 'NOT_FOUND' | 'BAD_REQUEST' | 'NETWORK';
  errorMessage?: string;
}

/** Command cho resolve/reject support request. */
export interface StaffResolveCommand {
  requestKey: string;
  expectedVersion: number;
  decision: 'Resolve' | 'Reject';
  notes?: string;
}

export interface StaffResolveResult {
  ok: boolean;
  request?: SupportRequestDto;
  appointment?: AppointmentDto;  // nếu Resolved sau khi booking action đã commit
  errorCode?: 'CONFLICT' | 'FORBIDDEN' | 'NOT_FOUND' | 'STALE_VERSION' | 'NETWORK' | 'BAD_REQUEST';
  errorMessage?: string;
}

/** Command xác minh requester (TV4). */
export interface StaffVerifyRequesterCommand {
  requestKey: string;
  expectedVersion: number;
  notes?: string;
}

// ============================================================================
// S12 — Schedule bị ảnh hưởng (khi staff giảm giờ làm)
// ============================================================================

/** Một dòng lịch bị ảnh hưởng khi giảm giờ làm. */
export interface ImpactedAppointment {
  appointmentId: string;
  code: string;
  startAt: string;
  endAt: string;
  attendeeName: string;
  attendeeLabel: string;     // "Bệnh nhân" / "Người thân"
  status: AppointmentStatus;
  link: string;               // deep link /appointments/{id}
}

/** Response từ POST /working-schedules/preview (UC-03). */
export interface AffectedScheduleResult {
  branchId: number;
  weekStart: string;
  impacted: ImpactedAppointment[];
}

// ============================================================================
// S26 — In-app notification
// ============================================================================

/** Loại notification — không chứa chẩn đoán (GBR-081). */
export type NotificationKind =
  | 'AppointmentBooked'      // vừa tạo lịch
  | 'AppointmentRescheduled' // đã đổi
  | 'AppointmentCancelled'   // đã huỷ
  | 'ReminderUpcoming'       // reminder 24h / 2h
  | 'SupportRequestUpdate'   // staff phản hồi request
  | 'NoShow'                 // staff đánh dấu không đến
  | 'ScheduleReduced';       // lịch bị ảnh hưởng

/** Kênh dispatch — in-app authoritative, external qua adapter. */
export type NotificationChannel = 'InApp' | 'Email' | 'Push' | 'Sms';

/** Trạng thái dispatch — tracking retry (max 3 lần, backoff 1/5/15p). */
export type DispatchStatus =
  | 'Pending'        // chờ gửi
  | 'Sent'           // đã gửi adapter
  | 'Delivered'      // adapter xác nhận delivered (chỉ với external)
  | 'Failed'         // thất bại sau 3 lần retry → staff handling item
  | 'Retrying';

export interface NotificationAttempt {
  channel: NotificationChannel;
  status: DispatchStatus;
  attemptCount: number;        // 0..3
  lastAttemptAt: string | null;
  nextRetryAt: string | null;  // backoff 1/5/15 phút
  errorMessage: string | null;
}

export interface NotificationDto {
  id: string;                  // "NT-000987"
  accountId: number | null;    // null cho anonymous broadcast (deep link vẫn có)
  kind: NotificationKind;
  title: string;
  body: string;                // KHÔNG chứa chẩn đoán
  createdAt: string;
  readAt: string | null;       // null nếu chưa đọc
  appointmentId: string | null; // để build deep link
  requestId: string | null;     // support request id (nếu liên quan)
  deepLink: string | null;     // "/appointments/{id}" — server re-check quyền khi mở
  attempts: NotificationAttempt[]; // 1+ mục: InApp + external (email/push) tuỳ cấu hình
  /** Lỗi external vẫn giữ booking; staff handling item nếu attempts Failed. */
  hasExternalFailure: boolean;
  /** Client dùng để dedupe: notification được dispatch nhiều lần nhưng vẫn 1 row. */
  dedupeKey: string;
}

// ============================================================================
// Helper — mapping từ BookingResult (API cũ) sang AppointmentDto
// ============================================================================

/**
 * Convert BookingResult của API cũ sang AppointmentDto mock — dùng tạm cho BookingScreen.
 * Khi S12 triển khai xong, BookingScreen sẽ đọc từ appointmentMockRepo.getById.
 */
export function fromBookingResult(b: BookingResult): AppointmentDto {
  const now = new Date().toISOString();
  return {
    id: b.appointmentId,
    code: b.code,
    version: b.version,
    status: (b.status as AppointmentStatus) ?? 'Pending',
    startAt: b.startAt,
    endAt: b.endAt,
    branchId: 0,
    branchName: '',
    chairId: null,
    dentistId: 0,
    dentistName: '',
    assistantId: null,
    serviceId: 0,
    serviceName: '',
    durationMinutes: 0,
    booker: { accountId: null, name: '', phone: null, email: null, isAnonymous: true },
    attendee: { patientId: null, name: '', birthDate: null },
    attendeeRelation: 'Self',
    source: 'App',
    reason: null,
    confirmedToAttend: 'Pending',
    history: [
      { at: now, actor: 'System', action: 'Created', after: { status: 'Pending' } },
    ],
  };
}
