import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { Banner, Button, Field, palette } from '@/features/patient-link/components/LinkUi';
import { familyApi } from './family-members.api';
import { displayDate, genderLabels, notProvided, relationshipLabel, type FamilyMember } from './family-members.types';
import { messageOf, useFamilySession } from './useFamilySession';

// "Người thân" list: docs/specs/MOBILE-FAMILY-MEMBERS.vi.md, step 1.
export default function FamilyMembersScreen() {
  const router = useRouter();
  const { token, handleAuth } = useFamilySession();
  const [search, setSearch] = useState(''), [keyword, setKeyword] = useState('');
  const [items, setItems] = useState<FamilyMember[]>([]), [total, setTotal] = useState(0), [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true), [more, setMore] = useState(false), [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const request = useRef(0);

  useEffect(() => { const timer = setTimeout(() => setKeyword(search.trim()), 300); return () => clearTimeout(timer); }, [search]);

  const load = useCallback(async (nextPage: number, mode: 'replace' | 'append' | 'refresh') => {
    if (!token) return;
    const id = ++request.current;
    if (mode === 'append') setMore(true); else if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setError(null);
    try {
      const result = await familyApi.list(token, keyword, nextPage);
      if (id !== request.current) return;
      setItems(previous => mode === 'append' ? [...previous, ...result.items.filter(x => !previous.some(p => p.id === x.id))] : result.items);
      setTotal(result.total); setPage(nextPage);
    } catch (failure) { if (id === request.current && !handleAuth(failure)) setError(messageOf(failure)); }
    finally { if (id === request.current) { setLoading(false); setMore(false); setRefreshing(false); } }
  }, [token, keyword, handleAuth]);

  // Reload whenever the screen regains focus (after add, edit or delete) and when the search changes.
  useFocusEffect(useCallback(() => { void load(1, 'replace'); }, [load]));

  if (!token) return null;
  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled"
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(1, 'refresh')} />}>
    <Text style={styles.intro}>Lưu thông tin người thân để đặt lịch khám hộ và theo dõi hồ sơ của họ.</Text>
    <Button title="Thêm người thân" onPress={() => router.push('/(account)/family-members/form')} />
    <Field label="Tìm theo họ tên" value={search} onChangeText={setSearch} maxLength={100} placeholder="Nhập họ tên" />
    {error && <Banner tone="error" message={error} action={{ label: 'Thử lại', onPress: () => void load(1, 'replace') }} />}
    {loading ? <View style={styles.center}><ActivityIndicator color={palette.primary} /><Text style={styles.muted}>Đang tải người thân…</Text></View>
      : !error && items.length === 0 ? <Text style={styles.empty}>Chưa có người thân phù hợp. Bạn có thể thêm người thân mới.</Text>
      : items.map(member => <Pressable key={member.id} accessibilityRole="button" accessibilityLabel={`Mở ${member.fullName}`}
        onPress={() => router.push(`/(account)/family-members/${member.id}`)} style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
        <View style={styles.cardHead}>
          <Text style={styles.name}>{member.fullName}</Text>
          <View style={styles.badge}><Text style={styles.badgeText}>{relationshipLabel(member.relationship)}</Text></View>
        </View>
        <Text style={styles.line}>Ngày sinh: {displayDate(member.birthDate)}</Text>
        <Text style={styles.line}>Giới tính: {member.gender ? genderLabels[member.gender] : notProvided} · SĐT: {member.contactPhone || notProvided}</Text>
      </Pressable>)}
    {!loading && items.length < total && <Button title="Xem thêm" variant="secondary" busy={more} onPress={() => void load(page + 1, 'append')} />}
    {!loading && total > 0 && <Text style={styles.muted}>Đang hiện {items.length}/{total} người thân.</Text>}
  </ScrollView>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background, paddingBottom: 40 },
  center: { alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.lg },
  intro: { color: palette.muted, lineHeight: 21 },
  muted: { color: palette.muted },
  empty: { color: palette.muted, textAlign: 'center', paddingVertical: spacing.lg },
  card: { backgroundColor: '#fff', borderRadius: 16, borderWidth: 1, borderColor: palette.border, padding: spacing.md, gap: 4 },
  pressed: { opacity: 0.8 },
  cardHead: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  name: { fontSize: 17, fontWeight: '700', color: palette.text, flexShrink: 1 },
  badge: { borderRadius: 999, backgroundColor: palette.infoSoft, paddingHorizontal: 10, paddingVertical: 3 },
  badgeText: { color: palette.primary, fontWeight: '600', fontSize: 13 },
  line: { color: palette.text },
});
