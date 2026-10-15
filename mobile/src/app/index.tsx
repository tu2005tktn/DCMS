/**
 * App entry — điều hướng theo trạng thái phiên.
 *  - Đang khởi tạo: chờ.
 *  - Đã đăng nhập: vào trang chủ tài khoản (có nút đăng xuất).
 *  - Khách: vào màn hình đăng nhập.
 */

import { Redirect } from 'expo-router';
import { ActivityIndicator, StyleSheet, View } from 'react-native';
import { useSessionState } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';

export default function IndexRoute() {
  const state = useSessionState();
  if (state.status === 'initializing') {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }
  if (state.status === 'authenticated') {
    return <Redirect href="/(account)/home" />;
  }
  return <Redirect href="/(auth)/sign-in" />;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
});
