/**
 * Màn hình menu trung tâm — liệt kê tất cả các screen hiện có trong app.
 *
 * Mục đích:
 *  - Cho tester/dev điều hướng nhanh tới từng màn hình khi chưa có dashboard chính thức.
 *  - Hiển thị các lối tắt chức năng (đăng nhập, trang chủ tài khoản) tuỳ trạng thái phiên:
 *      + Chưa đăng nhập: ẩn "Trang chủ tài khoản", chỉ hiện "Đăng nhập".
 *      + Đã đăng nhập:  hiện "Trang chủ tài khoản", ẩn "Đăng nhập" (vì trong phiên hiện tại
 *        đã có phiên đang hiệu lực — sign-in thêm sẽ xung đột).
 *      + Đang tải / lỗi mạng: ẩn cả hai, chờ SessionProvider hydrate xong.
 *
 * Khi UC-31 có dashboard chính thức, route này sẽ thay bằng dashboard.
 */

import { Link, type Href } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSessionState } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

interface MenuItem {
  label: string;
  description: string;
  href: Href;
  /** Nếu set, chỉ hiển thị khi session.status khớp giá trị. */
  requireAuth?: 'authenticated' | 'anonymous';
  /** Nếu set, ẩn khi session.status khớp giá trị. */
  hideWhenAuth?: 'authenticated' | 'anonymous';
}

interface MenuGroup {
  title: string;
  items: MenuItem[];
}

const GROUPS: MenuGroup[] = [
  {
    title: 'Điều hướng chính',
    items: [
      {
        label: 'Đăng nhập (S01)',
        description: 'Tạo phiên mới cho tài khoản DCMS.',
        href: '/(auth)/sign-in',
        hideWhenAuth: 'authenticated',
      },
      {
        label: 'Trang chủ tài khoản',
        description: 'Thông tin người dùng hiện tại và nút đăng xuất.',
        href: '/(account)/home',
        requireAuth: 'authenticated',
      },
      {
        label: 'Lịch hẹn của tôi (S12)',
        description: 'Xem code, lifecycle, booker/attendee, lịch sử và trạng thái yêu cầu hỗ trợ.',
        href: '/(home)/appointments',
        requireAuth: 'authenticated',
      },
      {
        label: 'Thông báo (S26)',
        description: 'Thông báo lịch hẹn của tài khoản hiện tại. Mở deep link sẽ kiểm quyền lại.',
        href: '/(home)/notifications',
        requireAuth: 'authenticated',
      },
    ],
  },
  {
    title: 'S12 · Staff (UC-05/06 + TV4)',
    items: [
      { label: 'Hộp thư hỗ trợ', description: 'Staff xem & xử lý yêu cầu đổi/huỷ/xác nhận từ booker & attendee.', href: '/(home)/staff/requests' },
      { label: 'Lịch bị ảnh hưởng (giảm giờ)', description: 'UC-03 S09: lịch Confirmed/Rescheduled nằm trong khung giờ sẽ thu hẹp.', href: '/(home)/staff/schedule-impact' },
    ],
  },
  {
    title: 'S01 · Đăng nhập',
    items: [
      { label: 'S01 · Đăng nhập', description: 'Màn hình chính của luồng S01.', href: '/(auth)/sign-in' },
    ],
  },
  {
    title: 'S02 · Đăng ký',
    items: [
      { label: 'S02 · Đăng ký', description: 'Tạo tài khoản Public mới.', href: '/(auth)/register' },
      { label: 'S02 · Xác minh liên hệ', description: 'Nhập mã OTP xác minh email/điện thoại.', href: '/(auth)/verify-contact' },
    ],
  },
  {
    title: 'S03 · Khôi phục',
    items: [
      { label: 'S03 · Khôi phục tài khoản', description: 'Bắt đầu luồng quên mật khẩu.', href: '/(auth)/recover-account' },
      { label: 'S03 · Đặt lại mật khẩu', description: 'Nhập mật khẩu mới sau khi xác minh.', href: '/(auth)/reset-password' },
    ],
  },
  {
    title: 'S07 · Liên kết tài khoản',
    items: [
      { label: 'S07 · Liên kết tài khoản', description: 'Màn hình dành cho team triển khai theo đặc tả.', href: '/(account)/account-link' },
    ],
  },
];

export default function HomeMenuScreen() {
  const state = useSessionState();
  const isAuthed = state.status === 'authenticated';
  const isAnon = state.status === 'anonymous';
  const isReady = isAuthed || isAnon;

  return (
    <ScrollView style={styles.scroll} contentContainerStyle={styles.container}>
      <Text style={styles.title}>DCMS Mobile</Text>
      <Text style={styles.subtitle}>
        Bản đồ các màn hình đang triển khai trong app. Bấm vào mục để mở.
      </Text>

      {!isReady ? (
        <View style={styles.notice}>
          <Text style={styles.noticeText}>
            Đang kiểm tra trạng thái phiên… các lối tắt sẽ xuất hiện sau khi hoàn tất.
          </Text>
        </View>
      ) : null}

      {GROUPS.map((group) => {
        const visibleItems = group.items.filter((item) => {
          if (item.requireAuth && state.status !== item.requireAuth) return false;
          if (item.hideWhenAuth && state.status === item.hideWhenAuth) return false;
          return true;
        });
        if (visibleItems.length === 0) return null;
        return (
          <View key={group.title} style={styles.group}>
            <Text style={styles.groupTitle}>{group.title}</Text>
            {visibleItems.map((item) => (
              <Link key={`${item.href.toString()}-${item.label}`} href={item.href} asChild>
                <View style={styles.card} accessibilityRole="button">
                  <Text style={styles.cardLabel}>{item.label}</Text>
                  <Text style={styles.cardDescription}>{item.description}</Text>
                </View>
              </Link>
            ))}
          </View>
        );
      })}

      <View style={styles.footer}>
        <Text style={styles.footerText}>
          Trạng thái phiên:{' '}
          <Text style={styles.footerState}>
            {state.status === 'initializing' && 'Đang tải'}
            {state.status === 'anonymous' && 'Khách (chưa đăng nhập)'}
            {state.status === 'authenticated' && `Đã đăng nhập — ${state.user.userName}`}
            {state.status === 'error' && 'Lỗi — thử lại'}
          </Text>
        </Text>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  scroll: { backgroundColor: colors.background },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.body - 2, marginBottom: spacing.sm },
  group: { gap: spacing.sm, marginTop: spacing.md },
  groupTitle: {
    fontSize: typography.body,
    fontWeight: '600',
    color: colors.primary,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 10,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    gap: spacing.xs,
  },
  cardLabel: { fontSize: typography.body, fontWeight: '600', color: colors.text },
  cardDescription: { fontSize: typography.caption + 1, color: colors.muted },
  notice: {
    backgroundColor: colors.infoSoft,
    borderRadius: 8,
    padding: spacing.md,
  },
  noticeText: { color: colors.primary, fontSize: typography.caption + 1 },
  footer: { marginTop: spacing.xl, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.border },
  footerText: { color: colors.muted, fontSize: typography.caption },
  footerState: { fontWeight: '600', color: colors.text },
});
