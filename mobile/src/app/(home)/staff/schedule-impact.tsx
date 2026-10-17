/**
 * S12 — StaffScheduleImpactScreen.
 *
 * Khi staff giảm giờ làm (UC-03 S09), cần hiển thị các lịch bị ảnh hưởng trước khi publish
 * (Tư vẫn chặn schedule publish cho đến khi hợp lệ).
 *
 * UC-03 endpoint preview trả về `AffectedScheduleResult`; UI này dùng `appointmentMockRepo.previewAffectedSchedule`
 * để demo. Mỗi impacted appointment link tới `/(home)/appointments/{id}`.
 */

import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { appointmentMockRepo } from '@/features/appointments/appointment.mock';
import type { AffectedScheduleResult, ImpactedAppointment } from '@/features/appointments/appointment.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

export default function StaffScheduleImpactScreen() {
  const router = useRouter();
  const [result, setResult] = useState<AffectedScheduleResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Mock: lấy tuần hiện tại (thứ 2 đầu tuần).
      const now = new Date();
      const day = now.getDay();
      const mondayOffset = day === 0 ? -6 : 1 - day;
      const weekStart = new Date(now);
      weekStart.setDate(now.getDate() + mondayOffset);
      weekStart.setHours(0, 0, 0, 0);
      const r = await appointmentMockRepo.previewAffectedSchedule(1, weekStart.toISOString());
      setResult(r);
    } catch {
      setError('Không thể tải lịch bị ảnh hưởng.');
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(useCallback(() => { void reload(); }, [reload]));

  if (loading) {
    return (
      <SafeAreaView style={styles.center} edges={['top']}>
        <ActivityIndicator color={colors.primary} />
        <Text style={styles.muted}>Đang tải…</Text>
      </SafeAreaView>
    );
  }
  if (error) {
    return (
      <SafeAreaView style={styles.center} edges={['top']}>
        <Text style={styles.title}>Lỗi</Text>
        <Text style={styles.muted}>{error}</Text>
        <Pressable onPress={() => void reload()} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Lịch bị ảnh hưởng</Text>
        <Text style={styles.subtitle}>
          Khi giảm giờ làm, các lịch hẹn Confirmed/Rescheduled trong tuần sẽ hiện ở đây.
          Bạn cần xử lý trước khi publish schedule mới.
        </Text>
      </View>

      <FlatList
        data={result?.impacted ?? []}
        keyExtractor={(item) => item.appointmentId}
        contentContainerStyle={styles.listContent}
        ItemSeparatorComponent={() => <View style={styles.sep} />}
        ListEmptyComponent={
          <View style={styles.emptyBox}>
            <Text style={styles.emptyTitle}>Không có lịch bị ảnh hưởng</Text>
            <Text style={styles.emptyBody}>
              Tuần này không có lịch Confirmed/Rescheduled nào nằm trong khung giờ sẽ thu hẹp.
            </Text>
          </View>
        }
        renderItem={({ item }: { item: ImpactedAppointment }) => (
          <Pressable
            onPress={() => router.push(item.link as never)}
            style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
          >
            <View style={styles.cardTop}>
              <Text style={styles.code}>{item.code}</Text>
              <View style={[styles.statusPill, { backgroundColor: item.status === 'Confirmed' ? '#D6F5E0' : '#D6E8FB' }]}>
                <Text style={styles.statusText}>{item.status}</Text>
              </View>
            </View>
            <Row label="Bệnh nhân" value={`${item.attendeeName} (${item.attendeeLabel})`} />
            <Row label="Bắt đầu" value={fmt(item.startAt)} />
            <Row label="Kết thúc" value={fmt(item.endAt)} />
            <Text style={styles.linkText}>Mở chi tiết ›</Text>
          </Pressable>
        )}
      />
    </SafeAreaView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

function fmt(iso: string): string {
  try { return new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' }); }
  catch { return iso; }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, padding: spacing.lg, backgroundColor: colors.background },
  header: { padding: spacing.lg, gap: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border, backgroundColor: colors.surface },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1 },
  listContent: { padding: spacing.md, paddingBottom: spacing.xl },
  card: { backgroundColor: colors.surface, borderRadius: 12, padding: spacing.md, gap: spacing.xs, borderWidth: 1, borderColor: colors.border },
  cardPressed: { backgroundColor: colors.infoSoft },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  code: { fontSize: typography.body, fontWeight: '700', color: colors.text },
  statusPill: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 6 },
  statusText: { fontSize: typography.caption, fontWeight: '600' },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  rowLabel: { color: colors.muted, fontSize: typography.caption + 1 },
  rowValue: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '500', maxWidth: '65%', textAlign: 'right' },
  linkText: { color: colors.primary, fontWeight: '600', marginTop: spacing.xs, fontSize: typography.caption },
  sep: { height: spacing.sm },
  emptyBox: { padding: spacing.lg, backgroundColor: colors.surface, borderRadius: 12, gap: spacing.xs },
  emptyTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  emptyBody: { color: colors.muted, fontSize: typography.caption + 1 },
  retry: { marginTop: spacing.sm, paddingVertical: spacing.sm, paddingHorizontal: spacing.lg, backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
  muted: { color: colors.muted },
});
