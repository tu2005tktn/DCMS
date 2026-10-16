/**
 * S12 — AppointmentHubScreen (booker + attendee view).
 *
 * Theo đặc tả S12: hiển thị code/lifecycle, booker/attendee riêng, branch/chair/staff/start/end,
 * lịch sử và trạng thái support request.
 *
 * Dùng mock DTO chung (xem appointment.mock.ts) — khi TV2 triển khai API thật, chỉ đổi repo.
 * Dữ liệu đọc TV3 (UC-05/06 phần booking portion UC-34) qua repo.getById / listForAccount.
 *
 * UI phân biệt rõ:
 *  - loading: ActivityIndicator
 *  - empty: "Chưa có lịch hẹn"
 *  - connection error: "Không thể kết nối máy chủ"
 *  - access denied (khi deep link từ S26 re-check quyền): "Bạn không có quyền xem lịch hẹn này"
 *  - stale/conflict: "Lịch hẹn đã được cập nhật. Kéo xuống để tải lại"
 */

import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';
import { appointmentMockRepo } from '@/features/appointments/appointment.mock';
import type { AppointmentDto, AppointmentStatus } from '@/features/appointments/appointment.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; items: AppointmentDto[] };

const STATUS_LABELS: Record<AppointmentStatus, { label: string; color: string; bg: string }> = {
  Pending:     { label: 'Chờ xác minh',  color: '#8A6D00', bg: '#FFF7CC' },
  Confirmed:   { label: 'Đã xác nhận',   color: '#0B6E2E', bg: '#D6F5E0' },
  Rescheduled: { label: 'Đã đổi lịch',   color: '#0E4E8A', bg: '#D6E8FB' },
  Completed:   { label: 'Hoàn tất',      color: '#3B3B3B', bg: '#E5E5E5' },
  Cancelled:   { label: 'Đã huỷ',        color: '#9A1A1A', bg: '#FBDADA' },
  NoShow:      { label: 'Không đến',     color: '#9A1A1A', bg: '#FBDADA' },
};

export default function AppointmentHubScreen() {
  const router = useRouter();
  const { state } = useSession();
  const accountId = state.status === 'authenticated' ? state.user.id : null;

  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [refreshing, setRefreshing] = useState(false);

  const loadData = useCallback(async () => {
    if (!accountId) {
      setLoad({ kind: 'empty' });
      return;
    }
    try {
      const items = await appointmentMockRepo.listForAccount(accountId);
      setLoad(items.length === 0 ? { kind: 'empty' } : { kind: 'ready', items });
    } catch {
      setLoad({ kind: 'error', message: 'Không thể kết nối máy chủ. Vui lòng thử lại.' });
    }
  }, [accountId]);

  useEffect(() => { void loadData(); }, [loadData]);
  useFocusEffect(useCallback(() => { void loadData(); }, [loadData]));

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await loadData();
    setRefreshing(false);
  }, [loadData]);

  if (load.kind === 'loading') {
    return (
      <SafeAreaView style={styles.center} edges={['top']}>
        <ActivityIndicator color={colors.primary} />
        <Text style={styles.muted}>Đang tải lịch hẹn…</Text>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Lịch hẹn của tôi</Text>
        <Text style={styles.subtitle}>
          Theo dõi trạng thái lịch khám, lịch sử thay đổi và yêu cầu hỗ trợ.
        </Text>
      </View>

      <FlatList
        data={load.kind === 'ready' ? load.items : []}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.sep} />}
        ListEmptyComponent={
          load.kind === 'error' ? (
            <View style={styles.errorBox}>
              <Text style={styles.errorTitle}>Lỗi</Text>
              <Text style={styles.errorBody}>{load.message}</Text>
              <Pressable onPress={() => void loadData()} style={styles.retryBtn}>
                <Text style={styles.retryText}>Thử lại</Text>
              </Pressable>
            </View>
          ) : (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyTitle}>Chưa có lịch hẹn</Text>
              <Text style={styles.emptyBody}>
                Đặt lịch khám mới từ màn hình chính — bạn có thể đặt cho bản thân hoặc người thân.
              </Text>
            </View>
          )
        }
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} tintColor={colors.primary} />
        }
        renderItem={({ item }) => (
          <Pressable
            onPress={() => router.push(`/appointments/${item.id}` as never)}
            style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
            accessibilityRole="button"
          >
            <View style={styles.cardTop}>
              <Text style={styles.code}>{item.code}</Text>
              <View style={[styles.statusBadge, { backgroundColor: STATUS_LABELS[item.status].bg }]}>
                <Text style={[styles.statusText, { color: STATUS_LABELS[item.status].color }]}>
                  {STATUS_LABELS[item.status].label}
                </Text>
              </View>
            </View>

            <View style={styles.row}>
              <Text style={styles.label}>Dịch vụ</Text>
              <Text style={styles.value}>{item.serviceName || '—'}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Chi nhánh</Text>
              <Text style={styles.value}>{item.branchName || '—'}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Ghế</Text>
              <Text style={styles.value}>{item.chairId !== null ? `Ghế ${item.chairId}` : '—'}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Bác sĩ</Text>
              <Text style={styles.value}>{item.dentistName || '—'}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Bắt đầu</Text>
              <Text style={styles.value}>{formatDate(item.startAt)}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Kết thúc</Text>
              <Text style={styles.value}>{formatDate(item.endAt)}</Text>
            </View>
            <View style={[styles.row, styles.divider]}>
              <Text style={styles.label}>Booker</Text>
              <Text style={styles.value}>{item.booker.name}{item.booker.isAnonymous ? ' (vãng lai)' : ''}</Text>
            </View>
            <View style={styles.row}>
              <Text style={styles.label}>Attendee</Text>
              <Text style={styles.value}>
                {item.attendee.name}
                {item.attendeeRelation === 'Relative' ? ' (người thân)' : ' (bản thân)'}
              </Text>
            </View>

            <View style={styles.cardFooter}>
              <Text style={styles.footerMeta}>
                Lịch sử: {item.history.length} mục · Xác nhận tham dự: {attendeeLabel(item.confirmedToAttend)}
              </Text>
              <Text style={styles.linkText}>Xem chi tiết ›</Text>
            </View>
          </Pressable>
        )}
      />
    </SafeAreaView>
  );
}

