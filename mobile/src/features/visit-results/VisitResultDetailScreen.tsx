/**
 * Chi tiết kết quả khám của một lượt (docs/specs/MOBILE-PATIENT-VISIT-RESULTS.vi.md §2.3), read-only.
 * Official confirmed result, non-cancelled investigations (results only once reviewed), prescription link and the
 * visit's Ready images/documents. Empty sections are hidden; nothing is invented when a source is empty.
 */

import { useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Image, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Banner, Button, Card, palette } from '@/features/patient-link/components/LinkUi';
import { formatDate, formatDateTime } from '@/features/appointments/appointments.rules';
import { useSession } from '@/features/auth/session/useSession';
import { RecordApiError } from '@/features/medical-records/medical-records.api';
import { spacing } from '@/theme/spacing';
import ImageViewer from './ImageViewer';
import { visitResultsApi } from './visit-results.api';
import { useAuthorizedImage } from './useAuthorizedImage';
import {
  dispositionLabels, documentTypeLabels, investigationStateLabels, isImage, sourceLabels, visitStatusLabels,
  type OwnDocument, type OwnVisitDetail,
} from './visit-results.types';

const surfaceLabels: Record<string, string> = { M: 'gần', D: 'xa', O: 'nhai', B: 'ngoài', L: 'trong', I: 'cắn' };

