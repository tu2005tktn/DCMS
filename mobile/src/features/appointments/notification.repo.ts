/**
 * S26 — Notification repo: gọi `/api/notifications`.
 *
 * UI gọi qua `notificationRepo.list/markRead/checkDeepLink/...`; token lấy từ SessionProvider.
 * Lỗi HTTP được đổi sang Error(code) để màn hình dịch sang System Message.
 */

import { ApiError } from '@/services/api/api-error';
import { notificationApi } from './notification.api';
import type {
  CheckDeepLinkRequest,
  CheckDeepLinkResult,
  ListNotificationsQuery,
  ListNotificationsResult,
  MarkReadRequest,
  MarkReadResult,
  UnreadCountResult,
} from './notification.types';

function mapApiError(error: unknown): Error {
  if (error instanceof ApiError) {
    return new Error(error.code || `HTTP_${error.status}`);
  }
  if (error instanceof Error) return error;
  return new Error('NETWORK');
}

export const notificationRepo = {
  async list(_accountId: number, query: ListNotificationsQuery | undefined, token: string | null): Promise<ListNotificationsResult> {
    if (!token) throw new Error('UNAUTHORIZED');
    try {
      return await notificationApi.list(query, token);
    } catch (e) { throw mapApiError(e); }
  },

  async markReadOne(id: string, accountId: number, token: string | null): Promise<{ ok: boolean; errorCode?: string }> {
    if (!token) return { ok: false, errorCode: 'UNAUTHORIZED' };
    try {
      await notificationApi.markReadOne(id, accountId, token);
      return { ok: true };
    } catch (e) {
      return { ok: false, errorCode: mapApiError(e).message };
    }
  },

  async markReadBatch(req: MarkReadRequest, token: string | null): Promise<MarkReadResult> {
    if (!token) return { marked: [], skipped: req.notificationIds };
    try {
      return await notificationApi.markRead(req, token);
    } catch { return { marked: [], skipped: req.notificationIds }; }
  },

  async unreadCount(_accountId: number, token: string | null): Promise<UnreadCountResult> {
    if (!token) return { count: 0 };
    try { return await notificationApi.unreadCount(token); } catch { return { count: 0 }; }
  },

  async checkDeepLink(req: CheckDeepLinkRequest, _accountId: number, token: string | null): Promise<CheckDeepLinkResult> {
    if (!token) return { allowed: false, reason: 'Bạn cần đăng nhập để mở thông báo này.' };
    return notificationApi.checkDeepLink(req, token);
  },
};
