/**
 * Root layout.
 *
 * Thay đổi so với scaffold:
 *  - Dùng headerTitle tiếng Việt thân thiện.
 *  - SessionProvider đã có auto-refresh + hydrate (xem SessionProvider.tsx).
 *  - Có thêm (account)/_layout và (auth)/_layout để chặn route ngược.
 *  - (home) là menu trung tâm — mọi màn hình được liệt kê ở đây để tester điều hướng
 *    nhanh; mục "Trang chủ tài khoản" chỉ xuất hiện khi đã authenticated.
 *  - S26+: hydrate persist store (AsyncStorage) lúc boot để data local sẵn sàng trước
 *    khi user mở các màn Appointments / Notifications.
 *  - S26+: RealtimeProvider (bên trong SessionProvider) tự start/stop SignalR theo session.
 *  - S26+: RealtimeToast hiển thị banner khi nhận notification mới từ server.
 */

import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { SessionProvider } from '@/features/auth/session/SessionProvider';
import { appointmentRepo } from '@/features/appointments/appointment.repo';
import { notificationRepoPersist } from '@/features/appointments/notification.repo.persist';
import { RealtimeProvider } from '@/features/appointments/realtime-provider';
import { RealtimeToast } from '@/features/appointments/realtime-toast';

export default function RootLayout() {
  useEffect(() => {
    void appointmentRepo.hydrate();
    void notificationRepoPersist.hydrate();
  }, []);
  return (
    <SessionProvider>
      <RealtimeProvider>
        <Stack>
          <Stack.Screen name="(home)" options={{ headerShown: false }} />
          <Stack.Screen name="(auth)" options={{ headerShown: false }} />
          <Stack.Screen name="(account)" options={{ headerShown: false }} />
          <Stack.Screen name="book-appointment" options={{ title: 'Đặt lịch khám' }} />
          <Stack.Screen name="index" options={{ headerShown: false }} />
          <Stack.Screen name="+not-found" options={{ title: 'Không tìm thấy' }} />
        </Stack>
        {/* S26+ — toast hiển thị trên mọi màn hình. Đặt ngoài Stack để z-index cao nhất. */}
        <RealtimeToast />
      </RealtimeProvider>
    </SessionProvider>
  );
}
