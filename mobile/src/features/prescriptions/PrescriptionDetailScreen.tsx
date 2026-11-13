import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { useSession } from '@/features/auth/session/useSession';
import { medicalRecordsApi, RecordApiError } from '@/features/medical-records/medical-records.api';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import { genderLabel, type PatientProfilePrescription } from './prescriptions.types';

const formatDate = (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });

export default function PrescriptionDetailScreen() {
  const { id: prescriptionId } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [item, setItem] = useState<PatientProfilePrescription | null>(null);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [denied, setDenied] = useState(false);
  const [missing, setMissing] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async (currentToken: string) => {
    setLoading(true); setError(null); setMissing(false);
    try {
      const [value] = await Promise.all([medicalRecordsApi.prescription(currentToken, prescriptionId), patientLinkApi.ownRecord(currentToken)]);
      setItem(value); setDenied(false);
    } catch (failure) {
      if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return; }
      const status = (failure as { status?: number }).status;
      if (status === 403) { setItem(null); setDenied(true); return; }
      if (status === 404) { setItem(null); setMissing(true); return; }
      setError(failure as RecordApiError);
    } finally { setLoading(false); }
  }, [prescriptionId, session.signOut]);

  useEffect(() => { if (token) void load(token); else setItem(null); }, [token, load]);
  if (!token) return null;
  if (loading && !item) return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải đơn thuốc…</Text></View>;
  if (denied) return <View style={styles.container}><Banner tone="error" message="Quyền xem hồ sơ đã bị thu hồi. Đơn thuốc không được hiển thị." /><Button title="Quay lại" onPress={() => router.back()} /></View>;
  if (missing) return <View style={styles.container}><Banner tone="error" message="Không tìm thấy đơn thuốc trong hồ sơ đã liên kết của bạn." /><Button title="Quay lại" onPress={() => router.back()} /></View>;
  if (error && !item) return <View style={styles.container}><Banner tone="error" message={error.message} /><Button title="Thử lại" onPress={() => void load(token)} /><Button title="Quay lại" variant="secondary" onPress={() => router.back()} /></View>;
  if (!item) return null;

  // Bố cục tờ đơn như web (docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md): phòng khám, bệnh nhân, chỉ định, lời dặn, ngày & bác sĩ.
  return <ScrollView contentContainerStyle={styles.container} refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load(token)} />}>
    <View style={styles.sheet}>
      <View style={styles.head}>
        <Text style={styles.title} accessibilityRole="header">ĐƠN THUỐC</Text>
        <Text style={styles.clinic}>{item.branchName}</Text>
        {item.branchPhone ? <Text style={styles.contact}>☎ {item.branchPhone}</Text> : null}
        {item.branchEmail ? <Text style={styles.contact}>✉ {item.branchEmail}</Text> : null}
        {item.branchAddress ? <Text style={styles.contact}>{item.branchAddress}</Text> : null}
      </View>

      <Text style={styles.section}>BỆNH NHÂN</Text>
      <Row label="Họ và tên" value={item.patientName} />
      <Row label="Mã BN" value={item.patientCode} />
      <Row label="Ngày sinh" value={item.patientBirthDate ? formatDate(item.patientBirthDate) : '—'} />
      <Row label="Giới tính" value={genderLabel(item.patientGender)} />
      <Row label="Chẩn đoán" value={item.diagnosis || '—'} />
      {item.otherDiagnosis ? <Row label="Chẩn đoán khác" value={item.otherDiagnosis} /> : null}

      <Text style={styles.section}>CHỈ ĐỊNH</Text>
      {item.items.map((medicine, index) => <View key={medicine.id} style={styles.medCard}>
        <View style={styles.medTop}>
          <Text style={styles.medName}>{index + 1}. {medicine.medicineName}{medicine.strength ? ` · ${medicine.strength}` : ''}</Text>
          <Text style={styles.quantity}>{medicine.quantity} {medicine.unit}</Text>
        </View>
        {medicine.dosageForm ? <Text style={styles.muted}>{medicine.dosageForm}</Text> : null}
        {medicine.instructions ? <Text style={styles.usage}>{medicine.instructions}</Text> : null}
      </View>)}

      <Text style={styles.section}>GHI CHÚ/LỜI DẶN</Text>
      <View style={styles.advice}><Text style={styles.adviceText}>{item.advice || '—'}</Text></View>

      <Text style={styles.section}>NGÀY &amp; BÁC SĨ KÊ ĐƠN</Text>
      <Row label="Ngày" value={formatDate(item.prescribedOn)} />
      <Row label="Bác sĩ" value={item.prescriberName} />
    </View>
    <Banner tone="info" message="Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ." />
    <Button title="Quay lại danh sách" variant="secondary" onPress={() => router.back()} />
  </ScrollView>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.label}>{label}</Text><Text style={styles.value}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  sheet: { backgroundColor: '#fff', borderRadius: 12, borderWidth: 1, borderColor: palette.border, padding: spacing.md, gap: 6 },
  head: { borderBottomWidth: 3, borderBottomColor: '#E4E8EC', paddingBottom: spacing.sm, gap: 2 },
  title: { fontSize: 22, fontWeight: '600', color: '#2F3B45', letterSpacing: 0.5 },
  clinic: { fontSize: 15, fontWeight: '700', color: palette.text },
  contact: { fontSize: 13, color: palette.muted },
  section: { marginTop: spacing.md, paddingVertical: 6, paddingHorizontal: 8, backgroundColor: '#F5F6F7', borderBottomWidth: 2, borderBottomColor: '#E4E8EC',
    fontSize: 12, fontWeight: '700', color: palette.text, letterSpacing: 0.4 },
  row: { flexDirection: 'row', gap: spacing.sm, paddingVertical: 4 },
  label: { width: 110, fontSize: 14, color: palette.muted },
  value: { flex: 1, fontSize: 14, fontWeight: '600', color: palette.text },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 },
  medCard: { borderRadius: 10, borderWidth: 1, borderColor: palette.border, padding: spacing.sm, gap: 4, backgroundColor: '#FAFBFC' },
  medTop: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm },
  medName: { flex: 1, fontSize: 15, fontWeight: '700', color: palette.text },
  quantity: { fontSize: 15, color: palette.text, fontWeight: '700' },
  usage: { fontSize: 14, lineHeight: 20, color: palette.text },
  advice: { borderWidth: 1, borderColor: '#84CAFF', borderRadius: 10, padding: spacing.sm, backgroundColor: '#F5FAFF' },
  adviceText: { fontSize: 15, lineHeight: 22, color: palette.text },
});
