import { Button, Input, InputNumber, Select } from 'antd';
import { useState } from 'react';
import type { ItemPlanInput, ItemPlanOptions, ItemPlanRow } from './itemPlanTypes';
import { isFdi } from './sheetTypes';

export default function ItemPlanRowEditor({ initial, options, busy, onDirty, onSave, onCancel }: {
  initial: ItemPlanRow | null; options: ItemPlanOptions; busy: boolean; onDirty: () => void;
  onSave: (value: ItemPlanInput) => Promise<void>; onCancel: () => void;
}) {
  const [value, setValue] = useState<ItemPlanInput>(() => initial ? { ...initial } : {
    date: null, teeth: [], serviceId: 0, estimatedMinutes: null, note: null, dentistId: null, assistantId: null, costVnd: 0 });
  const [teeth, setTeeth] = useState(initial?.teeth.join(', ') ?? '');
  const [issues, setIssues] = useState<string[]>([]);
  function update(patch: Partial<ItemPlanInput>) { setValue(v => ({ ...v, ...patch })); onDirty(); setIssues([]); }
  const services = [...options.services];
  if (initial && !services.some(x => x.id === initial.serviceId)) services.push({ id: initial.serviceId, name: `${initial.serviceName} (đã ngừng)`, referenceUnitPriceVnd: null });
  const members = [...options.members];
  if (initial?.dentistId != null && !members.some(x => x.id === initial.dentistId)) members.push({ id: initial.dentistId, name: `${initial.dentistName} (đã ngừng)`, canDentist: true, canAssistant: false });
  if (initial?.assistantId && !members.some(x => x.id === initial.assistantId)) members.push({ id: initial.assistantId, name: `${initial.assistantName} (đã ngừng)`, canDentist: false, canAssistant: true });
  async function save() {
    const codes = [...new Set(teeth.split(/[,;\s]+/).filter(Boolean))], errors: string[] = [];
    if (codes.some(x => !isFdi(x)) || codes.length > 52) errors.push('Nhập mã răng FDI, ví dụ 25, 26, 46.');
    if (!value.serviceId) errors.push('Chọn điều trị/dịch vụ.');
    if (value.date && (!/^\d{4}-\d{2}-\d{2}$/.test(value.date) || Number.isNaN(Date.parse(value.date)) || new Date(value.date).toISOString().slice(0, 10) !== value.date)) errors.push('Ngày không hợp lệ.');
    if (value.estimatedMinutes !== null && (!Number.isInteger(value.estimatedMinutes) || value.estimatedMinutes < 1 || value.estimatedMinutes > 480)) errors.push('Thời gian dự kiến phải là số phút nguyên từ 1 đến 480.');
    if (value.costVnd == null || !Number.isInteger(value.costVnd) || value.costVnd < 0 || value.costVnd > 999999999999) errors.push('Chi phí phải là số VND nguyên không âm, tối đa 999999999999.');
    if ((value.note?.length ?? 0) > 1000) errors.push('Ghi chú tối đa 1.000 ký tự.');
    setIssues(errors); if (!errors.length) await onSave({ ...value, teeth: codes });
  }
  return <tr className="item-plan-edit-row" aria-label={initial ? 'Sửa hạng mục' : 'Thêm hạng mục'}>
    <td><Input disabled={busy} aria-label="Ngày dự kiến" type="date" value={value.date ?? ''} onChange={e => update({ date: e.target.value || null })} /></td>
    <td><Input disabled={busy} aria-label="Răng" placeholder="25, 26, 46" value={teeth} maxLength={160} onChange={e => { setTeeth(e.target.value); onDirty(); }} /></td>
    <td><Select disabled={busy} aria-label="Điều trị/dịch vụ" showSearch optionFilterProp="label" placeholder="Chọn dịch vụ" value={value.serviceId || undefined}
      options={services.map(x => ({ value: x.id, label: x.name }))} onChange={id => {
        const selected = options.services.find(x => x.id === id);
        update({ serviceId: id, ...(selected?.referenceUnitPriceVnd != null ? { costVnd: selected.referenceUnitPriceVnd } : {}) });
      }} /></td>
    <td><InputNumber disabled={busy} aria-label="Thời gian dự kiến (phút)" changeOnBlur={false} value={value.estimatedMinutes} onChange={v => update({ estimatedMinutes: v })} /></td>
    <td><Input.TextArea disabled={busy} aria-label="Ghi chú" rows={2} maxLength={1000} value={value.note ?? ''} onChange={e => update({ note: e.target.value || null })} /></td>
    <td><Select disabled={busy} aria-label="Bác sĩ" showSearch optionFilterProp="label" placeholder="Bác sĩ (không bắt buộc)" allowClear value={value.dentistId ?? undefined}
      options={members.filter(x => x.canDentist).map(x => ({ value: x.id, label: x.name }))} onChange={id => update({ dentistId: id ?? null })} />
      <Select disabled={busy} aria-label="Phụ tá" showSearch optionFilterProp="label" placeholder="Phụ tá" allowClear value={value.assistantId ?? undefined}
        options={members.filter(x => x.canAssistant).map(x => ({ value: x.id, label: x.name }))} onChange={id => update({ assistantId: id ?? null })} /></td>
    <td><InputNumber disabled={busy} aria-label="Chi phí" changeOnBlur={false} value={value.costVnd} onChange={v => update({ costVnd: v as number })} /></td>
    <td><Button disabled={busy} loading={busy} type="primary" aria-label="Đồng ý" onClick={() => void save()}>✓</Button>
      <Button disabled={busy} aria-label="Hủy sửa hạng mục" onClick={onCancel}>×</Button>
      {!!issues.length && <ul role="alert">{issues.map(x => <li key={x}>{x}</li>)}</ul>}</td>
  </tr>;
}
