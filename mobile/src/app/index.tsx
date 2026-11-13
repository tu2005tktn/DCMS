/** App entry: restore the session before choosing sign-in or the authenticated home. */

import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { useSessionState } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';

export default function IndexRoute() {
  const state = useSessionState();
  // Chờ hydrate xong (SessionProvider đang gọi /me) trước khi redirect, tránh flash màn hình
  // sai. SessionProvider đã có auto-refresh nên trạng thái ổn định trong vài trăm ms.
  if (state.status === 'initializing') {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }
  return <Redirect href={state.status === 'authenticated' ? '/(home)/' : '/(auth)/sign-in'} />;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
});
