/**
 * S26 — Notification repo (adapter: mock ↔ real API).
 *
 * UI gọi qua `notificationRepo.list/markRead/checkDeepLink/...`. Bên trong tự chọn
 * implementation dựa trên `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API`.
 *
 * Mặc định dùng API thật; đặt `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=false` chỉ khi
 * cần demo offline. Token được lấy từ SessionProvider.
 *
 * Các method đều nhận `token` từ SessionProvider để truyền cho `authedRequest`.
 */

import { ApiError } from '@/services/api/api-error';
import { notificationApi } from './notification.api';
import { notificationMockRepo } from './notification.mock';
import type {
  CheckDeepLinkRequest,
  CheckDeepLinkResult,
  ListNotificationsQuery,
  ListNotificationsResult,
  MarkReadRequest,
  MarkReadResult,
  UnreadCountResult,
} from './notification.types';

const USE_REAL = process.env.EXPO_PUBLIC_USE_REAL_NOTIFICATION_API !== 'false';

function mapApiError(error: unknown): Error {
  if (error instanceof ApiError) {
    return new Error(error.code ?? `HTTP_${error.status}`);
  }
  if (error instanceof Error) return error;
  return new Error('NETWORK');
}

export const notificationRepo = {
  async list(accountId: number, query: ListNotificationsQuery | undefined, token: string | null): Promise<ListNotificationsResult> {
    if (USE_REAL) {
      if (!token) throw new Error('UNAUTHORIZED');
      try {
        return await notificationApi.list(query, token);
      } catch (e) { throw mapApiError(e); }
    }
    // mock
    const items = await notificationMockRepo.listForAccount(accountId, {
      unreadOnly: query?.unreadOnly,
      limit: query?.limit,
    });
    return { items, nextCursor: null };
  },

  async markReadOne(id: string, accountId: number, token: string | null): Promise<{ ok: boolean; errorCode?: string }> {
    if (USE_REAL) {
      if (!token) return { ok: false, errorCode: 'UNAUTHORIZED' };
      try {
        await notificationApi.markReadOne(id, accountId, token);
        return { ok: true };
      } catch (e) {
        const m = mapApiError(e);
        return { ok: false, errorCode: m.message };
      }
    }
    return notificationMockRepo.markRead(id, accountId);
  },

  async markReadBatch(req: MarkReadRequest, token: string | null): Promise<MarkReadResult> {
    if (USE_REAL) {
      if (!token) return { marked: [], skipped: req.notificationIds };
      try {
        return await notificationApi.markRead(req, token);
      } catch { return { marked: [], skipped: req.notificationIds }; }
    }
    // mock: gọi từng cái
    const marked: string[] = [];
    const skipped: string[] = [];
    for (const id of req.notificationIds) {
      const r = await notificationMockRepo.markRead(id, req.expectedAccountId);
      if (r.ok) marked.push(id); else skipped.push(id);
    }
    return { marked, skipped };
  },

  async unreadCount(accountId: number, token: string | null): Promise<UnreadCountResult> {
    if (USE_REAL) {
      if (!token) return { count: 0 };
      try { return await notificationApi.unreadCount(token); } catch { return { count: 0 }; }
    }
    return { count: await notificationMockRepo.countUnread(accountId) };
  },

  async checkDeepLink(req: CheckDeepLinkRequest, accountId: number, token: string | null): Promise<CheckDeepLinkResult> {
    if (USE_REAL) {
      if (!token) return { allowed: false, reason: 'Bạn cần đăng nhập để mở thông báo này.' };
      return notificationApi.checkDeepLink(req, token);
    }
    return notificationMockRepo.checkDeepLinkPermission(req.deepLink, accountId);
  },
};

/** Export cờ để UI debug / hiển thị badge "(mock)". */
export const NOTIFICATION_BACKEND = USE_REAL ? 'real' : 'mock';
