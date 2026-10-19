import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { useSession } from '@/features/auth/session/useSession';
import { Banner, Button, Card, Field, palette } from '@/features/patient-link/components/LinkUi';
import { AppointmentApiError, appointmentsApi, usingMockAppointments } from '../appointments.api';
import { formatClock, formatDate, formatDateInput, formatWeekday, parseInputDate, statusColors, statusLabels } from '../appointments.rules';
import type { AccessSource, AppointmentCard, AppointmentScope, AppointmentStatus } from '../appointments.types';

const PAGE_SIZE = 10;
const statuses: (AppointmentStatus | 'ALL')[] = ['ALL', 'Confirmed', 'CheckedIn', 'InProgress', 'Completed', 'Cancelled', 'NoShow'];
const accessLabels: Record<AccessSource, string> = { BOOKED_BY_ME: 'Lịch tôi đặt', MY_OWN: 'Lịch của tôi', BOTH: 'Lịch của tôi' };
type Filters = { status: AppointmentStatus | 'ALL'; from: string; to: string };
const emptyFilters: Filters = { status: 'ALL', from: '', to: '' };

export function StatusPill({ status }: { status: AppointmentStatus }) {
  const [color, background] = statusColors[status];
  return <View style={[styles.pill, { backgroundColor: background }]}><Text style={[styles.pillText, { color }]}>{statusLabels[status]}</Text></View>;
}

