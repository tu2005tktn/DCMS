import { Alert, Button, Card, Drawer, Empty, Form, Input, InputNumber, Result, Select, Skeleton, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type ApiError } from '../../services/api';
import type { AppointmentOptions } from './types';
import type { StepDefinition, TemplateListItem, TreatmentTemplate } from './progressionTypes';
import './treatment.css';

type StepForm = Omit<StepDefinition, 'logicalStepId' | 'itemId'>;
const blankStep = (): StepDefinition => ({ logicalStepId: crypto.randomUUID(), itemId: null, name: '', region: null,
  estimatedMinutes: 30, reservedMinutes: 30, predecessorIds: [], capability: null, labRequirement: null,
  earliestOffsetDays: null, targetOffsetDays: null, latestOffsetDays: null, chargingCoverage: 'IncludedInItem' });

export default function TemplatePage() {
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [list, setList] = useState<TemplateListItem[]>([]);
  const [template, setTemplate] = useState<TreatmentTemplate | null>(null);
  const [steps, setSteps] = useState<StepDefinition[]>([]);
  const [name, setName] = useState('');
  const [editing, setEditing] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [form] = Form.useForm<StepForm>();
  const requestKey = useRef(crypto.randomUUID());
  const actionKeys = useRef(new Map<string, string>());
  function keyFor(action: string) {
    if (!actionKeys.current.has(action)) actionKeys.current.set(action, crypto.randomUUID());
    return actionKeys.current.get(action)!;
  }
  const draft = template?.versions.find(x => x.state === 'Draft');
  const active = template?.versions.find(x => x.state === 'Active');

  useEffect(() => {
    api<AppointmentOptions>('/api/appointments/options').then(result => {
      setOptions(result); setBranchId(result.branches[0]?.id ?? null);
    }).catch(failure => setError(failure as ApiError)).finally(() => setLoading(false));
  }, []);
  useEffect(() => {
    if (!branchId) return;
    let live = true;
    api<TemplateListItem[]>(`/api/treatment-templates?branchId=${branchId}`)
      .then(rows => { if (live) setList(rows); }).catch(failure => { if (live) setError(failure as ApiError); });
    return () => { live = false; };
  }, [branchId]);

  async function open(id: string) {
    setLoading(true); setError(null);
    try {
      const result = await api<TreatmentTemplate>(`/api/treatment-templates/${id}`);
      setTemplate(result); setName(result.name);
      setSteps((result.versions.find(x => x.state === 'Draft') ?? result.versions.find(x => x.state === 'Active')
        ?? result.versions.at(-1))?.steps ?? []);
      requestKey.current = crypto.randomUUID(); actionKeys.current.clear(); setDirty(false);
    } catch (failure) { setError(failure as ApiError); }
    finally { setLoading(false); }
  }

  function updateStep(index: number, step: StepDefinition) {
    setSteps(current => index === current.length ? [...current, step] : current.map((row, i) => i === index ? step : row));
    requestKey.current = crypto.randomUUID(); setDirty(true); setEditing(null);
  }
  function stepEditor(index: number) {
    form.resetFields(); setEditing(index);
    form.setFieldsValue(steps[index] ?? blankStep());
  }
  async function command(url: string, body: object) {
    setBusy(true); setError(null); setNotice(null);
    try {
      const result = await api<TreatmentTemplate>(url, { method: 'POST', body: JSON.stringify(body) });
      setTemplate(result); setName(result.name);
      setSteps((result.versions.find(x => x.state === 'Draft') ?? result.versions.find(x => x.state === 'Active')
        ?? result.versions.at(-1))?.steps ?? []);
      setNotice('Đã lưu mẫu điều trị và phiên bản.'); requestKey.current = crypto.randomUUID();
      actionKeys.current.clear(); setDirty(false);
      if (branchId) setList(await api<TemplateListItem[]>(`/api/treatment-templates?branchId=${branchId}`));
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }
  function save() {
    if (!branchId || !name.trim() || steps.length === 0 || busy) return;
    if (!template) void command('/api/treatment-templates', { branchId, name: name.trim(), steps, requestKey: requestKey.current });
    else if (draft) void command(`/api/treatment-templates/${template.id}/versions/${draft.number}/draft`,
      { expectedVersion: template.version, steps, requestKey: requestKey.current });
  }

  if (loading && !options) return <Card><Skeleton active /></Card>;
  if (!options) return <Result status="error" title={error?.message ?? 'Không tải được chi nhánh'}
    extra={<Button onClick={() => window.location.reload()}>Thử lại</Button>} />;
  return <main className="treatment-page">
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Mẫu điều trị S58</Typography.Title>
      <Typography.Paragraph>Thiết kế tiến trình để bác sĩ tham khảo và sao chép vào kế hoạch của từng bệnh nhân.</Typography.Paragraph>
    </div><Select aria-label="Chi nhánh" value={branchId} style={{ minWidth: 200 }} options={options.branches.map(x => ({ label: x.name, value: x.id }))}
      onChange={value => { setBranchId(value); setTemplate(null); setSteps([]); setName(''); setDirty(false);
        actionKeys.current.clear(); requestKey.current = crypto.randomUUID(); }} /></div>
    {error && <Alert type="error" showIcon message={error.message}
      description={error.code === 'MSG111' || error.code === 'MSG236' ? 'Nội dung đang sửa vẫn được giữ. Hãy tải lại phiên bản trước khi xác nhận.' : undefined} />}
    {notice && <Alert type="success" showIcon message={notice} />}
    <div className="treatment-detail-grid"><Card title="Mẫu trong chi nhánh" extra={<Button onClick={() => {
      setTemplate(null); setName(''); setSteps([blankStep()]); requestKey.current = crypto.randomUUID(); setDirty(true);
    }}>Tạo mẫu</Button>}>
      {list.length === 0 ? <Empty description="Chưa có mẫu điều trị. Bác sĩ có thể tạo bản nháp đầu tiên." />
        : list.map(row => <div key={row.id} className="treatment-item-row"><div><strong>{row.name}</strong>
          <div className="treatment-muted">{row.activeVersion ? `Đang dùng phiên bản ${row.activeVersion}` : 'Chưa có phiên bản đang dùng'}</div></div>
          <Button onClick={() => void open(row.id)}>Mở</Button></div>)}
    </Card><Card title={template ? `${template.name} · phiên bản hệ thống ${template.version}` : 'Bản nháp mới'} loading={loading}>
      <label htmlFor="template-name">Tên mẫu</label><Input id="template-name" value={name} maxLength={160}
        disabled={!!template} onChange={event => { setName(event.target.value); requestKey.current = crypto.randomUUID(); setDirty(true); }} />
      {template && <Space wrap style={{ marginTop: 16 }}>
        {template.versions.map(version => <Tag key={version.number} color={version.state === 'Active' ? 'green' : undefined}>
          V{version.number} · {version.state === 'Active' ? 'Đang dùng' : version.state === 'Draft' ? 'Bản nháp' : 'Đã ngừng'}</Tag>)}
      </Space>}
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>Bước trong bản {draft ? 'nháp' : active ? 'đang dùng' : 'mới'}.
        Bản sao trong hồ sơ bệnh nhân không đổi khi mẫu được sửa.</Typography.Paragraph>
      <Table rowKey="logicalStepId" pagination={false} size="small" dataSource={steps} scroll={{ x: 600 }} columns={[
        { title: 'Bước', render: (_: unknown, row: StepDefinition) => <><strong>{row.name || 'Bước mới'}</strong><div>{row.region}</div></> },
        { title: 'Ước tính / giữ chỗ', render: (_: unknown, row: StepDefinition) => `${row.estimatedMinutes} / ${row.reservedMinutes} phút` },
        { title: 'Tiền nhiệm', render: (_: unknown, row: StepDefinition) => row.predecessorIds.length },
        { title: 'Thao tác', render: (_: unknown, row: StepDefinition, index: number) => !template || draft ?
          <Space><Button onClick={() => stepEditor(index)}>Sửa</Button><Button danger onClick={() => {
            setSteps(current => current.filter(x => x.logicalStepId !== row.logicalStepId)
              .map(x => ({ ...x, predecessorIds: x.predecessorIds.filter(id => id !== row.logicalStepId) })));
            requestKey.current = crypto.randomUUID(); setDirty(true);
          }}>Bỏ</Button></Space> : null },
      ]} />
      <Space wrap style={{ marginTop: 16 }}>
        {(!template || draft) && <><Button onClick={() => stepEditor(steps.length)}>Thêm bước</Button>
          <Button type="primary" loading={busy} disabled={!name.trim() || steps.length === 0} onClick={save}>Lưu bản nháp</Button></>}
        {draft && <Button loading={busy} disabled={steps.length === 0 || dirty} onClick={() => void command(
          `/api/treatment-templates/${template!.id}/versions/${draft.number}/activate`,
          { expectedVersion: template!.version, requestKey: keyFor(`activate:${template!.id}:${draft.number}`) })}>Kích hoạt</Button>}
        {active && !draft && <Button loading={busy} onClick={() => void command(
          `/api/treatment-templates/${template!.id}/versions`,
          { expectedVersion: template!.version, steps, requestKey: keyFor(`new:${template!.id}:${template!.version}`) })}>Tạo phiên bản mới</Button>}
        {active && <Button danger loading={busy} onClick={() => void command(
          `/api/treatment-templates/${template!.id}/versions/${active.number}/retire`,
          { expectedVersion: template!.version, requestKey: keyFor(`retire:${template!.id}:${active.number}`) })}>Ngừng sử dụng</Button>}
      </Space>
    </Card></div>
    <Drawer open={editing !== null} width={520} title={editing === steps.length ? 'Thêm bước' : 'Sửa bước'}
      onClose={() => setEditing(null)} extra={<Button type="primary" onClick={() => form.submit()}>Lưu bước</Button>}>
      <Form form={form} layout="vertical" onFinish={values => {
        const previous = editing === null ? null : steps[editing];
        const next = { ...blankStep(), ...previous, ...values, logicalStepId: previous?.logicalStepId ?? crypto.randomUUID(),
          itemId: null, predecessorIds: values.predecessorIds ?? [],
          region: values.region || null, capability: values.capability || null,
          labRequirement: values.labRequirement || null,
          earliestOffsetDays: values.earliestOffsetDays ?? null, targetOffsetDays: values.targetOffsetDays ?? null,
          latestOffsetDays: values.latestOffsetDays ?? null } as StepDefinition;
        updateStep(editing ?? steps.length, next);
      }}>
        <Form.Item name="name" label="Tên bước điều trị" rules={[{ required: true, message: 'Nhập tên bước' }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="region" label="Răng hoặc vùng mặc định"><Input maxLength={80} /></Form.Item>
        <div className="treatment-scope-grid"><Form.Item name="estimatedMinutes" label="Ước tính (phút)" rules={[{ required: true }]}>
          <InputNumber min={1} max={480} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="reservedMinutes" label="Giữ chỗ (phút, bội 15)" rules={[{ required: true },
            { validator: async (_, value: number) => { if (value % 15 !== 0 || value < form.getFieldValue('estimatedMinutes')) throw new Error('Chọn bội 15, không nhỏ hơn ước tính'); } }]}>
            <Select options={Array.from({ length: 32 }, (_, index) => ({ label: `${(index + 1) * 15} phút`, value: (index + 1) * 15 }))} />
          </Form.Item></div>
        <Form.Item name="predecessorIds" label="Bước cần hoàn thành trước"><Select mode="multiple" options={steps.filter(x => x.logicalStepId !== steps[editing ?? steps.length]?.logicalStepId)
          .map(x => ({ label: x.name || 'Bước chưa đặt tên', value: x.logicalStepId }))} /></Form.Item>
        <Form.Item name="capability" label="Năng lực bác sĩ cần có"><Input maxLength={160} /></Form.Item>
        <Form.Item name="labRequirement" label="Điều kiện Labo (nếu có)"><Input maxLength={160} /></Form.Item>
        <Typography.Paragraph>Khoảng ngày quay lại sau bước tiền nhiệm. Để trống nếu bác sĩ sẽ chọn trực tiếp.</Typography.Paragraph>
        <div className="treatment-scope-grid"><Form.Item name="earliestOffsetDays" label="Sớm nhất (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="targetOffsetDays" label="Mục tiêu (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="latestOffsetDays" label="Muộn nhất (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></div>
        <Form.Item name="chargingCoverage" label="Phạm vi giá" rules={[{ required: true }]}><Select options={[
          { label: 'Nằm trong giá hạng mục đã thỏa thuận', value: 'IncludedInItem' },
          { label: 'Cần xem xét tài chính riêng', value: 'FinancialReview' },
        ]} /></Form.Item>
      </Form>
    </Drawer>
  </main>;
}
