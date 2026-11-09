import { Alert, Button, Descriptions, Drawer, Empty, Form, Input, Select, Space, Spin, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { ApiError } from '../../services/api';
import { customerCareApi } from './customerCare.api';
import { channelLabels, clinicTime, contactResultChoices, contactResultLabels, statusColors, statusLabels, type CareStaff, type CareTaskDetail,
  type CareWorklistItem } from './types';

// S60 ngăn chi tiết (docs/specs/S60-CUSTOMER-CARE-WEB.vi.md §5). A contact never sends a message, books or completes clinical work.
type ContactValues = { channel: string; result: string; nextAction?: string };

export default function CareTaskDrawer({ item, staff, canManage, onClose, onEdit, onChanged }: {
  item: CareWorklistItem | null; staff: CareStaff[]; canManage: boolean; onClose: () => void;
  onEdit: (item: CareWorklistItem) => void; onChanged: () => void;
}) {
  const [detail, setDetail] = useState<CareTaskDetail | null>(null), [loadError, setLoadError] = useState<string>(), [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false), [error, setError] = useState<ApiError>();
  const [form] = Form.useForm<ContactValues>();
  const keys = useRef<Record<string, { fingerprint: string; value: string }>>({});
  const names = new Map(staff.map(x => [x.accountId, x.name]));

  useEffect(() => {
    if (!item) { setDetail(null); return; }
    let live = true; setLoadError(undefined); setError(undefined);
    customerCareApi.detail(item.task.id).then(d => { if (live) setDetail(d); }).catch(e => { if (live) { setDetail(null); setLoadError((e as Error).message); } });
    return () => { live = false; };
  }, [item?.task.id, revision]);
  useEffect(() => { keys.current = {}; form.resetFields(); form.setFieldsValue({ channel: 'Phone' }); }, [item?.task.id]);

  function requestKey(command: string, payload: unknown) {
    const fingerprint = JSON.stringify(payload);
    if (keys.current[command]?.fingerprint !== fingerprint) keys.current[command] = { fingerprint, value: crypto.randomUUID() };
    return keys.current[command].value;
  }
  async function run(action: () => Promise<unknown>, after?: () => void) {
    setBusy(true); setError(undefined);
    try { await action(); after?.(); setRevision(x => x + 1); onChanged(); }
    catch (e) { setError(e as ApiError); if ((e as ApiError).code === 'MSG111') { setRevision(x => x + 1); onChanged(); } }
    finally { setBusy(false); }
  }
  async function contact() {
    if (!detail) return;
    const v = await form.validateFields();
    const body = { channel: v.channel, result: v.result, nextAction: v.nextAction?.trim() || null, expectedTaskVersion: detail.task.version };
    await run(() => customerCareApi.contact(detail.task.id, { ...body, requestKey: requestKey('contact', body) }),
      () => { delete keys.current.contact; form.resetFields(); form.setFieldsValue({ channel: 'Phone' }); });
  }
  async function done() {
    if (!detail) return;
    const t = detail.task;
    const body = { expectedVersion: t.version, status: 'Done', ownerAccountId: t.ownerAccountId, dueAt: t.dueAt, content: t.content };
    await run(() => customerCareApi.update(t.id, { ...body, requestKey: requestKey('done', body) }));
  }

  const task = detail?.task;
  const editable = canManage && (task?.allowedActions.includes('update') ?? false);
  const contactable = canManage && (task?.allowedActions.includes('recordContact') ?? false);
  return <Drawer open={!!item} onClose={onClose} width={620} destroyOnClose title={item ? `${item.patientCode} · ${item.patientName}` : ''}
    extra={task && <Space>
      {editable && <Button disabled={busy} onClick={() => item && onEdit({ ...item, task })}>Sửa</Button>}
      {editable && <Button type="primary" loading={busy} onClick={() => void done()}>Đánh dấu đã xong</Button>}
    </Space>}>
    {loadError && <Alert type="error" showIcon message={loadError} action={<Button size="small" onClick={() => setRevision(x => x + 1)}>Thử lại</Button>} />}
    {!task && !loadError && <Spin />}
    {task && item && <>
      {error && <Alert type="error" showIcon style={{ marginBottom: 16 }} message={error.message}
        description={error.code === 'MSG111' ? 'Phiếu vừa thay đổi ở nơi khác. Dữ liệu mới đã được tải lại, hãy kiểm tra rồi làm lại.' : undefined}
        afterClose={() => setError(undefined)} closable />}
      <Descriptions column={1} size="small" bordered>
        <Descriptions.Item label="Chi nhánh">{item.branchName}</Descriptions.Item>
        <Descriptions.Item label="Khách hàng">{item.patientCode} · {item.patientName}</Descriptions.Item>
        <Descriptions.Item label="SĐT">{item.phone ?? '—'}</Descriptions.Item>
        <Descriptions.Item label="Thời gian chăm sóc">{clinicTime(task.dueAt)}</Descriptions.Item>
        <Descriptions.Item label="Loại chăm sóc">{item.typeName}</Descriptions.Item>
        <Descriptions.Item label="Người phụ trách">{task.ownerAccountId ? names.get(task.ownerAccountId) ?? item.ownerName ?? `Tài khoản #${task.ownerAccountId}` : 'Chưa giao'}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái"><Tag color={statusColors[task.status]}>{statusLabels[task.status]}</Tag>
          <Typography.Text type="secondary"> phiên bản {task.version}</Typography.Text></Descriptions.Item>
        <Descriptions.Item label="Nội dung"><Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>{task.content}</Typography.Paragraph></Descriptions.Item>
      </Descriptions>

      {contactable && <>
        <Typography.Title level={5} style={{ marginTop: 24 }}>Ghi nhận liên hệ</Typography.Title>
        <Form form={form} layout="vertical" disabled={busy}>
          <Space wrap align="start">
            <Form.Item name="channel" label="Kênh" rules={[{ required: true, message: 'Chọn kênh.' }]}>
              <Select aria-label="Kênh liên hệ" style={{ width: 160 }} options={Object.entries(channelLabels).map(([value, label]) => ({ value, label }))} />
            </Form.Item>
            <Form.Item name="result" label="Kết quả" rules={[{ required: true, message: 'Chọn kết quả.' }]}>
              <Select aria-label="Kết quả liên hệ" style={{ width: 200 }} placeholder="Chọn kết quả"
                options={contactResultChoices.map(value => ({ value, label: contactResultLabels[value] }))} />
            </Form.Item>
          </Space>
          <Form.Item name="nextAction" label="Bước tiếp theo"><Input.TextArea aria-label="Bước tiếp theo" rows={2} maxLength={1000} showCount /></Form.Item>
          <Button type="primary" loading={busy} onClick={() => void contact()}>Lưu liên hệ</Button>
        </Form>
      </>}

      <Typography.Title level={5} style={{ marginTop: 24 }}>Lịch sử liên hệ</Typography.Title>
      {detail.contacts.length === 0 ? <Empty description="Chưa có lần liên hệ nào" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        : <Timeline items={[...detail.contacts].reverse().map(c => ({ children: <>
            <Typography.Text strong>{contactResultLabels[c.result] ?? c.result}</Typography.Text> · {channelLabels[c.channel] ?? c.channel}
            <div><Typography.Text type="secondary">{clinicTime(c.occurredAt)} · {names.get(c.actorId) ?? `Tài khoản #${c.actorId}`}</Typography.Text></div>
            {c.nextAction && <div>Bước tiếp theo: {c.nextAction}</div>}
          </> }))} />}
    </>}
  </Drawer>;
}
