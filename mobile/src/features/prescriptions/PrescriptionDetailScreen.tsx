/**
 * Chi tiết một đơn thuốc mua ngoài đã hoàn tất + màn "bản in" để mang ra hiệu thuốc.
 * Không có nút nhận thuốc tại kho hay thanh toán tiền thuốc (phòng khám không bán thuốc).
 * Nguồn: GET /api/patients/me/prescriptions/visits/{visitId}; 404 khi đơn không thuộc hồ sơ đã liên kết.
 */

import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Image, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { useSession } from '@/features/auth/session/useSession';
import { medicalRecordsApi, RecordApiError } from '@/features/medical-records/medical-records.api';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { doseSlots, formatDate, mealLabel, type PatientPrescriptionDetail, type PrescriptionPrint } from './prescriptions.types';

function PrintView({ print }: { print: PrescriptionPrint }) {
  const at = new Date(print.prescribedAt);
  return (
    <View style={styles.paper} accessibilityLabel="Bản in đơn thuốc">
      <View style={styles.paperHeader}>
        {print.logoDataUrl ? <Image source={{ uri: print.logoDataUrl }} style={styles.logo} resizeMode="contain" accessibilityIgnoresInvertColors /> : null}
        <View style={{ flex: 1 }}>
          <Text style={styles.paperBranch}>{print.branchName}</Text>
          {print.branchAddress ? <Text style={styles.paperText}>{print.branchAddress}</Text> : null}
          {print.branchPhone ? <Text style={styles.paperText}>ĐT: {print.branchPhone}</Text> : null}
        </View>
      </View>
      <Text style={styles.paperTitle}>ĐƠN THUỐC</Text>
      <Text style={[styles.paperText, styles.centerText]}>Đơn thuốc mua ngoài — phòng khám không bán thuốc</Text>
      {print.revision > 1 && <Text style={[styles.paperText, styles.centerText]}>Bản điều chỉnh lần {print.revision - 1}</Text>}
      <View style={styles.paperBlock}>
        <Text style={styles.paperText}>Họ tên: <Text style={styles.bold}>{print.patientName}</Text> · Mã BN: {print.patientCode}</Text>
        <Text style={styles.paperText}>Năm sinh: {print.birthYear ?? '……'} · Giới tính: {print.gender ?? '……'}</Text>
        {print.address ? <Text style={styles.paperText}>Địa chỉ: {print.address}</Text> : null}
        <Text style={styles.paperText}>Chẩn đoán: <Text style={styles.bold}>{print.diagnosis || '……'}</Text></Text>
      </View>
      {print.items.map(item => (
        <View key={item.index} style={styles.paperItem}>
          <Text style={styles.paperText}>
            <Text style={styles.bold}>{item.index}. {item.medicineName}</Text>{item.strength ? ` ${item.strength}` : ''}{item.dosageForm ? ` (${item.dosageForm})` : ''}
          </Text>
          <Text style={styles.paperText}>Số lượng: <Text style={styles.bold}>{item.quantity} {item.unit}</Text> ({item.quantityInWords} {item.unit})</Text>
          <Text style={styles.paperText}>{item.dosageInstruction}</Text>
          {item.instructions ? <Text style={[styles.paperText, styles.italic]}>{item.instructions}</Text> : null}
        </View>
      ))}
      <Text style={styles.paperText}><Text style={styles.bold}>Lời dặn: </Text>{print.generalAdvice || 'Dùng thuốc đúng chỉ dẫn.'}</Text>
      <View style={styles.paperSign}>
        <Text style={styles.paperText}>Ngày {at.getDate()} tháng {at.getMonth() + 1} năm {at.getFullYear()}</Text>
        <Text style={styles.bold}>BÁC SĨ KÊ ĐƠN</Text>
        <Text style={styles.paperText}>{print.doctorName}</Text>
      </View>
      {print.verificationCode && (
        <View style={styles.codeBox}>
          <Text style={styles.paperText}>Mã xác thực đơn</Text>
          <Text style={styles.code} selectable>{print.verificationCode}</Text>
        </View>
      )}
    </View>
  );
}

