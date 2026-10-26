import { Alert, Button, Dropdown, Input, Skeleton, Tooltip } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import type { AppointmentOptions, TreatmentDossier, TreatmentItem } from './types';
import { decisionLabel, money } from './types';
import TreatmentSheetDraftRow, { type SheetActualPreview, type SheetPlanPreview } from './TreatmentSheetDraftForms';

type Activity = {
  id: string; dossierId: string | null; itemId: string | null; serviceId: number;
  region: string | null; quantity: number; performedAt: string; outcome: string;
  state: 'Draft' | 'Posted'; priceSnapshotVnd: number;
};
type VisitContext = { visit: { patientId: number }; activities: Activity[] };
type SheetDraft = { id: string; kind: 'Plan' | 'Actual'; visitId: string | null;
  row: SheetPlanPreview | SheetActualPreview; version: number };

const displayTime = (value: string | null) => value ? new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value)) : 'Chưa dự kiến';
const displayDate = (value: string, includeYear = true) => new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit', month: '2-digit', ...(includeYear ? { year: 'numeric' as const } : {}),
  timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

function unavailable(label: string, reason: string) {
  return <Tooltip title={reason}><Button disabled aria-label={`${label}: ${reason}`}>{label}</Button></Tooltip>;
}

function actionForItem(patientId: number, dossier: TreatmentDossier, item: TreatmentItem) {
  return dossier.planState === 'Committed' && dossier.state === 'Open'
    ? `/patients/${patientId}/dossiers/${dossier.id}/plan/revise/${item.id}?from=plan`
    : `/patients/${patientId}/dossiers/${dossier.id}/plan?item=${item.id}`;
}

function planTotals(dossier: TreatmentDossier, previews: SheetPlanPreview[]) {
  const items = dossier.items.filter(item => item.decision !== 'Refused');
  const gross = items.reduce((sum, item) => sum + Math.round(item.quantity * item.agreedUnitPriceVnd), 0)
    + previews.reduce((sum, preview) => sum + preview.costVnd, 0);
  const discount = items.reduce((sum, item) => sum
    + (item.discount?.status === 'Approved' ? item.discount.amountVnd : 0), 0)
    + previews.reduce((sum, preview) => sum + (preview.discountVnd ?? 0), 0);
  return { gross, discount, planned: gross - discount };
}

