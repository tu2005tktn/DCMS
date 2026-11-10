/**
 * S26 — NotificationListScreen.
 *
 * Theo đặc tả S26: chỉ phần thông báo lịch hẹn của account hiện tại. Mở deep link sẽ kiểm
 * quyền lại (xem notification.mock.ts.checkDeepLinkPermission).
 *
 * Theo GBR-081: notification KHÔNG chứa chẩn đoán. Theo AT16: nếu external push/email fail
 * sau 3 lần retry, staff sẽ có handling item — UI hiển thị badge "Gửi thất bại" để user biết
 * lịch vẫn còn hiệu lực.
 *
 * UI phân biệt rõ: loading / empty / connection error / ready.
 *
 * Read notification KHÔNG đổi lifecycle lịch (tiêu chí nghiệm thu: "Read notification/deep
 * link không làm đổi lifecycle lịch").
 */

import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';
import { notificationRepo } from '@/features/appointments/notification.repo';
import type { NotificationDto } from '@/features/appointments/notification.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; items: NotificationDto[] };

export default function NotificationListScreen() {
  const router = useRouter();
  const { state } = useSession();
  const accountId = state.status === 'authenticated' ? state.user.id : null;
  const accessToken = state.status === 'authenticated' ? state.accessToken : null;

  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [refreshing, setRefreshing] = useState(false);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (accountId === null || !accessToken) {
      setLoad({ kind: 'empty' });
      return;
    }
    try {
      const result = await notificationRepo.list(accountId, { limit: 50, unreadOnly }, accessToken);
      const items = result.items;
      setLoad(items.length === 0 ? { kind: 'empty' } : { kind: 'ready', items });
    } catch {
      setLoad({ kind: 'error', message: 'Không thể kết nối máy chủ. Vui lòng thử lại.' });
    }
  }, [accountId, accessToken, unreadOnly]);

  useFocusEffect(useCallback(() => { void reload(); }, [reload]));

  const openNotification = useCallback(async (n: NotificationDto) => {
    // 1. Mark read trước (KHÔNG đổi lifecycle lịch).
    setActionError(null);
    if (accountId !== null && n.readAt === null) {
      const result = await notificationRepo.markReadOne(n.id, accountId, accessToken);
      if (!result.ok) { setActionError('Chưa đánh dấu đã đọc được. Vui lòng thử lại.'); return; }
    }
    // 2. Nếu có deep link: server re-check quyền.
    if (n.deepLink && accountId !== null) {
      const check = await notificationRepo.checkDeepLink({ deepLink: n.deepLink }, accountId, accessToken);
      if (!check.allowed) {
        setActionError(check.reason ?? 'Không có quyền mở thông báo này.');
        return;
      }
      router.push(n.deepLink as never);
    }
    // Refresh để cập nhật read state.
    void reload();
  }, [accountId, accessToken, router, reload]);

  if (load.kind === 'loading') {
    return (
      <SafeAreaView style={styles.center} edges={['top']}>
        <ActivityIndicator color={colors.primary} />
        <Text style={styles.muted}>Đang tải thông báo…</Text>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Thông báo</Text>
        <Text style={styles.subtitle}>Chỉ thông báo lịch hẹn của tài khoản hiện tại.</Text>
        <View style={styles.filters}>
          <Pressable onPress={() => setUnreadOnly(false)} style={[styles.filter, !unreadOnly && styles.filterActive]} accessibilityRole="button"><Text style={[styles.filterText, !unreadOnly && styles.filterTextActive]}>Tất cả</Text></Pressable>
          <Pressable onPress={() => setUnreadOnly(true)} style={[styles.filter, unreadOnly && styles.filterActive]} accessibilityRole="button"><Text style={[styles.filterText, unreadOnly && styles.filterTextActive]}>Chưa đọc</Text></Pressable>
        </View>
        {actionError ? <Text style={styles.actionError}>{actionError}</Text> : null}
      </View>

      <FlatList
        data={load.kind === 'ready' ? load.items : []}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.sep} />}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={async () => { setRefreshing(true); await reload(); setRefreshing(false); }} tintColor={colors.primary} />
        }
        ListEmptyComponent={
          load.kind === 'error' ? (
            <View style={styles.errorBox}>
              <Text style={styles.errorTitle}>Lỗi</Text>
              <Text style={styles.errorBody}>{load.message}</Text>
              <Pressable onPress={() => void reload()} style={styles.retryBtn}><Text style={styles.retryText}>Thử lại</Text></Pressable>
            </View>
          ) : (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyTitle}>Chưa có thông báo</Text>
              <Text style={styles.emptyBody}>
                {unreadOnly ? 'Bạn đã đọc hết thông báo.' : 'Khi có thay đổi về lịch hẹn hoặc nhắc lịch sắp tới, thông báo sẽ xuất hiện ở đây.'}
              </Text>
            </View>
          )
        }
        renderItem={({ item }) => {
          const unread = item.readAt === null;
          return (
            <Pressable
              onPress={() => void openNotification(item)}
              style={({ pressed }) => [styles.card, unread && styles.cardUnread, pressed && styles.cardPressed]}
              accessibilityRole="button"
            >
              <View style={styles.cardTop}>
                <Text style={styles.cardTitle}>{item.title}</Text>
                {unread ? <View style={styles.dot} /> : null}
              </View>
              <Text style={styles.cardBody}>{item.body}</Text>
              <View style={styles.cardFooter}>
                <Text style={styles.cardTime}>{formatTime(item.createdAt)}</Text>
                {item.hasExternalFailure ? (
                  <View style={styles.failBadge}>
                    <Text style={styles.failText}>Gửi thất bại (đã có nhân viên xử lý)</Text>
                  </View>
                ) : null}
              </View>
            </Pressable>
          );
        }}
      />
    </SafeAreaView>
  );
}

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' });
  } catch { return iso; }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: colors.background },
  header: { padding: spacing.lg, gap: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border, backgroundColor: colors.surface },
  headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1 },
  backendBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 6, backgroundColor: colors.infoSoft ?? '#E6F0FA' },
  backendText: { fontSize: typography.caption, color: colors.primary, fontWeight: '700' },
  filters: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm },
  filter: { minHeight: 44, justifyContent: 'center', paddingHorizontal: spacing.md, borderRadius: 22, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
  filterActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  filterText: { color: colors.text, fontWeight: '600' }, filterTextActive: { color: colors.onPrimary },
  actionError: { color: colors.danger, paddingTop: spacing.sm },
  listContent: { padding: spacing.md, paddingBottom: spacing.xl },
  card: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, gap: spacing.xs, borderWidth: 1, borderColor: colors.border },
  cardUnread: { borderColor: colors.primary, backgroundColor: colors.infoSoft },
  cardPressed: { opacity: 0.8 },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  cardTitle: { fontSize: typography.body, fontWeight: '700', color: colors.text, flex: 1 },
  cardBody: { fontSize: typography.caption + 1, color: colors.text },
  cardFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.xs },
  cardTime: { color: colors.muted, fontSize: typography.caption },
  dot: { width: 8, height: 8, borderRadius: 4, backgroundColor: colors.primary, marginLeft: spacing.sm },
  failBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 6, backgroundColor: colors.dangerSoft ?? '#FBDADA' },
  failText: { fontSize: typography.caption, color: colors.danger, fontWeight: '600' },
  sep: { height: spacing.sm },
  emptyBox: { padding: spacing.lg, backgroundColor: colors.surface, borderRadius: 12, gap: spacing.xs },
  emptyTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  emptyBody: { color: colors.muted, fontSize: typography.caption + 1 },
  errorBox: { padding: spacing.lg, backgroundColor: colors.dangerSoft ?? '#FBDADA', borderRadius: 12, gap: spacing.xs },
  errorTitle: { fontSize: typography.body, fontWeight: '700', color: colors.danger },
  errorBody: { color: colors.text, fontSize: typography.caption + 1 },
  retryBtn: { marginTop: spacing.sm, paddingVertical: spacing.sm, alignItems: 'center', backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
  muted: { color: colors.muted },
});
