import { Alert, Button, Empty, Select, Space, Table, Tag } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { formatClock, formatDate, localDate, statusColors, statusLabels, type AppointmentListItem } from '../appointments/types';

type AppointmentPage = { items: AppointmentListItem[]; total: number; page: number; pageSize: number };

export default function PatientAppointmentsTab({ patientId, active, revision }: { patientId: number; active: boolean; revision: number }) {
  const [scope, setScope] = useState('ALL');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<AppointmentPage | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (!active) return;
    let current = true;
    setLoading(true); setError(null); setResult(null);
    const query = new URLSearchParams({ scope, page: String(page), pageSize: String(pageSize) });
    api<AppointmentPage>(`/api/patients/${patientId}/appointments?${query}`)
      .then(value => { if (current) setResult(value); })
      .catch(failure => { if (current) setError((failure as Error).message); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [patientId, active, revision, scope, page, pageSize, retry]);
  return <>
    <Space className="patient-actions" wrap>
      <Select aria-label="Nhóm lịch hẹn" value={scope} style={{ width: 180 }}
        options={[{ value: 'ALL', label: 'Tất cả' }, { value: 'UPCOMING', label: 'Sắp tới' }, { value: 'HISTORY', label: 'Lịch sử' }]}
        onChange={value => { setScope(value); setPage(1); }} />
      <Button loading={loading} onClick={() => setRetry(value => value + 1)}>Tải lại lịch hẹn</Button>
    </Space>
    {error ? <Alert type="error" showIcon message={error} action={<Button onClick={() => setRetry(value => value + 1)}>Thử lại</Button>} /> :
      <Table<AppointmentListItem> rowKey="id" loading={loading} dataSource={result?.items ?? []} scroll={{ x: 850 }}
        locale={{ emptyText: loading ? 'Đang tải lịch hẹn…' : <Empty description="Không có lịch hẹn phù hợp của bệnh nhân trong phạm vi của bạn." /> }}
        pagination={{ current: page, pageSize, total: result?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
          onChange: (next, size) => { setPage(size !== pageSize ? 1 : next); setPageSize(size); } }}
        columns={[
          { title: 'Mã lịch hẹn', render: (_, a) => <Link to={`/appointments/${a.id}`}>{a.code}</Link> },
          { title: 'Thời gian', render: (_, a) => `${formatDate(localDate(a.startAt))} · ${formatClock(a.startAt)}–${formatClock(a.endAt)}` },
          { title: 'Trạng thái', render: (_, a) => <Tag color={statusColors[a.status]}>{statusLabels[a.status]}</Tag> },
          { title: 'Chi nhánh / ghế', render: (_, a) => `${a.branchName} · ${a.chairName}` },
          { title: 'Nha sĩ', dataIndex: 'dentistName' },
          { title: 'Dịch vụ', render: (_, a) => a.serviceName ?? '—' },
        ]} />}
  </>;
}
