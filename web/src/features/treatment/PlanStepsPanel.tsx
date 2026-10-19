import { Alert, Button, Card, Drawer, Empty, Form, Input, InputNumber, Modal, Select, Skeleton, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type ApiError } from '../../services/api';
import type { TreatmentDossier } from './types';
import type { PlanStep, StepChange, StepDefinition, TemplateListItem, TreatmentTemplate } from './progressionTypes';

type Action = { step: PlanStep; kind: 'hold' | 'release-hold' | 'progress' };
type ActionForm = { reason: string; reviewCondition?: string; progressAction?: string };
type CopyStepForm = Pick<StepDefinition, 'name' | 'region' | 'estimatedMinutes' | 'reservedMinutes' |
  'predecessorIds' | 'capability' | 'labRequirement' | 'earliestOffsetDays' | 'targetOffsetDays' |
  'latestOffsetDays' | 'chargingCoverage'>;

export default function PlanStepsPanel({ dossier, dirty, onChanged }: {
  dossier: TreatmentDossier; dirty: boolean; onChanged: () => void;
}) {
  const [rows, setRows] = useState<PlanStep[] | null>(null);
  const [templates, setTemplates] = useState<TemplateListItem[]>([]);
  const [template, setTemplate] = useState<TreatmentTemplate | null>(null);
  const [mapped, setMapped] = useState<StepDefinition[]>([]);
  const [action, setAction] = useState<Action | null>(null);
  const [stepEditing, setStepEditing] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [form] = Form.useForm<ActionForm>();
  const [copyForm] = Form.useForm<CopyStepForm>();
  const requestKey = useRef(crypto.randomUUID());
  const actionEffectiveAt = useRef(new Date().toISOString());
  const active = template?.versions.find(x => x.state === 'Active');
  const agreedItems = dossier.items.filter(x => x.decision === 'Agreed');

  useEffect(() => {
    let live = true;
    Promise.all([api<PlanStep[]>(`/api/dossiers/${dossier.id}/steps`),
      api<TemplateListItem[]>(`/api/treatment-templates?branchId=${dossier.branchId}`)])
      .then(([steps, list]) => { if (live) { setRows(steps); setTemplates(list.filter(x => x.activeVersion !== null)); } })
      .catch(failure => { if (live) setError(failure as ApiError); });
    return () => { live = false; };
  }, [dossier.id, dossier.version, dossier.branchId]);

  async function selectTemplate(id: string) {
    setError(null);
    try {
      const result = await api<TreatmentTemplate>(`/api/treatment-templates/${id}`);
      setTemplate(result);
      setMapped((result.versions.find(x => x.state === 'Active')?.steps ?? [])
        .map(step => ({ ...step, itemId: agreedItems.length === 1 ? agreedItems[0].id : null })));
      requestKey.current = crypto.randomUUID();
    } catch (failure) { setError(failure as ApiError); }
  }

  async function apply() {
    if (!template || !active || mapped.some(x => !x.itemId) || busy || dirty) return;
    setBusy(true); setError(null);
    try {
      const result = await api<StepChange>(`/api/dossiers/${dossier.id}/plan/from-template`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: dossier.version, templateId: template.id,
          templateVersion: active.number, steps: mapped, requestKey: requestKey.current }),
      });
      setRows(result.steps); setNotice(result.notification.message); requestKey.current = crypto.randomUUID();
      onChanged();
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }

  async function saveAction(values: ActionForm) {
    if (!action || busy) return;
    setBusy(true); setError(null);
    try {
      const body = action.kind === 'progress'
        ? { expectedVersion: dossier.version, expectedStepVersion: action.step.version,
          outcome: values.progressAction ?? 'Partial', reason: values.reason, requestKey: requestKey.current }
        : { expectedVersion: dossier.version, expectedStepVersion: action.step.version,
          reason: values.reason, reviewCondition: values.reviewCondition || 'Bác sĩ xem lại ở lần hẹn tiếp theo',
          effectiveAt: actionEffectiveAt.current, requestKey: requestKey.current };
      const result = await api<StepChange>(`/api/dossiers/${dossier.id}/steps/${action.step.id}/${action.kind}`, {
        method: 'POST', body: JSON.stringify(body),
      });
      setRows(result.steps); setNotice(result.notification.message); setAction(null);
      requestKey.current = crypto.randomUUID(); onChanged();
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }

  return <Card title="Các bước điều trị" extra={<Typography.Text type="secondary">Kết quả thực hiện và điều kiện sẵn sàng được ghi riêng</Typography.Text>}>
    {error && <Alert type="error" showIcon message={error.message} style={{ marginBottom: 16 }}
      description={error.code === 'MSG111' || error.code === 'MSG236' ? 'Dữ liệu đang chọn được giữ lại. Hãy tải lại hồ sơ và đối chiếu phiên bản.' : undefined} />}
    {notice && <Alert type="success" showIcon message={notice} style={{ marginBottom: 16 }} />}
    {rows === null ? <Skeleton active /> : rows.length ? <Table rowKey="id" pagination={false} scroll={{ x: 750 }} dataSource={rows} columns={[
      { title: 'Bước', render: (_: unknown, row: PlanStep) => <><strong>{row.name}</strong><div>{row.region || 'Chưa chọn vùng'}</div></> },
      { title: 'Hạng mục', render: (_: unknown, row: PlanStep) => dossier.items.find(x => x.id === row.itemId)?.serviceName ?? 'Không tìm thấy' },
      { title: 'Thời gian', render: (_: unknown, row: PlanStep) => `${row.estimatedMinutes} phút dự kiến · ${row.reservedMinutes} phút giữ chỗ` },
      { title: 'Kết quả', render: (_: unknown, row: PlanStep) => <Tag>{row.outcome === 'Partial' ? 'Đã làm một phần' : row.outcome === 'Completed' ? 'Hoàn tất' : 'Chưa thực hiện'}</Tag> },
      { title: 'Sẵn sàng', render: (_: unknown, row: PlanStep) => <><Tag color={row.readiness === 'Ready' ? 'green' : 'orange'}>
        {row.readiness === 'Ready' ? 'Đủ điều kiện' : 'Đang bị chặn'}</Tag>{row.blockers.map(text => <div key={text} className="treatment-muted">{text}</div>)}</> },
      { title: 'Thao tác', render: (_: unknown, row: PlanStep) => dossier.planState === 'Committed' && dossier.state === 'Open'
        ? <Space wrap><Button disabled={row.outcome === 'Completed'} onClick={() => { form.resetFields(); setAction({ step: row, kind: 'progress' }); requestKey.current = crypto.randomUUID(); actionEffectiveAt.current = new Date().toISOString(); }}>Ghi một phần</Button>
          <Button disabled={row.outcome === 'Completed'} onClick={() => { form.resetFields(); setAction({ step: row, kind: row.holdReason ? 'release-hold' : 'hold' }); requestKey.current = crypto.randomUUID(); actionEffectiveAt.current = new Date().toISOString(); }}>
            {row.holdReason ? 'Gỡ tạm dừng' : 'Tạm dừng'}</Button></Space> : null },
    ]} /> : <Empty description="Chưa có bước điều trị. Có thể áp dụng mẫu vào bản nháp kế hoạch." />}
    {rows?.length === 0 && dossier.planState !== 'Committed' && <div style={{ marginTop: 24 }}>
      <Typography.Title level={4}>Áp dụng mẫu vào hồ sơ này</Typography.Title>
      {agreedItems.length === 0 && <Alert type="info" showIcon message="Cần lưu ít nhất một hạng mục đã đồng ý trước khi áp dụng mẫu." />}
      <Select aria-label="Chọn mẫu đang dùng" placeholder="Chọn mẫu đang dùng" style={{ width: '100%', maxWidth: 420 }}
        disabled={agreedItems.length === 0 || dirty} onChange={value => void selectTemplate(value)}
        options={templates.map(x => ({ value: x.id, label: `${x.name} · V${x.activeVersion}` }))} />
      {active && <><Typography.Paragraph style={{ marginTop: 16 }}>Nguồn: {template?.name}, phiên bản {active.number}.
        Bản sao của bệnh nhân sẽ không đổi khi mẫu được sửa.</Typography.Paragraph>
        {mapped.map((step, index) => <div className="treatment-item-row" key={step.logicalStepId}><div><strong>{step.name}</strong>
          <div className="treatment-muted">{step.reservedMinutes} phút giữ chỗ · {step.predecessorIds.length} bước tiền nhiệm</div></div>
          <Space wrap><Select aria-label={`Hạng mục cho bước ${step.name}`} placeholder="Gắn với hạng mục đã đồng ý" value={step.itemId}
            style={{ minWidth: 240 }} options={agreedItems.map(item => ({ value: item.id,
              label: `${item.serviceName} · ${item.region || 'Chưa chọn vùng'}` }))}
            onChange={value => { setMapped(current => current.map((row, i) => i === index ? { ...row, itemId: value } : row));
              requestKey.current = crypto.randomUUID(); }} /><Button onClick={() => {
                copyForm.resetFields(); copyForm.setFieldsValue(step); setStepEditing(index);
              }}>Chỉnh bước</Button></Space></div>)}
        <Button type="primary" loading={busy} disabled={mapped.some(x => !x.itemId) || dirty} onClick={() => void apply()}>Sao chép vào kế hoạch</Button>
      </>}
    </div>}
    <Modal open={!!action} title={action?.kind === 'hold' ? 'Tạm dừng bước' : action?.kind === 'release-hold' ? 'Gỡ tạm dừng' : 'Ghi đã làm một phần'}
      onCancel={() => setAction(null)} onOk={() => form.submit()} confirmLoading={busy} okText="Lưu thay đổi">
      <Form form={form} layout="vertical" onFinish={values => void saveAction(values)} onValuesChange={() => { requestKey.current = crypto.randomUUID(); }}>
        {action?.kind === 'progress' && <Form.Item name="progressAction" label="Đánh giá tiếp theo" initialValue="Partial">
          <Select options={[{ value: 'Partial', label: 'Đã làm một phần' }, { value: 'Repeat', label: 'Cần lặp lại' },
            { value: 'Deferred', label: 'Hoãn bước' }, { value: 'AddVisit', label: 'Cần thêm buổi' }]} /></Form.Item>}
        <Form.Item name="reason" label="Lý do của bác sĩ" rules={[{ required: true, message: 'Nhập lý do' }]}><Input.TextArea maxLength={1000} rows={3} /></Form.Item>
        {action?.kind !== 'progress' && <Form.Item name="reviewCondition" label="Điều kiện xem lại">
          <Select options={[{ label: 'Ở lần hẹn tiếp theo', value: 'Bác sĩ xem lại ở lần hẹn tiếp theo' },
            { label: 'Sau khi có kết quả Labo', value: 'Sau khi có kết quả Labo' },
            { label: 'Sau khi tư vấn lại', value: 'Sau khi tư vấn lại' }]} /></Form.Item>}
      </Form>
    </Modal>
    <Drawer open={stepEditing !== null} title="Chỉnh bước cho bệnh nhân" width={520}
      onClose={() => setStepEditing(null)} extra={<Button type="primary" onClick={() => copyForm.submit()}>Lưu bước</Button>}>
      <Typography.Paragraph type="secondary">Thay đổi ở đây chỉ thuộc bản sao của bệnh nhân, không sửa mẫu gốc.</Typography.Paragraph>
      <Form form={copyForm} layout="vertical" onFinish={values => {
        if (stepEditing === null) return;
        setMapped(current => current.map((row, i) => i === stepEditing ? { ...row, ...values,
          region: values.region || null, capability: values.capability || null,
          labRequirement: values.labRequirement || null,
          earliestOffsetDays: values.earliestOffsetDays ?? null,
          targetOffsetDays: values.targetOffsetDays ?? null,
          latestOffsetDays: values.latestOffsetDays ?? null } : row));
        requestKey.current = crypto.randomUUID(); setStepEditing(null);
      }}>
        <Form.Item name="name" label="Tên bước" rules={[{ required: true }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="region" label="Răng hoặc vùng"><Input maxLength={80} /></Form.Item>
        <div className="treatment-scope-grid"><Form.Item name="estimatedMinutes" label="Ước tính (phút)" rules={[{ required: true }]}>
          <InputNumber min={1} max={480} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="reservedMinutes" label="Giữ chỗ (phút)" rules={[{ required: true },
            { validator: async (_, value: number) => { if (value % 15 !== 0 || value < copyForm.getFieldValue('estimatedMinutes')) throw new Error('Phải là bội 15 và không nhỏ hơn ước tính'); } }]}>
            <Select options={Array.from({ length: 32 }, (_, index) => ({ label: `${(index + 1) * 15} phút`, value: (index + 1) * 15 }))} />
          </Form.Item></div>
        <Form.Item name="predecessorIds" label="Bước tiền nhiệm"><Select mode="multiple" options={mapped
          .filter(x => x.logicalStepId !== mapped[stepEditing ?? 0]?.logicalStepId)
          .map(x => ({ value: x.logicalStepId, label: x.name }))} /></Form.Item>
        <Form.Item name="capability" label="Năng lực bác sĩ"><Input maxLength={160} /></Form.Item>
        <Form.Item name="labRequirement" label="Điều kiện Labo"><Input maxLength={160} /></Form.Item>
        <div className="treatment-scope-grid"><Form.Item name="earliestOffsetDays" label="Sớm nhất sau (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="targetOffsetDays" label="Mục tiêu sau (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="latestOffsetDays" label="Muộn nhất sau (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></div>
        <Form.Item name="chargingCoverage" label="Phạm vi giá"><Select options={[
          { value: 'IncludedInItem', label: 'Trong giá hạng mục đã thỏa thuận' },
          { value: 'FinancialReview', label: 'Cần xem xét tài chính riêng' },
        ]} /></Form.Item>
      </Form>
    </Drawer>
  </Card>;
}
