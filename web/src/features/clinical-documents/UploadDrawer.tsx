import { Alert, Button, Drawer, Form, Input, Modal, Progress, Select, Space, Table, Tag, Typography, Upload } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import { documentsApi } from './documents.api';
import {
  documentTypeLabels, fileStateColors, fileStateLabels, formatBytes, newRequestKey, sourceLabels,
  type DocumentConstraints, type DocumentItem, type DocumentSource, type DocumentType, type PatientDocumentContexts,
} from './types';

export type UploadContext = { visitId?: string; dossierId?: string; planId?: string; investigationId?: string; consentVersion?: string };
type Row = {
  key: string; file: File; requestKey: string; status: 'ready' | 'uploading' | 'done' | 'failed' | 'invalid';
  progress: number; error?: string; result?: DocumentItem;
};
type Values = { documentType: DocumentType; source: DocumentSource; sourceDate: string; description?: string; visitId?: string; dossierId?: string; planId?: string };

const today = () => new Date(Date.now() + 7 * 3_600_000).toISOString().slice(0, 10);
const extensionTypes: Record<string, string> = { jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png', pdf: 'application/pdf' };

// Early client checks only; the server decides on content signature and scan.
function clientError(file: File, c: DocumentConstraints) {
  const extensionType = extensionTypes[file.name.split('.').pop()?.toLowerCase() ?? ''];
  if (!extensionType || !c.contentTypes.includes(file.type || extensionType)) return 'Chỉ nhận JPEG, PNG hoặc PDF.';
  if (file.size > c.maxBytes) return `Tệp vượt quá ${formatBytes(c.maxBytes)}.`;
  if (file.size === 0) return 'Tệp rỗng.';
  return undefined;
}

// S20 upload form. Every file is its own request with its own requestKey, progress and result, so one failure never
// marks the others as failed and a retry after a lost connection cannot create a duplicate.
export default function UploadDrawer({ open, patientId, contexts, initialContext, constraints, onClose, onUploaded }: {
  open: boolean; patientId: number; contexts: PatientDocumentContexts | null; initialContext: UploadContext;
  constraints: DocumentConstraints; onClose: () => void; onUploaded: () => void;
}) {
  const [form] = Form.useForm<Values>();
  const [modal, modalContext] = Modal.useModal();
  const [rows, setRows] = useState<Row[]>([]);
  const [selectionError, setSelectionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const aborts = useRef(new Map<string, AbortController>());

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({ documentType: 'XRay', source: 'Clinic', sourceDate: today(), visitId: initialContext.visitId,
      dossierId: initialContext.dossierId, planId: initialContext.planId });
    setRows([]); setSelectionError(null);
  }, [open]);

  const pending = rows.filter(r => r.status === 'ready' || r.status === 'failed');
  const uploading = rows.some(r => r.status === 'uploading');
  const done = rows.filter(r => r.status === 'done').length;
  const failed = rows.filter(r => r.status === 'failed').length;

  function add(files: File[]) {
    setSelectionError(null);
    const room = constraints.maxFiles - rows.length;
    if (files.length > room) setSelectionError(`Mỗi lần tối đa ${constraints.maxFiles} tệp. ${files.length - Math.max(room, 0)} tệp đã không được thêm.`);
    const accepted = files.slice(0, Math.max(room, 0)).map<Row>(file => {
      const error = clientError(file, constraints);
      return { key: newRequestKey(), file, requestKey: newRequestKey(), status: error ? 'invalid' : 'ready', progress: 0, error };
    });
    setRows(previous => [...previous, ...accepted]);
  }

  const update = (key: string, patch: Partial<Row>) => setRows(previous => previous.map(r => r.key === key ? { ...r, ...patch } : r));

  async function send(row: Row, values: Values) {
    const controller = new AbortController();
    aborts.current.set(row.key, controller);
    update(row.key, { status: 'uploading', progress: 0, error: undefined });
    try {
      const result = await documentsApi.upload({
        patientId, visitId: values.visitId ?? null, dossierId: values.dossierId ?? null, planId: values.planId ?? null,
        investigationId: initialContext.investigationId ?? null, consentVersion: initialContext.consentVersion ?? null,
        documentType: values.documentType, source: values.source, sourceDate: values.sourceDate, description: values.description?.trim() ?? '',
        requestKey: row.requestKey,
      }, row.file, progress => update(row.key, { progress }), controller.signal);
      update(row.key, { status: 'done', progress: 100, result });
    } catch (failure) {
      // The same requestKey is kept for retry, so the server returns the earlier result instead of a second copy.
      update(row.key, { status: 'failed', error: (failure as ApiError).message });
    } finally { aborts.current.delete(row.key); }
  }

  async function submit() {
    let values: Values;
    try { values = await form.validateFields(); } catch { return; }
    setBusy(true);
    try {
      // Two at a time keeps progress readable without one slow file blocking the rest.
      const queue = [...pending];
      await Promise.all([0, 1].map(async () => { for (let next = queue.shift(); next; next = queue.shift()) await send(next, values); }));
      onUploaded();
    } finally { setBusy(false); }
  }

  function close() {
    if (!uploading && pending.length === 0) { onClose(); return; }
    modal.confirm({
      title: uploading ? 'Đang tải lên. Hủy các tệp đang tải?' : 'Còn tệp chưa tải lên. Bỏ lựa chọn?',
      okText: 'Bỏ và đóng', cancelText: 'Ở lại', okButtonProps: { danger: true },
      onOk: () => { aborts.current.forEach(c => c.abort()); onClose(); },
    });
  }

  return <Drawer open={open} onClose={close} width={760} destroyOnClose title="Tải tài liệu lên (S20)"
    extra={<Space><Button onClick={close}>Đóng</Button>
      <Button type="primary" loading={busy} disabled={pending.length === 0 || uploading} onClick={() => void submit()}>
        {failed > 0 && pending.every(r => r.status === 'failed') ? `Thử lại ${failed} tệp lỗi` : `Tải lên ${pending.length} tệp`}</Button></Space>}>
    {modalContext}
    <Form<Values> form={form} layout="vertical" disabled={busy}>
      <div className="patient-form-grid">
        <Form.Item name="documentType" label="Loại tài liệu" rules={[{ required: true, message: 'Vui lòng nhập Loại tài liệu.' }]}>
          <Select options={(Object.keys(documentTypeLabels) as DocumentType[]).map(v => ({ value: v, label: documentTypeLabels[v] }))} /></Form.Item>
        <Form.Item name="source" label="Nguồn" rules={[{ required: true, message: 'Vui lòng nhập Nguồn.' }]}>
          <Select options={(Object.keys(sourceLabels) as DocumentSource[]).map(v => ({ value: v, label: sourceLabels[v] }))} /></Form.Item>
        <Form.Item name="sourceDate" label="Ngày nguồn" rules={[{ required: true, message: 'Vui lòng nhập Ngày nguồn.' }]}>
          <Input type="date" max={today()} /></Form.Item>
        <Form.Item name="visitId" label="Lượt khám"><Select allowClear placeholder="Không gắn lượt khám"
          options={contexts?.visits.map(v => ({ value: v.id, label: v.label }))} /></Form.Item>
        <Form.Item name="dossierId" label="Hồ sơ điều trị"><Select allowClear placeholder="Không gắn hồ sơ"
          options={contexts?.dossiers.map(d => ({ value: d.id, label: d.label }))} /></Form.Item>
        <Form.Item name="planId" label="Kế hoạch"><Select allowClear placeholder="Không gắn kế hoạch"
          options={contexts?.plans.map(p => ({ value: p.id, label: p.label }))} /></Form.Item>
      </div>
      {(initialContext.investigationId || initialContext.consentVersion) && <Typography.Paragraph type="secondary">
        Ngữ cảnh được truyền vào: {initialContext.investigationId ? `chỉ định cận lâm sàng ${initialContext.investigationId}` : ''}
        {initialContext.consentVersion ? ` · phiên bản đồng thuận ${initialContext.consentVersion}` : ''}</Typography.Paragraph>}
      <Form.Item name="description" label={`Mô tả (tối đa ${constraints.descriptionMax} ký tự)`}
        rules={[{ max: constraints.descriptionMax, message: `Mô tả không được vượt quá ${constraints.descriptionMax} ký tự.` }]}>
        <Input.TextArea rows={2} maxLength={constraints.descriptionMax} showCount /></Form.Item>
    </Form>
    <Upload.Dragger multiple showUploadList={false} accept=".jpg,.jpeg,.png,.pdf" disabled={busy || rows.length >= constraints.maxFiles}
      beforeUpload={(_, list) => { if (list[0] && _ === list[0]) add(list as File[]); return false; }}>
      <p>Kéo thả hoặc bấm để chọn tệp</p>
      <Typography.Text type="secondary">JPEG, PNG, PDF · tối đa {constraints.maxFiles} tệp, {formatBytes(constraints.maxBytes)}/tệp · đã chọn {rows.length}</Typography.Text>
    </Upload.Dragger>
    {selectionError && <Alert className="patient-section" type="warning" showIcon message={selectionError} />}
    {rows.length > 0 && <Table<Row> className="patient-section" size="small" rowKey="key" pagination={false} dataSource={rows} columns={[
      { title: 'Tệp', render: (_, r) => <>{r.file.name}<br /><Typography.Text type="secondary">{formatBytes(r.file.size)}</Typography.Text></> },
      { title: 'Tiến trình / kết quả', width: 300, render: (_, r) => r.status === 'invalid' ? <Typography.Text type="danger">{r.error}</Typography.Text>
        : r.status === 'failed' ? <Typography.Text type="danger">{r.error}</Typography.Text>
        : r.status === 'done' && r.result ? <Tag color={fileStateColors[r.result.fileState]}>{fileStateLabels[r.result.fileState]}</Tag>
        : <Progress percent={r.progress} size="small" status={r.status === 'uploading' ? 'active' : 'normal'} /> },
      { title: '', width: 90, render: (_, r) => r.status === 'uploading' ? null
        : r.status === 'done' ? null
        : <Button size="small" type="link" onClick={() => setRows(previous => previous.filter(x => x.key !== r.key))}>Bỏ</Button> },
    ]} />}
    {rows.length > 0 && !uploading && (done > 0 || failed > 0) && <Alert className="patient-section" showIcon
      type={failed > 0 ? 'warning' : 'success'}
      message={failed > 0 ? `${done} tệp đã gửi, ${failed} tệp lỗi. Các tệp lỗi được giữ lại để thử lại.` : `${done} tệp đã gửi.`}
      description={done > 0 ? 'Tệp đang kiểm tra an toàn chỉ mở được khi chuyển sang Sẵn sàng.' : undefined} />}
  </Drawer>;
}
