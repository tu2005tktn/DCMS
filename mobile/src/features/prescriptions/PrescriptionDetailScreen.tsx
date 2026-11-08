import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { useSession } from '@/features/auth/session/useSession';
import { medicalRecordsApi, RecordApiError } from '@/features/medical-records/medical-records.api';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import type { PatientProfilePrescription } from './prescriptions.types';

const formatDate = (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });

export default function PrescriptionDetailScreen() {
  const { visitId: prescriptionId } = useLocalSearchParams<{ visitId: string }>();
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

  return <ScrollView contentContainerStyle={styles.container} refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load(token)} />}>
    <Card title={`Đơn thuốc · ${formatDate(item.prescribedOn)}`}>
      <Text style={styles.muted}>Phòng khám: <Text style={styles.strong}>{item.branchName}</Text></Text>
      <Text style={styles.muted}>Bác sĩ: <Text style={styles.strong}>{item.prescriberName}</Text></Text>
      <Text style={styles.muted}>Chẩn đoán: <Text style={styles.strong}>{item.diagnosis}</Text></Text>
      {item.otherDiagnosis ? <Text style={styles.muted}>Chẩn đoán khác: <Text style={styles.strong}>{item.otherDiagnosis}</Text></Text> : null}
    </Card>
    {item.items.map((medicine, index) => <View key={medicine.id} style={styles.medCard}>
      <Text style={styles.medName}>{index + 1}. {medicine.medicineName}{medicine.strength ? ` · ${medicine.strength}` : ''}</Text>
      {medicine.dosageForm ? <Text style={styles.muted}>{medicine.dosageForm}</Text> : null}
      <Text style={styles.quantity}>Số lượng: {medicine.quantity} {medicine.unit}</Text>
      {medicine.instructions ? <Text style={styles.muted}>{medicine.instructions}</Text> : null}
    </View>)}
    {item.advice ? <View style={styles.advice}><Text style={styles.adviceTitle}>Lời dặn của bác sĩ</Text><Text style={styles.adviceText}>{item.advice}</Text></View> : null}
    <Button title="Quay lại danh sách" variant="secondary" onPress={() => router.back()} />
  </ScrollView>;
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 }, strong: { color: palette.text, fontWeight: '600' },
  medCard: { backgroundColor: '#fff', borderRadius: 10, borderWidth: 1, borderColor: palette.border, padding: spacing.md, gap: 6 },
  medName: { fontSize: 16, fontWeight: '700', color: palette.text }, quantity: { fontSize: 14, color: palette.text, fontWeight: '600' },
  advice: { borderWidth: 1, borderColor: '#84CAFF', borderRadius: 10, padding: spacing.md, backgroundColor: '#F5FAFF', gap: 4 },
  adviceTitle: { fontSize: 15, fontWeight: '700', color: '#175CD3' }, adviceText: { fontSize: 15, lineHeight: 22, color: palette.text },
});
