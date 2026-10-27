import { Alert, Button, Dropdown, Input, Modal, Pagination, Skeleton } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import type { AppointmentOptions, TreatmentDossier, TreatmentItem } from './types';
import { money } from './types';
import TreatmentSheetDraftRow, { type SheetActualPreview } from './TreatmentSheetDraftForms';
import DossierFinancialPanel from './DossierFinancialPanel';
import type { ActivityPage, SheetActivity, SheetContext } from './sheetTypes';

type Draft = { id: string; kind: 'Plan' | 'Actual'; row: SheetActualPreview; version: number };
type Editor = { kind: 'plan' | 'actual'; row?: SheetActualPreview; itemId?: string; activity?: SheetActivity; transferItemId?: string };
const date = (v: string) => new Date(v).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const fees: Record<string, string> = { Pending: 'Chưa ghi nhận phí', Posted: 'Đã ghi nhận phí', Included: 'Đã bao gồm', Free: 'Miễn phí', NotRequested: 'Chưa yêu cầu phí', NeedsRule: 'Chờ quy tắc phí gói', NeedsAdjustment: 'Chờ điều chỉnh phí' };
const included = (i: TreatmentItem) => !i.sheetDeleted && !i.hiddenFromSheet && (i.entrySource === 'ClinicianEntry' || i.decision === 'Agreed');

