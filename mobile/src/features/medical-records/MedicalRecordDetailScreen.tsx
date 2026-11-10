/**
 * MedicalRecordDetailScreen — S41 chi tiết một hồ sơ chính thức (read-only).
 *
 * Nguồn: GET /api/patients/me/treatment-summary (projection máy chủ) — tải lại mỗi lần mở,
 * không nhận PatientId từ client. Tách bạch kế hoạch đã chốt với tiến độ thực tế;
 * phần findings/prescription/files/sync chưa có API thì hiển thị trạng thái nguồn trung thực.
 * 401 → signOut('expired'); 403 → xóa dữ liệu nhạy cảm và quay về gate.
 */

import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { useSession } from '@/features/auth/session/useSession';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { medicalRecordsApi, RecordApiError } from './medical-records.api';
import { dossierStateLabels, money, type OwnTreatmentSummaryItem } from './medical-records.types';

export default function MedicalRecordDetailScreen() {
  const { dossierId } = useLocalSearchParams<{ dossierId: string }>();
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;

  const [item, setItem] = useState<OwnTreatmentSummaryItem | null>(null);
  const [missing, setMissing] = useState(false);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [denied, setDenied] = useState(false);
  const [loading, setLoading] = useState(true);
  const [lastSyncAt, setLastSyncAt] = useState<string | null>(null);
  const [showContact, setShowContact] = useState(false);

  const load = useCallback(async (currentToken: string) => {
    setLoading(true); setError(null); setMissing(false);
    try {
      const [summary] = await Promise.all([
        medicalRecordsApi.treatmentSummary(currentToken),
        patientLinkApi.ownRecord(currentToken), // kiểm quyền lại trên mỗi lần mở (403 → xóa dữ liệu)
      ]);
      const found = summary.find(x => x.dossierId === dossierId) ?? null;
      setItem(found);
      setDenied(false);
      if (!found) setMissing(true);
      else setLastSyncAt(new Date().toISOString());
    } catch (failure) {
      if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return; }
      if (failure instanceof RecordApiError && failure.status === 403) {
        setItem(null); setLastSyncAt(null); setDenied(true);
        return;
      }
      setError(failure as RecordApiError);
    } finally { setLoading(false); }
  }, [dossierId, session.signOut]);

  useEffect(() => { if (token) void load(token); }, [token, load]);

  if (!token) return null;
  if (loading && !item) {
    return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} />
      <Text style={styles.muted}>Đang tải hồ sơ…</Text></View>;
  }

  if (denied) {
    return <View style={styles.container}>
      <Banner tone="error" message="Quyền xem hồ sơ đã bị thu hồi — dữ liệu đang hiển thị đã được xóa." />
      <Button title="Quay lại danh sách hồ sơ" onPress={() => router.back()} />
    </View>;
  }

  if (error && !item) {
    return <View style={styles.container}>
      <Banner tone="error" message={error.message} />
      <Button title="Thử lại" onPress={() => token && void load(token)} />
      <Button title="Quay lại" variant="secondary" onPress={() => router.back()} />
    </View>;
  }

  if (missing || !item) {
    return <View style={styles.container}>
      <Banner tone="error" message="Không tìm thấy hồ sơ này trong danh sách hồ sơ chính thức của bạn."
        action={{ label: 'Quay lại', onPress: () => router.back() }} />
    </View>;
  }

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Card title={`Hồ sơ ${item.code}`}>
        <View style={styles.headerRow}>
          <View style={[styles.pill, { backgroundColor: item.state === 'Open' ? '#EFF8FF' : '#F2F4F7' }]}>
            <Text style={[styles.pillText, { color: item.state === 'Open' ? '#175CD3' : '#344054' }]}>
              {dossierStateLabels[item.state]}</Text>
          </View>
          <Text style={styles.muted}>Bắt đầu {item.startedOn}</Text>
        </View>
        <Text style={styles.diagnosis}>{item.diagnosis}</Text>
        {lastSyncAt && <Text style={styles.muted}>Cập nhật: {new Date(lastSyncAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</Text>}
      </Card>

      <Card title="Hạng mục bác sĩ đã làm">
        {(item.performedItems ?? []).length === 0
          ? <Text style={styles.muted}>Chưa có hạng mục điều trị đã ghi nhận.</Text>
          : item.performedItems.map(row => <View key={row.id} style={styles.itemBox}>
            <Text style={styles.itemTitle}>{row.serviceName}{row.region ? ` · ${row.region}` : ''}</Text>
            <Text style={styles.muted}>{new Date(row.performedAt).toLocaleDateString('vi-VN')} · {row.quantity} {row.unit}</Text>
            {row.outcome ? <Text style={styles.muted}>Kết quả: {row.outcome}</Text> : null}
          </View>)}
      </Card>

      <Card title="Thanh toán đã ghi">
        {(item.payments ?? []).length === 0 ? <Text style={styles.muted}>Chưa có khoản thanh toán trên phiếu điều trị.</Text>
          : item.payments.map(payment => <View key={payment.id} style={styles.itemBox}>
            <Text style={styles.itemTitle}>{money(payment.totalVnd)}</Text>
            <Text style={styles.muted}>{payment.paidOn} · {payment.method}</Text>
          </View>)}
      </Card>

      <Card title="Đơn thuốc">
        <Button title="Xem đơn thuốc" variant="secondary" onPress={() => router.push('/(account)/prescriptions')} />
      </Card>

      {showContact
        ? <Banner tone="info"
          message="Ứng dụng chỉ đọc. Hãy liên hệ phòng khám (quầy lễ tân hoặc số điện thoại trên lịch hẹn) để yêu cầu sửa đổi thông tin lâm sàng hoặc tài chính."
          action={{ label: 'Đóng', onPress: () => setShowContact(false) }} />
        : <Button title="Liên hệ phòng khám" variant="secondary" onPress={() => setShowContact(true)} />}
      <Button title="Quay lại danh sách" variant="secondary" onPress={() => router.back()} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 },
  headerRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' },
  diagnosis: { fontSize: 16, fontWeight: '600', color: palette.text },
  itemBox: { borderTopWidth: 1, borderTopColor: palette.border, paddingTop: spacing.sm, gap: 4 },
  itemTitle: { fontSize: 15, fontWeight: '600', color: palette.text },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4 },
  pillText: { fontSize: 12, fontWeight: '600' },
});
