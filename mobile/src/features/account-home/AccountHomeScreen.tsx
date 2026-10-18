/**
 * AccountHomeScreen — Trang chủ tài khoản.
 *
 * Đăng xuất đi qua SessionProvider.signOut (state machine chính thức). Trước đây từng
 * trộn useSignOut mới ở đây, gây trạng thái lệch: hook mới chỉ clear token storage mà
 * KHÔNG cập nhật state của SessionProvider, nên (account)/_layout vẫn authorized và
 * expo-router không tự pop — user thấy "bấm Đăng xuất nhưng không có gì xảy ra".
 * Xem docs/implementation/S01-ACCOUNT-HOME-LOGOUT.vi.md (sẽ tạo nếu cần) để tra log.
 */

import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

// eslint-disable-next-line no-console
const debug = (...args: unknown[]) => console.log('[AccountHome/sign-out]', ...args);

export default function AccountHomeScreen() {
  const { state, signOut } = useSession();
  const router = useRouter();
  const [signingOut, setSigningOut] = useState(false);

  const user = state.status === 'authenticated' ? state.user : null;

  async function handleSignOut() {
    if (signingOut) return;
    setSigningOut(true);
    debug('clicked', { state: state.status });
    try {
      // Dùng state machine chính thức: state -> anonymous, (account)/_layout tự pop về home.
      // Lý do không router.replace thủ công: useAuthGuard đã lo việc đó khi state đổi,
      // và replace thủ công dễ đẩy về path không hợp lệ.
      await signOut('user');
      debug('signOut resolved', { state: 'anonymous (expected)' });
    } catch (error) {
      debug('signOut failed', error);
    } finally {
      setSigningOut(false);
    }
  }

  if (!user) {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  const initials = user.fullName
    .split(' ')
    .map((p) => p[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  return (
    <ScrollView style={styles.scroll} contentContainerStyle={styles.container}>
      {/* Avatar */}
      <View style={styles.avatar}>
        <Text style={styles.avatarText}>{initials}</Text>
      </View>

      {/* User info */}
      <Text style={styles.name}>{user.fullName}</Text>
      <Text style={styles.username}>@{user.userName}</Text>

      {/* Role badges */}
      <View style={styles.badges}>
        <View style={styles.badge}>
          <Text style={styles.badgeText}>{user.accountType}</Text>
        </View>
        {user.effectiveRoles.map((role) => (
          <View key={role} style={[styles.badge, styles.badgeRole]}>
            <Text style={styles.badgeRoleText}>{role}</Text>
          </View>
        ))}
      </View>

      {user.accountType === 'Public' && (
        <View style={styles.bookingSection}>
          <Text style={styles.bookingTitle}>Đặt lịch khám</Text>
          <Text style={styles.linkDescription}>Chọn lịch khám cho bản thân hoặc người thân.</Text>
          <PrimaryButton label="Bắt đầu đặt lịch" onPress={() => router.push('/book-appointment')} disabled={signingOut} testID="start-booking" />
        </View>
      )}

      {/* Account details */}
      <View style={styles.card}>
        <Row label="ID tài khoản" value={String(user.id)} />
        <View style={styles.divider} />
        <Row label="Loại tài khoản" value={user.accountType} />
        <View style={styles.divider} />
        <Row label="Vai trò" value={user.roles.join(', ')} />
      </View>

      {user.accountType === 'Public' && (
        <View style={styles.linkSection}>
          <Text style={styles.linkDescription}>Xem, đổi hoặc hủy lịch hẹn bạn đã đặt.</Text>
          <PrimaryButton
            label="Lịch hẹn của tôi"
            onPress={() => router.push('/(account)/appointments')}
            disabled={signingOut}
            testID="open-appointments"
          />
        </View>
      )}

      {user.accountType === 'Public' && (
        <View style={styles.linkSection}>
          <Text style={styles.linkDescription}>Gửi yêu cầu hoặc xem trạng thái liên kết hồ sơ của bạn.</Text>
          <PrimaryButton
            label="Liên kết hồ sơ của tôi"
            onPress={() => router.push('/(account)/account-link')}
            disabled={signingOut}
            testID="open-account-link"
          />
        </View>
      )}

      {/* Sign out */}
      <View style={styles.footer}>
        <Pressable
          onPress={() => void handleSignOut()}
          disabled={signingOut}
          style={({ pressed }) => [
            styles.signOutBtn,
            pressed && styles.signOutPressed,
          ]}
        >
          {signingOut ? (
            <ActivityIndicator color={colors.danger} size="small" />
          ) : (
            <Text style={styles.signOutText}>Đăng xuất</Text>
          )}
        </Pressable>
      </View>
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
  scroll: { flex: 1, backgroundColor: colors.background },
  container: {
    flexGrow: 1,
    backgroundColor: colors.background,
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.xl,
    gap: spacing.md,
  },
  avatar: {
    width: 72,
    height: 72,
    borderRadius: 36,
    backgroundColor: colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    alignSelf: 'center',
    marginBottom: spacing.sm,
  },
  avatarText: {
    color: colors.onPrimary,
    fontSize: 24,
    fontWeight: '700',
  },
  name: {
    fontSize: typography.heading,
    fontWeight: '600',
    color: colors.text,
    textAlign: 'center',
  },
  username: {
    fontSize: typography.body,
    color: colors.muted,
    textAlign: 'center',
  },
  badges: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
    justifyContent: 'center',
  },
  badge: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    borderWidth: 1,
    borderColor: colors.border,
  },
  badgeText: {
    fontSize: typography.caption,
    color: colors.muted,
    fontWeight: '500',
  },
  badgeRole: {
    backgroundColor: colors.infoSoft,
    borderColor: colors.primary,
  },
  badgeRoleText: {
    fontSize: typography.caption,
    color: colors.primary,
    fontWeight: '600',
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 12,
    padding: spacing.md,
    gap: spacing.sm,
    marginTop: spacing.sm,
  },
  linkSection: { gap: spacing.sm, marginTop: spacing.sm },
  bookingSection: { gap: spacing.sm, padding: spacing.md, backgroundColor: colors.surface,
    borderRadius: 12, borderWidth: 1, borderColor: colors.border },
  bookingTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  linkDescription: { color: colors.muted, fontSize: typography.body - 2 },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  rowLabel: {
    fontSize: typography.body,
    color: colors.muted,
  },
  rowValue: {
    fontSize: typography.body,
    color: colors.text,
    fontWeight: '500',
    maxWidth: '60%',
    textAlign: 'right',
  },
  divider: {
    height: 1,
    backgroundColor: colors.border,
  },
  footer: {
    marginTop: 'auto',
    paddingVertical: spacing.lg,
    alignItems: 'center',
  },
  signOutBtn: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
    borderRadius: 8,
    borderWidth: 1.5,
    borderColor: colors.danger,
    minWidth: 160,
    alignItems: 'center',
    minHeight: 48,
    justifyContent: 'center',
  },
  signOutPressed: {
    backgroundColor: colors.dangerSoft,
  },
  signOutText: {
    color: colors.danger,
    fontSize: typography.body,
    fontWeight: '600',
  },
});
