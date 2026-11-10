import { useFocusEffect, useRouter, type Href } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';
import { patientLinkApi } from '@/features/patient-link/patient-link.api';
import { medicalRecordsApi, RecordApiError } from '@/features/medical-records/medical-records.api';
import { type OwnTreatmentSummaryItem, type VisitCard } from '@/features/medical-records/medical-records.types';
import { notificationRepo } from '@/features/appointments/notification.repo';
import type { NotificationDto } from '@/features/appointments/notification.types';
import { formatClock, formatDate } from '@/features/appointments/appointments.rules';
import { ApiError } from '@/services/api/api-error';

const blue = '#175CD3', ink = '#17233D', muted = '#617187', bg = '#F4F7FB';
type Load<T> = { status: 'loading' | 'ready' | 'error'; value: T | null };
const initial = <T,>(): Load<T> => ({ status: 'loading', value: null });

export default function HomeScreen() {
  const router = useRouter();
  const { state, signOut } = useSession();
  const user = state.status === 'authenticated' ? state.user : null;
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [link, setLink] = useState<Load<string>>(initial);
  const [records, setRecords] = useState<Load<OwnTreatmentSummaryItem[]>>(initial);
  const [visits, setVisits] = useState<Load<VisitCard[]>>(initial);
  const [notifications, setNotifications] = useState<Load<NotificationDto[]>>(initial);
  const [refreshing, setRefreshing] = useState(false);
  useEffect(() => {
    setLink(initial()); setRecords(initial()); setVisits(initial()); setNotifications(initial());
  }, [user?.id]);
  const load = useCallback(async () => {
    if (!token || !user || user.accountType !== 'Public') return;
    setRefreshing(true);
    const linkTask = patientLinkApi.overview(token).then(async overview => {
      setLink({ status: 'ready', value: overview.linkStatus });
      if (overview.linkStatus !== 'VERIFIED') { setRecords({ status: 'ready', value: null }); return; }
      try { setRecords({ status: 'ready', value: await medicalRecordsApi.treatmentSummary(token) }); }
      catch (error) {
        if (error instanceof ApiError && error.status === 401) { void signOut('expired'); return; }
        if (error instanceof RecordApiError && error.status === 403) {
          setLink({ status: 'ready', value: 'REVOKED' }); setRecords({ status: 'ready', value: null });
        } else setRecords({ status: 'error', value: null });
      }
    }).catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 401) { void signOut('expired'); return; }
      setLink({ status: 'error', value: null }); setRecords({ status: 'error', value: null });
    });
    const visitTask = medicalRecordsApi.visits(token, 'UPCOMING')
      .then(page => setVisits({ status: 'ready', value: page.items }))
      .catch((error: unknown) => { if (error instanceof ApiError && error.status === 401) void signOut('expired'); else setVisits({ status: 'error', value: null }); });
    const noticeTask = notificationRepo.list(user.id, { limit: 3 }, token)
      .then(page => setNotifications({ status: 'ready', value: page.items }))
      .catch(() => setNotifications({ status: 'error', value: null }));
    await Promise.allSettled([linkTask, visitTask, noticeTask]);
    setRefreshing(false);
  }, [token, user?.id, user?.accountType, signOut]);
  useFocusEffect(useCallback(() => { void load(); }, [load]));
  const go = (href: Href) => router.push(href);

  if (state.status === 'initializing') return <View style={styles.center}><ActivityIndicator color={blue} /></View>;
  if (!user) return <SafeAreaView style={styles.safe} edges={['top']}><ScrollView contentContainerStyle={styles.content}>
    <Text style={styles.brand}>DCMS</Text><Text style={styles.heroTitle}>Chăm sóc nụ cười của bạn</Text>
    <Action label="Đăng nhập" onPress={() => go('/(auth)/sign-in')} primary />
    <Action label="Tạo tài khoản" onPress={() => go('/(auth)/register')} />
  </ScrollView></SafeAreaView>;
  if (user.accountType !== 'Public') return <SafeAreaView style={styles.safe} edges={['top']}><ScrollView contentContainerStyle={styles.content}>
    <Text style={styles.eyebrow}>DCMS · NHÂN VIÊN</Text><Text style={styles.title}>Xin chào, {user.fullName}</Text>
    <Section title="Công việc của bạn" />
    <Action label="Hộp thư hỗ trợ" onPress={() => go('/(home)/staff/requests')} />
    <Action label="Lịch bị ảnh hưởng" onPress={() => go('/(home)/staff/schedule-impact')} />
    <Action label="Tài khoản" onPress={() => go('/(account)/home')} />
  </ScrollView></SafeAreaView>;

  const active = records.value?.filter(item => item.state === 'Open') ?? [];
  const nextVisit = visits.value?.[0];
  const unread = notifications.value?.filter(item => !item.readAt) ?? [];
  return <SafeAreaView style={styles.safe} edges={['top']}>
    <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load()} tintColor={blue} />} contentContainerStyle={styles.content}>
      <View style={styles.topRow}><Text style={styles.brand}>DCMS</Text><Pressable onPress={() => go('/(home)/notifications')} accessibilityRole="button" style={styles.bell}><Text style={styles.bellText}>Thông báo{unread.length ? ` · ${unread.length}` : ''}</Text></Pressable></View>
      <Text style={styles.eyebrow}>HÀNH TRÌNH SỨC KHỎE</Text><Text style={styles.title}>Xin chào, {user.fullName}</Text>
      <Pressable onPress={() => go('/book-appointment')} style={styles.primaryCard} accessibilityRole="button">
        <Text style={styles.primaryTitle}>Đặt lịch khám →</Text>
      </Pressable>
      <Section title="Bệnh án hiện tại" action="Xem tất cả" onPress={() => go('/(account)/medical-records')} />
      {link.status === 'loading' ? <State text="Đang kiểm tra hồ sơ…" /> : link.status === 'error' ? <State text="Chưa tải được trạng thái hồ sơ." retry={load} /> : link.value !== 'VERIFIED' ?
        <Action label={link.value === 'PENDING' ? 'Đang chờ xác minh' : 'Liên kết hồ sơ bệnh nhân'} onPress={() => go('/(account)/account-link')} /> :
        records.status === 'loading' ? <State text="Đang tải bệnh án…" /> : records.status === 'error' ? <State text="Chưa tải được bệnh án." retry={load} /> : active.length === 0 ? <State text="Chưa có hồ sơ đang điều trị. Bạn có thể xem hồ sơ trước đây." /> :
        active.slice(0, 2).map(item => <Pressable key={item.dossierId} style={styles.card} onPress={() => go({ pathname: '/(account)/medical-records/[dossierId]', params: { dossierId: item.dossierId } })} accessibilityRole="button">
          <View style={styles.cardRow}><Text style={styles.cardTitle}>{item.code}</Text><Text style={styles.pill}>Đang điều trị</Text></View>
          <Text style={styles.cardDetail}>Bắt đầu {formatDate(item.startedOn)} · {item.agreedItems.length} mục điều trị</Text><Text style={styles.cardLink}>Xem tiến độ  →</Text>
        </Pressable>)}
      <Section title="Lịch hẹn sắp tới" action="Xem lịch" onPress={() => go('/(account)/appointments')} />
      {visits.status === 'loading' ? <State text="Đang tải lịch hẹn…" /> : visits.status === 'error' ? <State text="Chưa tải được lịch hẹn." retry={load} /> : !nextVisit ? <State text="Bạn chưa có lịch hẹn sắp tới." /> :
        <Action label={`${formatClock(nextVisit.start)} · ${formatDate(nextVisit.start)}`} detail={`Với ${nextVisit.dentistName} · ${nextVisit.status}`} onPress={() => go('/(account)/appointments')} />}
      <Section title="Thông báo mới" action="Xem tất cả" onPress={() => go('/(home)/notifications')} />
      {notifications.status === 'loading' ? <State text="Đang tải thông báo…" /> : notifications.status === 'error' ? <State text="Chưa tải được thông báo." retry={load} /> : unread.length === 0 ? <State text="Bạn chưa có thông báo mới." /> :
        unread.slice(0, 2).map(item => <Action key={item.id} label={item.title} detail={item.body} onPress={() => go('/(home)/notifications')} />)}
      <Section title="Khám phá" />
      <Action label="Kết quả khám" onPress={() => go('/(account)/visit-results')} />
      <Action label="Đơn thuốc của tôi" onPress={() => go('/(account)/prescriptions')} />
      <Action label="Tài khoản của tôi" onPress={() => go('/(account)/home')} />
    </ScrollView>
  </SafeAreaView>;
}
function Section({ title, action, onPress }: { title: string; action?: string; onPress?: () => void }) { return <View style={styles.section}><Text style={styles.sectionTitle}>{title}</Text>{action && <Pressable onPress={onPress} accessibilityRole="button"><Text style={styles.sectionAction}>{action} →</Text></Pressable>}</View>; }
function Action({ label, detail, onPress, primary = false }: { label: string; detail?: string; onPress: () => void; primary?: boolean }) { return <Pressable onPress={onPress} style={[styles.card, primary && styles.actionPrimary]} accessibilityRole="button"><Text style={styles.cardTitle}>{label}</Text>{detail ? <Text style={styles.cardDetail}>{detail}</Text> : null}<Text style={styles.cardLink}>Mở  →</Text></Pressable>; }
function State({ text, retry }: { text: string; retry?: () => void }) { return <View style={styles.state}><Text style={styles.stateText}>{text}</Text>{retry && <Pressable onPress={retry} accessibilityRole="button"><Text style={styles.sectionAction}>Thử lại →</Text></Pressable>}</View>; }
const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: bg }, center: { flex: 1, justifyContent: 'center', alignItems: 'center' }, content: { padding: 20, paddingBottom: 38, gap: 12 },
  topRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }, brand: { fontSize: 22, fontWeight: '800', color: blue }, bell: { minHeight: 44, paddingHorizontal: 14, justifyContent: 'center', borderRadius: 22, backgroundColor: '#E8F0FF' }, bellText: { color: blue, fontWeight: '700' },
  eyebrow: { color: blue, fontSize: 11, fontWeight: '800', letterSpacing: 1.3 }, title: { color: ink, fontSize: 28, fontWeight: '800', lineHeight: 36 }, heroTitle: { color: ink, fontSize: 32, fontWeight: '800', marginTop: 50 }, heroDescription: { color: muted, fontSize: 17, lineHeight: 25, marginBottom: 28 }, subtitle: { color: muted, fontSize: 15, marginBottom: 8 },
  primaryCard: { backgroundColor: blue, padding: 22, borderRadius: 20, gap: 7, marginTop: 5, minHeight: 140, justifyContent: 'center' }, primaryOverline: { color: '#C5DAFF', fontSize: 11, fontWeight: '800', letterSpacing: 1 }, primaryTitle: { color: '#FFFFFF', fontSize: 23, fontWeight: '800' }, primaryDetail: { color: '#F0F5FF', fontSize: 15 },
  section: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 18, marginBottom: 2 }, sectionTitle: { color: ink, fontSize: 18, fontWeight: '800' }, sectionAction: { color: blue, fontSize: 13, fontWeight: '700' },
  card: { backgroundColor: '#FFFFFF', borderColor: '#E0E7F0', borderWidth: 1, borderRadius: 16, padding: 17, gap: 7, minHeight: 88 }, cardRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 }, cardTitle: { color: ink, fontSize: 16, fontWeight: '700', flexShrink: 1 }, cardDetail: { color: muted, fontSize: 14, lineHeight: 21 }, cardLink: { color: blue, fontSize: 13, fontWeight: '700', marginTop: 2 }, actionPrimary: { backgroundColor: '#EAF2FF' }, pill: { color: '#087443', backgroundColor: '#EAF8F0', paddingHorizontal: 9, paddingVertical: 5, borderRadius: 12, fontSize: 11, fontWeight: '700' },
  state: { backgroundColor: '#FFFFFF', borderWidth: 1, borderColor: '#E0E7F0', borderRadius: 16, padding: 18, gap: 10 }, stateText: { color: muted, fontSize: 14, lineHeight: 21 }, nav: { flexDirection: 'row', backgroundColor: '#FFFFFF', borderTopWidth: 1, borderTopColor: '#E0E7F0', paddingVertical: 8, paddingHorizontal: 8 }, navItem: { flex: 1, minHeight: 48, justifyContent: 'center', alignItems: 'center' }, navText: { color: muted, fontSize: 12, fontWeight: '600' }, navActive: { color: blue, fontWeight: '800' },
});
