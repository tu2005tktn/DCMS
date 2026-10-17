/**
 * Mock repository cho S26 — In-app notification + outbox/dispatcher.
 *
 * Theo GBR-081: notification KHÔNG chứa chẩn đoán. Theo AT16: external push tùy
 * adapter/cấu hình — lỗi external có staff handling item, KHÔNG rollback Appointment.
 *
 * Retry: tối đa 3 lần, backoff 1/5/15 phút (v2.0). Notification mới tạo sẽ có:
 *   - 1 attempt InApp (luôn thành công vì in-app authoritative)
 *   - 1+ attempt Email/Push (qua mock adapter — 60% thất bại ở attempt 1 để demo retry)
 *
 * Dedup: cùng `dedupeKey` chỉ tạo 1 row kể cả khi dispatch nhiều lần.
 */

import type {
  DispatchStatus,
  NotificationAttempt,
  NotificationChannel,
  NotificationDto,
  NotificationKind,
} from './appointment.types';

// ---------------------------------------------------------------------------
// In-memory store
// ---------------------------------------------------------------------------

const notifications: NotificationDto[] = [];
const _handledByStaff = new Set<string>(); // dedupe staff handling item

function nowIso(): string {
  return new Date().toISOString();
}

let _seedN = 0;
function nextId(): string {
  _seedN += 1;
  return `NT-${Date.now().toString(36).toUpperCase()}-${_seedN.toString(36).toUpperCase()}`;
}

const BACKOFF_MINUTES = [1, 5, 15] as const;

function buildAttempts(channels: NotificationChannel[]): NotificationAttempt[] {
  return channels.map((channel) => ({
    channel,
    status: 'Pending' as DispatchStatus,
    attemptCount: 0,
    lastAttemptAt: null,
    nextRetryAt: nowIso(),
    errorMessage: null,
  }));
}

// Seed vài notification mẫu cho account 1.
function seed(): void {
  if (notifications.length > 0) return;
  const now = Date.now();
  const base: NotificationDto[] = [
    {
      id: nextId(),
      accountId: 1,
      kind: 'AppointmentBooked',
      title: 'Đặt lịch thành công',
      body: 'Lịch khám AP-001 đã được tạo. Vui lòng đến trước 10 phút.',
      createdAt: new Date(now - 2 * 86400_000).toISOString(),
      readAt: new Date(now - 86400_000).toISOString(),
      appointmentId: 'AP-20261016-001',
      requestId: null,
      deepLink: '/appointments/AP-20261016-001',
      attempts: buildAttempts(['InApp', 'Email']),
      hasExternalFailure: false,
      dedupeKey: 'appt:AP-20261016-001:booked',
    },
    {
      id: nextId(),
      accountId: 1,
      kind: 'ReminderUpcoming',
      title: 'Nhắc lịch 24h',
      body: 'Bạn có lịch khám AP-001 vào ngày mai lúc 09:00.',
      createdAt: new Date(now - 3600_000).toISOString(),
      readAt: null,
      appointmentId: 'AP-20261016-001',
      requestId: null,
      deepLink: '/appointments/AP-20261016-001',
      attempts: buildAttempts(['InApp', 'Email', 'Push']),
      hasExternalFailure: false,
      dedupeKey: 'appt:AP-20261016-001:reminder:24h',
    },
    {
      id: nextId(),
      accountId: 1,
      kind: 'SupportRequestUpdate',
      title: 'Yêu cầu của bạn đang được xử lý',
      body: 'Yêu cầu đổi lịch AP-001 đang được nhân viên xem xét.',
      createdAt: new Date(now - 1800_000).toISOString(),
      readAt: null,
      appointmentId: 'AP-20261016-001',
      requestId: null,
      deepLink: '/appointments/AP-20261016-001',
      attempts: buildAttempts(['InApp']),
      hasExternalFailure: false,
      dedupeKey: 'req:AP-20261016-001:inreview',
    },
    {
      id: nextId(),
      accountId: 2,
      kind: 'AppointmentBooked',
      title: 'Đặt lịch thành công',
      body: 'Lịch khám AP-002 đã được tạo và đang chờ xác minh.',
      createdAt: new Date(now - 3600_000).toISOString(),
      readAt: null,
      appointmentId: 'AP-20261016-002',
      requestId: null,
      deepLink: '/appointments/AP-20261016-002',
      attempts: buildAttempts(['InApp', 'Email']),
      hasExternalFailure: false,
      dedupeKey: 'appt:AP-20261016-002:booked',
    },
  ];
  notifications.push(...base);
}
seed();

