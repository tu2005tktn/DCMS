/**
 * Notification mock persistence layer — S26+ fix bug "thông báo biến mất khi reload".
 *
 * Tương tự appointment.repo: wrap `notificationMockRepo` để persist toàn bộ store qua
 * AsyncStorage. Hydrate lúc mount, ghi lại sau mỗi mutation.
 *
 * Khi backend TV5 có, UI chuyển sang dùng real API (`notificationRepo` cũ) — file này
 * vẫn giữ như fallback offline.
 */

import { persistentStore } from '@/services/storage/persistent-store';
import { notificationMockRepo } from './notification.mock';
import type { NotificationDto, NotificationChannel, NotificationKind } from './appointment.types';

const STORE_KEY = persistentStore.keys.notifications;

let _hydrated = false;
let _hydratePromise: Promise<void> | null = null;

async function hydrate(): Promise<void> {
  if (_hydrated) return;
  if (_hydratePromise) return _hydratePromise;
  _hydratePromise = (async () => {
    const raw = await persistentStore.getItem(STORE_KEY);
    if (!raw) {
      _hydrated = true;
      return;
    }
    try {
      const list: NotificationDto[] = JSON.parse(raw);
      if (Array.isArray(list) && list.length > 0) {
        for (const n of list) _injected.set(n.id, n);
      }
    } catch (e) {
      // eslint-disable-next-line no-console
      console.warn('[notificationRepoPersist] hydrate failed:', e);
    } finally {
      _hydrated = true;
    }
  })();
  return _hydratePromise;
}

const _injected = new Map<string, NotificationDto>();

async function persistAll(): Promise<void> {
  try {
    const all = Array.from(_injected.values());
    await persistentStore.setItem(STORE_KEY, JSON.stringify(all));
  } catch (e) {
    // eslint-disable-next-line no-console
    console.warn('[notificationRepoPersist] persist failed:', e);
  }
}

export const notificationRepoPersist = {
  async hydrate(): Promise<void> {
    await hydrate();
  },

  async listForAccount(accountId: number, opts?: { unreadOnly?: boolean; limit?: number }): Promise<NotificationDto[]> {
    await hydrate();
    const seeded = await notificationMockRepo.listForAccount(accountId, opts);
    // Ưu tiên injected (mới hơn).
    const map = new Map<string, NotificationDto>();
    for (const n of seeded) map.set(n.id, n);
    for (const n of _injected.values()) {
      if (n.accountId === accountId) {
        if (opts?.unreadOnly && n.readAt !== null) continue;
        map.set(n.id, n);
      }
    }
    const list = Array.from(map.values()).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return opts?.limit ? list.slice(0, opts.limit) : list;
  },

  /**
   * Tạo notification mới và persist. Đây là API BookingScreen sẽ gọi ngay sau khi book
   * thành công (fix bug 2).
   */
  async create(input: {
    accountId: number | null;
    kind: NotificationKind;
    title: string;
    body: string;
    appointmentId: string | null;
    requestId: string | null;
    deepLink: string | null;
    channels?: NotificationChannel[];
    dedupeKey: string;
  }): Promise<NotificationDto> {
    await hydrate();
    // Idempotent: nếu đã tồn tại dedupeKey thì trả về bản cũ.
    for (const n of _injected.values()) {
      if (n.dedupeKey === input.dedupeKey) return { ...n };
    }
    const created = await notificationMockRepo.create(input);
    _injected.set(created.id, created);
    await persistAll();
    return { ...created };
  },

  async markRead(id: string, accountId: number): Promise<{ ok: boolean; errorCode?: string }> {
    await hydrate();
    if (_injected.has(id)) {
      const n = _injected.get(id)!;
      if (n.accountId !== accountId) return { ok: false, errorCode: 'FORBIDDEN' };
      if (n.readAt === null) {
        n.readAt = new Date().toISOString();
        await persistAll();
      }
      return { ok: true };
    }
    return notificationMockRepo.markRead(id, accountId);
  },

  async countUnread(accountId: number): Promise<number> {
    await hydrate();
    let c = 0;
    for (const n of _injected.values()) {
      if (n.accountId === accountId && n.readAt === null) c++;
    }
    return c + (await notificationMockRepo.countUnread(accountId));
  },

  async checkDeepLinkPermission(
    deepLink: string,
    accountId: number,
  ): Promise<{ allowed: boolean; reason?: string }> {
    await hydrate();
    return notificationMockRepo.checkDeepLinkPermission(deepLink, accountId);
  },

  async clearLocal(): Promise<void> {
    _injected.clear();
    await persistentStore.removeItem(STORE_KEY);
  },
};