export default function VisitResultDetailScreen() {
  const { visitId } = useLocalSearchParams<{ visitId: string }>();
  const router = useRouter();
  const session = useSession();
  const token = session.state.status === 'authenticated' ? session.state.accessToken : null;
  const [detail, setDetail] = useState<OwnVisitDetail | null>(null);
  const [documents, setDocuments] = useState<OwnDocument[]>([]);
  const [documentTotal, setDocumentTotal] = useState(0);
  const [documentPage, setDocumentPage] = useState(1);
  const [documentsError, setDocumentsError] = useState<RecordApiError | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<RecordApiError | null>(null);
  const [denied, setDenied] = useState(false);
  const [viewer, setViewer] = useState<number | null>(null);
  const order = useRef(0);

  const clear = useCallback(() => { setDetail(null); setDocuments([]); setDocumentTotal(0); setViewer(null); }, []);
  // 401 → end the session; 403 (link revoked) → stop and drop every result and image source held in state.
  const authFailure = useCallback((failure: unknown) => {
    if (failure instanceof RecordApiError && failure.status === 401) { clear(); void session.signOut('expired'); return true; }
    if (failure instanceof RecordApiError && failure.status === 403) { order.current++; clear(); setDenied(true); return true; }
    return false;
  }, [clear, session.signOut]);

  const load = useCallback(async (mode: 'initial' | 'refresh') => {
    if (!token || !visitId) return;
    const id = ++order.current;
    if (mode === 'refresh') setRefreshing(true); else setLoading(true);
    setError(null); setDocumentsError(null);
    try {
      const result = await visitResultsApi.visit(token, visitId);
      if (id !== order.current) return;
      setDetail(result); setDenied(false);
      try {
        const page = await visitResultsApi.documents(token, visitId, 1);
        if (id !== order.current) return;
        setDocuments(page.items); setDocumentTotal(page.total); setDocumentPage(1);
      } catch (failure) { if (id === order.current && !authFailure(failure)) setDocumentsError(failure as RecordApiError); }
    } catch (failure) { if (id === order.current && !authFailure(failure)) setError(failure as RecordApiError); }
    finally { if (id === order.current) { setLoading(false); setRefreshing(false); } }
  }, [token, visitId, authFailure]);

  const moreDocuments = useCallback(async () => {
    if (!token || !visitId || loadingMore || documents.length >= documentTotal) return;
    const id = order.current;
    setLoadingMore(true); setDocumentsError(null);
    try {
      const page = await visitResultsApi.documents(token, visitId, documentPage + 1);
      if (id !== order.current) return;
      setDocuments(current => [...current, ...page.items.filter(x => !current.some(y => y.documentId === x.documentId))]);
      setDocumentTotal(page.total); setDocumentPage(page.page);
    } catch (failure) { if (id === order.current && !authFailure(failure)) setDocumentsError(failure as RecordApiError); }
    finally { setLoadingMore(false); }
  }, [token, visitId, loadingMore, documents.length, documentTotal, documentPage, authFailure]);

  useEffect(() => { if (!token) { order.current++; clear(); return; } void load('initial'); }, [token, load, clear]);

  if (!token) return null;
  if (loading && !detail) return <View style={styles.center}><ActivityIndicator color={palette.primary} /><Text style={styles.muted}>Đang tải kết quả khám…</Text></View>;
  if (denied) return <View style={styles.page}>
    <Banner tone="error" message="Liên kết hồ sơ không còn hiệu lực. Nội dung kết quả khám đã được ẩn."
      action={{ label: 'Thử lại', onPress: () => void load('initial') }} />
    <Button title="Mở màn liên kết hồ sơ" onPress={() => router.push('/(account)/account-link')} />
  </View>;
  if (!detail) return <View style={styles.page}>
    <Banner tone="error" message={error?.status === 404 ? 'Không tìm thấy kết quả khám của lượt này.' : error?.message ?? 'Không tải được kết quả khám.'}
      action={error?.status === 404 ? undefined : { label: 'Thử lại', onPress: () => void load('initial') }} />
  </View>;

  const { visit, examination: exam, investigations } = detail;
  const images = documents.filter(isImage);
  const files = documents.filter(x => !isImage(x));

  return <>
    <ScrollView style={styles.list} contentContainerStyle={styles.page}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load('refresh')} />}>
      <Card title={`Lượt khám ${formatDate(visit.arrivalAt)}`}>
        <Text style={styles.meta}>{visit.dentistName} · {visit.branchName}</Text>
        <Text style={styles.ok}>Phiếu khám đã xác nhận · phiên bản {exam.version}</Text>
        <Text style={styles.muted}>{visit.completedAt ? `Lượt đã kết thúc lúc ${formatDateTime(visit.completedAt)}` : `Lượt: ${visitStatusLabels[visit.visitStatus] ?? visit.visitStatus}`}</Text>
      </Card>

      <Card title="Kết quả khám">
        <Item label="Lý do khám" value={exam.symptoms} />
        {exam.findings.length > 0 && <View style={styles.block}>
          <Text style={styles.label}>Kết quả theo răng / vùng</Text>
          {exam.findings.map((f, i) => <View key={i} style={styles.finding}>
            <Text style={styles.findingTitle}>{f.tooth ? `Răng ${f.tooth}` : f.region || 'Vùng khám'}{f.surfaces.length ? ` · mặt ${f.surfaces.map(s => surfaceLabels[s] ?? s).join(', ')}` : ''}</Text>
            {f.diagnoses.length > 0 && <Text style={styles.value}>{f.diagnoses.join('; ')}</Text>}
            {!!f.condition?.trim() && <Text style={styles.muted}>{f.condition}</Text>}
          </View>)}
        </View>}
        <Item label="Chẩn đoán" value={exam.diagnoses.join('; ')} />
        {exam.conclusion && exam.conclusion !== exam.diagnoses.join('; ') && <Item label="Kết luận" value={exam.conclusion} />}
        <Item label="Dặn dò" value={exam.advice} />
        <Item label="Hướng xử trí" value={exam.disposition ? dispositionLabels[exam.disposition] ?? exam.disposition : null} />
        <Item label="Nhu cầu quay lại" value={exam.returnNeed} />
        <Item label="Chuyển khám" value={exam.referralDestination} />
        <Text style={styles.note}>Nhu cầu quay lại chưa phải lịch hẹn. Lịch đã đặt nằm ở mục Lịch hẹn của tôi.</Text>
      </Card>

      {investigations.length > 0 && <Card title="Cận lâm sàng">
        {investigations.map(o => <View key={o.id} style={styles.finding}>
          <Text style={styles.findingTitle}>{o.name}{o.region ? ` · ${o.region}` : ''}</Text>
          <Text style={styles.muted}>{investigationStateLabels[o.state] ?? o.state}</Text>
          {o.resultText && <Text style={styles.value}>Kết quả: {o.resultText}</Text>}
          {o.interpretation && <Text style={styles.value}>Nhận định: {o.interpretation}</Text>}
        </View>)}
      </Card>}

      {visit.hasPrescription && <Card title="Đơn thuốc mua ngoài">
        <Button title="Xem đơn thuốc" variant="secondary" onPress={() => router.push('/(account)/prescriptions')} />
      </Card>}

      <Card title={`Hình ảnh & tài liệu (${documentTotal})`}>
        {documentsError && <Banner tone="error" message={documentsError.message} action={{ label: 'Thử lại', onPress: () => void load('refresh') }} />}
        {!documentsError && documentTotal === 0 && <Text style={styles.muted}>Lượt khám này chưa có ảnh hoặc tài liệu được phép xem.</Text>}
        {images.length > 0 && <View style={styles.grid}>
          {images.map((doc, i) => <Thumbnail key={doc.documentId} doc={doc} token={token} onOpen={() => setViewer(i)}
            onDenied={status => authFailure(new RecordApiError('Phiên hoặc liên kết không còn hiệu lực.', status, 'MSG06'))} />)}
        </View>}
        {files.map(doc => <View key={doc.documentId} style={styles.file}>
          <Text style={styles.findingTitle}>{documentTypeLabels[doc.documentType] ?? doc.documentType} · {formatDate(`${doc.sourceDate}T12:00:00+07:00`)}</Text>
          <Text style={styles.muted}>{doc.fileName} · Nguồn: {sourceLabels[doc.source] ?? doc.source}</Text>
          <Text style={styles.note}>Định dạng này chưa xem được trong app. Vui lòng liên hệ phòng khám để nhận bản sao.</Text>
        </View>)}
        {documents.length < documentTotal && <Button title="Tải thêm ảnh/tài liệu" variant="secondary" busy={loadingMore} onPress={() => void moreDocuments()} />}
      </Card>
    </ScrollView>
    <ImageViewer images={images} index={viewer} token={token} onIndex={setViewer} onClose={() => setViewer(null)}
      onDenied={status => authFailure(new RecordApiError('Phiên hoặc liên kết không còn hiệu lực.', status, 'MSG06'))} />
  </>;
}

