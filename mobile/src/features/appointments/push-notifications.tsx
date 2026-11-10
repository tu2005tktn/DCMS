import { useEffect } from 'react';
import { Platform } from 'react-native';
import { router } from 'expo-router';
import * as Notifications from 'expo-notifications';
import { useSession } from '@/features/auth/session/useSession';
import { authedRequest } from '@/services/api/http-client';

let registeredToken: string | null = null;
let registeredAccessToken: string | null = null;

if (Platform.OS === 'android') {
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldShowBanner: true,
      shouldShowList: true,
      shouldPlaySound: true,
      shouldSetBadge: false,
    }),
  });
}

export async function unregisterPushDevice(accessToken: string): Promise<void> {
  if (!registeredToken || Platform.OS !== 'android') return;
  const token = registeredToken;
  await authedRequest('/api/notifications/devices', accessToken, {
    method: 'DELETE',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token }),
  });
  registeredToken = null;
  registeredAccessToken = null;
}

export function PushNotificationsProvider() {
  const { state } = useSession();
  const accessToken = state.status === 'authenticated' ? state.accessToken : null;

  useEffect(() => {
    if (Platform.OS !== 'android' || !accessToken) return;
    let cancelled = false;
    let pendingToken: string | null = null;
    const syncToken = async (nativeToken: string) => {
      if (cancelled || !nativeToken || pendingToken === nativeToken) return;
      if (registeredToken === nativeToken && registeredAccessToken === accessToken) return;
      pendingToken = nativeToken;
      try {
        await authedRequest('/api/notifications/devices', accessToken, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token: nativeToken, platform: 'android' }),
        });
        if (!cancelled) {
          registeredToken = nativeToken;
          registeredAccessToken = accessToken;
        }
      } finally {
        pendingToken = null;
      }
    };
    const register = async () => {
      await Notifications.setNotificationChannelAsync('appointments', {
        name: 'Lịch hẹn và hồ sơ',
        importance: Notifications.AndroidImportance.HIGH,
      });
      const current = await Notifications.getPermissionsAsync();
      const permission = current.granted ? current : await Notifications.requestPermissionsAsync();
      if (!permission.granted || cancelled) return;
      const nativeToken = (await Notifications.getDevicePushTokenAsync()).data;
      if (typeof nativeToken !== 'string' || cancelled) return;
      await syncToken(nativeToken);
    };
    void register().catch(() => undefined);
    const tokenListener = Notifications.addPushTokenListener(token => {
      if (typeof token.data === 'string') void syncToken(token.data).catch(() => undefined);
    });
    return () => { cancelled = true; tokenListener.remove(); };
  }, [accessToken]);

  useEffect(() => {
    if (Platform.OS !== 'android' || !accessToken) return;
    const open = () => {
      router.push('/notifications');
      void Notifications.clearLastNotificationResponseAsync();
    };
    const listener = Notifications.addNotificationResponseReceivedListener(open);
    void Notifications.getLastNotificationResponseAsync().then(response => {
      if (response) open();
    });
    return () => listener.remove();
  }, [accessToken]);

  return null;
}
