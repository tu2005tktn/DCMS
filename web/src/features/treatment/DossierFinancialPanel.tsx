import { Alert, Button, Drawer, Form, Input, InputNumber, Modal, Select, Space, Table } from 'antd';
import { useRef, useState } from 'react';
import { api } from '../../services/api';
import { money } from './types';

export type FinancialCharge = { id: string; version: number; sourceCode: string; postedAt: string;
  service: string; dentist: string; netVnd: number; confirmedAllocatedVnd: number; dueVnd: number };
export type FinancialReceipt = { id: string; version: number; code: string; receivedAt: string; recordedAt: string;
  receivedVnd: number; method: string; collector: string; payer?: string; note?: string; status: string;
  allocations: { chargeId: string; amountVnd: number }[]; unusedAdvanceVnd: number };
type Snapshot = { available: boolean; sourceVersion: string; asOf: string; allowedActions: string[];
  charges: FinancialCharge[]; receipts: FinancialReceipt[]; unusedAdvanceVnd: number };

// UI consumer of the DCMS-101 contract. Tests/preview supply mocks at the API boundary.
export default function DossierFinancialPanel({ dossierId, available, onChanged }: {
  dossierId: string; available: boolean; onChanged: () => void;
}) {
  const [mode, setMode] = useState<'pay' | 'history' | null>(null);
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [receipt, setReceipt] = useState<FinancialReceipt | null>(null);
  const [largeFont, setLargeFont] = useState(false);
  const [error, setError] = useState<string | null>(null), [busy, setBusy] = useState(false);
  const [allocations, setAllocations] = useState<Record<string, number>>({});
  const [form] = Form.useForm();
  const pending = useRef<{ payload: string; key: string } | null>(null);
  const [verification, setVerification] = useState<{ action: 'confirm' | 'cancel'; receipt: FinancialReceipt; reference: string; note: string } | null>(null);
  async function verify() {
    if (!verification || !snapshot || busy) return;
    if (!verification.note.trim() || (verification.action === 'confirm' && !verification.reference.trim())) {
      setError('Nhập mã giao dịch và nội dung đối soát hoặc lý do hủy.'); return;
    }
    const body = verification.action === 'confirm'
      ? { expectedVersion: verification.receipt.version, expectedAccountVersion: Number(snapshot.sourceVersion),
          bankReference: verification.reference.trim(), note: verification.note.trim() }
      : { expectedVersion: verification.receipt.version, reason: verification.note.trim() };
    const payload = JSON.stringify({ action: verification.action, id: verification.receipt.id, body });
    if (pending.current?.payload !== payload) pending.current = { payload, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try {
      await api(`/api/finance/payments/${verification.receipt.id}/${verification.action}`, {
        method: 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }),
      });
      pending.current = null; setVerification(null); setReceipt(null);
      setSnapshot(await api<Snapshot>(`/api/dossiers/${dossierId}/finance`)); onChanged();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  function receiptLines() {
    if (!receipt) return [];
    return [`Chứng từ ${receipt.code}`, new Date(receipt.receivedAt).toLocaleString('vi-VN'),
      `Người thu: ${receipt.collector}`, `Người nộp: ${receipt.payer || '—'}`, `Tiền nhận: ${money(receipt.receivedVnd)}`,
      `Phương thức: ${receipt.method === 'Cash' ? 'Tiền mặt' : 'Chuyển khoản'}`,
      `Trạng thái: ${receipt.status === 'Confirmed' ? 'Đã xác nhận' : 'Chờ xác minh'}`,
      ...receipt.allocations.map(a => `${snapshot?.charges.find(c => c.id === a.chargeId)?.service ?? 'Dòng phí nguồn'}: ${money(a.amountVnd)}`),
      `Ứng chưa dùng từ chứng từ: ${money(receipt.unusedAdvanceVnd)}`, receipt.note || ''];
  }
  function printReceipt() {
    if (receipt?.status !== 'Confirmed') return;
    const frame = document.createElement('iframe'); frame.title = 'Bản in chứng từ'; frame.style.cssText = 'position:fixed;width:0;height:0;border:0';
    const escape = (value: string) => value.replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]!));
    frame.srcdoc = `<html lang="vi"><meta charset="utf-8"><title>Chứng từ</title><style>body{font:${largeFont ? 18 : 13}px Arial;padding:24px;line-height:1.6}</style><body>${receiptLines().map(l => `<p>${escape(l)}</p>`).join('')}</body></html>`;
    frame.onload = () => { frame.contentWindow?.focus(); frame.contentWindow?.print(); window.setTimeout(() => frame.remove(), 60_000); };
    document.body.appendChild(frame);
  }
  function downloadReceipt() {
    if (receipt?.status !== 'Confirmed') return;
    const canvas = document.createElement('canvas'), lines = receiptLines(); canvas.width = 1200; canvas.height = (lines.length + 3) * 52;
    const ctx = canvas.getContext('2d'); if (!ctx) return;
    ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, canvas.width, canvas.height); ctx.fillStyle = '#17233d'; ctx.font = '26px Arial';
    lines.forEach((line, i) => ctx.fillText(line, 32, 50 + i * 52, 1136));
    const link = document.createElement('a'); link.href = canvas.toDataURL('image/png'); link.download = `chung-tu-${receipt?.code}.png`; link.click();
  }
  async function open(next: 'pay' | 'history') {
    setMode(next); setError(null); setReceipt(null); setSnapshot(null);
    if (!available) return;
    setBusy(true);
    try { setSnapshot(await api<Snapshot>(`/api/dossiers/${dossierId}/finance`)); }
    catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  async function collect(values: { receivedVnd: number; method: string; payer?: string; note?: string }) {
    if (!snapshot || busy) return;
    const sum = Object.values(allocations).reduce((a, b) => a + b, 0);
    if (sum !== values.receivedVnd
      || snapshot.charges.some(c => (allocations[c.id] ?? 0) > c.dueVnd)) {
      setError('MSG256 — Phân bổ chưa khớp tiền nhận hoặc vượt khoản nợ.'); return;
    }
    const payload = { ...values, acknowledgedAdvanceVnd: 0, dossierId, receivedAt: new Date().toISOString(),
      expectedSourceVersion: snapshot.sourceVersion, allocations: snapshot.charges.filter(c => (allocations[c.id] ?? 0) > 0)
        .map(c => ({ chargeId: c.id, expectedChargeVersion: c.version, amountVnd: allocations[c.id] })) };
    const serialized = JSON.stringify(payload);
    if (pending.current?.payload !== serialized) pending.current = { payload: serialized, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try {
      const saved = await api<FinancialReceipt>(`/api/dossiers/${dossierId}/collections`, {
        method: 'POST', body: JSON.stringify({ ...payload, requestKey: pending.current.key }),
      });
      setReceipt(saved); pending.current = null;
      setSnapshot(await api<Snapshot>(`/api/dossiers/${dossierId}/finance`)); onChanged();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  return <Space wrap>
    <Button onClick={() => void open('history')}>LỊCH SỬ</Button>
    <Button className="treatment-sheet-create" onClick={() => void open('pay')}>TRẢ TIỀN...</Button>
    <Drawer open={mode !== null} width={760} title={mode === 'history' ? 'Lịch sử thanh toán' : 'Trả tiền cho phiếu'}
      onClose={() => { if (!busy) setMode(null); }} loading={busy && !snapshot}>
      {!available && <Alert type="info" showIcon message="Chưa có dữ liệu đối soát"
        description="Nguồn thanh toán của phòng khám chưa được kết nối. Chưa thể xác nhận tiền hoặc xem chứng từ." />}
      {error && <Alert role="alert" type="error" showIcon message={error} />}
      {snapshot?.available && !receipt && <>
        <p>Dữ liệu đối soát lúc {new Date(snapshot.asOf).toLocaleString('vi-VN')}. Ứng chưa dùng: {money(snapshot.unusedAdvanceVnd)}.</p>
        {mode === 'history' ? <Table rowKey="id" dataSource={snapshot.receipts} pagination={false} columns={[
          { title: 'Mã / Ngày', render: (_, r) => <Button type="link" onClick={() => setReceipt(r)}>{r.code} — {new Date(r.receivedAt).toLocaleDateString('vi-VN')}</Button> },
          { title: 'Tiền nhận', render: (_, r) => money(r.receivedVnd) },
          { title: 'Phương thức', dataIndex: 'method' }, { title: 'Người thu', dataIndex: 'collector' },
          { title: 'Trạng thái', dataIndex: 'status' },
        ]} /> : <Form form={form} layout="vertical" onFinish={values => void collect(values)} initialValues={{ method: 'Cash' }}>
          <Table rowKey="id" dataSource={snapshot.charges} pagination={false} size="small" columns={[
            { title: 'Dịch vụ / Bác sĩ', render: (_, c) => <>{c.service}<br />{c.dentist}</> },
            { title: 'Còn nợ', render: (_, c) => money(c.dueVnd) },
            { title: 'Phân bổ lần này', render: (_, c) => <InputNumber min={0} max={c.dueVnd} precision={0}
              aria-label={`Phân bổ ${c.service}`} value={allocations[c.id] ?? 0} onChange={v => setAllocations(a => ({ ...a, [c.id]: v ?? 0 }))} /> },
          ]} />
          <Button onClick={() => {
            let remaining = form.getFieldValue('receivedVnd') as number ?? 0;
            const proposed: Record<string, number> = {};
            [...snapshot.charges].sort((a, b) => a.postedAt.localeCompare(b.postedAt) || a.sourceCode.localeCompare(b.sourceCode))
              .forEach(c => { proposed[c.id] = Math.min(c.dueVnd, remaining); remaining -= proposed[c.id]; });
            setAllocations(proposed);
          }}>Gợi ý phân bổ theo khoản nợ cũ nhất</Button>
          <p>Kiểm tra từng dòng trước khi xác nhận.</p>
          <Form.Item name="receivedVnd" label="Tiền thực nhận (VND)" rules={[{ required: true }, { type: 'number', min: 1 }]}><InputNumber precision={0} min={1} /></Form.Item>
          <Form.Item name="method" label="Phương thức"><Select options={[{ value: 'Cash', label: 'Tiền mặt' }, { value: 'Transfer', label: 'Chuyển khoản — cần đối soát' }]} /></Form.Item>
          <Form.Item name="payer" label="Người nộp"><Input maxLength={160} /></Form.Item>
          <Form.Item name="note" label="Ghi chú thanh toán"><Input.TextArea maxLength={1000} /></Form.Item>
          <p>Người thu lấy từ phiên đăng nhập. Chuyển khoản chỉ tăng số đã trả sau xác minh.</p>
          <Button type="primary" htmlType="submit" loading={busy} disabled={!snapshot.allowedActions.includes('Collect')}>Xác nhận tiền thực nhận</Button>
        </Form>}
      </>}
      {receipt && <div className="treatment-receipt-preview" style={{ fontSize: largeFont ? 18 : 14 }}>
        <h3>Chứng từ {receipt.code}</h3><p>{new Date(receipt.receivedAt).toLocaleString('vi-VN')} — {receipt.method}</p>
        <p>Người thu: {receipt.collector}. Người nộp: {receipt.payer || '—'}.</p>
        <strong>{money(receipt.receivedVnd)}</strong><p>Trạng thái: {receipt.status}</p>
        <ul>{receipt.allocations.map(a => <li key={a.chargeId}>{snapshot?.charges.find(c => c.id === a.chargeId)?.service ?? a.chargeId}: {money(a.amountVnd)}</li>)}</ul>
        <p>Ứng chưa dùng từ chứng từ: {money(receipt.unusedAdvanceVnd)}</p><p>{receipt.note}</p>
        <Space wrap><Button onClick={printReceipt} disabled={receipt.status !== 'Confirmed'}>In chứng từ</Button><Button onClick={downloadReceipt} disabled={receipt.status !== 'Confirmed'}>Lưu ảnh</Button>
          <Button onClick={() => setLargeFont(v => !v)}>Cỡ chữ {largeFont ? 'thường' : 'lớn'}</Button><Button onClick={() => setReceipt(null)}>Quay lại</Button></Space>
        {receipt.status === 'Pending' && <Space wrap>
          {snapshot?.allowedActions.includes('Confirm') && <Button type="primary" onClick={() => setVerification({ action: 'confirm', receipt, reference: '', note: '' })}>Xác nhận tiền vào</Button>}
          {snapshot?.allowedActions.includes('Cancel') && <Button danger onClick={() => setVerification({ action: 'cancel', receipt, reference: '', note: '' })}>Hủy khoản chờ</Button>}
        </Space>}
      </div>}
    </Drawer>
    <Modal open={verification !== null} title={verification?.action === 'confirm' ? 'Xác nhận chuyển khoản' : 'Hủy khoản chờ'}
      confirmLoading={busy} onOk={() => void verify()} onCancel={() => { if (!busy) { setVerification(null); pending.current = null; } }}>
      {error && <Alert role="alert" type="error" message={error} />}
      {verification?.action === 'confirm' && <Input aria-label="Mã giao dịch ngân hàng" placeholder="Mã giao dịch ngân hàng"
        value={verification.reference} onChange={e => setVerification(v => v && { ...v, reference: e.target.value })} />}
      <Input.TextArea aria-label={verification?.action === 'confirm' ? 'Nội dung đối soát' : 'Lý do hủy'}
        placeholder={verification?.action === 'confirm' ? 'Nội dung đối soát' : 'Lý do hủy'}
        value={verification?.note ?? ''} onChange={e => setVerification(v => v && { ...v, note: e.target.value })} />
    </Modal>
  </Space>;
}
