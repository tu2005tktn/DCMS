import { Alert, Button, Dropdown, Empty, Form, Input, Modal, Select, Skeleton, Typography } from 'antd';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { AppointmentOptions, DossierListItem, DossierPage, TreatmentChange, TreatmentDossier } from './types';
import { money } from './types';
import TreatmentSheet from './TreatmentSheet';

type Values = { branchId: number; dentistId: number; diagnosis: string; note?: string };
const displayTime = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

export default function DossierList({ patientId, visitId, diagnosis, branchId: contextBranchId, dentistId,
  patientName, patientCode, patientAge }: { patientId: number; visitId?: string; diagnosis?: string | null;
  branchId?: number; dentistId?: number; patientName?: string; patientCode?: string; patientAge?: number | null }) {
  const auth = useAuth();
  const navigate = useNavigate();
  const [page, setPage] = useState<DossierPage | null>(null);
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [optionsError, setOptionsError] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [selected, setSelected] = useState<TreatmentDossier | null>(null);
  const [visiblePlanTotal, setVisiblePlanTotal] = useState<{ dossierId: string; amount: number } | null>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DossierListItem | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const deleteKey = useRef(crypto.randomUUID());
  const [statusError, setStatusError] = useState<{ id: string; message: string } | null>(null);
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null);
  const statusRequest = useRef<{ id: string; completed: boolean; key: string } | null>(null);
  const [form] = Form.useForm<Values>();
  const createKey = useRef(crypto.randomUUID());
  const branchId = Form.useWatch('branchId', form);
  const canUse = auth.user?.effectiveRoles.includes('Dentist') ?? false;
  const onPlanTotalChange = useCallback((dossierId: string, amount: number) => {
    setVisiblePlanTotal(current => current?.dossierId === dossierId && current.amount === amount
      ? current : { dossierId, amount });
  }, []);

  useEffect(() => {
    if (!canUse) return;
    let active = true;
    setActiveId(null); setSelected(null);
    setPage(null); setError(null);
    api<DossierPage>(`/api/patients/${patientId}/dossiers?page=${pageNumber}&pageSize=20`)
      .then(result => { if (active) { setPage(result); setActiveId(result.items[0]?.id ?? null); } })
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

  async function create(values: Values) {
    if (busy) return;
    setBusy(true); setSaveError(null);
    try {
      const result = await api<TreatmentChange>(`/api/patients/${patientId}/dossiers`, {
        method: 'POST', body: JSON.stringify({ ...values, requestKey: createKey.current }),
      });
      setOpen(false); form.resetFields(); createKey.current = crypto.randomUUID();
      navigate(`/patients/${patientId}/dossiers/${result.dossier.id}${visitId ? `?visitId=${visitId}` : ''}`);
    } catch (failure) { setSaveError((failure as Error).message); }
    finally { setBusy(false); }
  }

  async function deleteDossier() {
    if (!deleteTarget || deleteBusy) return;
    setDeleteBusy(true); setDeleteError(null);
    try {
      await api<TreatmentChange>(`/api/dossiers/${deleteTarget.id}`, {
        method: 'DELETE', body: JSON.stringify({ expectedVersion: deleteTarget.version,
          requestKey: deleteKey.current }),
      });
      setDeleteTarget(null); setSelected(null); setRevision(value => value + 1);
      deleteKey.current = crypto.randomUUID();
    } catch (failure) { setDeleteError((failure as Error).message); }
    finally { setDeleteBusy(false); }
  }

  async function changeStatus(item: DossierListItem, completed: boolean) {
    if (item.displayCompleted === completed || statusBusyId) return;
    if (statusRequest.current?.id !== item.id || statusRequest.current.completed !== completed)
      statusRequest.current = { id: item.id, completed, key: crypto.randomUUID() };
    setStatusBusyId(item.id); setStatusError(null);
    try {
      const result = await api<TreatmentChange>(`/api/dossiers/${item.id}/display-status`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: item.version, completed,
          requestKey: statusRequest.current.key }),
      });
      setPage(current => current && ({ ...current, items: current.items.map(row => row.id === item.id
        ? { ...row, displayCompleted: result.dossier.displayCompleted, version: result.dossier.version }
        : row) }));
      setSelected(current => current?.id === item.id ? result.dossier : current);
      statusRequest.current = null;
    } catch (failure) { setStatusError({ id: item.id, message: (failure as Error).message }); }
    finally { setStatusBusyId(null); }
  }

  if (!canUse) return <Alert type="info" showIcon message="Hồ sơ điều trị chỉ dành cho bác sĩ được phân quyền." />;
  const planned = selected && visiblePlanTotal?.dossierId === selected.id ? visiblePlanTotal.amount
    : selected?.financialSummary?.plannedVnd ?? selected?.items.filter(item => item.decision === 'Agreed')
    .reduce((sum, item) => sum + Math.round(item.quantity * item.agreedUnitPriceVnd)
      - (item.discount?.status === 'Approved' ? item.discount.amountVnd : 0), 0);
  return <section className="treatment-list treatment-sheets" aria-label="Các phiếu điều trị">
    <div className="treatment-sheets-heading"><div>
      <Typography.Title level={4}>DS PHIẾU ĐIỀU TRỊ/DỊCH VỤ</Typography.Title>
      <Typography.Paragraph>{patientName || 'Bệnh nhân'} (MSKH: {patientCode || `#${patientId}`},
        {' '}{patientAge !== null && patientAge !== undefined ? `${patientAge} tuổi` : 'chưa rõ tuổi'})</Typography.Paragraph>
    </div>
    <div className="treatment-sheets-summary" aria-label="Tóm tắt phiếu đang mở">
      <div><strong>Tổng cộng{selected ? ' (dự kiến)' : ''}:</strong> <span>{planned === undefined ? '—' : money(planned)}</span></div>
      <div><strong>Đã trả:</strong> <span>{selected?.financialSummary?.available && selected.financialSummary.receivedVnd !== null
        && selected.financialSummary.receivedVnd !== undefined ? money(selected.financialSummary.receivedVnd) : 'Chưa có dữ liệu đối soát'}</span></div>
      <div><strong>Còn lại:</strong> <span>{selected?.financialSummary?.available && selected.financialSummary.outstandingVnd !== null
        && selected.financialSummary.outstandingVnd !== undefined ? money(selected.financialSummary.outstandingVnd) : 'Chưa có dữ liệu đối soát'}</span></div>
    </div></div>
    {error ? <Alert type="error" showIcon message={error.message}
      action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
      : !page ? <Skeleton active /> : page.items.length === 0
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
                className={`treatment-sheet-state ${item.displayCompleted ? 'is-complete' : 'is-pending'}`}
                aria-label={`Trạng thái phiếu: ${item.displayCompleted ? 'Hoàn tất' : 'Chưa xong'}`}
                aria-busy={statusBusyId === item.id} disabled={statusBusyId === item.id}
                title="Chọn trạng thái phiếu">
                {item.displayCompleted ? 'Hoàn tất' : 'Chưa xong'} <span aria-hidden="true">⌄</span>
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
              options={options} onLoaded={setSelected} onPlanTotalChange={onPlanTotalChange} />
          </div>}
        </article>)}</div>
        {page.total > page.page * page.pageSize && <Button onClick={() => setPageNumber(value => value + 1)}>Xem thêm phiếu</Button>}
        {page.page > 1 && <Button onClick={() => setPageNumber(value => value - 1)}>Trang trước</Button>}</>}
    <div className="treatment-sheet-create-row"><Button className="treatment-sheet-create" onClick={() => { setSaveError(null); createKey.current = crypto.randomUUID(); form.setFieldsValue({ diagnosis: diagnosis ?? undefined, branchId: contextBranchId, dentistId }); setOpen(true); }}>+ TẠO PHIẾU MỚI</Button></div>
    <Modal open={!!deleteTarget} title={`Xóa phiếu ${deleteTarget?.code ?? ''}?`}
      okText="Xóa phiếu" okButtonProps={{ danger: true, loading: deleteBusy }}
      onOk={() => void deleteDossier()} onCancel={() => { if (!deleteBusy) setDeleteTarget(null); }}>
      <Typography.Paragraph>Chỉ xóa được phiếu còn trống, chưa có kế hoạch hoặc dữ liệu điều trị. Phiếu đã có nghiệp vụ cần dùng quy trình đóng hồ sơ.</Typography.Paragraph>
      {deleteError && <Alert type="error" showIcon message={deleteError} />}
    </Modal>
    <Modal open={open} title="Tạo đợt điều trị" okText="Tạo hồ sơ" okButtonProps={{ loading: busy }}
      onOk={() => form.submit()} onCancel={() => { if (!busy) { setOpen(false); createKey.current = crypto.randomUUID(); } }}>
      <Typography.Paragraph>Đợt điều trị có thể kéo dài nhiều buổi. Tạo hồ sơ chưa tạo lịch hẹn hoặc khoản thu.</Typography.Paragraph>
      {optionsError && <Alert type="error" showIcon message={`Không tải được chi nhánh và bác sĩ: ${optionsError}`}
        action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
      {saveError && <Alert type="error" showIcon className="form-alert" message={saveError} />}
      <Form form={form} layout="vertical" onValuesChange={() => { createKey.current = crypto.randomUUID(); }}
        onFinish={values => void create(values)}>
        <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh.' }]}>
          <Select options={options?.branches.map(x => ({ value: x.id, label: x.name })) ?? []} />
        </Form.Item>
        <Form.Item name="dentistId" label="Bác sĩ phụ trách" rules={[{ required: true, message: 'Chọn bác sĩ.' }]}>
          <Select disabled={!branchId} options={options?.dentists.filter(x => x.branchId === branchId)
            .map(x => ({ value: x.id, label: x.name })) ?? []} />
        </Form.Item>
        <Form.Item name="diagnosis" label="Chẩn đoán hoặc lý do điều trị" rules={[
          { required: true, whitespace: true, message: 'Nhập chẩn đoán hoặc lý do.' },
          { max: 4000, message: 'Không quá 4000 ký tự.' },
        ]}><Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Form.Item name="note" label="Ghi chú" rules={[{ max: 4000, message: 'Không quá 4000 ký tự.' }]}>
          <Input.TextArea rows={2} maxLength={4000} /></Form.Item>
      </Form>
    </Modal>
  </section>;
}
