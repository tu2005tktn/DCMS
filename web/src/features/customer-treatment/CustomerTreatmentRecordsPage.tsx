import { Alert, Button, Card, Empty, Spin, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';

type PerformedItem = { id: string; performedAt: string; serviceName: string; region: string | null;
  quantity: number; unit: string; outcome: string | null };
type PlanItem = { id: string; serviceName: string; region: string | null; quantity: number; unit: string; progress: string };
type Dossier = { dossierId: string; code: string; state: string; diagnosis: string; startedOn: string;
  agreedItems: PlanItem[]; performedItems: PerformedItem[] };

export default function CustomerTreatmentRecordsPage() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [rows, setRows] = useState<Dossier[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!token) return;
    let active = true;
    setRows(null); setError(null);
    void customerApi<Dossier[]>('/api/patients/me/treatment-summary', {}, token)
      .then(value => { if (active) setRows(value); })
      .catch(failure => {
        if (!active) return;
        if (failure instanceof ApiError && failure.status === 401) expire();
        else setError(failure instanceof ApiError && failure.status === 403
          ? 'Hãy xác minh liên kết hồ sơ để xem quá trình điều trị.'
          : (failure as Error).message);
      });
    return () => { active = false; };
  }, [token, reload, expire]);
  return <main className="customer-page" style={{ maxWidth: 980, margin: '0 auto', padding: 24 }}>
    <Typography.Title level={2}>Hồ sơ điều trị</Typography.Title>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setReload(x => x + 1)}>Thử lại</Button>} />}
    {!error && rows === null && <Spin />}
    {!error && rows?.length === 0 && <Empty description="Chưa có hồ sơ điều trị đã xác nhận" />}
    {rows?.map(dossier => <Card key={dossier.dossierId} title={`${dossier.code} · ${dossier.diagnosis}`} style={{ marginBottom: 20 }}
      extra={<Tag color={dossier.state === 'Open' ? 'blue' : 'green'}>{dossier.state === 'Open' ? 'Đang điều trị' : 'Đã kết thúc'}</Tag>}>
      <Typography.Text type="secondary">Bắt đầu {new Date(dossier.startedOn).toLocaleDateString('vi-VN')}</Typography.Text>
      <Typography.Title level={4}>Hạng mục bác sĩ đã làm</Typography.Title>
      {(dossier.performedItems ?? []).length === 0 ? <Empty description="Chưa có hạng mục đã ghi nhận" image={Empty.PRESENTED_IMAGE_SIMPLE} />
        : dossier.performedItems.map(item => <div key={item.id} style={{ borderTop: '1px solid #e5eaf0', padding: '12px 0' }}>
          <strong>{item.serviceName}{item.region ? ` · ${item.region}` : ''}</strong>
          <div>{new Date(item.performedAt).toLocaleDateString('vi-VN')} · {item.quantity} {item.unit}</div>
          {item.outcome && <div>Kết quả: {item.outcome}</div>}
        </div>)}
      <Typography.Title level={4}>Kế hoạch điều trị</Typography.Title>
      {dossier.agreedItems.map(item => <div key={item.id} style={{ borderTop: '1px solid #e5eaf0', padding: '10px 0' }}>
        {item.serviceName}{item.region ? ` · ${item.region}` : ''} · {item.quantity} {item.unit}
      </div>)}
    </Card>)}
  </main>;
}
