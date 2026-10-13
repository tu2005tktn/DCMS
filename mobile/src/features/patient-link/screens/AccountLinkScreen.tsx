import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { spacing } from '@/theme/spacing';
import { DemoSignIn } from '../components/DemoSignIn';
import { Banner, Button, Card, Field, StatusBadge, formatTime, palette } from '../components/LinkUi';
import { LinkApiError, patientLinkApi } from '../patient-link.api';
import { emptyLinkForm, formatBirthDateInput, limits, validateLinkForm, type LinkForm, type LinkFormErrors } from '../patient-link.schema';
import type { OwnRecordSummary, SelfLinkOverview } from '../patient-link.types';

// S07 Account Link Request (UC-36). The account always comes from the session; no Patient search is exposed.
export default function AccountLinkScreen() {
  const [token, setToken] = useState<string | null>(null);
  const [signInNotice, setSignInNotice] = useState<string | null>(null);
  const [overview, setOverview] = useState<SelfLinkOverview | null>(null);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<LinkApiError | null>(null);
  const [form, setForm] = useState<LinkForm>(emptyLinkForm);
  const [errors, setErrors] = useState<LinkFormErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmWithdraw, setConfirmWithdraw] = useState(false);
  const [withdrawing, setWithdrawing] = useState(false);
  const [record, setRecord] = useState<OwnRecordSummary | null>(null);
  const [recordBusy, setRecordBusy] = useState(false);

  const handleAuthFailure = useCallback((failure: unknown) => {
    if (failure instanceof LinkApiError && failure.status === 401) {
      setToken(null); setOverview(null); setSignInNotice(failure.message);
      return true;
    }
    return false;
  }, []);

  const load = useCallback(async (current: string, mode: 'initial' | 'refresh' = 'initial') => {
    if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setLoadError(null);
    try { setOverview(await patientLinkApi.overview(current)); }
    catch (failure) { if (!handleAuthFailure(failure)) setLoadError(failure as LinkApiError); }
    finally { setLoading(false); setRefreshing(false); }
  }, [handleAuthFailure]);

  useEffect(() => { if (token) void load(token); }, [token, load]);

  function update<K extends keyof LinkForm>(key: K, value: LinkForm[K]) {
    setForm(previous => ({ ...previous, [key]: value }));
    if (errors[key]) setErrors(previous => ({ ...previous, [key]: undefined }));
  }

  async function submit() {
    if (!token || submitting) return;
    const { errors: found, request } = validateLinkForm(form);
    setErrors(found); setActionError(null); setNotice(null);
    if (!request) return;
    setSubmitting(true);
    try {
      const result = await patientLinkApi.submit(token, request);
      setNotice(result.notification.message);
      setForm(emptyLinkForm); setRecord(null);
      await load(token, 'refresh');
    } catch (failure) { if (!handleAuthFailure(failure)) setActionError((failure as Error).message); }
    finally { setSubmitting(false); }
  }

  async function withdraw() {
    const current = overview?.current;
    if (!token || !current || withdrawing) return;
    setWithdrawing(true); setActionError(null); setNotice(null);
    try {
      await patientLinkApi.withdraw(token, current.id, current.version);
      setConfirmWithdraw(false); setNotice('Đã rút yêu cầu liên kết. Chưa có quyền xem hồ sơ.');
      await load(token, 'refresh');
    } catch (failure) {
      if (!handleAuthFailure(failure)) { setActionError((failure as Error).message); setConfirmWithdraw(false); await load(token, 'refresh'); }
    } finally { setWithdrawing(false); }
  }

  async function openRecord() {
    if (!token || recordBusy) return;
    setRecordBusy(true); setActionError(null);
    try { setRecord(await patientLinkApi.ownRecord(token)); }
    catch (failure) { if (!handleAuthFailure(failure)) { setRecord(null); setActionError((failure as Error).message); await load(token, 'refresh'); } }
    finally { setRecordBusy(false); }
  }

  async function signOut() {
    if (token) await patientLinkApi.signOut(token).catch(() => null);
    setToken(null); setOverview(null); setRecord(null); setNotice(null); setActionError(null); setForm(emptyLinkForm); setErrors({});
  }

  if (!token) return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
    <Text style={styles.title}>Liên kết hồ sơ của tôi</Text>
    <DemoSignIn notice={signInNotice} onSignedIn={value => { setSignInNotice(null); setToken(value); }} />
  </ScrollView>;

  if (loading && !overview) return <View style={styles.center}><ActivityIndicator size="large" color={palette.primary} /><Text style={styles.muted}>Đang tải…</Text></View>;

  if (!overview) return <ScrollView contentContainerStyle={styles.container}>
    <Banner tone="error" message={loadError?.message ?? 'Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối và thử lại.'}
      action={{ label: 'Thử lại', onPress: () => void load(token) }} />
    {loadError?.status === 403 && <Text style={styles.muted}>Chức năng này dành cho tài khoản công khai của người bệnh.</Text>}
    <Button title="Đăng nhập tài khoản khác" variant="secondary" onPress={() => void signOut()} />
  </ScrollView>;

  const { account, current, linkStatus } = overview;
  const rejectedOrRevoked = linkStatus === 'REJECTED' || linkStatus === 'REVOKED';
  const history = overview.history.filter(item => item.id !== current?.id);
  return <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled"
    refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(token, 'refresh')} />}>
    <Text style={styles.title}>Liên kết hồ sơ của tôi</Text>
    <Text style={styles.muted}>Phòng khám xác minh danh tính trước khi cấp quyền xem hồ sơ. Xác minh liên hệ hoặc đặt lịch hộ người thân không tạo liên kết.</Text>
    {notice && <Banner tone="success" message={notice} />}
    {actionError && <Banner tone="error" message={actionError} />}
    {loadError && <Banner tone="error" message={loadError.message} action={{ label: 'Thử lại', onPress: () => void load(token, 'refresh') }} />}

    <Card title="Tài khoản hiện tại">
      <Row label="Tên đăng nhập" value={account.userName} />
      <Row label="Mã tài khoản" value={`#${account.id}`} />
      <Row label="Liên hệ đã xác minh" value={account.verifiedEmail ?? account.verifiedPhone ?? 'Chưa có'} />
    </Card>

    <Card title="Trạng thái liên kết">
      <StatusBadge status={linkStatus} />
      {current && linkStatus !== 'NOT_LINKED' && <>
        <Row label="Mã yêu cầu" value={current.requestCode} />
        <Row label="Gửi lúc" value={formatTime(current.requestedAt)} />
        {current.decidedAt && <Row label="Xử lý lúc" value={formatTime(current.decidedAt)} />}
        {current.result && <Text style={styles.body}>{current.result.message}</Text>}
        {current.publicNote && <Text style={styles.body}>Phòng khám: {current.publicNote}</Text>}
      </>}
      {linkStatus === 'NOT_LINKED' && <Text style={styles.muted}>Bạn chưa có liên kết hồ sơ. Chỉ gửi yêu cầu sau khi đã đến khám và được tiếp nhận tại phòng khám.</Text>}
      {linkStatus === 'PENDING' && <>
        <Text style={styles.muted}>Yêu cầu đang chờ phòng khám xác minh. Bạn chưa có quyền xem hồ sơ.</Text>
        {confirmWithdraw
          ? <View style={styles.actions}>
              <Text style={styles.body}>Rút yêu cầu này? Bạn có thể gửi lại sau.</Text>
              <Button title="Xác nhận rút yêu cầu" variant="danger" busy={withdrawing} onPress={() => void withdraw()} />
              <Button title="Hủy" variant="secondary" disabled={withdrawing} onPress={() => setConfirmWithdraw(false)} />
            </View>
          : <Button title="Rút yêu cầu" variant="secondary" onPress={() => setConfirmWithdraw(true)} />}
      </>}
      {linkStatus === 'VERIFIED' && <>
        <Button title="Xem hồ sơ của tôi" busy={recordBusy} onPress={() => void openRecord()} />
        {record && <View style={styles.record}>
          <Row label="Mã hồ sơ" value={record.patientCode} />
          <Row label="Họ tên" value={record.fullName} />
          <Row label="Ngày sinh" value={record.birthDate ? record.birthDate.split('-').reverse().join('/') : '—'} />
        </View>}
      </>}
      {rejectedOrRevoked && <Text style={styles.muted}>Vui lòng liên hệ phòng khám để được hỗ trợ, hoặc bổ sung thông tin và gửi yêu cầu mới bên dưới.</Text>}
      <Button title="Tải lại trạng thái" variant="secondary" busy={refreshing} onPress={() => void load(token, 'refresh')} />
    </Card>

    {overview.canSubmit && <Card title={rejectedOrRevoked ? 'Gửi yêu cầu mới' : 'Gửi yêu cầu liên kết'}>
      <Field label="Họ tên *" value={form.fullName} onChangeText={value => update('fullName', value)} maxLength={limits.fullName}
        error={errors.fullName} autoComplete="name" />
      <Field label="Ngày sinh" value={form.birthDate} onChangeText={value => update('birthDate', formatBirthDateInput(value))}
        placeholder="DD-MM-YYYY" hint="Ví dụ: 12-05-1990" error={errors.birthDate} keyboardType="number-pad" maxLength={10} />
      <Field label="Mã hồ sơ (nếu biết)" value={form.patientCode} onChangeText={value => update('patientCode', value)} autoCapitalize="characters"
        error={errors.patientCode} maxLength={limits.patientCode} />
      <Field label="Thông tin đối chiếu" value={form.matchingInformation} onChangeText={value => update('matchingInformation', value)} multiline
        maxLength={limits.matchingInformation} error={errors.matchingInformation}
        hint={`Ví dụ ngày khám gần nhất. Không nhập thông tin không cần thiết. ${form.matchingInformation.length}/${limits.matchingInformation}`} />
      <View style={styles.switchRow}>
        <Switch value={form.confirmSelf} onValueChange={value => update('confirmSelf', value)} accessibilityLabel="Xác nhận đây là hồ sơ của chính tôi" />
        <Text style={styles.switchText}>Tôi xác nhận đây là hồ sơ của chính tôi, không phải của người thân.</Text>
      </View>
      {errors.confirmSelf && <Text style={styles.error}>{errors.confirmSelf}</Text>}
      <Button title="Gửi yêu cầu" busy={submitting} onPress={() => void submit()} />
    </Card>}

    {history.length > 0 && <Card title="Yêu cầu trước đây">
      {history.map(item => <View key={item.id} style={styles.historyItem}>
        <Text style={styles.body}>{item.requestCode} · {formatTime(item.requestedAt)}</Text>
        <StatusBadge status={item.status} />
      </View>)}
    </Card>}

    <Button title="Đăng xuất" variant="secondary" onPress={() => void signOut()} />
  </ScrollView>;
}

function Row({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><Text style={styles.rowLabel}>{label}</Text><Text style={styles.rowValue}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, gap: spacing.md, backgroundColor: palette.background, flexGrow: 1 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm },
  title: { fontSize: 24, fontWeight: '600', color: palette.text },
  muted: { color: palette.muted },
  body: { color: palette.text, fontSize: 15 },
  error: { color: palette.danger, fontSize: 13 },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { color: palette.muted },
  rowValue: { color: palette.text, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  actions: { gap: spacing.sm },
  record: { gap: spacing.xs, paddingTop: spacing.sm },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  switchText: { flex: 1, color: palette.text },
  historyItem: { gap: spacing.xs, paddingVertical: spacing.xs },
});
