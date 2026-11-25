import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { uuid } from 'expo-modules-core';
import { spacing } from '@/theme/spacing';
import { Banner, Button, Card, Field, palette } from '@/features/patient-link/components/LinkUi';
import { formatBirthDateInput, isoToDisplay, toIsoDate } from '@/features/patient-link/patient-link.schema';
import { FamilyApiError, familyApi, keyFor } from './family-members.api';
import { genderLabels, relationships, type FamilyMember, type Gender, type Relationship, type SaveFamilyMember } from './family-members.types';
import { messageOf, useFamilySession } from './useFamilySession';

type Form = { fullName: string; relationship: Relationship | null; birthDate: string; gender: Gender | null; contactPhone: string };
type Errors = Partial<Record<keyof Form, string>>;
const empty: Form = { fullName: '', relationship: null, birthDate: '', gender: null, contactPhone: '' };
const toForm = (m: FamilyMember): Form => ({ fullName: m.fullName, relationship: m.relationship, birthDate: isoToDisplay(m.birthDate), gender: m.gender, contactPhone: m.contactPhone ?? '' });
// Server field names (FAMILY_MEMBER_VALIDATION.field) map one-to-one onto the form.
const serverFields: Record<string, keyof Form> = { fullName: 'fullName', relationship: 'relationship', birthDate: 'birthDate', gender: 'gender', contactPhone: 'contactPhone' };

function validate(form: Form): { errors: Errors; body?: SaveFamilyMember } {
  const errors: Errors = {};
  const fullName = form.fullName.trim(), phone = form.contactPhone.trim(), birth = form.birthDate.trim();
  if (!fullName) errors.fullName = 'Vui lòng nhập Họ tên.';
  else if (fullName.length > 100) errors.fullName = 'Họ tên không được vượt quá 100 ký tự.';
  if (!form.relationship) errors.relationship = 'Vui lòng chọn Quan hệ.';
  const iso = birth ? toIsoDate(birth) : null;
  if (birth && !iso) errors.birthDate = 'Ngày sinh không đúng định dạng hoặc sau hôm nay.';
  if (!form.gender) errors.gender = 'Vui lòng chọn Giới tính.';
  if (!phone) errors.contactPhone = 'Vui lòng nhập Số điện thoại liên hệ.';
  else if (!/^0[35789][0-9]{8}$/.test(phone)) errors.contactPhone = 'Số di động Việt Nam gồm 10 số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.';
  if (Object.keys(errors).length) return { errors };
  return { errors, body: { fullName, relationship: form.relationship!, birthDate: iso, gender: form.gender!, contactPhone: phone } };
}

