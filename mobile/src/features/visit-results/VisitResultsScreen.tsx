/**
 * Kết quả khám theo lượt — danh sách (docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §2).
 * Only visits with a confirmed examination of the caller's VERIFIED own record. 401 → signOut('expired');
 * 403 → clear the list and show the link banner; paging follows the API contract (20 per page).
 */

import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, palette } from '@/features/patient-link/components/LinkUi';
import { formatDate, formatDateTime } from '@/features/appointments/appointments.rules';
import { useSession } from '@/features/auth/session/useSession';
import { RecordApiError } from '@/features/medical-records/medical-records.api';
import { spacing } from '@/theme/spacing';
import { visitResultsApi } from './visit-results.api';
import { visitStatusLabels, type OwnVisitItem } from './visit-results.types';

export default function VisitResultsScreen() {
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [items, setItems] = useState<OwnVisitItem[] | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [denied, setDenied] = useState(false);
  const order = useRef(0);

  const fail = useCallback((failure: unknown) => {
    if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return; }
    if (failure instanceof RecordApiError && failure.status === 403) { setItems(null); setTotal(0); setDenied(true); setError(null); return; }
    setError(failure as RecordApiError);
  }, [session.signOut]);

  const load = useCallback(async (mode: 'initial' | 'refresh') => {
    if (!token) return;
    const id = ++order.current;
    if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setError(null);
    try {
      const result = await visitResultsApi.visits(token, 1);
      if (id !== order.current) return;
      setItems(result.items); setTotal(result.total); setPage(1); setDenied(false);
    } catch (failure) { if (id === order.current) fail(failure); }
    finally { if (id === order.current) { setLoading(false); setRefreshing(false); } }
  }, [token, fail]);

  const loadMore = useCallback(async () => {
    if (!token || !items || loadingMore || items.length >= total) return;
    const id = order.current;
    setLoadingMore(true);
    try {
      const result = await visitResultsApi.visits(token, page + 1);
      if (id !== order.current) return;
      setItems(current => [...(current ?? []), ...result.items.filter(x => !(current ?? []).some(y => y.visitId === x.visitId))]);
      setTotal(result.total); setPage(result.page);
    } catch (failure) { if (id === order.current) fail(failure); }
    finally { setLoadingMore(false); }
  }, [token, items, loadingMore, total, page, fail]);

  // Session gone (sign-out or expiry): drop everything shown.
  useEffect(() => { if (!token) { order.current++; setItems(null); setTotal(0); return; } void load('initial'); }, [token, load]);

  if (!token) return null;
  if (loading && !items) return <View style={styles.center}><ActivityIndicator color={palette.primary} /><Text style={styles.muted}>Đang tải kết quả khám…</Text></View>;
  if (denied) return <View style={styles.page}>
    <Banner tone="error" message="Bạn cần liên kết hồ sơ bệnh nhân đã được phòng khám xác minh để xem kết quả khám."
      action={{ label: 'Thử lại', onPress: () => void load('initial') }} />
    <Button title="Mở màn liên kết hồ sơ" onPress={() => router.push('/(account)/account-link')} />
  </View>;

  return <FlatList
    style={styles.list}
    contentContainerStyle={styles.page}
    data={items ?? []}
    keyExtractor={item => item.visitId}
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load('refresh')} />}
    ListHeaderComponent={<View style={styles.header}>
      <Text style={styles.title}>Kết quả khám theo lượt</Text>
      <Text style={styles.muted}>Chỉ hiển thị phiếu khám đã được bác sĩ xác nhận. Kéo xuống để làm mới.</Text>
      {error && <Banner tone="error" message={error.message} action={{ label: 'Thử lại', onPress: () => void load('initial') }} />}
    </View>}
    ListEmptyComponent={!error ? <View style={styles.empty}><Text style={styles.muted}>Chưa có kết quả khám chính thức.</Text></View> : null}
    renderItem={({ item }) => <Pressable accessibilityRole="button" onPress={() => router.push(`/(account)/visit-results/${item.visitId}`)}
      android_ripple={{ color: palette.border }} style={styles.card}>
      <View style={styles.row}>
        <Text style={styles.date}>{formatDate(item.arrivalAt)}</Text>
        <Text style={[styles.badge, item.visitStatus === 'Completed' ? styles.done : styles.open]}>{visitStatusLabels[item.visitStatus] ?? item.visitStatus}</Text>
      </View>
      <Text style={styles.meta}>{item.dentistName} · {item.branchName}</Text>
      <Text style={styles.diagnosis} numberOfLines={2}>{item.diagnosisSummary ?? 'Không ghi chẩn đoán'}</Text>
      <Text style={styles.muted}>
        Phiếu đã xác nhận · phiên bản {item.examinationVersion}{item.completedAt ? ` · kết thúc ${formatDateTime(item.completedAt)}` : ''}
      </Text>
      <Text style={styles.muted}>{item.documentCount} ảnh/tài liệu{item.hasPrescription ? ' · có đơn thuốc' : ''}</Text>
    </Pressable>}
    onEndReachedThreshold={0.3}
    onEndReached={() => void loadMore()}
    ListFooterComponent={items && items.length < total
      ? <Button title={loadingMore ? 'Đang tải…' : 'Tải thêm'} variant="secondary" busy={loadingMore} onPress={() => void loadMore()} />
      : null}
  />;
}

const styles = StyleSheet.create({
  list: { backgroundColor: palette.background },
  page: { padding: spacing.lg, gap: spacing.md },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  header: { gap: spacing.sm },
  title: { fontSize: 26, fontWeight: '800', color: palette.text },
  muted: { color: palette.muted, fontSize: 13 },
  empty: { padding: spacing.lg, alignItems: 'center' },
  card: { backgroundColor: palette.card, borderRadius: 16, padding: spacing.md, gap: 7, borderWidth: 1, borderColor: palette.border },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  date: { fontSize: 16, fontWeight: '600', color: palette.text },
  badge: { fontSize: 12, paddingHorizontal: 8, paddingVertical: 2, borderRadius: 10, overflow: 'hidden' },
  done: { color: palette.success, backgroundColor: palette.successBg },
  open: { color: '#B54708', backgroundColor: '#FFFAEB' },
  meta: { color: palette.text, fontSize: 14 },
  diagnosis: { color: palette.text, fontSize: 15, fontWeight: '500' },
});
