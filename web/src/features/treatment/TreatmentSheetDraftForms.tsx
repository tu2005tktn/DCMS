import { Button, Form, Input, InputNumber, Select } from 'antd';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import type { AppointmentOptions, TreatmentDossier, TreatmentItem } from './types';

export type SheetPlanPreview = {
  id: string; draftVersion?: number; date: string; serviceId: number; dentistId: number;
  serviceName: string; regions: string[]; quantity: number;
  note?: string; dentistName: string; assistantName?: string;
  costVnd: number; discountVnd?: number; discountType: 'VND' | 'Percent';
  discountValue: number; discountNote?: string;
};
export type SheetActualPreview = {
  id: string; draftVersion?: number; date: string; serviceId: number; dentistId: number;
  sourceItemId?: string;
  serviceName: string; regions: string[]; quantity: number;
  note: string; dentistName: string; assistantName?: string; labWork?: string;
  labSupplier?: string; costVnd: number; discountVnd?: number;
  discountType: 'VND' | 'Percent'; discountValue: number; discountNote?: string;
};

type Values = {
  date: string; toothNumbers: string; quantity: number; serviceId: number;
  note?: string; dentistId: number; assistantName?: string; labWork?: string;
  labSupplier?: string; costVnd: number; discountValue: number;
  discountType: 'VND' | 'Percent'; discountNote?: string;
};

