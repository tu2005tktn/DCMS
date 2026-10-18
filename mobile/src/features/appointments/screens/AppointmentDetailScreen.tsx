import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { useSession } from '@/features/auth/session/useSession';
import { Banner, Button, Card, Field, palette } from '@/features/patient-link/components/LinkUi';
import { AppointmentApiError, appointmentsApi } from '../appointments.api';
import {
  addDays, clinicDate, formatClock, formatDate, formatDateTime, formatWeekday, ineligibleLabels, newRequestKey, reasonError,
  REASON_MAX, remainingLabel, statusLabels, supportKindLabels, supportStatusLabels,
} from '../appointments.rules';
import type { AppointmentDetail, AvailabilitySlot, SupportRequest, SupportRequestKind } from '../appointments.types';
import { StatusPill } from './MyAppointmentsScreen';

type Mode = 'view' | 'reschedule' | 'cancel' | 'support';
// A confirmed command keeps its requestKey until it succeeds, so a retry after a lost response is not applied twice.
type Pending = { key: string };

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

// S16 Appointment Detail and Change (UC-05, UC-06). Access is rechecked by code on every request; the URL is not trusted.
export default function AppointmentDetailScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [detail, setDetail] = useState<AppointmentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<AppointmentApiError | null>(null);
  const [mode, setMode] = useState<Mode>('view');
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<AppointmentApiError | null>(null);
  const [busy, setBusy] = useState(false);
  // Form state survives errors so the user does not retype.
  const [reason, setReason] = useState('');
  const [reasonMessage, setReasonMessage] = useState<string | undefined>();
  const [date, setDate] = useState(clinicDate(new Date()));
  const [slots, setSlots] = useState<AvailabilitySlot[] | null>(null);
  const [slotsError, setSlotsError] = useState<string | null>(null);
  const [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [kind, setKind] = useState<SupportRequestKind>('RESCHEDULE');
  const pending = useRef<Pending | null>(null);

  const handleAuth = useCallback((failure: unknown) => {
    if (failure instanceof AppointmentApiError && failure.status === 401) { void session.signOut('expired'); return true; }
    return false;
  }, [session.signOut]);

  const load = useCallback(async (refresh = false) => {
    if (!token || !code) return;
    if (refresh) setRefreshing(true); else setLoading(true);
    setLoadError(null);
    try { setDetail(await appointmentsApi.detail(token, code)); }
    catch (failure) { if (!handleAuth(failure)) { setDetail(null); setLoadError(failure as AppointmentApiError); } }
    finally { setLoading(false); setRefreshing(false); }
  }, [token, code, handleAuth]);

  useEffect(() => { void load(); }, [load]);

  const loadSlots = useCallback(async (day: string) => {
    if (!detail || !token) return;
    setSlots(null); setSlotsError(null); setSlot(null);
    try { setSlots(await appointmentsApi.availability(token, detail, day)); }
    catch (failure) { if (!handleAuth(failure)) setSlotsError((failure as Error).message); }
  }, [detail, token, handleAuth]);

  useEffect(() => { if (mode === 'reschedule') void loadSlots(date); }, [mode, date, loadSlots]);

  function open(next: Mode) {
    setMode(next); setNotice(null); setActionError(null); setReasonMessage(undefined); setReviewing(false);
    pending.current = null;
    if (next === 'reschedule' && detail) setDate(clinicDate(new Date(Math.max(Date.now(), Date.parse(detail.start) - 86_400_000))));
  }
  function close() { setMode('view'); setReason(''); setSlot(null); setReviewing(false); setActionError(null); pending.current = null; }

  function checkReason() {
    const message = reasonError(reason);
    setReasonMessage(message);
    return !message;
  }

  async function run<T>(action: (key: string) => Promise<T>, done: (result: T) => void) {
    if (!token || busy) return;
    pending.current ??= { key: newRequestKey() };
    setBusy(true); setActionError(null);
    try {
      done(await action(pending.current.key));
      pending.current = null;
    } catch (failure) {
      if (handleAuth(failure)) return;
      const error = failure as AppointmentApiError;
      setActionError(error);
      // A rejected command will not be replayed; a new attempt gets a new key. Connection failures keep the key.
      if (error.status !== 0) pending.current = null;
      if (error.code === 'MSG34') { setSlot(null); setReviewing(false); void loadSlots(date); }
    } finally { setBusy(false); }
  }

  function confirmReschedule() {
    if (!detail || !slot) return;
    void run(key => appointmentsApi.reschedule(token!, detail.code, { expectedVersion: detail.version, requestKey: key, newStart: slot.start,
      chairId: slot.chairId, scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: slot.assistantScheduleVersion, reason: reason.trim() }),
    result => { setDetail(result.appointment); close(); setNotice(result.notification.message); });
  }
  function confirmCancel() {
    if (!detail || !checkReason()) return;
    void run(key => appointmentsApi.cancel(token!, detail.code, { expectedVersion: detail.version, requestKey: key, reason: reason.trim() }),
      result => { setDetail(result.appointment); close(); setNotice(result.notification.message); });
  }
  function submitSupport() {
    if (!detail || !checkReason()) return;
    void run(key => appointmentsApi.requestSupport(token!, detail.code, { requestKey: key, kind, reason: reason.trim() }),
      result => { close(); setNotice(result.notification.message); void load(true); });
  }
  function withdraw(request: SupportRequest) {
    void run(() => appointmentsApi.withdrawSupport(token!, request.id, request.version), () => {
      setNotice('Đã rút yêu cầu hỗ trợ. Lịch hẹn không thay đổi.'); void load(true);
    });
  }

  if (!token) return null;
  if (loading && !detail) return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>;
  if (!detail) return <ScrollView contentContainerStyle={styles.container}>
    <Banner tone="error" message={loadError?.message ?? 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.'}
      action={loadError && loadError.status !== 404 && loadError.status !== 403 ? { label: 'Thử lại', onPress: () => void load() } : undefined} />
    <Button title="Về danh sách lịch hẹn" variant="secondary" onPress={() => router.replace('/appointments')} />
  </ScrollView>;

  const e = detail.eligibility;
  const pendingSupport = detail.supportRequests.find(r => r.status === 'Pending');
  const stale = actionError?.code === 'MSG111';
  const errorBanner = actionError && <Banner tone="error" message={actionError.message}
    action={stale ? { label: 'Tải lại lịch hẹn', onPress: () => { close(); void load(true); } } : undefined} />;
  const reasonField = <Field label={`Lý do * (tối đa ${REASON_MAX} ký tự)`} value={reason} multiline maxLength={REASON_MAX}
    error={reasonMessage} onChangeText={value => { setReason(value); if (reasonMessage) setReasonMessage(undefined); }}
    hint="Không ghi thông tin bệnh lý; thông báo gửi đi không chứa chẩn đoán." />;

  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled"
    refreshControl={mode === 'view' ? <RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} /> : undefined}>
    {notice && <Banner tone="success" message={notice} />}

    <View style={styles.head}>
      <View><Text style={styles.code}>{detail.code}</Text><Text style={styles.muted}>{detail.serviceName} · {detail.branchName}</Text></View>
      <StatusPill status={detail.status} />
    </View>

    <Card title="Thời gian">
      <Text style={styles.when}>{formatWeekday(detail.start)}, {formatDate(detail.start)}</Text>
      <Text style={styles.when}>{formatClock(detail.start)} – {formatClock(detail.end)} ({detail.durationMinutes} phút)</Text>
      <Row label="Nha sĩ" value={detail.dentistName} />
      {detail.followUp && <Row label="Lịch tái khám" value={detail.followUp.status === 'NeedsBooking' ? 'Cần đặt lại' : 'Đã gắn với lịch này'} />}
    </Card>

    <Card title="Người khám">
      <Row label="Họ tên" value={detail.attendeeName} />
      <Row label="Quan hệ" value={detail.attendeeKind === 'SELF' ? 'Bản thân' : 'Người thân'} />
      {detail.attendeeKind === 'RELATIVE' && <Text style={styles.muted}>Đặt hộ người thân không cho phép xem hồ sơ khám hay chi phí của họ.</Text>}
    </Card>
    <Card title="Người đặt">
      <Row label="Họ tên" value={detail.bookerName} />
      <Row label="Liên hệ" value={detail.bookerContactMasked} />
    </Card>

    {mode === 'view' && <Card title="Đổi hoặc hủy lịch">
      {detail.status === 'Confirmed' && e.hoursRemaining > 0 && <Text style={styles.remaining}>{remainingLabel(e.hoursRemaining)} đến giờ hẹn</Text>}
      {e.canSelfService ? <>
        <Text style={styles.muted}>Bạn có thể tự đổi hoặc hủy khi còn ít nhất 12 giờ trước giờ hẹn.</Text>
        <Button title="Đổi lịch" onPress={() => open('reschedule')} />
        <Button title="Hủy lịch" variant="danger" onPress={() => open('cancel')} />
      </> : <>
        <Text style={styles.muted}>{e.reasonCode ? ineligibleLabels[e.reasonCode] : ''}</Text>
        {e.canRequestSupport && !pendingSupport && <Button title="Gửi yêu cầu hỗ trợ" onPress={() => open('support')} />}
        {e.canRequestSupport && pendingSupport && <Text style={styles.muted}>Bạn đã có một yêu cầu hỗ trợ đang chờ xử lý cho lịch này.</Text>}
      </>}
    </Card>}

    {mode === 'reschedule' && <Card title="Đổi lịch hẹn">
      {errorBanner}
      <Text style={styles.muted}>Giờ hiện tại vẫn giữ nguyên cho đến khi đổi thành công. Thời lượng {detail.durationMinutes} phút và nha sĩ giữ như lịch gốc.</Text>
      {!reviewing ? <>
        <View style={styles.dateBar}>
          <Button title="‹" variant="secondary" disabled={date <= clinicDate(new Date())} onPress={() => setDate(addDays(date, -1))} />
          <Text style={styles.dateText}>{formatWeekday(`${date}T05:00:00Z`)}, {formatDate(`${date}T05:00:00Z`)}</Text>
          <Button title="›" variant="secondary" onPress={() => setDate(addDays(date, 1))} />
        </View>
        {slotsError ? <Banner tone="error" message={slotsError} action={{ label: 'Thử lại', onPress: () => void loadSlots(date) }} />
          : slots === null ? <ActivityIndicator color={palette.primary} />
          : slots.length === 0 ? <Text style={styles.muted}>Không còn khung giờ phù hợp trong ngày này. Vui lòng chọn ngày khác.</Text>
          : <View style={styles.slots}>{slots.map(s => {
            const selected = slot?.start === s.start && slot.chairId === s.chairId;
            return <Pressable key={`${s.start}-${s.chairId}`} accessibilityRole="radio" accessibilityState={{ selected }} onPress={() => setSlot(s)}
              style={[styles.slot, selected && styles.slotSelected]}><Text style={[styles.slotText, selected && styles.slotTextSelected]}>{formatClock(s.start)}</Text></Pressable>;
          })}</View>}
        {reasonField}
        <Button title="Xem lại" disabled={!slot} onPress={() => { if (checkReason()) setReviewing(true); }} />
      </> : <>
        <Row label="Giờ cũ" value={formatDateTime(detail.start)} />
        <Row label="Giờ mới" value={slot ? formatDateTime(slot.start) : '—'} />
        <Row label="Nha sĩ" value={detail.dentistName} />
        <Row label="Lý do" value={reason.trim()} />
        <Button title="Xác nhận đổi lịch" busy={busy} onPress={confirmReschedule} />
        <Button title="Chọn lại" variant="secondary" disabled={busy} onPress={() => setReviewing(false)} />
      </>}
      <Button title="Không đổi nữa" variant="secondary" disabled={busy} onPress={close} />
    </Card>}

    {mode === 'cancel' && <Card title="Hủy lịch hẹn">
      {errorBanner}
      <Text style={styles.muted}>Lịch {detail.code} lúc {formatDateTime(detail.start)} sẽ bị hủy. {detail.followUp ? 'Lịch tái khám liên quan sẽ chuyển về trạng thái cần đặt lại.' : ''}</Text>
      {reasonField}
      <Button title="Xác nhận hủy lịch" variant="danger" busy={busy} onPress={confirmCancel} />
      <Button title="Không hủy" variant="secondary" disabled={busy} onPress={close} />
    </Card>}

    {mode === 'support' && <Card title="Yêu cầu hỗ trợ">
      {errorBanner}
      <Text style={styles.muted}>Lịch hẹn hiện tại vẫn có hiệu lực cho đến khi phòng khám xử lý thành công. Gửi yêu cầu không giữ khung giờ mới.</Text>
      <View style={styles.slots}>{(Object.keys(supportKindLabels) as SupportRequestKind[]).map(value =>
        <Pressable key={value} accessibilityRole="radio" accessibilityState={{ selected: kind === value }} onPress={() => setKind(value)}
          style={[styles.slot, kind === value && styles.slotSelected]}><Text style={[styles.slotText, kind === value && styles.slotTextSelected]}>{supportKindLabels[value]}</Text></Pressable>)}</View>
      {reasonField}
      <Button title="Gửi yêu cầu" busy={busy} onPress={submitSupport} />
      <Button title="Không gửi" variant="secondary" disabled={busy} onPress={close} />
    </Card>}

    {mode === 'view' && actionError && errorBanner}

    {detail.supportRequests.length > 0 && <Card title="Yêu cầu hỗ trợ">
      {detail.supportRequests.map(r => <View key={r.id} style={styles.request}>
        <Text style={styles.rowValue}>{supportKindLabels[r.kind]} · {supportStatusLabels[r.status]}</Text>
        <Text style={styles.muted}>Gửi lúc {formatDateTime(r.createdAt)}{r.decidedAt ? ` · Cập nhật ${formatDateTime(r.decidedAt)}` : ''}</Text>
        <Text style={styles.line}>Lý do: {r.reason}</Text>
        {r.publicNote && <Text style={styles.line}>Phản hồi: {r.publicNote}</Text>}
        {r.status === 'Pending' && <Button title="Rút yêu cầu" variant="secondary" busy={busy} onPress={() => withdraw(r)} />}
      </View>)}
    </Card>}

    {detail.history.length > 0 && <Card title="Lịch sử thay đổi">
      {detail.history.map((h, index) => <View key={index} style={styles.request}>
        <Text style={styles.rowValue}>{h.action === 'CANCEL' ? 'Hủy lịch' : 'Đổi lịch'} · {h.actorKind === 'SELF' ? 'Bạn thực hiện' : 'Phòng khám thực hiện'}</Text>
        <Text style={styles.muted}>{formatDateTime(h.occurredAt)}</Text>
        {h.newStart && <Text style={styles.line}>{formatDateTime(h.oldStart)} → {formatDateTime(h.newStart)}</Text>}
        <Text style={styles.line}>Lý do: {h.reason}</Text>
      </View>)}
    </Card>}

    <Card title="Liên hệ phòng khám">
      <Row label="Điện thoại" value={detail.clinicContact.phone} />
      <Row label="Giờ tiếp nhận" value={detail.clinicContact.hours} />
      <Text style={styles.muted}>Yêu cầu gửi ngoài giờ tiếp nhận sẽ được xử lý trong giờ làm việc kế tiếp.</Text>
    </Card>
    <Text style={styles.muted}>Trạng thái: {statusLabels[detail.status]} · Phiên bản {detail.version}</Text>
  </ScrollView>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  muted: { color: palette.muted },
  head: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: spacing.md },
  code: { fontSize: 22, fontWeight: '700', color: palette.text },
  when: { fontSize: 16, fontWeight: '600', color: palette.text },
  remaining: { fontWeight: '600', color: palette.primary },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { color: palette.muted, flexShrink: 0 },
  rowValue: { color: palette.text, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  line: { color: palette.text },
  dateBar: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  dateText: { flex: 1, textAlign: 'center', fontWeight: '600', color: palette.text },
  slots: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  slot: { borderWidth: 1, borderColor: palette.border, borderRadius: 8, paddingHorizontal: 14, paddingVertical: 10, backgroundColor: '#fff' },
  slotSelected: { borderColor: palette.primary, backgroundColor: palette.primary },
  slotText: { color: palette.text, fontWeight: '600' },
  slotTextSelected: { color: '#fff' },
  request: { gap: 2, paddingVertical: spacing.xs, borderTopWidth: 1, borderTopColor: palette.border },
});
