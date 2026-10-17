/**
 * Mock repository cho S12 — UI dùng trong khi chờ TV2 backend.
 *
 * Lưu trong memory (module-level). Dữ liệu sinh ngẫu nhiên có kiểm soát; mỗi lần mount app
 * sẽ có cùng seed (cố định bằng Date.now() trong test) để UI ổn định khi dev.
 *
 * Theo chuẩn bàn giao: UI + API thật + dữ liệu/migration thuộc phạm vi. Repo này là tầng
 * abstraction: khi TV2 hoàn thành, đổi implementation sang `appointmentHttpRepo` mà UI
 * không cần đổi.
 *
 * Pattern: tất cả mutation đều dùng `requestKey` (idempotency) và `expectedVersion` (conflict).
 * Mutation thất bại trả về object `{ ok: false, errorCode, errorMessage }` — UI KHÔNG được
 * tự thông báo thành công khi `ok === false` (tiêu chí nghiệm thu: "Không báo xử lý thành
 * công nếu mutation thất bại").
 */

import type {
  AffectedScheduleResult,
  AppointmentDto,
  AppointmentStatus,
  ChangeReason,
  ImpactedAppointment,
  StaffChangeCommand,
  StaffChangeResult,
  StaffResolveCommand,
  StaffResolveResult,
  StaffVerifyRequesterCommand,
  SupportRequestDto,
  SupportRequestKind,
  SupportRequestStatus,
} from './appointment.types';

// ---------------------------------------------------------------------------
// In-memory store
// ---------------------------------------------------------------------------

let _seed = 0;
function nextId(prefix: string): string {
  _seed += 1;
  return `${prefix}-${Date.now().toString(36).toUpperCase()}-${_seed.toString(36).toUpperCase()}`;
}

const appointments = new Map<string, AppointmentDto>();
const requests = new Map<string, SupportRequestDto>();
let _nextRequestNum = 1;
function nextRequestId(): string {
  const n = _nextRequestNum++;
  return `SR-${n.toString().padStart(6, '0')}`;
}

