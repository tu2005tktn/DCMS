/**
 * Root layout.
 *
 * Thay đổi so với scaffold:
 *  - Dùng headerTitle tiếng Việt thân thiện.
 *  - SessionProvider đã có auto-refresh + hydrate (xem SessionProvider.tsx).
 *  - Có thêm (account)/_layout và (auth)/_layout để chặn route ngược.
 */

import { Stack } from 'expo-router';
import { SessionProvider } from '@/features/auth/session/SessionProvider';

export default function RootLayout() {
  return (
    <SessionProvider>
      <Stack>
        <Stack.Screen name="index" options={{ title: 'DCMS Mobile' }} />
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
        <Stack.Screen name="(account)" options={{ headerShown: false }} />
        <Stack.Screen name="book-appointment" options={{ title: 'Đặt lịch khám' }} />
      </Stack>
    </SessionProvider>
  );
}
