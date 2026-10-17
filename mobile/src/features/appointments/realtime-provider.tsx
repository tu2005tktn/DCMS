/**
 * S26+ — RealtimeProvider.
 *
 * Quản lý lifecycle SignalR connection dựa trên session:
 *  - Khi `state.status === 'authenticated'`: khởi tạo (hoặc restart nếu token đổi).
 *  - Khi sign out / error / anonymous: dừng connection.
 *
 * Phải nằm TRONG SessionProvider trong React tree.
 */

import { useEffect } from 'react';
import { startRealtime, stopRealtime } from './realtime-client';
import { useSession } from '@/features/auth/session/useSession';

/** Internal hook — quản lý start/stop. Tách riêng để tránh re-render không cần thiết. */
function useRealtimeLifecycle() {
  const { state } = useSession();

  // Khi session thay đổi → quyết định start/stop.
  useEffect(() => {
    if (state.status === 'authenticated' && state.accessToken) {
      void startRealtime(state.accessToken);
    } else {
      void stopRealtime();
    }
  }, [state.status, state.status === 'authenticated' ? state.accessToken : undefined]);

  // Cleanup khi unmount.
  useEffect(() => {
    return () => { void stopRealtime(); };
  }, []);
}

/**
 * RealtimeProvider — bọc app để SignalR chạy bên dưới.
 *
 * Usage:
 *   <SessionProvider>
 *     <RealtimeProvider>
 *       <App />
 *     </RealtimeProvider>
 *   </SessionProvider>
 *
 * Component này không render gì (null), chỉ mount side-effect.
 * Dùng `onNotificationToast` để nhận event từ bên trong app.
 */
export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  useRealtimeLifecycle();
  return <>{children}</>;
}