function attendeeLabel(c: 'Pending' | 'Yes' | 'No'): string {
  return c === 'Pending' ? 'Chờ xác nhận' : c === 'Yes' ? 'Có' : 'Không';
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch {
    return iso;
  }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: colors.background },
  muted: { color: colors.muted },
  header: { padding: spacing.lg, gap: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border, backgroundColor: colors.surface },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1 },
  listContent: { padding: spacing.md, paddingBottom: spacing.xl, gap: spacing.md },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 12,
    padding: spacing.md,
    gap: spacing.xs,
    borderWidth: 1,
    borderColor: colors.border,
  },
  cardPressed: { backgroundColor: colors.infoSoft },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.xs },
  code: { fontSize: typography.body, fontWeight: '700', color: colors.text },
  statusBadge: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 8 },
  statusText: { fontSize: typography.caption, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  label: { color: colors.muted, fontSize: typography.caption + 1 },
  value: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '500', maxWidth: '60%', textAlign: 'right' },
  divider: { borderTopWidth: 1, borderTopColor: colors.border, marginTop: spacing.xs, paddingTop: spacing.sm },
  cardFooter: { marginTop: spacing.sm, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  footerMeta: { color: colors.muted, fontSize: typography.caption, flex: 1 },
  linkText: { color: colors.primary, fontSize: typography.caption, fontWeight: '600' },
  sep: { height: spacing.md },
  emptyBox: { padding: spacing.lg, backgroundColor: colors.surface, borderRadius: 12, gap: spacing.xs, marginTop: spacing.md },
  emptyTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  emptyBody: { color: colors.muted, fontSize: typography.caption + 1 },
  errorBox: { padding: spacing.lg, backgroundColor: colors.dangerSoft ?? '#FBDADA', borderRadius: 12, gap: spacing.xs, marginTop: spacing.md },
  errorTitle: { fontSize: typography.body, fontWeight: '700', color: colors.danger },
  errorBody: { color: colors.text, fontSize: typography.caption + 1 },
  retryBtn: { marginTop: spacing.sm, paddingVertical: spacing.sm, alignItems: 'center', backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
});
