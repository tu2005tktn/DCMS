import { Alert, Button, Checkbox, Collapse, Drawer, Form, Input, Select, Space } from 'antd';
import { useRef, useState } from 'react';
import type { SheetContext } from './sheetTypes';

export default function SheetConfirmationDrawer({ kind, direct, note, sourceItemId, context, qualifyingItems, onClose, onConfirm, onReload }: {
  kind: 'plan' | 'actual'; direct: boolean; note?: string; sourceItemId?: string;
  context: SheetContext; onClose: () => void;
  qualifyingItems?: { id: string; serviceName: string }[];
  onConfirm: (values: Record<string, unknown>, key: string) => Promise<void>;
  onReload: () => Promise<void>;
}) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const pending = useRef<{ body: string; key: string } | null>(null);
  const decision = Form.useWatch('decision', form);
  const basis = Form.useWatch('chargingBasis', form);
  const steps = context.steps.filter(s => s.itemId === sourceItemId && s.outcome !== 'Completed');
  async function submit(values: Record<string, unknown>) {
    if (busy) return;
    const body = JSON.stringify(values);
    if (pending.current?.body !== body) pending.current = { body, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try { await onConfirm(values, pending.current.key); onClose(); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Drawer open width={520} title={kind === 'plan' ? 'Ghi nhận quyết định hạng mục' : 'Xác nhận việc đã thực hiện'}
    onClose={() => { if (!busy) onClose(); }} destroyOnClose
    footer={<Space><Button onClick={onClose} disabled={busy}>Quay lại</Button>
      <Button type="primary" loading={busy} onClick={() => form.submit()}>{kind === 'plan' ? 'Ghi nhận quyết định' : 'Xác nhận đã thực hiện'}</Button></Space>}>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {error && <Button disabled={busy} onClick={() => {
      setBusy(true); void onReload().then(() => { pending.current = null; setError('Đã đọc phiên bản mới. Kiểm tra lại thông tin trước khi xác nhận.'); })
        .catch(f => setError((f as Error).message)).finally(() => setBusy(false));
    }}>Đọc lại phiên bản, giữ nội dung đang nhập</Button>}
    <Form form={form} layout="vertical" onFinish={submit} initialValues={{ decision: 'Agreed',
      visitId: context.visits[0]?.id, outcome: note, chargingBasis: 'Separate', completeItem: false }}>
      {kind === 'actual' && <>
        <Form.Item name="visitId" label="Lượt khám đang điều trị" rules={[{ required: true, message: 'Chọn lượt khám InProgress.' }]}>
          <Select options={context.visits.map(v => ({ value: v.id, label: new Date(v.arrivalAt).toLocaleString('vi-VN') }))}
            placeholder="Chọn lượt khám" /></Form.Item>
        {!context.visits.length && <Alert type="info" message="Chưa có lượt khám đang điều trị. Nháp được giữ; hãy tiếp nhận và bắt đầu lượt khám hợp lệ." />}
        <Form.Item name="outcome" label="Việc thực tế đã làm và kết quả" rules={[{ required: true, whitespace: true }]}>
          <Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        {steps.length > 0 && <Form.Item name="stepId" label="Bước điều trị" rules={[{ required: true }]}>
          <Select options={steps.map(s => ({ value: s.id, label: `${s.name}${s.held ? ' — đang tạm dừng' : ''}`, disabled: s.held }))} /></Form.Item>}
        <Form.Item name="chargingBasis" label="Cơ sở tính phí"><Select options={[
          { value: 'Separate', label: 'Tính phí theo thỏa thuận' }, { value: 'Included', label: 'Đã nằm trong phạm vi bao gồm' },
          { value: 'Free', label: 'Miễn phí có căn cứ' }]} /></Form.Item>
        {basis !== 'Separate' && <Form.Item name="chargingBasisEvidence" label="Căn cứ bao gồm / miễn phí" rules={[{ required: true, whitespace: true }]}>
          <Input.TextArea maxLength={1000} rows={2} /></Form.Item>}
        <Form.Item name="completeItem" valuePropName="checked"><Checkbox>Bác sĩ xác nhận hạng mục đã hoàn tất về lâm sàng</Checkbox></Form.Item>
      </>}
      {(kind === 'plan' || direct) && <>
        <Form.Item name="decision" label="Quyết định của bệnh nhân" rules={[{ required: true }]}>
          <Select disabled={direct} options={[
            { value: 'Proposed', label: 'Đề xuất, chưa quyết định' }, { value: 'Agreed', label: 'Đồng ý' },
            { value: 'Deferred', label: 'Hoãn' }, { value: 'Refused', label: 'Từ chối' }]} /></Form.Item>
        <Form.Item name="counseling" label="Tư vấn, lựa chọn, lợi ích/rủi ro và giá thỏa thuận" rules={[{ required: decision !== 'Proposed', whitespace: true }]}>
          <Input.TextArea rows={3} maxLength={4000} /></Form.Item>
        <Form.Item name="decisionMaker" label="Người đưa ra quyết định" rules={[{ required: decision !== 'Proposed', whitespace: true }]}><Input maxLength={160} /></Form.Item>
        <Form.Item name="representativeBasis" label="Căn cứ đại diện (nếu có)"><Input.TextArea rows={2} maxLength={1000} /></Form.Item>
        <Form.Item name="consentDocumentId" label="Bằng chứng đồng ý từ S20 (nếu áp dụng)">
          <Select allowClear placeholder="Chọn file Ready của phiếu" options={context.consentDocuments?.map(d => ({ value: d.id, label: d.fileName })) ?? []} /></Form.Item>
      </>}
      {kind === 'plan' && <Collapse items={[{ key: 'estimates', label: 'Ước tính và điều kiện giảm trừ (tùy chọn)', children: <>
        {['minMinutes', 'selectedMinutes', 'maxMinutes'].map((name, index) => <Form.Item key={name} name={name} label={['Ít nhất (phút)', 'Thời lượng bác sĩ chọn (phút)', 'Nhiều nhất (phút)'][index]}>
          <Select allowClear options={[15,20,25,30,45,60,90,120,180,240,300,360,420,480].map(v => ({ value: v, label: `${v} phút` }))} /></Form.Item>)}
        <Form.Item name="sessions" label="Số buổi dự kiến"><Select allowClear options={Array.from({ length: 30 }, (_, i) => ({ value: i + 1, label: `${i + 1} buổi` }))} /></Form.Item>
        <p>Ước tính có thể thay đổi. Giữ ghế và thời gian hẹn được xác nhận khi đặt lịch.</p>
        {['earliestOn', 'targetOn', 'latestOn'].map((name, i) => <Form.Item key={name} name={name} label={['Quay lại sớm nhất', 'Ngày quay lại mục tiêu', 'Quay lại muộn nhất'][i]}><Input type="date" /></Form.Item>)}
        <Form.Item name="qualifyingItemId" label="Hạng mục đủ điều kiện để hưởng giảm trừ"><Select allowClear placeholder="Chỉ chọn khi giảm trừ có điều kiện"
          options={qualifyingItems?.map(i => ({ value: i.id, label: i.serviceName })) ?? []} /></Form.Item>
      </> }]} />}
      {kind === 'actual' && <Alert type="info" message="Sau xác nhận, dòng được ghi vào hồ sơ. Phí cần ledger xử lý; số tiền đã thu được theo dõi riêng." />}
    </Form>
  </Drawer>;
}