export default function TreatmentSheet({ patientId, dossierId, dossierVersion, visitId, options, onLoaded, onPlanTotalChange }: {
  patientId: number; dossierId: string; dossierVersion: number; visitId?: string;
  options: AppointmentOptions | null;
  onLoaded: (value: TreatmentDossier | null) => void;
  onPlanTotalChange: (dossierId: string, total: number) => void;
}) {
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [reload, setReload] = useState(0);
  const [context, setContext] = useState<VisitContext | null>(null);
  const [activityError, setActivityError] = useState<ApiError | null>(null);
  const [activityReload, setActivityReload] = useState(0);
  const [planEditing, setPlanEditing] = useState(false);
  const [editingPlanPreview, setEditingPlanPreview] = useState<SheetPlanPreview | null>(null);
  const [actualEditing, setActualEditing] = useState(false);
  const [editingActualPreview, setEditingActualPreview] = useState<SheetActualPreview | null>(null);
  const [actualSourceItem, setActualSourceItem] = useState<TreatmentItem | null>(null);
  const [actualSourcePreview, setActualSourcePreview] = useState<SheetPlanPreview | null>(null);
  const actualSectionRef = useRef<HTMLElement>(null);
  const [planPreviews, setPlanPreviews] = useState<SheetPlanPreview[]>([]);
  const [actualPreviews, setActualPreviews] = useState<SheetActualPreview[]>([]);
  const [draftError, setDraftError] = useState<string | null>(null);
  const [noteText, setNoteText] = useState('');
  const [noteBusy, setNoteBusy] = useState(false);
  const [noteError, setNoteError] = useState<string | null>(null);
  const [noteBlocked, setNoteBlocked] = useState(false);
  const savedNote = useRef('');
  const noteRequest = useRef<{ text: string; key: string } | null>(null);

  useEffect(() => {
    let live = true;
    setDossier(null); setError(null); onLoaded(null);
    api<TreatmentDossier>(`/api/dossiers/${dossierId}`).then(value => {
      if (!live) return;
      if (value.patientId !== patientId) {
        setError(new ApiError(403, 'ACCESS_DENIED', 'Phiếu điều trị không thuộc bệnh nhân này.'));
        return;
      }
      setDossier(value); onLoaded(value);
      const previousSaved = savedNote.current;
      savedNote.current = value.note ?? '';
      setNoteText(current => current === previousSaved ? savedNote.current : current);
    }).catch(failure => { if (live) setError(failure as ApiError); });
    return () => { live = false; };
  }, [dossierId, dossierVersion, patientId, reload]);

  useEffect(() => {
    if (!dossier || noteBusy || noteBlocked || noteText === savedNote.current) return;
    const timeout = window.setTimeout(async () => {
      const text = noteText;
      if (!noteRequest.current || noteRequest.current.text !== text)
        noteRequest.current = { text, key: crypto.randomUUID() };
      setNoteBusy(true); setNoteError(null);
      try {
        const result = await api<{ dossier: TreatmentDossier }>(`/api/dossiers/${dossier.id}/note`, {
          method: 'PATCH', body: JSON.stringify({ note: text, expectedVersion: dossier.version,
            requestKey: noteRequest.current.key }),
        });
        savedNote.current = text;
        noteRequest.current = null;
        setDossier(result.dossier); onLoaded(result.dossier);
      } catch (failure) {
        setNoteError((failure as Error).message);
        setNoteBlocked(true);
      } finally { setNoteBusy(false); }
    }, 700);
    return () => window.clearTimeout(timeout);
  }, [dossier, noteText, noteBusy, noteBlocked, onLoaded]);

  useEffect(() => {
    if (!visitId) return;
    let live = true;
    setContext(null); setActivityError(null);
    api<VisitContext>(`/api/visits/${visitId}/treatment-context`).then(value => {
      if (live) setContext(value);
    }).catch(failure => { if (live) setActivityError(failure as ApiError); });
    return () => { live = false; };
  }, [visitId, activityReload]);

  useEffect(() => {
    let live = true;
    setPlanPreviews([]); setActualPreviews([]); setDraftError(null);
    api<SheetDraft[]>(`/api/dossiers/${dossierId}/sheet-drafts`).then(rows => {
      if (!live) return;
      setPlanPreviews(rows.filter(row => row.kind === 'Plan').map(row =>
        ({ ...row.row, id: row.id, draftVersion: row.version } as SheetPlanPreview)));
      setActualPreviews(rows.filter(row => row.kind === 'Actual' && row.visitId === visitId).map(row =>
        ({ ...row.row, id: row.id, draftVersion: row.version } as SheetActualPreview)));
    }).catch(failure => { if (live) setDraftError((failure as Error).message); });
    return () => { live = false; };
  }, [dossierId, visitId]);

  useEffect(() => {
    if (actualEditing) actualSectionRef.current?.scrollIntoView({ block: 'nearest' });
  }, [actualEditing]);

  useEffect(() => {
    if (dossier) onPlanTotalChange(dossier.id, planTotals(dossier, planPreviews).planned);
  }, [dossier, planPreviews, onPlanTotalChange]);

  if (error) return <Alert type="error" showIcon message={error.message}
    action={<Button onClick={() => setReload(value => value + 1)}>Thử lại</Button>} />;
  if (!dossier) return <Skeleton active paragraph={{ rows: 4 }} />;
  const dentistName = options?.dentists.find(x => x.id === dossier.dentistId)?.name ?? `#${dossier.dentistId}`;
  const { gross, discount, planned } = planTotals(dossier, planPreviews);
  const activities = context?.visit.patientId === patientId
    ? context.activities.filter(value => value.dossierId === dossier.id && value.state === 'Posted') : [];

  const received = dossier.financialSummary?.available && dossier.financialSummary.receivedVnd !== null
    && dossier.financialSummary.receivedVnd !== undefined ? money(dossier.financialSummary.receivedVnd) : 'Chưa có dữ liệu đối soát';
  const outstanding = dossier.financialSummary?.available && dossier.financialSummary.outstandingVnd !== null
    && dossier.financialSummary.outstandingVnd !== undefined ? money(dossier.financialSummary.outstandingVnd) : 'Chưa có dữ liệu đối soát';
  const activityTotal = activities.reduce((sum, activity) => sum + activity.priceSnapshotVnd, 0);

  async function saveSheetRow(kind: 'Plan' | 'Actual', row: SheetPlanPreview | SheetActualPreview,
    expectedVersion: number, requestKey: string) {
    if (kind === 'Actual' && !visitId) throw new Error('Cần mở phiếu từ lượt khám để lưu nháp quá trình điều trị.');
    const saved = await api<SheetDraft>(`/api/dossiers/${dossierId}/sheet-drafts/${row.id}`, {
      method: 'PUT', body: JSON.stringify({ kind, visitId: kind === 'Actual' ? visitId : null,
        row, expectedVersion, requestKey }),
    });
    if (kind === 'Plan') setPlanPreviews(values => values.some(value => value.id === row.id)
      ? values.map(value => value.id === row.id ? { ...row, draftVersion: saved.version } as SheetPlanPreview : value)
      : [...values, { ...row, draftVersion: saved.version } as SheetPlanPreview]);
    else setActualPreviews(values => values.some(value => value.id === row.id)
      ? values.map(value => value.id === row.id ? { ...row, draftVersion: saved.version } as SheetActualPreview : value)
      : [...values, { ...row, draftVersion: saved.version } as SheetActualPreview]);
    setDraftError(null);
    return saved.version;
  }

  async function removeSheetRow(kind: 'Plan' | 'Actual', row: SheetPlanPreview | SheetActualPreview) {
    if (!window.confirm('Xóa hàng nháp đã lưu?')) return;
    if (row.draftVersion) {
      try {
        await api<void>(`/api/dossiers/${dossierId}/sheet-drafts/${row.id}?expectedVersion=${row.draftVersion}`,
          { method: 'DELETE' });
      } catch (failure) { setDraftError((failure as Error).message); return; }
    }
    if (kind === 'Plan') setPlanPreviews(values => values.filter(value => value.id !== row.id));
    else setActualPreviews(values => values.filter(value => value.id !== row.id));
  }

  return <div className="treatment-sheet-body">
    {draftError && <Alert type="error" showIcon message={`Không tải/lưu được hàng nháp: ${draftError}`} />}
    <h2 className="treatment-sheet-print-title">Phiếu điều trị {dossier.code}</h2>
    <section aria-labelledby={`sheet-plan-${dossier.id}`}>
      <div className="treatment-sheet-band"><h3 id={`sheet-plan-${dossier.id}`}>KẾ HOẠCH ĐIỀU TRỊ/DỊCH VỤ</h3>
        <div><span>Tổng dự toán: {money(gross)}</span><span>Giảm giá: {money(discount)}</span><strong>Thành tiền: {money(planned)}</strong></div></div>
      <p className="treatment-sheet-scroll-hint">Vuốt bảng sang ngang để xem đủ cột.</p>
      <div className="treatment-sheet-table-scroll"><table className="treatment-sheet-table treatment-sheet-plan-table">
        <thead><tr><th>Ngày</th><th>Răng</th><th>SL</th><th>Điều trị/dịch vụ</th><th>Ghi chú</th>
          <th>Bác sĩ & phụ tá</th><th>Chi phí</th><th>Giảm giá</th>
          <th><button type="button" className="treatment-sheet-plus" aria-label="Thêm hạng mục kế hoạch"
            title="Thêm hạng mục kế hoạch" onClick={() => {
              const active = sessionStorage.getItem(`dcms.sheet-active.${dossier.id}.plan.new`);
              setEditingPlanPreview(planPreviews.find(row => row.id === active) ?? null);
              setPlanEditing(true);
            }}
            disabled={planEditing}>+</button></th></tr></thead>
        <tbody>{dossier.items.length ? dossier.items.map(item => <tr key={item.id}>
          <td>{item.estimatedStart ? <Tooltip title={displayTime(item.estimatedStart)}>
            <span>{displayDate(item.estimatedStart, false)}</span></Tooltip> : '—'}</td><td>{item.region || '—'}</td>
          <td>{item.quantity}</td><td><Tooltip title={decisionLabel[item.decision]}><strong>{item.serviceName}</strong></Tooltip></td>
          <td>{item.material ? `Vật liệu: ${item.material}` : '—'}</td><td>{dentistName}</td>
          <td className="treatment-sheet-money">{money(Math.round(item.quantity * item.agreedUnitPriceVnd))}</td>
          <td className="treatment-sheet-money">{item.discount?.status === 'Approved' ? `−${money(item.discount.amountVnd)}`
            : item.discount?.status === 'Proposed' ? 'Chờ duyệt' : '—'}</td>
          <td><Dropdown trigger={['click']} overlayClassName="treatment-sheet-action-menu" menu={{ items: [
            { key: 'perform', disabled: actualEditing,
              onClick: () => { setActualSourceItem(item); setActualSourcePreview(null); setActualEditing(true); },
              label: <span title="Mở dòng nhập việc điều trị; chưa ghi nhận thực tế">
                <span className="treatment-sheet-menu-symbol is-green">▶</span> Thực hiện</span> },
            { key: 'complete', disabled: true,
              label: <span title="Cần bằng chứng việc đã làm và xác nhận của bác sĩ">
                <span className="treatment-sheet-menu-symbol is-blue">✓</span> Hoàn tất</span> },
            { key: 'edit', label: <Link to={actionForItem(patientId, dossier, item)}>
              <span className="treatment-sheet-menu-symbol is-blue">✎</span> Sửa</Link> },
            { key: 'delete', disabled: true,
              label: <span title="Chưa có lệnh xóa hạng mục an toàn cho kế hoạch này">
                <span className="treatment-sheet-menu-symbol is-red">▤</span> Xóa</span> },
          ] }}>
            <button type="button" className="treatment-sheet-row-action"
              aria-label={`Chức năng ${item.serviceName}`} title="Chức năng">⋯</button>
          </Dropdown></td>
        </tr>) : planPreviews.length === 0 && <tr><td colSpan={9} className="treatment-sheet-empty">Chưa có hạng mục kế hoạch.</td></tr>}
          {planPreviews.map(preview => <tr key={preview.id} className="treatment-sheet-preview-row">
            <td>{preview.date}</td><td>{preview.regions.join(', ')}</td><td>{preview.quantity}</td>
            <td><strong>{preview.serviceName}</strong><small>Đã lưu nháp</small></td>
            <td>{preview.note || '—'}</td><td>{preview.dentistName}<small>Phụ tá: {preview.assistantName || '—'}</small></td>
            <td className="treatment-sheet-money">{money(preview.costVnd)}</td>
            <td className="treatment-sheet-money">{preview.discountVnd ? `−${money(preview.discountVnd)}` : '—'}
              {preview.discountType === 'Percent' && <small>{preview.discountValue}%</small>}
              {preview.discountNote && <small>{preview.discountNote}</small>}</td>
            <td><Dropdown trigger={['click']} overlayClassName="treatment-sheet-action-menu" menu={{ items: [
              { key: 'perform', disabled: actualEditing,
                onClick: () => { setActualSourceItem(null); setActualSourcePreview(preview); setActualEditing(true); },
                label: <span title="Chuyển nội dung sang dòng nhập quá trình; kế hoạch này chưa được lưu">
                <span className="treatment-sheet-menu-symbol is-green">▶</span> Thực hiện</span> },
              { key: 'complete', disabled: true, label: <span title="Hạng mục chưa được lưu">
                <span className="treatment-sheet-menu-symbol is-blue">✓</span> Hoàn tất</span> },
              { key: 'edit', onClick: () => { setEditingPlanPreview(preview); setPlanEditing(true); },
                label: <span><span className="treatment-sheet-menu-symbol is-blue">✎</span> Sửa</span> },
              { key: 'delete', onClick: () => { void removeSheetRow('Plan', preview);
                if (editingPlanPreview?.id === preview.id) { setPlanEditing(false); setEditingPlanPreview(null); } },
                label: <span><span className="treatment-sheet-menu-symbol is-red">▤</span> Xóa</span> },
            ] }}><button type="button" className="treatment-sheet-row-action"
              aria-label={`Chức năng bản xem trước ${preview.serviceName}`} title="Chức năng">⋯</button></Dropdown></td>
          </tr>)}
          {planEditing && <TreatmentSheetDraftRow key={editingPlanPreview?.id ?? 'new'} kind="plan" dossier={dossier} options={options}
            initialPlan={editingPlanPreview}
            onCancel={() => { setPlanEditing(false); setEditingPlanPreview(null); }}
            onPlanPreview={(value, expectedVersion, requestKey) =>
              saveSheetRow('Plan', value, expectedVersion, requestKey)}
            onActualPreview={async () => 0} />}</tbody>
      </table></div>
    </section>

    <section ref={actualSectionRef} aria-labelledby={`sheet-actual-${dossier.id}`}>
      <div className="treatment-sheet-band"><h3 id={`sheet-actual-${dossier.id}`}>QUÁ TRÌNH ĐIỀU TRỊ/DỊCH VỤ</h3>
        {actualSourceItem && <span>{`Đang nhập từ kế hoạch: ${actualSourceItem.serviceName} · ${actualSourceItem.decision === 'Agreed' && dossier.planState === 'Committed' ? 'Chưa ghi nhận' : 'Kế hoạch chưa chốt, chưa ghi nhận'}`}</span>}
        {actualSourcePreview && <span>{`Đang nhập từ kế hoạch chưa lưu: ${actualSourcePreview.serviceName} · Chưa ghi nhận`}</span>}</div>
      <p className="treatment-sheet-scroll-hint">Vuốt bảng sang ngang để xem đủ cột.</p>
      <div className="treatment-sheet-table-scroll"><table className="treatment-sheet-table treatment-sheet-actual-table">
        <thead><tr><th>Ngày</th><th>Răng</th><th>SL</th><th>Điều trị/dịch vụ</th><th>Ghi chú</th>
          <th>Bác sĩ & phụ tá</th><th>Công việc labo</th><th>Chi phí</th><th>Giảm giá</th><th aria-label="Thao tác"><span aria-hidden="true">⚙</span></th></tr></thead>
        <tbody>{!visitId ? <tr><td colSpan={10} className="treatment-sheet-empty">Chưa có nguồn tổng hợp việc đã làm của toàn đợt. Mở phiếu từ một lượt khám để xem hoạt động của lượt đó.</td></tr>
          : activityError ? <tr><td colSpan={10} className="treatment-sheet-empty">{activityError.message}{' '}
              <Button size="small" onClick={() => setActivityReload(value => value + 1)}>Thử lại</Button></td></tr>
            : !context ? <tr><td colSpan={10}><Skeleton active paragraph={{ rows: 1 }} /></td></tr>
              : context.visit.patientId !== patientId ? <tr><td colSpan={10} className="treatment-sheet-empty">Lượt khám không thuộc bệnh nhân này.</td></tr>
                : activities.length === 0 ? <tr><td colSpan={10} className="treatment-sheet-empty">Lượt khám này chưa ghi việc điều trị cho phiếu.</td></tr>
                  : activities.map(activity => <tr key={activity.id}>
                    <td>{displayDate(activity.performedAt)}</td><td>{activity.region || '—'}</td><td>{activity.quantity}</td>
                    <td>{dossier.items.find(item => item.id === activity.itemId)?.serviceName ?? `Dịch vụ #${activity.serviceId}`}</td>
                    <td>{activity.outcome || '—'}</td><td>{dentistName}</td><td>—</td>
                    <td className="treatment-sheet-money"><Tooltip title="Giá tham chiếu của việc đã làm">
                      <span>{money(activity.priceSnapshotVnd)}</span></Tooltip></td>
                    <td>—</td><td><div className="treatment-sheet-actual-actions">
                      <Link className="treatment-sheet-row-action" aria-label="Mở việc điều trị"
                        to={`/patients/${patientId}/visits/${visitId}/treatment?dossierId=${dossier.id}`}>✎</Link>
                      <Tooltip title="Chưa hỗ trợ xóa việc đã ghi"><Button size="small" disabled aria-label="Xóa việc điều trị">×</Button></Tooltip>
                    </div></td>
                  </tr>)}
          {actualPreviews.map(preview => <tr key={preview.id} className="treatment-sheet-preview-row">
            <td>{preview.date}</td><td>{preview.regions.join(', ')}</td><td>{preview.quantity}</td>
            <td><strong>{preview.serviceName}</strong><small>Đã lưu nháp · Chưa ghi nhận</small></td>
            <td>{preview.note}</td><td>{preview.dentistName}<small>Phụ tá: {preview.assistantName || '—'}</small></td>
            <td>{preview.labWork || '—'}{preview.labSupplier && <small>{preview.labSupplier}</small>}</td>
            <td className="treatment-sheet-money">{money(preview.costVnd)}</td>
            <td className="treatment-sheet-money">{preview.discountVnd ? `−${money(preview.discountVnd)}` : '—'}
              {preview.discountType === 'Percent' && <small>{preview.discountValue}%</small>}
              {preview.discountNote && <small>{preview.discountNote}</small>}</td>
            <td><div className="treatment-sheet-actual-actions">
              <Button size="small" aria-label={`Sửa nháp ${preview.serviceName}`}
                onClick={() => { setEditingActualPreview(preview); setActualEditing(true); }}>✎</Button>
              <Button size="small" aria-label={`Xóa nháp ${preview.serviceName}`}
                onClick={() => void removeSheetRow('Actual', preview)}>×</Button></div></td>
          </tr>)}
          {actualEditing && <TreatmentSheetDraftRow key={editingActualPreview?.id ?? actualSourceItem?.id ?? actualSourcePreview?.id ?? 'new-actual'} kind="actual"
            dossier={dossier} options={options} initialActual={editingActualPreview}
            sourceItem={actualSourceItem} sourcePlanPreview={actualSourcePreview}
            onCancel={() => { setActualEditing(false); setEditingActualPreview(null);
              setActualSourceItem(null); setActualSourcePreview(null); }}
            onPlanPreview={async () => 0}
            onActualPreview={(value, expectedVersion, requestKey) =>
              saveSheetRow('Actual', value, expectedVersion, requestKey)} />}</tbody>
        <tfoot><tr><td colSpan={7}><div className="treatment-sheet-footer-label">
          <button type="button" className="treatment-sheet-plus treatment-sheet-actual-plus"
            onClick={() => { const active = sessionStorage.getItem(`dcms.sheet-active.${dossier.id}.actual.new`);
              setEditingActualPreview(actualPreviews.find(row => row.id === active) ?? null);
              setActualSourceItem(null); setActualSourcePreview(null); setActualEditing(true); }}
            disabled={actualEditing} aria-label="Thêm quá trình điều trị"
            title="Thêm quá trình điều trị">+</button><strong>Tổng</strong></div></td>
          <td className="treatment-sheet-money"><strong>{activities.length ? money(activityTotal) : '—'}</strong></td>
          <td>—</td><td /></tr></tfoot>
      </table></div>
      <div className="treatment-sheet-payment"><div><strong>Đã trả</strong> <span>{received}</span>
        {unavailable('LỊCH SỬ', 'Chưa có API lịch sử thanh toán cho phiếu.')}</div>
        <div><strong>Còn lại</strong> <span>{outstanding}</span>
          {unavailable('TRẢ TIỀN...', 'Chưa có API thanh toán được kết nối với phiếu điều trị.')}</div></div>
    </section>

    <section className="treatment-sheet-images"><h3 className="treatment-sheet-minor-heading">HÌNH ẢNH</h3>
      <Link className="treatment-sheet-image-tile" to={`/patients/${patientId}/documents`}
        aria-label="Mở hình ảnh và tài liệu của bệnh nhân" title="Mở tài liệu của bệnh nhân">
        <svg viewBox="0 0 64 64" aria-hidden="true"><path d="M15 4h26l12 12v44H15zM41 4v13h12M21 49l10-12 7 7 5-6 7 11z" fill="none" stroke="currentColor" strokeWidth="3" strokeLinejoin="round"/><circle cx="28" cy="27" r="4" fill="currentColor"/></svg>
        <span className="treatment-sheet-visually-hidden">Xem tài liệu</span></Link>
    </section>

    <section className="treatment-sheet-notes"><h3 className="treatment-sheet-minor-heading">GHI CHÚ</h3>
      <div><label htmlFor={`dossier-note-${dossier.id}`}><strong>Ghi chú hồ sơ</strong></label>
        <Input.TextArea id={`dossier-note-${dossier.id}`} aria-label="Ghi chú hồ sơ"
          value={noteText} maxLength={4000} rows={3} placeholder="Nhập ghi chú cho phiếu điều trị"
          onChange={event => { setNoteText(event.target.value); setNoteError(null); setNoteBlocked(false); }} />
        <span className="treatment-sheet-note-hint" role="status">{noteError
          ? <>Chưa lưu được: {noteError} <Button size="small" onClick={() => { noteRequest.current = null; setNoteBlocked(false); }}>Thử lại</Button></>
          : noteBusy ? 'Đang lưu ghi chú…' : noteText === savedNote.current ? 'Đã lưu ghi chú' : 'Sẽ tự lưu sau khi ngừng nhập'}</span></div>
    </section>
  </div>;
}