// Each thumbnail fetches its own content lazily; an error only affects that cell and can be retried.
function Thumbnail({ doc, token, onOpen, onDenied }: { doc: OwnDocument; token: string; onOpen: () => void; onDenied: (status: number) => void }) {
  const [attempt, setAttempt] = useState(0);
  const image = useAuthorizedImage(token, doc.documentId, attempt);
  useEffect(() => { if (image.status === 401 || image.status === 403) onDenied(image.status); }, [image.status]);
  const label = documentTypeLabels[doc.documentType] ?? doc.documentType;
  return <Pressable accessibilityRole="imagebutton" accessibilityLabel={`${label}, ngày ${formatDate(`${doc.sourceDate}T12:00:00+07:00`)}`}
    onPress={image.failed ? () => setAttempt(v => v + 1) : onOpen} style={styles.thumb}>
    {image.failed
      ? <View style={styles.thumbError}><Text style={styles.thumbErrorText}>Lỗi tải{'\n'}Thử lại</Text></View>
      : image.uri ? <Image source={{ uri: image.uri }} style={styles.thumbImage} resizeMode="cover" />
        : <View style={[styles.thumbImage, styles.thumbLoading]}><ActivityIndicator color={palette.primary} /></View>}
    <Text style={styles.thumbLabel} numberOfLines={1}>{label}</Text>
  </Pressable>;
}

function Item({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value?.trim()) return null;
  return <View style={styles.block}><Text style={styles.label}>{label}</Text><Text style={styles.value}>{value}</Text></View>;
}

const styles = StyleSheet.create({
  list: { backgroundColor: palette.background },
  page: { padding: spacing.lg, gap: spacing.md },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.sm, backgroundColor: palette.background },
  muted: { color: palette.muted, fontSize: 13 },
  note: { color: palette.muted, fontSize: 12, fontStyle: 'italic' },
  meta: { color: palette.text, fontSize: 14 },
  ok: { color: palette.success, fontWeight: '600' },
  block: { gap: 2 },
  label: { color: palette.muted, fontSize: 13, fontWeight: '600' },
  value: { color: palette.text, fontSize: 15 },
  finding: { borderLeftWidth: 3, borderLeftColor: palette.primary, paddingLeft: spacing.sm, gap: 2, marginTop: 4 },
  findingTitle: { color: palette.text, fontWeight: '600', fontSize: 14 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  thumb: { width: 96, gap: 2 },
  thumbImage: { width: 96, height: 96, borderRadius: 8, backgroundColor: palette.border },
  thumbLoading: { alignItems: 'center', justifyContent: 'center' },
  thumbError: { width: 96, height: 96, borderRadius: 8, backgroundColor: palette.dangerBg, alignItems: 'center', justifyContent: 'center' },
  thumbErrorText: { color: palette.danger, textAlign: 'center', fontSize: 12 },
  thumbLabel: { color: palette.muted, fontSize: 11 },
  file: { gap: 2, paddingVertical: 4 },
});
