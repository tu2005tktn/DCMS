import { Alert, Button, Card, Empty, Pagination, Spin, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';
import './customerPrescriptions.css';

type Summary = { id: string; prescribedOn: string; branchName: string; diagnosis: string; prescriberName: string; requiresExternalPurchase: boolean };
type Page = { items: Summary[]; total: number; page: number; pageSize: number };
type Item = { medicineName: string; strength: string | null; dosageForm: string | null; unit: string; quantity: number; instructions: string; isExternalPurchase: boolean };
type Detail = Summary & { otherDiagnosis: string | null; advice: string | null; items: Item[] };

function formatDate(value: string) { const [y, m, d] = value.split('-'); return `${d}/${m}/${y}`; }
function Failure({ error, retry }: { error: ApiError | Error; retry: () => void }) {
  if (error instanceof ApiError && error.status === 403) return <Card><Empty description="Bạn cần xác minh liên kết hồ sơ bệnh nhân để xem đơn thuốc." />
    <Link to="/customer/patient-link">Liên kết hồ sơ của tôi</Link></Card>;
  return <Alert type="error" showIcon message={error.message} action={<Button onClick={retry}>Thử lại</Button>} />;
}

export function CustomerPrescriptionsPage() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Page | null>(null);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const [busy, setBusy] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController(); let active = true;
    setBusy(true); setError(null); setData(null);
    void customerApi<Page>(`/api/me/prescriptions?page=${page}`, { signal: controller.signal }, token)
      .then(value => { if (active) setData(value); })
      .catch(failure => { if (!active) return; if (failure instanceof ApiError && failure.status === 401) expire(); else setError(failure); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, page, revision, expire]);
  return <section className="customer-prescriptions"><div className="customer-page-heading"><span className="customer-kicker">Hồ sơ điều trị</span>
    <Typography.Title level={2}>Đơn thuốc của tôi</Typography.Title><Typography.Paragraph>Đơn do nha sĩ kê cho hồ sơ đã xác minh của bạn.</Typography.Paragraph></div>
    <Button onClick={() => setRevision(x => x + 1)} loading={busy}>Tải lại</Button>
    {error ? <Failure error={error} retry={() => setRevision(x => x + 1)} /> : busy ? <Spin tip="Đang tải đơn thuốc"><div className="customer-loading" /></Spin>
      : !data?.items.length ? <Empty description="Bạn chưa có đơn thuốc nào." /> : <>
        <div className="customer-prescription-grid">{data.items.map(item => <Card key={item.id} className="customer-prescription-card">
          <Typography.Text type="secondary">{formatDate(item.prescribedOn)} · {item.branchName}</Typography.Text>
          <h3>{item.diagnosis}</h3><p>Nha sĩ: {item.prescriberName}</p>
          {item.requiresExternalPurchase && <Tag color="blue">Có thuốc mua ngoài</Tag>}
          <p><Link to={`/customer/prescriptions/${item.id}`}>Xem đơn thuốc</Link></p>
        </Card>)}</div>
        <Pagination current={page} total={data.total} pageSize={20} showSizeChanger={false} onChange={setPage} />
      </>}
  </section>;
}

export function CustomerPrescriptionDetailPage() {
  const { id } = useParams(); const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [data, setData] = useState<Detail | null>(null);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const [busy, setBusy] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!token || !id) return;
    const controller = new AbortController(); let active = true;
    setBusy(true); setError(null); setData(null);
    void customerApi<Detail>(`/api/me/prescriptions/${encodeURIComponent(id)}`, { signal: controller.signal }, token)
      .then(value => { if (active) setData(value); })
      .catch(failure => { if (!active) return; if (failure instanceof ApiError && failure.status === 401) expire(); else setError(failure); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; controller.abort(); };
  }, [token, id, revision, expire]);
  return <section className="customer-prescriptions"><Link to="/customer/prescriptions">← Tất cả đơn thuốc</Link>
    <Typography.Title level={2}>Chi tiết đơn thuốc</Typography.Title>
    {error ? <Failure error={error} retry={() => setRevision(x => x + 1)} /> : busy ? <Spin /> : data && <Card>
      <p>Ngày kê: {formatDate(data.prescribedOn)} · {data.branchName}</p><p>Nha sĩ: {data.prescriberName}</p>
      <p><strong>Chẩn đoán:</strong> {data.diagnosis}</p>{data.otherDiagnosis && <p><strong>Chẩn đoán khác:</strong> {data.otherDiagnosis}</p>}
      <Typography.Title level={4}>Thuốc được kê</Typography.Title>
      <div className="customer-prescription-items">{data.items.map((item, index) => <div key={index} className="customer-prescription-item">
        <strong>{index + 1}. {item.medicineName}</strong> {item.isExternalPurchase ? <Tag color="blue">Mua ngoài</Tag> : <Tag color="green">Thuốc phòng khám</Tag>}
        <p>{[item.strength, item.dosageForm].filter(Boolean).join(' · ')} · Số lượng: {item.quantity} {item.unit}</p>
        <p>Hướng dẫn: {item.instructions || 'Theo hướng dẫn của nha sĩ'}</p>
      </div>)}</div>
      {data.advice && <p><strong>Lời dặn:</strong> {data.advice}</p>}
    </Card>}
  </section>;
}
