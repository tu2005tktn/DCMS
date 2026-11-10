import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';

const blue = '#175CD3', ink = '#17233D', muted = '#617187';

export default function AccountHomeScreen() {
  const { state, signOut } = useSession();
  const router = useRouter();
  const [signingOut, setSigningOut] = useState(false);
  const user = state.status === 'authenticated' ? state.user : null;
  if (!user) return <View style={styles.center}><ActivityIndicator color={blue} /></View>;
  const initials = user.fullName.split(' ').filter(Boolean).slice(-2).map(part => part[0]).join('').toUpperCase();
  async function handleSignOut() {
    if (signingOut) return;
    setSigningOut(true);
    try { await signOut('user'); } finally { setSigningOut(false); }
  }
  const go = (href: Href) => router.push(href);
  return <SafeAreaView style={styles.safe} edges={['top']}><ScrollView contentContainerStyle={styles.content}>
    <Text style={styles.eyebrow}>TÀI KHOẢN</Text><Text style={styles.title}>Thông tin của tôi</Text>
    <View style={styles.profile}><View style={styles.avatar}><Text style={styles.avatarText}>{initials}</Text></View>
      <View style={styles.profileText}><Text style={styles.name}>{user.fullName}</Text><Text style={styles.username}>@{user.userName}</Text></View></View>
    <Text style={styles.section}>Chăm sóc của tôi</Text>
    {user.accountType === 'Public' ? <>
      <Item title="Lịch hẹn" detail="Xem, đổi và hủy lịch" onPress={() => go('/(account)/appointments')} />
      <Item title="Bệnh án" detail="Theo dõi điều trị và lịch sử khám" onPress={() => go('/(account)/medical-records')} />
      <Item title="Kết quả khám" detail="Xem kết quả theo từng lượt" onPress={() => go('/(account)/visit-results')} />
      <Item title="Đơn thuốc" detail="Thuốc và lời dặn của nha sĩ" onPress={() => go('/(account)/prescriptions')} />
      <Item title="Thông báo" detail="Cập nhật lịch hẹn của bạn" onPress={() => go('/(home)/notifications')} />
      <Item title="Liên kết hồ sơ" detail="Xác minh hồ sơ bệnh nhân" onPress={() => go('/(account)/account-link')} />
      <Item title="Thông tin bệnh nhân" detail="Xem hồ sơ đã liên kết" onPress={() => go('/(account)/my-record')} />
    </> : <Item title="Hộp thư hỗ trợ" detail="Yêu cầu của bệnh nhân" onPress={() => go('/(home)/staff/requests')} />}
    <Text style={styles.section}>Phiên đăng nhập</Text>
    <View style={styles.info}><Text style={styles.infoLabel}>Loại tài khoản</Text><Text style={styles.infoValue}>{user.accountType === 'Public' ? 'Bệnh nhân' : 'Nhân viên'}</Text></View>
    <Pressable onPress={() => void handleSignOut()} disabled={signingOut} style={styles.signOut} accessibilityRole="button">
      <Text style={styles.signOutText}>{signingOut ? 'Đang đăng xuất…' : 'Đăng xuất'}</Text>
    </Pressable>
  </ScrollView></SafeAreaView>;
}
function Item({ title, detail, onPress }: { title: string; detail: string; onPress: () => void }) {
  return <Pressable style={styles.item} onPress={onPress} accessibilityRole="button"><View style={styles.itemText}><Text style={styles.itemTitle}>{title}</Text><Text style={styles.itemDetail}>{detail}</Text></View><Text style={styles.arrow}>›</Text></Pressable>;
}
const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F4F7FB' }, center: { flex: 1, alignItems: 'center', justifyContent: 'center' }, content: { padding: 20, paddingBottom: 42, gap: 11 },
  eyebrow: { color: blue, fontSize: 11, fontWeight: '800', letterSpacing: 1.2 }, title: { color: ink, fontSize: 28, fontWeight: '800', marginBottom: 12 },
  profile: { backgroundColor: '#FFFFFF', borderRadius: 20, padding: 20, flexDirection: 'row', alignItems: 'center', gap: 15, borderWidth: 1, borderColor: '#E0E7F0' }, avatar: { width: 56, height: 56, borderRadius: 28, backgroundColor: '#E8F0FF', alignItems: 'center', justifyContent: 'center' }, avatarText: { color: blue, fontSize: 18, fontWeight: '800' }, profileText: { flex: 1 }, name: { color: ink, fontSize: 18, fontWeight: '800' }, username: { color: muted, fontSize: 13, marginTop: 3 },
  section: { color: ink, fontSize: 17, fontWeight: '800', marginTop: 20, marginBottom: 3 }, item: { backgroundColor: '#FFFFFF', borderRadius: 14, borderWidth: 1, borderColor: '#E0E7F0', paddingHorizontal: 17, minHeight: 72, flexDirection: 'row', alignItems: 'center' }, itemText: { flex: 1, gap: 4 }, itemTitle: { color: ink, fontWeight: '700', fontSize: 16 }, itemDetail: { color: muted, fontSize: 13 }, arrow: { color: blue, fontSize: 27, marginLeft: 10 },
  info: { backgroundColor: '#FFFFFF', padding: 17, borderRadius: 14, flexDirection: 'row', justifyContent: 'space-between' }, infoLabel: { color: muted }, infoValue: { color: ink, fontWeight: '700' }, signOut: { minHeight: 50, borderRadius: 14, borderWidth: 1, borderColor: '#F3B6B2', backgroundColor: '#FFF4F3', alignItems: 'center', justifyContent: 'center', marginTop: 18 }, signOutText: { color: '#B42318', fontWeight: '700', fontSize: 16 },
});