export default function TreatmentSheetDraftRow({ kind, dossier, options, initialPlan, initialActual,
  sourceItem, sourcePlanPreview, onCancel, onPlanPreview,
  onActualPreview }: {
  kind: 'plan' | 'actual'; dossier: TreatmentDossier; options: AppointmentOptions | null;
  initialPlan?: SheetPlanPreview | null;
  initialActual?: SheetActualPreview | null;
  sourceItem?: TreatmentItem | null;
  sourcePlanPreview?: SheetPlanPreview | null;
  onCancel: () => void;
  onPlanPreview: (value: SheetPlanPreview, expectedVersion: number, requestKey: string) => Promise<number>;
  onActualPreview: (value: SheetActualPreview, expectedVersion: number, requestKey: string) => Promise<number>;
}) {
  const [form] = Form.useForm<Values>();
  const activeKey = `dcms.sheet-active.${dossier.id}.${kind}.${sourceItem?.id ?? sourcePlanPreview?.id ?? 'new'}`;
  const draftId = useRef(initialPlan?.id ?? initialActual?.id ?? sessionStorage.getItem(activeKey) ?? crypto.randomUUID());
  const version = useRef(initialPlan?.draftVersion ?? initialActual?.draftVersion ?? 0);
  const pendingRequest = useRef<{ payload: string; key: string } | null>(null);
  const storageKey = `dcms.sheet-draft.${dossier.id}.${kind}.${draftId.current}`;
  const [changeTick, setChangeTick] = useState(0);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const services = options?.services.filter(service => service.branchId === dossier.branchId) ?? [];
  const dentists = options?.dentists.filter(dentist => dentist.branchId === dossier.branchId) ?? [];
  const dentistOptions = dentists.some(dentist => dentist.id === dossier.dentistId)
    ? dentists.map(dentist => ({ value: dentist.id, label: dentist.name }))
    : [{ value: dossier.dentistId, label: `Bác sĩ #${dossier.dentistId}` },
      ...dentists.map(dentist => ({ value: dentist.id, label: dentist.name }))];

  async function preview(values: Values) {
    const regions = values.toothNumbers.trim().split(/[\s,;]+/).filter(Boolean);
    const discountVnd = values.discountType === 'Percent'
      ? Math.round(values.costVnd * values.discountValue / 100) : values.discountValue;
    const base = {
      id: draftId.current, date: values.date,
      serviceId: values.serviceId, dentistId: values.dentistId,
      regions, quantity: values.quantity,
      serviceName: services.find(service => service.id === values.serviceId)?.name ?? `Dịch vụ #${values.serviceId}`,
      dentistName: dentistOptions.find(dentist => dentist.value === values.dentistId)?.label ?? `Bác sĩ #${values.dentistId}`,
      assistantName: values.assistantName?.trim(), costVnd: values.costVnd,
      discountVnd, discountType: values.discountType, discountValue: values.discountValue,
      discountNote: values.discountNote?.trim(),
    };
    const payload = JSON.stringify(values);
    if (pendingRequest.current?.payload !== payload)
      pendingRequest.current = { payload, key: crypto.randomUUID() };
    const requestKey = pendingRequest.current.key;
    setSaving(true); setSaveError(null);
    try {
      version.current = kind === 'plan'
        ? await onPlanPreview({ ...base, note: values.note?.trim() }, version.current, requestKey)
        : await onActualPreview({ ...base, sourceItemId: sourceItem?.id ?? initialActual?.sourceItemId,
          note: values.note?.trim() ?? '',
          labWork: values.labWork?.trim(), labSupplier: values.labSupplier?.trim() }, version.current, requestKey);
      pendingRequest.current = null;
      setSaved(true);
      sessionStorage.removeItem(storageKey);
    } catch (failure) { setSaveError((failure as Error).message); }
    finally { setSaving(false); }
  }

  useEffect(() => {
    if (!initialPlan && !initialActual) sessionStorage.setItem(activeKey, draftId.current);
    const pending = sessionStorage.getItem(storageKey);
    if (pending) {
      try { form.setFieldsValue(JSON.parse(pending) as Values); } catch { sessionStorage.removeItem(storageKey); }
    }
  }, [activeKey, form, initialPlan, initialActual, storageKey]);
  useEffect(() => {
    if (!changeTick || saving) return;
    const timer = window.setTimeout(() => {
      void form.validateFields({ validateOnly: true }).then(values => preview(values)).catch(() => {});
    }, 850);
    return () => window.clearTimeout(timer);
  }, [changeTick, saving, form]);

  const item = (name: keyof Values, children: ReactNode, requiredMessage?: string) =>
    <Form.Item name={name} className="treatment-sheet-cell-field"
      rules={requiredMessage ? [{ required: true, message: requiredMessage }] : undefined}>{children}</Form.Item>;

  return <Form form={form} component={false} onValuesChange={() => {
    sessionStorage.setItem(storageKey, JSON.stringify(form.getFieldsValue(true)));
    setSaved(false); setSaveError(null); setChangeTick(value => value + 1);
  }} initialValues={{ quantity: 1, dentistId: dossier.dentistId,
    costVnd: 0, discountValue: 0, discountType: kind === 'plan' ? 'VND' : 'Percent',
    ...(kind === 'actual' && sourceItem ? {
      toothNumbers: sourceItem.region ?? undefined,
      quantity: sourceItem.quantity, serviceId: sourceItem.serviceId,
      costVnd: Math.round(sourceItem.quantity * sourceItem.agreedUnitPriceVnd),
    } : {}),
    ...(kind === 'actual' && sourcePlanPreview ? {
      toothNumbers: sourcePlanPreview.regions.join(', '), quantity: sourcePlanPreview.quantity,
      serviceId: sourcePlanPreview.serviceId, dentistId: sourcePlanPreview.dentistId,
      costVnd: sourcePlanPreview.costVnd,
    } : {}),
    ...(initialPlan ? { date: initialPlan.date, toothNumbers: initialPlan.regions.join(', '),
      quantity: initialPlan.quantity, serviceId: initialPlan.serviceId, note: initialPlan.note,
      dentistId: initialPlan.dentistId, assistantName: initialPlan.assistantName,
      costVnd: initialPlan.costVnd, discountValue: initialPlan.discountValue,
      discountType: initialPlan.discountType, discountNote: initialPlan.discountNote } : {}),
    ...(initialActual ? { date: initialActual.date, toothNumbers: initialActual.regions.join(', '),
      quantity: initialActual.quantity, serviceId: initialActual.serviceId, note: initialActual.note,
      dentistId: initialActual.dentistId, assistantName: initialActual.assistantName,
      labWork: initialActual.labWork, labSupplier: initialActual.labSupplier,
      costVnd: initialActual.costVnd, discountValue: initialActual.discountValue,
      discountType: initialActual.discountType, discountNote: initialActual.discountNote } : {}) }} onFinish={preview}>
    <tr className="treatment-sheet-edit-row" aria-label={kind === 'plan' ? 'Nhập hạng mục kế hoạch' : 'Nhập quá trình điều trị'}>
      <td>{item('date', <Input type="date" size="small" aria-label={kind === 'plan' ? 'Ngày dự kiến' : 'Ngày thực hiện'} />, 'Chọn ngày.')}</td>
      <td><Form.Item name="toothNumbers" className="treatment-sheet-cell-field" rules={[
        { required: true, message: 'Nhập số răng.' },
        { validator: (_, value: string) => !value || value.trim().split(/[\s,;]+/).every(tooth =>
          /^\d{1,2}$/.test(tooth) && Number(tooth) >= 1 && Number(tooth) <= 99)
          ? Promise.resolve() : Promise.reject(new Error('Nhập các số răng từ 1 đến 99, cách nhau bằng dấu phẩy.')) },
      ]}><Input size="small" aria-label="Răng" placeholder="11, 12, 21" maxLength={100} /></Form.Item></td>
      <td>{item('quantity', <InputNumber min={1} max={1000} precision={0} size="small" aria-label="SL"
        style={{ width: '100%' }} />, 'Chọn SL.')}</td>
      <td>{item('serviceId', <Select showSearch optionFilterProp="label" size="small" aria-label="Điều trị/dịch vụ"
        placeholder={services.length ? 'Điều trị/dịch vụ' : 'Chưa có dịch vụ'}
        options={services.map(service => ({ value: service.id, label: service.name }))} />, 'Chọn dịch vụ.')}</td>
      <td>{item('note', <Input.TextArea rows={2} aria-label={kind === 'plan' ? 'Ghi chú' : 'Ghi chú việc đã làm'}
        placeholder={kind === 'plan' ? 'Ghi chú' : 'Việc đã làm'} maxLength={1000} />,
        kind === 'actual' ? 'Nhập việc đã làm.' : undefined)}</td>
      <td><div className="treatment-sheet-staff-fields">
        {item('dentistId', <Select showSearch optionFilterProp="label" size="small" aria-label="Bác sĩ"
          options={dentistOptions} />, 'Chọn bác sĩ.')}
        {item('assistantName', <Input size="small" aria-label="Phụ tá" placeholder="Phụ tá" maxLength={200} />)}
      </div></td>
      {kind === 'actual' && <td><div className="treatment-sheet-staff-fields">
        {item('labWork', <Input size="small" aria-label="Công việc labo"
          placeholder="Công việc labo" maxLength={200} />)}
        {item('labSupplier', <Input size="small" aria-label="Cty cung cấp"
          placeholder="Cty cung cấp" maxLength={200} />)}
      </div></td>}
      <td>{item('costVnd', <InputNumber min={0} precision={0} size="small"
        aria-label={kind === 'plan' ? 'Chi phí' : 'Chi phí tham chiếu'}
        style={{ width: '100%' }} />, 'Nhập chi phí.')}</td>
      <td><div className="treatment-sheet-discount-fields">
        <Form.Item name="discountValue" className="treatment-sheet-cell-field" dependencies={['costVnd', 'discountType']}
          rules={[{ validator: (_, value) => {
            const type = form.getFieldValue('discountType') as Values['discountType'];
            const max = type === 'Percent' ? 100 : form.getFieldValue('costVnd');
            return value === undefined || value <= max ? Promise.resolve()
              : Promise.reject(new Error(type === 'Percent' ? 'Tối đa 100%.' : 'Giảm giá vượt chi phí.'));
          } }]}>
          <InputNumber min={0} precision={0} size="small" aria-label="Giảm giá" style={{ width: '100%' }} />
        </Form.Item>
        {item('discountType', <Select size="small" aria-label="Loại giảm giá" options={[
          { value: 'VND', label: '₫' }, { value: 'Percent', label: '%' },
        ]} />)}
        {item('discountNote', <Input size="small" aria-label="Ghi chú giảm giá"
          placeholder="Ghi chú" maxLength={200} />)}
      </div></td>
      <td><div className="treatment-sheet-edit-actions">
        <small role="status">{saveError ? `Chưa lưu: ${saveError}` : saving ? 'Đang lưu…'
          : saved ? 'Đã lưu nháp' : 'Tự lưu khi đủ thông tin'}</small>
        {saveError && <Button size="small" onClick={() => { setSaveError(null); setChangeTick(value => value + 1); }}>Thử lại</Button>}
        <Button size="small" aria-label="Đóng hàng nhập" onClick={() => {
          if (version.current > 0) sessionStorage.removeItem(activeKey);
          onCancel();
        }}>×</Button>
      </div></td>
    </tr>
  </Form>;
}