// Seed một vài appointment + request để UI có data ngay khi mở.
function seed(): void {
  if (appointments.size > 0) return;
  const now = Date.now();
  const baseAppts: AppointmentDto[] = [
    {
      id: 'AP-20261016-001',
      code: 'AP-001',
      version: 3,
      status: 'Confirmed',
      startAt: new Date(now + 86400_000).toISOString(),
      endAt: new Date(now + 86400_000 + 30 * 60_000).toISOString(),
      branchId: 1,
      branchName: 'Chi nhánh Quận 1',
      chairId: 1,
      dentistId: 1,
      dentistName: 'BS. Nguyễn Văn A',
      assistantId: null,
      serviceId: 1,
      serviceName: 'Khám tổng quát',
      durationMinutes: 30,
      booker: { accountId: 1, name: 'Trần Văn B', phone: '0901234567', email: 'tranb@example.com', isAnonymous: false },
      attendee: { patientId: 101, name: 'Trần Văn B', birthDate: '1990-05-10' },
      attendeeRelation: 'Self',
      source: 'App',
      reason: 'Đau răng hàm dưới',
      confirmedToAttend: 'Yes',
      history: [
        { at: new Date(now - 86400_000 * 2).toISOString(), actor: 'Booker', actorUserId: 1, action: 'Created', after: { status: 'Pending' } },
        { at: new Date(now - 86400_000 * 2 + 600_000).toISOString(), actor: 'Staff', action: 'RequestSubmitted' },
        { at: new Date(now - 86400_000).toISOString(), actor: 'Staff', action: 'RequestResolved', after: { status: 'Confirmed' } },
        { at: new Date(now - 86400_000 + 1200_000).toISOString(), actor: 'Booker', actorUserId: 1, action: 'ConfirmedToAttend', notes: 'Sẽ đến đúng giờ' },
      ],
    },
    {
      id: 'AP-20261016-002',
      code: 'AP-002',
      version: 1,
      status: 'Pending',
      startAt: new Date(now + 3 * 86400_000).toISOString(),
      endAt: new Date(now + 3 * 86400_000 + 45 * 60_000).toISOString(),
      branchId: 1,
      branchName: 'Chi nhánh Quận 1',
      chairId: 2,
      dentistId: 1,
      dentistName: 'BS. Nguyễn Văn A',
      assistantId: null,
      serviceId: 2,
      serviceName: 'Lấy cao răng',
      durationMinutes: 45,
      booker: { accountId: 2, name: 'Lê Thị C', phone: '0912345678', email: 'lec@example.com', isAnonymous: false },
      attendee: { patientId: 102, name: 'Lê Thị C', birthDate: '1995-08-22' },
      attendeeRelation: 'Self',
      source: 'App',
      reason: 'Cao răng tích tụ lâu ngày',
      confirmedToAttend: 'Pending',
      history: [
        { at: new Date(now - 3600_000).toISOString(), actor: 'Booker', actorUserId: 2, action: 'Created', after: { status: 'Pending' } },
      ],
    },
    {
      id: 'AP-20261016-003',
      code: 'AP-003',
      version: 2,
      status: 'Rescheduled',
      startAt: new Date(now + 5 * 86400_000).toISOString(),
      endAt: new Date(now + 5 * 86400_000 + 30 * 60_000).toISOString(),
      branchId: 1,
      branchName: 'Chi nhánh Quận 1',
      chairId: 1,
      dentistId: 2,
      dentistName: 'BS. Phạm Thị D',
      assistantId: null,
      serviceId: 1,
      serviceName: 'Khám tổng quát',
      durationMinutes: 30,
      booker: { accountId: null, name: 'Khách vãng lai', phone: '0923456789', email: null, isAnonymous: true },
      attendee: { patientId: null, name: 'Khách vãng lai', birthDate: null },
      attendeeRelation: 'Self',
      source: 'WalkIn',
      reason: null,
      confirmedToAttend: 'No',
      history: [
        { at: new Date(now - 7 * 86400_000).toISOString(), actor: 'System', action: 'Created', after: { status: 'Confirmed', startAt: new Date(now + 2 * 86400_000).toISOString() } },
        { at: new Date(now - 86400_000).toISOString(), actor: 'Staff', action: 'Rescheduled', before: { startAt: new Date(now + 2 * 86400_000).toISOString() }, after: { startAt: new Date(now + 5 * 86400_000).toISOString(), reason: 'DentistUnavailable' }, notes: 'BS. A bận họp' },
        { at: new Date(now - 86400_000 + 600_000).toISOString(), actor: 'Booker', action: 'ConfirmedToAttend', notes: 'Không thể đến ngày mới', after: { status: 'Rescheduled' } as { status: AppointmentStatus } },
      ],
    },
  ];
  for (const a of baseAppts) appointments.set(a.id, a);

  // Seed support requests tương ứng.
  const req1: SupportRequestDto = {
    id: nextRequestId(),
    appointmentId: 'AP-20261016-002',
    appointmentCode: 'AP-002',
    kind: 'ConfirmToAttend',
    status: 'Pending',
    expectedVersion: 1,
    requestKey: 'seed-key-1',
    reason: 'Bệnh nhân muốn xác nhận đến',
    requester: { accountId: 2, name: 'Lê Thị C', phone: '0912345678', isAnonymous: false },
    history: [
      { at: new Date(now - 1800_000).toISOString(), actor: 'Booker', actorUserId: 2, action: 'RequestSubmitted', notes: 'Bệnh nhân muốn xác nhận đến' },
    ],
    createdAt: new Date(now - 1800_000).toISOString(),
    updatedAt: new Date(now - 1800_000).toISOString(),
  };
  const req2: SupportRequestDto = {
    id: nextRequestId(),
    appointmentId: 'AP-20261016-001',
    appointmentCode: 'AP-001',
    kind: 'Change',
    status: 'InReview',
    expectedVersion: 2,
    requestKey: 'seed-key-2',
    reason: 'Cần dời sang chiều thứ 6',
    proposedChange: { newStartAt: new Date(now + 6 * 86400_000).toISOString(), reason: 'PatientRequest' },
    requester: { accountId: 1, name: 'Trần Văn B', phone: '0901234567', isAnonymous: false },
    history: [
      { at: new Date(now - 7200_000).toISOString(), actor: 'Booker', actorUserId: 1, action: 'RequestSubmitted' },
      { at: new Date(now - 3600_000).toISOString(), actor: 'Staff', action: 'InReview' as 'RequestSubmitted' },
    ],
    createdAt: new Date(now - 7200_000).toISOString(),
    updatedAt: new Date(now - 3600_000).toISOString(),
  };
  requests.set(req1.id, req1);
  requests.set(req2.id, req2);
}
seed();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function nowIso(): string {
  return new Date().toISOString();
}