// Add (no id) or edit (?id=) one family member: docs/specs/MOBILE-FAMILY-MEMBERS.vi.md, step 1.
export default function FamilyMemberFormScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id?: string }>();
  const { token, handleAuth } = useFamilySession();
  const [member, setMember] = useState<FamilyMember | null>(null);
  const [form, setForm] = useState<Form>(empty), [errors, setErrors] = useState<Errors>({});
  const [loading, setLoading] = useState(!!id), [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false), [saveError, setSaveError] = useState<string | null>(null), [stale, setStale] = useState(false);
  const pending = useRef<{ text: string; key: string } | null>(null), sending = useRef(false);

  const load = useCallback(async (keepForm: boolean) => {
    if (!token || !id) return;
    setLoading(!keepForm); setLoadError(null);
    try {
      const value = await familyApi.get(token, id);
      setMember(value); if (!keepForm) setForm(toForm(value));
      setStale(false); setSaveError(null);
    } catch (failure) { if (!handleAuth(failure)) setLoadError(messageOf(failure)); }
    finally { setLoading(false); }
  }, [token, id, handleAuth]);
  useEffect(() => { void load(false); }, [load]);

  function update<K extends keyof Form>(key: K, value: Form[K]) {
    setForm(previous => ({ ...previous, [key]: value }));
    if (errors[key]) setErrors(previous => ({ ...previous, [key]: undefined }));
  }

  async function save() {
    if (!token || sending.current) return;
    const { errors: found, body } = validate(form);
    setErrors(found); setSaveError(null);
    if (!body) return;
    sending.current = true; setSaving(true);
    const key = keyFor(pending, { id, version: member?.version, body }, () => uuid.v4());
    try {
      if (id && member) await familyApi.update(token, id, body, member.version, key);
      else await familyApi.create(token, body, key);
      pending.current = null;
      router.back();
    } catch (failure) {
      if (handleAuth(failure)) return;
      if (failure instanceof FamilyApiError && failure.field && serverFields[failure.field]) setErrors(previous => ({ ...previous, [serverFields[failure.field!]]: failure.message }));
      else setSaveError(messageOf(failure));
      setStale(failure instanceof FamilyApiError && failure.status === 409 && failure.code === 'MSG41');
    } finally { sending.current = false; setSaving(false); }
  }

  if (!token) return null;
  if (loading) return <View style={styles.center}><ActivityIndicator color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>;
  if (id && !member) return <ScrollView contentContainerStyle={styles.container}>
    <Banner tone="error" message={loadError ?? 'Không tìm thấy người thân trong danh sách của bạn.'} action={{ label: 'Thử lại', onPress: () => void load(false) }} />
    <Button title="Về danh sách người thân" variant="secondary" onPress={() => router.replace('/(account)/family-members')} />
  </ScrollView>;

  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
    {saveError && <Banner tone="error" message={saveError} action={stale ? { label: 'Tải bản mới nhất', onPress: () => void load(true) } : undefined} />}
    {stale && <Text style={styles.hint}>Nội dung bạn đang nhập vẫn được giữ. Tải bản mới nhất rồi bấm Lưu lại.</Text>}
    <Card title="Thông tin người thân">
      <Field label="Họ tên *" value={form.fullName} onChangeText={value => update('fullName', value)} maxLength={100} autoCapitalize="words" error={errors.fullName} />
      <Choices label="Quan hệ *" error={errors.relationship} options={relationships} value={form.relationship} onChange={value => update('relationship', value)} />
      <Field label="Ngày sinh" value={form.birthDate} onChangeText={value => update('birthDate', formatBirthDateInput(value))}
        placeholder="DD-MM-YYYY" hint="Ví dụ: 12-05-2015. Để trống nếu chưa rõ." keyboardType="number-pad" error={errors.birthDate} />
      <Choices label="Giới tính *" error={errors.gender} value={form.gender} onChange={value => update('gender', value)}
        options={(Object.keys(genderLabels) as Gender[]).map(value => ({ value, label: genderLabels[value] }))} />
      <Field label="Số điện thoại liên hệ *" value={form.contactPhone} onChangeText={value => update('contactPhone', value)} maxLength={10}
        keyboardType="phone-pad" placeholder="0912345678" error={errors.contactPhone} />
      {id && <Text style={styles.hint}>Đổi họ tên hoặc ngày sinh sẽ chấm dứt yêu cầu hoặc quyền xem hồ sơ đang có của người thân này.</Text>}
    </Card>
    <Button title={id ? 'Lưu thay đổi' : 'Thêm người thân'} busy={saving} onPress={() => void save()} />
    <Button title="Hủy" variant="secondary" disabled={saving} onPress={() => router.back()} />
  </ScrollView>;
}

function Choices<T extends string>({ label, options, value, onChange, error }:
  { label: string; options: { value: T; label: string }[]; value: T | null; onChange: (value: T) => void; error?: string }) {
  return <View style={styles.field}>
    <Text style={styles.label}>{label}</Text>
    <View style={styles.choices} accessibilityRole="radiogroup" accessibilityLabel={label}>
      {options.map(option => {
        const selected = value === option.value;
        return <Pressable key={option.value} accessibilityRole="radio" accessibilityState={{ checked: selected }} aria-checked={selected} onPress={() => onChange(option.value)}
          style={[styles.choice, selected && styles.choiceSelected, !!error && !selected && styles.choiceError]}>
          <Text style={[styles.choiceText, selected && styles.choiceTextSelected]}>{option.label}</Text>
        </Pressable>;
      })}
    </View>
    {error && <Text style={styles.error}>{error}</Text>}
  </View>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background, paddingBottom: 40 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  muted: { color: palette.muted },
  hint: { color: palette.muted, fontSize: 12 },
  field: { gap: spacing.xs },
  label: { fontWeight: '600', color: palette.text },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  choice: { borderWidth: 1, borderColor: palette.border, borderRadius: 999, paddingHorizontal: 16, minHeight: 40, justifyContent: 'center', backgroundColor: '#fff' },
  choiceSelected: { borderColor: palette.primary, backgroundColor: '#EFF8FF' },
  choiceError: { borderColor: palette.danger },
  choiceText: { color: palette.text },
  choiceTextSelected: { color: palette.primary, fontWeight: '600' },
  error: { color: palette.danger, fontSize: 13 },
});
