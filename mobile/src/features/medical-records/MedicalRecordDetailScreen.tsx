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
import {
  decisionLabels, dossierStateLabels, money, progressLabel,
  type OwnTreatmentSummaryItem, type TreatmentItem,
} from './medical-records.types';

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

  const agreed = item.agreedItems.filter(x => x.decision === 'Agreed');
  const warrantyItems = agreed.filter(x => x.warrantyConfirmedAt);

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Card title={`Hồ sơ ${item.code}`}>
        <View style={styles.headerRow}>
          <View style={[styles.pill, { backgroundColor: item.state === 'Open' ? '#EFF8FF' : '#F2F4F7' }]}>
            <Text style={[styles.pillText, { color: item.state === 'Open' ? '#175CD3' : '#344054' }]}>
              {dossierStateLabels[item.state]}</Text>
          </View>
          <Text style={styles.muted}>Kế hoạch v{item.planVersion} · Bắt đầu {item.startedOn}</Text>
        </View>
        <Text style={styles.diagnosis}>{item.diagnosis}</Text>
        <Text style={styles.note}>
          “Đang điều trị” nghĩa là đợt còn mở — hoàn thành một lượt khám không làm hồ sơ tự chuyển sang hoàn tất.
        </Text>
        {lastSyncAt && <Text style={styles.muted}>Lần tải thành công gần nhất: {new Date(lastSyncAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</Text>}
      </Card>

      <Card title="Kế hoạch đã chốt (plan)">
        <Text style={styles.muted}>
          Các hạng mục dưới đây là phần đã thỏa thuận trong kế hoạch; “Tiến độ thực tế” do phòng khám ghi nhận,
          tách riêng với dự kiến.
        </Text>
        {agreed.length === 0
          ? <Text style={styles.muted}>Hồ sơ chưa có hạng mục nào ở trạng thái đã đồng ý.</Text>
          : agreed.map((row: TreatmentItem) => (
            <View key={row.id} style={styles.itemBox}>
              <Text style={styles.itemTitle}>{row.serviceName}{row.region ? ` · ${row.region}` : ''}</Text>
              <Text style={styles.muted}>
                {row.quantity} {row.unit} · {money(row.agreedUnitPriceVnd)}
                {row.estimatedMinutes ? ` · dự kiến ${row.estimatedMinutes} phút` : ''}
              </Text>
              <View style={styles.tagRow}>
                <View style={styles.tag}><Text style={styles.tagText}>Quyết định: {decisionLabels[row.decision]}</Text></View>
                <View style={styles.tag}><Text style={styles.tagText}>Tiến độ thực tế: {progressLabel(row.progress)}</Text></View>
              </View>
              {row.counseling && <Text style={styles.muted}>Tư vấn: {row.counseling}</Text>}
            </View>
          ))}
      </Card>

      <Card title="Bảo hành">
        {warrantyItems.length === 0
          ? <Text style={styles.muted}>
            Chưa có bảo hành nào được xác nhận cho hồ sơ này. Bảo hành chỉ hiển thị khi phòng khám xác nhận qua API.
          </Text>
          : warrantyItems.map(row => (
            <View key={row.id} style={styles.itemBox}>
              <Text style={styles.itemTitle}>{row.serviceName}{row.region ? ` · ${row.region}` : ''}</Text>
              <Text style={styles.muted}>
                {row.warrantyPolicy} {row.warrantyPolicyVersion ? `· ${row.warrantyPolicyVersion}` : ''}
                {row.warrantyEndsOn ? ` · đến ${row.warrantyEndsOn}` : ''}
              </Text>
              {row.warrantyScope && <Text style={styles.muted}>{row.warrantyScope}</Text>}
            </View>
          ))}
      </Card>

      <Card title="Nguồn chưa kết nối">
        <Text style={styles.muted}>
          • Kết quả/diagnosis/chẩn đoán chính thức (DCMS-93/96/99){'\n'}
          • Đơn thuốc & dặn dò đã duyệt (DCMS-103){'\n'}
          • Ảnh/tài liệu ở trạng thái Ready — chỉ tải qua endpoint được cấp quyền (S20 / DCMS-95){'\n'}
          • Lịch sử phiên bản/sửa đổi/đồng bộ chi tiết (DCMS-95){'\n'}
          • Hóa đơn thanh toán (S42 — màn đã có thì chỉ hiển thị link)
        </Text>
        <Text style={styles.note}>
          Ứng dụng không hiển thị ghi chú nội bộ của nhân viên, phí nhà cung cấp hay dữ liệu người thân;
          không có quy trình “bấm xuất bản” — nguồn chính thức tự hiển thị theo UC-55.
        </Text>
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
  note: { color: palette.muted, fontSize: 12, lineHeight: 18, fontStyle: 'italic' },
  headerRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' },
  diagnosis: { fontSize: 16, fontWeight: '600', color: palette.text },
  itemBox: { borderTopWidth: 1, borderTopColor: palette.border, paddingTop: spacing.sm, gap: 4 },
  itemTitle: { fontSize: 15, fontWeight: '600', color: palette.text },
  tagRow: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  tag: { backgroundColor: '#F2F4F7', borderRadius: 6, paddingHorizontal: 8, paddingVertical: 3 },
  tagText: { fontSize: 12, color: palette.text },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4 },
  pillText: { fontSize: 12, fontWeight: '600' },
});
