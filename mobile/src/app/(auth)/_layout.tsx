/**
 * (auth) layout — nếu đã authenticated thì tự đẩy về (account).
 * Bảo vệ ngược: không cho vào màn hình auth khi đã đăng nhập.
 */

import { Redirect, Stack } from 'expo-router';
import { ActivityIndicator, View, StyleSheet } from 'react-native';
import { useSession } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';

export default function AuthLayout() {
  const { state } = useSession();
  if (state.status === 'initializing') {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }
  if (state.status === 'authenticated') {
    // Đã đăng nhập mà lại vào route thuộc nhóm (auth) -> đẩy về menu trung tâm.
    // Ở đó user sẽ thấy mục "Trang chủ tài khoản" mới xuất hiện.
    return <Redirect href="/(home)/" />;
  }
  return <Stack screenOptions={{ headerTitle: 'Tài khoản' }} />;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
});
