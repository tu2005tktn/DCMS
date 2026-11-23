import { Alert, Button, Image, Input, Modal, Pagination, Progress, Select, Spin } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { documentsApi } from './documents.api';
import { fileStateLabels, newRequestKey, type DocumentItem, type DocumentPage, type PatientDocumentContexts, type UploadMetadata } from './types';
import './patientImageGallery.css';

const changedEvent = 'dcms:patient-images-changed';
type UploadRow = { id: string; file: File; metadata: UploadMetadata; state: 'uploading' | 'done' | 'failed'; progress: number; error?: string };

function StoredImage({ item, onDelete }: { item: DocumentItem; onDelete?: (item: DocumentItem) => void }) {
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
    {onDelete && <Button size="small" danger className="patient-image-delete" aria-label={`Xóa ảnh ${item.fileName}`} onClick={() => onDelete(item)}>Xóa</Button>}
  </figure>;
}

// Both surfaces read the same authenticated ClinicalDocument source; no second file copy.
// Upload needs a visit or dossier (CK_CLINICAL_DOCUMENT_Context): the patient profile picks one; the treatment sheet passes its dossier.
type Target = { visitId: string | null; dossierId: string | null };
const targetKey = (t: Target) => t.visitId ? `visit:${t.visitId}` : `dossier:${t.dossierId}`;
export default function PatientImageGallery({ patientId, dossierId, canUpload = false, canDelete = false }: {
  patientId: number; dossierId?: string; canUpload?: boolean; canDelete?: boolean;
}) {
  const [contexts, setContexts] = useState<PatientDocumentContexts | null>(null), [target, setTarget] = useState<string>();
  const [removing, setRemoving] = useState<{ item: DocumentItem; reason: string; key: string; busy: boolean; error?: string }>();
  useEffect(() => {
    setContexts(null); setTarget(undefined);
    if (!canUpload || dossierId) return;
    let live = true;
    void documentsApi.contexts(patientId).then(c => {
      if (!live) return; setContexts(c);
      const open = c.visits.find(v => v.acceptsUpload);
      const first: Target | undefined = open ? { visitId: open.id, dossierId: null } : c.dossiers[0] ? { visitId: null, dossierId: c.dossiers[0].id } : undefined;
      if (first) setTarget(targetKey(first));
    }).catch(() => { if (live) setContexts(null); });
    return () => { live = false; };
  }, [patientId, dossierId, canUpload]);
  const targetOptions = contexts ? [
    ...contexts.visits.filter(v => v.acceptsUpload).map(v => ({ value: targetKey({ visitId: v.id, dossierId: null }), label: `Lượt khám · ${v.label.replace(/^Lượt /, '')}` })),
    ...contexts.dossiers.map(d => ({ value: targetKey({ visitId: null, dossierId: d.id }), label: `Hồ sơ điều trị · ${d.label}` })),
  ] : [];
  const uploadTarget: Target | undefined = dossierId ? { visitId: null, dossierId }
    : target ? (target.startsWith('visit:') ? { visitId: target.slice(6), dossierId: null } : { visitId: null, dossierId: target.slice(8) }) : undefined;
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
    if (!uploadTarget) return;
    const limits = data?.constraints ?? { maxFiles: 10, maxBytes: 20 * 1024 * 1024 };
    if (files.length > limits.maxFiles) { setSelectionError(`Chọn tối đa ${limits.maxFiles} ảnh mỗi lần.`); return; }
    if (files.some(f => !['image/jpeg', 'image/png'].includes(f.type) || !/\.(jpe?g|png)$/i.test(f.name) || f.size === 0 || f.size > limits.maxBytes)) {
      setSelectionError('Chỉ chọn ảnh JPEG/PNG không rỗng, tối đa 20 MB mỗi ảnh.'); return;
    }
    setSelectionError(undefined);
    const added: UploadRow[] = files.map(file => ({ id: newRequestKey(), file, state: 'uploading', progress: 0,
      metadata: { patientId, dossierId: uploadTarget.dossierId, visitId: uploadTarget.visitId, documentType: 'Other', source: 'Clinic',
        sourceDate: new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10), description: '', requestKey: newRequestKey() } }));
    setRows(rs => [...rs.filter(r => r.state !== 'done'), ...added]);
    const batchGeneration = generation.current;
    void (async () => { for (const row of added) await send(row, batchGeneration); })();
  }
  async function confirmRemove() {
    if (!removing || removing.busy) return;
    const reason = removing.reason.trim();
    if (!reason) { setRemoving({ ...removing, error: 'Nhập lý do xóa ảnh.' }); return; }
    setRemoving({ ...removing, busy: true, error: undefined });
    try {
      await documentsApi.remove(removing.item.documentId, removing.item.version, reason, removing.key);
      setRemoving(undefined); window.dispatchEvent(new CustomEvent(changedEvent, { detail: patientId }));
    } catch (e) { setRemoving(r => r && { ...r, busy: false, error: (e as Error).message }); }
  }
  return <div className="patient-image-gallery">
    {canUpload && !dossierId && targetOptions.length > 0 && <div className="patient-image-target">
      <span>Gắn ảnh mới với</span>
      <Select aria-label="Gắn ảnh mới với" value={target} options={targetOptions} onChange={setTarget} style={{ minWidth: 280 }} />
    </div>}
    {canUpload && !dossierId && contexts && targetOptions.length === 0 && <Alert type="info" showIcon style={{ marginBottom: 12 }}
      message="Cần một lượt khám đang mở hoặc hồ sơ điều trị để tải ảnh lên." />}
    <div className="patient-image-grid">
      {canUpload && uploadTarget && <>
        <input ref={input} type="file" accept=".jpg,.jpeg,.png,image/jpeg,image/png" multiple hidden aria-label="Chọn ảnh từ máy"
          onChange={e => { select(Array.from(e.target.files ?? [])); e.target.value = ''; }} />
        <button type="button" className="treatment-sheet-image-tile" aria-label="Thêm ảnh" title="Chọn ảnh từ máy"
          disabled={rows.some(r => r.state === 'uploading')} onClick={() => input.current?.click()}>
          <svg width="54" height="64" viewBox="0 0 54 64" fill="none" aria-hidden="true"><path d="M5 3h31l13 13v45H5z" stroke="currentColor" strokeWidth="4"/><path d="M35 3v15h14" stroke="currentColor" strokeWidth="4"/><circle cx="18" cy="28" r="5" fill="currentColor"/><path d="m9 52 13-15 9 10 7-8 8 13" stroke="currentColor" strokeWidth="4"/></svg>
        </button>
      </>}
      {data?.items.map(item => <StoredImage key={item.documentId} item={item}
        onDelete={canDelete ? doc => setRemoving({ item: doc, reason: '', key: newRequestKey(), busy: false }) : undefined} />)}
    </div>
    {loading && <Spin size="small" />}
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={refresh}>Thử lại</Button>} />}
    {selectionError && <Alert type="error" message={selectionError} />}
    {!loading && !error && data?.total === 0 && !canUpload && <p>Chưa có hình ảnh.</p>}
    {rows.map(r => r.state === 'done' ? null : <div key={r.id} className="patient-image-upload" role="status">
      <span>{r.file.name}</span>{r.state === 'uploading' ? <Progress percent={r.progress} size="small" />
        : <><span role="alert">{r.error}</span><Button onClick={() => void send(r)}>Thử lại tải ảnh</Button></>}
    </div>)}
    <Modal open={!!removing} title="Xóa ảnh" okText="Xóa ảnh" okButtonProps={{ danger: true, disabled: removing?.busy }} confirmLoading={removing?.busy}
      cancelButtonProps={{ disabled: removing?.busy }} closable={!removing?.busy} onOk={() => void confirmRemove()} onCancel={() => { if (!removing?.busy) setRemoving(undefined); }}>
      <p>Ảnh <strong>{removing?.item.fileName}</strong> sẽ bị ẩn khỏi hồ sơ. Hệ thống vẫn lưu lịch sử và người xóa.</p>
      <Input.TextArea aria-label="Lý do xóa ảnh" placeholder="Lý do xóa (bắt buộc)" maxLength={500} rows={3} value={removing?.reason}
        disabled={removing?.busy} onChange={e => setRemoving(r => r && { ...r, reason: e.target.value, key: newRequestKey(), error: undefined })} />
      {removing?.error && <Alert type="error" showIcon message={removing.error} style={{ marginTop: 12 }} />}
    </Modal>
    {(data?.total ?? 0) > 20 && <Pagination size="small" current={page} pageSize={20} total={data?.total} onChange={setPage} showSizeChanger={false} />}
  </div>;
}
