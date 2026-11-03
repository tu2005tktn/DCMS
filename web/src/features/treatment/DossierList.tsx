import { Alert, Button, Dropdown, Empty, Modal, Select, Skeleton, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';

import { api, type ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { AppointmentOptions, DossierListItem, DossierPage, TreatmentChange, TreatmentDossier } from './types';
import TreatmentSheet from './TreatmentSheet';
import DossierClosureModal from './DossierClosureModal';
import FinancialPatientDossiers from './FinancialPatientDossiers';


const displayTime = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

export default function DossierList({ patientId, visitId, sourceVisitVersion, branchId: contextBranchId,
  patientName, patientCode, patientAge }: { patientId: number; visitId?: string; diagnosis?: string | null;
  branchId?: number; dentistId?: number; sourceVisitVersion?: number; patientName?: string; patientCode?: string; patientAge?: number | null }) {
  const auth = useAuth();

  const [page, setPage] = useState<DossierPage | null>(null);
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [optionsError, setOptionsError] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [selected, setSelected] = useState<TreatmentDossier | null>(null);

  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DossierListItem | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const deleteKey = useRef(crypto.randomUUID());
  const [statusError, setStatusError] = useState<{ id: string; message: string } | null>(null);
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null);
  const [clinicalTarget, setClinicalTarget] = useState<{ dossier: TreatmentDossier; completed: boolean } | null>(null);
  const [createContext,setCreateContext] = useState<{dentistId:number;branches:{id:number;name:string}[]} | null>(null);
  const [branchId,setBranchId] = useState<number>();
  const [unsaved,setUnsaved] = useState(false);
  const [contextRetry,setContextRetry]=useState(0);
  const created = useRef<DossierListItem | null>(null);
  const sending = useRef(false);
  const createPayload = useRef('');
  const createKey = useRef(crypto.randomUUID());

  const canUse = auth.user?.effectiveRoles.includes('Dentist') ?? false;

  useEffect(() => {
    if (!canUse) return;
    let active = true;
    setActiveId(created.current?.patientId===patientId && pageNumber===1 ? created.current.id : null); setSelected(null);
    if(!(created.current?.patientId===patientId && pageNumber===1))setPage(null); setError(null);
    api<DossierPage>(`/api/patients/${patientId}/dossiers?page=${pageNumber}&pageSize=20`)
      .then(result => { if (active) { const pinned = created.current?.patientId === patientId ? created.current : null; const items = pinned && pageNumber === 1 && !result.items.some(x=>x.id===pinned.id) ? [pinned,...result.items] : result.items; setPage({...result,items}); setActiveId(pinned && pageNumber === 1 ? pinned.id : result.items[0]?.id ?? null); } })
      .catch(failure => { if (active) setError(failure as ApiError); });
    return () => { active = false; };
  }, [patientId, pageNumber, revision, canUse]);
  useEffect(() => {
    if (!canUse) return;
    let active = true;
    setOptionsError(null);
    api<AppointmentOptions>('/api/appointments/options')
      .then(value => { if (active) setOptions(value); })
      .catch(failure => { if (active) setOptionsError((failure as Error).message); });
    return () => { active = false; };
  }, [canUse, revision]);

  useEffect(() => {
    if (!canUse) return; let active=true; setCreateContext(null);
    void api<{dentistId:number;branches:{id:number;name:string}[]}>(`/api/patients/${patientId}/dossiers/create-context`).then(value=>{
      if (active) { setCreateContext(value); setBranchId(previous => value.branches.some(b=>b.id===previous) ? previous : contextBranchId && value.branches.some(b=>b.id===contextBranchId) ? contextBranchId : value.branches.length===1 ? value.branches[0].id : undefined); }
    }).catch(e=>{if(active)setSaveError(e.message);});
    return ()=>{active=false;};
  },[patientId,canUse,contextBranchId,contextRetry]);
  async function create() {
    if (sending.current || !branchId) return;
    if (unsaved && !window.confirm('Bỏ các thay đổi chưa lưu để tạo phiếu mới?')) return;
    sending.current=true; setBusy(true); setSaveError(null);
    const body={branchId,...(visitId && sourceVisitVersion ? {sourceVisitId:visitId,expectedSourceVisitVersion:sourceVisitVersion}:{})};
    const payload=JSON.stringify(body); if(createPayload.current!==payload){createPayload.current=payload;createKey.current=crypto.randomUUID();}
    try {
      const result=await api<TreatmentChange>(`/api/patients/${patientId}/dossiers/quick`,{method:'POST',body:JSON.stringify({...body,requestKey:createKey.current})});
      created.current={...result.dossier}; setPage(current=>({items:[created.current!,...(current?.items ?? []).filter(x=>x.id!==result.dossier.id)],total:(current?.total ?? 0)+1,page:1,pageSize:20})); setUnsaved(false);setSelected(result.dossier);setActiveId(result.dossier.id);setPageNumber(1);setRevision(v=>v+1);
      createKey.current=crypto.randomUUID();createPayload.current='';
    } catch(e){setSaveError((e as Error).message);}finally{sending.current=false;setBusy(false);}
  }
  useEffect(()=>{if(activeId && created.current?.id===activeId && page?.items.some(x=>x.id===activeId))document.getElementById(`treatment-sheet-${activeId}`)?.scrollIntoView({behavior:'smooth',block:'start'});},[activeId,page]);
  async function deleteDossier() {
    if (!deleteTarget || deleteBusy) return;
    setDeleteBusy(true); setDeleteError(null);
    try {
      await api<TreatmentChange>(`/api/dossiers/${deleteTarget.id}`, {
        method: 'DELETE', body: JSON.stringify({ expectedVersion: deleteTarget.version,
          requestKey: deleteKey.current }),
      });
      if(created.current?.id===deleteTarget.id)created.current=null;
      setDeleteTarget(null); setSelected(null); setRevision(value => value + 1);
      deleteKey.current = crypto.randomUUID();
    } catch (failure) { setDeleteError((failure as Error).message); }
    finally { setDeleteBusy(false); }
  }

  async function changeStatus(item: DossierListItem, completed: boolean) {
    if ((item.state === 'Open' && !completed) || statusBusyId) return;
    setStatusBusyId(item.id); setStatusError(null);
    try { const value = await api<TreatmentDossier>(`/api/dossiers/${item.id}`); setClinicalTarget({ dossier: value, completed }); }
    catch (failure) { setStatusError({ id: item.id, message: (failure as Error).message }); }
    finally { setStatusBusyId(null); }
  }

  if (!canUse) return <FinancialPatientDossiers patientId={patientId} />;
  return <section className="treatment-list treatment-sheets" aria-label="Các phiếu điều trị">
    <div className="treatment-sheets-heading"><div>
      <Typography.Title level={4}>DS PHIẾU ĐIỀU TRỊ/DỊCH VỤ</Typography.Title>
      <Typography.Paragraph>{patientName || 'Bệnh nhân'} (MSKH: {patientCode || `#${patientId}`},
        {' '}{patientAge !== null && patientAge !== undefined ? `${patientAge} tuổi` : 'chưa rõ tuổi'})</Typography.Paragraph>
    </div>
    </div>
    {error && <Alert type="error" showIcon message={error.message}
      action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
      }{!page ? (error ? null : <Skeleton active />) : page.items.length === 0
        ? <Empty description="Chưa có phiếu điều trị. Bác sĩ có thể tạo phiếu mới cho bệnh nhân này." />
        : <><div className="treatment-list-cards">{page.items.map(item => <article key={item.id}
          className={`treatment-sheet-card${activeId === item.id ? ' is-open' : ''}`}>
          <div className="treatment-sheet-card-top">
            <span className="treatment-sheet-created"><strong>Ngày lập phiếu</strong>{' '}
              {activeId === item.id && selected?.id === item.id ? displayTime(selected.createdAt) : `Phiếu ${item.code}`}</span>
            <div className="treatment-sheet-card-actions">
              <Dropdown trigger={['click']} menu={{ items: [
                { key: 'pending', label: 'Chưa xong', onClick: () => void changeStatus(item, false) },
                { key: 'complete', label: 'Hoàn tất', onClick: () => void changeStatus(item, true) },
              ] }}><button type="button"
                className={`treatment-sheet-state ${item.state === 'Completed' ? 'is-complete' : 'is-pending'}`}
                aria-label={`Trạng thái phiếu: ${item.state === 'Completed' ? 'Hoàn tất' : item.state === 'Stopped' ? 'Đã ngừng' : 'Chưa xong'}`}
                aria-busy={statusBusyId === item.id} disabled={statusBusyId === item.id}
                title="Chọn trạng thái phiếu">
                {item.state === 'Completed' ? 'Hoàn tất' : item.state === 'Stopped' ? 'Đã ngừng' : 'Chưa xong'} <span aria-hidden="true">⌄</span>
              </button></Dropdown>
              <Button aria-label={`Xóa phiếu ${item.code}`} onClick={() => {
                setDeleteTarget(item); setDeleteError(null); deleteKey.current = crypto.randomUUID();
              }}>
                <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden="true">
                  <path d="M3 4h10M6 4V2.5h4V4M5 5.5l.5 8h5l.5-8M7 7v4.5M9 7v4.5" />
                </svg></Button>
              <Button aria-label="In phiếu đang mở" onClick={() => window.print()} disabled={activeId !== item.id || selected?.id !== item.id}>
                <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden="true">
                  <path d="M4 5V2h8v3M4 11H2V6h12v5h-2M4 9h8v5H4zM11.5 7.5h.5" />
                </svg></Button>
              <button type="button" className="treatment-sheet-toggle" aria-label={`${activeId === item.id ? 'Thu gọn' : 'Mở'} phiếu ${item.code}`}
                aria-expanded={activeId === item.id} aria-controls={`treatment-sheet-${item.id}`}
                onClick={() => { setActiveId(value => value === item.id ? null : item.id); setSelected(null); }}>
                {activeId === item.id ? '−' : '+'}
              </button>
            </div>
          </div>
          {statusError?.id === item.id && <Alert type="error" showIcon message={statusError.message}
            action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Tải lại</Button>} />}
          {activeId === item.id && <div id={`treatment-sheet-${item.id}`}>
            <TreatmentSheet patientId={patientId} dossierId={item.id} dossierVersion={item.version} visitId={visitId}
              options={options} onUnsavedChange={setUnsaved} onLoaded={setSelected} />
          </div>}
        </article>)}</div>
        {page.total > page.page * page.pageSize && <Button onClick={() => setPageNumber(value => value + 1)}>Xem thêm phiếu</Button>}
        {page.page > 1 && <Button onClick={() => setPageNumber(value => value - 1)}>Trang trước</Button>}</>}
    <div className="treatment-sheet-create-row">
      <label>Chi nhánh <Select aria-label="Chi nhánh tạo phiếu" style={{minWidth:200}} value={branchId} disabled={busy || !!contextBranchId} placeholder="Chọn chi nhánh" options={createContext?.branches.map(x=>({value:x.id,label:x.name})) ?? []} onChange={setBranchId}/></label>
      <Button className="treatment-sheet-create" aria-label="Tạo phiếu mới" loading={busy} disabled={!branchId || !createContext?.branches.length} onClick={()=>void create()}>+ TẠO PHIẾU MỚI</Button>
      {saveError && <Alert type="error" message={saveError} action={<Button onClick={()=>createContext && branchId ? void create() : setContextRetry(v=>v+1)}>Thử lại</Button>}/>}
      {optionsError && <Alert type="error" message={optionsError}/>}
    </div>
    {clinicalTarget && <DossierClosureModal dossier={clinicalTarget.dossier} completed={clinicalTarget.completed}
      onClose={() => setClinicalTarget(null)} onSaved={() => { setClinicalTarget(null); setRevision(v => v + 1); }} />}
    <Modal open={!!deleteTarget} title={`Xóa phiếu ${deleteTarget?.code ?? ''}?`}
      okText="Xóa phiếu" okButtonProps={{ danger: true, loading: deleteBusy }}
      onOk={() => void deleteDossier()} onCancel={() => { if (!deleteBusy) setDeleteTarget(null); }}>
      <Typography.Paragraph>Chỉ xóa được phiếu còn trống, chưa có kế hoạch hoặc dữ liệu điều trị. Phiếu đã có nghiệp vụ cần dùng quy trình đóng hồ sơ.</Typography.Paragraph>
      {deleteError && <Alert type="error" showIcon message={deleteError} />}
    </Modal>

  </section>;
}
