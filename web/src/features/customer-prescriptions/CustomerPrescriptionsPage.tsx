import { Alert, Button, Card, Empty, Space, Spin, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import { formatDate, PrescriptionSheetView, type Prescription } from '../prescriptions/PrescriptionSheet';
import './customerPrescriptions.css';

// docs/specs/S23-PATIENT-PRESCRIPTIONS.vi.md — "Cổng bệnh nhân web": chỉ đọc đơn của hồ sơ đã liên kết VERIFIED.
const notice = 'Đơn thuốc mua ngoài. Phòng khám không bán thuốc tại chỗ.';

function useCustomerLoad<T>(path: string | null) {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [data, setData] = useState<T | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token || !path) return;
    let active = true;
    const controller = new AbortController();
    setBusy(true); setError(null); setData(null);
    void customerApi<T>(path, { signal: controller.signal }, token)
      .then(value => { if (active) setData(value); })
      .catch(failure => {
        if (!active) return;
        if (failure instanceof ApiError && failure.status === 401) expire();
        else setError(failure as Error);
      }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, path, revision, expire]);
  return { data, busy, error, reload: () => setRevision(value => value + 1) };
}

export default function CustomerPrescriptionsPage() {
  const { data, busy, error, reload } = useCustomerLoad<Prescription[]>('/api/patients/me/prescriptions');
  return <section className="customer-prescriptions-page">
    <div className="customer-page-heading"><span className="customer-kicker">Sức khỏe</span>
      <Typography.Title level={2}>Đơn thuốc của tôi</Typography.Title>
      <Typography.Paragraph>Xem thuốc, liều dùng và lời dặn bác sĩ đã kê cho hồ sơ bạn đã liên kết.</Typography.Paragraph></div>
    <Alert type="info" showIcon message={notice} style={{ marginBottom: 16 }} />
    {busy ? <div className="customer-rx-loading" role="status"><Spin /><span>Đang tải đơn thuốc…</span></div>
      : error ? <Alert type="error" showIcon message={error.message} action={<Button onClick={reload}>Thử lại</Button>} />
        : data?.length ? <div className="customer-rx-list">
          {data.map(item => <Link key={item.id} to={`/customer/prescriptions/${item.id}`} className="customer-rx-card"
            aria-label={`Đơn thuốc ngày ${formatDate(item.prescribedOn)} tại ${item.branchName}`}>
            <div className="customer-rx-card-head"><strong>{formatDate(item.prescribedOn)}</strong><span>{item.items.length} thuốc</span></div>
            <div className="customer-rx-card-clinic">{item.branchName}</div>
            <div className="customer-rx-card-meta">Bác sĩ {item.prescriberName}</div>
            {item.diagnosis && <div className="customer-rx-card-meta">Chẩn đoán: {item.diagnosis}</div>}
            <div className="customer-rx-card-drugs">{item.items.map(x => x.medicineName).join(', ')}</div>
            <span className="customer-text-action">Xem đơn thuốc <span aria-hidden="true">→</span></span>
          </Link>)}
        </div>
          : <Empty description={<span>Chưa có đơn thuốc nào. Đơn chỉ hiển thị khi tài khoản đã <Link to="/customer/patient-link">liên kết hồ sơ</Link> được xác minh.</span>} />}
  </section>;
}

export function CustomerPrescriptionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, busy, error, reload } = useCustomerLoad<Prescription>(id ? `/api/patients/me/prescriptions/${encodeURIComponent(id)}` : null);
  const missing = error instanceof ApiError && (error.status === 404 || error.status === 403);
  return <section className="customer-prescriptions-page">
    <Space style={{ marginBottom: 16 }}><Link to="/customer/prescriptions">← Đơn thuốc của tôi</Link></Space>
    {busy ? <div className="customer-rx-loading" role="status"><Spin /><span>Đang tải đơn thuốc…</span></div>
      : missing ? <Alert type="error" showIcon message="Không tìm thấy đơn thuốc trong hồ sơ đã liên kết của bạn." />
        : error ? <Alert type="error" showIcon message={error.message} action={<Button onClick={reload}>Thử lại</Button>} />
          : data && <>
            <Card className="customer-rx-sheet-card"><PrescriptionSheetView doc={data} /></Card>
            <Alert type="info" showIcon message={notice} style={{ marginTop: 16 }} />
          </>}
  </section>;
}