// ---------------------------------------------------------------------------
// Mock adapter — 60% attempt external fail ở lần 1 (demo retry/backoff)
// ---------------------------------------------------------------------------

function simulateExternalSend(channel: NotificationChannel, attemptCount: number): {
  ok: boolean;
  errorMessage: string | null;
} {
  if (channel === 'InApp') return { ok: true, errorMessage: null }; // authoritative
  // Email/Push: lần 1 fail 60%, lần 2 fail 30%, lần 3 fail 5%
  const r = Math.random();
  if (attemptCount === 0 && r < 0.6) return { ok: false, errorMessage: `${channel} adapter trả 500` };
  if (attemptCount === 1 && r < 0.3) return { ok: false, errorMessage: `${channel} adapter timeout` };
  if (attemptCount === 2 && r < 0.05) return { ok: false, errorMessage: `${channel} adapter lỗi không xác định` };
  return { ok: true, errorMessage: null };
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export const notificationMockRepo = {
  /** Lấy notification cho account hiện tại (S26: chỉ của account hiện tại, mở deep link kiểm quyền lại). */
  async listForAccount(accountId: number, opts?: { unreadOnly?: boolean; limit?: number }): Promise<NotificationDto[]> {
    await sleep(80);
    let list = notifications.filter((n) => n.accountId === accountId);
    if (opts?.unreadOnly) list = list.filter((n) => n.readAt === null);
    list.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return opts?.limit ? list.slice(0, opts.limit) : list;
  },

  /** Lấy theo id (dùng cho staff / debug). */
  async getById(id: string): Promise<NotificationDto | null> {
    await sleep(30);
    return notifications.find((n) => n.id === id) ?? null;
  },

  /**
   * Tạo notification. Idempotent theo dedupeKey: nếu đã có row với cùng dedupeKey, trả về row cũ.
   * Đây là điểm neo cho "dispatch lặp không tạo trùng thông báo" (tiêu chí nghiệm thu).
   *
   * Theo spec: notification tạo SAU booking commit. Mock repo này tự gọi scheduleDispatch()
   * để mô phỏng outbox/dispatcher.
   */
  async create(input: {
    accountId: number | null;
    kind: NotificationKind;
    title: string;
    body: string;
    appointmentId: string | null;
    requestId: string | null;
    deepLink: string | null;
    channels?: NotificationChannel[]; // default: ['InApp', 'Email', 'Push']
    dedupeKey: string;
  }): Promise<NotificationDto> {
    await sleep(20);
    const existing = notifications.find((n) => n.dedupeKey === input.dedupeKey);
    if (existing) return { ...existing };
    const channels = input.channels ?? ['InApp', 'Email', 'Push'];
    const dto: NotificationDto = {
      id: nextId(),
      accountId: input.accountId,
      kind: input.kind,
      title: input.title,
      body: input.body,
      createdAt: nowIso(),
      readAt: null,
      appointmentId: input.appointmentId,
      requestId: input.requestId,
      deepLink: input.deepLink,
      attempts: buildAttempts(channels),
      hasExternalFailure: false,
      dedupeKey: input.dedupeKey,
    };
    notifications.unshift(dto);
    // Mô phỏng dispatcher chạy async (không await — UI không cần đợi).
    void this.runDispatch(dto.id);
    return { ...dto };
  },

  /** Đánh dấu đã đọc. KHÔNG đổi lifecycle lịch (tiêu chí nghiệm thu: read không làm đổi lịch). */
  async markRead(id: string, accountId: number): Promise<{ ok: boolean; errorCode?: string }> {
    await sleep(30);
    const n = notifications.find((x) => x.id === id);
    if (!n) return { ok: false, errorCode: 'NOT_FOUND' };
    if (n.accountId !== accountId) return { ok: false, errorCode: 'FORBIDDEN' };
    if (n.readAt === null) n.readAt = nowIso();
    return { ok: true };
  },

  /** Đếm unread cho badge. */
  async countUnread(accountId: number): Promise<number> {
    await sleep(20);
    return notifications.filter((n) => n.accountId === accountId && n.readAt === null).length;
  },

  /**
   * Chạy dispatcher cho notification: thử gửi từng attempt với backoff 1/5/15 phút.
   * Tối đa 3 lần. Lỗi external KHÔNG rollback Appointment — chỉ đánh dấu attempt Failed
   * và tạo staff handling item (nếu external fail toàn bộ).
   */
  async runDispatch(id: string): Promise<void> {
    const n = notifications.find((x) => x.id === id);
    if (!n) return;
    for (const att of n.attempts) {
      if (att.channel === 'InApp') {
        att.status = 'Delivered';
        att.attemptCount = 1;
        att.lastAttemptAt = nowIso();
        att.nextRetryAt = null;
        continue;
      }
      // External: thử 3 lần với backoff.
      for (let i = 0; i < 3; i++) {
        att.attemptCount = i + 1;
        att.lastAttemptAt = nowIso();
        // Trong mock: KHÔNG await thời gian thật — chỉ cập nhật nextRetryAt để UI thấy.
        const result = simulateExternalSend(att.channel, i);
        if (result.ok) {
          att.status = 'Delivered';
          att.nextRetryAt = null;
          att.errorMessage = null;
          break;
        }
        att.status = i === 2 ? 'Failed' : 'Retrying';
        att.errorMessage = result.errorMessage;
        att.nextRetryAt = i < 2 ? new Date(Date.now() + BACKOFF_MINUTES[i] * 60_000).toISOString() : null;
        // Không sleep thật để test nhanh; production sẽ dùng scheduler thật.
        if (i < 2) await sleep(10);
      }
      if (att.status === 'Failed') n.hasExternalFailure = true;
    }
    // Tạo staff handling item nếu external fail toàn bộ (mock: log console).
    if (n.hasExternalFailure && !_handledByStaff.has(n.dedupeKey)) {
      _handledByStaff.add(n.dedupeKey);
      // eslint-disable-next-line no-console
      console.warn(`[notification] ${n.id}: external channels failed after 3 retries — staff handling item created`);
    }
  },

  /**
   * Mô phỏng server-side deep link permission check (S26).
   * UI gọi hàm này trước khi navigate tới deep link. Server re-check quyền lại — UI không
   * được tự tin vào local state.
   */
  async checkDeepLinkPermission(
    deepLink: string,
    accountId: number,
  ): Promise<{ allowed: boolean; reason?: string }> {
    await sleep(40);
    if (!deepLink.startsWith('/appointments/')) return { allowed: false, reason: 'Liên kết không hợp lệ.' };
    const id = deepLink.replace('/appointments/', '');
    const n = notifications.find((x) => x.appointmentId === id && x.accountId === accountId);
    if (!n) return { allowed: false, reason: 'Bạn không có quyền mở thông báo này.' };
    return { allowed: true };
  },

  /**
   * Vô hiệu reminder cũ + tạo reminder mới theo lịch hiện tại (theo spec: booking/change/cancel
   * cập nhật lịch, vô hiệu reminder cũ và lập reminder đúng lịch hiện tại).
   *
   * Mock: chỉ tạo mới với dedupeKey dựa trên appointmentId+version; cũ vẫn tồn tại nhưng
   * version không khớp → staff/inbox filter sẽ ẩn.
   */
  async rebuildRemindersForAppointment(appointmentId: string, version: number): Promise<NotificationDto[]> {
    const created: NotificationDto[] = [];
    for (const offset of [24, 2]) {
      const dto = await this.create({
        accountId: null, // sẽ filter theo appointmentId
        kind: 'ReminderUpcoming',
        title: `Nhắc lịch ${offset}h`,
        body: `Lịch khám sẽ diễn ra trong ${offset} giờ.`,
        appointmentId,
        requestId: null,
        deepLink: `/appointments/${appointmentId}`,
        channels: ['InApp', 'Email', 'Push'],
        dedupeKey: `appt:${appointmentId}:v${version}:reminder:${offset}h`,
      });
      created.push(dto);
    }
    return created;
  },
};

function sleep(ms: number): Promise<void> {
  return new Promise((res) => setTimeout(res, ms));
}
