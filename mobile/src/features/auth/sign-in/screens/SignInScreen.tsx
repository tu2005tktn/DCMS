import { Link } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

export default function SignInScreen() {
  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={styles.eyebrow}>DCMS · Phòng khám nha khoa</Text>
      <Text style={styles.title}>Đăng nhập</Text>
      <Text style={styles.intro}>Truy cập các chức năng theo vai trò được cấp cho tài khoản của bạn.</Text>
      <View style={styles.card}>
        <Text style={styles.note}>
          Màn hình đăng nhập S01 dành cho team S01 triển khai theo đặc tả.
        </Text>
      </View>
      <Link href="/(auth)/recover-account" style={styles.link}>
        Quên mật khẩu? Khôi phục tài khoản
      </Link>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: spacing.lg,
    gap: spacing.md,
    backgroundColor: colors.background,
    flexGrow: 1,
  },
  eyebrow: {
    color: colors.primary,
    fontSize: typography.caption,
    fontWeight: '700',
    letterSpacing: 1,
  },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  intro: { fontSize: typography.body, color: colors.muted, lineHeight: 24 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: '#E4E9F2',
    padding: spacing.md,
  },
  note: { fontSize: 14, color: colors.muted, lineHeight: 21 },
  link: { color: colors.primary, fontSize: 15, fontWeight: '600', paddingVertical: spacing.md },
});