export default function PrescriptionDetailScreen() {
  const { visitId } = useLocalSearchParams<{ visitId: string }>();
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;

  const [detail, setDetail] = useState<PatientPrescriptionDetail | null>(null);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [missing, setMissing] = useState(false);
  const [denied, setDenied] = useState(false);
  const [loading, setLoading] = useState(true);
  const [showPrint, setShowPrint] = useState(false);

  const load = useCallback(async (currentToken: string) => {
    setLoading(true); setError(null); setMissing(false);
    try {
      const [value] = await Promise.all([medicalRecordsApi.prescription(currentToken, visitId), patientLinkApi.ownRecord(currentToken)]);
      setDetail(value); setDenied(false);
    } catch (failure) {
      if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return; }
      const status = (failure as { status?: number }).status;
      if (status === 403) { setDetail(null); setDenied(true); return; }
      if (status === 404) { setDetail(null); setMissing(true); return; }
      setError(failure as RecordApiError);
    } finally { setLoading(false); }
  }, [visitId, session.signOut]);

  useEffect(() => { if (token) void load(token); else setDetail(null); }, [token, load]);

  if (!token) return null;
  if (loading && !detail) {
    return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải đơn thuốc…</Text></View>;
  }
  if (denied) {
    return <View style={styles.container}>
      <Banner tone="error" message="Quyền xem hồ sơ đã bị thu hồi — dữ liệu đơn thuốc đã được xóa khỏi màn hình." />
      <Button title="Quay lại" onPress={() => router.back()} />
    </View>;
  }
  if (missing) {
    return <View style={styles.container}>
      <Banner tone="error" message="Không tìm thấy đơn thuốc này trong các hồ sơ đã liên kết của bạn." action={{ label: 'Quay lại', onPress: () => router.back() }} />
    </View>;
  }
  if (error && !detail) {
    return <View style={styles.container}>
      <Banner tone="error" message={error.message} />
      <Button title="Thử lại" onPress={() => void load(token)} />
      <Button title="Quay lại" variant="secondary" onPress={() => router.back()} />
    </View>;
  }
  if (!detail) return null;

  const rx = detail.prescription;
  if (showPrint) {
    return (
      <ScrollView contentContainerStyle={styles.container}>
        <PrintView print={detail.print} />
        <Text style={styles.note}>Đưa màn hình này cho hiệu thuốc hoặc chụp màn hình để lưu lại. Mã xác thực giúp phòng khám đối chiếu đơn gốc.</Text>
        <Button title="Quay lại chi tiết đơn" variant="secondary" onPress={() => setShowPrint(false)} />
      </ScrollView>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <Card title={`Đơn thuốc ngày ${formatDate(rx.prescribedAt)}`}>
        <Text style={styles.muted}>Chi nhánh: <Text style={styles.strong}>{rx.branchName}</Text></Text>
        <Text style={styles.muted}>Bác sĩ kê đơn: <Text style={styles.strong}>{rx.doctorName}</Text></Text>
        {rx.diagnosis ? <Text style={styles.muted}>Chẩn đoán: <Text style={styles.strong}>{rx.diagnosis}</Text></Text> : null}
        {rx.isCorrection && <Text style={styles.note}>Đây là bản điều chỉnh mới nhất của đơn thuốc; bản cũ không còn hiệu lực.</Text>}
      </Card>

      <View style={styles.notice}><Text style={styles.noticeText}>{rx.externalPurchaseNotice}</Text></View>

      {rx.medicines.map((m, index) => (
        <View key={`${index}-${m.medicineName}`} style={styles.medCard}>
          <Text style={styles.medName}>{index + 1}. {m.medicineName}</Text>
          <Text style={styles.muted}>{[m.strength, m.dosageForm, m.route].filter(Boolean).join(' · ')}</Text>
          <Text style={styles.qty}>Số lượng: {m.quantity} {m.unit} · Dùng {m.days} ngày</Text>
          <View style={styles.slots}>
            {doseSlots(m).map(slot => {
              const on = slot.value !== '0';
              return (
                <View key={slot.key} style={[styles.slot, on ? styles.slotOn : styles.slotOff]} accessibilityLabel={`${slot.label}: ${on ? slot.value : 'không dùng'}`}>
                  <Text style={[styles.slotLabel, on && styles.slotLabelOn]}>{slot.label}</Text>
                  <Text style={[styles.slotValue, on && styles.slotLabelOn]}>{on ? slot.value : '—'}</Text>
                </View>
              );
            })}
          </View>
          <Text style={styles.meal}>{mealLabel(m.mealTiming)}</Text>
          {m.instructions ? <Text style={styles.muted}>{m.instructions}</Text> : null}
        </View>
      ))}

      {rx.generalAdvice ? (
        <View style={styles.advice}>
          <Text style={styles.adviceTitle}>Lời dặn của bác sĩ</Text>
          <Text style={styles.adviceText}>{rx.generalAdvice}</Text>
        </View>
      ) : null}

      <Button title="Xem bản in đơn thuốc" onPress={() => setShowPrint(true)} />
      <Button title="Quay lại danh sách" variant="secondary" onPress={() => router.back()} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 },
  strong: { color: palette.text, fontWeight: '600' },
  note: { color: palette.muted, fontSize: 12, lineHeight: 18, fontStyle: 'italic' },
  notice: { backgroundColor: '#FFFAEB', borderColor: '#FEC84B', borderWidth: 1, borderRadius: 8, padding: spacing.sm },
  noticeText: { color: '#93370D', fontSize: 14, lineHeight: 20, fontWeight: '600' },
  medCard: { backgroundColor: '#FFFFFF', borderRadius: 10, borderWidth: 1, borderColor: palette.border, padding: spacing.md, gap: 6 },
  medName: { fontSize: 16, fontWeight: '700', color: palette.text },
  qty: { fontSize: 14, color: palette.text, fontWeight: '600' },
  slots: { flexDirection: 'row', gap: 6 },
  slot: { flex: 1, borderRadius: 8, paddingVertical: 6, alignItems: 'center', borderWidth: 1 },
  slotOn: { backgroundColor: '#EFF8FF', borderColor: '#84CAFF' },
  slotOff: { backgroundColor: '#F9FAFB', borderColor: palette.border },
  slotLabel: { fontSize: 12, color: palette.muted },
  slotValue: { fontSize: 14, color: palette.muted, fontWeight: '600' },
  slotLabelOn: { color: '#175CD3' },
  meal: { alignSelf: 'flex-start', backgroundColor: '#F2F4F7', borderRadius: 6, paddingHorizontal: 8, paddingVertical: 3, fontSize: 12, color: palette.text },
  advice: { borderWidth: 2, borderColor: '#2E90FA', borderRadius: 10, padding: spacing.md, backgroundColor: '#F5FAFF', gap: 4 },
  adviceTitle: { fontSize: 15, fontWeight: '700', color: '#175CD3' },
  adviceText: { fontSize: 15, lineHeight: 22, color: palette.text },
  paper: { backgroundColor: '#FFFFFF', borderRadius: 4, borderWidth: 1, borderColor: '#D0D5DD', padding: spacing.md, gap: 8 },
  paperHeader: { flexDirection: 'row', gap: spacing.sm, borderBottomWidth: 2, borderBottomColor: '#101828', paddingBottom: 8 },
  logo: { width: 48, height: 48 },
  paperBranch: { fontSize: 15, fontWeight: '700', color: '#101828', textTransform: 'uppercase' },
  paperTitle: { fontSize: 20, fontWeight: '700', textAlign: 'center', color: '#101828', marginTop: 4 },
  paperText: { fontSize: 13, lineHeight: 19, color: '#101828' },
  paperBlock: { gap: 2 },
  paperItem: { borderTopWidth: 1, borderTopColor: '#EAECF0', paddingTop: 6, gap: 2 },
  paperSign: { alignItems: 'flex-end', gap: 2, marginTop: 8 },
  centerText: { textAlign: 'center' },
  bold: { fontWeight: '700' },
  italic: { fontStyle: 'italic' },
  codeBox: { alignItems: 'center', borderWidth: 1, borderStyle: 'dashed', borderColor: '#98A2B3', borderRadius: 6, padding: 8, marginTop: 4 },
  code: { fontSize: 18, letterSpacing: 2, fontWeight: '700', color: '#101828', fontFamily: 'monospace' },
});
