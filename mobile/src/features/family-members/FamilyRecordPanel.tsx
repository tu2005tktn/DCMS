import { useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { uuid } from 'expo-modules-core';
import { spacing } from '@/theme/spacing';
import { Banner, Button, Card, Field, formatTime, palette } from '@/features/patient-link/components/LinkUi';
import { base64 } from '@/features/visit-results/useAuthorizedImage';
import { FamilyApiError, familyApi, keyFor } from './family-members.api';
import {
  accessLabels, appointmentLabels, displayDate, mealLabels, progressLabels, scopeLabels,
  type AccessOverview, type FamilyAppointment, type FamilyDocument, type FamilyExamination, type FamilyPrescription,
  type FamilyTreatment, type Page, type RecordHeader, type Scope,
} from './family-members.types';
import { messageOf, useFamilySession } from './useFamilySession';

const denied = (failure: unknown) => failure instanceof FamilyApiError && [403, 404].includes(failure.status);
const statusTone: Record<string, [string, string]> = {
  Verified: ['#067647', '#ECFDF3'], Pending: ['#B54708', '#FFFAEB'], none: ['#344054', '#F2F4F7'],
};

// Step 2 of docs/specs/MOBILE-FAMILY-MEMBERS.vi.md: appointments booked for the member, record access and the record itself.
// Nothing here is stored on the device; leaving the screen or signing out drops it with the screen state.
export default function FamilyRecordPanel({ memberId }: { memberId: string }) {
  const { token, handleAuth } = useFamilySession();
  const [overview, setOverview] = useState<AccessOverview | null>(null), [header, setHeader] = useState<RecordHeader | null>(null);
  const [loading, setLoading] = useState(true), [error, setError] = useState<string | null>(null), [revision, setRevision] = useState(0);
  const [patientCode, setPatientCode] = useState(''), [matching, setMatching] = useState(''), [showRequest, setShowRequest] = useState(false);
  const [saving, setSaving] = useState(false), [confirmWithdraw, setConfirmWithdraw] = useState(false), [tab, setTab] = useState<Scope | null>(null);
  const pending = useRef<{ text: string; key: string } | null>(null);

  useEffect(() => {
    if (!token) return;
    let live = true;
    setLoading(true); setError(null);
    (async () => {
      try {
        const value = await familyApi.access(token, memberId);
        if (!live) return;
        setOverview(value);
        if (value.current?.status !== 'Verified') { setHeader(null); return; }
        const record = await familyApi.header(token, memberId);
        if (live) { setHeader(record); setTab(current => current && record.scopes.includes(current) ? current : record.scopes[0] ?? null); }
      } catch (failure) { if (live && !handleAuth(failure)) { setHeader(null); setError(messageOf(failure)); } }
      finally { if (live) setLoading(false); }
    })();
    return () => { live = false; };
  }, [token, memberId, revision, handleAuth]);
  const reload = useCallback(() => { setHeader(null); setRevision(v => v + 1); }, []);

  async function submit() {
    if (!token || saving) return;
    const code = patientCode.trim(), info = matching.trim();
    if (code.length > 40 || info.length > 1000) return;
    setSaving(true); setError(null);
    try {
      await familyApi.requestAccess(token, memberId, code || null, info || null,
        keyFor(pending, { action: 'submit', code, info }, () => uuid.v4()));
      pending.current = null; setShowRequest(false); setPatientCode(''); setMatching(''); reload();
    } catch (failure) { if (!handleAuth(failure)) { setError(messageOf(failure)); if (denied(failure)) reload(); } }
    finally { setSaving(false); }
  }

  async function withdraw() {
    const current = overview?.current;
    if (!token || !current || saving) return;
    setSaving(true); setError(null);
    try {
      await familyApi.withdrawAccess(token, memberId, current.id, current.version,
        keyFor(pending, { action: 'withdraw', id: current.id, version: current.version }, () => uuid.v4()));
      pending.current = null; setConfirmWithdraw(false); reload();
    } catch (failure) { if (!handleAuth(failure)) { setError(messageOf(failure)); reload(); } }
    finally { setSaving(false); }
  }

  if (!token) return null;
  const current = overview?.current ?? null;
  const [color, background] = statusTone[current?.status ?? 'none'] ?? statusTone.none;
  return <>
    <Card title="Lịch đặt hộ">
      <SectionList<FamilyAppointment> token={token} memberId={memberId} section="appointments" onDenied={reload}
        empty="Chưa có lịch đặt hộ được liên kết với người thân này." render={item => <AppointmentRow key={item.code} item={item} />} />
    </Card>

    <Card title="Hồ sơ của người thân">
      {!header && <Text style={styles.muted}>Hồ sơ tự hiển thị sau khi phòng khám tiếp nhận đúng người thân từ lịch bạn đặt hộ; trường hợp này không cần gửi yêu cầu. Người thân không cần có tài khoản DCMS.</Text>}
      {error && <Banner tone="error" message={error} action={{ label: 'Thử lại', onPress: reload }} />}
      {loading ? <ActivityIndicator color={palette.primary} /> : overview && <>
        <View style={styles.statusRow}>
          <View style={[styles.badge, { backgroundColor: background }]}><Text style={[styles.badgeText, { color }]}>{current ? accessLabels[current.status] : 'Chưa có hồ sơ liên kết'}</Text></View>
          <Pressable accessibilityRole="button" onPress={reload}><Text style={styles.link}>Tải lại trạng thái</Text></Pressable>
        </View>
        {current?.publicNote && <Banner tone="info" message={current.publicNote} />}
        {current?.status === 'Pending' && <Text style={styles.text}>Yêu cầu đã gửi. Khi người thân đến khám, vui lòng phối hợp với phòng khám để đối chiếu danh tính và xác nhận phạm vi chia sẻ.</Text>}

        {overview.canRequest && (!showRequest
          ? <Button title="Liên kết hồ sơ đã có tại phòng khám" variant="secondary" onPress={() => setShowRequest(true)} />
          : <View style={styles.box}>
            <Field label="Mã hồ sơ người thân (nếu biết)" value={patientCode} onChangeText={setPatientCode} maxLength={40} autoCapitalize="characters" />
            <Field label="Thông tin giúp phòng khám đối chiếu" value={matching} onChangeText={setMatching} maxLength={1000} multiline
              hint={`${matching.length}/1000 ký tự. Ví dụ: ngày khám gần nhất, chi nhánh đã khám.`} />
            <Button title="Yêu cầu quyền xem hồ sơ" busy={saving} onPress={() => void submit()} />
            <Button title="Hủy" variant="secondary" disabled={saving} onPress={() => setShowRequest(false)} />
          </View>)}

        {current && (current.status === 'Pending' || current.status === 'Verified') && (!confirmWithdraw
          ? <Button title={current.status === 'Pending' ? 'Rút yêu cầu' : 'Chấm dứt quyền truy cập'} variant="danger" onPress={() => setConfirmWithdraw(true)} />
          : <View style={styles.box}>
            <Text style={styles.strong}>{current.status === 'Pending' ? 'Rút yêu cầu xác minh?' : 'Chấm dứt quyền xem hồ sơ?'}</Text>
            <Button title="Xác nhận" variant="danger" busy={saving} onPress={() => void withdraw()} />
            <Button title="Hủy" variant="secondary" disabled={saving} onPress={() => setConfirmWithdraw(false)} />
          </View>)}

        {header && <View style={styles.box}>
          <Text style={styles.strong}>{header.fullName} · {header.patientCode}</Text>
          <Text style={styles.text}>Ngày sinh: {displayDate(header.birthDate)}</Text>
          <Text style={styles.text}>Chi nhánh: {header.branchName}</Text>
          <Text style={styles.text}>Có hiệu lực đến: {formatTime(header.expiresAt)}</Text>
        </View>}
        {header && header.scopes.length > 0 && <>
          <View style={styles.tabs} accessibilityRole="tablist">{header.scopes.map(scope =>
            <Pressable key={scope} accessibilityRole="tab" accessibilityState={{ selected: tab === scope }} onPress={() => setTab(scope)}
              style={[styles.tab, tab === scope && styles.tabActive]}><Text style={[styles.tabText, tab === scope && styles.tabTextActive]}>{scopeLabels[scope]}</Text></Pressable>)}
          </View>
          {tab && <ScopeContent key={tab} token={token} memberId={memberId} scope={tab} onDenied={reload} />}
        </>}

        {overview.history.length > 0 && <View style={styles.history}>
          <Text style={styles.strong}>Lịch sử yêu cầu</Text>
          {overview.history.map(item => <Text key={item.id} style={styles.muted}>{accessLabels[item.status]} · {formatTime(item.requestedAt)}{item.publicNote ? ` · ${item.publicNote}` : ''}</Text>)}
        </View>}
      </>}
    </Card>
  </>;
}

function ScopeContent({ token, memberId, scope, onDenied }: { token: string; memberId: string; scope: Scope; onDenied: () => void }) {
  const empty = 'Chưa có nội dung chính thức trong phạm vi đã cấp.';
  if (scope === 'Examinations') return <SectionList<FamilyExamination> token={token} memberId={memberId} section="examinations" onDenied={onDenied} empty={empty}
    render={item => <View key={item.visitId} style={styles.item}>
      <Text style={styles.strong}>{formatTime(item.arrivalAt)} · {item.dentistName}</Text>
      <Text style={styles.muted}>{item.branchName}</Text>
      <Line label="Triệu chứng" value={item.symptoms} /><Line label="Chẩn đoán" value={item.diagnoses.join('; ')} />
      <Line label="Kết luận" value={item.conclusion} /><Line label="Lời dặn" value={item.advice} />
    </View>} />;
  if (scope === 'Treatment') return <SectionList<FamilyTreatment> token={token} memberId={memberId} section="treatment" onDenied={onDenied} empty={empty}
    render={item => <View key={item.id} style={styles.item}>
      <Text style={styles.strong}>{item.code} · {displayDate(item.startedOn)}</Text>
      {!!item.diagnosis && <Text style={styles.text}>{item.diagnosis}</Text>}
      {item.items.map((line, index) => <Text key={index} style={styles.text}>• {line.serviceName}{line.region ? ` · ${line.region}` : ''} · {progressLabels[line.progress] ?? line.progress}
        {line.estimatedStart || line.estimatedEnd ? `\n   Dự kiến ${displayDate(line.estimatedStart)} – ${displayDate(line.estimatedEnd)}` : ''}</Text>)}
    </View>} />;
  if (scope === 'Prescriptions') return <SectionList<FamilyPrescription> token={token} memberId={memberId} section="prescriptions" onDenied={onDenied} empty={empty}
    render={item => <View key={item.id} style={styles.item}>
      <Text style={styles.strong}>Đơn ngày {formatTime(item.finalizedAt)}</Text>
      {!!item.diagnosis && <Text style={styles.muted}>{item.diagnosis}</Text>}
      {item.lines.map((line, index) => <View key={index} style={styles.medicine}>
        <Text style={styles.strong}>{line.medicineName}{line.strength ? ` ${line.strength}` : ''} · {line.quantity} {line.unit}</Text>
        <Text style={styles.text}>{line.route} · {line.days} ngày · Sáng {line.morningDose}, trưa {line.noonDose}, chiều {line.afternoonDose}, tối {line.eveningDose} · {mealLabels[line.mealTiming] ?? line.mealTiming}</Text>
        {!!line.instructions && <Text style={styles.muted}>{line.instructions}</Text>}
      </View>)}
      {!!item.generalAdvice && <Line label="Lời dặn" value={item.generalAdvice} />}
    </View>} />;
  return <Documents token={token} memberId={memberId} onDenied={onDenied} />;
}

function Documents({ token, memberId, onDenied }: { token: string; memberId: string; onDenied: () => void }) {
  const [open, setOpen] = useState<FamilyDocument | null>(null);
  return <>
    <SectionList<FamilyDocument> token={token} memberId={memberId} section="documents" onDenied={onDenied} empty="Chưa có nội dung chính thức trong phạm vi đã cấp."
      render={item => <Pressable key={item.id} accessibilityRole="button" onPress={() => setOpen(item)} style={styles.item}>
        <Text style={styles.strong}>{item.fileName}</Text>
        <Text style={styles.muted}>{displayDate(item.sourceDate)} · {Math.max(1, Math.ceil(item.sizeBytes / 1024))} KB</Text>
        <Text style={styles.link}>{item.contentType.startsWith('image/') ? 'Xem ảnh' : 'Tệp này xem trên web khách hàng'}</Text>
      </Pressable>} />
    {open && <DocumentViewer token={token} memberId={memberId} document={open} onClose={() => setOpen(null)} onDenied={() => { setOpen(null); onDenied(); }} />}
  </>;
}

function DocumentViewer({ token, memberId, document, onClose, onDenied }:
  { token: string; memberId: string; document: FamilyDocument; onClose: () => void; onDenied: () => void }) {
  const image = document.contentType.startsWith('image/');
  const [uri, setUri] = useState<string | null>(null), [failed, setFailed] = useState(false), [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!image) return;
    let live = true; setUri(null); setFailed(false);
    (async () => {
      try {
        const response = await fetch(familyApi.documentUrl(memberId, document.id), { headers: { Authorization: `Bearer ${token}` }, cache: 'no-store' });
        if ([403, 404].includes(response.status)) { if (live) onDenied(); return; }
        if (!response.ok) throw new Error();
        const data = base64(new Uint8Array(await response.arrayBuffer()));
        if (live) setUri(`data:${response.headers.get('content-type') ?? document.contentType};base64,${data}`);
      } catch { if (live) setFailed(true); }
    })();
    return () => { live = false; };
  }, [image, token, memberId, document.id, document.contentType, attempt]);
  return <Modal visible transparent animationType="fade" onRequestClose={onClose}>
    <View style={styles.viewer}>
      <Text style={styles.viewerTitle}>{document.fileName}</Text>
      {!image ? <Text style={styles.viewerText}>Ứng dụng chưa mở được loại tệp này. Vui lòng xem trên web khách hàng.</Text>
        : failed ? <><Text style={styles.viewerText}>Không thể tải ảnh. Quyền truy cập có thể đã thay đổi.</Text>
          <Pressable accessibilityRole="button" onPress={() => setAttempt(v => v + 1)}><Text style={styles.viewerLink}>Thử lại</Text></Pressable></>
        : uri ? <Image source={{ uri }} style={styles.viewerImage} resizeMode="contain" accessibilityLabel={document.fileName} />
        : <ActivityIndicator color="#fff" />}
      <Pressable accessibilityRole="button" onPress={onClose} style={styles.viewerClose}><Text style={styles.viewerLink}>Đóng</Text></Pressable>
    </View>
  </Modal>;
}

