import { Alert, Button, Card, Empty, Space, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../authentication/AuthContext';
import RequestDrawer from '../support-inbox/RequestDrawer';
import { supportInboxApi } from '../support-inbox/supportInbox.api';
import { kindLabels, statusColors, statusLabels, type SupportRequestDto, type SupportRequestPage } from '../support-inbox/types';
import AppointmentDetailContent from './AppointmentDetailContent';

export default function AppointmentDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  return <Detail key={id} id={id} />;
}

function Detail({ id }: { id: string }) {
  const { user } = useAuth();
  const canHandleSupport = user?.permissions.includes('appointments.support.handle') ?? false;
  const [revision, setRevision] = useState(0);
  return <>
    <Link to="/appointments">← Lịch hẹn</Link>
    <div className="page-heading"><Typography.Title level={2}>Chi tiết lịch hẹn</Typography.Title>
      <Button onClick={() => setRevision(value => value + 1)}>Tải lại</Button></div>
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Card><AppointmentDetailContent id={id} revision={revision} /></Card>
      {canHandleSupport && <AppointmentRequests id={id} revision={revision} onChanged={() => setRevision(value => value + 1)} />}
    </Space>
  </>;
}

function AppointmentRequests({ id, revision, onChanged }: { id: string; revision: number; onChanged: () => void }) {
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<SupportRequestPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    setLoading(true); setError(null); setResult(null);
    supportInboxApi.listRequestsForStaff({ appointmentId: id,
      statuses: ['Pending', 'Resolved', 'Rejected', 'Withdrawn'], page, pageSize: 10 })
      .then(value => { if (active) setResult(value); })
      .catch(failure => { if (active) setError((failure as Error).message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id, page, revision]);
  return <Card title="Yêu cầu đổi / hủy của lịch hẹn">
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={onChanged}>Thử lại</Button>} /> :
      <Table<SupportRequestDto> rowKey="id" loading={loading} dataSource={result?.items ?? []}
        locale={{ emptyText: <Empty description="Lịch hẹn này chưa có yêu cầu hỗ trợ." /> }}
        pagination={{ current: page, pageSize: 10, total: result?.total ?? 0, showSizeChanger: false, onChange: setPage }}
        columns={[
          { title: 'Yêu cầu', render: (_, request) => <Button type="link" onClick={() => setSelected(request.id)}>{request.id}</Button> },
          { title: 'Loại', render: (_, request) => kindLabels[request.kind] },
          { title: 'Trạng thái', render: (_, request) => <Tag color={statusColors[request.status]}>{statusLabels[request.status]}</Tag> },
          { title: 'Lý do', dataIndex: 'reason' },
        ]} />}
    <RequestDrawer id={selected} refreshToken={0} onClose={() => setSelected(null)} onChanged={onChanged} />
  </Card>;
}
