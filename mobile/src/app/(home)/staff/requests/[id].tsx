/**
 * S12/S26 — StaffRequestDetailScreen (mobile).
 *
 * Đọc `GET /api/support-requests/{id}` và chạy command thật cùng hợp đồng với web:
 *  - Yêu cầu đổi/huỷ lịch: một bước `change-appointment` — server tự xác minh người yêu cầu và đóng yêu cầu
 *    (S12, 25/11/2026). Lý do bắt buộc; đổi lịch chọn ngày và khung giờ trống từ `GET /api/availability`.
 *  - Loại khác: xác minh người yêu cầu (`verify-requester`, tùy chọn) rồi Resolve.
 *  - Reject (`reject`) bắt buộc lý do; Withdrawn không được xử lý (GBR-075).
 *
 * Quyền (`permission`) do server tính. Lỗi command hiển thị lỗi thật, không báo thành công giả;
 * 409 → tải lại bản mới nhất. `requestKey` giữ nguyên khi thử lại cùng nội dung sau lỗi mạng/5xx.
 */

import { useLocalSearchParams } from 'expo-router';
import { uuid } from 'expo-modules-core';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { supportRequestsApi, type AvailabilitySlot } from '@/features/support-inbox/support-requests.api';
import {
  appointmentStatusLabels,
  kindLabels,
  reasonLabels,
  statusLabels,
  type ChangeReason,
  type CommandResult,
  type SupportRequestDetail,
  type SupportRequestStatus,
} from '@/features/support-inbox/support-requests.types';
import { ApiError } from '@/services/api/api-error';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'denied'; reason: string }
  | { kind: 'not_found' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; request: SupportRequestDetail };

type ModalKind = 'change' | 'cancel' | 'resolve' | 'reject';

const REASONS: ChangeReason[] = ['PatientRequest', 'DentistUnavailable', 'ChairUnavailable', 'ScheduleReduced', 'Other'];
const VN_OFFSET_MS = 7 * 3_600_000; // Asia/Ho_Chi_Minh, no DST.

/** Clinic date (YYYY-MM-DD) of an instant, in Asia/Ho_Chi_Minh. */
function clinicDate(value: Date | string): string {
  return new Date(new Date(value).getTime() + VN_OFFSET_MS).toISOString().slice(0, 10);
}

function nextClinicDates(count: number): string[] {
  const today = new Date(clinicDate(new Date()) + 'T00:00:00Z').getTime();
  return Array.from({ length: count }, (_, i) => new Date(today + i * 86_400_000).toISOString().slice(0, 10));
}

function dateChipLabel(date: string): string {
  const [, m, d] = date.split('-');
  const weekday = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'][new Date(date + 'T00:00:00Z').getUTCDay()];
  return `${weekday} ${d}/${m}`;
}

function clock(iso: string): string {
  return new Date(new Date(iso).getTime() + VN_OFFSET_MS).toISOString().slice(11, 16);
}

