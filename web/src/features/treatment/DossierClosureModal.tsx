import { Alert, Form, Input, Modal, Select } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import type { TreatmentDossier } from './types';

export default function DossierClosureModal({ dossier, completed, onClose, onSaved }: {
  dossier: TreatmentDossier; completed: boolean; onClose: () => void; onSaved: () => void;
}) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false), [error, setError] = useState<string | null>(null);
  const [current, setCurrent] = useState(dossier);
  const pending = useRef<{ body: string; key: string } | null>(null);
  const unresolved = current.items.filter(i => i.decision === 'Agreed' && i.progress !== 'Completed');
  const outcome = Form.useWatch('outcome', form);
  useEffect(() => { void api<TreatmentDossier>(`/api/dossiers/${dossier.id}`).then(setCurrent).catch(f => setError((f as Error).message)); }, [dossier.id]);
  async function submit(values: { outcome: string; reason: string; dispositions?: Record<string, string> }) {
    const payload = { expectedVersion: current.version, ...(completed ? {
      outcome: values.outcome, reason: values.reason,
      dispositions: values.outcome === 'CompletedAsAgreed' ? [] : unresolved.map(i => ({ itemId: i.id, outcome: values.dispositions?.[i.id], reason: values.reason })),
    } : { reason: values.reason }) };
    const body = JSON.stringify(payload);
    if (pending.current?.body !== body) pending.current = { body, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try { await api(`/api/dossiers/${dossier.id}/${completed ? 'close' : 'reopen'}`, {
      method: 'POST', body: JSON.stringify({ ...payload, requestKey: pending.current.key }),
    }); onSaved(); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Modal open title={completed ? 'Kết thúc hồ sơ điều trị' : 'Mở lại hồ sơ điều trị'} confirmLoading={busy}
    onCancel={() => { if (!busy) onClose(); }} onOk={() => form.submit()} okText={completed ? 'Xác nhận kết thúc' : 'Mở lại'}>
    {error && <Alert type="error" showIcon message={error} />}
    <Form form={form} layout="vertical" onFinish={values => void submit(values)} initialValues={{ outcome: 'CompletedAsAgreed' }}>
      {completed && <Form.Item name="outcome" label="Kết quả điều trị" rules={[{ required: true }]}><Select options={[
        { value: 'CompletedAsAgreed', label: 'Hoàn tất đúng thỏa thuận' },
        { value: 'CompletedRevisedScope', label: 'Hoàn tất theo phạm vi đã điều chỉnh' },
        { value: 'StoppedIncomplete', label: 'Ngừng khi còn phần chưa làm' },
      ]} /></Form.Item>}
      {completed && unresolved.length > 0 && <Alert type="info" message={`Còn ${unresolved.length} hạng mục chưa hoàn tất; cần xử lý trước khi đóng (MSG243).`} />}
      {completed && outcome !== 'CompletedAsAgreed' && unresolved.map(i => <Form.Item key={i.id} name={['dispositions', i.id]}
        label={`Xử lý: ${i.serviceName}`} rules={[{ required: true }]}><Select options={[
          { value: 'Stopped', label: 'Dừng phần còn lại' }, { value: 'Cancelled', label: 'Hủy phần chưa thực hiện' },
          { value: 'Skipped', label: 'Bỏ qua theo quyết định đã ghi nhận' },
        ]} /></Form.Item>)}
      <Form.Item name="reason" label="Lý do" rules={[{ required: !completed || outcome !== 'CompletedAsAgreed', whitespace: true }]}>
        <Input.TextArea maxLength={1000} rows={3} /></Form.Item>
      <p>Việc đã làm và công nợ được giữ theo nguồn. Đóng hồ sơ không xác nhận đã thanh toán.</p>
    </Form>
  </Modal>;
}
