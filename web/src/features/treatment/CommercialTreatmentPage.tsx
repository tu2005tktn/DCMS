import { Alert, Button, Card, Empty, Form, Input, InputNumber, Modal, Skeleton, Space, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../services/api';
import type { ServicePrice, TreatmentChange, TreatmentItem } from './types';
import { money } from './types';
import './treatment.css';

type CommercialListItem = { dossierId: string; code: string; branchId: number;
  version: number; pendingDiscounts: number };
type CommercialPlan = { dossierId: string; code: string; branchId: number; version: number;
  planState: string; items: TreatmentItem[]; depositExceptionReason: string | null;
  preview: { grossVnd: number; approvedDiscountVnd: number; netBeforeTaxVnd: number } };

export default function CommercialTreatmentPage() {
  const [search, setSearch] = useSearchParams();
  const selectedId = search.get('dossier');
  const [list, setList] = useState<CommercialListItem[] | null>(null);
  const [prices, setPrices] = useState<ServicePrice[] | null>(null);
  const [plan, setPlan] = useState<CommercialPlan | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [priceEditing, setPriceEditing] = useState<ServicePrice | null>(null);
  const [depositOpen, setDepositOpen] = useState(false);
  const [priceForm] = Form.useForm<{ referenceUnitPriceVnd: number | null }>();
  const [depositForm] = Form.useForm<{ reason: string }>();
  const requestKey = useRef(crypto.randomUUID());

  async function reload() {
    const [dossiers, catalog] = await Promise.all([
      api<CommercialListItem[]>('/api/commercial/dossiers'), api<ServicePrice[]>('/api/service-prices'),
    ]);
    setList(dossiers); setPrices(catalog);
    if (selectedId) setPlan(await api<CommercialPlan>(`/api/commercial/dossiers/${selectedId}/plan`));
    else setPlan(null);
  }
  useEffect(() => {
    let active = true;
    Promise.all([api<CommercialListItem[]>('/api/commercial/dossiers'),
      api<ServicePrice[]>('/api/service-prices'),
      selectedId ? api<CommercialPlan>(`/api/commercial/dossiers/${selectedId}/plan`) : Promise.resolve(null)])
      .then(([dossiers, catalog, selected]) => { if (active) { setList(dossiers); setPrices(catalog); setPlan(selected); setError(null); } })
      .catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; };
  }, [selectedId]);

  async function decide(item: TreatmentItem, decision: 'Approved' | 'Rejected') {
    if (!plan || busy) return;
    setBusy(true); setError(null); setNotice(null);
    try {
      const result = await api<TreatmentChange>(`/api/commercial/dossiers/${plan.dossierId}/items/${item.id}/discount/decision`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: plan.version, decision,
          requestKey: requestKey.current }),
      });
      setNotice(result.notification.message); requestKey.current = crypto.randomUUID();
      await reload();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  async function savePrice(values: { referenceUnitPriceVnd: number | null }) {
    if (!priceEditing || busy) return;
    setBusy(true); setError(null);
    try {
      await api<ServicePrice>(`/api/service-prices/${priceEditing.serviceId}`, { method: 'PUT',
        body: JSON.stringify({ expectedVersion: priceEditing.priceVersion,
          referenceUnitPriceVnd: values.referenceUnitPriceVnd ?? null, requestKey: requestKey.current }) });
      requestKey.current = crypto.randomUUID(); setPriceEditing(null); setNotice('Đã cập nhật giá tham chiếu. Kế hoạch cũ giữ nguyên snapshot.');
      await reload();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  async function approveDeposit(values: { reason: string }) {
    if (!plan || busy) return;
    setBusy(true); setError(null);
    try {
      const result = await api<TreatmentChange>(`/api/commercial/dossiers/${plan.dossierId}/deposit-exception`, {
        method: 'POST', body: JSON.stringify({ expectedVersion: plan.version, reason: values.reason,
          requestKey: requestKey.current }),
      });
      requestKey.current = crypto.randomUUID(); setDepositOpen(false); depositForm.resetFields();
      setNotice(result.notification.message); await reload();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <main className="treatment-page">
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Duyệt thương mại điều trị</Typography.Title>
      <Typography.Paragraph>Bác sĩ trong chi nhánh được phân công, Admin hoặc chủ phòng khám có thể cập nhật giá tham chiếu, duyệt giảm trừ và ngoại lệ cọc. Các thao tác này không ghi nhận khoản thu.</Typography.Paragraph></div></div>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => void reload()}>Thử lại</Button>} />}
    {notice && <Alert type="success" showIcon message={notice} />}
    <div className="treatment-detail-grid">
      <Card title="Kế hoạch đang soạn">{list === null ? <Skeleton active /> : list.length === 0
        ? <Empty description="Chưa có kế hoạch nháp cần xem." />
        : list.map(row => <div className="treatment-item-row" key={row.dossierId}>
          <div><strong>{row.code}</strong><div>Chi nhánh #{row.branchId}</div>
            {row.pendingDiscounts > 0 && <Tag color="orange">{row.pendingDiscounts} giảm trừ chờ duyệt</Tag>}</div>
          <Button type={row.dossierId === selectedId ? 'primary' : 'default'}
            onClick={() => setSearch({ dossier: row.dossierId })}>Xem</Button>
        </div>)}</Card>
      <Card title={plan ? `Kế hoạch ${plan.code}` : 'Chi tiết kế hoạch'}>{!plan
        ? <Empty description="Chọn kế hoạch để xem giảm trừ." /> : <>
          {plan.items.map(item => <div className="treatment-item-row" key={item.id}>
            <div><strong>{item.serviceName} {item.region || ''}</strong>
              <div>Giá thỏa thuận: {money(item.agreedUnitPriceVnd)} × {item.quantity}</div>
              {item.discount && <><div>Đề xuất: {item.discount.type === 'Fixed'
                ? money(item.discount.value) : `${item.discount.value}%`} · {item.discount.reason}</div>
                {item.discount.qualifyingItemId && <div className="treatment-muted">Điều kiện: hạng mục #{item.discount.qualifyingItemId.slice(0, 8)}</div>}
                <Tag color={item.discount.status === 'Approved' ? 'green' : 'orange'}>{item.discount.status}</Tag></>}</div>
            {item.discount?.status === 'Proposed' && <Space><Button disabled={busy} onClick={() => void decide(item, 'Rejected')}>Từ chối</Button>
              <Button type="primary" loading={busy} onClick={() => void decide(item, 'Approved')}>Duyệt</Button></Space>}
          </div>)}
          <div className="treatment-price-summary"><span>Giá trước giảm: {money(plan.preview.grossVnd)}</span>
            <span>Đã duyệt: {money(plan.preview.approvedDiscountVnd)}</span>
            <strong>Dự kiến trước thuế: {money(plan.preview.netBeforeTaxVnd)}</strong></div>
          <Button onClick={() => { requestKey.current = crypto.randomUUID(); setDepositOpen(true); }}>
            {plan.depositExceptionReason ? 'Sửa ngoại lệ không cọc' : 'Duyệt ngoại lệ không cọc'}</Button>
          {plan.depositExceptionReason && <Typography.Paragraph>Lý do đã duyệt: {plan.depositExceptionReason}</Typography.Paragraph>}
        </>}</Card>
    </div>
    <Card title="Giá tham chiếu dịch vụ" extra={<Typography.Text type="secondary">Thay đổi catalog không sửa giá đã chốt</Typography.Text>}>
      {prices === null ? <Skeleton active /> : prices.length === 0 ? <Empty description="Chưa có dịch vụ trong phạm vi chi nhánh." />
        : prices.map(price => <div key={price.serviceId} className="treatment-item-row">
          <div><strong>{price.name}</strong><div>Chi nhánh #{price.branchId} · {price.referenceUnitPriceVnd == null
            ? 'Chưa có giá tham chiếu' : money(price.referenceUnitPriceVnd)}</div></div>
          <Button onClick={() => { priceForm.setFieldsValue({ referenceUnitPriceVnd: price.referenceUnitPriceVnd });
            requestKey.current = crypto.randomUUID(); setPriceEditing(price); }}>Cập nhật giá</Button>
        </div>)}</Card>
    <Modal open={priceEditing !== null} title={`Giá tham chiếu · ${priceEditing?.name ?? ''}`} okText="Lưu giá"
      okButtonProps={{ loading: busy }} onOk={() => priceForm.submit()} onCancel={() => !busy && setPriceEditing(null)}>
      <Form form={priceForm} layout="vertical" onFinish={values => void savePrice(values)}>
        <Form.Item name="referenceUnitPriceVnd" label="Giá tham chiếu mỗi đơn vị (VND)">
          <InputNumber min={0} precision={0} className="treatment-number-full" /></Form.Item>
      </Form></Modal>
    <Modal open={depositOpen} title="Ngoại lệ không cọc" okText="Duyệt ngoại lệ"
      okButtonProps={{ loading: busy }} onOk={() => depositForm.submit()}
      onCancel={() => !busy && setDepositOpen(false)}>
      <Form form={depositForm} layout="vertical" onFinish={values => void approveDeposit(values)}>
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true }]}>
          <Input.TextArea rows={3} maxLength={1000} /></Form.Item>
      </Form></Modal>
  </main>;
}
