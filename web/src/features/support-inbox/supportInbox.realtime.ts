import * as signalR from '@microsoft/signalr';
import { readAccessToken } from '../../services/api';

const apiBase = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');

export type SupportRequestUpdatedEvent = {
  requestId: string;
  appointmentId: string;
  branchId: number;
  status: string;
  version: number;
  action: string;
  updatedAt: string;
};

export type RealtimeState = 'connecting' | 'connected' | 'reconnecting' | 'offline';

/**
 * Subscribe to branch-scoped support invalidation events.
 * The payload is deliberately not used as authoritative UI state: callers refetch through
 * the permission-checked API, and duplicate events are coalesced into one refresh.
 */
export function subscribeSupportInbox(
  onUpdate: (event: SupportRequestUpdatedEvent) => void,
  onRestored: () => void,
  onState: (state: RealtimeState) => void,
): () => void {
  let disposed = false;
  let retryTimer: number | undefined;
  let refreshTimer: number | undefined;
  let queuedEvent: SupportRequestUpdatedEvent | undefined;
  const latestVersion = new Map<string, number>();

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${apiBase}/hubs/notifications`, {
      accessTokenFactory: () => readAccessToken() ?? '',
    })
    .withAutomaticReconnect([0, 1_000, 5_000, 15_000, 30_000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const flush = () => {
    refreshTimer = undefined;
    if (!disposed && queuedEvent) onUpdate(queuedEvent);
    queuedEvent = undefined;
  };

  connection.on('supportRequestUpdated', (event: SupportRequestUpdatedEvent) => {
    const seen = latestVersion.get(event.requestId) ?? 0;
    if (event.version <= seen) return;
    latestVersion.set(event.requestId, event.version);
    queuedEvent = event;
    if (refreshTimer !== undefined) window.clearTimeout(refreshTimer);
    refreshTimer = window.setTimeout(flush, 150);
  });
  connection.onreconnecting(() => onState('reconnecting'));
  connection.onreconnected(() => {
    onState('connected');
    onRestored();
  });
  connection.onclose(() => {
    if (disposed) return;
    onState('offline');
    retryTimer = window.setTimeout(() => { void start(); }, 15_000);
  });

  async function start() {
    if (disposed || connection.state !== signalR.HubConnectionState.Disconnected) return;
    onState('connecting');
    try {
      await connection.start();
      if (!disposed) onState('connected');
    } catch {
      if (disposed) return;
      onState('offline');
      retryTimer = window.setTimeout(() => { void start(); }, 5_000);
    }
  }

  void start();
  return () => {
    disposed = true;
    if (retryTimer !== undefined) window.clearTimeout(retryTimer);
    if (refreshTimer !== undefined) window.clearTimeout(refreshTimer);
    connection.off('supportRequestUpdated');
    void connection.stop();
  };
}
