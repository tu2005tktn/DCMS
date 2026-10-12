/**
 * (account) layout — chỉ cho authenticated user.
 *
 * Theo đặc tả S01: route bảo vệ + menu theo quyền. AccountLinkScreen là entry mặc định;
 * các menu khác (admin, lâm sàng) sẽ thêm trong từng use case tương ứng.
 */

import { Stack } from 'expo-router';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import { useAuthGuard } from '@/features/auth/session/useAuthGuard';
import { colors } from '@/theme/colors';

export default function AccountLayout() {
  const { ready, authorized } = useAuthGuard();
  if (!ready) {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }
  if (!authorized) return null;
  return <Stack screenOptions={{ headerTitle: 'Tài khoản' }} />;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
});
