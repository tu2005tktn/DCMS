import { Link, type Href } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';

const routes: { label: string; href: Href }[] = [
  { label: 'S01 · Đăng nhập', href: '/(auth)/sign-in' },
  { label: 'S02 · Đăng ký', href: '/(auth)/register' },
  { label: 'S03 · Khôi phục tài khoản', href: '/(auth)/recover-account' },
  { label: 'S03 · Đặt lại mật khẩu', href: '/(auth)/reset-password' },
  { label: 'S07 · Liên kết tài khoản', href: '/(account)/account-link' },
  { label: 'Trang chủ tài khoản', href: '/(account)/home' },
];
export function PlaceholderScreen({ title, description }: { title: string; description: string }) {
  return <ScrollView contentContainerStyle={styles.container}>
    <Text style={styles.title}>{title}</Text>
    <Text>{description}</Text>
    <Text style={styles.note}>Placeholder — chức năng đang chờ triển khai.</Text>
    <View style={styles.links}>{routes.map(route => <Link key={route.href.toString()} href={route.href} style={styles.link}>{route.label}</Link>)}</View>
  </ScrollView>;
}
const styles = StyleSheet.create({
  container: { padding: spacing.lg, gap: spacing.md, backgroundColor: colors.background, flexGrow: 1 },
  title: { fontSize: 24, fontWeight: '600', color: colors.text },
  note: { color: colors.muted },
  links: { gap: spacing.md },
  link: { color: colors.primary, paddingVertical: spacing.sm },
});
