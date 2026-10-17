/**
 * S12 — AppointmentDetailScreen.
 *
 * Hiển thị: code/lifecycle, booker/attendee riêng, branch/chair/staff/start/end, lịch sử và
 * trạng thái support request.
 *
 * Phân biệt rõ các trạng thái UI:
 *  - loading / empty / connection error / access-denied / stale-conflict / ready.
 *
 * Nút hành động:
 *  - Tạo support request (Đổi / Huỷ / Xác nhận tham dự) — mở modal.
 *  - Nếu request Pending/InReview: hiện nút rút.
 *  - Nếu người xem không phải booker/attendee (deep link từ S26 chưa verify): hiện "denied".
 *
 * Khi TV2 API thật sẵn sàng: thay `appointmentMockRepo` bằng http client; UI không đổi.
 */

import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { appointmentMockRepo } from '@/features/appointments/appointment.mock';
import type {
  AppointmentDto,
  AppointmentStatus,
  ChangeReason,
  SupportRequestDto,
  SupportRequestKind,
} from '@/features/appointments/appointment.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'denied'; reason: string }
  | { kind: 'not_found' }
  | { kind: 'error'; message: string }
  | { kind: 'stale'; reason: string }
  | { kind: 'ready'; appt: AppointmentDto; activeRequest: SupportRequestDto | null };

const STATUS_LABELS: Record<AppointmentStatus, string> = {
  Pending: 'Chờ xác minh',
  Confirmed: 'Đã xác nhận',
  Rescheduled: 'Đã đổi lịch',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã huỷ',
  NoShow: 'Không đến',
};

