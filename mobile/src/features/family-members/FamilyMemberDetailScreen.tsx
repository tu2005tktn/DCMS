import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, StyleSheet, Text, View } from 'react-native';
import { uuid } from 'expo-modules-core';
import { spacing } from '@/theme/spacing';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { familyApi, keyFor } from './family-members.api';
import { displayDate, genderLabels, notProvided, relationshipLabel, type FamilyMember } from './family-members.types';
import FamilyRecordPanel from './FamilyRecordPanel';
import { messageOf, useFamilySession } from './useFamilySession';

// One family member: info, booking shortcut, edit/delete (step 1) and delegated appointments/record (step 2).
// docs/specs/MOBILE-FAMILY-MEMBERS.vi.md
export default function FamilyMemberDetailScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { token, handleAuth } = useFamilySession();
  const [member, setMember] = useState<FamilyMember | null>(null);
  const [loading, setLoading] = useState(true), [error, setError] = useState<string | null>(null), [missing, setMissing] = useState(false);
  const [confirming, setConfirming] = useState(false), [deleting, setDeleting] = useState(false), [deleteError, setDeleteError] = useState<string | null>(null);
  const pending = useRef<{ text: string; key: string } | null>(null);

  const load = useCallback(async () => {
    if (!token || !id) return;
    setLoading(true); setError(null);
    try { setMember(await familyApi.get(token, id)); setMissing(false); }
    catch (failure) {
      if (handleAuth(failure)) return;
      const status = (failure as { status?: number }).status;
      if (status === 404) { setMember(null); setMissing(true); } else setError(messageOf(failure));
    } finally { setLoading(false); }
  }, [token, id, handleAuth]);
  // Coming back from the edit form shows the saved values.
  useFocusEffect(useCallback(() => { void load(); }, [load]));

  async function remove() {
    if (!token || !member || deleting) return;
    setDeleting(true); setDeleteError(null);
    try {
      await familyApi.remove(token, member.id, member.version, keyFor(pending, { id: member.id, version: member.version }, () => uuid.v4()));
      pending.current = null;
      router.replace('/(account)/family-members');
    } catch (failure) {
      if (handleAuth(failure)) return;
      setDeleteError(messageOf(failure));
      if ((failure as { status?: number }).status === 409) { setConfirming(false); void load(); }
    } finally { setDeleting(false); }
  }

  if (!token) return null;
  if (loading && !member) return <View style={styles.center}><ActivityIndicator color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>;
  if (!member) return <ScrollView contentContainerStyle={styles.container}>
    <Banner tone="error" message={missing ? 'Không tìm thấy người thân trong danh sách của bạn.' : error ?? 'Không thể tải người thân.'}
      action={missing ? undefined : { label: 'Thử lại', onPress: () => void load() }} />
    <Button title="Về danh sách người thân" variant="secondary" onPress={() => router.replace('/(account)/family-members')} />
  </ScrollView>;

  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
    <View>
      <Text style={styles.name}>{member.fullName}</Text>
      <Text style={styles.relation}>{relationshipLabel(member.relationship)}</Text>
    </View>
    {error && <Banner tone="error" message={error} action={{ label: 'Thử lại', onPress: () => void load() }} />}
    <Card title="Thông tin">
      <Row label="Ngày sinh" value={displayDate(member.birthDate)} />
      <Row label="Giới tính" value={member.gender ? genderLabels[member.gender] : notProvided} />
      <Row label="Số điện thoại" value={member.contactPhone || notProvided} />
    </Card>
    <Button title="Đặt lịch khám cho người thân" onPress={() => router.push(`/book-appointment?familyMemberId=${member.id}`)} />
    <Button title="Sửa thông tin" variant="secondary" onPress={() => router.push(`/(account)/family-members/form?id=${member.id}`)} />
    {deleteError && <Banner tone="error" message={deleteError} />}
    {!confirming ? <Button title="Xóa khỏi danh sách" variant="danger" onPress={() => { setDeleteError(null); setConfirming(true); }} />
      : <Card>
        <Text style={styles.confirm}>Bạn có muốn xóa người thân này khỏi danh sách không?</Text>
        <Text style={styles.muted}>Lịch hẹn và hồ sơ tại phòng khám không bị xóa. Quyền xem hồ sơ của người thân (nếu có) sẽ chấm dứt.</Text>
        <Button title="Đồng ý" variant="danger" busy={deleting} onPress={() => void remove()} />
        <Button title="Không đồng ý" variant="secondary" disabled={deleting} onPress={() => setConfirming(false)} />
      </Card>}
    <FamilyRecordPanel key={`${member.id}:${member.version}`} memberId={member.id} />
  </ScrollView>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background, paddingBottom: 48 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  muted: { color: palette.muted },
  name: { fontSize: 22, fontWeight: '700', color: palette.text },
  relation: { fontSize: 15, fontWeight: '600', color: palette.primary },
  confirm: { fontWeight: '700', color: palette.text, fontSize: 16 },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { color: palette.muted },
  rowValue: { color: palette.text, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
});