export default function StaffRequestDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { state, signOut } = useSession();
  const token = state.status === 'authenticated' ? state.accessToken : null;

  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [modal, setModal] = useState<ModalKind | null>(null);
  const [reason, setReason] = useState<ChangeReason>('PatientRequest');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [newStartAt, setNewStartAt] = useState<string | null>(null);
  const [slotDate, setSlotDate] = useState(() => clinicDate(new Date()));
  const [slots, setSlots] = useState<{ status: 'idle' | 'loading' | 'ready' | 'error'; items: AvailabilitySlot[] }>({ status: 'idle', items: [] });
  const retryKeys = useRef<Record<string, { fingerprint: string; key: string }>>({});

  const requestKey = (action: string, fingerprint: string) => {
    const previous = retryKeys.current[action];
    if (previous?.fingerprint === fingerprint) return previous.key;
    const key = uuid.v4();
    retryKeys.current[action] = { fingerprint, key };
    return key;
  };

  const reload = useCallback(async () => {
    if (!id || !token) { setLoad({ kind: 'denied', reason: 'Bạn cần đăng nhập bằng tài khoản nhân viên.' }); return; }
    try {
      setLoad({ kind: 'ready', request: await supportRequestsApi.get(id, token) });
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) { void signOut('expired'); return; }
      if (error instanceof ApiError && error.status === 403) { setLoad({ kind: 'denied', reason: error.message }); return; }
      if (error instanceof ApiError && error.status === 404) { setLoad({ kind: 'not_found' }); return; }
      setLoad({ kind: 'error', message: error instanceof ApiError ? error.message : 'Không thể kết nối máy chủ.' });
    }
  }, [id, token, signOut]);

  useEffect(() => { void reload(); }, [reload]);

  const appointmentForSlots = load.kind === 'ready' ? load.request.appointment : null;
  useEffect(() => {
    if (modal !== 'change' || !appointmentForSlots || !token) return;
    let active = true;
    setSlots({ status: 'loading', items: [] });
    supportRequestsApi.availability({
      branchId: appointmentForSlots.branchId, dentistId: appointmentForSlots.dentistId, serviceId: appointmentForSlots.serviceId,
      date: slotDate, durationMinutes: appointmentForSlots.durationMinutes,
    }, token).then(items => { if (active) setSlots({ status: 'ready', items }); })
      .catch(() => { if (active) setSlots({ status: 'error', items: [] }); });
    return () => { active = false; };
  }, [modal, slotDate, appointmentForSlots, token]);

  const openChange = (proposed?: string) => {
    setNewStartAt(proposed ?? null);
    setSlotDate(clinicDate(proposed ?? new Date()));
    setModal('change');
  };

  const finish = useCallback(async (action: string, result: CommandResult, success: string) => {
    if (!result.ok) {
      // Giữ key khi lỗi mạng/5xx để lần thử lại là idempotent; lỗi nghiệp vụ thì bỏ key.
      if (!['NETWORK', 'SERVER'].includes(result.errorCode)) delete retryKeys.current[action];
      if (result.errorCode === 'UNAUTHORIZED') { void signOut('expired'); return false; }
      Alert.alert('Không thể hoàn tất', result.errorMessage);
      if (result.errorCode === 'STALE_VERSION') await reload();
      return false;
    }
    delete retryKeys.current[action];
    if (result.request) setLoad({ kind: 'ready', request: result.request }); else await reload();
    Alert.alert('Thành công', success);
    return true;
  }, [reload, signOut]);

  const run = useCallback(async (kind: 'verify' | ModalKind) => {
    if (load.kind !== 'ready' || !token) return;
    const request = load.request;
    const trimmed = notes.trim() || undefined;
    if (kind === 'reject' && !trimmed) { Alert.alert('Thiếu lý do', 'Vui lòng nhập lý do từ chối.'); return; }
    if (kind === 'change' && !newStartAt) { Alert.alert('Chưa chọn giờ', 'Vui lòng chọn khung giờ mới.'); return; }
    const fingerprint = JSON.stringify({ id: request.id, version: request.expectedVersion, kind, reason, notes: trimmed,
      newStartAt: kind === 'change' ? newStartAt : null });
    setBusy(true);
    try {
      let ok = false;
      if (kind === 'verify') {
        const notesText = trimmed ?? 'Đã đối chiếu người yêu cầu với thông tin đặt lịch.';
        ok = await finish('verify', await supportRequestsApi.verifyRequester(request.id, {
          requestKey: requestKey('verify', fingerprint), expectedVersion: request.expectedVersion, notes: notesText,
        }, token), 'Đã xác minh người yêu cầu.');
      } else if (kind === 'change' || kind === 'cancel') {
        ok = await finish('change', await supportRequestsApi.changeAppointment(request.id, {
          requestKey: requestKey('change', fingerprint), expectedVersion: request.expectedVersion, reason, notes: trimmed,
          newStartAt: kind === 'change' ? newStartAt ?? undefined : undefined,
        }, token), kind === 'change' ? 'Đã đổi lịch và đóng yêu cầu.' : 'Đã huỷ lịch và đóng yêu cầu.');
      } else {
        ok = await finish(`decision-${kind}`, await supportRequestsApi.decide(request.id, {
          requestKey: requestKey(`decision-${kind}`, fingerprint), expectedVersion: request.expectedVersion,
          decision: kind === 'resolve' ? 'Resolve' : 'Reject',
          decisionReason: kind === 'reject' ? trimmed : undefined,
          notes: kind === 'resolve' ? trimmed : undefined,
        }, token), kind === 'resolve' ? 'Đã xử lý yêu cầu.' : 'Đã từ chối yêu cầu.');
      }
      if (ok) { setModal(null); setNotes(''); }
    } finally {
      setBusy(false);
    }
  }, [load, token, notes, reason, newStartAt, finish]);

  if (load.kind === 'loading') {
    return <Center><ActivityIndicator color={colors.primary} /><Text style={styles.muted}>Đang tải…</Text></Center>;
  }
  if (load.kind === 'not_found') return <Center><Text style={styles.muted}>Không tìm thấy yêu cầu.</Text></Center>;
  if (load.kind === 'denied') return <Center><Text style={styles.title}>Không có quyền</Text><Text style={styles.muted}>{load.reason}</Text></Center>;
  if (load.kind === 'error') return <Center><Text style={styles.title}>Lỗi</Text><Text style={styles.muted}>{load.message}</Text><Pressable onPress={() => void reload()} style={styles.retry}><Text style={styles.retryText}>Tải lại</Text></Pressable></Center>;

  const { request } = load;
  const { appointment, permission } = request;
  const withdrawn = request.status === 'Withdrawn';
  const closed = request.status === 'Resolved' || request.status === 'Rejected' || withdrawn;
  const needsAppointmentAction = request.kind === 'Change' || request.kind === 'Cancel';
  const canDecide = !needsAppointmentAction || request.appointmentActionCommitted;
  const proposed = request.proposedChange?.newStartAt ?? null;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.code}>{request.id}</Text>
        <Text style={styles.subtitle}>Yêu cầu cho lịch {appointment.code} · v{request.expectedVersion}</Text>

        <View style={styles.statusRow}>
          <Text style={styles.statusLabel}>Trạng thái:</Text>
          <View style={[styles.statusPill, pillBg(request.status)]}>
            <Text style={styles.statusPillText}>{statusLabels[request.status] ?? request.status}</Text>
          </View>
        </View>

        {!permission.branchMatch ? (
          <Text style={styles.warn}>⚠ Yêu cầu không thuộc chi nhánh của bạn — không thể xử lý.</Text>
        ) : null}

        <Section title="Thông tin yêu cầu">
          <Row label="Loại" value={kindLabels[request.kind] ?? request.kind} />
          <Row label="Lý do" value={request.reason || '—'} />
          {request.proposedChange ? (
            <Row label="Giờ mới đề xuất" value={request.proposedChange.newStartAt ? fmt(request.proposedChange.newStartAt) : '—'} />
          ) : null}
          <Row label="Yêu cầu bởi" value={`${request.requester.name}${request.requester.isAnonymous ? ' (vãng lai)' : ''}`} />
          <Row label="Ngày tạo" value={fmt(request.createdAt)} />
          <Row label="Cập nhật" value={fmt(request.updatedAt)} />
          {request.decisionReason ? <Row label="Lý do quyết định" value={request.decisionReason} /> : null}
          {request.deciderName ? <Row label="Người xử lý" value={request.deciderName} /> : null}
        </Section>

        <Section title="Lịch hẹn liên quan">
          <Row label="Mã lịch" value={appointment.code} />
          <Row label="Trạng thái" value={appointmentStatusLabels[appointment.status] ?? appointment.status} />
          <Row label="Bắt đầu" value={fmt(appointment.startAt)} />
          <Row label="Kết thúc" value={fmt(appointment.endAt)} />
          <Row label="Chi nhánh" value={appointment.branchName || '—'} />
          <Row label="Dịch vụ" value={appointment.serviceName || '—'} />
          <Row label="Bác sĩ" value={appointment.dentistName || '—'} />
        </Section>

        <Section title="Lịch sử yêu cầu">
          {request.history.length === 0 ? <Text style={styles.muted}>Chưa có lịch sử.</Text> : request.history.map((h, idx) => (
            <View key={idx} style={styles.historyRow}>
              <Text style={styles.historyWhen}>{fmt(h.at)}</Text>
              <Text style={styles.historyAction}>{actionLabel(h.action)}{h.notes ? ` — ${h.notes}` : ''}</Text>
              <Text style={styles.historyActor}>bởi {h.actor}</Text>
            </View>
          ))}
        </Section>

        <Section title="Thao tác">
          {withdrawn ? (
            <Text style={styles.muted}>⚠ Yêu cầu đã bị rút bởi người yêu cầu — không thể xử lý (GBR-075).</Text>
          ) : closed ? (
            <Text style={styles.muted}>Yêu cầu đã đóng. Không thể thực hiện thêm thao tác.</Text>
          ) : !permission.canResolve ? (
            <Text style={styles.muted}>Bạn không có quyền xử lý yêu cầu này.</Text>
          ) : (
            <View style={{ gap: spacing.sm }}>
              {request.status === 'Pending' && !needsAppointmentAction ? (
                <PrimaryButton label="Xác minh người yêu cầu" onPress={() => void run('verify')} loading={busy} disabled={busy} testID="verify-requester" />
              ) : null}
              {request.kind === 'Change' && !request.appointmentActionCommitted && permission.canEdit ? (
                <PrimaryButton label="Đổi lịch theo yêu cầu" onPress={() => openChange(request.proposedChange?.newStartAt)} loading={busy} disabled={busy} testID="staff-change" />
              ) : null}
              {request.kind === 'Cancel' && !request.appointmentActionCommitted && permission.canEdit ? (
                <PrimaryButton label="Huỷ lịch theo yêu cầu" onPress={() => setModal('cancel')} loading={busy} disabled={busy} testID="staff-cancel" />
              ) : null}
              {!canDecide ? null : (
                <PrimaryButton label="Đánh dấu đã xử lý" onPress={() => setModal('resolve')} loading={busy} disabled={busy} testID="staff-resolve" />
              )}
              <PrimaryButton label="Từ chối yêu cầu" onPress={() => setModal('reject')} loading={busy} disabled={busy} testID="staff-reject" />
            </View>
          )}
        </Section>

        <View style={{ height: spacing.xl }} />
      </ScrollView>

      <Modal visible={modal !== null} animationType="slide" transparent onRequestClose={() => setModal(null)}>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>
              {modal === 'change' && 'Đổi lịch'}
              {modal === 'cancel' && 'Huỷ lịch'}
              {modal === 'resolve' && 'Đánh dấu đã xử lý'}
              {modal === 'reject' && 'Từ chối yêu cầu'}
            </Text>
            {modal === 'change' ? (
              <View>
                <Text style={styles.modalLabel}>Ngày khám mới *</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.reasonRow}>
                  {nextClinicDates(21).map(date => (
                    <Pressable key={date} onPress={() => setSlotDate(date)} style={[styles.reasonChip, slotDate === date && styles.reasonChipActive]}>
                      <Text style={[styles.reasonChipText, slotDate === date && styles.reasonChipTextActive]}>{dateChipLabel(date)}</Text>
                    </Pressable>
                  ))}
                </ScrollView>
                <Text style={styles.modalLabel}>Khung giờ trống *</Text>
                {proposed && clinicDate(proposed) === slotDate && !slots.items.some(s => s.start === proposed) ? (
                  <Pressable onPress={() => setNewStartAt(proposed)}
                    style={[styles.reasonChip, newStartAt === proposed && styles.reasonChipActive, styles.proposedChip]}>
                    <Text style={[styles.reasonChipText, newStartAt === proposed && styles.reasonChipTextActive]}>
                      {clock(proposed)} (bệnh nhân đề xuất)
                    </Text>
                  </Pressable>
                ) : null}
                {slots.status === 'loading' ? <ActivityIndicator color={colors.primary} />
                  : slots.status === 'error' ? <Text style={styles.warn}>Không tải được khung giờ trống. Chọn ngày khác hoặc thử lại.</Text>
                    : slots.items.length === 0 ? <Text style={styles.muted}>Không còn khung giờ trống trong ngày này.</Text>
                      : <View style={styles.reasonRow}>
                        {slots.items.map(slot => (
                          <Pressable key={`${slot.start}-${slot.chairId}`} onPress={() => setNewStartAt(slot.start)}
                            style={[styles.reasonChip, newStartAt === slot.start && styles.reasonChipActive]}>
                            <Text style={[styles.reasonChipText, newStartAt === slot.start && styles.reasonChipTextActive]}>{clock(slot.start)}</Text>
                          </Pressable>
                        ))}
                      </View>}
                {newStartAt ? <Text style={styles.muted}>Giờ mới: {fmt(newStartAt)}</Text> : null}
              </View>
            ) : null}
            {modal === 'change' || modal === 'cancel' ? (
              <View>
                <Text style={styles.modalLabel}>Lý do *</Text>
                <View style={styles.reasonRow}>
                  {REASONS.map((r) => (
                    <Pressable key={r} onPress={() => setReason(r)} style={[styles.reasonChip, reason === r && styles.reasonChipActive]}>
                      <Text style={[styles.reasonChipText, reason === r && styles.reasonChipTextActive]}>{reasonLabels[r]}</Text>
                    </Pressable>
                  ))}
                </View>
              </View>
            ) : null}
            <Text style={styles.modalLabel}>{modal === 'reject' ? 'Lý do từ chối *' : 'Ghi chú'}</Text>
            <TextInput
              style={styles.input}
              placeholder={modal === 'reject' ? 'Lý do gửi cho người yêu cầu' : 'Ghi chú cho lịch sử xử lý'}
              value={notes}
              onChangeText={setNotes}
              multiline
              maxLength={500}
            />
            <View style={styles.modalActions}>
              <Pressable onPress={() => setModal(null)} style={styles.modalBtn}><Text style={styles.modalBtnText}>Đóng</Text></Pressable>
              <PrimaryButton label={busy ? 'Đang xử lý…' : 'Xác nhận'} onPress={() => { if (modal) void run(modal); }} loading={busy} disabled={busy} />
            </View>
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