function SectionList<T>({ token, memberId, section, render, empty, onDenied }:
  { token: string; memberId: string; section: string; render: (item: T) => React.ReactNode; empty: string; onDenied: () => void }) {
  const { handleAuth } = useFamilySession();
  const [data, setData] = useState<Page<T> | null>(null), [items, setItems] = useState<T[]>([]);
  const [loading, setLoading] = useState(true), [error, setError] = useState<string | null>(null), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const deniedRef = useRef(onDenied); deniedRef.current = onDenied;
  useEffect(() => {
    let live = true; setLoading(true); setError(null);
    const call = section === 'appointments' ? familyApi.appointments(token, memberId, page) as unknown as Promise<Page<T>>
      : familyApi.section<T>(token, memberId, section, page);
    call.then(value => { if (!live) return; setData(value); setItems(previous => page === 1 ? value.items : [...previous, ...value.items]); })
      .catch(failure => { if (!live || handleAuth(failure)) return; setError(messageOf(failure)); if (denied(failure) && section !== 'appointments') { setItems([]); deniedRef.current(); } })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [token, memberId, section, page, revision, handleAuth]);
  return <View style={styles.list}>
    {error && <Banner tone="error" message={error} action={{ label: 'Thử lại', onPress: () => { setPage(1); setRevision(v => v + 1); } }} />}
    {!error && !loading && items.length === 0 && <Text style={styles.muted}>{empty}</Text>}
    {items.map(render)}
    {loading ? <ActivityIndicator color={palette.primary} />
      : data && items.length < data.total && <Button title="Xem thêm" variant="secondary" onPress={() => setPage(p => p + 1)} />}
  </View>;
}

function AppointmentRow({ item }: { item: FamilyAppointment }) {
  const router = useRouter();
  return <Pressable accessibilityRole="button" onPress={() => router.push(`/(account)/appointments/${encodeURIComponent(item.code)}`)} style={styles.item}>
    <Text style={styles.strong}>{item.code} · {appointmentLabels[item.status] ?? item.status}</Text>
    <Text style={styles.text}>{formatTime(item.startAt)}</Text>
    <Text style={styles.muted}>{item.branchName} · {item.dentistName}</Text>
  </Pressable>;
}

function Line({ label, value }: { label: string; value: string | null }) {
  return <Text style={styles.text}><Text style={styles.muted}>{label}: </Text>{value || '—'}</Text>;
}

const styles = StyleSheet.create({
  muted: { color: palette.muted, lineHeight: 20 },
  text: { color: palette.text, lineHeight: 20 },
  strong: { color: palette.text, fontWeight: '700' },
  link: { color: palette.primary, fontWeight: '600' },
  statusRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: spacing.sm },
  badge: { borderRadius: 999, paddingHorizontal: 12, paddingVertical: 4 },
  badgeText: { fontWeight: '600' },
  box: { gap: spacing.sm, borderWidth: 1, borderColor: palette.border, borderRadius: 12, padding: spacing.md, backgroundColor: '#FAFCFF' },
  tabs: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  tab: { borderWidth: 1, borderColor: palette.border, borderRadius: 999, paddingHorizontal: 14, minHeight: 38, justifyContent: 'center', backgroundColor: '#fff' },
  tabActive: { borderColor: palette.primary, backgroundColor: '#EFF8FF' },
  tabText: { color: palette.text },
  tabTextActive: { color: palette.primary, fontWeight: '700' },
  list: { gap: spacing.sm },
  item: { gap: 3, borderTopWidth: 1, borderTopColor: palette.border, paddingTop: spacing.sm },
  medicine: { gap: 2, paddingLeft: spacing.sm },
  history: { gap: 4 },
  viewer: { flex: 1, backgroundColor: 'rgba(0,0,0,0.92)', alignItems: 'center', justifyContent: 'center', padding: spacing.md, gap: spacing.md },
  viewerTitle: { color: '#fff', fontWeight: '700', fontSize: 16, textAlign: 'center' },
  viewerText: { color: '#fff', textAlign: 'center' },
  viewerLink: { color: '#9EC5FF', fontWeight: '700', fontSize: 16 },
  viewerImage: { width: '100%', height: '75%' },
  viewerClose: { minHeight: 44, justifyContent: 'center', paddingHorizontal: spacing.md },
});
