import { Alert, Button, Descriptions, Input, InputNumber, Modal, QRCode, Select, Tag } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import { money } from './types';
import type { PayOsOrder } from './sheetTypes';

export type DossierAdvance = {
  id: string; dossierId: string | null; kind: 'Deposit' | 'ServiceAdvance';
  paymentState: string; method: string; amountVnd: number; allocatedVnd: number;
  refundedVnd: number; availableVnd: number; createdAt: string;
};

export default function DossierAdvanceModal({ open, dossierId, patientId, branchId, patientName, onClose, onSaved }: {
  open: boolean; dossierId: string; patientId: number; branchId: number; patientName: string;
  onClose: () => void; onSaved: () => void;
}) {
  const [kind, setKind] = useState<'Deposit' | 'ServiceAdvance'>('ServiceAdvance');
  const [amountVnd, setAmountVnd] = useState<number>(0);
  const [payer, setPayer] = useState(patientName);
  const [note, setNote] = useState('');
  const [method, setMethod] = useState<'Cash' | 'Transfer'>('Cash');
  const [order, setOrder] = useState<PayOsOrder | null>(null);
  const [showBill, setShowBill] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const request = useRef<{ body: string; key: string } | null>(null);

  useEffect(() => {
    if (!open) return;
    setKind('ServiceAdvance'); setAmountVnd(0); setPayer(patientName); setNote(''); setMethod('Cash');
    setOrder(null); setShowBill(false); setError(undefined);
    request.current = null;
    void api<PayOsOrder | null>(`/api/dossiers/${dossierId}/advance-payos-orders/active`)
      .then(active => { if (active) { setOrder(active); setMethod('Transfer'); setAmountVnd(active.amountVnd); setShowBill(true); } })
      .catch(failure => setError((failure as Error).message));
  }, [open, patientName, dossierId]);

  useEffect(() => {
    if (!open || !showBill || !order || !['Creating', 'Pending'].includes(order.state)) return;
    const timer = window.setInterval(() => {
      void api<PayOsOrder>(`/api/dossiers/${dossierId}/advance-payos-orders/${order.id}`)
        .then(next => { setOrder(next); if (next.state === 'Paid') onSaved(); })
        .catch(failure => setError((failure as Error).message));
    }, 3000);
    return () => window.clearInterval(timer);
  }, [open, showBill, order?.id, order?.state, dossierId, onSaved]);

  const valid = amountVnd > 0 && Number.isInteger(amountVnd) && payer.trim().length > 0 && !busy;
  async function save() {
    if (!valid) return;
    const body = { patientId, branchId, dossierId, kind, method, amountVnd,
      payer: payer.trim(), note: note.trim() || null };
    const serialized = JSON.stringify(body);
    if (request.current?.body !== serialized) request.current = { body: serialized, key: crypto.randomUUID() };
    setBusy(true); setError(undefined);
    try {
      if (method === 'Cash') {
        await api('/api/finance/advances', { method: 'POST', body: JSON.stringify({ ...body, requestKey: request.current.key }) });
        request.current = null; onSaved(); onClose();
      } else {
        const created = await api<PayOsOrder>(`/api/dossiers/${dossierId}/advance-payos-orders`, { method: 'POST',
          body: JSON.stringify({ requestKey: request.current.key, kind, amountVnd, payer: payer.trim(), note: note.trim() || null }) });
        setOrder(created); setShowBill(true);
      }
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <Modal open={open} title={showBill ? 'Bill chuyển khoản nhận trước' : 'Nhận trước cho phiếu điều trị'} onCancel={onClose} maskClosable={false}
    footer={<><Button onClick={showBill ? () => setShowBill(false) : onClose} disabled={busy}>{showBill ? 'Quay lại' : 'Hủy'}</Button>
      {!showBill && <Button type="primary" onClick={() => void save()} disabled={!valid} loading={busy}>
        {method === 'Cash' ? 'Ghi nhận tiền mặt' : 'Tạo QR và xem bill'}</Button>}</>}>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {showBill && order ? <>
      <Alert type={order.state === 'Paid' ? 'success' : order.state === 'Review' || order.state === 'LinkError' ? 'error' : 'info'}
        showIcon message={order.state === 'Paid' ? 'PayOS đã xác nhận. Khoản nhận trước đã có thể sử dụng.'
          : order.state === 'Review' ? 'Giao dịch cần đối soát.'
          : order.state === 'LinkError' ? 'Không tạo được QR PayOS.'
          : order.state === 'Expired' || order.state === 'Cancelled' ? 'Lệnh chuyển khoản đã kết thúc.'
          : 'Đang chờ PayOS xác nhận. Trạng thái tự cập nhật.'} />
      <Descriptions style={{ marginTop: 16 }} column={1} items={[
        { key: 'patient', label: 'Bệnh nhân', children: patientName },
        { key: 'kind', label: 'Khoản thu', children: 'Nhận trước điều trị' },
        { key: 'order', label: 'Mã PayOS', children: order.orderCode },
        { key: 'amount', label: 'Số tiền', children: money(order.amountVnd) },
        { key: 'bank', label: 'Tài khoản', children: `${order.bankBin ?? ''} · ${order.accountNumber ?? ''} · ${order.accountName ?? ''}` },
      ]} />
      <Tag color={order.state === 'Paid' ? 'green' : 'gold'}>{order.state === 'Paid' ? 'Đã nhận tiền' : 'Chưa xác nhận'}</Tag>
      {order.qrCode && order.state === 'Pending' && <div style={{ textAlign: 'center', marginTop: 16 }}>
        <QRCode value={order.qrCode} size={220} aria-label={`Mã QR nhận trước ${money(order.amountVnd)}`} />
      </div>}
    </> : <>
    <p>Tiền nhận trước được giữ riêng cho bệnh nhân và phiếu này. Chưa tính là doanh thu dịch vụ hoặc thanh toán cho việc đã làm.</p>
    <label>Loại khoản nhận trước</label>
    <Select value={kind} onChange={setKind} style={{ width: '100%', marginBottom: 16 }} disabled={busy}
      options={[{ value: 'Deposit', label: 'Đặt cọc' }, { value: 'ServiceAdvance', label: 'Ứng trước lộ trình điều trị' }]} />
    <label>Số tiền (đ)</label>
    <InputNumber value={amountVnd} onChange={v => setAmountVnd(v ?? 0)} min={1} precision={0}
      style={{ width: '100%', marginBottom: 16 }} disabled={busy} aria-label="Số tiền nhận trước" />
    <label>Người trả</label>
    <Input value={payer} onChange={e => setPayer(e.target.value)} maxLength={160} style={{ marginBottom: 16 }} disabled={busy} />
    <label>Ghi chú</label>
    <Input.TextArea value={note} onChange={e => setNote(e.target.value)} maxLength={1000} rows={3} disabled={busy} />
    <label>Thanh toán bằng</label>
    <Select value={method} onChange={setMethod} style={{ width: '100%', marginTop: 8 }} disabled={busy}
      options={[{ value: 'Cash', label: 'Tiền mặt' }, { value: 'Transfer', label: 'Chuyển khoản PayOS' }]} />
    <Alert type="info" showIcon style={{ marginTop: 16 }} message={method === 'Cash'
      ? `Sẽ ghi nhận ${money(amountVnd)} bằng tiền mặt sau khi lưu.`
      : `Sẽ tạo QR ${money(amountVnd)}. Chỉ ghi nhận sau khi PayOS xác nhận.`} />
    </>}
  </Modal>;
}
