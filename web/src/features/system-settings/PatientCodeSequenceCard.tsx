import { Alert, Button, Descriptions, Form, Input, InputNumber, List, Modal, Space, Typography, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import type { PatientCodeSequence, PatientCodeSequencePreview } from './generalSettings';

const base = '/api/system-settings/settings/patient-code-sequence';

// DCMS-122 / SET-AT-08: the next number is shown for information only (no reservation) and can only move forward.
export default function PatientCodeSequenceCard({ revision }: { revision: number }) {
  const [data, setData] = useState<PatientCodeSequence | null>(null), [loadError, setLoadError] = useState<string>();
  const [open, setOpen] = useState(false), [next, setNext] = useState<number | null>(null), [reason, setReason] = useState('');
  const [preview, setPreview] = useState<PatientCodeSequencePreview | null>(null), [busy, setBusy] = useState(false), [error, setError] = useState<ApiError>();
  const pending = useRef<{ payload: string; key: string } | null>(null);
  const [toast, toastContext] = message.useMessage();

  async function load() {
    setLoadError(undefined);
    try { setData(await api<PatientCodeSequence>(base)); } catch (e) { setLoadError((e as Error).message); }
  }
  // A prefix change (new general version) changes the next code shown here.
  useEffect(() => { void load(); }, [revision]);

  const tooLow = data != null && next != null && next <= data.nextNumber;
  async function check() {
    if (!data || next == null || tooLow) return;
    setBusy(true); setError(undefined);
    try {
      setPreview(await api<PatientCodeSequencePreview>(`${base}/preview`, { method: 'POST',
        body: JSON.stringify({ expectedNextNumber: data.nextNumber, nextNumber: next }) }));
    } catch (e) { setError(e as ApiError); }
    finally { setBusy(false); }
  }

  async function save() {
    if (!data || !preview?.allowed || preview.stale || next == null || !reason.trim()) return;
    const body = { expectedNextNumber: data.nextNumber, nextNumber: next, reason: reason.trim() };
    const payload = JSON.stringify(body);
    // Same payload keeps the key so a retry after a lost response replays instead of moving twice.
    if (pending.current?.payload !== payload) pending.current = { payload, key: crypto.randomUUID() };
    setBusy(true); setError(undefined);
    try {
      const saved = await api<PatientCodeSequence>(base, { method: 'PUT', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      pending.current = null; setData(saved); setOpen(false);
      void toast.success(`Mã tự động tiếp theo: ${saved.nextCode}.`);
    } catch (e) {
      const failure = e as ApiError;
      if (failure.status !== 0) pending.current = null;
      setError(failure); setPreview(null);
      if (failure.code === 'MSG111') await load();
    } finally { setBusy(false); }
  }

  function start() {
    if (!data) return;
    setNext(data.nextNumber + 1); setReason(''); setPreview(null); setError(undefined); setOpen(true);
  }

  return <div className="general-settings-sequence">
    {toastContext}
    <Typography.Text strong>Số tiếp theo</Typography.Text>
    {loadError ? <Alert type="error" showIcon message={`Không tải được số mã tiếp theo: ${loadError}`} action={<Button size="small" onClick={() => void load()}>Thử lại</Button>} />
      : !data ? <Typography.Paragraph type="secondary">Đang tải…</Typography.Paragraph>
      : <Space direction="vertical" style={{ width: '100%' }}>
        <Descriptions size="small" column={{ xs: 1, sm: 3 }} items={[
          { key: 'next', label: 'Mã tự động kế tiếp', children: <Typography.Text code>{data.nextCode}</Typography.Text> },
          { key: 'last', label: 'Số đã cấp gần nhất', children: data.lastIssued ?? '—' },
          { key: 'max', label: `Số lớn nhất đang có (${data.prefix || 'không tiền tố'})`, children: data.highestExisting ?? '—' },
        ]} />
        <Typography.Text type="secondary">Chỉ để xem, không giữ chỗ mã. Mã đã tồn tại (kể cả nhập tay) được bỏ qua khi cấp.</Typography.Text>
        <Button onClick={start}>Đổi số tiếp theo</Button>
      </Space>}

    <Modal open={open} title="Đổi số tiếp theo của mã bệnh nhân" okText="Lưu" cancelText="Hủy" confirmLoading={busy} closable={!busy}
      cancelButtonProps={{ disabled: busy }} okButtonProps={{ disabled: !preview?.allowed || preview.stale || !reason.trim() }}
      onCancel={() => { if (!busy) setOpen(false); }} onOk={() => void save()}>
      {data && <Form layout="vertical" disabled={busy}>
        {error && <Alert className="form-alert" type="error" showIcon message={error.message} />}
        <Form.Item label="Số tiếp theo mới" required validateStatus={tooLow ? 'error' : undefined}
          help={tooLow ? `Chỉ được tăng: phải lớn hơn ${data.nextNumber}.` : `Hiện tại: ${data.nextNumber}`}>
          <Space.Compact>
            <InputNumber min={1} precision={0} style={{ width: 200 }} value={next} onChange={v => { setNext(v); setPreview(null); }} />
            <Button disabled={next == null || tooLow} onClick={() => void check()}>Xem trước</Button>
          </Space.Compact>
        </Form.Item>
        {preview?.stale && <Alert className="form-alert" type="warning" showIcon message="Số tiếp theo vừa thay đổi (có hồ sơ mới hoặc người khác vừa sửa). Đọc lại rồi xem trước lần nữa."
          action={<Button size="small" onClick={() => { setPreview(null); void load(); }}>Đọc lại</Button>} />}
        {preview && <List size="small" dataSource={[`Mã tự động kế tiếp sẽ là ${preview.nextCode}.`,
          ...(preview.skippedCodes > 0 ? [`${preview.skippedCodes} mã đã tồn tại từ số này trở đi sẽ được bỏ qua.`] : []), ...preview.issues]}
          renderItem={x => <List.Item>{x}</List.Item>} />}
        <Form.Item label="Lý do" required>
          <Input.TextArea rows={2} maxLength={500} showCount value={reason} onChange={e => setReason(e.target.value)} />
        </Form.Item>
      </Form>}
    </Modal>
  </div>;
}
