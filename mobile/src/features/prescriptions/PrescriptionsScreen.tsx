/**
 * Danh sách đơn thuốc mua ngoài đã hoàn tất của các hồ sơ đã liên kết Verified (docs/specs/PRESCRIPTION-MANAGEMENT.md §4).
 * Mỗi lần mở đều tải lại và kiểm quyền liên kết; 403 → xóa dữ liệu đang hiển thị, 401 → signOut('expired').
 */

import { useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { useSession } from '@/features/auth/session/useSession';
import { medicalRecordsApi, RecordApiError } from '@/features/medical-records/medical-records.api';
import { colors } from '@/theme/colors';
import { spacing } from '@/theme/spacing';
import type { PatientProfilePrescription } from './prescriptions.types';

const formatDate = (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });

export default function PrescriptionsScreen() {
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;

  const [items, setItems] = useState<PatientProfilePrescription[] | null>(null);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [denied, setDenied] = useState(false);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async (currentToken: string) => {
    setLoading(true); setError(null);
    try {
      const [rows] = await Promise.all([medicalRecordsApi.prescriptions(currentToken), patientLinkApi.ownRecord(currentToken)]);
      setItems(rows); setDenied(false);
    } catch (failure) {
      if (failure instanceof RecordApiError && failure.status === 401) { void session.signOut('expired'); return; }
      if ((failure as { status?: number }).status === 403) { setItems(null); setDenied(true); return; }
      setError(failure as RecordApiError);
    } finally { setLoading(false); }
  }, [session.signOut]);

  useEffect(() => { if (token) void load(token); else setItems(null); }, [token, load]);

  if (!token) return null;
  if (loading && !items) {
    return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải đơn thuốc…</Text></View>;
  }
  if (denied) {
    return <View style={styles.container}>
      <Banner tone="info" message="Tài khoản chưa liên kết hồ sơ đã xác minh, hoặc quyền xem đã bị thu hồi. Dữ liệu đơn thuốc không được hiển thị." />
      <Button title="Mở màn liên kết hồ sơ" onPress={() => router.push('/(account)/account-link')} />
    </View>;
  }
  if (error && !items) {
    return <View style={styles.container}>
      <Banner tone="error" message={error.message} />
      <Button title="Thử lại" onPress={() => void load(token)} />
    </View>;
  }

  return (
    <ScrollView contentContainerStyle={styles.container} refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load(token)} />}>
      <Banner tone="info" message="Đơn thuốc mua ngoài tại hiệu thuốc. Phòng khám không bán thuốc tại chỗ." />
      {error && <Banner tone="error" message={error.message} />}
      <Card title="Đơn thuốc của tôi">
        {(items ?? []).length === 0
          ? <Text style={styles.muted}>Chưa có đơn thuốc nào.</Text>
          : (items ?? []).map(item => (
            <Pressable
              key={item.id}
              accessibilityRole="button"
              accessibilityLabel={`Đơn thuốc ngày ${formatDate(item.prescribedOn)} tại ${item.branchName}`}
              style={({ pressed }) => [styles.row, pressed && styles.pressed]}
              onPress={() => router.push(`/(account)/prescriptions/${item.id}`)}
            >
              <View style={{ flex: 1, gap: 2 }}>
                <Text style={styles.rowTitle}>{formatDate(item.prescribedOn)} · {item.branchName}</Text>
                <Text style={styles.muted}>Bác sĩ {item.prescriberName}</Text>
                {item.diagnosis ? <Text style={styles.muted} numberOfLines={2}>Chẩn đoán: {item.diagnosis}</Text> : null}
                <Text style={styles.muted}>{item.items.length} thuốc</Text>
              </View>
              <Text accessibilityElementsHidden style={styles.chevron}>›</Text>
            </Pressable>
          ))}
      </Card>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background, gap: spacing.sm },
  container: { padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xl * 2, backgroundColor: colors.background },
  muted: { color: palette.muted, fontSize: 14, lineHeight: 20 },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, borderTopWidth: 1, borderTopColor: palette.border, paddingVertical: spacing.sm, minHeight: 56 },
  pressed: { opacity: 0.7 },
  rowTitle: { fontSize: 15, fontWeight: '600', color: palette.text },
  pill: { borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4, backgroundColor: '#FFF6ED' },
  pillText: { fontSize: 12, fontWeight: '600', color: '#B54708' },
  chevron: { fontSize: 26, color: palette.muted, paddingLeft: spacing.xs },
});
