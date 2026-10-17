/**
 * S26 — Staff Support Inbox mock repo.
 *
 * Cho tới khi backend TV5 triển khai xong, UI dùng mock in-memory này để staff
 * thấy đầy đủ luồng nghiệp vụ (list → drawer → command). Khi chuyển sang HTTP
 * thật, chỉ thay phần thân hàm; UI không đổi.
 *
 * Theo dặn dò S26: Resolve chỉ `ok=true` khi booking action (Change/Cancel) đã
 * commit. ConfirmToAttend/WithdrawAttend/General thì Resolve không cần gọi
 * staffChange trước.
 *
 * Lỗi STALE_VERSION cố ý để staff thử flow xung đột.
 */

import {
  type AppointmentSummary,
  type AppointmentHistoryEntry,
  type StaffChangeCommand,
  type StaffChangeResult,
  type StaffResolveCommand,
  type StaffResolveResult,
  type StaffVerifyRequesterCommand,
  type SupportRequestDetail,
  type SupportRequestDto,
  type SupportRequestPage,
  type SupportRequestStatus,
} from './types';

function isoFromOffset(days: number, hour: number, minute = 0): string {
  const base = new Date('2026-10-17T00:00:00+07:00');
  base.setUTCDate(base.getUTCDate() + days);
  base.setUTCHours(hour - 7, minute, 0, 0); // chuyển sang UTC vì base là +07:00
  return base.toISOString();
}

// ----------------------------------------------------------------------------
// Seed: 1 appointment + 6 support request
// ----------------------------------------------------------------------------

const appointment1: AppointmentSummary = {
  id: 'AP-20261017-001',
  code: 'AP-1710-001',
  version: 3,
  status: 'Confirmed',
  startAt: isoFromOffset(1, 9, 0),
  endAt: isoFromOffset(1, 9, 30),
  branchId: 1,
  branchName: 'Chi nhánh Trung tâm',
  chairId: 1,
  dentistId: 1,
  dentistName: 'BS. Nguyễn Văn A',
  serviceId: 1,
  serviceName: 'Khám tổng quát',
  durationMinutes: 30,
  attendee: { patientId: 1001, name: 'Nguyễn Văn An', birthDate: '1990-05-12', relation: 'Self' },
  booker: { accountId: 501, name: 'Nguyễn Văn An', phone: '0901000001', isAnonymous: false },
  confirmedToAttend: 'Pending',
};

const appointment2: AppointmentSummary = {
  ...appointment1,
  id: 'AP-20261018-002',
  code: 'AP-1810-002',
  version: 1,
  startAt: isoFromOffset(2, 14, 0),
  endAt: isoFromOffset(2, 14, 45),
  attendee: { patientId: 1002, name: 'Trần Thị Bình', birthDate: '1985-11-03', relation: 'Self' },
  booker: { accountId: 502, name: 'Trần Thị Bình', phone: '0901000002', isAnonymous: false },
};

const appointment3: AppointmentSummary = {
  ...appointment1,
  id: 'AP-20261019-003',
  code: 'AP-1910-003',
  version: 1,
  startAt: isoFromOffset(3, 10, 30),
  endAt: isoFromOffset(3, 11, 15),
  attendee: { patientId: null, name: 'Con của chị Lan', birthDate: '2018-03-22', relation: 'Relative' },
  booker: { accountId: 503, name: 'Phạm Thị Lan', phone: '0901000003', isAnonymous: false },
  confirmedToAttend: 'Yes',
};

const appointment4: AppointmentSummary = {
  ...appointment1,
  id: 'AP-20261020-004',
  code: 'AP-2010-004',
  version: 4,
  status: 'Rescheduled',
  startAt: isoFromOffset(4, 15, 0),
  endAt: isoFromOffset(4, 15, 30),
  attendee: { patientId: 1004, name: 'Lê Minh Châu', birthDate: '1992-09-09', relation: 'Self' },
  booker: { accountId: 504, name: 'Lê Minh Châu', phone: '0901000004', isAnonymous: false },
  confirmedToAttend: 'Yes',
};

