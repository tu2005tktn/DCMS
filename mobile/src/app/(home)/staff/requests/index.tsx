/**
 * S12 — StaffSupportInboxScreen.
 *
 * Staff xem danh sách support request, lọc theo status/branch. Mở request → chi tiết với
 * các command TV4: xác minh requester, đổi/huỷ lịch, resolve/reject.
 *
 * Theo dặn dò:
 *  - Quyền staff kiểm tra quyền + branch match + ownership.
 *  - Đổi/huỷ với lý do qua command TV4 (staffChange).
 *  - Resolve chỉ sau khi booking action cần thiết đã commit (staffResolve tự gọi staffChange
 *    cho Change/Cancel; chỉ trả ok=true khi mutation thật sự thành công).
 *  - Withdrawn KHÔNG được resolve lại.
 *  - Không báo xử lý thành công nếu mutation thất bại.
 *
 * Mock staff user: mọi staff role (branch/chair/staff) đều có quyền trong demo.
 * Production: sẽ kiểm qua `state.user.effectiveRoles` + `state.user.branchId`.
 */

import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';
import { appointmentMockRepo } from '@/features/appointments/appointment.mock';
import type { SupportRequestDto, SupportRequestStatus } from '@/features/appointments/appointment.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'empty' }
  | { kind: 'denied' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; items: SupportRequestDto[] };

const STATUS_FILTERS: { value: SupportRequestStatus[]; label: string }[] = [
  { value: ['Pending', 'InReview'], label: 'Cần xử lý' },
  { value: ['Pending'], label: 'Chờ staff' },
  { value: ['InReview'], label: 'Đang xem xét' },
  { value: ['Resolved', 'Rejected', 'Withdrawn'], label: 'Đã đóng' },
];

export default function StaffSupportInboxScreen() {
  const router = useRouter();
  const { state } = useSession();
  const [filter, setFilter] = useState(0);
  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [refreshing, setRefreshing] = useState(false);

  // Mock: lấy branchId từ user (public account thì = 1).
  const staffBranchId = 1;
  const isStaff = state.status === 'authenticated' && state.user.accountType === 'Public'
    ? false
    : state.status === 'authenticated' && (state.user.roles?.length ?? 0) > 0;

  const reload = useCallback(async () => {
    if (!isStaff) {
      setLoad({ kind: 'denied' });
      return;
    }
    try {
      const items = await appointmentMockRepo.listRequestsForStaff({
        statuses: STATUS_FILTERS[filter].value,
        branchId: staffBranchId,
      });
      setLoad(items.length === 0 ? { kind: 'empty' } : { kind: 'ready', items });
    } catch {
      setLoad({ kind: 'error', message: 'Không thể kết nối máy chủ.' });
    }
  }, [isStaff, filter]);

  useEffect(() => { void reload(); }, [reload]);
  useFocusEffect(useCallback(() => { void reload(); }, [reload]));

  if (!isStaff) {
    return (
      <SafeAreaView style={styles.center} edges={['top']}>
        <Text style={styles.title}>Không có quyền</Text>
        <Text style={styles.muted}>Màn hình này chỉ dành cho nhân viên DCMS.</Text>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Hộp thư hỗ trợ (Staff)</Text>
        <Text style={styles.subtitle}>
          Xử lý yêu cầu đổi/huỷ/xác nhận từ booker & attendee thuộc chi nhánh của bạn.
        </Text>
      </View>

      <View style={styles.tabBar}>
        {STATUS_FILTERS.map((f, idx) => (
          <Pressable
            key={f.label}
            onPress={() => setFilter(idx)}
            style={[styles.tab, filter === idx && styles.tabActive]}
          >
            <Text style={[styles.tabText, filter === idx && styles.tabTextActive]}>{f.label}</Text>
          </Pressable>
        ))}
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
          load.kind === 'loading' ? (
            <View style={styles.center}><ActivityIndicator color={colors.primary} /></View>
          ) : load.kind === 'error' ? (
            <View style={styles.errorBox}>
              <Text style={styles.errorTitle}>Lỗi</Text>
              <Text style={styles.errorBody}>{load.message}</Text>
              <Pressable onPress={() => void reload()} style={styles.retryBtn}><Text style={styles.retryText}>Thử lại</Text></Pressable>
            </View>
          ) : load.kind === 'denied' ? null : (
            <View style={styles.emptyBox}>
              <Text style={styles.emptyTitle}>Không có yêu cầu nào</Text>
              <Text style={styles.emptyBody}>
                Khi có yêu cầu mới thuộc bộ lọc "{STATUS_FILTERS[filter].label}", chúng sẽ xuất hiện ở đây.
              </Text>
            </View>
          )
        }
        renderItem={({ item }) => (
          <Pressable
            onPress={() => router.push(`/(home)/staff/requests/${item.id}` as never)}
            style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
          >
            <View style={styles.cardTop}>
              <Text style={styles.cardCode}>{item.appointmentCode}</Text>
              <View style={[styles.statusPill, statusPillStyle(item.status)]}>
                <Text style={styles.statusPillText}>{statusLabel(item.status)}</Text>
              </View>
            </View>
            <Text style={styles.cardTitle}>{kindLabel(item.kind)}</Text>
            <Text style={styles.cardReason}>{item.reason || '—'}</Text>
            <View style={styles.cardFooter}>
              <Text style={styles.cardMeta}>
                {item.requester.name}{item.requester.isAnonymous ? ' (vãng lai)' : ''} · cập nhật {formatTime(item.updatedAt)}
              </Text>
            </View>
          </Pressable>
        )}
      />
    </SafeAreaView>
  );
}

