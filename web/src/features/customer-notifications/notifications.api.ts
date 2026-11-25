import * as signalR from '@microsoft/signalr';
import { customerApi } from '../../services/customerApi';

// S26 web customer notifications (docs/specs/S26-CUSTOMER-NOTIFICATIONS-WEB.vi.md).

export type CustomerNotification = {
  id: string;
  kind: string;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
  appointmentId: string | null;
  requestId: string | null;
  deepLink: string | null;
  hasExternalFailure: boolean;
};

export type NotificationPage = { items: CustomerNotification[]; nextCursor: string | null };

const json = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) });

export const customerNotificationsApi = {
  list(token: string, unreadOnly: boolean, cursor: string | null) {
    const query = new URLSearchParams({ limit: '30' });
    if (unreadOnly) query.set('unreadOnly', 'true');
    if (cursor) query.set('cursor', cursor);
    return customerApi<NotificationPage>(`/api/notifications?${query}`, {}, token);
  },
  unreadCount: (token: string) => customerApi<{ count: number }>('/api/notifications/unread-count', {}, token),
  markReadOne: (token: string, id: string, accountId: number) =>
    customerApi<{ ok: boolean }>(`/api/notifications/${encodeURIComponent(id)}/mark-read`, json({ expectedAccountId: accountId }), token),
  markRead: (token: string, ids: string[], accountId: number) =>
    customerApi<{ marked: string[]; skipped: string[] }>('/api/notifications/mark-read',
      json({ notificationIds: ids.slice(0, 100), expectedAccountId: accountId }), token),
  checkDeepLink: (token: string, deepLink: string) =>
    customerApi<{ allowed: boolean; reason: string | null }>('/api/notifications/check-deep-link', json({ deepLink }), token),
};

/** Maps a server deep link (mobile route shape) to the customer web route; null = nothing to open. */
export function webRouteFor(deepLink: string | null): string | null {
  if (!deepLink) return null;
  if (deepLink === '/appointments') return '/customer/appointments';
  if (deepLink === '/prescriptions') return '/customer/prescriptions';
  if (deepLink === '/book-appointment') return '/customer/book';
  const [kind, id, ...rest] = deepLink.split('/').filter(Boolean);
  if (!id || rest.length > 0) return null;
  const safe = encodeURIComponent(decodeURIComponent(id));
  switch (kind) {
    case 'appointments': return `/customer/appointments/${safe}`;
    case 'prescriptions': return `/customer/prescriptions/${safe}`;
    case 'visit-results':
    case 'medical-records': return '/customer/treatment-records';
    default: return null;
  }
}

const apiBase = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');

/** `notificationCreated` is only a refresh signal; callers re-read through the API. */
export function subscribeNotifications(token: string, onSignal: () => void): () => void {
  let disposed = false;
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${apiBase}/hubs/notifications`, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
  connection.on('notificationCreated', () => { if (!disposed) onSignal(); });
  connection.onreconnected(() => { if (!disposed) onSignal(); });
  void connection.start().catch(() => undefined);
  return () => { disposed = true; void connection.stop().catch(() => undefined); };
}