export default function AppointmentDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { state } = useSession();
  const accountId = state.status === 'authenticated' ? state.user.id : null;

  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [modal, setModal] = useState<null | { kind: SupportRequestKind; newStartIso?: string }>(null);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const reload = useCallback(async () => {
    if (!id) return;
    try {
      const appt = await appointmentMockRepo.getById(id);
      if (!appt) {
        setLoad({ kind: 'not_found' });
        return;
      }
      // Deep link từ S26 re-check quyền (S26 tiêu chí).
      const isOwner =
        (accountId !== null && appt.booker.accountId === accountId) ||
        (accountId !== null && appt.attendee.patientId === accountId);
      if (accountId !== null && !isOwner) {
        setLoad({ kind: 'denied', reason: 'Bạn không có quyền xem lịch hẹn này.' });
        return;
      }
      // Tìm active request.
      const reqs = accountId ? await appointmentMockRepo.listRequestsForAccount(accountId) : [];
      const active = reqs.find(
        (r) => r.appointmentId === appt.id && (r.status === 'Pending' || r.status === 'InReview'),
      ) ?? null;
      setLoad({ kind: 'ready', appt, activeRequest: active });
    } catch {
      setLoad({ kind: 'error', message: 'Không thể kết nối máy chủ. Vui lòng thử lại.' });
    }
  }, [id, accountId]);

  useEffect(() => { void reload(); }, [reload]);

  const submitRequest = useCallback(async () => {
    if (!load || load.kind !== 'ready' || !modal) return;
    if (accountId === null) {
      Alert.alert('Cần đăng nhập', 'Vui lòng đăng nhập để gửi yêu cầu.');
      return;
    }
    setSubmitting(true);
    try {
      const res = await appointmentMockRepo.createRequest({
        appointmentId: load.appt.id,
        kind: modal.kind,
        reason: reason.trim() || null,
        requester: {
          accountId,
          name: state.status === 'authenticated' ? state.user.fullName : 'Tôi',
          phone: null,
          isAnonymous: false,
        },
        proposedChange: modal.kind === 'Change' ? {
          newStartAt: modal.newStartIso,
          reason: 'PatientRequest' as ChangeReason,
        } : undefined,
        requestKey: `req-${load.appt.id}-${modal.kind}-${Date.now()}`,
      });
      if (!res.ok) {
        Alert.alert('Lỗi', res.errorMessage ?? 'Không thể tạo yêu cầu.');
        return;
      }
      setModal(null);
      setReason('');
      void reload();
    } finally {
      setSubmitting(false);
    }
  }, [load, modal, accountId, reason, state, reload]);

  const withdraw = useCallback(async () => {
    if (!load || load.kind !== 'ready' || !load.activeRequest || accountId === null) return;
    Alert.alert('Rút yêu cầu?', 'Bạn sẽ không thể resolve lại sau khi rút.', [
      { text: 'Huỷ', style: 'cancel' },
      {
        text: 'Rút',
        style: 'destructive',
        onPress: async () => {
          const res = await appointmentMockRepo.withdrawRequest(
            load.activeRequest!.id, accountId, load.activeRequest!.expectedVersion,
          );
          if (!res.ok) Alert.alert('Lỗi', res.errorMessage ?? 'Không thể rút yêu cầu.');
          else void reload();
        },
      },
    ]);
  }, [load, accountId, reload]);

  if (load.kind === 'loading') {
    return <Center><ActivityIndicator color={colors.primary} /><Text style={styles.muted}>Đang tải lịch hẹn…</Text></Center>;
  }
  if (load.kind === 'not_found') {
    return <Center><Text style={styles.muted}>Không tìm thấy lịch hẹn.</Text></Center>;
  }
  if (load.kind === 'denied') {
    return <Center><Text style={styles.title}>Không có quyền truy cập</Text><Text style={styles.muted}>{load.reason}</Text></Center>;
  }
  if (load.kind === 'error') {
    return <Center><Text style={styles.title}>Lỗi kết nối</Text><Text style={styles.muted}>{load.message}</Text><Pressable onPress={() => void reload()} style={styles.retry}><Text style={styles.retryText}>Thử lại</Text></Pressable></Center>;
  }
  if (load.kind === 'stale') {
    return <Center><Text style={styles.title}>Dữ liệu đã cũ</Text><Text style={styles.muted}>{load.reason}</Text><Pressable onPress={() => void reload()} style={styles.retry}><Text style={styles.retryText}>Tải lại</Text></Pressable></Center>;
  }

  const { appt, activeRequest } = load;
  const editable = appt.status === 'Pending' || appt.status === 'Confirmed' || appt.status === 'Rescheduled';
  const attendeesOnly = appt.attendeeRelation === 'Self'
    ? 'Bản thân'
    : appt.attendeeRelation === 'Relative' ? 'Người thân' : '—';

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.code}>{appt.code}</Text>
        <Text style={styles.subtitle}>ID hệ thống: {appt.id} · v{appt.version}</Text>

        <View style={styles.statusRow}>
          <Text style={styles.statusLabel}>Trạng thái lịch:</Text>
          <View style={styles.statusPill}><Text style={styles.statusPillText}>{STATUS_LABELS[appt.status]}</Text></View>
        </View>

        <Section title="Chi tiết lịch">
          <Row label="Dịch vụ" value={appt.serviceName || '—'} />
          <Row label="Chi nhánh" value={appt.branchName || '—'} />
          <Row label="Ghế" value={appt.chairId !== null ? `Ghế ${appt.chairId}` : '—'} />
          <Row label="Bác sĩ" value={appt.dentistName || '—'} />
          <Row label="Trợ thủ" value={appt.assistantId !== null ? `#${appt.assistantId}` : '—'} />
          <Row label="Bắt đầu" value={fmt(appt.startAt)} />
          <Row label="Kết thúc" value={fmt(appt.endAt)} />
          <Row label="Thời lượng" value={`${appt.durationMinutes} phút`} />
          <Row label="Nguồn" value={appt.source} />
          <Row label="Lý do khám" value={appt.reason || '—'} />
        </Section>

        <Section title="Booker & Attendee">
          <Row label="Booker" value={`${appt.booker.name}${appt.booker.isAnonymous ? ' (vãng lai)' : ''}`} />
          <Row label="SĐT booker" value={appt.booker.phone || '—'} />
          <Row label="Email booker" value={appt.booker.email || '—'} />
          <View style={styles.divider} />
          <Row label="Attendee" value={appt.attendee.name} />
          <Row label="Quan hệ" value={attendeesOnly} />
          <Row label="Ngày sinh" value={appt.attendee.birthDate || '—'} />
          <Row label="Mã BN" value={appt.attendee.patientId !== null ? `#${appt.attendee.patientId}` : '—'} />
        </Section>

        <Section title="Xác nhận tham dự">
          <Row label="Cờ ConfirmedToAttend" value={flagLabel(appt.confirmedToAttend)} />
          <Text style={styles.note}>
            Theo GBR-013/077: cờ này độc lập với Appointment lifecycle. Thay đổi cờ không check-in
            hoặc tạo Visit.
          </Text>
        </Section>

        <Section title="Yêu cầu hỗ trợ">
          {activeRequest ? (
            <View>
              <Row label="Mã yêu cầu" value={activeRequest.id} />
              <Row label="Loại" value={kindLabel(activeRequest.kind)} />
              <Row label="Trạng thái" value={statusLabel(activeRequest.status)} />
              <Row label="Cập nhật" value={fmt(activeRequest.updatedAt)} />
              {activeRequest.reason ? <Row label="Lý do" value={activeRequest.reason} /> : null}
              <PrimaryButton
                label="Rút yêu cầu"
                onPress={() => void withdraw()}
                disabled={submitting}
                testID="withdraw-request"
              />
            </View>
          ) : (
            <Text style={styles.muted}>Chưa có yêu cầu nào đang mở.</Text>
          )}
        </Section>

        <Section title="Hành động">
          {!editable ? (
            <Text style={styles.muted}>
              Lịch ở trạng thái "{STATUS_LABELS[appt.status]}" — không thể gửi yêu cầu đổi/huỷ.
            </Text>
          ) : activeRequest ? (
            <Text style={styles.muted}>
              Đã có yêu cầu đang mở. Vui lòng rút trước khi gửi yêu cầu mới.
            </Text>
          ) : (
            <View style={{ gap: spacing.sm }}>
              <PrimaryButton
                label="Yêu cầu đổi lịch"
                onPress={() => setModal({ kind: 'Change' })}
                disabled={submitting}
                testID="request-change"
              />
              <PrimaryButton
                label="Yêu cầu huỷ lịch"
                onPress={() => setModal({ kind: 'Cancel' })}
                disabled={submitting}
                testID="request-cancel"
              />
              <PrimaryButton
                label={appt.confirmedToAttend === 'Yes' ? 'Rút xác nhận tham dự' : 'Xác nhận tham dự'}
                onPress={() => setModal({ kind: appt.confirmedToAttend === 'Yes' ? 'WithdrawAttend' : 'ConfirmToAttend' })}
                disabled={submitting}
                testID="request-attend"
              />
            </View>
          )}
        </Section>

        <Section title="Lịch sử thay đổi">
          {appt.history.map((h, idx) => (
            <View key={idx} style={styles.historyRow}>
              <Text style={styles.historyWhen}>{fmt(h.at)}</Text>
              <Text style={styles.historyAction}>{actionLabel(h.action)}{h.notes ? ` — ${h.notes}` : ''}</Text>
              <Text style={styles.historyActor}>bởi {h.actor}{h.actorUserId ? ` (#${h.actorUserId})` : ''}</Text>
              {h.before ? <Text style={styles.historyDetail}>trước: {JSON.stringify(h.before)}</Text> : null}
              {h.after ? <Text style={styles.historyDetail}>sau: {JSON.stringify(h.after)}</Text> : null}
            </View>
          ))}
        </Section>

        <View style={{ height: spacing.xl }} />
      </ScrollView>

      <Modal visible={modal !== null} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>
              {modal?.kind === 'Change' && 'Yêu cầu đổi lịch'}
              {modal?.kind === 'Cancel' && 'Yêu cầu huỷ lịch'}
              {modal?.kind === 'ConfirmToAttend' && 'Xác nhận tham dự'}
              {modal?.kind === 'WithdrawAttend' && 'Rút xác nhận tham dự'}
            </Text>
            <Text style={styles.muted}>
              {modal?.kind === 'Change' && 'Mô tả khung giờ mới bạn muốn (nếu có). Staff sẽ xem xét và chọn khung phù hợp.'}
              {modal?.kind === 'Cancel' && 'Cho staff biết lý do bạn cần huỷ (tuỳ chọn nhưng giúp staff xử lý nhanh hơn).'}
              {modal?.kind === 'ConfirmToAttend' && 'Xác nhận bạn sẽ đến theo lịch.'}
              {modal?.kind === 'WithdrawAttend' && 'Rút xác nhận tham dự trước đó.'}
            </Text>
            {modal && modal.kind !== 'ConfirmToAttend' && modal.kind !== 'WithdrawAttend' ? (
              <TextInput
                style={styles.input}
                placeholder="Lý do / ghi chú cho staff"
                value={reason}
                onChangeText={setReason}
                multiline
                testID="request-reason"
              />
            ) : null}
            <View style={styles.modalActions}>
              <Pressable
                onPress={() => { setModal(null); setReason(''); }}
                style={({ pressed }) => [styles.modalBtn, pressed && styles.modalBtnPressed]}
                disabled={submitting}
              >
                <Text style={styles.modalBtnText}>Đóng</Text>
              </Pressable>
              <PrimaryButton
                label={submitting ? 'Đang gửi…' : 'Gửi yêu cầu'}
                onPress={() => void submitRequest()}
                loading={submitting}
                disabled={submitting}
                testID="submit-request"
              />
            </View>
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

