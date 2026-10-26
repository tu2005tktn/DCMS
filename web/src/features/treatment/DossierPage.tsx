import { Alert, Button, Card, Descriptions, Empty, Form, Input, InputNumber, Modal, Result, Select, Skeleton, Space, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { api, type ApiError } from '../../services/api';
import type { TreatmentChange, TreatmentDossier, TreatmentItem } from './types';
import { decisionLabel, money, stateLabel } from './types';
import './treatment.css';

const eventLabels: Record<string, string> = {
  DossierCreated: 'Tạo đợt điều trị', PlanDraftSaved: 'Lưu nháp kế hoạch',
  PlanCommitted: 'Chốt kế hoạch', DossierClosed: 'Kết thúc đợt điều trị',
  DossierReopened: 'Mở lại đợt điều trị', WarrantyConfirmed: 'Xác nhận bảo hành',
  PlanRevised: 'Điều chỉnh kế hoạch điều trị',
};
const displayTime = (value: string) => new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(value));

export default function DossierPage() {
  const { id, dossierId } = useParams();
  const [search] = useSearchParams();
  const currentVisitId = search.get('visitId');
  const location = useLocation();
  const selectedItemId = search.get('item');
  const revisionNotice = (location.state as { revisionNotice?: string } | null)?.revisionNotice;
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
  const [warrantyForm] = Form.useForm<{ policy: string; policyVersion: string; scope: string;
    component: string; exclusions?: string; activationOn: string; endsOn?: string;
    explanation: string; expectedServiceLifeMonths?: number }>();

  useEffect(() => {
    let active = true;
    setError(null); setDossier(null);
    api<TreatmentDossier>(`/api/dossiers/${dossierId}`)
      .then(result => { if (active) setDossier(result); })
      .catch(failure => { if (active) setError(failure as ApiError); });
    return () => { active = false; };
  }, [dossierId, revision]);
  useEffect(() => {
    if (!dossier || !selectedItemId) return;
    const frame = requestAnimationFrame(() => document.querySelector('.treatment-selected-item')
      ?.scrollIntoView({ block: 'center' }));
    return () => cancelAnimationFrame(frame);
  }, [dossier, selectedItemId]);

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

  async function confirmWarranty(values: { policy: string; policyVersion: string; scope: string;
    component: string; exclusions?: string; activationOn: string; endsOn?: string;
    explanation: string; expectedServiceLifeMonths?: number }) {
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
    {currentVisitId && <Space><Link to={`/visits/${currentVisitId}`}>Về lượt khám hiện tại</Link>
      <Link to={`/patients/${id}/visits/${currentVisitId}/treatment?dossierId=${dossier.id}`}>Điều trị trong lượt này</Link></Space>}
    <Typography.Paragraph><Link to={`/patients/${id}`}>← Hồ sơ bệnh nhân</Link></Typography.Paragraph>
    <div className="treatment-page-heading"><div>
      <Typography.Title level={2}>Đợt điều trị {dossier.code}</Typography.Title>
      <Typography.Paragraph>{dossier.diagnosis}</Typography.Paragraph>
    </div><Tag>{stateLabel[dossier.state]}</Tag></div>
    {(notice || revisionNotice) && <Alert type="success" showIcon className="form-alert"
      message={notice || revisionNotice} />}
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
            <div key={item.id} className={`treatment-item-row${item.id === selectedItemId ? ' treatment-selected-item' : ''}`}>
              <div><strong>{item.serviceName}</strong>
              <div>{item.region ? `Răng/vùng ${item.region} · ` : ''}{item.quantity} {item.unit} · {money(item.agreedUnitPriceVnd)}</div>
              {item.warrantyConfirmedAt && <div className="treatment-muted">Bảo hành {item.warrantyComponent || item.warrantyScope}
                {item.warrantyExclusions ? ` · Loại trừ: ${item.warrantyExclusions}` : ''}
                {item.warrantyEndsOn ? ` · Đến ${item.warrantyEndsOn}` : ''}</div>}
              {item.expectedServiceLifeMonths && <div className="treatment-muted">Tuổi thọ dự kiến: {item.expectedServiceLifeMonths} tháng (khác thời hạn bảo hành)</div>}</div>
              <div><Tag>{decisionLabel[item.decision]}</Tag><Tag>{item.progress}</Tag>
                {dossier.planState === 'Committed' && dossier.state === 'Open' &&
                  <Link to={`/patients/${id}/dossiers/${dossier.id}/plan/revise/${item.id}?from=dossier`}>
                    <Button size="small">Điều chỉnh kế hoạch</Button></Link>}
                {item.progress === 'Completed' && !item.warrantyConfirmedAt && <Button size="small" onClick={() => {
                  warrantyKey.current = crypto.randomUUID(); setWarrantyError(null); setWarrantyItem(item);
                  warrantyForm.setFieldsValue({ activationOn: new Intl.DateTimeFormat('sv-SE', {
                    timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit',
                  }).format(new Date()) });
                }}>Xác nhận bảo hành</Button>}
                {item.warrantyConfirmedAt && <Tag color="green">Bảo hành đã xác nhận</Tag>}</div></div>)}</div>}
      </Card>
      <Card title="Kế hoạch và đối soát tài chính">
        <Descriptions column={1} items={[{ label: 'Tổng dự kiến trước thuế',
          children: money(dossier.financialSummary?.plannedVnd ?? dossier.items
            .filter(item => item.decision === 'Agreed').reduce((sum, item) =>
              sum + Math.round(item.quantity * item.agreedUnitPriceVnd)
                - (item.discount?.status === 'Approved' ? item.discount.amountVnd : 0), 0)) }]} />
        {dossier.financialSummary?.available
          ? <Descriptions column={1} items={[
            { label: 'Khoản đã ghi nhận', children: money(dossier.financialSummary.postedChargeVnd ?? 0) },
            { label: 'Đã thu', children: money(dossier.financialSummary.receivedVnd ?? 0) },
            { label: 'Đã phân bổ', children: money(dossier.financialSummary.allocatedVnd ?? 0) },
            { label: 'Còn nợ', children: money(dossier.financialSummary.outstandingVnd ?? 0) },
          ]} />
          : <Alert type="info" showIcon message="Chưa có dữ liệu tài chính từ nguồn đối soát"
              description="Giá kế hoạch và mốc thanh toán không phải khoản đã thu. Số đã ghi nhận, đã thu và còn nợ sẽ hiển thị khi DCMS-101 cung cấp dữ liệu." />}
        {dossier.depositExceptionReason && <Typography.Paragraph>Đã duyệt ngoại lệ không cọc: {dossier.depositExceptionReason}</Typography.Paragraph>}
      </Card>
    </div>
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
        <Form.Item name="component" label="Bộ phận được bảo hành" rules={[{ required: true, whitespace: true }]}>
          <Input maxLength={500} placeholder="Ví dụ: mão sứ răng 25" /></Form.Item>
        <Form.Item name="exclusions" label="Trường hợp không được bảo hành">
          <Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Form.Item name="activationOn" label="Ngày bắt đầu bảo hành" rules={[{ required: true }]}>
          <Input type="date" /></Form.Item>
        <Form.Item name="endsOn" label="Ngày kết thúc (nếu có)"><Input type="date" /></Form.Item>
        <Form.Item name="expectedServiceLifeMonths" label="Tuổi thọ dự kiến (tháng, nếu có)">
          <InputNumber min={1} max={1200} className="treatment-number-full" /></Form.Item>
        <Form.Item name="explanation" label="Nội dung đã giải thích cho bệnh nhân"
          rules={[{ required: true, whitespace: true }]}><Input.TextArea rows={3} maxLength={2000} /></Form.Item>
      </Form>
    </Modal>
  </main>;
}
