import { usePermissions } from '../authentication/usePermissions';
import { Alert, Button, Card, Checkbox, Drawer, Empty, Form, Input, InputNumber, Modal, Result, Select, Skeleton, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type ApiError } from '../../services/api';
import type { TreatmentDossier } from './types';
import { durationOptions, durationOptionsFor, durationRangeValid, type PlanStep, type StepChange, type StepDefinition } from './progressionTypes';

type Action = { step: PlanStep; kind: 'hold' | 'release-hold' | 'progress' };
type ActionForm = { reason: string; reviewCondition?: string; progressAction?: string };
type CopyStepForm = Pick<StepDefinition, 'name' | 'region' | 'estimatedMinMinutes' | 'estimatedMaxMinutes' | 'estimatedMinutes' | 'reservedMinutes' |
  'predecessorIds' | 'capability' | 'labRequirement' | 'earliestOffsetDays' | 'targetOffsetDays' |
  'latestOffsetDays' | 'chargingCoverage'>;
type AddStepForm = CopyStepForm & { itemId: string; reason: string; acknowledgeShortReservation?: boolean };
type FollowUpForm = { reason: string; earliestOn: string; targetOn: string; latestOn: string };

export default function PlanStepsPanel({ dossier, dirty, onChanged }: {
  dossier: TreatmentDossier; dirty: boolean; onChanged: () => void;
}) {
  const has = usePermissions();
  const [rows, setRows] = useState<PlanStep[] | null>(null);
  const [reload, setReload] = useState(0);
  const [action, setAction] = useState<Action | null>(null);
  const [addingStep, setAddingStep] = useState(false);
  const [followUpStep, setFollowUpStep] = useState<PlanStep | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [form] = Form.useForm<ActionForm>();
  const [addForm] = Form.useForm<AddStepForm>();
  const [followUpForm] = Form.useForm<FollowUpForm>();
  const addMax = Form.useWatch('estimatedMaxMinutes', addForm);
  const addMin = Form.useWatch('estimatedMinMinutes', addForm);
  const addEstimate = Form.useWatch('estimatedMinutes', addForm);
  const addReserved = Form.useWatch('reservedMinutes', addForm);
  const requestKey = useRef(crypto.randomUUID());
  const actionEffectiveAt = useRef(new Date().toISOString());
  const agreedItems = dossier.items.filter(x => x.decision === 'Agreed');

  useEffect(() => {
    let live = true;
    setRows(null); setError(null);
    api<PlanStep[]>(`/api/dossiers/${dossier.id}/steps`)
      .then(steps => { if (live) setRows(steps); })
      .catch(failure => { if (live) setError(failure as ApiError); });
    return () => { live = false; };
  }, [dossier.id, dossier.version, dossier.branchId, reload]);

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
      if (action.kind === 'progress' && values.progressAction === 'AddVisit') {
        followUpForm.resetFields(); setFollowUpStep(action.step);
      }
      requestKey.current = crypto.randomUUID(); onChanged();
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }

  async function addPatientStep(values: AddStepForm) {
    if (busy || dirty) return;
    if (!durationRangeValid(values.estimatedMinMinutes, values.estimatedMaxMinutes,
      values.estimatedMinutes, values.reservedMinutes)) {
      addForm.setFields([{ name: 'reservedMinutes', errors: ['Kiểm tra khoảng ước tính và thời gian giữ ghế'] }]);
      return;
    }
    setBusy(true); setError(null);
    try {
      const result = await api<StepChange>(`/api/dossiers/${dossier.id}/steps`, { method: 'POST',
        body: JSON.stringify({ ...values, expectedVersion: dossier.version,
          predecessorIds: values.predecessorIds ?? [], requestKey: requestKey.current }) });
      setRows(result.steps); setNotice(result.notification.message); setAddingStep(false);
      requestKey.current = crypto.randomUUID(); onChanged();
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }

  async function createFollowUp(values: FollowUpForm) {
    if (!followUpStep || busy) return;
    setBusy(true); setError(null);
    try {
      const result = await api<{ instruction: { id: string; status: string; version: number } }>(
        '/api/follow-up-instructions', { method: 'POST', body: JSON.stringify({
          patientId: dossier.patientId, branchId: dossier.branchId,
          source: { dossierId: dossier.id, itemId: followUpStep.itemId, stepId: followUpStep.id,
            visitId: null, activityId: null }, purpose: 'TreatmentContinuation',
          reason: values.reason, guidance: null, earliestOn: values.earliestOn,
          targetOn: values.targetOn, latestOn: values.latestOn,
          durationMinutes: followUpStep.reservedMinutes, dentistId: dossier.dentistId,
          capability: followUpStep.capability, requestKey: requestKey.current,
        }) });
      setNotice(`Đã tạo chỉ định quay lại ${result.instruction.id} · ${result.instruction.status}. Chưa đặt lịch hẹn.`);
      setFollowUpStep(null); requestKey.current = crypto.randomUUID();
    } catch (failure) { setError(failure as ApiError); }
    finally { setBusy(false); }
  }

  return <Card title="Các bước điều trị" extra={<Typography.Text type="secondary">Kết quả thực hiện và điều kiện sẵn sàng được ghi riêng</Typography.Text>}>
    {error && <Alert type="error" showIcon message={error.message} style={{ marginBottom: 16 }}
      description={error.code === 'MSG111' || error.code === 'MSG236' ? 'Dữ liệu đang chọn được giữ lại. Hãy tải lại hồ sơ và đối chiếu phiên bản.' : undefined} />}
    {notice && <Alert type="success" showIcon message={notice} style={{ marginBottom: 16 }} />}
    {rows === null && error ? <Result status="error" title="Không tải được các bước điều trị"
      subTitle={error.message} extra={<Button onClick={() => setReload(value => value + 1)}>Thử lại</Button>} />
      : rows === null ? <Skeleton active /> : rows.length ? <Table rowKey="id" pagination={false} scroll={{ x: 750 }} dataSource={rows} columns={[
      { title: 'Bước', render: (_: unknown, row: PlanStep) => <><strong>{row.name}</strong><div>{row.region || 'Chưa chọn vùng'}</div></> },
      { title: 'Hạng mục', render: (_: unknown, row: PlanStep) => dossier.items.find(x => x.id === row.itemId)?.serviceName ?? 'Không tìm thấy' },
      { title: 'Thời gian', render: (_: unknown, row: PlanStep) => <><div>{row.estimatedMinMinutes != null && row.estimatedMaxMinutes != null ? `${row.estimatedMinMinutes}–${row.estimatedMaxMinutes} phút · ` : ''}{row.estimatedMinutes} phút đã chọn</div><div className="treatment-muted">{row.reservedMinutes} phút giữ ghế{row.shortReservationAcknowledgedAt ? ' · đã xác nhận cảnh báo' : ''}</div></> },
      { title: 'Kết quả', render: (_: unknown, row: PlanStep) => <Tag>{row.outcome === 'Partial' ? 'Đã làm một phần' : row.outcome === 'Completed' ? 'Hoàn tất' : 'Chưa thực hiện'}</Tag> },
      { title: 'Sẵn sàng', render: (_: unknown, row: PlanStep) => <><Tag color={row.readiness === 'Ready' ? 'green' : 'orange'}>
        {row.readiness === 'Ready' ? 'Đủ điều kiện' : 'Đang bị chặn'}</Tag>{row.blockers.map(text => <div key={text} className="treatment-muted">{text}</div>)}</> },
      { title: 'Thao tác', render: (_: unknown, row: PlanStep) => has('treatment.progress.manage') && dossier.planState === 'Committed' && dossier.state === 'Open'
        ? <Space wrap><Button disabled={row.outcome === 'Completed'} onClick={() => { form.resetFields(); setAction({ step: row, kind: 'progress' }); requestKey.current = crypto.randomUUID(); actionEffectiveAt.current = new Date().toISOString(); }}>Ghi một phần</Button>
          <Button disabled={row.outcome === 'Completed'} onClick={() => { followUpForm.resetFields(); setFollowUpStep(row); requestKey.current = crypto.randomUUID(); }}>Chỉ định thêm buổi</Button>
          <Button disabled={row.outcome === 'Completed'} onClick={() => { form.resetFields(); setAction({ step: row, kind: row.holdReason ? 'release-hold' : 'hold' }); requestKey.current = crypto.randomUUID(); actionEffectiveAt.current = new Date().toISOString(); }}>
            {row.holdReason ? 'Gỡ tạm dừng' : 'Tạm dừng'}</Button></Space> : null },
    ]} /> : <Empty description={dossier.planState === 'Committed'
      ? 'Chưa có bước điều trị. Bác sĩ có thể thêm bước cho hạng mục đã đồng ý.'
      : 'Chưa có bước điều trị. Các bước bệnh nhân sẽ hiển thị tại đây.'} />}
    {rows && dossier.planState === 'Committed' && dossier.state === 'Open' && agreedItems.length > 0 && <Button
      disabled={dirty} onClick={() => { addForm.resetFields(); addForm.setFieldsValue({ estimatedMinutes: 30,
        reservedMinutes: 30, estimatedMinMinutes: null, estimatedMaxMinutes: null,
        predecessorIds: [], chargingCoverage: 'IncludedInItem' });
        requestKey.current = crypto.randomUUID(); setAddingStep(true); }} style={{ marginTop: 16 }}>
      Thêm bước cho bệnh nhân
    </Button>}
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

    <Drawer open={addingStep} title="Thêm bước cho bệnh nhân" width={520} onClose={() => setAddingStep(false)}
      extra={<Button type="primary" loading={busy} onClick={() => addForm.submit()}>Lưu bước</Button>}>
      <Typography.Paragraph type="secondary">Chỉ thêm công việc thuộc hạng mục đã đồng ý. Thao tác này không đổi giá hoặc tự đặt lịch.</Typography.Paragraph>
      {error && <Alert type="error" showIcon message={error.message} style={{ marginBottom: 16 }} />}
      <Form form={addForm} layout="vertical" onFinish={values => void addPatientStep(values)}
        onValuesChange={() => { requestKey.current = crypto.randomUUID(); }}>
        <Form.Item name="itemId" label="Hạng mục đã đồng ý" rules={[{ required: true, message: 'Chọn hạng mục' }]}>
          <Select options={agreedItems.map(item => ({ value: item.id, label: item.serviceName }))} /></Form.Item>
        <Form.Item name="name" label="Tên bước" rules={[{ required: true, message: 'Nhập tên bước' }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="region" label="Răng hoặc vùng"><Input maxLength={80} /></Form.Item>
        <div className="treatment-scope-grid"><Form.Item name="estimatedMinMinutes" label="Ngắn nhất (nếu có)"><Select allowClear options={durationOptionsFor(addMin)} /></Form.Item>
          <Form.Item name="estimatedMaxMinutes" label="Dài nhất (nếu có)"><Select allowClear options={durationOptionsFor(addMax)} /></Form.Item></div>
        <div className="treatment-scope-grid"><Form.Item name="estimatedMinutes" label="Thời gian bác sĩ chọn" rules={[{ required: true }]}><Select options={durationOptionsFor(addEstimate)} /></Form.Item>
          <Form.Item name="reservedMinutes" label="Thời gian giữ ghế" rules={[{ required: true },
            { validator: async (_, value: number) => { if (!durationRangeValid(addForm.getFieldValue('estimatedMinMinutes'), addForm.getFieldValue('estimatedMaxMinutes'), addForm.getFieldValue('estimatedMinutes'), value)) throw new Error('Kiểm tra khoảng ước tính và thời gian giữ ghế'); } }]}><Select options={durationOptions} /></Form.Item></div>
        {addMax != null && addReserved != null && addReserved < addMax && <><Alert type="warning" showIcon
          message="Giữ ghế ngắn hơn mức ước tính dài nhất." style={{ marginBottom: 12 }} />
          <Form.Item name="acknowledgeShortReservation" valuePropName="checked" rules={[{
            validator: async (_, value: boolean) => { if (!value) throw new Error('Bác sĩ cần xác nhận'); },
          }]}><Checkbox>Tôi đã xem và chấp nhận thời gian giữ ghế này</Checkbox></Form.Item></>}
        <Form.Item name="predecessorIds" label="Bước cần hoàn thành trước"><Select mode="multiple"
          options={(rows ?? []).map(step => ({ value: step.id, label: step.name }))} /></Form.Item>
        <Form.Item name="capability" label="Năng lực bác sĩ"><Input maxLength={160} /></Form.Item>
        <Form.Item name="labRequirement" label="Điều kiện Labo"><Input maxLength={160} /></Form.Item>
        <div className="treatment-scope-grid"><Form.Item name="earliestOffsetDays" label="Quay lại sớm nhất (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="targetOffsetDays" label="Mục tiêu (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="latestOffsetDays" label="Muộn nhất (ngày)"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item></div>
        <Form.Item name="chargingCoverage" label="Phạm vi giá"><Select options={[
          { value: 'IncludedInItem', label: 'Trong giá hạng mục đã đồng ý' },
          { value: 'FinancialReview', label: 'Cần xem xét tài chính riêng' },
        ]} /></Form.Item>
        <Form.Item name="reason" label="Lý do thêm bước" rules={[{ required: true, message: 'Nhập lý do của bác sĩ' }]}>
          <Input.TextArea maxLength={1000} rows={3} /></Form.Item>
      </Form>
    </Drawer>
    <Drawer open={followUpStep !== null} title="Chỉ định thêm buổi" width={520}
      onClose={() => setFollowUpStep(null)} extra={<Button type="primary" loading={busy}
        onClick={() => followUpForm.submit()}>Lưu chỉ định</Button>}>
      <Typography.Paragraph>Chỉ định cho bước <strong>{followUpStep?.name}</strong>. Sau khi lưu, trạng thái là cần đặt lịch; chưa có giờ hẹn được giữ.</Typography.Paragraph>
      {error && <Alert type="error" showIcon message={error.message} style={{ marginBottom: 16 }} />}
      <Form form={followUpForm} layout="vertical" onFinish={values => void createFollowUp(values)}
        onValuesChange={() => { requestKey.current = crypto.randomUUID(); }}>
        <Form.Item name="reason" label="Lý do quay lại" rules={[{ required: true, message: 'Nhập lý do' }]}>
          <Input.TextArea maxLength={2000} rows={3} /></Form.Item>
        <Form.Item name="earliestOn" label="Ngày sớm nhất" rules={[{ required: true }]}><Input type="date" /></Form.Item>
        <Form.Item name="targetOn" label="Ngày mục tiêu" rules={[{ required: true }]}><Input type="date" /></Form.Item>
        <Form.Item name="latestOn" label="Ngày muộn nhất" rules={[{ required: true }]}><Input type="date" /></Form.Item>
        <Typography.Text type="secondary">Bác sĩ và thời gian dự kiến lấy từ hồ sơ/bước; S24 sẽ dùng cùng chỉ định này để tìm giờ.</Typography.Text>
      </Form>
    </Drawer>
  </Card>;
}
