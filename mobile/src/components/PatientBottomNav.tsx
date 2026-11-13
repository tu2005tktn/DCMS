import { usePathname, useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useSession } from '@/features/auth/session/useSession';

const destinations = [
  { label: 'Trang chủ', icon: '⌂', href: '/(home)/', match: '/' },
  { label: 'Lịch hẹn', icon: '▦', href: '/(account)/appointments', match: '/appointments' },
  { label: 'Bệnh án', icon: '✚', href: '/(account)/medical-records', match: '/medical-records' },
  { label: 'Tài khoản', icon: '◉', href: '/(account)/home', match: '/home' },
] as const;

export default function PatientBottomNav() {
  const router = useRouter();
  const path = usePathname();
  const { state } = useSession();
  if (state.status !== 'authenticated' || state.user.accountType !== 'Public') return null;
  return <SafeAreaView edges={['bottom']} style={styles.safe}>
    <View style={styles.bar}>
      {destinations.map((destination, index) => {
        const active = index === 0 ? path === '/' : index === 3 ? path === '/home' : path.startsWith(destination.match);
        return <Pressable key={destination.label} accessibilityRole="tab" accessibilityState={{ selected: active }}
          accessibilityLabel={destination.label} onPress={() => router.navigate(destination.href)} style={styles.item}>
          <View style={[styles.iconBox, active && styles.iconBoxActive]}>
            {index === 3 ? <View style={styles.personIcon}>
              <View style={[styles.personHead, active && styles.personActive]} />
              <View style={[styles.personBody, active && styles.personActive]} />
            </View> : <Text style={[styles.icon, active && styles.active]}>{destination.icon}</Text>}
          </View>
          <Text style={[styles.label, active && styles.active]}>{destination.label}</Text>
        </Pressable>;
      })}
    </View>
  </SafeAreaView>;
}

const styles = StyleSheet.create({
  safe: { backgroundColor: '#FFFFFF', borderTopColor: '#E0E7F0', borderTopWidth: 1 },
  bar: { flexDirection: 'row', paddingHorizontal: 8, paddingTop: 6, paddingBottom: 2 },
  item: { flex: 1, alignItems: 'center', justifyContent: 'center', minHeight: 54, gap: 2 },
  iconBox: { width: 38, height: 29, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  iconBoxActive: { backgroundColor: '#E8F0FF' },
  icon: { fontSize: 23, lineHeight: 27, color: '#617187', fontWeight: '700' },
  personIcon: { width: 24, height: 24, alignItems: 'center', justifyContent: 'flex-end', gap: 2 },
  personHead: { width: 8, height: 8, borderRadius: 4, backgroundColor: '#617187' },
  personBody: { width: 17, height: 9, borderTopLeftRadius: 9, borderTopRightRadius: 9, backgroundColor: '#617187' },
  personActive: { backgroundColor: '#175CD3' },
  label: { color: '#617187', fontSize: 11, fontWeight: '600' },
  active: { color: '#175CD3', fontWeight: '800' },
});
