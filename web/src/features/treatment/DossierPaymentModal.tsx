import { Alert, Button, Input, InputNumber, Modal, Select, Skeleton } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { money } from './types';
import type { PaymentContext, PayOsOrder } from './sheetTypes';
import { usePermissions } from '../authentication/usePermissions';
import TransferBill from './TransferBill';

const today = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date());
const day = (v: string) => new Date(v).toLocaleDateString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric' });
const group = (v: number | string | undefined) => `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
const digits = (v: string | undefined) => Number((v ?? '').replace(/\D/g, '')) || 0;

// Records a payment against the posted activities of one dossier. See docs/specs/TREATMENT-DOSSIER-PAYMENTS.md.
export default function DossierPaymentModal({ dossierId, open, onClose, onSaved }: {
  dossierId: string; open: boolean; onClose: () => void; onSaved: () => void;
}) {
  const has = usePermissions();
  const [ctx, setCtx] = useState<PaymentContext | null>(null);
  const [loading, setLoading] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<string | null>(null);
  const [amounts, setAmounts] = useState<Record<string, number>>({});
  const [paidOn, setPaidOn] = useState(today()), [method, setMethod] = useState<'Cash' | 'Transfer'>('Cash');
  const [collectorId, setCollectorId] = useState<number>(), [note, setNote] = useState('');
  const [showBill, setShowBill] = useState(false);
  const [order, setOrder] = useState<PayOsOrder | null>(null);
  const paidNotice = useRef(false);
  const pending = useRef<{ payload: string; key: string } | null>(null);

  async function load(keepForm = false) {
    setLoading(true);
    try {
      const value = await api<PaymentContext>(`/api/dossiers/${dossierId}/payments/context`);
      setCtx(value);
      setAmounts(current => Object.fromEntries(value.lines.map(l => [l.activityId, Math.min(keepForm ? current[l.activityId] ?? 0 : 0, l.dueVnd)])));
      if (!keepForm) {
        setCollectorId(value.defaultCollectorId ?? undefined);
        const active = await api<PayOsOrder | null>(`/api/dossiers/${dossierId}/payos-orders/active`);
        if (active) {
          setOrder(active); setAmounts(Object.fromEntries(active.lines.map(l => [l.activityId, l.amountVnd])));
          setPaidOn(active.paidOn); setMethod('Transfer'); setShowBill(true);
        }
      }
    } catch (failure) { setError((failure as Error).message); }
    finally { setLoading(false); }
  }
  useEffect(() => {
    if (!open) return;
    setError(null); setPaidOn(today()); setMethod('Cash'); setNote(''); setCtx(null); setShowBill(false); setOrder(null); paidNotice.current = false; pending.current = null;
    void load();
  }, [open, dossierId]);

  const total = useMemo(() => Object.values(amounts).reduce((a, b) => a + b, 0), [amounts]);
  const totalDue = useMemo(() => ctx?.lines.reduce((s, l) => s + l.dueVnd, 0) ?? 0, [ctx]);
  const hasDue = totalDue > 0;
  // Typing a total spreads it over the activities, oldest first, never above each line's remaining debt.
  function spread(value: number) {
    let rest = Math.min(Math.max(value, 0), totalDue);
    setAmounts(Object.fromEntries((ctx?.lines ?? []).map(l => { const part = Math.min(l.dueVnd, rest); rest -= part; return [l.activityId, part]; })));
  }
  const canSave = has('finance.payments.create') && (method === 'Cash' || has('finance.payos.create')) && !!ctx && !busy && total > 0 && !!collectorId && !!paidOn && paidOn <= today();
  function payload() { return { paidOn, method, collectorId, note: note.trim() || null,
    lines: Object.entries(amounts).filter(([, amount]) => amount > 0).map(([activityId, amountVnd]) => ({ activityId, amountVnd })) }; }
  async function prepareBill() {
    if (!canSave) return;
    const serialized = JSON.stringify(payload());
    if (pending.current?.payload !== serialized) pending.current = { payload: serialized, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try {
      const body = payload();
      const result = await api<PayOsOrder>(`/api/dossiers/${dossierId}/payos-orders`, { method: 'POST',
        body: JSON.stringify({ paidOn: body.paidOn, collectorId: body.collectorId, note: body.note,
          lines: body.lines, requestKey: pending.current.key }) });
      setOrder(result); setShowBill(true);
      if (result.state === 'Paid' && !paidNotice.current) { paidNotice.current = true; onSaved(); }
    } catch (failure) {
      const apiError = failure as ApiError;
      setError(apiError.status === 409 ? 'Phiếu đã có yêu cầu chuyển khoản chờ hoặc số dư đã đổi. Kiểm tra lịch sử và tải lại phiếu.' : apiError.message);
      pending.current = null;
      if (apiError.status === 409) await load(true);
    } finally { setBusy(false); }
  }

  useEffect(() => {
    if (!showBill || !order || !['Creating', 'Pending'].includes(order.state)) return;
    let active = true;
    const timer = window.setInterval(() => {
      void api<PayOsOrder>(`/api/dossiers/${dossierId}/payos-orders/${order.id}`)
        .then(next => {
          if (!active) return;
          setOrder(next);
          if (next.state === 'Paid' && !paidNotice.current) { paidNotice.current = true; onSaved(); }
        }).catch(failure => { if (active) setError((failure as Error).message); });
    }, 3000);
    return () => { active = false; window.clearInterval(timer); };
  }, [showBill, order?.id, order?.state, dossierId]);

  async function save() {
    if (!canSave || !collectorId || method !== 'Cash') return;
    const body = payload();
    const serialized = JSON.stringify(body);
    if (pending.current?.payload !== serialized) pending.current = { payload: serialized, key: crypto.randomUUID() };
    setBusy(true); setError(null);
    try {
      await api(`/api/dossiers/${dossierId}/payments`, { method: 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      pending.current = null; onSaved(); onClose();
    } catch (failure) {
      const apiError = failure as ApiError;
      setError(apiError.status === 409
        ? 'Số tiền vượt phần còn nợ hiện tại hoặc phiếu vừa thay đổi. Đã tải lại phần còn nợ; kiểm tra lại số tiền rồi lưu.'
        : apiError.message);
      if (apiError.status === 409) { pending.current = null; setShowBill(false); await load(true); }
    } finally { setBusy(false); }
  }

  return <Modal open={open} title={showBill ? 'Bill chuyển khoản PayOS' : 'Tạo thanh toán mới'} width={showBill ? 960 : 640} onCancel={() => { if (!busy) onClose(); }} maskClosable={false}
    footer={<div className="dossier-payment-footer">
      <Button onClick={showBill ? () => setShowBill(false) : onClose} disabled={busy}>QUAY LẠI</Button>
      {method === 'Transfer' && !showBill
        ? <Button type="primary" onClick={() => void prepareBill()} loading={busy} disabled={!canSave}>TẠO QR VÀ XEM BILL</Button>
        : !showBill && <Button type="primary" className="dossier-payment-save" onClick={() => void save()} loading={busy}
          disabled={!canSave}>LƯU LẠI</Button>}
    </div>} destroyOnClose>
    {showBill && ctx && order ? <>
      {error && <Alert role="alert" type="error" showIcon message={error} />}
      <TransferBill context={ctx} paidOn={paidOn} amounts={amounts} order={order} />
    </> : <div className="dossier-payment-form">
      {error && <Alert role="alert" type="error" showIcon message={error} />}
      <div className="dossier-payment-row"><label htmlFor="payment-date">{method === 'Transfer' ? 'Ngày điều trị trên bill' : 'Ngày thanh toán'}</label>
        <Input id="payment-date" type="date" value={paidOn} max={today()} onChange={e => setPaidOn(e.target.value)} disabled={busy} /></div>
      <p className="dossier-payment-hint">Để có thống kê chính xác cho từng dịch vụ và bác sĩ, nên nhập số tiền thanh toán cho từng dịch vụ/thủ thuật dưới đây thay vì nhập một số tổng tiền.</p>
      <div className="dossier-payment-row"><label htmlFor="payment-total">Tổng tiền thanh toán (đ)</label>
        <InputNumber id="payment-total" className="dossier-payment-total" min={0} max={totalDue} precision={0} controls={false}
          formatter={group} parser={digits} value={total} disabled={busy || !hasDue} onChange={v => spread(Number(v) || 0)} />
        {hasDue && total === 0 && <small className="dossier-payment-help">Nhập tổng tiền để tự chia vào các việc bên dưới (tối đa {money(totalDue)}).</small>}</div>
      {loading && !ctx ? <Skeleton active paragraph={{ rows: 3 }} /> : ctx && <div className="dossier-payment-table-scroll">
        <table className="dossier-payment-table">
          <thead><tr><th>Ngày ĐT/DV</th><th>Bác sĩ</th><th>Điều trị/dịch vụ</th><th>Số tiền TT (đ)</th></tr></thead>
          <tbody>{ctx.lines.map(line => <tr key={line.activityId}>
            <td>{day(line.performedAt)}</td><td>{line.dentistName}</td><td>{line.serviceName ?? '—'}</td>
            <td><InputNumber className="dossier-payment-amount" min={0} max={line.dueVnd} precision={0} controls={false}
              aria-label={`Số tiền thanh toán ${line.serviceName ?? 'việc đã làm'} ngày ${day(line.performedAt)}`}
              formatter={group} parser={digits} value={amounts[line.activityId] ?? 0} disabled={busy || line.dueVnd === 0}
              onChange={v => setAmounts(a => ({ ...a, [line.activityId]: Math.min(Math.max(Number(v) || 0, 0), line.dueVnd) }))} />
              <small>{line.dueVnd === 0 ? 'Đã thanh toán đủ' : `Còn nợ ${money(line.dueVnd)}`}</small></td>
          </tr>)}</tbody>
        </table>
        {ctx.lines.length === 0 && <Alert type="info" showIcon message="Phiếu chưa có việc đã làm để thanh toán." />}
        {ctx.lines.length > 0 && !hasDue && <Alert type="success" showIcon message="Tất cả việc đã làm đã được thanh toán đủ." />}
      </div>}
      <div className="dossier-payment-row"><label htmlFor="payment-method">Thanh toán bằng</label>
        <Select id="payment-method" value={method} onChange={setMethod} disabled={busy}
          options={[{ value: 'Cash', label: 'Tiền mặt' }, { value: 'Transfer', label: 'Chuyển khoản', disabled: !has('finance.payos.create') }]} /></div>
      <div className="dossier-payment-row"><label htmlFor="payment-collector">Người thu tiền</label>
        <Select id="payment-collector" value={collectorId} onChange={setCollectorId} disabled={busy} placeholder="Chọn người thu tiền" showSearch optionFilterProp="label"
          options={(ctx?.collectors ?? []).map(c => ({ value: c.id, label: c.name }))} /></div>
      <div className="dossier-payment-row"><label htmlFor="payment-note">Ghi chú</label>
        <Input.TextArea id="payment-note" rows={3} maxLength={1000} value={note} onChange={e => setNote(e.target.value)} disabled={busy} /></div>
    </div>}
  </Modal>;
}
