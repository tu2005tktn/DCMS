import { usePermissions } from '../authentication/usePermissions';
import { Alert, Button, Card, Empty, Form, InputNumber, Modal, Select, Skeleton, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../services/api';
import type { ServicePrice } from './types';
import { money } from './types';
import './treatment.css';

export default function CommercialTreatmentPage() {
  const has = usePermissions();
  const [prices, setPrices] = useState<ServicePrice[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [priceEditing, setPriceEditing] = useState<ServicePrice | null>(null);
  const [priceForm] = Form.useForm<{ referenceUnitPriceVnd: number | null; unit?: string }>();
  const requestKey = useRef(crypto.randomUUID());

  async function reload() {
    setPrices(await api<ServicePrice[]>('/api/service-prices'));
  }
  useEffect(() => {
    let active = true;
    api<ServicePrice[]>('/api/service-prices')
      .then(catalog => { if (active) { setPrices(catalog); setError(null); } })
      .catch(failure => { if (active) setError((failure as Error).message); });
    return () => { active = false; };
  }, []);

  async function savePrice(values: { referenceUnitPriceVnd: number | null; unit?: string }) {
    if (!priceEditing || busy) return;
    setBusy(true); setError(null);
    try {
      await api<ServicePrice>(`/api/service-prices/${priceEditing.serviceId}`, { method: 'PUT',
        body: JSON.stringify({ expectedVersion: priceEditing.priceVersion,
          referenceUnitPriceVnd: values.referenceUnitPriceVnd ?? null, unit: values.unit, requestKey: requestKey.current }) });
      requestKey.current = crypto.randomUUID(); setPriceEditing(null); setNotice('Đã cập nhật giá tham chiếu. Kế hoạch cũ giữ nguyên snapshot.');
      await reload();
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <main className="treatment-page">
    <div className="treatment-page-heading"><div><Typography.Title level={2}>Giá tham chiếu điều trị</Typography.Title></div></div>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => void reload()}>Thử lại</Button>} />}
    {notice && <Alert type="success" showIcon message={notice} />}
    <Card title="Giá tham chiếu dịch vụ">
      {prices === null ? <Skeleton active /> : prices.length === 0 ? <Empty description="Chưa có dịch vụ trong phạm vi chi nhánh." />
        : prices.map(price => <div key={price.serviceId} className="treatment-item-row">
          <div><strong>{price.name}</strong><div>Chi nhánh #{price.branchId} · {price.referenceUnitPriceVnd == null
            ? 'Chưa có giá tham chiếu' : money(price.referenceUnitPriceVnd)}</div></div>
          <Button disabled={!has('treatment.prices.manage')} onClick={() => { priceForm.setFieldsValue({ referenceUnitPriceVnd: price.referenceUnitPriceVnd, unit: price.unit ?? 'lần' });
            requestKey.current = crypto.randomUUID(); setPriceEditing(price); }}>Cập nhật giá</Button>
        </div>)}</Card>
    <Modal open={priceEditing !== null} title={`Giá tham chiếu · ${priceEditing?.name ?? ''}`} okText="Lưu giá"
      okButtonProps={{ loading: busy }} onOk={() => priceForm.submit()} onCancel={() => !busy && setPriceEditing(null)}>
      <Form form={priceForm} layout="vertical" onFinish={values => void savePrice(values)}>
        <Form.Item name="unit" label="Đơn vị dịch vụ"><Select options={['lần','răng','hàm','gói','g','ml'].map(v => ({ value: v, label: v }))} /></Form.Item>
        <Form.Item name="referenceUnitPriceVnd" label="Giá tham chiếu mỗi đơn vị (VND)">
          <InputNumber min={0} precision={0} className="treatment-number-full" /></Form.Item>
      </Form></Modal>
  </main>;
}
