import { Alert, Button, Card, Empty, Input, Space, Table, Tag } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import { addDays, formatClock, formatDate } from '../appointments/types';

type Booking = { id: string; code: string; branchId: number; branchName: string; startAt: string; endAt: string;
  patientName: string; serviceName: string; status: string; isTreatmentPlan: boolean; patientConsentConfirmedAt: string | null };
const states: Record<string, string> = { Confirmed: 'Đã xác nhận', CheckedIn: 'Đã tiếp nhận', InProgress: 'Đang khám', Completed: 'Đã hoàn tất', Cancelled: 'Đã hủy', NoShow: 'Không đến' };
const monday = (day: string) => addDays(day, -((new Date(`${day}T00:00:00Z`).getUTCDay() + 6) % 7));
const clinicDay = (instant: string) => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(instant));

export default function DoctorAppointmentsPanel({ dentistId, date }: { dentistId: number; date: string }) {
  const has = usePermissions();
  const [selected, setSelected] = useState(date), [revision, setRevision] = useState(0);
  const [items, setItems] = useState<Booking[]>([]), [loading, setLoading] = useState(false), [error, setError] = useState<string>();
  const from = monday(selected), to = addDays(from, 6);
  useEffect(() => setSelected(date), [dentistId, date]);
  useEffect(() => {
    const refresh = () => setRevision(value => value + 1);
    window.addEventListener('focus', refresh);
    return () => window.removeEventListener('focus', refresh);
  }, []);
  useEffect(() => {
    let current = true; setLoading(true); setError(undefined); setItems([]);
    void api<{ items: Booking[] }>(`/api/working-schedules/appointments?dentistId=${dentistId}&from=${from}&to=${to}`)
      .then(result => { if (current) setItems(result.items); })
      .catch(failure => { if (current) setError((failure as Error).message); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [dentistId, from, to, revision]);
  return <Card title="Lịch hẹn của bác sĩ trong tuần" aria-label="Lịch hẹn của bác sĩ trong tuần">
    <Space wrap style={{ marginBottom: 12 }}>
      <Button aria-label="Tuần trước của lịch hẹn" onClick={() => setSelected(addDays(from, -7))}>Tuần trước</Button>
      <Input type="date" aria-label="Xem lịch hẹn theo ngày" value={selected} onChange={event => { if (event.target.value) setSelected(event.target.value); }} />
      <Button aria-label="Tuần sau của lịch hẹn" onClick={() => setSelected(addDays(from, 7))}>Tuần sau</Button>
      <Button loading={loading} onClick={() => setRevision(value => value + 1)}>Tải lại lịch hẹn</Button>
    </Space>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại lịch hẹn</Button>} />}
    <Table<Booking> rowKey="id" size="small" loading={loading} dataSource={items} scroll={{ x: 900 }} pagination={{ pageSize: 10, hideOnSinglePage: true }}
      locale={{ emptyText: <Empty description="Chưa có lịch hẹn trong tuần này." /> }} columns={[
        { title: 'Thời gian', render: (_, row) => <>{formatDate(clinicDay(row.startAt))}<br />{formatClock(row.startAt)}–{formatClock(row.endAt)}</> },
        { title: 'Mã lịch', render: (_, row) => has('appointments.read') ? <Link to={`/appointments/${row.id}`}>{row.code}</Link> : row.code },
        { title: 'Bệnh nhân', dataIndex: 'patientName' }, { title: 'Dịch vụ', dataIndex: 'serviceName' }, { title: 'Chi nhánh', dataIndex: 'branchName' },
        { title: 'Trạng thái', render: (_, row) => <Tag color={row.status === 'Cancelled' ? 'red' : row.status === 'Confirmed' ? 'blue' : 'default'}>{states[row.status] ?? row.status}</Tag> },
        { title: 'Đồng ý đặt lịch', render: (_, row) => !row.isTreatmentPlan ? '—' : row.patientConsentConfirmedAt
          ? <Tag color="green" title={new Date(row.patientConsentConfirmedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}>Đã đồng ý</Tag>
          : <Tag>Chưa ghi nhận</Tag> },
      ]} />
  </Card>;
}
