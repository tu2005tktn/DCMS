/**
 * S12 — StaffRequestDetailScreen.
 *
 * Staff xem chi tiết support request và chạy command TV4:
 *  1. Xác minh requester (verifyRequester) — chuyển Pending → InReview.
 *  2. Đổi/huỷ appointment (staffChange) — lý do bắt buộc.
 *  3. Resolve/Reject support request (staffResolve):
 *     - Resolve chỉ trả ok=true SAU khi staffChange thành công (nếu Change/Cancel).
 *     - Reject thì chỉ cập nhật request, không đổi appointment.
 *     - Withdrawn KHÔNG được resolve (GBR-075).
 *
 * Lỗi mutation → UI hiển thị lỗi thật, KHÔNG thông báo thành công.
 * Version conflict → re-read lại rồi cho staff thao tác tiếp.
 *
 * Tất cả quyền (canResolve, canEdit, branchMatch, ownershipMatch) kiểm tra trước khi gọi command.
 */

import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, Modal, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { PrimaryButton } from '@/components/ui/PrimaryButton';
import { useSession } from '@/features/auth/session/useSession';
import { appointmentMockRepo } from '@/features/appointments/appointment.mock';
import type {
  AppointmentDto,
  ChangeReason,
  StaffPermission,
  SupportRequestDto,
  SupportRequestStatus,
} from '@/features/appointments/appointment.types';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { typography } from '@/theme/typography';

type LoadState =
  | { kind: 'loading' }
  | { kind: 'denied'; reason: string }
  | { kind: 'not_found' }
  | { kind: 'error'; message: string }
  | { kind: 'ready'; request: SupportRequestDto; appointment: AppointmentDto };

const REASONS: ChangeReason[] = ['PatientRequest', 'DentistUnavailable', 'ChairUnavailable', 'ScheduleReduced', 'Other'];

