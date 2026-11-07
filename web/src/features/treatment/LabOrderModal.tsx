import { Alert, AutoComplete, Button, Checkbox, Input, InputNumber, Modal, Select, Skeleton } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { clinicToday } from '../appointments/types';
import './labOrder.css';

// Phiếu labo của một dòng quá trình điều trị. Quy tắc: docs/specs/LAB-ORDER.md.
export type LabOrderSummary = { activityId: string; workName: string; dueOn: string | null; deliveredOn: string | null };
type Order = { activityId: string; code: string | null; orderedOn: string; dueOn: string | null; deliveredOn: string | null; dentistId: number; labProviderId: string | null; labWorkTypeId: string | null;
  workName: string; marginOuter: string | null; marginInner: string | null; marginSide: string | null; bridgeShapes: string[]; isChild: boolean; teeth: string[]; shade: Record<string, string>;
  note: string | null; material: string | null; quantity: number; totalFeeVnd: number; paidVnd: number; warranty: string | null; version: number };
type Context = { order: Order | null; patient: { name: string; code: string; age: number | null; gender: string | null };
  options: { labs: { id: string; name: string }[]; workTypes: { id: string; name: string; providerId: string | null; referencePrice: number | null; warranty: string | null }[];
    dentists: { id: number; name: string }[]; defaultDentistId: number | null } };
type Form = { code: string; orderedOn: string; dueOn: string; deliveredOn: string; dentistId?: number; labProviderId?: string; labWorkTypeId?: string; workName: string;
  marginOuter?: string; marginInner?: string; marginSide?: string; bridgeShapes: string[]; isChild: boolean; teeth: string[]; shade: Record<string, string>;
  note: string; material: string; quantity: number; totalFeeVnd: number; paidVnd: number; warranty: string };