function Center({ children }: { children: React.ReactNode }) {
  return (
    <SafeAreaView style={styles.center}>
      <View style={{ alignItems: 'center', gap: spacing.sm }}>{children}</View>
    </SafeAreaView>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      <View style={styles.sectionBody}>{children}</View>
    </View>
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

function fmt(iso: string): string {
  try {
    return new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
  } catch { return iso; }
}

function flagLabel(f: 'Pending' | 'Yes' | 'No'): string {
  return f === 'Pending' ? 'Chờ xác nhận' : f === 'Yes' ? 'Có' : 'Không';
}

function kindLabel(k: SupportRequestKind): string {
  switch (k) {
    case 'Change': return 'Đổi lịch';
    case 'Cancel': return 'Huỷ lịch';
    case 'ConfirmToAttend': return 'Xác nhận tham dự';
    case 'WithdrawAttend': return 'Rút xác nhận tham dự';
    case 'General': return 'Hỏi chung';
  }
}

function statusLabel(s: string): string {
  switch (s) {
    case 'Pending': return 'Chờ staff';
    case 'InReview': return 'Đang xem xét';
    case 'Resolved': return 'Đã xử lý';
    case 'Rejected': return 'Bị từ chối';
    case 'Withdrawn': return 'Đã rút';
    default: return s;
  }
}

function actionLabel(a: string): string {
  switch (a) {
    case 'Created': return 'Tạo lịch';
    case 'Rescheduled': return 'Đổi lịch';
    case 'Cancelled': return 'Huỷ lịch';
    case 'Completed': return 'Hoàn tất';
    case 'NoShow': return 'Không đến';
    case 'ConfirmedToAttend': return 'Đổi cờ tham dự';
    case 'RequestSubmitted': return 'Gửi yêu cầu';
    case 'RequestResolved': return 'Staff xử lý yêu cầu';
    case 'RequestRejected': return 'Staff từ chối yêu cầu';
    case 'RequestWithdrawn': return 'Rút yêu cầu';
    case 'ScheduleReduced': return 'Giảm giờ làm';
    default: return a;
  }
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.lg, backgroundColor: colors.background },
  scroll: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl },
  title: { fontSize: typography.heading, fontWeight: '700', color: colors.text, textAlign: 'center' },
  code: { fontSize: typography.heading, fontWeight: '700', color: colors.text },
  subtitle: { color: colors.muted, fontSize: typography.caption + 1, marginBottom: spacing.sm },
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm },
  statusLabel: { color: colors.muted, fontSize: typography.caption + 1 },
  statusPill: { backgroundColor: colors.infoSoft, paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: 8 },
  statusPillText: { color: colors.primary, fontSize: typography.caption, fontWeight: '600' },
  section: { backgroundColor: colors.surface, borderRadius: 12, borderWidth: 1, borderColor: colors.border, padding: spacing.md, gap: spacing.xs },
  sectionTitle: { fontSize: typography.body, fontWeight: '700', color: colors.primary, marginBottom: spacing.xs, textTransform: 'uppercase', letterSpacing: 0.5 },
  sectionBody: { gap: spacing.xs },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  rowLabel: { color: colors.muted, fontSize: typography.caption + 1 },
  rowValue: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '500', maxWidth: '65%', textAlign: 'right' },
  divider: { height: 1, backgroundColor: colors.border, marginVertical: spacing.xs },
  note: { color: colors.muted, fontSize: typography.caption, fontStyle: 'italic' },
  muted: { color: colors.muted, fontSize: typography.caption + 1 },
  retry: { marginTop: spacing.sm, paddingVertical: spacing.sm, paddingHorizontal: spacing.lg, backgroundColor: colors.primary, borderRadius: 8 },
  retryText: { color: colors.onPrimary, fontWeight: '600' },
  historyRow: { paddingVertical: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border },
  historyWhen: { color: colors.muted, fontSize: typography.caption },
  historyAction: { color: colors.text, fontSize: typography.caption + 1, fontWeight: '600' },
  historyActor: { color: colors.muted, fontSize: typography.caption, fontStyle: 'italic' },
  historyDetail: { color: colors.muted, fontSize: typography.caption, fontFamily: Platform.select({ ios: 'Menlo', android: 'monospace', default: 'monospace' }) as string },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: { backgroundColor: colors.surface, borderTopLeftRadius: 16, borderTopRightRadius: 16, padding: spacing.lg, gap: spacing.sm },
  modalTitle: { fontSize: typography.body + 2, fontWeight: '700', color: colors.text },
  input: { minHeight: 80, borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm, color: colors.text, textAlignVertical: 'top' },
  modalActions: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm, gap: spacing.sm },
  modalBtn: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, borderRadius: 8, borderWidth: 1, borderColor: colors.border, minWidth: 80, alignItems: 'center' },
  modalBtnPressed: { backgroundColor: colors.background },
  modalBtnText: { color: colors.text, fontWeight: '600' },
});
