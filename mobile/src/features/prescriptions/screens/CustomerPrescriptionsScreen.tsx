import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSession } from '@/features/auth/session/useSession';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { ApiError } from '@/services/api/api-error';
import { customerPrescriptionsApi, type PrescriptionDetail, type PrescriptionSummary } from '../customer-prescriptions.api';

const date = (value: string) => { const [y, m, d] = value.split('-'); return `${d}/${m}/${y}`; };

export function CustomerPrescriptionsScreen() {
  const router = useRouter(); const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [items, setItems] = useState<PrescriptionSummary[]>([]); const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0); const [loading, setLoading] = useState(true);
  const [more, setMore] = useState(false); const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<Error | null>(null); const request = useRef(0);
  const load = useCallback(async (next: number, mode: 'replace' | 'more' | 'refresh') => {
    if (!token) return; const id = ++request.current;
    if (mode === 'more') setMore(true); else if (mode === 'refresh') setRefreshing(true); else { setLoading(true); setItems([]); setTotal(0); }
    setError(null);
    try {
      const result = await customerPrescriptionsApi.list(token, next);
      if (id !== request.current) return;
      setItems(old => mode === 'more' ? [...old, ...result.items.filter(x => !old.some(y => y.id === x.id))] : result.items);
      setTotal(result.total); setPage(next);
    } catch (failure) {
      if (id !== request.current) return;
      if (failure instanceof ApiError && failure.status === 401) { void session.signOut('expired'); return; }
      setItems([]); setError(failure as Error);
    } finally { if (id === request.current) { setLoading(false); setMore(false); setRefreshing(false); } }
  }, [token, session.signOut]);
  useFocusEffect(useCallback(() => { void load(1, 'replace'); return () => { request.current++; }; }, [load]));
  if (!token) return null;
  return <ScrollView contentContainerStyle={styles.container} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(1, 'refresh')} />}>
    <Text style={styles.title}>Đơn thuốc của tôi</Text><Text style={styles.muted}>Đơn thuốc trong hồ sơ đã xác minh của bạn.</Text>
    {error instanceof ApiError && error.status === 403 ? <Card><Text>Hãy xác minh liên kết hồ sơ để xem đơn thuốc.</Text>
      <Button title="Liên kết hồ sơ" onPress={() => router.push('/(account)/account-link')} /></Card>
      : error ? <Banner tone="error" message={error.message} action={{ label: 'Thử lại', onPress: () => void load(1, 'replace') }} />
        : loading ? <ActivityIndicator color={palette.primary} /> : items.length === 0 ? <Text style={styles.muted}>Bạn chưa có đơn thuốc nào.</Text>
          : <>{items.map(item => <Pressable key={item.id} accessibilityRole="button" onPress={() => router.push({ pathname: '/(account)/prescriptions/[id]', params: { id: item.id } })}>
            <Card><Text style={styles.cardTitle}>{item.diagnosis}</Text><Text style={styles.muted}>{date(item.prescribedOn)} · {item.branchName}</Text>
              <Text>Nha sĩ: {item.prescriberName}</Text>{item.requiresExternalPurchase && <Text style={styles.external}>Có thuốc mua ngoài</Text>}
              <Text style={styles.link}>Xem chi tiết →</Text></Card></Pressable>)}
            {items.length < total && <Button title="Tải thêm" busy={more} onPress={() => void load(page + 1, 'more')} />}</>}
  </ScrollView>;
}

export function CustomerPrescriptionDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>(); const router = useRouter(); const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [data, setData] = useState<PrescriptionDetail | null>(null); const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(true); const [revision, setRevision] = useState(0);
  useFocusEffect(useCallback(() => {
    if (!token || !id) return; let active = true; setLoading(true); setError(null); setData(null);
    void customerPrescriptionsApi.detail(token, id).then(value => { if (active) setData(value); }).catch(failure => {
      if (!active) return; if (failure instanceof ApiError && failure.status === 401) { void session.signOut('expired'); return; } setError(failure as Error);
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [token, id, revision, session.signOut]));
  if (!token) return null;
  return <ScrollView contentContainerStyle={styles.container}><Text style={styles.title}>Chi tiết đơn thuốc</Text>
    {error instanceof ApiError && error.status === 403 ? <Banner tone="info" message="Hãy xác minh liên kết hồ sơ để xem đơn thuốc."
      action={{ label: 'Liên kết hồ sơ', onPress: () => router.push('/(account)/account-link') }} />
      : error ? <Banner tone="error" message={error.message} action={{ label: 'Thử lại', onPress: () => setRevision(x => x + 1) }} />
        : loading ? <ActivityIndicator color={palette.primary} /> : data && <>
          <Card><Text>Ngày kê: {date(data.prescribedOn)} · {data.branchName}</Text><Text>Nha sĩ: {data.prescriberName}</Text>
            <Text>Chẩn đoán: {data.diagnosis}</Text>{data.otherDiagnosis && <Text>Chẩn đoán khác: {data.otherDiagnosis}</Text>}</Card>
          <Text style={styles.section}>Thuốc được kê</Text>{data.items.map((item, index) => <Card key={index}>
            <Text style={styles.cardTitle}>{index + 1}. {item.medicineName}</Text>
            <Text style={item.isExternalPurchase ? styles.external : styles.muted}>{item.isExternalPurchase ? 'Mua ngoài' : 'Thuốc phòng khám'}</Text>
            <Text>{[item.strength, item.dosageForm].filter(Boolean).join(' · ')} · Số lượng: {item.quantity} {item.unit}</Text>
            <Text>Hướng dẫn: {item.instructions || 'Theo hướng dẫn của nha sĩ'}</Text></Card>)}
          {data.advice && <Card title="Lời dặn"><Text>{data.advice}</Text></Card>}
        </>}
  </ScrollView>;
}

const styles = StyleSheet.create({ container: { padding: 20, gap: 14, backgroundColor: '#fff', flexGrow: 1 },
  title: { color: palette.text, fontSize: 24, fontWeight: '700' }, section: { color: palette.text, fontSize: 18, fontWeight: '700' },
  cardTitle: { color: palette.text, fontSize: 16, fontWeight: '700' }, muted: { color: palette.muted },
  external: { color: palette.primary, fontWeight: '600' }, link: { color: palette.primary, fontWeight: '600' } });