const margins = ['Vai', 'Vát', 'Lưỡi dao', 'Mỏng dần'].map(value => ({ value, label: value }));
const shapes = [['saddle', 'Hình yên ngựa'], ['semi-saddle', 'Hình bán yên ngựa'], ['ovoid', 'Hình trứng'], ['t-ridge', 'Hình gờ T']] as const;
const materials = ['Sứ kim loại', 'Titanium', 'Zirconia', 'Emax', 'Nhựa', 'Khung kim loại'].map(value => ({ value }));
const range = (from: number, to: number) => Array.from({ length: Math.abs(to - from) + 1 }, (_v, i) => from < to ? from + i : from - i);
// Răng theo số FDI. Hàng trên: phần tư 1 (từ xa về gần) rồi phần tư 2; hàng dưới: phần tư 4 rồi 3.
function teethRows(child: boolean) {
  const [a, b, c, d] = child ? [5, 6, 8, 7] : [1, 2, 4, 3], last = child ? 5 : 8;
  return { upper: [...range(last, 1).map(n => `${a}${n}`), ...range(1, last).map(n => `${b}${n}`)], lower: [...range(last, 1).map(n => `${c}${n}`), ...range(1, last).map(n => `${d}${n}`)] };
}
const dots = (v: string | number | undefined) => `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
const undot = (v: string | undefined) => Number((v ?? '').replace(/\./g, '')) || 0;
const zones = ['1', '2', '3', '4', '5', '6'];
const blank = (ctx: Context): Form => ({ code: '', orderedOn: clinicToday(), dueOn: '', deliveredOn: '', dentistId: ctx.options.defaultDentistId ?? undefined, workName: '', bridgeShapes: [],
  isChild: false, teeth: [], shade: {}, note: '', material: '', quantity: 1, totalFeeVnd: 0, paidVnd: 0, warranty: '' });
const fromOrder = (o: Order): Form => ({ code: o.code ?? '', orderedOn: o.orderedOn, dueOn: o.dueOn ?? '', deliveredOn: o.deliveredOn ?? '', dentistId: o.dentistId, labProviderId: o.labProviderId ?? undefined,
  labWorkTypeId: o.labWorkTypeId ?? undefined, workName: o.workName, marginOuter: o.marginOuter ?? undefined, marginInner: o.marginInner ?? undefined, marginSide: o.marginSide ?? undefined,
  bridgeShapes: o.bridgeShapes, isChild: o.isChild, teeth: o.teeth, shade: o.shade, note: o.note ?? '', material: o.material ?? '', quantity: o.quantity, totalFeeVnd: o.totalFeeVnd,
  paidVnd: o.paidVnd, warranty: o.warranty ?? '' });
const gender = (g: string | null) => g === 'Male' ? 'Nam' : g === 'Female' ? 'Nữ' : g ? 'Khác' : '—';

export default function LabOrderModal({ dossierId, activityId, serviceName, canEdit, onClose, onSaved }: {
  dossierId: string; activityId: string; serviceName: string; canEdit: boolean; onClose: () => void; onSaved: () => void;
}) {
  const url = `/api/dossiers/${dossierId}/sheet-activities/${activityId}/lab-order`;
  const [ctx, setCtx] = useState<Context>(), [form, setForm] = useState<Form>(), [initial, setInitial] = useState(''), [loadError, setLoadError] = useState<string>();
  const [busy, setBusy] = useState(false), [error, setError] = useState<string>(), [attempt, setAttempt] = useState(0), [submitted, setSubmitted] = useState(false);
  useEffect(() => {
    let current = true; setCtx(undefined); setLoadError(undefined);
    api<Context>(url).then(c => { if (!current) return; const f = c.order ? fromOrder(c.order) : blank(c); setCtx(c); setForm(f); setInitial(JSON.stringify(f)); })
      .catch((e: unknown) => { if (current) setLoadError(e instanceof Error ? e.message : 'Không tải được phiếu labo.'); });
    return () => { current = false; };
  }, [url, attempt]);
  const dirty = !!form && JSON.stringify(form) !== initial;
  const rows = useMemo(() => teethRows(form?.isChild ?? false), [form?.isChild]);
  function set(patch: Partial<Form>) { setForm(f => f && { ...f, ...patch }); }
  const problems = useMemo(() => {
    const p: Record<string, string> = {};
    if (!form) return p;
    if (!form.orderedOn) p.orderedOn = 'Vui lòng nhập Ngày.';
    if (form.dueOn && form.dueOn < form.orderedOn) p.dueOn = 'Hẹn không được trước Ngày.';
    if (form.deliveredOn && form.deliveredOn < form.orderedOn) p.deliveredOn = 'Giao không được trước Ngày.';
    if (!form.dentistId) p.dentistId = 'Vui lòng chọn Bác sĩ.';
    if (!form.workName.trim()) p.workName = 'Vui lòng nhập Công việc labo.';
    if (form.paidVnd > form.totalFeeVnd) p.paidVnd = 'Đã trả không được lớn hơn Tổng phí.';
    return p;
  }, [form]);
  function pickWork(id: string | undefined) {
    const type = ctx?.options.workTypes.find(t => t.id === id);
    if (!type) { set({ labWorkTypeId: undefined }); return; }
    setForm(f => f && { ...f, labWorkTypeId: type.id, workName: type.name, labProviderId: type.providerId && ctx?.options.labs.some(l => l.id === type.providerId) ? type.providerId : f.labProviderId,
      totalFeeVnd: type.referencePrice != null ? type.referencePrice * f.quantity : f.totalFeeVnd, warranty: type.warranty ?? f.warranty });
  }
  function toggle(list: string[], value: string) { return list.includes(value) ? list.filter(x => x !== value) : [...list, value]; }
  function close() {
    if (!dirty || busy) { if (!busy) onClose(); return; }
    Modal.confirm({ title: 'Phiếu labo có thay đổi chưa lưu. Đóng không lưu?', okText: 'Đóng không lưu', cancelText: 'Ở lại', onOk: onClose });
  }
  async function save() {
    setSubmitted(true); setError(undefined);
    if (!form || Object.keys(problems).length) return;
    setBusy(true);
    try {
      const shade = Object.fromEntries(Object.entries(form.shade).filter(([, v]) => v.trim()));
      await api(url, { method: 'PUT', body: JSON.stringify({ code: form.code || null, orderedOn: form.orderedOn, dueOn: form.dueOn || null, deliveredOn: form.deliveredOn || null,
        dentistId: form.dentistId, labProviderId: form.labProviderId ?? null, labWorkTypeId: form.labWorkTypeId ?? null, workName: form.workName, marginOuter: form.marginOuter ?? null,
        marginInner: form.marginInner ?? null, marginSide: form.marginSide ?? null, bridgeShapes: form.bridgeShapes, isChild: form.isChild, teeth: form.teeth, shade, note: form.note || null,
        material: form.material || null, quantity: form.quantity, totalFeeVnd: form.totalFeeVnd, paidVnd: form.paidVnd, warranty: form.warranty || null, expectedVersion: ctx?.order?.version ?? null }) });
      onSaved();
    } catch (e) { setError(e instanceof ApiError || e instanceof Error ? e.message : 'Không lưu được phiếu labo.'); }
    finally { setBusy(false); }
  }
  async function remove() {
    setBusy(true); setError(undefined);
    try { await api(url, { method: 'DELETE' }); onSaved(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Không xóa được phiếu labo.'); }
    finally { setBusy(false); }
  }
  const lock = !canEdit || busy;
  const err = (key: string) => submitted && problems[key] ? <div className="lab-order-error" role="alert">{problems[key]}</div> : null;
  const field = (label: string, node: React.ReactNode, key?: string, cls?: string) => <div className={`lab-order-field ${cls ?? ''}`}><label>{label}</label><div>{node}{key && err(key)}</div></div>;
  return <Modal open width={960} className="lab-order-modal" title={null} footer={null} closable={false} maskClosable={false} onCancel={close} destroyOnClose>
    {loadError ? <Alert type="error" showIcon message={loadError} action={<Button size="small" onClick={() => setAttempt(a => a + 1)}>Thử lại</Button>} />
      : !ctx || !form ? <Skeleton active /> : <div className="lab-order-form">
      <div className="lab-order-actions lab-order-noprint">
        <Button onClick={() => window.print()}>IN</Button>
        {canEdit && ctx.order && <Button danger disabled={busy} onClick={() => Modal.confirm({ title: 'Xóa phiếu labo này?', okText: 'Xóa', okButtonProps: { danger: true }, cancelText: 'Không', onOk: remove })}>XÓA</Button>}
        {canEdit && <Button type="primary" loading={busy} onClick={() => void save()}>LƯU LẠI</Button>}
        <Button aria-label="Đóng" onClick={close}>✕</Button>
      </div>
      <div className="lab-order-head">
        <h2>PHIẾU LABO</h2>
        <div className="lab-order-head-fields">
          <label>Mã số<Input aria-label="Mã số" maxLength={40} disabled={lock} value={form.code} onChange={e => set({ code: e.target.value })} /></label>
          <label>Ngày<Input type="date" aria-label="Ngày" disabled={lock} value={form.orderedOn} onChange={e => set({ orderedOn: e.target.value })} />{err('orderedOn')}</label>
          <label>Hẹn<Input type="date" aria-label="Hẹn" disabled={lock} value={form.dueOn} onChange={e => set({ dueOn: e.target.value })} />{err('dueOn')}</label>
          <label>Giao<Input type="date" aria-label="Giao" disabled={lock} value={form.deliveredOn} onChange={e => set({ deliveredOn: e.target.value })} />{err('deliveredOn')}</label>
        </div>
      </div>
      {error && <Alert className="lab-order-noprint" type="error" showIcon message={error} />}
      {field('Khách hàng', <><strong>{ctx.patient.name}</strong> <em>(MSKH: {ctx.patient.code}, Tuổi: {ctx.patient.age ?? '—'}, Giới tính: {gender(ctx.patient.gender)})</em></>)}
      <div className="lab-order-pair">
        {field('Bác sĩ', <Select aria-label="Bác sĩ" disabled={lock} style={{ width: '100%' }} value={form.dentistId} onChange={dentistId => set({ dentistId })} options={ctx.options.dentists.map(d => ({ value: d.id, label: d.name }))} />, 'dentistId')}
        {field('Cty cung cấp', <Select aria-label="Cty cung cấp" allowClear disabled={lock} style={{ width: '100%' }} value={form.labProviderId} onChange={labProviderId => set({ labProviderId })} options={ctx.options.labs.map(l => ({ value: l.id, label: l.name }))} />)}
      </div>
      {field('Công việc labo', <>
        <Select aria-label="Loại công việc labo" allowClear showSearch optionFilterProp="label" placeholder={`Chọn loại công việc (dịch vụ: ${serviceName})`} disabled={lock} style={{ width: '100%', marginBottom: 6 }}
          value={form.labWorkTypeId} onChange={pickWork} options={ctx.options.workTypes.map(t => ({ value: t.id, label: t.name }))} />
        <Input aria-label="Công việc labo" maxLength={160} disabled={lock} placeholder="Tên công việc labo" value={form.workName} onChange={e => set({ workName: e.target.value })} /></>, 'workName')}
      {field('Đường hoàn tất', <div className="lab-order-margins">{([['Mặt ngoài', 'marginOuter'], ['Mặt trong', 'marginInner'], ['Mặt bên', 'marginSide']] as const).map(([label, key]) =>
        <label key={key}><em>{label}</em><Select aria-label={label} allowClear disabled={lock} style={{ width: 130 }} value={form[key]} onChange={v => set({ [key]: v })} options={margins} /></label>)}</div>)}
      {field('Nhịp cầu răng', <div className="lab-order-shapes">{shapes.map(([value, label]) =>
        <Checkbox key={value} disabled={lock} checked={form.bridgeShapes.includes(value)} onChange={() => set({ bridgeShapes: toggle(form.bridgeShapes, value) })}>{label}</Checkbox>)}</div>)}
      <div className="lab-order-colour-teeth">
        {field('Màu', <div className="lab-order-shade" role="group" aria-label="Màu răng">{zones.map(z =>
          <input key={z} aria-label={`Màu vùng ${z}`} maxLength={20} disabled={lock} value={form.shade[z] ?? ''} placeholder={z} onChange={e => set({ shade: { ...form.shade, [z]: e.target.value } })} />)}</div>)}
        {field('Răng', <div className="lab-order-teeth">
          <Checkbox disabled={lock} checked={form.isChild} onChange={e => set({ isChild: e.target.checked, teeth: [] })}>Trẻ em</Checkbox>
          {(['upper', 'lower'] as const).map(row => <div key={row} className="lab-order-teeth-row" role="group" aria-label={row === 'upper' ? 'Hàm trên' : 'Hàm dưới'}>{rows[row].map(t =>
            <button type="button" key={t} aria-label={`Răng ${t}`} aria-pressed={form.teeth.includes(t)} disabled={lock} className={form.teeth.includes(t) ? 'on' : ''}
              onClick={() => set({ teeth: toggle(form.teeth, t) })}>{t}</button>)}</div>)}
          <div className="lab-order-teeth-legend"><span>Phải</span><span>Hàm trên / Hàm dưới</span><span>Trái</span></div></div>)}
      </div>
      {field('Ghi chú', <Input.TextArea aria-label="Ghi chú labo" rows={2} maxLength={1000} disabled={lock} value={form.note} onChange={e => set({ note: e.target.value })} />)}
      <div className="lab-order-money">
        <label>Chất liệu<AutoComplete aria-label="Chất liệu" disabled={lock} style={{ width: 150 }} options={materials} value={form.material} onChange={material => set({ material })} /></label>
        <label>Số lượng<InputNumber aria-label="Số lượng" min={1} max={999} precision={0} disabled={lock} value={form.quantity} onChange={v => set({ quantity: v ?? 1 })} /></label>
        <label>Tổng phí (₫)<InputNumber aria-label="Tổng phí" min={0} precision={0} disabled={lock} style={{ width: 140 }} formatter={dots} parser={undot} value={form.totalFeeVnd} onChange={v => set({ totalFeeVnd: v ?? 0 })} />{err('paidVnd')}</label>
        <label>Đã trả (₫)<InputNumber aria-label="Đã trả" min={0} precision={0} disabled={lock} style={{ width: 140 }} formatter={dots} parser={undot} value={form.paidVnd} onChange={v => set({ paidVnd: v ?? 0 })} /></label>
      </div>
      {field('Bảo hành', <Input aria-label="Bảo hành" maxLength={100} disabled={lock} placeholder="Ví dụ: Trọn đời, 7 năm, 24 tháng..." value={form.warranty} onChange={e => set({ warranty: e.target.value })} />)}
    </div>}
  </Modal>;
}
