import InvestigationBillingDrawer from '../finance/InvestigationBillingDrawer';
import {useAuth} from '../authentication/AuthContext';
﻿import { Alert, AutoComplete, Button, Card, Checkbox, Empty, Form, Input, Modal, Select, Space, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import UploadDrawer from '../clinical-documents/UploadDrawer';
import DocumentDetailDrawer from '../clinical-documents/DocumentDetailDrawer';
import { defaultConstraints, fileStateLabels, formatStamp, type PatientDocumentContexts } from '../clinical-documents/types';
import type { Investigation, InvestigationPage, Workspace } from './types';

const stateLabels = { Ordered: 'Chờ kết quả', ResultAvailable: 'Chờ bác sĩ đọc', Reviewed: 'Bác sĩ đã đọc', Cancelled: 'Đã hủy' };
const kindLabels = { XRay: 'Chụp phim răng hàm mặt', Imaging: 'Chẩn đoán hình ảnh khác', Lab: 'Xét nghiệm', Pathology: 'Giải phẫu bệnh / tế bào học', Other: 'Khác' };
const techniqueNames: Record<string, string[]> = {
  XRay: ['Phim quanh chóp (cận chóp)', 'Phim cánh cắn (Bitewing)', 'Phim mặt nhai (Occlusal)', 'Phim toàn cảnh (Panorama / OPG)', 'Phim sọ nghiêng (Cephalometric)', 'Phim sọ thẳng', 'CBCT vùng răng / hàm'],
  Imaging: ['Siêu âm tuyến nước bọt', 'Siêu âm vùng hàm mặt', 'MRI khớp thái dương hàm', 'CT hàm mặt'],
  Lab: ['Công thức máu', 'Đường huyết', 'HbA1c', 'PT / INR', 'aPTT', 'Chức năng gan (AST / ALT)', 'Chức năng thận (creatinin / eGFR)', 'Vi sinh: nuôi cấy và kháng sinh đồ'],
  Pathology: ['Mô bệnh học mẫu sinh thiết', 'Tế bào học tổn thương miệng'],
  Other: [],
};
type Values = { kind: string; name: string; region?: string; reason: string; requiredForDiagnosis: boolean; amendmentReason?: string; resultText?: string; interpretation?: string };
type Mode = { action: 'create' | 'result' | 'review' | 'cancel'; order?: Investigation };
export default function InvestigationsPanel({ workspace, contexts, busy, onBusy, onChanged, amending = false, amendmentReason, onAmendmentReason }: {
  amending?: boolean; amendmentReason?: string; onAmendmentReason?: (reason: string) => void;
  workspace: Workspace; contexts: PatientDocumentContexts | null; busy: boolean; onBusy: (busy: boolean) => void;
  onChanged: (page: InvestigationPage, mutation: boolean) => Promise<void>;
}) {
  const {user}=useAuth(); const [billingOrder,setBillingOrder]=useState<Investigation>();
  const [form] = Form.useForm<Values>();
  const [mode, setMode] = useState<Mode>(), [upload, setUpload] = useState<string>(), [documentId, setDocumentId] = useState<string | null>(null);
  const [error, setError] = useState<string>(), [working, setWorking] = useState(false);
  const active = useRef(true), pending = useRef<{ fingerprint: string; key: string } | undefined>(undefined);
  const orders = workspace.investigations ?? [];
  const visitId = workspace.visit.visitId;
  const amendmentOrder = amending && workspace.examination?.confirmed === true && workspace.visit.status === 'InProgress' && workspace.allowedActions.includes('amend');
  const canCreate = workspace.allowedActions.includes('save') || amendmentOrder;
  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);
  async function load(mutation = false) {
    try { const page = await api<InvestigationPage>(`/api/visits/${visitId}/investigations`); if (active.current) await onChanged(page, mutation); }
    catch (failure) { if (active.current) setError((failure as Error).message); }
  }
  const pendingScan = orders.some(order => order.documents.some(file => file.fileState === 'PendingScan'));
  const polls = useRef(0);
  useEffect(() => {
    if (!pendingScan || polls.current >= 24 || busy) return;
    const timer = window.setTimeout(() => { polls.current++; void load(); }, 5000);
    return () => window.clearTimeout(timer);
  }, [orders, pendingScan, busy]);
  function open(action: Mode['action'], order?: Investigation) {
    setError(undefined); pending.current = undefined; form.resetFields();
    form.setFieldsValue({ amendmentReason, kind: 'XRay', requiredForDiagnosis: true, resultText: order?.resultText ?? '', interpretation: '' });
    setMode({ action, order });
  }
  async function submit() {
    if (!mode || busy || working) return;
    let values: Values; try { values = await form.validateFields(); } catch { return; }
    const order = mode.order;
    const body = { patientId: workspace.visit.patientId, expectedVisitVersion: workspace.visit.visitVersion,
      ...(order ? { expectedVersion: order.version } : {}),
      ...(mode.action === 'create' ? { ...(amendmentOrder ? { expectedExaminationVersion: workspace.examination?.version, amendmentReason: values.amendmentReason } : {}), kind: values.kind, name: values.name, region: values.region || null, reason: values.reason, requiredForDiagnosis: values.requiredForDiagnosis }
        : mode.action === 'result' ? { resultText: values.resultText || null } : mode.action === 'review' ? { interpretation: values.interpretation || null } : { reason: values.reason }) };
    const fingerprint = JSON.stringify({ action: mode.action, order: order?.id, body });
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: crypto.randomUUID() };
    setWorking(true); onBusy(true); setError(undefined);
    try {
      const page = await api<InvestigationPage>(`/api/visits/${visitId}/investigations${order ? `/${order.id}/${mode.action}` : ''}`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      if (!active.current) return;
      await onChanged(page, true); if(mode.action === 'create' && amendmentOrder) onAmendmentReason?.(values.amendmentReason!.trim()); pending.current = undefined; setMode(undefined);
    } catch (failure) { if (active.current) setError((failure as ApiError).code === 'MSG111' ? 'Lượt hoặc chỉ định đã thay đổi. Bản nhập vẫn còn; dùng Tải lại phiên bản ở đầu trang trước khi thử lại.' : (failure as Error).message); }
    finally { if (active.current) { setWorking(false); onBusy(false); } }
  }
  const selectedKind = Form.useWatch('kind', form) ?? 'XRay';
  const names = techniqueNames[selectedKind] ?? [];
  return <section id="exam-investigations" tabIndex={-1} aria-label="Chỉ định cận lâm sàng" className="investigation-panel">
    <Space wrap className="investigation-toolbar"><Typography.Title level={4}>Cận lâm sàng</Typography.Title>
      <Button disabled={busy} onClick={() => { polls.current = 0; void load(); }}>Tải lại chỉ định</Button>
      {canCreate && <Button type="primary" disabled={busy} onClick={() => open('create')}>Thêm chỉ định</Button>}
    </Space>
    {error && !mode && <Alert role="alert" type="error" showIcon message={error} />}
    {workspace.examination?.confirmed && workspace.visit.status === 'Completed' && <Alert type="info" message="Lượt khám đã kết thúc. Có thể đính chính hồ sơ cũ; cần tạo lượt khám mới để lập chỉ định mới." />}
    {workspace.examination?.confirmed && workspace.visit.status === 'InProgress' && !amending && workspace.allowedActions.includes('amend') && <Alert type="info" message="Để bổ sung chỉ định, vào Tổng kết khám và bấm Đính chính kết quả." />}
    {amendmentOrder && <Alert type="info" message="Chỉ định bổ sung được lưu ngay. Hủy đính chính không tự hủy chỉ định đã tạo." />}
    {!orders.length && <Empty description="Chưa có chỉ định. Tạo khi bác sĩ cần thêm kết quả để đánh giá." />}
    {orders.map(order => <Card key={order.id} size="small" className="investigation-card" title={<Space wrap><span>{order.name}</span><Tag color={order.state === 'Reviewed' ? 'green' : order.state === 'Cancelled' ? 'default' : 'gold'}>{stateLabels[order.state]}</Tag>{order.requiredForDiagnosis && order.state !== 'Cancelled' && <Tag>Cần để kết luận</Tag>}</Space>}>
      <Typography.Text type="secondary">{kindLabels[order.kind]}{order.region ? ` · ${order.region}` : ''} · {order.createdByName} · {formatStamp(order.createdAt)}</Typography.Text>
      <p>Lý do: {order.reason}</p>
      {order.resultText && <div className="clinical-result-text"><strong>Kết quả</strong><p>{order.resultText}</p></div>}
      {order.resultAt && <p><Typography.Text type="secondary">Nhận bởi {order.resultByName} · {formatStamp(order.resultAt)}</Typography.Text></p>}
      {order.documents.map(file => <div key={file.documentId}><Button type="link" onClick={() => setDocumentId(file.documentId)}>{file.fileName}</Button><Tag>{fileStateLabels[file.fileState]}</Tag>{order.resultDocumentIds.includes(file.documentId) && <Tag color="blue">Thuộc kết quả đã nhận</Tag>}</div>)}
      {order.interpretation && <p>Nhận định: {order.interpretation}</p>}
      {order.reviewedAt && <p>Đã đọc: {order.reviewedByName} · {formatStamp(order.reviewedAt)}</p>}
      {order.cancellationReason && <p>Lý do hủy: {order.cancellationReason}</p>}
      <Space wrap>
        {user?.permissions.includes('finance.read') && <Button disabled={busy} onClick={()=>setBillingOrder(order)}>Dịch vụ và phí</Button>}
        {order.allowedActions.includes('upload') && <Button disabled={busy} onClick={() => setUpload(order.id)}>Tải tệp kết quả</Button>}
        {order.allowedActions.includes('result') && <Button disabled={busy} onClick={() => open('result', order)}>Nhận kết quả</Button>}
        {order.allowedActions.includes('review') && <Button type="primary" disabled={busy} onClick={() => open('review', order)}>Đọc kết quả</Button>}
        {order.allowedActions.includes('cancel') && <Button disabled={busy} onClick={() => open('cancel', order)}>Hủy chỉ định</Button>}
      </Space>
    </Card>)}
    <Modal open={!!mode} destroyOnClose title={mode?.action === 'create' ? 'Tạo chỉ định cận lâm sàng' : mode?.action === 'result' ? 'Nhận kết quả cận lâm sàng' : mode?.action === 'review' ? 'Bác sĩ đọc kết quả' : 'Hủy chỉ định cận lâm sàng'}
      okText={mode?.action === 'review' ? 'Xác nhận đã đọc kết quả' : mode?.action === 'create' ? 'Tạo chỉ định' : mode?.action === 'cancel' ? 'Xác nhận hủy chỉ định' : 'Lưu kết quả'} cancelText="Đóng" confirmLoading={working}
      okButtonProps={{ disabled: busy && !working }} cancelButtonProps={{ disabled: working }} onCancel={() => { if (!working) setMode(undefined); }} onOk={() => void submit()}>
      {error && mode && <Alert role="alert" type="error" message={error} />}
      <Form form={form} layout="vertical" disabled={working} preserve={false} initialValues={{ kind: 'XRay', requiredForDiagnosis: true, amendmentReason }}>
        {mode?.action === 'create' && <>
          {amendmentOrder && <Form.Item label="Lý do bổ sung sau xác nhận khám" name="amendmentReason" rules={[{required:true,whitespace:true,max:1000}]}><Input.TextArea aria-label="Lý do bổ sung sau xác nhận khám" maxLength={1000} /></Form.Item>}
          <Form.Item label="Nhóm chỉ định" name="kind" rules={[{ required: true }]}><Select aria-label="Nhóm chỉ định" onChange={() => form.setFieldValue('name', '')} options={Object.entries(kindLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
          <Form.Item label="Tên chỉ định" name="name" extra="Chọn kỹ thuật cụ thể hoặc nhập tên khác; chỉ tạo khi bác sĩ có chỉ định." rules={[{ required: true, whitespace: true, max: 160 }]}><AutoComplete style={{ width: '100%' }} aria-label="Tên chỉ định" placeholder="Tìm hoặc nhập tên kỹ thuật cụ thể" options={names.map(value => ({ value }))} filterOption={(input, option) => (option?.value ?? '').toLocaleLowerCase('vi').includes(input.toLocaleLowerCase('vi'))} /></Form.Item>
          <Form.Item label="Răng / vùng" name="region" rules={[{ max: 200 }]}><Input aria-label="Răng / vùng chỉ định" /></Form.Item>
          <Form.Item label="Lý do chỉ định" name="reason" rules={[{ required: true, whitespace: true, max: 1000 }]}><Input.TextArea aria-label="Lý do chỉ định" maxLength={1000} /></Form.Item>
          <Form.Item name="requiredForDiagnosis" valuePropName="checked"><Checkbox>Cần bác sĩ đọc kết quả trước khi hoàn tất khám</Checkbox></Form.Item>
        </>}
        {mode?.action === 'result' && <>
          <Typography.Paragraph>{mode.order?.name} · {mode.order?.region}</Typography.Paragraph>
          <Form.Item label="Nội dung kết quả" name="resultText" extra="Ghi nội dung hoặc sử dụng tệp sẵn sàng đã tải vào chỉ định." rules={[{ max: 4000 }]}><Input.TextArea aria-label="Nội dung kết quả" rows={4} maxLength={4000} /></Form.Item>
          {mode.order?.documents.some(file => file.fileState !== 'Ready') && <Alert type="info" message="Tệp đang kiểm tra hoặc bị từ chối chưa được dùng làm bằng chứng kết quả. Nội dung văn bản vẫn có thể được ghi nhận riêng." />}
        </>}
        {mode?.action === 'review' && <>
          <Typography.Paragraph strong>{mode.order?.name}</Typography.Paragraph><Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{mode.order?.resultText || 'Kết quả nằm trong các tệp đã nhận.'}</Typography.Paragraph>
          {mode.order?.documents.filter(file => mode.order?.resultDocumentIds.includes(file.documentId)).map(file => <Button key={file.documentId} type="link" onClick={() => setDocumentId(file.documentId)}>{file.fileName}</Button>)}
          <Form.Item label="Nhận định của bác sĩ (nếu có)" name="interpretation" rules={[{ max: 4000 }]}><Input.TextArea aria-label="Nhận định của bác sĩ" rows={3} maxLength={4000} /></Form.Item>
        </>}
        {mode?.action === 'cancel' && <><Alert type="info" message="Nếu dịch vụ đã thực hiện, hủy chỉ định không tự xóa phí, khoản phải trả hoặc phiếu thu."/><Form.Item label="Lý do hủy" name="reason" rules={[{ required: true, whitespace: true, max: 1000 }]}><Input.TextArea aria-label="Lý do hủy chỉ định" maxLength={1000} /></Form.Item></>}
      </Form>
    </Modal>
    {billingOrder&&<InvestigationBillingDrawer key={billingOrder.id} workspace={workspace} order={billingOrder} onClose={()=>setBillingOrder(undefined)} onChanged={()=>load(true)}/>}
    <UploadDrawer key={upload ?? 'none'} open={!!upload} patientId={workspace.visit.patientId} contexts={contexts} initialContext={{ visitId, investigationId: upload, investigationLabel: orders.find(order => order.id === upload)?.name, documentType: orders.find(order => order.id === upload)?.kind === 'XRay' ? 'XRay' : ['Lab', 'Pathology'].includes(orders.find(order => order.id === upload)?.kind ?? '') ? 'LabResult' : 'Other' }} constraints={defaultConstraints}
      lockedVisitId={visitId} onClose={() => setUpload(undefined)} onUploaded={() => { polls.current = 0; void load(true); }} />
    <DocumentDetailDrawer documentId={documentId} onClose={() => setDocumentId(null)} />
  </section>;
}
