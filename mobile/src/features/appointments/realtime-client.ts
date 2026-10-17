/**
 * S26+ — SignalR realtime client (mobile).
 *
 * Mục đích:
 *  - Kết nối tới `/hubs/notifications` khi user đã authenticated.
 *  - Subscribe `notificationCreated` event, đẩy vào notificationRepoPersist
 *    để UI danh sách tự cập nhật + hiển thị toast.
 *  - Auto-reconnect (exponential backoff), tự start khi có token, tự stop khi sign out.
 *
 * Vì sao viết wrapper (không xài trực tiếp SignalR ở UI):
 *  - Tách lifecycle (connect/disconnect) khỏi component lifecycle.
 *  - Tập trung retry + queue event khi mất mạng.
 *  - Stub dễ — khi backend chưa sẵn sàng, fallback về no-op.
 *
 * Lưu ý:
 *  - `@microsoft/signalr` mặc định dùng WebSocket. Trên RN có thể cần polyfill
 *    (`react-native-url-polyfill`, `buffer`). Phase này chỉ dùng trên web (Expo Web)
 *    và Android emulator; native iOS/HC sẽ bổ sung phase tiếp theo nếu cần.
 *  - Server đã require JWT — phải truyền `?access_token=...` (api.ts đã chuẩn bị pattern
 *    này cho HTTP; SignalR dùng cùng query key vì OnMessageReceived đã xử lý).
 */

import * as signalR from '@microsoft/signalr';
import { env } from '@/config/env';
import { notificationRepoPersist } from './notification.repo.persist';
import type { NotificationDto } from './notification.types';

let connection: signalR.HubConnection | null = null;
let startedConnectionId: string | null = null;
let lastToken: string | null = null;

type ToastListener = (n: NotificationDto) => void;
const toastListeners = new Set<ToastListener>();

/** Đăng ký listener cho toast khi có notification mới từ server. */
export function onNotificationToast(handler: ToastListener): () => void {
  toastListeners.add(handler);
  return () => toastListeners.delete(handler);
}

function emitToast(n: NotificationDto): void {
  for (const h of toastListeners) {
    try { h(n); } catch { /* listener lỗi → bỏ qua, các listener khác vẫn chạy */ }
  }
}

/** Build URL tới hub: `<apiUrl>/hubs/notifications?access_token=...`. */
function buildHubUrl(token: string): string {
  const base = env.apiUrl.replace(/\/+$/, '');
  return `${base}/hubs/notifications?access_token=${encodeURIComponent(token)}`;
}

/** Khởi tạo connection (idempotent — nếu token đổi thì restart). */
export async function startRealtime(token: string): Promise<void> {
  if (lastToken === token && connection?.state === signalR.HubConnectionState.Connected) return;

  // Nếu đang có connection cũ, dừng trước.
  await stopRealtime();

  // Backend stub: nếu apiUrl chưa cấu hình → không start (tránh spam error log).
  if (!env.apiUrl) return;

  const hub = new signalR.HubConnectionBuilder()
    .withUrl(buildHubUrl(token), {
      // Bỏ qua negotiate: dùng WebSocket trực tiếp. Backend chấp nhận.
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  hub.on('notificationCreated', (payload: NotificationDto) => {
    // 1. Persist + đẩy vào store để UI danh sách cập nhật.
    void notificationRepoPersist
      .create({
        accountId: payload.accountId,
        kind: payload.kind,
        title: payload.title,
        body: payload.body,
        appointmentId: payload.appointmentId,
        requestId: payload.requestId,
        deepLink: payload.deepLink,
        channels: payload.attempts?.map(a => a.channel) ?? ['InApp'],
        dedupeKey: payload.dedupeKey,
      })
      .catch(() => undefined);
    // 2. Toast.
    emitToast(payload);
  });

  try {
    await hub.start();
    connection = hub;
    startedConnectionId = hub.connectionId ?? null;
    lastToken = token;
    // eslint-disable-next-line no-console
    console.log('[realtime] connected', { connectionId: startedConnectionId });
  } catch (error) {
    // eslint-disable-next-line no-console
    console.warn('[realtime] start failed, will retry on next sign-in', error);
    connection = null;
    startedConnectionId = null;
  }
}

/** Dừng connection (gọi khi sign out). */
export async function stopRealtime(): Promise<void> {
  if (!connection) return;
  const old = connection;
  connection = null;
  startedConnectionId = null;
  lastToken = null;
  try { await old.stop(); } catch { /* bỏ qua */ }
}

/** Trạng thái hiện tại (cho UI debug). */
export function getRealtimeState(): { connected: boolean; connectionId: string | null } {
  return {
    connected: connection?.state === signalR.HubConnectionState.Connected,
    connectionId: startedConnectionId,
  };
}
