import { Button, Form, Input, InputNumber, Select, Tooltip } from 'antd';
import { ToolOutlined } from '@ant-design/icons';
import MaterialNotePicker from './MaterialNotePicker';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import type { AppointmentOptions, TreatmentDossier, TreatmentItem } from './types';
import { isFdi, type SheetContext } from './sheetTypes';

export type SheetPlanPreview = {
  id: string; draftVersion?: number; date?: string; serviceId: number; dentistId: number;
  serviceName: string; regions: string[]; quantity: number; unitPriceVnd?: number; estimatedMinutes?: number;
  note?: string; dentistName: string; assistantName?: string; assistantId?: number;
  costVnd: number; discountVnd?: number; discountType: 'VND' | 'Percent'; discountValue: number; discountNote?: string;
};
export type SheetActualPreview = SheetPlanPreview & { sourceItemId?: string; labWork?: string; labSupplier?: string };
type Values = Omit<SheetActualPreview, 'id' | 'regions'> & { toothNumbers?: string };

export default function TreatmentSheetDraftRow({ kind, dossier, options, context, initialPlan, initialActual,
  sourceItem, onCancel, onSubmit, onReload }: {
  kind: 'plan' | 'actual'; dossier: TreatmentDossier; options: AppointmentOptions | null; context?: SheetContext | null;
  initialPlan?: SheetPlanPreview | null; initialActual?: SheetActualPreview | null; sourceItem?: TreatmentItem | null;
  onCancel: () => void; onSubmit: (row: SheetActualPreview, version: number, values: Record<string, unknown>, key: string) => Promise<void>;
  onReload: () => Promise<void>;
}) {
  const [form] = Form.useForm<Values>();
  const [materialPicker, setMaterialPicker] = useState(false);
  const initial = initialPlan ?? initialActual;
  const id = useRef(initial?.id ?? crypto.randomUUID());
  const pending = useRef<{ payload: string; key: string } | null>(null), guard = useRef(false);
  const [busy, setBusy] = useState(false), [dirty, setDirty] = useState(false), [error, setError] = useState<string>();
  const catalog = context?.services ?? options?.services.filter(s => s.branchId === dossier.branchId) ?? [];
  const services = initial && !catalog.some(s => s.id === initial.serviceId)
    ? [...catalog, { id: initial.serviceId, branchId: dossier.branchId, name: initial.serviceName }] : catalog;
  const members = context?.members ?? [];
  const dentists = members.filter(m => m.canDentist).map(m => ({ value: m.id, label: m.name }));
  const assistants = members.filter(m => !m.canDentist).map(m => ({ value: m.id, label: m.name }));
  const quantity = initial?.quantity ?? sourceItem?.quantity ?? 1;
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => { if (dirty) e.preventDefault(); };
    window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);
  async function submit() {
    if (guard.current) return;
    guard.current = true;
    try {
      const values = await form.validateFields();
      if (values.estimatedMinutes != null && (!Number.isInteger(values.estimatedMinutes) || values.estimatedMinutes < 1 || values.estimatedMinutes > 480)) {
        form.setFields([{ name: 'estimatedMinutes', errors: ['Nhập số phút nguyên từ 1 đến 480.'] }]); return;
      }
      const regions = [...new Set((values.toothNumbers ?? '').split(/[,;\s]+/).filter(Boolean))];
      const row: SheetActualPreview = { id: id.current, date: values.date || undefined,
        regions, quantity, serviceId: values.serviceId, dentistId: values.dentistId, assistantId: values.assistantId,
        estimatedMinutes: values.estimatedMinutes, note: values.note, costVnd: values.costVnd,
        unitPriceVnd: values.costVnd / quantity, discountValue: values.discountValue ?? 0,
        discountType: values.discountType ?? 'VND', discountNote: values.discountNote, labWork: values.labWork,
        serviceName: services.find(s => s.id === values.serviceId)?.name ?? '',
        dentistName: dentists.find(m => m.value === values.dentistId)?.label ?? '',
        sourceItemId: initialActual?.sourceItemId };
      const payload = JSON.stringify(row);
      if (pending.current?.payload !== payload) pending.current = { payload, key: crypto.randomUUID() };
      setBusy(true); setError(undefined);
      await onSubmit(row, initial?.draftVersion ?? 0, {}, pending.current.key); setDirty(false);
    } catch (e) { if (e instanceof Error) setError(e.message); }
    finally { guard.current = false; setBusy(false); }
  }
  const field = (name: keyof Values, child: ReactNode, required = false) => <Form.Item name={name} className="treatment-sheet-cell-field"
    rules={required ? [{ required: true, message: 'Vui lòng nhập trường này.' }] : []}>{child}</Form.Item>;
  return <Form form={form} component={false} disabled={busy} initialValues={{ dentistId: dossier.dentistId,
    costVnd: 0, discountValue: 0, discountType: 'VND', ...initial, toothNumbers: initial?.regions.join(', ') ?? '' }}
    onValuesChange={changed => {
      if ('serviceId' in changed) {
        const service = context?.services.find(s => s.id === changed.serviceId);
        if (service?.referenceUnitPriceVnd != null) form.setFieldValue('costVnd', Math.round(quantity * service.referenceUnitPriceVnd));
      }
      if ('estimatedMinutes' in changed) form.setFields([{ name: 'estimatedMinutes', errors: [] }]);
      setDirty(true); setError(undefined);
    }}><tr className="treatment-sheet-edit-row" aria-label={kind === 'plan' ? 'Nhập hạng mục kế hoạch' : 'Nhập quá trình điều trị'}>
    <td>{field('date', <Input type="date" size="small" aria-label={kind === 'plan' ? 'Ngày dự kiến' : 'Ngày thực hiện'} />)}</td>
    <td><Form.Item name="toothNumbers" className="treatment-sheet-cell-field" rules={[{ validator: (_, v?: string) =>
      !v || v.split(/[,;\s]+/).filter(Boolean).every(isFdi) ? Promise.resolve() : Promise.reject(new Error('Nhập mã FDI, ví dụ 25, 26, 46.')) }]}>
      <Input size="small" aria-label="Răng" placeholder="25, 26, 46" maxLength={100} /></Form.Item></td>
    <td>{field('serviceId', <Select size="small" showSearch optionFilterProp="label" aria-label="Điều trị/dịch vụ" placeholder="Chọn dịch vụ"
      options={services.map(s => ({ value: s.id, label: s.name }))} />, true)}</td>
    {kind === 'plan' && <td>{field('estimatedMinutes', <InputNumber step={1} changeOnBlur={false} size="small" aria-label="Thời gian dự kiến (phút)" />)}</td>}
    <td data-col="note"><div className="treatment-sheet-note-input">{field('note', <Input.TextArea rows={2} maxLength={1000} aria-label={kind === 'plan' ? 'Ghi chú' : 'Ghi chú việc đã làm'} />)}
      {kind === 'actual' && <Tooltip title="Chọn vật liệu dùng"><Button className="material-note-trigger" type="text" size="small" icon={<ToolOutlined />} aria-label="Chọn vật liệu dùng" onClick={()=>setMaterialPicker(true)} /></Tooltip>}
    </div>{materialPicker && <MaterialNotePicker note={form.getFieldValue('note') ?? ''} onCancel={()=>setMaterialPicker(false)}
      onApply={value=>{form.setFieldValue('note',value);setDirty(true);setError(undefined);setMaterialPicker(false);}} />}</td>
    <td>{field('dentistId', <Select size="small" showSearch optionFilterProp="label" aria-label="Bác sĩ" options={dentists} />, true)}
      {field('assistantId', <Select size="small" showSearch optionFilterProp="label" allowClear aria-label="Phụ tá" placeholder="Phụ tá" options={assistants} />)}</td>
    {kind === 'actual' && <td>{field('labWork', <Input.TextArea rows={2} maxLength={1000} aria-label="Ghi chú Labo" placeholder="Công việc Labo" />)}</td>}
    <td>{field('costVnd', <InputNumber min={0} precision={0} size="small" aria-label="Chi phí" />, true)}</td>
    <td data-col="discount">{field('discountValue', <InputNumber min={0} precision={0} size="small" aria-label="Giảm giá" />)}
      {field('discountType', <Select size="small" aria-label="Loại giảm giá" options={[{ value: 'VND', label: '₫' }, { value: 'Percent', label: '%' }]} />)}
      {field('discountNote', <Input size="small" aria-label="Ghi chú giảm giá" placeholder="Lý do giảm trừ" maxLength={1000} />)}</td>
    {kind === 'actual' && <td>—</td>}
    <td><div className="treatment-sheet-edit-actions">{error && <small role="alert">{error}</small>}
      {error && <Button size="small" onClick={() => { setBusy(true); void onReload().then(() => { pending.current = null; setError(undefined); }).catch(e => setError(e.message)).finally(() => setBusy(false)); }}>Đọc lại phiên bản</Button>}
      <Button size="small" type="primary" loading={busy} aria-label="Đồng ý" title="Đồng ý" onClick={() => void submit()}>✓</Button>
      <Button size="small" aria-label="Đóng hàng nhập" onClick={() => { if (!dirty || window.confirm('Bỏ nội dung hàng chưa lưu?')) onCancel(); }}>×</Button>
    </div></td>
  </tr></Form>;
}
