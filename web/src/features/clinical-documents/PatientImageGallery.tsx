import { Alert, Button, Image, Pagination, Progress, Spin } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { documentsApi } from './documents.api';
import { fileStateLabels, newRequestKey, type DocumentItem, type DocumentPage, type UploadMetadata } from './types';
import './patientImageGallery.css';

const changedEvent = 'dcms:patient-images-changed';
type UploadRow = { id: string; file: File; metadata: UploadMetadata; state: 'uploading' | 'done' | 'failed'; progress: number; error?: string };

function StoredImage({ item }: { item: DocumentItem }) {
  const [url, setUrl] = useState<string>(), [error, setError] = useState<string>();
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    if (item.fileState !== 'Ready') return;
    let live = true, objectUrl: string | undefined;
    setError(undefined);
    void documentsApi.content(item.documentId, 'inline').then(blob => {
      if (!live) return;
      objectUrl = URL.createObjectURL(blob); setUrl(objectUrl);
    }).catch(e => { if (live) setError(e.message); });
    return () => { live = false; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [item.documentId, item.version, item.fileState, retry]);
  return <figure className="patient-image-card">
    {item.fileState !== 'Ready' ? <div className="patient-image-placeholder">{fileStateLabels[item.fileState]}{item.rejectReason && <small>{item.rejectReason}</small>}</div>
      : error ? <div role="alert">{error}<Button onClick={() => setRetry(v => v + 1)}>Thử lại</Button></div>
      : url ? <Image src={url} alt={item.fileName} width={172} height={112} /> : <Spin />}
    <figcaption title={item.fileName}>{item.fileName}</figcaption>
  </figure>;
}

// Both surfaces read the same authenticated ClinicalDocument source; no second file copy.
export default function PatientImageGallery({ patientId, dossierId, canUpload = false }: {
  patientId: number; dossierId?: string; canUpload?: boolean;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [data, setData] = useState<DocumentPage>(), [error, setError] = useState<string>();
  const [loading, setLoading] = useState(false), [page, setPage] = useState(1), [revision, setRevision] = useState(0);
  const [rows, setRows] = useState<UploadRow[]>([]), [selectionError, setSelectionError] = useState<string>();
  const controllers = useRef(new Map<string, AbortController>());
  const sending = useRef(new Set<string>());
  const generation = useRef(0);
  const refresh = () => setRevision(v => v + 1);
  useEffect(() => { const changed = (e: Event) => { if ((e as CustomEvent<number>).detail === patientId) refresh(); };
    window.addEventListener(changedEvent, changed); return () => window.removeEventListener(changedEvent, changed);
  }, [patientId]);
  useEffect(() => { setPage(1); setRows([]); setData(undefined); }, [patientId, dossierId]);
  useEffect(() => {
    let live = true; setLoading(true); setError(undefined);
    void documentsApi.list({ patientId, dossierId, imagesOnly: true, page, pageSize: 20 }).then(v => { if (live) setData(v); })
      .catch(e => { if (live) setError(e.message); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [patientId, dossierId, page, revision]);
  useEffect(() => {
    if (!data?.items.some(i => i.fileState === 'PendingScan')) return;
    const timer = window.setTimeout(refresh, 5000); return () => window.clearTimeout(timer);
  }, [data]);
  useEffect(() => () => { generation.current++; controllers.current.forEach(c => c.abort()); }, [patientId, dossierId]);
  const patch = (id: string, value: Partial<UploadRow>) => setRows(rs => rs.map(r => r.id === id ? { ...r, ...value } : r));
  async function send(row: UploadRow, batchGeneration = generation.current) {
    if (batchGeneration !== generation.current || sending.current.has(row.id)) return;
    sending.current.add(row.id); const controller = new AbortController(); controllers.current.set(row.id, controller);
    patch(row.id, { state: 'uploading', error: undefined, progress: 0 });
    try {
      await documentsApi.upload(row.metadata, row.file, progress => patch(row.id, { progress }), controller.signal);
      if (controller.signal.aborted || batchGeneration !== generation.current) return;
      patch(row.id, { state: 'done', progress: 100 }); setPage(1);
      window.dispatchEvent(new CustomEvent(changedEvent, { detail: patientId }));
    } catch (e) { if (!controller.signal.aborted) patch(row.id, { state: 'failed', error: (e as Error).message }); }
    finally { sending.current.delete(row.id); controllers.current.delete(row.id); }
  }
  function select(files: File[]) {
    if (!dossierId) return;
    const limits = data?.constraints ?? { maxFiles: 10, maxBytes: 20 * 1024 * 1024 };
    if (files.length > limits.maxFiles) { setSelectionError(`Chọn tối đa ${limits.maxFiles} ảnh mỗi lần.`); return; }
    if (files.some(f => !['image/jpeg', 'image/png'].includes(f.type) || !/\.(jpe?g|png)$/i.test(f.name) || f.size === 0 || f.size > limits.maxBytes)) {
      setSelectionError('Chỉ chọn ảnh JPEG/PNG không rỗng, tối đa 20 MB mỗi ảnh.'); return;
    }
    setSelectionError(undefined);
    const added: UploadRow[] = files.map(file => ({ id: newRequestKey(), file, state: 'uploading', progress: 0,
      metadata: { patientId, dossierId, visitId: null, planId: null, documentType: 'Other', source: 'Clinic',
        sourceDate: new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10), description: '', requestKey: newRequestKey() } }));
    setRows(rs => [...rs.filter(r => r.state !== 'done'), ...added]);
    const batchGeneration = generation.current;
    void (async () => { for (const row of added) await send(row, batchGeneration); })();
  }
  return <div className="patient-image-gallery">
    <div className="patient-image-grid">
      {canUpload && dossierId && <>
        <input ref={input} type="file" accept=".jpg,.jpeg,.png,image/jpeg,image/png" multiple hidden aria-label="Chọn ảnh từ máy"
          onChange={e => { select(Array.from(e.target.files ?? [])); e.target.value = ''; }} />
        <button type="button" className="treatment-sheet-image-tile" aria-label="Thêm ảnh" title="Chọn ảnh từ máy"
          disabled={rows.some(r => r.state === 'uploading')} onClick={() => input.current?.click()}>
          <svg width="54" height="64" viewBox="0 0 54 64" fill="none" aria-hidden="true"><path d="M5 3h31l13 13v45H5z" stroke="currentColor" strokeWidth="4"/><path d="M35 3v15h14" stroke="currentColor" strokeWidth="4"/><circle cx="18" cy="28" r="5" fill="currentColor"/><path d="m9 52 13-15 9 10 7-8 8 13" stroke="currentColor" strokeWidth="4"/></svg>
        </button>
      </>}
      {data?.items.map(item => <StoredImage key={item.documentId} item={item} />)}
    </div>
    {loading && <Spin size="small" />}
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={refresh}>Thử lại</Button>} />}
    {selectionError && <Alert type="error" message={selectionError} />}
    {!loading && !error && data?.total === 0 && !canUpload && <p>Chưa có hình ảnh.</p>}
    {rows.map(r => r.state === 'done' ? null : <div key={r.id} className="patient-image-upload" role="status">
      <span>{r.file.name}</span>{r.state === 'uploading' ? <Progress percent={r.progress} size="small" />
        : <><span role="alert">{r.error}</span><Button onClick={() => void send(r)}>Thử lại tải ảnh</Button></>}
    </div>)}
    {(data?.total ?? 0) > 20 && <Pagination size="small" current={page} pageSize={20} total={data?.total} onChange={setPage} showSizeChanger={false} />}
  </div>;
}
