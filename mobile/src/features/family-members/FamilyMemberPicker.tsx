import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { colors } from '@/theme/colors';
import { familyApi } from './family-members.api';
import { displayDate, relationshipLabel, type FamilyMember } from './family-members.types';
import { messageOf } from './useFamilySession';

// Booking for a relative: the attendee is picked from saved family members only (docs/specs/MOBILE-FAMILY-MEMBERS.vi.md).
export default function FamilyMemberPicker({ token, selected, onSelect }:
  { token: string; selected: FamilyMember | null; onSelect: (member: FamilyMember) => void }) {
  const router = useRouter();
  const [search, setSearch] = useState(''), [keyword, setKeyword] = useState('');
  const [items, setItems] = useState<FamilyMember[]>([]), [total, setTotal] = useState(0), [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true), [error, setError] = useState<string | null>(null), [revision, setRevision] = useState(0);
  const request = useRef(0);
  useEffect(() => { const timer = setTimeout(() => { setKeyword(search.trim()); setPage(1); }, 300); return () => clearTimeout(timer); }, [search]);
  useEffect(() => {
    const id = ++request.current;
    setLoading(true); setError(null);
    familyApi.list(token, keyword, page)
      .then(result => { if (id !== request.current) return; setItems(previous => page === 1 ? result.items : [...previous, ...result.items]); setTotal(result.total); })
      .catch(failure => { if (id === request.current) setError(messageOf(failure)); })
      .finally(() => { if (id === request.current) setLoading(false); });
  }, [token, keyword, page, revision]);

  return <View style={styles.box}>
    <Text style={styles.label}>Chọn người thân đi khám</Text>
    <TextInput value={search} onChangeText={setSearch} maxLength={100} placeholder="Tìm theo họ tên" placeholderTextColor={colors.muted}
      style={styles.input} accessibilityLabel="Tìm người thân" />
    {error && <Pressable accessibilityRole="button" onPress={() => setRevision(v => v + 1)}><Text style={styles.error}>{error} Bấm để thử lại.</Text></Pressable>}
    {!loading && !error && items.length === 0 && <>
      <Text style={styles.muted}>{keyword ? 'Không có người thân phù hợp.' : 'Bạn chưa lưu người thân nào. Thêm người thân trước rồi quay lại đặt lịch.'}</Text>
      <Pressable accessibilityRole="button" onPress={() => router.push('/(account)/family-members/form')}><Text style={styles.link}>+ Thêm người thân</Text></Pressable>
    </>}
    {items.map(member => {
      const active = selected?.id === member.id;
      return <Pressable key={member.id} accessibilityRole="radio" accessibilityState={{ checked: active }} aria-checked={active} onPress={() => onSelect(member)}
        style={[styles.option, active && styles.optionActive]}>
        <Text style={[styles.name, active && styles.nameActive]}>{member.fullName}</Text>
        <Text style={styles.muted}>{relationshipLabel(member.relationship)} · Ngày sinh {displayDate(member.birthDate)}</Text>
      </Pressable>;
    })}
    {loading ? <ActivityIndicator color={colors.primary} />
      : items.length < total && <Pressable accessibilityRole="button" onPress={() => setPage(p => p + 1)}><Text style={styles.link}>Xem thêm người thân</Text></Pressable>}
  </View>;
}

const styles = StyleSheet.create({
  box: { gap: 8 },
  label: { color: colors.text, fontWeight: '700' },
  input: { minHeight: 44, borderWidth: 1, borderColor: colors.border, borderRadius: 12, paddingHorizontal: 12, color: colors.text, backgroundColor: colors.surface },
  option: { borderWidth: 1, borderColor: colors.border, borderRadius: 12, padding: 12, backgroundColor: colors.surface, gap: 2 },
  optionActive: { borderColor: colors.primary, backgroundColor: colors.infoSoft },
  name: { color: colors.text, fontWeight: '700' },
  nameActive: { color: colors.primary },
  muted: { color: colors.muted },
  link: { color: colors.primary, fontWeight: '700', paddingVertical: 6 },
  error: { color: colors.danger },
});
