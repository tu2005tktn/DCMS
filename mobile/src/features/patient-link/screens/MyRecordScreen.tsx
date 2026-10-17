import { useRouter } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { useSession } from '@/features/auth/session/useSession';
import { Banner, Button, Card, Field, formatTime, palette } from '../components/LinkUi';
import { LinkApiError, patientLinkApi } from '../patient-link.api';
import { formatBirthDateInput, recordToForm, validateRecordForm, type RecordForm, type RecordFormErrors } from '../patient-link.schema';
import type { Gender, OwnPatientRecord } from '../patient-link.types';

const genderLabels: Record<Gender, string> = { MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' };
const unknown = 'Chưa rõ';

function Row({ label, value }: { label: string; value: string | null }) {
  return <View style={styles.row}>
    <Text style={styles.rowLabel}>{label}</Text>
    <Text style={[styles.rowValue, !value && styles.rowEmpty]}>{value || '—'}</Text>
  </View>;
}

// "Hồ sơ của tôi" (docs/implementation/UC08-09-38-S05-S06.md §10): own record through a VERIFIED self link.
// Patient code is read-only; the clinic's internal note is never shown.
export default function MyRecordScreen() {
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [record, setRecord] = useState<OwnPatientRecord | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<LinkApiError | null>(null);
  const [form, setForm] = useState<RecordForm | null>(null);
  const [errors, setErrors] = useState<RecordFormErrors>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const handleAuthFailure = useCallback((failure: unknown) => {
    if (failure instanceof LinkApiError && failure.status === 401) { void session.signOut('expired'); return true; }
    return false;
  }, [session.signOut]);

  const load = useCallback(async (current: string, mode: 'initial' | 'refresh' = 'initial') => {
    if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setLoadError(null);
    try { setRecord(await patientLinkApi.myRecord(current)); }
    catch (failure) { if (!handleAuthFailure(failure)) { setRecord(null); setLoadError(failure as LinkApiError); } }
    finally { setLoading(false); setRefreshing(false); }
  }, [handleAuthFailure]);

  useEffect(() => { if (token) void load(token); }, [token, load]);

  function update<K extends keyof RecordForm>(key: K, value: RecordForm[K]) {
    setForm(previous => previous && ({ ...previous, [key]: value }));
    if (errors[key]) setErrors(previous => ({ ...previous, [key]: undefined }));
  }

  function startEdit() {
    if (!record) return;
    setForm(recordToForm(record)); setErrors({}); setSaveError(null); setNotice(null);
  }

  async function save() {
    if (!token || !record || !form || saving) return;
    const { errors: found, request } = validateRecordForm(form, record.version);
    setErrors(found); setSaveError(null);
    if (!request) return;
    setSaving(true);
    try {
      const change = await patientLinkApi.updateMyRecord(token, request);
      setRecord(change.record); setForm(null); setNotice(change.notification.message);
    } catch (failure) {
      if (!handleAuthFailure(failure)) setSaveError((failure as Error).message);
    } finally { setSaving(false); }
  }

  if (!token) return null;
  if (loading && !record) return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>;

  // MSG06 means the self link is missing or was revoked: nothing private is shown.
  if (!record) return <ScrollView contentContainerStyle={styles.container}>
    <Banner tone="error" message={loadError?.message ?? 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.'}
      action={loadError?.status === 403 ? undefined : { label: 'Thử lại', onPress: () => void load(token) }} />
    <Button title="Quay lại trạng thái liên kết" variant="secondary" onPress={() => router.replace('/account-link')} />
  </ScrollView>;

  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled"
    refreshControl={form ? undefined : <RefreshControl refreshing={refreshing} onRefresh={() => void load(token, 'refresh')} />}>
    <View style={styles.banner}>
      <Text style={styles.name}>{record.fullName}</Text>
      <Text style={styles.code}>{record.patientCode}</Text>
      <Text style={styles.muted}>Liên kết từ {formatTime(record.linkedAt)}</Text>
    </View>
    {notice && <Banner tone="success" message={notice} />}
    {record.isArchived && <Banner tone="info" message="Hồ sơ đang tạm ngừng sử dụng tại phòng khám. Vui lòng liên hệ phòng khám nếu cần cập nhật thông tin." />}

    {!form ? <>
      <Card title="Thông tin cơ bản">
        <Row label="Mã bệnh nhân" value={record.patientCode} />
        <Row label="Họ tên" value={record.fullName} />
        <Row label="Ngày sinh" value={record.birthDate ? record.birthDate.split('-').reverse().join('/') : unknown} />
        <Row label="Giới tính" value={record.gender ? genderLabels[record.gender] : unknown} />
        <Row label="Số điện thoại" value={record.phone} />
        <Row label="Địa chỉ" value={record.address} />
      </Card>
      <Card title="Người giám hộ">
        {record.guardianName ? <>
          <Row label="Họ tên" value={record.guardianName} />
          <Row label="Quan hệ" value={record.guardianRelationship} />
          <Row label="Số điện thoại" value={record.guardianPhone} />
        </> : <Text style={styles.muted}>Không có người giám hộ.</Text>}
      </Card>
      <Text style={styles.muted}>Cập nhật lúc {formatTime(record.updatedAt)}</Text>
      {!record.isArchived && <Button title="Chỉnh sửa thông tin" onPress={startEdit} />}
      <Button title="Quay lại" variant="secondary" onPress={() => router.back()} />
    </> : <>
      <Card title="Chỉnh sửa thông tin">
        {saveError && <Banner tone="error" message={saveError} action={{ label: 'Tải lại hồ sơ', onPress: () => { setForm(null); void load(token, 'refresh'); } }} />}
        <View style={styles.readonly}>
          <Text style={styles.label}>Mã bệnh nhân</Text>
          <Text style={styles.readonlyValue}>{record.patientCode}</Text>
          <Text style={styles.hint}>Mã do phòng khám cấp, không thể thay đổi.</Text>
        </View>
        <Field label="Họ tên *" value={form.fullName} onChangeText={value => update('fullName', value)} maxLength={100}
          autoCapitalize="words" error={errors.fullName} />
        <Field label="Ngày sinh" value={form.birthDate} onChangeText={value => update('birthDate', formatBirthDateInput(value))}
          placeholder="DD-MM-YYYY" hint="Ví dụ: 12-05-1990. Để trống nếu chưa rõ." keyboardType="number-pad" error={errors.birthDate} />
        <View style={styles.field}>
          <Text style={styles.label}>Giới tính</Text>
          <View style={styles.choices}>
            {([null, 'MALE', 'FEMALE', 'OTHER'] as (Gender | null)[]).map(option => {
              const selected = form.gender === option;
              return <Pressable key={option ?? 'unknown'} accessibilityRole="radio" accessibilityState={{ selected }}
                onPress={() => update('gender', option)} style={[styles.choice, selected && styles.choiceSelected]}>
                <Text style={[styles.choiceText, selected && styles.choiceTextSelected]}>{option ? genderLabels[option] : unknown}</Text>
              </Pressable>;
            })}
          </View>
        </View>
        <Field label="Số điện thoại" value={form.phone} onChangeText={value => update('phone', value)} maxLength={20}
          keyboardType="phone-pad" error={errors.phone} />
        <Field label="Địa chỉ" value={form.address} onChangeText={value => update('address', value)} maxLength={255} error={errors.address} />
      </Card>
      <Card title="Người giám hộ">
        <Text style={styles.hint}>Nhập khi cần người đại diện. Thông tin này không cấp quyền xem hồ sơ qua ứng dụng.</Text>
        <Field label="Tên người giám hộ" value={form.guardianName} onChangeText={value => update('guardianName', value)} maxLength={160}
          error={errors.guardianName} />
        <Field label="Quan hệ với bệnh nhân" value={form.guardianRelationship} onChangeText={value => update('guardianRelationship', value)}
          maxLength={40} placeholder="Ví dụ: Mẹ" error={errors.guardianRelationship} />
        <Field label="SĐT người giám hộ" value={form.guardianPhone} onChangeText={value => update('guardianPhone', value)} maxLength={20}
          keyboardType="phone-pad" error={errors.guardianPhone} />
      </Card>
      <Button title="Lưu thay đổi" busy={saving} onPress={() => void save()} />
      <Button title="Hủy" variant="secondary" disabled={saving} onPress={() => { setForm(null); setErrors({}); setSaveError(null); }} />
    </>}
  </ScrollView>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  banner: { gap: 2 },
  name: { fontSize: 22, fontWeight: '700', color: palette.text },
  code: { fontSize: 16, fontWeight: '600', color: palette.primary },
  muted: { color: palette.muted },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { color: palette.muted, flexShrink: 0 },
  rowValue: { color: palette.text, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  rowEmpty: { color: palette.muted, fontWeight: '400' },
  field: { gap: spacing.xs },
  label: { fontWeight: '600', color: palette.text },
  hint: { color: palette.muted, fontSize: 12 },
  readonly: { gap: spacing.xs },
  readonlyValue: { borderWidth: 1, borderColor: palette.border, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 10, fontSize: 16, color: palette.muted, backgroundColor: '#EEF1F5' },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  choice: { borderWidth: 1, borderColor: palette.border, borderRadius: 999, paddingHorizontal: 16, paddingVertical: 8, backgroundColor: '#fff' },
  choiceSelected: { borderColor: palette.primary, backgroundColor: '#EFF8FF' },
  choiceText: { color: palette.text },
  choiceTextSelected: { color: palette.primary, fontWeight: '600' },
});
