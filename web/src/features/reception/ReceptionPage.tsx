import { ReloadOutlined } from '@ant-design/icons';
import type { ColumnType } from 'antd/es/table';
import { Alert, Button, Card, Input, message, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import './reception.css';
import { api } from '../../services/api';
import BookingDrawer, { type BookingPrefill } from '../appointments/BookingDrawer';
import StaffAppointmentActions from '../appointments/StaffAppointmentActions';
import { formatClock, formatDate, localDate, statusLabels, statusView, type AppointmentListItem } from '../appointments/types';
import { arrivalClock, purposeLabels, visitLabels, type PageResult, type ReceptionAppointment, type ReceptionOptions, type VisitResult } from './types';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

function withLabels<R>(columns: ColumnType<R>[]): ColumnType<R>[] {
  return columns.map(c => ({ ...c, onCell: () => ({ 'data-label': String(c.title ?? '') } as object) }));
}

const toListItem = (row: ReceptionAppointment): AppointmentListItem => ({
  id: row.appointmentId, code: row.code, branchId: row.branchId, branchName: row.branchName, startAt: row.startAt, endAt: row.endAt,
  durationMinutes: Math.round((Date.parse(row.endAt) - Date.parse(row.startAt)) / 60000), dentistId: row.dentistId, dentistName: row.dentistName,
  chairId: row.chairId, chairName: row.chairName, serviceName: null, attendeeName: row.attendeeName, attendeeRelation: row.attendeeRelation,
  kind: '', source: '', status: row.status, version: row.version, canUndoNoShow: row.canUndoNoShow,
});
const statusTag = (row: ReceptionAppointment) => { const view = statusView(row); return <Tag color={view.color}>{view.label}</Tag>; };
// The server lists these last; the table dims them.
const closedStatuses: ReceptionAppointment['status'][] = ['Completed', 'NoShow', 'Cancelled'];

export default function ReceptionPage() {
  const canPickBranch = useCanPickBranch();
  const has = usePermissions();
  const navigate = useNavigate();
  const [options, setOptions] = useState<ReceptionOptions>();
  const [branchId, setBranch] = useState<number>(); const [date, setDate] = useState('');
  const [dentistId, setDentist] = useState<number>(); const [keyword, setKeyword] = useState('');
  const [tab, setTab] = useState('appointments'); const [status, setStatus] = useState<string>();
  const [page, setPage] = useState(1); const [pageSize, setPageSize] = useState(20);
  const [appointments, setAppointments] = useState<PageResult<ReceptionAppointment>>();
  const [queue, setQueue] = useState<PageResult<VisitResult>>();
  const [overdue, setOverdue] = useState<PageResult<ReceptionAppointment>>();
  const [toast, toastContext] = message.useMessage();
  const [busy, setBusy] = useState(false); const [error, setError] = useState<string>();
  const [revision, setRevision] = useState(0); const [booking, setBooking] = useState<BookingPrefill | null>(null);
  useEffect(() => { let active = true;
    api<ReceptionOptions>('/api/reception/options').then(result => {
      if (active) { setOptions(result); setBranch(result.calendar.branches[0]?.id); setDate(result.clinicDate); }
    }).catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, []);
  useEffect(() => { const timer = window.setInterval(() => setRevision(x => x + 1), 20000); return () => window.clearInterval(timer); }, []);
  useEffect(() => {
    if (!branchId || !date || tab === 'overdue') return;
    let active = true; setBusy(true); setError(undefined);
    const query = new URLSearchParams({ branchId: String(branchId), date, page: String(page), pageSize: String(pageSize) });
    if (dentistId) query.set('dentistId', String(dentistId)); if (keyword) query.set('keyword', keyword); if (status) query.set('status', status);
    const load = tab === 'appointments'
      ? api<PageResult<ReceptionAppointment>>(`/api/reception/appointments?${query}`).then(result => { if (active) setAppointments(result); })
      : api<PageResult<VisitResult>>(`/api/reception/queue?${query}`).then(result => { if (active) setQueue(result); });
    void load.catch(e => { if (active) { setError((e as Error).message); setAppointments(undefined); setQueue(undefined); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [branchId, date, dentistId, keyword, tab, status, page, pageSize, revision]);
  // Loaded on every tab so the tab title can show how many bookings still wait for a decision.
  useEffect(() => {
    if (!branchId) return;
    let active = true;
    const open = tab === 'overdue';
    const query = new URLSearchParams({ branchId: String(branchId), page: String(open ? page : 1), pageSize: String(open ? pageSize : 20) });
    if (dentistId) query.set('dentistId', String(dentistId)); if (keyword) query.set('keyword', keyword);
    if (open) { setBusy(true); setError(undefined); }
    api<PageResult<ReceptionAppointment>>(`/api/reception/overdue?${query}`)
      .then(result => { if (active) setOverdue(result); })
      .catch(e => { if (active) { setOverdue(undefined); if (open) setError((e as Error).message); } })
      .finally(() => { if (active && open) setBusy(false); });
    return () => { active = false; };
  }, [branchId, dentistId, keyword, tab, page, pageSize, revision]);
  const current = tab === 'appointments' ? appointments : tab === 'queue' ? queue : overdue;
  const clinicDate = current?.clinicDate ?? options?.clinicDate;
  const pagination = { current: page, pageSize, total: current?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
    onChange: (p: number, size: number) => { setPage(size === pageSize ? p : 1); setPageSize(size); } };
  function reset() { setPage(1); setStatus(undefined); }
  const attendance = (row: ReceptionAppointment) => <StaffAppointmentActions attendanceOnly appointment={toListItem(row)}
    onChanged={notice => { void toast.success(notice); setRevision(x => x + 1); }} />;
  return <>
    {toastContext}
    <div className="page-heading">
      <Typography.Title level={2} style={{ margin: 0 }}>Tiếp nhận {date && (date === clinicDate ? 'hôm nay' : formatDate(date))}</Typography.Title>
      <Space><Button icon={<ReloadOutlined />} onClick={() => setRevision(x => x + 1)}>Tải lại</Button>{has('appointments.create') && options?.canCheckIn && branchId &&
        <Button type="primary" onClick={() => setBooking({ branchId, date, dentistId, kind: 'WalkIn', source: 'InPerson' })}>Xếp lịch khách vãng lai</Button>}</Space>
    </div>
    <Card className="reception-panel"><Space wrap>
      {canPickBranch && (options?.calendar.branches.length ?? 0) > 1 && <Select aria-label="Chi nhánh tiếp nhận" placeholder="Chi nhánh" style={{ minWidth: 220 }} value={branchId}
        options={options?.calendar.branches.map(x => ({ value: x.id, label: x.name }))} onChange={value => { setBranch(value); setDentist(undefined); reset(); }} />}
      <Input aria-label="Ngày tiếp nhận" type="date" value={date} disabled={tab === 'overdue'} onChange={e => { setDate(e.target.value); reset(); }} />
      <Select aria-label="Bác sĩ tiếp nhận" allowClear placeholder="Bác sĩ" style={{ minWidth: 180 }} value={dentistId}
        options={options?.calendar.dentists.filter(x => x.branchId === branchId).map(x => ({ value: x.id, label: x.name }))}
        onChange={value => { setDentist(value); reset(); }} />
      {tab !== 'overdue' && <Select aria-label="Trạng thái tiếp nhận" allowClear placeholder="Trạng thái" style={{ minWidth: 160 }} value={status}
        options={Object.entries(tab === 'appointments' ? statusLabels : visitLabels).map(([value, label]) => ({ value, label }))}
        onChange={value => { setStatus(value); setPage(1); }} />}
      <Input.Search aria-label="Tìm người đến khám" maxLength={100} placeholder="Tên, mã lịch hoặc mã bệnh nhân" onSearch={value => { setKeyword(value.trim()); setPage(1); }} />
    </Space>
    {error && <Alert style={{ marginTop: 16 }} type="error" showIcon message={error} description="Chưa tải được dữ liệu. Vui lòng tải lại." />}
    {options && options.calendar.branches.length === 0 && <Alert type="info" message="Bạn chưa có chi nhánh được phép tiếp nhận." />}
    <Tabs activeKey={tab} onChange={value => { setTab(value); reset(); }} items={[
      { key: 'appointments', label: 'Lịch hẹn', children: !error && <Table<ReceptionAppointment> rowKey="appointmentId" loading={busy}
        dataSource={appointments?.items} pagination={pagination} className="reception-table"
        rowClassName={row => closedStatuses.includes(row.status) ? 'reception-row-closed' : ''} columns={withLabels<ReceptionAppointment>([
          { title: 'Lịch', key: 'time', render: (_, row) => <>{formatClock(row.startAt)}–{formatClock(row.endAt)}</> },
          { title: 'Người khám', key: 'patient', render: (_, row) => <strong>{row.attendeeName}</strong> },
          { title: 'Người đặt', dataIndex: 'bookerName' }, { title: 'Bác sĩ', dataIndex: 'dentistName' }, { title: 'Ghế', dataIndex: 'chairName' },
          { title: 'Trạng thái', key: 'status', render: (_, row) => statusTag(row) },
          { title: 'Thao tác', key: 'action', render: (_, row) => row.visitId ? <Link to={`/visits/${row.visitId}`}>Mở lượt khám</Link>
            : <div className="reception-actions"><Link to={`/reception/appointments/${row.appointmentId}`}>{row.canCheckIn ? 'Tiếp nhận' : 'Xem điều kiện tiếp nhận'}</Link>{attendance(row)}</div> },
        ])} /> },
      { key: 'queue', label: 'Hàng chờ và lượt khám', children: !error && <Table<VisitResult> rowKey="visitId" loading={busy}
        dataSource={queue?.items} pagination={pagination} className="reception-table" columns={withLabels<VisitResult>([
          { title: 'Bệnh nhân', key: 'patient', render: (_, row) => <strong>{row.patientName}</strong> },
          { title: 'Giờ đến', dataIndex: 'arrivalAt', render: arrivalClock },
          { title: 'Chờ', key: 'wait', render: (_, row) => row.status === 'Queued' ? `${row.waitingMinutes} phút` : '—' },
          { title: 'Bác sĩ', dataIndex: 'dentistName' }, { title: 'Lý do đến', dataIndex: 'purpose', render: value => purposeLabels[value] ?? value },
          { title: 'Trạng thái lượt', dataIndex: 'status', render: value => <Tag color={value === 'Queued' ? 'blue' : undefined}>{visitLabels[value] ?? value}</Tag> },
          { title: 'Thao tác', key: 'open', render: (_, row) => <Link to={row.openVisitPath}>Mở lượt khám</Link> },
        ])} /> },
      { key: 'overdue', label: `Quá giờ chưa xử lý${overdue ? ` (${overdue.total})` : ''}`, children: !error && <Table<ReceptionAppointment> rowKey="appointmentId" loading={busy}
        dataSource={overdue?.items} pagination={{ ...pagination, total: overdue?.total ?? 0 }} className="reception-table"
        locale={{ emptyText: 'Không còn lịch quá giờ cần xử lý.' }} columns={withLabels<ReceptionAppointment>([
          { title: 'Ngày giờ', key: 'time', render: (_, row) => <>{formatDate(localDate(row.startAt))} {formatClock(row.startAt)}–{formatClock(row.endAt)}</> },
          { title: 'Người khám', key: 'patient', render: (_, row) => <strong>{row.attendeeName}</strong> },
          { title: 'Người đặt', dataIndex: 'bookerName' }, { title: 'Liên hệ', key: 'contact', render: (_, row) => row.bookerPhone || row.bookerEmail || '—' },
          { title: 'Bác sĩ', dataIndex: 'dentistName' }, { title: 'Trạng thái', key: 'status', render: (_, row) => statusTag(row) },
          { title: 'Thao tác', key: 'action', render: (_, row) => attendance(row) },
        ])} /> },
    ]} /></Card>
    <BookingDrawer open={booking !== null} prefill={booking} options={options?.calendar ?? null} onClose={() => setBooking(null)}
      onBooked={result => { setBooking(null); navigate(`/reception/appointments/${result.appointmentId}`); }} />
  </>;
}
import { usePermissions } from '../authentication/usePermissions';