function statusPillStyle(s: SupportRequestStatus) {
  switch (s) {
    case 'Pending': return { backgroundColor: '#FFF7CC' };
    case 'InReview': return { backgroundColor: '#D6E8FB' };
    case 'Resolved': return { backgroundColor: '#D6F5E0' };
    case 'Rejected': return { backgroundColor: '#FBDADA' };
    case 'Withdrawn': return { backgroundColor: '#E5E5E5' };
  }
}

function statusLabel(s: SupportRequestStatus): string {
  switch (s) {
    case 'Pending': return 'Chờ staff';
    case 'InReview': return 'Đang xem xét';
    case 'Resolved': return 'Đã xử lý';
    case 'Rejected': return 'Bị từ chối';
    case 'Withdrawn': return 'Đã rút';
  }
}

function kindLabel(k: string): string {
  switch (k) {
    case 'Change': return 'Yêu cầu đổi lịch';
    case 'Cancel': return 'Yêu cầu huỷ lịch';
    case 'ConfirmToAttend': return 'Xác nhận tham dự';
    case 'WithdrawAttend': return 'Rút xác nhận tham dự';
    default: return 'Hỏi chung';
  }
}

function formatTime(iso: string): string {
  try { return new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }); }
  catch { return iso; }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, padding: spacing.lg, backgroundColor: colors.background },
  header: { padding: spacing.lg, gap: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border, backgroundColor: colors.surface },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1 },
  tabBar: { flexDirection: 'row', backgroundColor: colors.surface, borderBottomWidth: 1, borderBottomColor: colors.border },
  tab: { flex: 1, paddingVertical: spacing.sm, alignItems: 'center', borderBottomWidth: 2, borderBottomColor: 'transparent' },
  tabActive: { borderBottomColor: colors.primary },
  tabText: { color: colors.muted, fontSize: typography.caption + 1, fontWeight: '500' },
  tabTextActive: { color: colors.primary, fontWeight: '700' },
  listContent: { padding: spacing.md, paddingBottom: spacing.xl },
  card: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, gap: spacing.xs, borderWidth: 1, borderColor: colors.border },
  cardPressed: { backgroundColor: colors.infoSoft },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  cardCode: { fontSize: typography.body, fontWeight: '700', color: colors.text },
  statusPill: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 6 },
  statusPillText: { fontSize: typography.caption, fontWeight: '600' },
  cardTitle: { fontSize: typography.body, fontWeight: '600', color: colors.text },
  cardReason: { color: colors.muted, fontSize: typography.caption + 1 },
  cardFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.xs },
  cardMeta: { color: colors.muted, fontSize: typography.caption, flex: 1 },
  sep: { height: spacing.sm },
  emptyBox: { padding: spacing.lg, backgroundColor: colors.surface, borderRadius: 12, gap: spacing.xs },
  emptyTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  emptyBody: { color: colors.muted, fontSize: typography.caption + 1 },
  errorBox: { padding: spacing.lg, backgroundColor: colors.dangerSoft ?? '#FBDADA', borderRadius: 12, gap: spacing.xs },
  errorTitle: { fontSize: typography.body, fontWeight: '700', color: colors.danger },
  errorBody: { color: colors.text, fontSize: typography.caption + 1 },
  retryBtn: { marginTop: spacing.sm, paddingVertical: spacing.sm, alignItems: 'center', backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
  muted: { color: colors.muted, textAlign: 'center' },
});