export default function StaffRequestDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { state } = useSession();

  const staffBranchId = 1; // mock
  const isStaff = state.status === 'authenticated';

  const [load, setLoad] = useState<LoadState>({ kind: 'loading' });
  const [modal, setModal] = useState<null | { kind: 'change' | 'cancel' | 'resolve' | 'reject' }>(null);
  const [reason, setReason] = useState<ChangeReason>('PatientRequest');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  const reload = useCallback(async () => {
    if (!id) return;
    if (!isStaff) {
      setLoad({ kind: 'denied', reason: 'Bạn không có quyền xem yêu cầu này.' });
      return;
    }
    try {
      const r = await appointmentMockRepo.getRequest(id);
      if (!r) { setLoad({ kind: 'not_found' }); return; }
      const appt = await appointmentMockRepo.getById(r.appointmentId);
      if (!appt) { setLoad({ kind: 'not_found' }); return; }
      setLoad({ kind: 'ready', request: r, appointment: appt });
    } catch {
      setLoad({ kind: 'error', message: 'Không thể kết nối máy chủ.' });
    }
  }, [id, isStaff]);

  useEffect(() => { void reload(); }, [reload]);

  const permission: StaffPermission | null = useMemo(() => {
    if (load.kind !== 'ready') return null;
    return {
      canResolve: isStaff && load.appointment.branchId === staffBranchId,
      canEdit: isStaff && load.appointment.branchId === staffBranchId,
      branchMatch: load.appointment.branchId === staffBranchId,
      ownershipMatch: isStaff,
    };
  }, [load, isStaff, staffBranchId]);

  const runCommand = useCallback(async (kind: 'verify' | 'change' | 'cancel' | 'resolve' | 'reject') => {
    if (load.kind !== 'ready' || !permission) return;
    setBusy(true);
    try {
      if (kind === 'verify') {
        const res = await appointmentMockRepo.verifyRequester({
          requestKey: `verify-${load.request.id}-${Date.now()}`,
          expectedVersion: load.request.expectedVersion,
          notes: notes.trim() || undefined,
          staffBranchId,
          hasVerifyPermission: permission.canResolve,
        });
        if (!res.ok) { Alert.alert('Lỗi', res.errorMessage ?? 'Không thể xác minh.'); return; }
        await reload();
        return;
      }
      if (kind === 'change' || kind === 'cancel') {
        const newStatus = kind === 'cancel' ? 'Cancelled' : load.appointment.status;
        const res = await appointmentMockRepo.staffChange(load.appointment.id, {
          requestKey: `change-${load.appointment.id}-${Date.now()}`,
          expectedVersion: load.appointment.version,
          reason,
          notes: notes.trim() || undefined,
          staffBranchId,
          hasEditPermission: permission.canEdit,
          newStatus,
        });
        if (!res.ok) { Alert.alert('Lỗi', res.errorMessage ?? 'Mutation thất bại.'); return; }
        await reload();
        return;
      }
      if (kind === 'resolve' || kind === 'reject') {
        const res = await appointmentMockRepo.staffResolve({
          requestKey: `${kind}-${load.request.id}-${Date.now()}`,
          expectedVersion: load.request.expectedVersion,
          decision: kind === 'resolve' ? 'Resolve' : 'Reject',
          notes: notes.trim() || undefined,
          requestId: load.request.id,
          staffBranchId,
          hasResolvePermission: permission.canResolve,
          changeReason: reason,
        });
        if (!res.ok) {
          Alert.alert('Lỗi', res.errorMessage ?? 'Không thể xử lý yêu cầu.');
          return;
        }
        Alert.alert(kind === 'resolve' ? 'Đã xử lý' : 'Đã từ chối', 'Mutation đã commit thành công.');
        await reload();
      }
    } finally {
      setBusy(false);
      setModal(null);
      setNotes('');
    }
  }, [load, permission, reason, notes, staffBranchId, reload]);

  if (load.kind === 'loading') {
    return <Center><ActivityIndicator color={colors.primary} /><Text style={styles.muted}>Đang tải…</Text></Center>;
  }
  if (load.kind === 'not_found') return <Center><Text style={styles.muted}>Không tìm thấy yêu cầu.</Text></Center>;
  if (load.kind === 'denied') return <Center><Text style={styles.title}>Không có quyền</Text><Text style={styles.muted}>{load.reason}</Text></Center>;
  if (load.kind === 'error') return <Center><Text style={styles.title}>Lỗi</Text><Text style={styles.muted}>{load.message}</Text><Pressable onPress={() => void reload()} style={styles.retry}><Text style={styles.retryText}>Tải lại</Text></Pressable></Center>;

  const { request, appointment } = load;
  const withdrawBlock = request.status === 'Withdrawn';
  const closed = request.status === 'Resolved' || request.status === 'Rejected' || withdrawBlock;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <Text style={styles.code}>{request.id}</Text>
        <Text style={styles.subtitle}>Yêu cầu cho lịch {appointment.code} · v{appointment.version}</Text>

        <View style={styles.statusRow}>
          <Text style={styles.statusLabel}>Trạng thái:</Text>
          <View style={[styles.statusPill, pillBg(request.status)]}>
            <Text style={styles.statusPillText}>{statusLabel(request.status)}</Text>
          </View>
        </View>

        <Section title="Quyền của bạn">
          <Row label="Có thể Resolve/Reject" value={permission?.canResolve ? 'Có' : 'Không'} />
          <Row label="Có thể chỉnh sửa lịch" value={permission?.canEdit ? 'Có' : 'Không'} />
          <Row label="Khớp chi nhánh" value={permission?.branchMatch ? 'Có' : 'Không'} />
          <Row label="Khớp quyền sở hữu" value={permission?.ownershipMatch ? 'Có' : 'Không'} />
          {!permission?.branchMatch ? <Text style={styles.warn}>⚠ Yêu cầu không thuộc chi nhánh của bạn — không thể xử lý.</Text> : null}
        </Section>

        <Section title="Thông tin yêu cầu">
          <Row label="Loại" value={kindLabel(request.kind)} />
          <Row label="Lý do" value={request.reason || '—'} />
          {request.proposedChange ? (
            <>
              <Row label="Giờ mới đề xuất" value={request.proposedChange.newStartAt ? fmt(request.proposedChange.newStartAt) : '—'} />
              <Row label="Lý do đề xuất" value={request.proposedChange.reason} />
            </>
          ) : null}
          <Row label="Yêu cầu bởi" value={`${request.requester.name}${request.requester.isAnonymous ? ' (vãng lai)' : ''}`} />
          <Row label="Ngày tạo" value={fmt(request.createdAt)} />
          <Row label="Cập nhật" value={fmt(request.updatedAt)} />
        </Section>

        <Section title="Lịch hẹn liên quan">
          <Row label="Mã lịch" value={appointment.code} />
          <Row label="Trạng thái" value={apptStatusLabel(appointment.status)} />
          <Row label="Bắt đầu" value={fmt(appointment.startAt)} />
          <Row label="Kết thúc" value={fmt(appointment.endAt)} />
          <Row label="Dịch vụ" value={appointment.serviceName || '—'} />
          <Row label="Bác sĩ" value={appointment.dentistName || '—'} />
          <Row label="Ghế" value={appointment.chairId !== null ? `Ghế ${appointment.chairId}` : '—'} />
        </Section>

        <Section title="Lịch sử yêu cầu">
          {request.history.map((h, idx) => (
            <View key={idx} style={styles.historyRow}>
              <Text style={styles.historyWhen}>{fmt(h.at)}</Text>
              <Text style={styles.historyAction}>{actionLabel(h.action)}{h.notes ? ` — ${h.notes}` : ''}</Text>
              <Text style={styles.historyActor}>bởi {h.actor}{h.actorUserId ? ` (#${h.actorUserId})` : ''}</Text>
            </View>
          ))}
        </Section>

        <Section title="Lệnh staff (TV4)">
          {withdrawBlock ? (
            <Text style={styles.muted}>
              ⚠ Yêu cầu đã bị rút bởi requester — KHÔNG thể resolve lại (GBR-075).
            </Text>
          ) : closed ? (
            <Text style={styles.muted}>Yêu cầu đã đóng. Không thể thực hiện thêm lệnh.</Text>
          ) : !permission?.canResolve ? (
            <Text style={styles.muted}>Bạn không có quyền xử lý yêu cầu này.</Text>
          ) : (
            <View style={{ gap: spacing.sm }}>
              {request.status === 'Pending' ? (
                <PrimaryButton label="Xác minh requester" onPress={() => runCommand('verify')} loading={busy} disabled={busy} testID="verify-requester" />
              ) : null}
              {request.kind === 'Change' ? (
                <PrimaryButton label="Đổi lịch theo yêu cầu" onPress={() => setModal({ kind: 'change' })} loading={busy} disabled={busy} testID="staff-change" />
              ) : null}
              {request.kind === 'Cancel' ? (
                <PrimaryButton label="Huỷ lịch theo yêu cầu" onPress={() => setModal({ kind: 'cancel' })} loading={busy} disabled={busy} testID="staff-cancel" />
              ) : null}
              {request.kind === 'ConfirmToAttend' || request.kind === 'WithdrawAttend' ? (
                <Text style={styles.muted}>Loại yêu cầu này chỉ cần Resolve để cập nhật cờ tham dự (không đổi appointment).</Text>
              ) : null}
              <PrimaryButton label="Resolve (chấp nhận)" onPress={() => setModal({ kind: 'resolve' })} loading={busy} disabled={busy} testID="staff-resolve" />
              <PrimaryButton label="Reject (từ chối)" onPress={() => setModal({ kind: 'reject' })} loading={busy} disabled={busy} testID="staff-reject" />
            </View>
          )}
        </Section>

        <View style={{ height: spacing.xl }} />
      </ScrollView>

      <Modal visible={modal !== null} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>
              {modal?.kind === 'change' && 'Đổi lịch'}
              {modal?.kind === 'cancel' && 'Huỷ lịch'}
              {modal?.kind === 'resolve' && 'Resolve yêu cầu'}
              {modal?.kind === 'reject' && 'Reject yêu cầu'}
            </Text>
            {modal && modal.kind !== 'resolve' && modal.kind !== 'reject' ? (
              <View>
                <Text style={styles.modalLabel}>Lý do *</Text>
                <View style={styles.reasonRow}>
                  {REASONS.map((r) => (
                    <Pressable
                      key={r}
                      onPress={() => setReason(r)}
                      style={[styles.reasonChip, reason === r && styles.reasonChipActive]}
                    >
                      <Text style={[styles.reasonChipText, reason === r && styles.reasonChipTextActive]}>{r}</Text>
                    </Pressable>
                  ))}
                </View>
              </View>
            ) : null}
            <Text style={styles.modalLabel}>Ghi chú</Text>
            <TextInput
              style={styles.input}
              placeholder="Ghi chú cho log / audit"
              value={notes}
              onChangeText={setNotes}
              multiline
            />
            <View style={styles.modalActions}>
              <Pressable onPress={() => setModal(null)} style={styles.modalBtn}><Text style={styles.modalBtnText}>Đóng</Text></Pressable>
              <PrimaryButton
                label={busy ? 'Đang chạy…' : 'Xác nhận'}
                onPress={() => {
                  const m = modal;
                  if (!m) return;
                  if (m.kind === 'change') void runCommand('change');
                  else if (m.kind === 'cancel') void runCommand('cancel');
                  else if (m.kind === 'resolve') void runCommand('resolve');
                  else void runCommand('reject');
                }}
                loading={busy}
                disabled={busy}
              />
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
  try { return new Date(iso).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' }); }
  catch { return iso; }
}