export default function TreatmentSheet({ patientId, dossierId, dossierVersion, options, onLoaded, onPlanTotalChange }: {
  patientId: number; dossierId: string; dossierVersion: number; visitId?: string; options: AppointmentOptions | null;
  onLoaded: (v: TreatmentDossier | null) => void; onPlanTotalChange: (id: string, total: number) => void;
}) {
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null), [context, setContext] = useState<SheetContext | null>(null);
  const [drafts, setDrafts] = useState<Draft[]>([]), [activities, setActivities] = useState<ActivityPage | null>(null);
  const [error, setError] = useState<string>(), [activityError, setActivityError] = useState<string>();
  const [reload, setReload] = useState(0), [page, setPage] = useState(1), [editor, setEditor] = useState<Editor | null>(null);
  const [history, setHistory] = useState(false);
  const [deletingActivity, setDeletingActivity] = useState<SheetActivity | null>(null);
  const [deleting, setDeleting] = useState<TreatmentItem | null>(null);
  const [busy, setBusy] = useState(false), actionGuard = useRef(false);
  const actionKey = useRef<{ payload: string; key: string } | null>(null);
  const write = useRef<{ id: string; payload: string; key: string; expected: number; saved?: number } | null>(null);
  const [note, setNote] = useState(''), [noteBusy, setNoteBusy] = useState(false), [noteError, setNoteError] = useState<string>();
  const noteDirty = useRef(false), noteSaved = useRef('');
  const noteKey = useRef<{ text: string; key: string; version: number } | null>(null);
  const refresh = () => setReload(v => v + 1);
  async function load() {
    const [value, ctx, rows] = await Promise.all([api<TreatmentDossier>(`/api/dossiers/${dossierId}`),
      api<SheetContext>(`/api/dossiers/${dossierId}/sheet-context`), api<Draft[]>(`/api/dossiers/${dossierId}/sheet-drafts`)]);
    if (value.patientId !== patientId) throw new ApiError(403, 'ACCESS_DENIED', 'Phiếu không thuộc bệnh nhân này.');
    return { value, ctx, rows };
  }
  function accept({ value, ctx, rows }: Awaited<ReturnType<typeof load>>) {
    setDossier(value); setContext(ctx); setDrafts(rows.map(r => ({ ...r, row: { ...r.row, id: r.id, draftVersion: r.version } })));
    onLoaded(value); onPlanTotalChange(value.id, value.items.filter(included).reduce((s,i) => s + Math.round(i.quantity * i.agreedUnitPriceVnd) - (i.discount?.status === 'Approved' ? i.discount.amountVnd : 0), 0));
    if (!noteDirty.current) { setNote(value.note ?? ''); noteSaved.current = value.note ?? ''; }
  }
  useEffect(() => { let live = true; setError(undefined); void load().then(v => { if (live) accept(v); }).catch(e => { if (live) setError(e.message); }); return () => { live = false; }; }, [dossierId, dossierVersion, reload, patientId]);
  useEffect(() => {
    let live = true; setActivityError(undefined);
    void api<ActivityPage>(`/api/dossiers/${dossierId}/activities?page=${page}&pageSize=20`).then(v => { if (live) setActivities(v); }).catch(e => { if (live) setActivityError(e.message); });
    return () => { live = false; };
  }, [dossierId, page, reload]);
  useEffect(() => { const warn = (e: BeforeUnloadEvent) => { if (noteDirty.current) e.preventDefault(); }; window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn); }, []);
  async function reloadSource() {
    const loaded = await load(); accept(loaded);
    const latestActivities = await api<ActivityPage>(`/api/dossiers/${dossierId}/activities?page=${page}&pageSize=20`);
    setActivities(latestActivities);
    setEditor(current => current?.activity ? { ...current, activity: latestActivities.items.find(a => a.id === current.activity?.id) ?? current.activity } : current);
    if (write.current) {
      const row = loaded.rows.find(r => r.id === write.current?.id);
      if (row) write.current = { ...write.current, payload: '', expected: row.version, saved: row.version };
    }
  }
  async function saveRow(kind: 'plan' | 'actual', row: SheetActualPreview, version: number, key: string) {
    if (!dossier || !editor) throw new Error('Chưa tải được phiếu.');
    if (editor.activity) {
      await api(`/api/dossiers/${dossierId}/sheet-activities/${editor.activity.id}`, { method: 'PUT', body: JSON.stringify({ row,
        expectedVersion: dossier.version, expectedActivityVersion: editor.activity.version,
        expectedSheetVersion: editor.activity.sheetVersion ?? 0, requestKey: key }) });
    } else if (editor.transferItemId) {
      await api(`/api/dossiers/${dossierId}/sheet-items/${editor.transferItemId}/perform`, { method: 'POST',
        body: JSON.stringify({ row, expectedVersion: dossier.version, requestKey: key }) });
    } else if (editor.itemId) {
      await api(`/api/dossiers/${dossierId}/sheet-items/${editor.itemId}`, { method: 'PUT', body: JSON.stringify({ row, expectedVersion: dossier.version, requestKey: key }) });
    } else {
      const payload = JSON.stringify({ ...row, draftVersion: undefined });
      if (write.current?.payload !== payload) write.current = { id: row.id, payload, key: crypto.randomUUID(),
        expected: write.current?.id === row.id ? write.current.saved ?? write.current.expected : version };
      if (write.current.saved === undefined) {
        const saved = await api<Draft>(`/api/dossiers/${dossierId}/sheet-drafts/${row.id}`, { method: 'PUT', body: JSON.stringify({ kind: kind === 'plan' ? 'Plan' : 'Actual', row, expectedVersion: write.current.expected, requestKey: write.current.key }) });
        write.current.saved = saved.version;
      }
      await api(`/api/dossiers/${dossierId}/sheet-drafts/${row.id}/confirm-${kind}`, { method: 'POST', body: JSON.stringify({
        expectedVersion: dossier.version, expectedDraftVersion: write.current.saved, requestKey: key, decision: null,
        visitId: null, expectedVisitVersion: 0, outcome: row.note ?? '', chargingBasis: 'Separate', completeItem: false, directDecision: null,
      }) });
    }
    setEditor(null); write.current = null; refresh();
  }
  async function act(item: TreatmentItem, command: 'perform' | 'mark' | 'delete') {
    if (!dossier || actionGuard.current) return;
    actionGuard.current = true; setBusy(true); setError(undefined);
    const body = { expectedVersion: dossier.version, ...(command === 'mark' ? { completed: !item.displayCompleted } : {}) };
    const payload = JSON.stringify({ id: item.id, command, body });
    if (actionKey.current?.payload !== payload) actionKey.current = { payload, key: crypto.randomUUID() };
    try {
      await api(`/api/dossiers/${dossierId}/sheet-items/${item.id}${command === 'perform' ? '/perform' : command === 'mark' ? '/display-completion' : ''}`, {
        method: command === 'perform' ? 'POST' : command === 'mark' ? 'PATCH' : 'DELETE', body: JSON.stringify({ ...body, requestKey: actionKey.current.key }) });
      actionKey.current = null; refresh(); return true;
    } catch (e) { setError((e as Error).message); return false; }
    finally { actionGuard.current = false; setBusy(false); }
  }
  function edit(item: TreatmentItem) {
    const percent = item.discount?.type === 'Percent';
    setEditor({ kind: 'plan', itemId: item.id, row: { id: item.id, serviceId: item.serviceId, serviceName: item.serviceName,
      date: item.estimatedStart ?? undefined, regions: (item.region ?? '').split(/[,;\s]+/).filter(Boolean), quantity: item.quantity,
      dentistId: item.responsibleDentistId ?? dossier!.dentistId, dentistName: '', assistantId: item.assistantId ?? undefined,
      note: item.note ?? undefined, estimatedMinutes: item.estimatedMinutes ?? undefined, unitPriceVnd: item.agreedUnitPriceVnd,
      costVnd: Math.round(item.quantity * item.agreedUnitPriceVnd), discountType: percent ? 'Percent' : 'VND',
      discountValue: item.discount?.value ?? 0, discountNote: item.discount?.reason } });
  }
  function editActivity(activity: SheetActivity) {
    setEditor({ kind: 'actual', activity, row: { id: activity.id, serviceId: activity.serviceId,
      serviceName: activity.serviceNameSnapshot ?? '', date: new Intl.DateTimeFormat('sv-SE', {timeZone:'Asia/Ho_Chi_Minh'}).format(new Date(activity.performedAt)),
      regions: (activity.region ?? '').split(/[,;\s]+/).filter(Boolean), quantity: activity.quantity,
      dentistId: activity.performerId, dentistName: '', assistantId: activity.assistantId, note: activity.outcome,
      labWork: activity.labWorkSnapshot, costVnd: Math.round(activity.quantity * activity.priceSnapshotVnd),
      unitPriceVnd: activity.priceSnapshotVnd, discountType: activity.sheetDiscountType === 'Percent' ? 'Percent' : 'VND',
      discountValue: activity.sheetDiscountValue ?? activity.discountSnapshotVnd ?? 0, discountNote: activity.sheetDiscountReason } });
  }
  function beginTransfer(item: TreatmentItem) {
    const existing = activities?.items.find(a => a.itemId === item.id && a.sourceKind === 'Dossier');
    if (existing) { editActivity(existing); return; }
    setEditor({ kind: 'actual', transferItemId: item.id, row: { id: crypto.randomUUID(), sourceItemId: item.id,
      date: new Intl.DateTimeFormat('sv-SE', {timeZone:'Asia/Ho_Chi_Minh'}).format(new Date()), serviceId: item.serviceId,
      serviceName: item.serviceName, regions: (item.region ?? '').split(/[,;\s]+/).filter(Boolean), quantity: item.quantity,
      dentistId: item.responsibleDentistId ?? dossier!.dentistId, dentistName: '', assistantId: item.assistantId,
      note: item.note ?? undefined, costVnd: Math.round(item.quantity * item.agreedUnitPriceVnd),
      unitPriceVnd: item.agreedUnitPriceVnd, discountType: item.discount?.type === 'Percent' ? 'Percent' : 'VND',
      discountValue: item.discount?.status === 'Approved' ? item.discount.value : 0, discountNote: item.discount?.reason } });
  }
  async function deleteActivity(activity: SheetActivity) {
    if (!dossier || actionGuard.current) return false;
    actionGuard.current = true; setBusy(true); setError(undefined);
    const body = { expectedVersion: dossier.version, expectedActivityVersion: activity.version, expectedSheetVersion: activity.sheetVersion ?? 0 };
    const payload = JSON.stringify({ activityId: activity.id, body });
    if (actionKey.current?.payload !== payload) actionKey.current = { payload, key: crypto.randomUUID() };
    try {
      await api(`/api/dossiers/${dossierId}/sheet-activities/${activity.id}`, { method: 'DELETE', body: JSON.stringify({ ...body, requestKey: actionKey.current.key }) });
      actionKey.current = null; refresh(); return true;
    } catch (e) { setError((e as Error).message); return false; }
    finally { actionGuard.current = false; setBusy(false); }
  }
  async function saveNote() {
    if (!dossier || noteBusy || note === noteSaved.current) return;
    if (noteKey.current?.text !== note) noteKey.current = { text: note, key: crypto.randomUUID(), version: dossier.version };
    setNoteBusy(true); setNoteError(undefined);
    try {
      const result = await api<{ dossier: TreatmentDossier }>(`/api/dossiers/${dossierId}/note`, { method: 'PATCH', body: JSON.stringify({ note, expectedVersion: noteKey.current.version, requestKey: noteKey.current.key }) });
      noteSaved.current = note; noteDirty.current = false; noteKey.current = null; setDossier(result.dossier); onLoaded(result.dossier);
    } catch (e) { setNoteError((e as Error).message); } finally { setNoteBusy(false); }
  }
  if (!dossier || !context) return error ? <Alert type="error" message={error} action={<Button onClick={refresh}>Thử lại</Button>} /> : <Skeleton active />;
  const items = dossier.items.filter(i => !i.sheetDeleted && !i.hiddenFromSheet);
  const gross = items.filter(included).reduce((s,i) => s + Math.round(i.quantity * i.agreedUnitPriceVnd), 0);
  const discount = items.filter(included).reduce((s,i) => s + (i.discount?.status === 'Approved' ? i.discount.amountVnd : 0), 0);
  const member = (id?: number | null) => context.members.find(m => m.id === id)?.name ?? '—';
  const canWrite = context.allowedActions.includes('Draft'), finance = dossier.financialSummary;
  const closeEditor = () => { setEditor(null); write.current = null; refresh(); };
  const rowEditor = (kind: 'plan' | 'actual') => editor?.kind === kind && <TreatmentSheetDraftRow key={editor.row?.id ?? `new-${kind}`} kind={kind}
    dossier={dossier} options={options} context={context} initialPlan={kind === 'plan' ? editor.row : undefined} initialActual={kind === 'actual' ? editor.row : undefined}
    onCancel={closeEditor} onSubmit={(r,v,_values,key) => saveRow(kind,r,v,key)} onReload={reloadSource} />;
  return <div className="treatment-sheet-body">
    {error && <Alert type="error" message={error} action={<Button onClick={() => void reloadSource().catch(e => setError(e.message))}>Đọc lại phiên bản</Button>} />}
    <div className="treatment-sheet-tools"><Button size="small" onClick={() => setHistory(true)}>Lịch sử phiếu</Button>
      {dossier.sourceExaminationId && <Link to={`/visits/${dossier.sourceExaminationId}`}>Mở nguồn khám</Link>}</div>
    <h2 className="treatment-sheet-print-title">Phiếu điều trị {dossier.code}</h2>
    <section><div className="treatment-sheet-band"><h3>KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ</h3><div><span>Tổng dự toán: {money(gross)}</span><span>Giảm giá: {money(discount)}</span><strong>Thành tiền: {money(gross-discount)}</strong></div></div>
      <div className="treatment-sheet-table-scroll"><table className="treatment-sheet-table treatment-sheet-plan-table"><thead><tr>
        <th>Ngày</th><th>Răng</th><th>Điều trị/dịch vụ</th><th>Thời gian dự kiến (phút)</th><th>Ghi chú</th><th>Bác sĩ & phụ tá</th><th>Chi phí</th><th>Giảm giá</th>
        <th><button className="treatment-sheet-plus" aria-label="Thêm hạng mục kế hoạch" disabled={!canWrite || !!editor || busy} onClick={() => setEditor({ kind: 'plan' })}>+</button></th>
      </tr></thead><tbody>{items.filter(i => i.id !== editor?.itemId).map(item => <tr key={item.id} className={item.displayCompleted ? 'treatment-sheet-row-completed' : undefined}>
        <td>{item.estimatedStart ? date(item.estimatedStart) : '—'}</td><td>{item.region || '—'}</td><td>{item.serviceName}</td><td>{item.estimatedMinutes ?? '—'}</td><td>{item.note || '—'}</td>
        <td>{member(item.responsibleDentistId ?? dossier.dentistId)}<small>{member(item.assistantId)}</small></td>
        <td>{money(Math.round(item.quantity*item.agreedUnitPriceVnd))}</td><td>{item.discount?.status === 'Approved' ? money(item.discount.amountVnd) : '—'}</td>
        <td><Dropdown trigger={['click']} menu={{ items: [
          { key: 'perform', label: 'Thực hiện', disabled: !canWrite || busy || !!editor, onClick: () => beginTransfer(item) },
          { key: 'complete', label: item.displayCompleted ? 'Bỏ hoàn tất' : 'Hoàn tất', disabled: !canWrite || busy || !!editor, onClick: () => void act(item,'mark') },
          { key: 'edit', label: 'Sửa', disabled: !canWrite || busy || !!editor, onClick: () => edit(item) },
          { key: 'delete', label: 'Xóa', disabled: !canWrite || busy || !!editor, onClick: () => { setError(undefined); setDeleting(item); } },
        ] }}><button className="treatment-sheet-row-action" aria-label={`Chức năng ${item.serviceName}`}>⋯</button></Dropdown></td>
      </tr>)}{rowEditor('plan')}
      {drafts.filter(d => d.kind === 'Plan' && d.id !== editor?.row?.id).map(d => <tr key={d.id}><td>{d.row.date || '—'}</td><td>{d.row.regions?.join(', ')}</td><td>{d.row.serviceName}<small>Nháp</small></td><td>{d.row.estimatedMinutes ?? '—'}</td><td>{d.row.note}</td><td>{member(d.row.dentistId)}</td><td>{money(d.row.costVnd)}</td><td>—</td><td><Button size="small" onClick={() => setEditor({ kind:'plan',row:d.row })}>Sửa nháp</Button></td></tr>)}
      {!items.length && !editor && !drafts.some(d => d.kind === 'Plan') && <tr><td colSpan={9}>Chưa có hạng mục. Nhấn + để thêm.</td></tr>}</tbody></table></div></section>
    <section><div className="treatment-sheet-band"><h3>QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ</h3></div>
      {activityError && <Alert type="error" message={activityError} action={<Button onClick={refresh}>Thử lại</Button>} />}
      <div className="treatment-sheet-table-scroll"><table className="treatment-sheet-table treatment-sheet-actual-table"><thead><tr><th>Ngày</th><th>Răng</th><th>Điều trị/dịch vụ</th><th>Ghi chú</th><th>Bác sĩ & phụ tá</th><th>Labo</th><th>Chi phí</th><th>Giảm giá</th><th>Đã trả</th><th /></tr></thead><tbody>
        {activities?.items.filter(a => a.id !== editor?.activity?.id).map(a => <tr key={a.id}><td>{date(a.performedAt)}</td><td>{a.region || '—'}</td><td>{a.serviceNameSnapshot ?? dossier.items.find(i => i.id === a.itemId)?.serviceName ?? context.services.find(s => s.id === a.serviceId)?.name ?? 'Dịch vụ đã lưu'}</td><td>{a.outcome || '—'}</td>
          <td>{member(a.performerId)}<small>{member(a.assistantId)}</small></td><td>{a.labWorkSnapshot || '—'}</td><td>{money(Math.round(a.quantity*a.priceSnapshotVnd))}<small>{fees[a.feeStatus] ?? a.feeStatus}</small></td><td>{money(a.discountSnapshotVnd ?? 0)}</td>
          <td>{finance?.available && finance.lines?.some(l => l.activityId === a.id) ? money(finance.lines.filter(l => l.activityId === a.id).reduce((s,l) => s+l.confirmedAllocatedVnd,0)) : 'Chưa có dữ liệu'}</td>
          <td><div className="treatment-sheet-edit-actions"><Button size="small" title="Sửa" aria-label={`Sửa quá trình ${a.serviceNameSnapshot ?? 'điều trị'}`} disabled={!canWrite || busy || !!editor} onClick={() => editActivity(a)}>✎</Button>
            <Button size="small" title="Xóa" aria-label={`Xóa quá trình ${a.serviceNameSnapshot ?? 'điều trị'}`} disabled={!canWrite || busy || !!editor} onClick={() => { setError(undefined); setDeletingActivity(a); }}>×</Button></div></td></tr>)}
        {activities?.total === 0 && <tr><td colSpan={10}>Phiếu chưa có việc điều trị.</td></tr>}
        {!activities && !activityError && <tr><td colSpan={10}><Skeleton active paragraph={{rows:1}} /></td></tr>}
        {drafts.filter(d => d.kind === 'Actual' && d.id !== editor?.row?.id).map(d => <tr key={d.id}><td>{d.row.date || '—'}</td><td>{d.row.regions?.join(', ')}</td><td>{d.row.serviceName}<small>Nháp</small></td><td>{d.row.note}</td><td>{member(d.row.dentistId)}</td><td>{d.row.labWork}</td><td>{money(d.row.costVnd)}</td><td>—</td><td>—</td><td><Button size="small" onClick={() => setEditor({kind:'actual',row:d.row})}>Sửa nháp</Button></td></tr>)}
        {rowEditor('actual')}
      </tbody></table></div><Button className="treatment-sheet-create" aria-label="Thêm quá trình điều trị" disabled={!canWrite || !!editor || busy} onClick={() => setEditor({kind:'actual'})}>+ Thêm việc đã làm</Button>
      {(activities?.total ?? 0) > 20 && <Pagination current={page} total={activities?.total} pageSize={20} onChange={setPage} />}
      <div className="treatment-sheet-payment"><p><strong>Phí đã ghi nhận:</strong> {finance?.available && finance.postedChargeVnd != null ? money(finance.postedChargeVnd) : 'Chưa có dữ liệu đối soát'}</p>
        <p><strong>Đã trả:</strong> {finance?.available && finance.allocatedVnd != null ? money(finance.allocatedVnd) : 'Chưa có dữ liệu đối soát'}</p>
        <p><strong>Còn nợ:</strong> {finance?.available && finance.outstandingVnd != null ? money(finance.outstandingVnd) : 'Chưa có dữ liệu đối soát'}</p>
        <DossierFinancialPanel dossierId={dossierId} available={finance?.available ?? false} onChanged={refresh} /></div></section>
    <section className="treatment-sheet-images"><h3 className="treatment-sheet-minor-heading">HÌNH ẢNH</h3><Link to={`/patients/${patientId}/documents?dossierId=${dossierId}`} className="treatment-sheet-image-tile" aria-label="Mở hình ảnh và tài liệu của phiếu"><svg viewBox="0 0 64 72" fill="none" aria-hidden="true"><path d="M10 4h30l15 16v48H10V4Z" stroke="currentColor" strokeWidth="4"/><path d="M39 4v18h16M17 57l12-15 9 10 7-8 9 13" stroke="currentColor" strokeWidth="4"/><circle cx="25" cy="31" r="5" fill="currentColor"/></svg></Link></section>
    <section className="treatment-sheet-notes"><h3 className="treatment-sheet-minor-heading">GHI CHÚ</h3><Input.TextArea aria-label="Ghi chú hồ sơ" value={note} maxLength={4000} disabled={!canWrite || noteBusy}
      onChange={e => { setNote(e.target.value); noteDirty.current = e.target.value !== noteSaved.current; }} /><Button size="small" loading={noteBusy} disabled={!canWrite || note === noteSaved.current} onClick={() => void saveNote()}>Lưu ghi chú</Button>{noteError && <Alert type="error" message={noteError} />}</section>
    <Modal open={!!deleting} title="Bạn có muốn xóa dòng này không?" okText="Đồng ý" cancelText="Không đồng ý" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{ disabled: busy }} onCancel={() => setDeleting(null)} onOk={() => { if (deleting) void act(deleting,'delete').then(ok => { if (ok) setDeleting(null); }); }}>
      <p>{deleting?.serviceName}</p>{error && <Alert type="error" message={error} />}</Modal>
    <Modal open={history} title="Lịch sử phiếu" footer={null} onCancel={() => setHistory(false)}><ul>{dossier.timeline.map(e => <li key={e.id}>{new Date(e.occurredAt).toLocaleString('vi-VN')} — {e.kind}<details><summary>Chi tiết</summary><pre style={{whiteSpace:'pre-wrap'}}>{e.payloadJson}</pre></details></li>)}</ul></Modal>
    <Modal open={!!deletingActivity} title="Bạn có muốn xóa dòng này không?" okText="Đồng ý" cancelText="Không đồng ý" confirmLoading={busy}
      closable={!busy} cancelButtonProps={{disabled:busy}} onCancel={() => setDeletingActivity(null)} onOk={() => { if (deletingActivity) void deleteActivity(deletingActivity).then(ok => { if (ok) setDeletingActivity(null); }); }}>
      <p>{deletingActivity?.serviceNameSnapshot}</p>{error && <Alert type="error" message={error} />}</Modal>
  </div>;
}