function Center({ children }: { children: React.ReactNode }) {
  return <SafeAreaView style={styles.center}><View style={{ alignItems: 'center', gap: spacing.sm }}>{children}</View></SafeAreaView>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <View style={styles.section}><Text style={styles.sectionTitle}>{title}</Text><View style={styles.sectionBody}>{children}</View></View>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

function fmt(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
}

function pillBg(s: SupportRequestStatus) {
  switch (s) {
    case 'Pending': return { backgroundColor: '#FFF7CC' };
    case 'InReview': return { backgroundColor: '#D6E8FB' };
    case 'Resolved': return { backgroundColor: '#D6F5E0' };
    case 'Rejected': return { backgroundColor: '#FBDADA' };
    default: return { backgroundColor: '#E5E5E5' };
  }
}

function actionLabel(a: string): string {
  switch (a) {
    case 'RequestSubmitted': return 'Gửi yêu cầu';
    case 'RequestResolved': return 'Staff xử lý yêu cầu';
    case 'RequestRejected': return 'Staff từ chối yêu cầu';
    case 'RequestWithdrawn': return 'Người yêu cầu rút yêu cầu';
    case 'InReview': return 'Staff xem xét';
    case 'Rescheduled': return 'Đổi lịch';
    case 'Cancelled': return 'Huỷ lịch';
    default: return a;
  }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.lg, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text, textAlign: 'center' },
  code: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1 },
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginTop: spacing.xs },
  statusLabel: { color: colors.muted, fontSize: typography.caption + 1 },
  statusPill: { paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 6 },
  statusPillText: { fontSize: typography.caption, fontWeight: '600' },
  section: { backgroundColor: colors.surface, borderRadius: 12, borderWidth: 1, borderColor: colors.border, padding: spacing.md, gap: spacing.xs },
  sectionTitle: { fontSize: typography.body, fontWeight: '700', color: colors.primary, marginBottom: spacing.xs, textTransform: 'uppercase', letterSpacing: 0.5 },
  sectionBody: { gap: spacing.xs },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  rowLabel: { color: colors.muted, fontSize: typography.caption + 1 },
  rowValue: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '500', maxWidth: '65%', textAlign: 'right' },
  muted: { color: colors.muted, fontSize: typography.caption + 1 },
  warn: { color: colors.danger, fontSize: typography.caption, marginTop: spacing.xs },
  retry: { marginTop: spacing.sm, paddingVertical: spacing.sm, paddingHorizontal: spacing.lg, backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
  historyRow: { paddingVertical: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border },
  historyWhen: { color: colors.muted, fontSize: typography.caption },
  historyAction: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '600' },
  historyActor: { color: colors.muted, fontSize: typography.caption, fontStyle: 'italic' },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: { backgroundColor: colors.surface, borderTopLeftRadius: 16, borderTopRightRadius: 16, padding: spacing.lg, gap: spacing.sm },
  modalTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  modalLabel: { color: colors.muted, fontSize: typography.caption + 1, marginTop: spacing.xs },
  reasonRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs },
  reasonChip: { paddingVertical: 4, paddingHorizontal: spacing.sm, borderRadius: 6, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.background },
  reasonChipActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  reasonChipText: { color: colors.text, fontSize: typography.caption },
  proposedChip: { alignSelf: 'flex-start', marginBottom: spacing.xs },
  reasonChipTextActive: { color: colors.onPrimary, fontWeight: '600' },
  input: { minHeight: 60, borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm, color: colors.text, textAlignVertical: 'top' },
  modalActions: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm, gap: spacing.sm },
  modalBtn: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, borderRadius: 8, borderWidth: 1, borderColor: colors.border, minWidth: 80, alignItems: 'center' },
  modalBtnText: { color: colors.text, fontWeight: '600' },
});
