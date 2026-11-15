import { Alert, Button, InputNumber, Modal, QRCode, Segmented, Spin, Tabs } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { money } from './types';

type PaymentLine = { activityId: string; performedAt: string; serviceName: string; priceVnd: number; paidVnd: number; dueVnd: number };
type LabFee = { labOrderId: string; activityId: string; workName: string; totalFeeVnd: number; paidVnd: number; dueVnd: number; providerPaidVnd: number; minimum30PercentVnd: number; priorityRemainingVnd: number };
type PaymentContext = { dossierId: string; lines: PaymentLine[]; advanceReceivedVnd: number; labFees: LabFee[]; totalCostVnd: number; labReceivedVnd: number };
type PayOsState = { order: { id: string; purpose: string; amountVnd: number; qrCode: string; bankBin: string; accountNumber: string; accountName: string; expiresAt: string; lines: { activityId: string; amountVnd: number }[]; labLines: { labOrderId: string; amountVnd: number }[] }; status: string; context: PaymentContext };

export default function DossierPaymentModal({ dossierId, open, onClose, onSaved }: {
  dossierId: string; open: boolean; onClose: () => void; onSaved: () => void;
}) {
  const [context, setContext] = useState<PaymentContext | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [tab, setTab] = useState('due');
  const [amounts, setAmounts] = useState<Record<string, number>>({});
  const [labAmounts, setLabAmounts] = useState<Record<string, number>>({});
  const [advanceAmount, setAdvanceAmount] = useState(0);
  const [method, setMethod] = useState<'Cash' | 'Transfer'>('Cash');
  const [order, setOrder] = useState<PayOsState | null>(null);
  const pending = useRef<{ signature: string; key: string } | null>(null);

  useEffect(() => {
    if (!open) return;
    let active = true;
    setContext(null); setAmounts({}); setLabAmounts({}); setAdvanceAmount(0); setOrder(null); setTab('due'); setError(''); setLoading(true); pending.current = null;
    api<PaymentContext>(`/api/dossiers/${dossierId}/payments/context`)
      .then(value => { if (active) setContext(value); })
      .catch(reason => { if (active) setError(reason instanceof ApiError ? reason.message : 'Không tải được dữ liệu thanh toán.'); })
      .finally(() => { if (active) setLoading(false); });
    Promise.all(['payos-orders', 'advance-payos-orders'].map(kind => api<PayOsState | null>(`/api/dossiers/${dossierId}/payments/${kind}/active`)))
      .then(states => { if (active) setOrder(states.find(Boolean) ?? null); }).catch(() => { /* Context error is displayed separately. */ });
    return () => { active = false; };
  }, [dossierId, open]);

  useEffect(() => {
    if (!open || !order || order.status !== 'PENDING') return;
    const timer = window.setInterval(() => {
      const kind = order.order.purpose === 'Advance' ? 'advance-payos-orders' : 'payos-orders';
      void api<PayOsState>(`/api/dossiers/${dossierId}/payments/${kind}/${order.order.id}`)
        .then(value => {
          setOrder(value);
          if (value.status === 'PAID') { setContext(value.context); setAmounts({}); setLabAmounts({}); onSaved(); }
        }).catch(reason => setError(reason instanceof ApiError ? reason.message : 'Không kiểm tra được trạng thái payOS.'));
    }, 5000);
    return () => window.clearInterval(timer);
  }, [dossierId, onSaved, open, order]);

  const due = context?.lines.filter(line => line.dueVnd > 0) ?? [];
  const paid = context?.lines.filter(line => line.priceVnd > 0 && line.dueVnd === 0) ?? [];
  const paidLabs = context?.labFees.filter(lab => lab.totalFeeVnd > 0 && lab.dueVnd === 0) ?? [];
  const selected = due.map(line => ({ activityId: line.activityId, amountVnd: amounts[line.activityId] ?? 0 }))
    .filter(line => line.amountVnd > 0);
  const selectedLabs = (context?.labFees ?? []).filter(lab => lab.dueVnd > 0)
    .map(lab => ({ labOrderId: lab.labOrderId, amountVnd: labAmounts[lab.labOrderId] ?? 0 }))
    .filter(lab => lab.amountVnd > 0);
  const total = selected.reduce((sum, line) => sum + line.amountVnd, 0)
    + selectedLabs.reduce((sum, lab) => sum + lab.amountVnd, 0);
  const labFees = context?.labFees.filter(lab => lab.dueVnd > 0) ?? [];
  const labRemaining = labFees.reduce((sum, lab) => sum + lab.priorityRemainingVnd, 0);
  const violatesLabPriority = selected.length > 0 && labFees.some(lab =>
    lab.paidVnd + (labAmounts[lab.labOrderId] ?? 0) < lab.minimum30PercentVnd);

  async function save() {
    if ((tab === 'advance' ? advanceAmount <= 0 : total <= 0 || violatesLabPriority) || saving) return;
    setSaving(true); setError('');
    try {
      const signature = JSON.stringify({ tab, method, selected, selectedLabs, advanceAmount });
      if (pending.current?.signature !== signature) pending.current = { signature, key: crypto.randomUUID() };
      const advance = tab === 'advance';
      const transfer = method === 'Transfer';
      const path = transfer ? (advance ? 'advance-payos-orders' : 'payos-orders') : (advance ? 'advances' : '');
      const updated = await api<PaymentContext | PayOsState>(`/api/dossiers/${dossierId}/payments${path ? `/${path}` : ''}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestKey: pending.current.key, paidOn: new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }),
          lines: advance ? [] : selected, labLines: advance ? [] : selectedLabs, amountVnd: advance ? advanceAmount : null }),
      });
      pending.current = null;
      if (transfer) { setOrder(updated as PayOsState); }
      else {
        const value = updated as PaymentContext;
        setContext(value); setAmounts({}); setLabAmounts({}); setAdvanceAmount(0);
        if (selected.some(item => value.lines.some(line => line.activityId === item.activityId && line.dueVnd === 0))) setTab('paid');
        onSaved();
      }
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Không thể lưu khoản thu. Vui lòng thử lại.');
    } finally { setSaving(false); }
  }

  const columns = (lines: PaymentLine[], editable: boolean) => lines.length ? <div style={{ overflowX: 'auto' }}><table className="treatment-sheet-table" style={{ width: '100%' }}>
    <thead><tr><th>Ngày điều trị</th><th>Dịch vụ</th><th>Chi phí</th><th>Đã trả</th><th>Còn nợ</th>{editable && <th>Thu lần này (₫)</th>}</tr></thead>
    <tbody>{lines.map(line => <tr key={line.activityId}>
      <td>{new Date(line.performedAt).toLocaleDateString('vi-VN')}</td><td>{line.serviceName}</td>
      <td>{money(line.priceVnd)}</td><td>{money(line.paidVnd)}</td><td>{money(line.dueVnd)}</td>
      {editable && <td><InputNumber aria-label={`Số tiền thu cho ${line.serviceName}`} min={0} max={line.dueVnd} precision={0} style={{ width: 150 }}
        value={amounts[line.activityId] ?? 0} disabled={saving}
        onChange={value => setAmounts(previous => ({ ...previous, [line.activityId]: value ?? 0 }))} /></td>}
    </tr>)}</tbody></table></div> : <p>{editable ? 'Không còn dịch vụ cần thanh toán.' : 'Chưa có dịch vụ đã thanh toán đủ.'}</p>;

  return <Modal open={open} title="Thanh toán phiếu điều trị" onCancel={onClose} width={960} footer={null} destroyOnClose>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    {order && <div style={{ border: '1px solid #d9d9d9', borderRadius: 8, padding: 20, marginBottom: 16 }}>
      <h3>Bill chuyển khoản {order.order.purpose === 'Advance' ? 'tiền cọc' : 'dịch vụ điều trị'}</h3>
      {order.order.purpose === 'Treatment' && <ul>{order.order.lines.map(line => <li key={line.activityId}>
        {context?.lines.find(item => item.activityId === line.activityId)?.serviceName ?? 'Dịch vụ'}: {money(line.amountVnd)}</li>)}</ul>}
      <p><strong>Tổng tiền: {money(order.order.amountVnd)}</strong></p>
      <p>Trạng thái: {order.status === 'PAID' ? 'Đã thanh toán' : order.status === 'PENDING' ? 'Đang chờ chuyển khoản' : order.status}</p>
      {order.status === 'PENDING' && <div style={{ display: 'flex', gap: 20, alignItems: 'center', flexWrap: 'wrap' }}>
        <QRCode value={order.order.qrCode} size={200} /><div><p>Ngân hàng BIN: {order.order.bankBin}</p><p>Số tài khoản: {order.order.accountNumber}</p><p>Chủ tài khoản: {order.order.accountName}</p>
        <p>Hết hạn: {new Date(order.order.expiresAt).toLocaleString('vi-VN')}</p></div></div>}
    </div>}
    {loading ? <Spin /> : <Tabs activeKey={tab} onChange={setTab} items={[
      { key: 'due', label: `Cần thanh toán (${due.length + labFees.length})`, children: <>
        <p><strong>Tổng chi phí gồm dịch vụ và labo: {money(context?.totalCostVnd ?? 0)}</strong></p>
        {!!labFees.length && <div style={{ overflowX: 'auto', marginBottom: 16 }}><h4>Phí labo cần thu riêng</h4>
          <table className="treatment-sheet-table" style={{ width: '100%' }}><thead><tr><th>Phiếu labo</th><th>Chi phí</th><th>Đã thu</th><th>Còn nợ</th><th>Thu lần này (₫)</th></tr></thead>
          <tbody>{labFees.map(lab => <tr key={lab.labOrderId}><td>{lab.workName}</td><td>{money(lab.totalFeeVnd)}</td>
            <td>{money(lab.paidVnd)}</td><td>{money(lab.dueVnd)}</td><td><InputNumber min={0} max={lab.dueVnd} precision={0}
            aria-label={`Số tiền thu cho labo ${lab.workName}`} value={labAmounts[lab.labOrderId] ?? 0} disabled={saving}
            onChange={value => setLabAmounts(previous => ({ ...previous, [lab.labOrderId]: value ?? 0 }))} /></td></tr>)}</tbody></table>
          {labRemaining > 0 && <p>Trước khi thu dịch vụ, cần thu tối thiểu {money(labRemaining)} phí labo. Tiền thu labo không tính vào doanh thu.</p>}
        </div>}
        {columns(due, true)}{violatesLabPriority && <Alert type="warning" showIcon message="Cần thu đủ ít nhất 30% phí labo trước khi thu dịch vụ." style={{ marginTop: 12 }} />}
        <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: 16, marginTop: 20 }}>
        <strong>Tổng thu: {money(total)}</strong><Segmented value={method} onChange={value => setMethod(value as 'Cash' | 'Transfer')} options={[{ label: 'Tiền mặt', value: 'Cash' }, { label: 'Chuyển khoản', value: 'Transfer' }]} />
        <Button type="primary" loading={saving} disabled={total <= 0 || violatesLabPriority || order?.status === 'PENDING'} onClick={() => void save()}>{method === 'Cash' ? 'Xác nhận thu tiền mặt' : 'Tạo QR và xem bill'}</Button>
      </div></> },
      { key: 'paid', label: `Lịch sử dịch vụ đã thanh toán (${paid.length + paidLabs.length})`, children: <>{columns(paid, false)}{paidLabs.length > 0 && <div><h4>Phí labo đã thanh toán</h4><ul>{paidLabs.map(lab => <li key={lab.labOrderId}>{lab.workName}: {money(lab.paidVnd)}</li>)}</ul></div>}</> },
      { key: 'advance', label: 'Nhận trước (cọc)', children: <><p>Tiền nhận trước đã xác nhận: <strong>{money(context?.advanceReceivedVnd ?? 0)}</strong>. Khoản này chưa trừ nợ dịch vụ.</p>
        {!!labFees.length && <p>Khoản nhận trước mới sẽ ưu tiên thu riêng tối đa {money(labRemaining)} phí labo; phần còn lại ghi là tiền cọc.</p>}
        <InputNumber aria-label="Số tiền cọc" min={1} precision={0} value={advanceAmount} onChange={value => setAdvanceAmount(value ?? 0)} style={{ width: 180 }} />
        <Segmented value={method} onChange={value => setMethod(value as 'Cash' | 'Transfer')} options={[{ label: 'Tiền mặt', value: 'Cash' }, { label: 'Chuyển khoản', value: 'Transfer' }]} style={{ margin: '0 12px' }} />
        <Button type="primary" loading={saving} disabled={advanceAmount <= 0 || order?.status === 'PENDING'} onClick={() => void save()}>{method === 'Cash' ? 'Ghi nhận tiền cọc' : 'Tạo QR cọc'}</Button></> },
    ]} />}
  </Modal>;
}