function checkVersion(current: number, expected: number): 'OK' | 'STALE' {
  return current === expected ? 'OK' : 'STALE';
}

// ---------------------------------------------------------------------------
// Public API — UI dùng
// ---------------------------------------------------------------------------

export const appointmentMockRepo = {
  /** Lấy appointment theo id. */
  async getById(id: string): Promise<AppointmentDto | null> {
    await sleep(50);
    return appointments.get(id) ?? null;
  },

  /** Lấy appointment theo mã (code). */
  async getByCode(code: string): Promise<AppointmentDto | null> {
    await sleep(50);
    for (const a of appointments.values()) if (a.code === code) return a;
    return null;
  },

  /** List appointment trong khoảng thời gian của account hiện tại (UI hub). */
  async listForAccount(accountId: number, opts?: { limit?: number }): Promise<AppointmentDto[]> {
    await sleep(80);
    const list: AppointmentDto[] = [];
    for (const a of appointments.values()) {
      if (a.booker.accountId === accountId) list.push(a);
      else if (a.attendee.patientId !== null) {
        // giả lập: bệnh nhân có liên kết tài khoản cũng xem được
        // mock: chỉ accountId trùng
      }
    }
    list.sort((x, y) => x.startAt.localeCompare(y.startAt));
    return opts?.limit ? list.slice(0, opts.limit) : list;
  },

  /** List support request của account hiện tại. */
  async listRequestsForAccount(accountId: number): Promise<SupportRequestDto[]> {
    await sleep(80);
    const list: SupportRequestDto[] = [];
    for (const r of requests.values()) {
      if (r.requester.accountId === accountId) list.push(r);
    }
    list.sort((x, y) => y.updatedAt.localeCompare(x.updatedAt));
    return list;
  },

  /** List support request cho staff (filter theo status, branch). */
  async listRequestsForStaff(filter?: {
    statuses?: SupportRequestStatus[];
    branchId?: number;
  }): Promise<SupportRequestDto[]> {
    await sleep(80);
    const list: SupportRequestDto[] = [];
    for (const r of requests.values()) {
      if (filter?.statuses && !filter.statuses.includes(r.status)) continue;
      if (filter?.branchId) {
        const a = appointments.get(r.appointmentId);
        if (!a || a.branchId !== filter.branchId) continue;
      }
      list.push(r);
    }
    list.sort((x, y) => y.updatedAt.localeCompare(x.updatedAt));
    return list;
  },

  /** Lấy support request theo id. */
  async getRequest(id: string): Promise<SupportRequestDto | null> {
    await sleep(50);
    return requests.get(id) ?? null;
  },

  /**
   * Staff command: xác minh requester (TV4). Chỉ chuyển status Pending → InReview.
   * Yêu cầu quyền staff + branch match.
   */
  async verifyRequester(
    cmd: StaffVerifyRequesterCommand & { staffBranchId: number; hasVerifyPermission: boolean },
  ): Promise<{ ok: boolean; request?: SupportRequestDto; errorCode?: string; errorMessage?: string }> {
    await sleep(100);
    const r = requests.get(cmd.requestKey) ?? [...requests.values()].find((x) => x.requestKey === cmd.requestKey);
    if (!r) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy yêu cầu.' };
    if (!cmd.hasVerifyPermission) return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không có quyền xác minh.' };
    const a = appointments.get(r.appointmentId);
    if (!a) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy lịch hẹn.' };
    if (a.branchId !== cmd.staffBranchId) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Yêu cầu không thuộc chi nhánh của bạn.' };
    }
    if (r.status !== 'Pending') {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: 'Yêu cầu đã được xử lý.' };
    }
    if (checkVersion(r.expectedVersion, cmd.expectedVersion) === 'STALE') {
      return { ok: false, errorCode: 'STALE_VERSION', errorMessage: 'Phiên bản đã thay đổi. Vui lòng tải lại.' };
    }
    r.status = 'InReview';
    r.expectedVersion += 1;
    r.updatedAt = nowIso();
    r.history.push({ at: nowIso(), actor: 'Staff', action: 'InReview' as 'RequestSubmitted', notes: cmd.notes });
    return { ok: true, request: { ...r } };
  },

  /**
   * Staff command: đổi/huỷ appointment (TV4).
   * - Phải có quyền edit (chỉ staff role: branch, chair, staff — không phải booker/attendee).
   * - Status hợp lệ: Pending/Confirmed/Rescheduled.
   * - expectedVersion khớp.
   * - Sau khi commit thành công mới trả ok=true; mutation thất bại trả ok=false.
   */
  async staffChange(
    appointmentId: string,
    cmd: StaffChangeCommand & {
      staffBranchId: number;
      hasEditPermission: boolean;
      newStatus?: AppointmentStatus;
      newStartAt?: string;
    },
  ): Promise<StaffChangeResult> {
    await sleep(150);
    const a = appointments.get(appointmentId);
    if (!a) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy lịch hẹn.' };
    if (!cmd.hasEditPermission) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không có quyền chỉnh sửa lịch hẹn.' };
    }
    if (a.branchId !== cmd.staffBranchId) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Lịch hẹn không thuộc chi nhánh của bạn.' };
    }
    if (checkVersion(a.version, cmd.expectedVersion) === 'STALE') {
      return { ok: false, errorCode: 'CONFLICT', errorMessage: 'Phiên bản đã thay đổi. Vui lòng tải lại.' };
    }
    if (!['Pending', 'Confirmed', 'Rescheduled'].includes(a.status)) {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: `Không thể đổi/huỷ lịch ở trạng thái ${a.status}.` };
    }
    // Mutation thật sự.
    const newStatus: AppointmentStatus = cmd.newStatus ?? a.status;
    const before = { status: a.status, startAt: a.startAt };
    a.status = newStatus;
    if (cmd.newStartAt) a.startAt = cmd.newStartAt;
    a.version += 1;
    a.history.push({
      at: nowIso(),
      actor: 'Staff',
      action: newStatus === 'Cancelled' ? 'Cancelled' : 'Rescheduled',
      before,
      after: { status: a.status, startAt: a.startAt, reason: cmd.reason },
      notes: cmd.notes,
    });
    return { ok: true, appointment: { ...a }, notificationCode: 'MSG37' };
  },

  /**
   * Staff command: resolve/reject support request (TV5).
   * - Quyền staff + branch match.
   * - Request status phải là Pending HOẶC InReview (GBR-075: Withdrawn không được resolve).
   * - expectedVersion khớp.
   * - Resolve CHỈ trả ok=true SAU khi staffChange thành công (nếu Change/Cancel).
   * - Mutation thất bại: trả ok=false, KHÔNG thông báo thành công.
   */
  async staffResolve(
    cmd: StaffResolveCommand & {
      requestId: string;
      staffBranchId: number;
      hasResolvePermission: boolean;
      newStatus?: AppointmentStatus;
      newStartAt?: string;
      changeReason?: ChangeReason;
    },
  ): Promise<StaffResolveResult> {
    await sleep(150);
    const r = requests.get(cmd.requestId);
    if (!r) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy yêu cầu.' };
    if (!cmd.hasResolvePermission) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không có quyền xử lý yêu cầu.' };
    }
    if (!['Pending', 'InReview'].includes(r.status)) {
      // Withdrawn/Resolved/Rejected đều bị chặn (GBR-075).
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: `Không thể xử lý yêu cầu ở trạng thái ${r.status}.` };
    }
    const a = appointments.get(r.appointmentId);
    if (!a) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy lịch hẹn.' };
    if (a.branchId !== cmd.staffBranchId) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Yêu cầu không thuộc chi nhánh của bạn.' };
    }
    if (checkVersion(r.expectedVersion, cmd.expectedVersion) === 'STALE') {
      return { ok: false, errorCode: 'STALE_VERSION', errorMessage: 'Phiên bản đã thay đổi. Vui lòng tải lại.' };
    }

    // Nếu Resolve và cần booking action (Change/Cancel): phải commit change TRƯỚC.
    if (cmd.decision === 'Resolve' && (r.kind === 'Change' || r.kind === 'Cancel')) {
      const newStatus: AppointmentStatus = r.kind === 'Cancel'
        ? 'Cancelled'
        : (cmd.newStatus ?? a.status);
      const change = await this.staffChange(a.id, {
        requestKey: `${cmd.requestKey}-change`,
        expectedVersion: a.version,
        reason: cmd.changeReason ?? r.proposedChange?.reason ?? 'Other',
        notes: cmd.notes,
        staffBranchId: cmd.staffBranchId,
        hasEditPermission: cmd.hasResolvePermission,
        newStatus,
        newStartAt: cmd.newStartAt ?? r.proposedChange?.newStartAt,
      });
      if (!change.ok) {
        // Mutation thất bại → resolve KHÔNG thành công.
        return { ok: false, errorCode: change.errorCode, errorMessage: change.errorMessage };
      }
      // Commit change xong mới resolve request.
      r.status = 'Resolved';
      r.expectedVersion += 1;
      r.updatedAt = nowIso();
      r.history.push({ at: nowIso(), actor: 'Staff', action: 'RequestResolved', notes: cmd.notes });
      return { ok: true, request: { ...r }, appointment: change.appointment };
    }

    // ConfirmToAttend / WithdrawAttend / General: chỉ cập nhật flag/cờ, không đổi appointment.
    if (r.kind === 'ConfirmToAttend' && cmd.decision === 'Resolve') {
      a.confirmedToAttend = 'Yes';
      a.version += 1;
      a.history.push({ at: nowIso(), actor: 'Staff', action: 'ConfirmedToAttend', notes: cmd.notes });
    } else if (r.kind === 'WithdrawAttend' && cmd.decision === 'Resolve') {
      a.confirmedToAttend = 'No';
      a.version += 1;
      a.history.push({ at: nowIso(), actor: 'Staff', action: 'ConfirmedToAttend', notes: cmd.notes });
    }
    r.status = cmd.decision === 'Resolve' ? 'Resolved' : 'Rejected';
    r.expectedVersion += 1;
    r.updatedAt = nowIso();
    r.history.push({
      at: nowIso(),
      actor: 'Staff',
      action: cmd.decision === 'Resolve' ? 'RequestResolved' : 'RequestRejected',
      notes: cmd.notes,
    });
    return { ok: true, request: { ...r }, appointment: { ...a } };
  },

  /**
   * Requester rút request (Withdrawn). Sau khi rút KHÔNG được resolve lại (GBR-075).
   */
  async withdrawRequest(
    requestId: string,
    requesterAccountId: number,
    expectedVersion: number,
  ): Promise<{ ok: boolean; request?: SupportRequestDto; errorCode?: string; errorMessage?: string }> {
    await sleep(80);
    const r = requests.get(requestId);
    if (!r) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy yêu cầu.' };
    if (r.requester.accountId !== requesterAccountId) {
      return { ok: false, errorCode: 'FORBIDDEN', errorMessage: 'Bạn không phải người tạo yêu cầu này.' };
    }
    if (r.status === 'Resolved' || r.status === 'Rejected' || r.status === 'Withdrawn') {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: 'Không thể rút yêu cầu đã xử lý.' };
    }
    if (checkVersion(r.expectedVersion, expectedVersion) === 'STALE') {
      return { ok: false, errorCode: 'STALE_VERSION', errorMessage: 'Phiên bản đã thay đổi. Vui lòng tải lại.' };
    }
    r.status = 'Withdrawn';
    r.expectedVersion += 1;
    r.updatedAt = nowIso();
    r.history.push({ at: nowIso(), actor: 'Booker', actorUserId: requesterAccountId, action: 'RequestWithdrawn' });
    return { ok: true, request: { ...r } };
  },

  /**
   * Schedule bị ảnh hưởng khi staff giảm giờ làm.
   * Trả về các appointment Confirmed/Rescheduled nằm trong khung giờ bị thu hẹp.
   * UC-03 S09: Tư vẫn chặn schedule publish cho đến khi hợp lệ.
   */
  async previewAffectedSchedule(branchId: number, weekStart: string): Promise<AffectedScheduleResult> {
    await sleep(120);
    const weekStartMs = new Date(weekStart).getTime();
    const weekEndMs = weekStartMs + 7 * 86400_000;
    const impacted: ImpactedAppointment[] = [];
    for (const a of appointments.values()) {
      if (a.branchId !== branchId) continue;
      if (!['Confirmed', 'Rescheduled'].includes(a.status)) continue;
      const start = new Date(a.startAt).getTime();
      if (start >= weekStartMs && start < weekEndMs) {
        impacted.push({
          appointmentId: a.id,
          code: a.code,
          startAt: a.startAt,
          endAt: a.endAt,
          attendeeName: a.attendee.name,
          attendeeLabel: a.attendeeRelation === 'Self' ? 'Bệnh nhân' : 'Người thân',
          status: a.status,
          link: `/appointments/${a.id}`,
        });
      }
    }
    return { branchId, weekStart, impacted };
  },

  /** Tạo mới một support request (UC-05/06 phần hỗ trợ). */
  async createRequest(input: {
    appointmentId: string;
    kind: SupportRequestKind;
    reason: string | null;
    requester: { accountId: number | null; name: string; phone: string | null; isAnonymous: boolean };
    proposedChange?: { newStartAt?: string; newDentistId?: number; newServiceId?: number; reason: ChangeReason };
    requestKey: string;
  }): Promise<{ ok: boolean; request?: SupportRequestDto; errorCode?: string; errorMessage?: string }> {
    await sleep(100);
    const a = appointments.get(input.appointmentId);
    if (!a) return { ok: false, errorCode: 'NOT_FOUND', errorMessage: 'Không tìm thấy lịch hẹn.' };
    if (!['Pending', 'Confirmed', 'Rescheduled'].includes(a.status)) {
      return { ok: false, errorCode: 'BAD_REQUEST', errorMessage: `Không thể tạo yêu cầu khi lịch ở trạng thái ${a.status}.` };
    }
    // Idempotency: nếu đã có request cùng key thì trả về bản cũ.
    for (const r of requests.values()) {
      if (r.requestKey === input.requestKey) {
        return { ok: true, request: { ...r } };
      }
    }
    const req: SupportRequestDto = {
      id: nextRequestId(),
      appointmentId: a.id,
      appointmentCode: a.code,
      kind: input.kind,
      status: 'Pending',
      expectedVersion: a.version,
      requestKey: input.requestKey,
      reason: input.reason,
      proposedChange: input.proposedChange,
      requester: input.requester,
      history: [{ at: nowIso(), actor: input.requester.accountId ? 'Booker' : 'System', actorUserId: input.requester.accountId ?? undefined, action: 'RequestSubmitted' }],
      createdAt: nowIso(),
      updatedAt: nowIso(),
    };
    requests.set(req.id, req);
    return { ok: true, request: { ...req } };
  },
};

function sleep(ms: number): Promise<void> {
  return new Promise((res) => setTimeout(res, ms));
}
