import { Alert, Button, Card, Descriptions, Empty, Form, Input, Modal, Result, Select, Skeleton, Space, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import type { TreatmentChange, TreatmentDossier, TreatmentItem } from './types';
import { decisionLabel, money, stateLabel } from './types';
import FollowUpPanel from '../follow-ups/FollowUpPanel';
import './treatment.css';

const eventLabels: Record<string, string> = {
  DossierCreated: 'Tạo đợt điều trị', PlanDraftSaved: 'Lưu nháp kế hoạch',
  PlanCommitted: 'Chốt kế hoạch', DossierClosed: 'Kết thúc đợt điều trị',
  DossierReopened: 'Mở lại đợt điều trị', WarrantyConfirmed: 'Xác nhận bảo hành',
};
const displayTime = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

export default function DossierPage() {
  const { id, dossierId } = useParams();
  const [dossier, setDossier] = useState<TreatmentDossier | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [action, setAction] = useState<'close' | 'reopen' | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [warrantyItem, setWarrantyItem] = useState<TreatmentItem | null>(null);
  const [warrantyError, setWarrantyError] = useState<string | null>(null);
  const actionKey = useRef(crypto.randomUUID());
  const warrantyKey = useRef(crypto.randomUUID());
  const [form] = Form.useForm<{ outcome: string; reason?: string; dispositions: { itemId: string; reason: string }[] }>();
  const [warrantyForm] = Form.useForm<{ policy: string; policyVersion: string; scope: string; endsOn?: string }>();

  useEffect(() => {
    let active = true;
    setError(null); setDossier(null);
    api<TreatmentDossier>(`/api/dossiers/${dossierId}`)
      .then(result => { if (active) setDossier(result); })
      .catch(failure => { if (active) setError(failure as ApiError); });
    return () => { active = false; };
  }, [dossierId, revision]);

  if (error && !dossier) return <Result status={error.status === 403 ? '403' : error.status === 404 ? '404' : 'error'}
    title={error.message} extra={<Space><Button onClick={() => setRevision(x => x + 1)}>Thử lại</Button>
      <Link to={`/patients/${id}`}>Về hồ sơ bệnh nhân</Link></Space>} />;
  if (!dossier) return <Card><Skeleton active /></Card>;
  const unresolved = dossier.items.filter(item => item.decision === 'Agreed' && item.progress !== 'Completed');

  async function submit(values: { outcome: string; reason?: string; dispositions: { itemId: string; reason: string }[] }) {
    if (!dossier || busy) return;
    setBusy(true); setActionError(null);
    try {
      const command = action === 'close'
        ? { expectedVersion: dossier.version, outcome: values.outcome, reason: values.reason,
          dispositions: values.outcome === 'StoppedIncomplete'
            ? values.dispositions.map(x => ({ ...x, outcome: 'Stopped' })) : [], requestKey: actionKey.current }
        : { expectedVersion: dossier.version, reason: values.reason, requestKey: actionKey.current };
      const result = await api<TreatmentChange>(`/api/dossiers/${dossier.id}/${action}`, {
        method: 'POST', body: JSON.stringify(command),
      });
      setDossier(result.dossier); setNotice(result.notification.message); setAction(null); form.resetFields();
      actionKey.current = crypto.randomUUID();
    } catch (failure) {
      const problem = failure as ApiError;
      setActionError(problem.message);
    } finally { setBusy(false); }
  }

  async function confirmWarranty(values: { policy: string; policyVersion: string; scope: string; endsOn?: string }) {
    if (!dossier || !warrantyItem || busy) return;
    setBusy(true); setWarrantyError(null);
    try {
      const result = await api<TreatmentChange>(`/api/dossiers/${dossier.id}/items/${warrantyItem.id}/warranty/confirm`, {
        method: 'POST', body: JSON.stringify({ ...values, expectedVersion: dossier.version,
          requestKey: warrantyKey.current }),
      });
      setDossier(result.dossier); setNotice(result.notification.message); setWarrantyItem(null);
      warrantyForm.resetFields(); warrantyKey.current = crypto.randomUUID();
    } catch (failure) { setWarrantyError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <main className="treatment-page">
    <Typography.Paragraph><Link to={`/patients/${id}`}>← Hồ sơ bệnh nhân</Link></Typography.Paragraph>
    <div className="treatment-page-heading"><div>
      <Typography.Title level={2}>Đợt điều trị {dossier.code}</Typography.Title>
      <Typography.Paragraph>{dossier.diagnosis}</Typography.Paragraph>
    </div><Tag>{stateLabel[dossier.state]}</Tag></div>
    {notice && <Alert type="success" showIcon className="form-alert" message={notice} />}
    <Card title="Thông tin đợt điều trị" extra={<Space wrap>
      {dossier.state === 'Open' ? <Button onClick={() => { setActionError(null); actionKey.current = crypto.randomUUID(); setAction('close');
        form.setFieldsValue({ outcome: unresolved.length ? 'StoppedIncomplete' : 'CompletedAsAgreed',
          dispositions: unresolved.map(item => ({ itemId: item.id, reason: '' })) }); }}>Kết thúc đợt</Button>
        : <Button onClick={() => { setActionError(null); actionKey.current = crypto.randomUUID(); setAction('reopen'); }}>Mở lại</Button>}
      <Link to={`/patients/${id}/dossiers/${dossier.id}/plan`}><Button type="primary">Mở kế hoạch S21</Button></Link>
    </Space>}>
      <Descriptions column={{ xs: 1, md: 2 }} items={[
        { label: 'Bệnh nhân', children: <Link to={`/patients/${id}`}>Hồ sơ #{dossier.patientId}</Link> },
        { label: 'Chi nhánh', children: dossier.branchId },
        { label: 'Bác sĩ phụ trách', children: dossier.dentistId },
        { label: 'Ngày bắt đầu', children: dossier.startedOn },
        { label: 'Trạng thái lâm sàng', children: stateLabel[dossier.state] },
        { label: 'Kết quả đóng', children: dossier.closureOutcome ?? 'Chưa đóng' },
        { label: 'Phiên bản', children: dossier.version },
        { label: 'Kế hoạch', children: dossier.planVersion ? `Phiên bản ${dossier.planVersion}` : 'Chưa chốt' },
      ]} />
      {dossier.note && <Typography.Paragraph>Ghi chú: {dossier.note}</Typography.Paragraph>}
    </Card>
    <div className="treatment-detail-grid">
      <Card title="Kế hoạch và việc tiếp theo" extra={<Link to={`/patients/${id}/dossiers/${dossier.id}/plan`}>Xem S21</Link>}>
        {dossier.items.length === 0 ? <Empty description="Chưa có hạng mục kế hoạch." />
          : <div className="treatment-item-stack">{dossier.items.map((item: TreatmentItem) =>
            <div key={item.id} className="treatment-item-row"><div><strong>{item.serviceName}</strong>
              <div>{item.region ? `Răng/vùng ${item.region} · ` : ''}{item.quantity} {item.unit} · {money(item.agreedUnitPriceVnd)}</div></div>
              <div><Tag>{decisionLabel[item.decision]}</Tag><Tag>{item.progress}</Tag>
                {item.progress === 'Completed' && !item.warrantyConfirmedAt && <Button size="small" onClick={() => {
                  warrantyKey.current = crypto.randomUUID(); setWarrantyError(null); setWarrantyItem(item);
                }}>Xác nhận bảo hành</Button>}
                {item.warrantyConfirmedAt && <Tag color="green">Bảo hành đã xác nhận</Tag>}</div></div>)}</div>}
      </Card>
      <Card title="Tài chính">
        {dossier.financialSummaryAvailable
          ? <Typography.Text>Đọc số liệu đã ghi nhận tại màn hình hóa đơn.</Typography.Text>
          : <Alert type="info" showIcon message="Chưa có dữ liệu tài chính từ nguồn đối soát"
              description="Kế hoạch và các mốc thanh toán không phải khoản đã thu. Số dư sẽ hiển thị khi module tài chính cung cấp dữ liệu chính thức." />}
      </Card>
    </div>
    <FollowUpPanel dossier={dossier} />
    <Card title="Dòng thời gian" className="treatment-timeline">
      {dossier.timeline.length === 0 ? <Empty description="Chưa có sự kiện chính thức." />
        : <Timeline items={dossier.timeline.map(event => ({ key: event.id,
          children: <div><strong>{eventLabels[event.kind] ?? event.kind}</strong>
            <div>{displayTime(event.occurredAt)} · Phiên bản {event.dossierVersion}</div></div>,
        }))} />}
      <Typography.Text type="secondary">Lượt khám, actual và review sẽ xuất hiện khi các nguồn lâm sàng được kết nối.</Typography.Text>
    </Card>
    <Modal open={action !== null} title={action === 'close' ? 'Kết thúc đợt điều trị' : 'Mở lại đợt điều trị'}
      okText={action === 'close' ? 'Xác nhận kết thúc' : 'Mở lại'} okButtonProps={{ loading: busy }}
      onOk={() => form.submit()} onCancel={() => { if (!busy) setAction(null); }}>
      {actionError && <Alert type="error" showIcon className="form-alert" message={actionError} />}
      <Form form={form} layout="vertical" onValuesChange={() => { actionKey.current = crypto.randomUUID(); }}
        onFinish={values => void submit(values)}>
        {action === 'close' && <><Form.Item name="outcome" label="Kết quả lâm sàng" rules={[{ required: true }]}>
          <Select options={[
            { value: 'CompletedAsAgreed', label: 'Hoàn tất đúng phần đã đồng ý', disabled: unresolved.length > 0 },
            { value: 'StoppedIncomplete', label: 'Dừng khi còn phần chưa thực hiện' },
          ]} /></Form.Item>
          {unresolved.map((item, index) => <div key={item.id}>
            <Typography.Text strong>{item.serviceName} · {item.region ?? 'Chưa chọn vùng'}</Typography.Text>
            <Form.Item name={['dispositions', index, 'itemId']} hidden><Input /></Form.Item>
            <Form.Item name={['dispositions', index, 'reason']} label="Lý do dừng hạng mục"
              rules={[{ required: true, whitespace: true, message: 'Ghi lý do cho hạng mục chưa hoàn tất.' }]}>
              <Input.TextArea rows={2} maxLength={1000} /></Form.Item>
          </div>)}
        </>}
        <Form.Item name="reason" label="Lý do/ghi chú" rules={action === 'reopen' || unresolved.length > 0
          ? [{ required: true, whitespace: true, message: 'Vui lòng ghi lý do.' }] : []}>
          <Input.TextArea rows={2} maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
    <Modal open={warrantyItem !== null} title={`Xác nhận bảo hành · ${warrantyItem?.serviceName ?? ''}`}
      okText="Xác nhận bảo hành" okButtonProps={{ loading: busy }} onOk={() => warrantyForm.submit()}
      onCancel={() => { if (!busy) setWarrantyItem(null); }}>
      {warrantyError && <Alert type="error" showIcon message={warrantyError} className="form-alert" />}
      <Form form={warrantyForm} layout="vertical" onValuesChange={() => { warrantyKey.current = crypto.randomUUID(); }}
        onFinish={values => void confirmWarranty(values)}>
        <Form.Item name="policy" label="Chính sách bảo hành" rules={[{ required: true, whitespace: true }]}>
          <Input maxLength={160} /></Form.Item>
        <Form.Item name="policyVersion" label="Phiên bản chính sách" rules={[{ required: true, whitespace: true }]}>
          <Input maxLength={40} /></Form.Item>
        <Form.Item name="scope" label="Phạm vi bảo hành" rules={[{ required: true, whitespace: true }]}>
          <Input.TextArea rows={3} maxLength={1000} /></Form.Item>
        <Form.Item name="endsOn" label="Ngày kết thúc (nếu có)"><Input type="date" /></Form.Item>
      </Form>
    </Modal>
  </main>;
}
