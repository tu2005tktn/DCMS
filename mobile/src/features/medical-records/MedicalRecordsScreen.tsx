/**
 * MedicalRecordsScreen — S41 My Medical Records (docs/specs/S41-MOBILE-MEDICAL-RECORDS.md).
 *
 * Gate: session S01 + self-link S07 (chỉ VERIFIED xem dữ liệu). Mọi trạng thái unlinked/pending/
 * rejected/revoked/withdrawn hiển thị hướng dẫn điều hướng tới màn liên kết hiện có.
 * Dữ liệu: /api/patient-links/me/record (identity), /api/patients/me/treatment-summary (nguồn chính thức),
 * /api/me/appointments (lượt khám). 401 → signOut('expired'); 403 → xóa dữ liệu nhạy cảm và kiểm tra lại link.
 */

import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { formatTime, Banner, Button, Card, StatusBadge, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import type { LinkStatus, OwnRecordSummary, SelfLinkOverview } from '@/features/patient-link/patient-link.types';
import { formatClock, formatDate, statusColors, statusLabels } from '@/features/appointments/appointments.rules';
import { useSession } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { medicalRecordsApi, RecordApiError } from './medical-records.api';
import { dossierStateLabels, type OwnTreatmentSummaryItem, type VisitCard, type VisitScope } from './medical-records.types';

const linkMessages: Record<Exclude<LinkStatus, 'VERIFIED'>, string> = {
  NOT_LINKED: 'Bạn chưa liên kết hồ sơ bệnh nhân. Gửi yêu cầu liên kết để xem hồ sơ y tế chính thức của mình.',
  PENDING: 'Yêu cầu liên kết đang chờ phòng khám xác minh. Hồ sơ sẽ hiển thị sau khi liên kết được xác minh.',
  REJECTED: 'Yêu cầu liên kết chưa được chấp thuận. Bạn có thể gửi lại yêu cầu tại màn liên kết hồ sơ.',
  REVOKED: 'Liên kết hồ sơ đã bị thu hồi. Gửi yêu cầu liên kết mới để tiếp tục xem hồ sơ.',
  WITHDRAWN: 'Bạn đã rút yêu cầu liên kết. Gửi yêu cầu mới tại màn liên kết hồ sơ.',
};

const visitScopeLabels: Record<VisitScope, string> = { HISTORY: 'Lịch sử khám', UPCOMING: 'Lịch sắp tới' };

export default function MedicalRecordsScreen() {
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;

  const [overview, setOverview] = useState<SelfLinkOverview | null>(null);
  const [gateError, setGateError] = useState<RecordApiError | null>(null);
  const [record, setRecord] = useState<OwnRecordSummary | null>(null);
  const [summary, setSummary] = useState<OwnTreatmentSummaryItem[] | null>(null);
  const [visits, setVisits] = useState<VisitCard[] | null>(null);
  const [visitsScope, setVisitsScope] = useState<VisitScope>('HISTORY');
  const [visitsError, setVisitsError] = useState<RecordApiError | null>(null);
  const [loadError, setLoadError] = useState<RecordApiError | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [lastSyncAt, setLastSyncAt] = useState<string | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);
  const [denied, setDenied] = useState(false);
  const [showContact, setShowContact] = useState(false);
  const loadOrder = useRef(0);
  const hasLoadedOnce = useRef(false);

  const clearSensitive = useCallback(() => {
    setRecord(null); setSummary(null); setVisits(null); setLastSyncAt(null); hasLoadedOnce.current = false;
  }, []);

  // 401 → dừng phiên; 403 → dừng request, xóa dữ liệu nhạy cảm, không dùng cache cũ.
  const handleAuthFailure = useCallback((failure: unknown): boolean => {
    if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return true; }
    if (failure instanceof RecordApiError && failure.status === 403) {
      clearSensitive(); setDenied(true); setLoadError(null); setVisitsError(null); setSyncError(null);
      return true;
    }
    return false;
  }, [clearSensitive, session.signOut]);

  const loadVisits = useCallback(async (scope: VisitScope, currentToken: string) => {
    const id = loadOrder.current;
    setVisitsError(null);
    try {
      const page = await medicalRecordsApi.visits(currentToken, scope);
      if (id !== loadOrder.current) return;
      setVisits(page.items);
    } catch (failure) {
      if (id !== loadOrder.current || handleAuthFailure(failure)) return;
      setVisitsError(failure as RecordApiError);
    }
  }, [handleAuthFailure]);

  const loadRecords = useCallback(async (currentToken: string, mode: 'initial' | 'refresh') => {
    const id = ++loadOrder.current;
    if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setLoadError(null);
    try {
      const [own, treatment] = await Promise.all([
        patientLinkApi.ownRecord(currentToken),
        medicalRecordsApi.treatmentSummary(currentToken),
      ]);
      if (id !== loadOrder.current) return;
      setRecord(own); setSummary(treatment); setDenied(false);
      hasLoadedOnce.current = true;
      setLastSyncAt(new Date().toISOString()); setSyncError(null);
    } catch (failure) {
      if (id !== loadOrder.current || handleAuthFailure(failure)) return;
      // Lỗi đồng bộ: giữ dữ liệu cũ (nếu có) và ghi rõ đây là bản tải gần nhất, không xoá thành empty.
      setLoadError(failure as RecordApiError);
      if (hasLoadedOnce.current) setSyncError(failure instanceof RecordApiError ? failure.message : null);
    } finally {
      if (id === loadOrder.current) { setLoading(false); setRefreshing(false); }
    }
  }, [handleAuthFailure]);

  const loadGate = useCallback(async (currentToken: string) => {
    setLoading(true);
    setGateError(null);
    try {
      const next = await patientLinkApi.overview(currentToken);
      setOverview(next);
      if (next.linkStatus === 'VERIFIED') await loadRecords(currentToken, 'initial');
      else clearSensitive();
    } catch (failure) {
      if (handleAuthFailure(failure)) return;
      setGateError(failure as RecordApiError);
    } finally { setLoading(false); }
  }, [clearSensitive, handleAuthFailure, loadRecords]);

  useEffect(() => { if (token) void loadGate(token); }, [token, loadGate]);
  useEffect(() => { if (token && overview?.linkStatus === 'VERIFIED') void loadVisits(visitsScope, token); },
    [token, overview?.linkStatus, visitsScope, loadVisits]);

  function refresh() {
    if (!token) return;
    if (overview?.linkStatus === 'VERIFIED') {
      void loadRecords(token, 'refresh').then(() => loadVisits(visitsScope, token));
    } else void loadGate(token);
  }

  if (!token) return null;
  if (loading && !overview) {
    return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} />
      <Text style={styles.muted}>Đang tải hồ sơ…</Text></View>;
  }

  if (gateError) {
    return <View style={styles.container}>
      <Banner tone="error" message={gateError.message} />
      <Button title="Thử lại" onPress={() => void loadGate(token)} />
    </View>;
  }

  const status = overview?.linkStatus ?? 'NOT_LINKED';
  if (status !== 'VERIFIED') {
    return <ScrollView contentContainerStyle={styles.container}>
      <Card title="Hồ sơ y tế của tôi">
        <View style={styles.badgeRow}><StatusBadge status={status} /></View>
        <Banner tone={status === 'PENDING' ? 'info' : 'error'}
          message={denied ? 'Quyền xem hồ sơ vừa bị thu hồi — dữ liệu đang hiển thị đã được xóa.' : linkMessages[status]} />
        <Text style={styles.muted}>
          Màn hình này chỉ đọc; việc liên kết và xác minh được thực hiện tại màn Liên kết hồ sơ.
        </Text>
        <Button title="Mở màn liên kết hồ sơ" onPress={() => router.push('/(account)/account-link')} />
      </Card>
    </ScrollView>;
  }

  return (
    <ScrollView contentContainerStyle={styles.container} refreshControl={
      <RefreshControl refreshing={refreshing} onRefresh={refresh} tintColor={palette.primary} />}>
      <Text style={styles.heading}>Hồ sơ y tế chính thức</Text>
      <Text style={styles.muted}>Chỉ đọc — dữ liệu do phòng khám phát hành; ứng dụng không chỉnh sửa nguồn lâm sàng hay tài chính.</Text>

      {denied && <View style={styles.block}>
        <Banner tone="error" message="Quyền xem hồ sơ vừa bị thu hồi — dữ liệu đang hiển thị đã được xóa."
          action={{ label: 'Kiểm tra lại liên kết', onPress: () => { setDenied(false); if (token) void loadGate(token); } }} />
      </View>}
      {loadError && !syncError && <View style={styles.block}>
        <Banner tone="error" message={loadError.message}
          action={{ label: 'Thử lại', onPress: () => { if (token) void loadRecords(token, 'refresh'); } }} />
      </View>}
      {syncError && lastSyncAt && <View style={styles.block}>
        <Banner tone="error" message={`Đồng bộ gặp lỗi — đang hiển thị bản tải gần nhất (${formatTime(lastSyncAt)}).`}
          action={{ label: 'Thử lại', onPress: () => { if (token) void loadRecords(token, 'refresh'); } }} />
      </View>}

      <Card title="Thông tin nhận diện">
        {record ? <View>
          <Row label="Mã bệnh nhân" value={record.patientCode} />
          <Row label="Họ tên" value={record.fullName} />
          <Row label="Ngày sinh" value={record.birthDate ?? 'Chưa rõ'} />
          <Row label="Ngày liên kết" value={formatTime(record.linkedAt)} />
        </View> : <Text style={styles.muted}>Đang tải thông tin nhận diện…</Text>}
      </Card>

      <Card title="Lịch sử sức khỏe & dị ứng">
        <Text style={styles.muted}>
          Chưa có nguồn dữ liệu sức khỏe/dị ứng từ máy chủ (DCMS-95 — Tư). Mục này không hiển thị dữ liệu suy diễn;
          sẽ tự cập nhật khi API cung cấp.
        </Text>
      </Card>

      <Card title="Các lượt khám">
        <View style={styles.chipRow}>
          {(Object.keys(visitScopeLabels) as VisitScope[]).map(scope => (
            <Pressable key={scope} accessibilityRole="button" accessibilityState={{ selected: visitsScope === scope }}
              onPress={() => setVisitsScope(scope)}
              style={[styles.chip, visitsScope === scope && styles.chipSelected]}>
              <Text style={[styles.chipText, visitsScope === scope && styles.chipTextSelected]}>
                {visitScopeLabels[scope]}</Text>
            </Pressable>
          ))}
        </View>
        {visitsError
          ? <Banner tone="error" message={visitsError.message}
            action={{ label: 'Thử lại', onPress: () => token && void loadVisits(visitsScope, token) }} />
          : visits === null ? <Text style={styles.muted}>Đang tải lượt khám…</Text>
            : visits.length === 0 ? <Text style={styles.muted}>Chưa có lượt khám nào trong nhóm này.</Text>
              : visits.map(visit => {
                const [color, background] = statusColors[visit.status as keyof typeof statusColors] ?? ['#344054', '#F2F4F7'];
                return (
                  <Pressable key={visit.code} accessibilityRole="button"
                    onPress={() => router.push(`/(account)/appointments/${visit.code}`)}
                    style={styles.visitRow}>
                    <View style={styles.visitMain}>
                      <Text style={styles.visitDate}>{formatDate(visit.start)} · {formatClock(visit.start)}</Text>
                      <Text style={styles.muted}>{visit.dentistName} · {visit.durationMinutes} phút · nhấn để xem chi nhánh</Text>
                    </View>
                    <View style={[styles.pill, { backgroundColor: background }]}>
                      <Text style={[styles.pillText, { color }]}>{statusLabels[visit.status as keyof typeof statusLabels] ?? visit.status}</Text>
                    </View>
                  </Pressable>
                );
              })}
        <Text style={styles.note}>
          Lượt khám hoàn thành khác với đợt điều trị hoàn tất: trạng thái từng đợt hiển thị ở danh sách hồ sơ bên dưới.
        </Text>
      </Card>

      <Card title="Hồ sơ chính thức của tôi">
        {summary === null ? <Text style={styles.muted}>Đang tải hồ sơ…</Text>
          : summary.length === 0 ? <Text style={styles.muted}>Chưa có hồ sơ chính thức nào (kế hoạch điều trị đã chốt).</Text>
            : summary.map(item => (
              <Pressable key={item.dossierId} accessibilityRole="button"
                onPress={() => router.push(`/(account)/medical-records/${item.dossierId}`)}
                style={styles.recordRow}>
                <View style={styles.recordMain}>
                  <Text style={styles.recordCode}>{item.code}</Text>
                  <Text style={styles.muted}>{item.diagnosis}</Text>
                  <Text style={styles.muted}>Bắt đầu {item.startedOn} · Kế hoạch v{item.planVersion} · {item.agreedItems.length} hạng mục đã đồng ý</Text>
                </View>
                <View style={[styles.pill, { backgroundColor: item.state === 'Open' ? '#EFF8FF' : '#F2F4F7' }]}>
                  <Text style={[styles.pillText, { color: item.state === 'Open' ? '#175CD3' : '#344054' }]}>
                    {dossierStateLabels[item.state]}</Text>
                </View>
              </Pressable>
            ))}
        <Text style={styles.note}>
          “Đang điều trị” nghĩa là đợt còn mở; nguồn chính thức tự hiển thị theo UC-55, không cần bấm xuất bản.
        </Text>
      </Card>

      <Card title="Nguồn chưa kết nối">
        <Text style={styles.muted}>
          • Kết quả/chẩn đoán lâm sàng (DCMS-93/96/99){'\n'}
          • Đơn thuốc & dặn dò đã duyệt (DCMS-103){'\n'}
          • Ảnh/tài liệu ở trạng thái Ready (S20 / DCMS-95){'\n'}
          • Lịch sử phiên bản, đồng bộ chi tiết và hóa đơn (DCMS-95 / S42)
        </Text>
        <Text style={styles.note}>Phần này hiển thị trạng thái trung thực; ứng dụng không tự tạo dữ liệu khi API chưa có.</Text>
      </Card>

      {showContact
        ? <View style={styles.block}>
          <Banner tone="info"
            message="Ứng dụng chỉ đọc. Hãy liên hệ phòng khám (quầy lễ tân hoặc số điện thoại trên biên nhận/lịch hẹn) để yêu cầu sửa đổi thông tin lâm sàng hoặc tài chính — không chỉnh sửa trực tiếp từ app."
            action={{ label: 'Đóng', onPress: () => setShowContact(false) }} />
        </View>
        : <Button title="Liên hệ phòng khám" variant="secondary" onPress={() => setShowContact(true)} />}

      <Text style={styles.syncText}>
        {lastSyncAt ? `Lần tải thành công gần nhất: ${formatTime(lastSyncAt)}` : 'Chưa tải thành công lần nào trong phiên này.'}
      </Text>
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}>
    <Text style={styles.rowLabel}>{label}</Text>
    <Text style={styles.rowValue}>{value}</Text>
  </View>;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  block: { gap: spacing.sm },
  heading: { fontSize: 20, fontWeight: '700', color: palette.text },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 },
  note: { color: palette.muted, fontSize: 12, lineHeight: 18, fontStyle: 'italic' },
  badgeRow: { flexDirection: 'row' },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm, paddingVertical: 4 },
  rowLabel: { color: palette.muted, fontSize: 14 },
  rowValue: { color: palette.text, fontSize: 14, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  chipRow: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.sm },
  chip: { borderWidth: 1, borderColor: palette.border, borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6, backgroundColor: '#fff' },
  chipSelected: { backgroundColor: palette.primary, borderColor: palette.primary },
  chipText: { fontSize: 13, color: palette.text },
  chipTextSelected: { color: '#fff', fontWeight: '600' },
  visitRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm,
    paddingVertical: spacing.sm, borderTopWidth: 1, borderTopColor: palette.border },
  visitMain: { gap: 2, flexShrink: 1 },
  visitDate: { fontSize: 15, fontWeight: '600', color: palette.text },
  recordRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm,
    paddingVertical: spacing.sm, borderTopWidth: 1, borderTopColor: palette.border },
  recordMain: { gap: 2, flexShrink: 1 },
  recordCode: { fontSize: 15, fontWeight: '700', color: palette.text },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4 },
  pillText: { fontSize: 12, fontWeight: '600' },
  syncText: { fontSize: 12, color: palette.muted, textAlign: 'center' },
});