function Chip({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return <Pressable accessibilityRole="button" accessibilityState={{ selected }} onPress={onPress} style={[styles.chip, selected && styles.chipSelected]}>
    <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{label}</Text>
  </Pressable>;
}

// S14 My Appointments (UC-05). The list contains only bookings this account made or its own verified appointments.
export default function MyAppointmentsScreen() {
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const verified = session.state.status === 'authenticated' && session.state.user.hasVerifiedPatientLink;
  const [scope, setScope] = useState<AppointmentScope>('UPCOMING');
  const [draft, setDraft] = useState<Filters>(emptyFilters);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [filterError, setFilterError] = useState<{ from?: string; to?: string }>({});
  const [items, setItems] = useState<AppointmentCard[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<AppointmentApiError | null>(null);
  const [showFilters, setShowFilters] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const request = useRef(0);

  const load = useCallback(async (nextPage: number, mode: 'replace' | 'refresh' | 'more') => {
    if (!token) return;
    const id = ++request.current;
    if (mode === 'more') setLoadingMore(true); else if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setError(null);
    try {
      const result = await appointmentsApi.list(token, { scope, status: filters.status, from: parseInputDate(filters.from) ?? null,
        to: parseInputDate(filters.to) ?? null, page: nextPage, pageSize: PAGE_SIZE });
      if (id !== request.current) return;
      // Merge by code so a page shift never shows the same booking twice.
      setItems(previous => {
        const base = mode === 'more' ? previous : [];
        const seen = new Set(base.map(x => x.code));
        return [...base, ...result.items.filter(x => !seen.has(x.code))];
      });
      setTotal(result.total); setPage(nextPage);
    } catch (failure) {
      if (id !== request.current) return;
      if (failure instanceof AppointmentApiError && failure.status === 401) { void session.signOut('expired'); return; }
      setError(failure as AppointmentApiError);
    } finally {
      if (id === request.current) { setLoading(false); setLoadingMore(false); setRefreshing(false); }
    }
  }, [token, scope, filters, session.signOut]);

  // Reload whenever the screen regains focus so a change made on S16 is reflected.
  useFocusEffect(useCallback(() => { void load(1, 'replace'); }, [load]));

  function applyFilters() {
    const from = parseInputDate(draft.from);
    const to = parseInputDate(draft.to);
    const errors = {
      from: from === undefined ? 'Từ ngày không đúng định dạng.' : undefined,
      to: to === undefined ? 'Đến ngày không đúng định dạng.' : from && to && from > to ? 'Khoảng ngày không đúng định dạng.' : undefined,
    };
    setFilterError(errors);
    if (errors.from || errors.to) return;
    setFilters({ ...draft });
  }

  if (!token) return null;

  return <ScrollView contentContainerStyle={styles.container}
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(1, 'refresh')} />}>
    {usingMockAppointments && <Banner tone="info" message="Dữ liệu minh họa (mock) — API lịch hẹn chưa sẵn sàng. Thao tác chỉ lưu tạm trên ứng dụng." />}
    {notice && <Banner tone="info" message={notice} />}
    <View style={styles.tabs}>
      {(['UPCOMING', 'HISTORY'] as AppointmentScope[]).map(value =>
        <Pressable key={value} accessibilityRole="tab" accessibilityState={{ selected: scope === value }} onPress={() => setScope(value)}
          style={[styles.tab, scope === value && styles.tabSelected]}>
          <Text style={[styles.tabText, scope === value && styles.tabTextSelected]}>{value === 'UPCOMING' ? 'Sắp tới' : 'Lịch sử'}</Text>
        </Pressable>)}
    </View>
    <Text style={styles.muted}>Bạn xem được lịch do mình đặt và lịch của chính mình qua liên kết hồ sơ đã xác minh. Giờ hẹn theo giờ Việt Nam.</Text>
    <View style={styles.toolbar}>
      <Pressable accessibilityRole="button" onPress={() => setShowFilters(value => !value)}>
        <Text style={styles.link}>{showFilters ? 'Ẩn bộ lọc' : 'Bộ lọc'}{filters.status !== 'ALL' || filters.from || filters.to ? ' •' : ''}</Text>
      </Pressable>
      <Pressable accessibilityRole="button" onPress={() => router.push('/book-appointment')}>
        <Text style={styles.link}>+ Đặt lịch mới</Text>
      </Pressable>
    </View>
    {showFilters && <Card>
      <Text style={styles.label}>Trạng thái</Text>
      <View style={styles.chips}>{statuses.map(value =>
        <Chip key={value} label={value === 'ALL' ? 'Tất cả' : statusLabels[value]} selected={draft.status === value}
          onPress={() => setDraft({ ...draft, status: value })} />)}</View>
      <Field label="Từ ngày" value={draft.from} placeholder="DD-MM-YYYY" keyboardType="number-pad" error={filterError.from}
        onChangeText={value => setDraft({ ...draft, from: formatDateInput(value) })} />
      <Field label="Đến ngày" value={draft.to} placeholder="DD-MM-YYYY" keyboardType="number-pad" error={filterError.to}
        onChangeText={value => setDraft({ ...draft, to: formatDateInput(value) })} />
      <Button title="Áp dụng" onPress={applyFilters} />
      <Button title="Đặt lại" variant="secondary" onPress={() => { setDraft(emptyFilters); setFilterError({}); setFilters(emptyFilters); }} />
    </Card>}

    {loading && items.length === 0 ? <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>
      : error ? <Banner tone="error" message={error.message} action={{ label: 'Thử lại', onPress: () => void load(1, 'replace') }} />
      : items.length === 0 ? <Card>
        <Text style={styles.emptyTitle}>Không có lịch hẹn phù hợp với bộ lọc đã chọn.</Text>
        <Button title="Đặt lịch mới" onPress={() => router.push('/book-appointment')} />
        {!verified && <Text style={styles.muted}>Lịch khám của chính bạn chỉ hiển thị sau khi tài khoản được liên kết với hồ sơ tại phòng khám.</Text>}
        {!verified && <Button title="Liên kết hồ sơ của tôi" variant="secondary" onPress={() => router.push('/account-link')} />}
      </Card>
      : <>
        <Text style={styles.muted}>{total} lịch hẹn</Text>
        {items.map(item => <Pressable key={item.code} accessibilityRole="button" accessibilityLabel={`Lịch hẹn ${item.code}`}
          onPress={() => router.push(`/(account)/appointments/${encodeURIComponent(item.code)}` as never)}
          style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
          <View style={styles.cardHead}><Text style={styles.code}>{item.code}</Text><StatusPill status={item.status} /></View>
          <Text style={styles.when}>{formatWeekday(item.start)}, {formatDate(item.start)} · {formatClock(item.start)}–{formatClock(item.end)}</Text>
          <Text style={styles.line}>Nha sĩ: {item.dentistName}</Text>
          <Text style={styles.line}>Người khám: {item.attendeeName}{item.attendeeKind === 'RELATIVE' ? ' (người thân)' : ''}</Text>
          <Text style={styles.access}>{accessLabels[item.access]}</Text>
        </Pressable>)}
        {items.length < total && <Button title="Xem thêm" variant="secondary" busy={loadingMore} onPress={() => void load(page + 1, 'more')} />}
      </>}
  </ScrollView>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background },
  center: { alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.xl },
  muted: { color: palette.muted },
  tabs: { flexDirection: 'row', backgroundColor: '#EEF1F5', borderRadius: 10, padding: 4 },
  tab: { flex: 1, paddingVertical: 10, borderRadius: 8, alignItems: 'center' },
  tabSelected: { backgroundColor: '#fff' },
  tabText: { color: palette.muted, fontWeight: '600' },
  tabTextSelected: { color: palette.primary },
  toolbar: { flexDirection: 'row', justifyContent: 'space-between' },
  link: { color: palette.primary, fontWeight: '600', fontSize: 15 },
  label: { fontWeight: '600', color: palette.text },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  chip: { borderWidth: 1, borderColor: palette.border, borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6, backgroundColor: '#fff' },
  chipSelected: { borderColor: palette.primary, backgroundColor: '#EFF8FF' },
  chipText: { color: palette.text, fontSize: 13 },
  chipTextSelected: { color: palette.primary, fontWeight: '600' },
  emptyTitle: { fontWeight: '600', color: palette.text },
  card: { backgroundColor: '#fff', borderRadius: 12, padding: spacing.md, gap: 4, borderWidth: 1, borderColor: palette.border },
  pressed: { opacity: 0.85 },
  cardHead: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  code: { fontWeight: '700', color: palette.text, fontSize: 16 },
  when: { color: palette.text, fontWeight: '600' },
  line: { color: palette.text },
  access: { color: palette.muted, fontSize: 12, marginTop: 2 },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 3 },
  pillText: { fontSize: 12, fontWeight: '600' },
});
