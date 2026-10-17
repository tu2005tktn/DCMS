/**
 * S26 — Notification HTTP client (real API).
 *
 * Contract-first: gọi endpoint backend TV5 theo spec S26. Khi backend chưa sẵn sàng,
 * bật `EXPO_PUBLIC_USE_REAL_NOTIFICATION_API=false` (mặc định) để dùng mock.
 *
 * Endpoint contract (sẽ thống nhất với backend TV5):
 *
 *   GET    /api/notifications?unreadOnly=&limit=&cursor=
 *          → 200 { items: NotificationDto[], nextCursor: string | null }
 *          → 401 MSG07, 403 MSG06
 *
 *   POST   /api/notifications/mark-read
 *          Body: MarkReadRequest
 *          → 200 MarkReadResult
 *          → 401/403/409 (stale version → MSG111)
 *
 *   GET    /api/notifications/unread-count
 *          → 200 { count: number }
 *
 *   POST   /api/notifications/check-deep-link
 *          Body: CheckDeepLinkRequest
 *          → 200 CheckDeepLinkResult
 *
 *   POST   /api/notifications/{id}/mark-read
 *          Body: { expectedAccountId: number }
 *          → 200 { ok: true }
 *          → 409 MSG111 nếu id không thuộc account
 *
 * Lỗi server trả {code, message} theo convention chung; UI dịch sang System Message.
 *
 * KHÔNG log body request/response vì có thể chứa token / thông tin cá nhân.
 */

import { ApiError } from '@/services/api/api-error';
import { authedRequest } from '@/services/api/http-client';
import type {
  CheckDeepLinkRequest,
  CheckDeepLinkResult,
  ListNotificationsQuery,
  ListNotificationsResult,
  MarkReadRequest,
  MarkReadResult,
  UnreadCountResult,
} from './notification.types';

function buildQuery(q: ListNotificationsQuery | undefined): string {
  if (!q) return '';
  const sp = new URLSearchParams();
  if (q.unreadOnly) sp.set('unreadOnly', 'true');
  if (q.limit !== undefined) sp.set('limit', String(q.limit));
  if (q.cursor) sp.set('cursor', q.cursor);
  const s = sp.toString();
  return s ? `?${s}` : '';
}

export const notificationApi = {
  /** Lấy notification của account hiện tại (S26). */
  async list(query: ListNotificationsQuery | undefined, token: string): Promise<ListNotificationsResult> {
    const r = await authedRequest(`/api/notifications${buildQuery(query)}`, token, { method: 'GET' });
    return (await r.json()) as ListNotificationsResult;
  },

  /** Mark nhiều notification là đã đọc (tối đa 100 theo tiêu chí nghiệm thu). */
  async markRead(req: MarkReadRequest, token: string): Promise<MarkReadResult> {
    const r = await authedRequest('/api/notifications/mark-read', token, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    return (await r.json()) as MarkReadResult;
  },

  /** Mark 1 notification (tiện cho tap-to-read). */
  async markReadOne(id: string, expectedAccountId: number, token: string): Promise<{ ok: boolean }> {
    const r = await authedRequest(`/api/notifications/${encodeURIComponent(id)}/mark-read`, token, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ expectedAccountId }),
    });
    return (await r.json()) as { ok: boolean };
  },

  /** Count unread cho badge. */
  async unreadCount(token: string): Promise<UnreadCountResult> {
    const r = await authedRequest('/api/notifications/unread-count', token, { method: 'GET' });
    return (await r.json()) as UnreadCountResult;
  },

  /**
   * Server-side permission check trước khi navigate tới deep link.
   * UI KHÔNG được tự tin vào local state — phải hỏi server (S26).
   */
  async checkDeepLink(req: CheckDeepLinkRequest, token: string): Promise<CheckDeepLinkResult> {
    try {
      const r = await authedRequest('/api/notifications/check-deep-link', token, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      });
      return (await r.json()) as CheckDeepLinkResult;
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        return { allowed: false, reason: 'Phiên đã hết hạn hoặc bạn không có quyền.' };
      }
      if (error instanceof ApiError && error.status === 404) {
        return { allowed: false, reason: 'Liên kết không tồn tại.' };
      }
      return { allowed: false, reason: 'Không thể kiểm tra quyền. Vui lòng thử lại.' };
    }
  },
};