function pillBg(s: SupportRequestStatus) {
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

function apptStatusLabel(s: string): string {
  switch (s) {
    case 'Pending': return 'Chờ xác minh';
    case 'Confirmed': return 'Đã xác nhận';
    case 'Rescheduled': return 'Đã đổi lịch';
    case 'Completed': return 'Hoàn tất';
    case 'Cancelled': return 'Đã huỷ';
    case 'NoShow': return 'Không đến';
    default: return s;
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

function actionLabel(a: string): string {
  switch (a) {
    case 'RequestSubmitted': return 'Gửi yêu cầu';
    case 'RequestResolved': return 'Staff xử lý yêu cầu';
    case 'RequestRejected': return 'Staff từ chối yêu cầu';
    case 'RequestWithdrawn': return 'Requester rút yêu cầu';
    case 'InReview': return 'Staff xem xét';
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
  reasonChipTextActive: { color: colors.onPrimary, fontWeight: '600' },
  input: { minHeight: 60, borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm, color: colors.text, textAlignVertical: 'top' },
  modalActions: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm, gap: spacing.sm },
  modalBtn: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, borderRadius: 8, borderWidth: 1, borderColor: colors.border, minWidth: 80, alignItems: 'center' },
  modalBtnText: { color: colors.text, fontWeight: '600' },
});