const appointment5: AppointmentSummary = {
  ...appointment1,
  id: 'AP-20261021-005',
  code: 'AP-2110-005',
  version: 1,
  status: 'Cancelled',
  startAt: isoFromOffset(5, 8, 30),
  endAt: isoFromOffset(5, 9, 0),
  attendee: { patientId: 1005, name: 'Hoàng Văn Em', birthDate: '1979-04-18', relation: 'Self' },
  booker: { accountId: null, name: 'Khách vãng lai', phone: '0901000005', isAnonymous: true },
};

const baseHistory: AppointmentHistoryEntry[] = [
  { at: isoFromOffset(0, 8, 0), actor: 'System', action: 'Created', after: { status: 'Pending' } },
  { at: isoFromOffset(0, 8, 5), actor: 'Staff', actorUserId: 1, action: 'Rescheduled', after: { status: 'Confirmed' }, notes: 'Xác minh requester tại quầy.' },
];

const seedDetails: SupportRequestDetail[] = [
  {
    id: 'SR-000101', appointmentId: appointment1.id, appointmentCode: appointment1.code,
    kind: 'Change', status: 'Pending', expectedVersion: 3, requestKey: 'k-101',
    reason: 'Bệnh nhân bị kẹt xe, xin dời sang chiều.',
    proposedChange: { newStartAt: isoFromOffset(1, 14, 0), reason: 'PatientRequest' },
    requester: { accountId: 501, name: 'Nguyễn Văn An', phone: '0901000001', isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(0, 9, 0), updatedAt: isoFromOffset(0, 9, 5),
    appointment: appointment1, history: baseHistory, decisionReason: null, decidedAt: null, deciderName: null,
  },
  {
    id: 'SR-000102', appointmentId: appointment2.id, appointmentCode: appointment2.code,
    kind: 'Change', status: 'Pending', expectedVersion: 1, requestKey: 'k-102',
    reason: 'Có công việc phát sinh, xin đổi sang thứ Hai tuần sau.',
    proposedChange: { newStartAt: isoFromOffset(7, 9, 0), reason: 'PatientRequest' },
    requester: { accountId: 502, name: 'Trần Thị Bình', phone: '0901000002', isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(0, 10, 0), updatedAt: isoFromOffset(0, 10, 0),
    appointment: appointment2, history: baseHistory, decisionReason: null, decidedAt: null, deciderName: null,
  },
  {
    id: 'SR-000103', appointmentId: appointment3.id, appointmentCode: appointment3.code,
    kind: 'Cancel', status: 'InReview', expectedVersion: 1, requestKey: 'k-103',
    reason: 'Con bị sốt, muốn dời sang tuần sau; staff đang liên hệ đặt lịch mới.',
    requester: { accountId: 503, name: 'Phạm Thị Lan', phone: '0901000003', isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(0, 7, 30), updatedAt: isoFromOffset(0, 11, 0),
    appointment: appointment3, history: baseHistory, decisionReason: null, decidedAt: null, deciderName: null,
  },
  {
    id: 'SR-000104', appointmentId: appointment1.id, appointmentCode: appointment1.code,
    kind: 'ConfirmToAttend', status: 'Pending', expectedVersion: 3, requestKey: 'k-104',
    reason: null,
    requester: { accountId: 501, name: 'Nguyễn Văn An', phone: '0901000001', isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(0, 12, 0), updatedAt: isoFromOffset(0, 12, 0),
    appointment: appointment1, history: baseHistory, decisionReason: null, decidedAt: null, deciderName: null,
  },
  {
    id: 'SR-000105', appointmentId: appointment4.id, appointmentCode: appointment4.code,
    kind: 'Change', status: 'Resolved', expectedVersion: 4, requestKey: 'k-105',
    reason: 'Bệnh nhân yêu cầu đổi sang chiều; staff đã đổi và xác nhận lại.',
    proposedChange: { newStartAt: appointment4.startAt, reason: 'PatientRequest' },
    requester: { accountId: 504, name: 'Lê Minh Châu', phone: '0901000004', isAnonymous: false },
    permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(-1, 9, 0), updatedAt: isoFromOffset(-1, 9, 30),
    appointment: appointment4,
    history: [
      ...baseHistory,
      { at: isoFromOffset(-1, 9, 25), actor: 'Staff', actorUserId: 1, action: 'Rescheduled', after: { status: 'Rescheduled' }, notes: 'Đổi sang 15:00 theo yêu cầu.' },
      { at: isoFromOffset(-1, 9, 30), actor: 'Staff', actorUserId: 1, action: 'RequestResolved', notes: 'Đã đổi lịch thành công.' },
    ],
    decisionReason: 'Đã đổi sang khung 15:00 cùng ngày; appointment status: Rescheduled.',
    decidedAt: isoFromOffset(-1, 9, 30), deciderName: 'BS. Nguyễn Văn A',
  },
  {
    id: 'SR-000106', appointmentId: appointment5.id, appointmentCode: appointment5.code,
    kind: 'Cancel', status: 'Withdrawn', expectedVersion: 1, requestKey: 'k-106',
    reason: 'Khách vãng lai gọi điện xin huỷ; staff lên yêu cầu nhưng khách đã gọi lại tự huỷ qua điện thoại lễ tân.',
    requester: { accountId: null, name: 'Khách vãng lai', phone: '0901000005', isAnonymous: true },
    permission: { canResolve: false, canEdit: false, branchMatch: true, ownershipMatch: true },
    createdAt: isoFromOffset(-2, 14, 0), updatedAt: isoFromOffset(-2, 14, 30),
    appointment: appointment5,
    history: [
      ...baseHistory,
      { at: isoFromOffset(-2, 14, 25), actor: 'Patient', action: 'RequestWithdrawn', notes: 'Khách tự rút sau khi staff gọi lại.' },
    ],
    decisionReason: null, decidedAt: isoFromOffset(-2, 14, 25), deciderName: 'Khách vãng lai',
  },
];

// ----------------------------------------------------------------------------
// State (in-memory)
// ----------------------------------------------------------------------------

let details: SupportRequestDetail[] = seedDetails.map(d => ({ ...d }));
let requestCounter = details.length;
let requestKeyCounter = details.length;

function toDto(detail: SupportRequestDetail): SupportRequestDto {
  const { appointment: _appointment, history: _history, decisionReason: _decisionReason, decidedAt: _decidedAt, deciderName: _deciderName, ...dto } = detail;
  return dto;
}

function nextRequestKey(): string {
  requestKeyCounter += 1;
  return `k-${requestKeyCounter}`;
}

function nowIso(): string {
  return new Date().toISOString();
}

function appendHistory(detail: SupportRequestDetail, entry: AppointmentHistoryEntry): void {
  detail.history = [...detail.history, entry];
  detail.updatedAt = entry.at;
}

// ----------------------------------------------------------------------------
// Repo public API
// ----------------------------------------------------------------------------

export const supportInboxMockRepo = {
  /** Trả về trang danh sách cho staff. */
  async listRequestsForStaff(input: {
    statuses: SupportRequestStatus[];
    branchId: number;
    page: number;
    pageSize: number;
  }): Promise<SupportRequestPage> {
    await delay(150);
    const filtered = details
      .filter(d => d.appointment.branchId === input.branchId)
      .filter(d => input.statuses.length === 0 || input.statuses.includes(d.status));
    const sorted = sortByPriority(filtered);
    const start = (input.page - 1) * input.pageSize;
    return {
      items: sorted.slice(start, start + input.pageSize).map(toDto),
      total: sorted.length,
      page: input.page,
      pageSize: input.pageSize,
    };
  },

  /** Lấy chi tiết kèm appointment + history. */
  async getRequest(id: string): Promise<SupportRequestDetail> {
    await delay(120);
    const found = details.find(d => d.id === id);
    if (!found) throw notFound('Yêu cầu không tồn tại hoặc đã được xử lý.');
    return clone(found);
  },

  /** Xác minh requester — không đổi lifecycle appointment, chỉ ghi audit. */
  async verifyRequester(id: string, command: StaffVerifyRequesterCommand): Promise<SupportRequestDetail> {
    await delay(120);
    const detail = details.find(d => d.id === id);
    if (!detail) throw notFound('Yêu cầu không tồn tại.');
    if (detail.expectedVersion !== command.expectedVersion) throw staleVersion();
    appendHistory(detail, {
      at: nowIso(), actor: 'Staff', actorUserId: 1,
      action: 'ConfirmedToAttend', notes: command.notes ?? 'Đã xác minh requester.',
    });
    return clone(detail);
  },

  /** Đổi/huỷ appointment kèm lý do. */
  async staffChange(id: string, command: StaffChangeCommand): Promise<StaffChangeResult> {
    await delay(150);
    const detail = details.find(d => d.id === id);
    if (!detail) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Yêu cầu không tồn tại.' };
    if (detail.expectedVersion !== command.expectedVersion) {
      return { ok: false, errorCode: 'STALE_VERSION', errorMessage: 'Phiên làm việc đã thay đổi, vui lòng tải lại.' };
    }
    if (detail.status === 'Withdrawn') {
      return { ok: false, errorCode: 'CONFLICT', errorMessage: 'Yêu cầu đã rút, không thể thay đổi lịch.' };
    }
    if (!detail.permission.canEdit || !detail.permission.branchMatch) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không có quyền chỉnh sửa lịch này.' };
    }
    if (detail.kind !== 'Change' && detail.kind !== 'Cancel') {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: 'Loại yêu cầu này không dùng Change/Cancel.' };
    }

    const isCancel = detail.kind === 'Cancel';
    if (!isCancel && !command.newStartAt) {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: 'Vui lòng chọn khung giờ mới.' };
    }

    const newAppointment: AppointmentSummary = {
      ...detail.appointment,
      version: detail.appointment.version + 1,
      status: isCancel ? 'Cancelled' : 'Rescheduled',
      startAt: command.newStartAt ?? detail.appointment.startAt,
      endAt: command.newStartAt
        ? addMinutesIso(command.newStartAt, detail.appointment.durationMinutes)
        : detail.appointment.endAt,
    };
    detail.appointment = newAppointment;
    detail.expectedVersion += 1;
    appendHistory(detail, {
      at: nowIso(), actor: 'Staff', actorUserId: 1,
      action: isCancel ? 'Cancelled' : 'Rescheduled',
      after: { status: newAppointment.status, startAt: newAppointment.startAt, reason: command.reason },
      notes: command.notes,
    });
    return { ok: true, appointment: newAppointment, notificationCode: isCancel ? 'MSG35' : 'MSG34' };
  },

  /** Resolve/Reject request. Resolve chỉ `ok=true` khi đã có action cần thiết commit. */
  async resolveRequest(id: string, command: StaffResolveCommand): Promise<StaffResolveResult> {
    await delay(140);
    const detail = details.find(d => d.id === id);
    if (!detail) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Yêu cầu không tồn tại.' };
    if (detail.expectedVersion !== command.expectedVersion) {
      return { ok: false, errorCode: 'STALE_VERSION', errorMessage: 'Phiên làm việc đã thay đổi, vui lòng tải lại.' };
    }
    if (!detail.permission.canResolve) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không có quyền xử lý yêu cầu này.' };
    }
    if (command.decision === 'Reject') {
      if (!command.decisionReason?.trim()) {
        return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: 'Vui lòng nhập lý do từ chối.' };
      }
      detail.status = 'Rejected';
      detail.decisionReason = command.decisionReason.trim();
      detail.decidedAt = nowIso();
      detail.deciderName = 'BS. Nguyễn Văn A';
      detail.expectedVersion += 1;
      appendHistory(detail, {
        at: nowIso(), actor: 'Staff', actorUserId: 1,
        action: 'RequestRejected', notes: command.notes,
      });
      return { ok: true, request: clone(detail), notificationCode: 'MSG38' };
    }

    // Resolve
    if (detail.status === 'Withdrawn') {
      return { ok: false, errorCode: 'CONFLICT', errorMessage: 'Yêu cầu đã rút, không thể xử lý.' };
    }
    if (detail.status === 'Resolved' || detail.status === 'Rejected') {
      return { ok: false, errorCode: 'CONFLICT', errorMessage: 'Yêu cầu đã được xử lý trước đó.' };
    }
    if (detail.kind === 'Change' || detail.kind === 'Cancel') {
      // Bắt buộc đã có staffChange commit; nếu appointment vẫn 'Confirmed'/'Pending' thì coi như chưa đổi.
      if (detail.appointment.status !== 'Rescheduled' && detail.appointment.status !== 'Cancelled') {
        return {
          ok: false, errorCode: 'BAD_REQUEST',
          errorMessage: 'Cần đổi hoặc huỷ lịch trước khi xử lý yêu cầu này.',
        };
      }
    }
    detail.status = 'Resolved';
    detail.decisionReason = command.notes?.trim() || (
      detail.kind === 'Change' ? 'Đã đổi lịch thành công.' :
      detail.kind === 'Cancel' ? 'Đã huỷ lịch.' :
      'Đã xử lý yêu cầu.'
    );
    detail.decidedAt = nowIso();
    detail.deciderName = 'BS. Nguyễn Văn A';
    detail.expectedVersion += 1;
    appendHistory(detail, {
      at: nowIso(), actor: 'Staff', actorUserId: 1,
      action: 'RequestResolved', notes: command.notes,
    });
    return { ok: true, request: clone(detail), notificationCode: 'MSG37' };
  },

  /** Test helper: tăng version để demo STALE_VERSION. Không dùng trong production. */
  __bumpVersionForTesting(id: string): void {
    const detail = details.find(d => d.id === id);
    if (detail) detail.expectedVersion += 1;
  },

  /** Test helper: thêm request mới (giữ mock ổn định qua các lần reload). */
  __seedForTesting(): void {
    if (details.length > 6) return;
    const fresh: SupportRequestDetail = {
      id: `SR-000${100 + requestCounter}`,
      appointmentId: appointment1.id,
      appointmentCode: appointment1.code,
      kind: 'WithdrawAttend',
      status: 'Pending',
      expectedVersion: appointment1.version,
      requestKey: nextRequestKey(),
      reason: 'Bệnh nhân không thể tham dự, xin rút xác nhận.',
      requester: { accountId: 501, name: 'Nguyễn Văn An', phone: '0901000001', isAnonymous: false },
      permission: { canResolve: true, canEdit: true, branchMatch: true, ownershipMatch: true },
      createdAt: nowIso(), updatedAt: nowIso(),
      appointment: appointment1, history: baseHistory, decisionReason: null, decidedAt: null, deciderName: null,
    };
    details = [fresh, ...details];
    requestCounter += 1;
  },
};

// ----------------------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------------------

function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function notFound(message: string): Error {
  const error = new Error(message) as Error & { code: string };
  error.code = 'MSG01';
  return error;
}

function staleVersion(): Error {
  const error = new Error('Phiên làm việc đã thay đổi, vui lòng tải lại.') as Error & { code: string };
  error.code = 'MSG111';
  return error;
}

function addMinutesIso(iso: string, minutes: number): string {
  const date = new Date(iso);
  date.setMinutes(date.getMinutes() + minutes);
  return date.toISOString();
}

function sortByPriority(items: SupportRequestDetail[]): SupportRequestDetail[] {
  // Pending trước, sau đó InReview, các trạng thái đóng xếp theo updatedAt desc.
  const order: Record<SupportRequestStatus, number> = {
    Pending: 0, InReview: 1, Resolved: 2, Rejected: 3, Withdrawn: 4,
  };
  return [...items].sort((a, b) => {
    const diff = order[a.status] - order[b.status];
    if (diff !== 0) return diff;
    return b.updatedAt.localeCompare(a.updatedAt);
  });
}

export const __supportInboxInternals = { details };
