import { Alert, Button, Card, Input, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import BookingDrawer, { type BookingPrefill } from '../appointments/BookingDrawer';
import { formatClock, formatDate, statusLabels } from '../appointments/types';
import { arrivalClock, purposeLabels, visitLabels, type PageResult, type ReceptionAppointment, type ReceptionOptions, type VisitResult } from './types';

export default function ReceptionPage() {
  const navigate = useNavigate();
  const [options, setOptions] = useState<ReceptionOptions>();
  const [branchId, setBranch] = useState<number>(); const [date, setDate] = useState('');
  const [dentistId, setDentist] = useState<number>(); const [keyword, setKeyword] = useState('');
  const [tab, setTab] = useState('appointments'); const [status, setStatus] = useState<string>();
  const [page, setPage] = useState(1); const [pageSize, setPageSize] = useState(20);
  const [appointments, setAppointments] = useState<PageResult<ReceptionAppointment>>();
  const [queue, setQueue] = useState<PageResult<VisitResult>>();
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
    if (!branchId || !date) return;
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
  const current = tab === 'appointments' ? appointments : queue;
  const clinicDate = current?.clinicDate ?? options?.clinicDate;
  const pagination = { current: page, pageSize, total: current?.total ?? 0, showSizeChanger: true, pageSizeOptions: [20, 50, 100],
    onChange: (p: number, size: number) => { setPage(size === pageSize ? p : 1); setPageSize(size); } };
  function reset() { setPage(1); setStatus(undefined); }
  return <>
    <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
      <Typography.Title level={2}>Tiếp nhận {date && (date === clinicDate ? 'hôm nay' : formatDate(date))}</Typography.Title>
      <Space><Button onClick={() => setRevision(x => x + 1)}>Tải lại</Button>{options?.canCheckIn && branchId &&
        <Button type="primary" onClick={() => setBooking({ branchId, date, dentistId, kind: 'WalkIn', source: 'InPerson' })}>Xếp lịch khách vãng lai</Button>}</Space>
    </Space>
    <Typography.Paragraph>Lịch hẹn và lượt khám có trạng thái riêng. Đối chiếu người thực sự đến trước khi tiếp nhận.</Typography.Paragraph>
    <Card><Space wrap>
      <Select aria-label="Chi nhánh tiếp nhận" placeholder="Chi nhánh" style={{ minWidth: 220 }} value={branchId}
        options={options?.calendar.branches.map(x => ({ value: x.id, label: x.name }))} onChange={value => { setBranch(value); setDentist(undefined); reset(); }} />
      <Input aria-label="Ngày tiếp nhận" type="date" value={date} onChange={e => { setDate(e.target.value); reset(); }} />
      <Select aria-label="Bác sĩ tiếp nhận" allowClear placeholder="Bác sĩ" style={{ minWidth: 180 }} value={dentistId}
        options={options?.calendar.dentists.filter(x => x.branchId === branchId).map(x => ({ value: x.id, label: x.name }))}
        onChange={value => { setDentist(value); reset(); }} />
      <Select aria-label="Trạng thái tiếp nhận" allowClear placeholder="Trạng thái" style={{ minWidth: 160 }} value={status}
        options={Object.entries(tab === 'appointments' ? statusLabels : visitLabels).map(([value, label]) => ({ value, label }))}
        onChange={value => { setStatus(value); setPage(1); }} />
      <Input.Search aria-label="Tìm người đến khám" maxLength={100} placeholder="Tên, mã lịch hoặc mã bệnh nhân" onSearch={value => { setKeyword(value.trim()); setPage(1); }} />
    </Space></Card>
    {error && <Alert style={{ marginTop: 16 }} type="error" showIcon message={error} description="Chưa tải được dữ liệu. Vui lòng tải lại." />}
    {options && options.calendar.branches.length === 0 && <Alert type="info" message="Bạn chưa có chi nhánh được phép tiếp nhận." />}
    <Tabs activeKey={tab} onChange={value => { setTab(value); reset(); }} items={[
      { key: 'appointments', label: 'Lịch hẹn', children: !error && <Table<ReceptionAppointment> rowKey="appointmentId" loading={busy}
        dataSource={appointments?.items} pagination={pagination} scroll={{ x: 1000 }} columns={[
          { title: 'Lịch', key: 'time', render: (_, row) => <><div>{formatClock(row.startAt)}–{formatClock(row.endAt)}</div><Typography.Text type="secondary">{row.code}</Typography.Text></> },
          { title: 'Người khám', key: 'patient', render: (_, row) => <><strong>{row.attendeeName}</strong><div>{row.patientCode ?? 'Chưa gắn hồ sơ'}</div></> },
          { title: 'Người đặt', dataIndex: 'bookerName' }, { title: 'Bác sĩ', dataIndex: 'dentistName' }, { title: 'Ghế', dataIndex: 'chairName' },
          { title: 'Trạng thái', dataIndex: 'status', render: value => <Tag>{statusLabels[value as keyof typeof statusLabels] ?? value}</Tag> },
          { title: 'Thao tác', key: 'action', render: (_, row) => row.visitId ? <Link to={`/visits/${row.visitId}`}>Mở lượt khám</Link>
            : <Link to={`/reception/appointments/${row.appointmentId}`}>{row.canCheckIn ? 'Tiếp nhận' : 'Xem điều kiện tiếp nhận'}</Link> },
        ]} /> },
      { key: 'queue', label: 'Hàng chờ và lượt khám', children: !error && <Table<VisitResult> rowKey="visitId" loading={busy}
        dataSource={queue?.items} pagination={pagination} scroll={{ x: 1000 }} columns={[
          { title: 'Bệnh nhân', key: 'patient', render: (_, row) => <><strong>{row.patientName}</strong><div>{row.patientCode}</div></> },
          { title: 'Giờ đến', dataIndex: 'arrivalAt', render: arrivalClock },
          { title: 'Chờ', key: 'wait', render: (_, row) => row.status === 'Queued' ? `${row.waitingMinutes} phút` : '—' },
          { title: 'Bác sĩ', dataIndex: 'dentistName' }, { title: 'Lý do đến', dataIndex: 'purpose', render: value => purposeLabels[value] ?? value },
          { title: 'Trạng thái lượt', dataIndex: 'status', render: value => <Tag color={value === 'Queued' ? 'blue' : undefined}>{visitLabels[value] ?? value}</Tag> },
          { title: 'Thao tác', key: 'open', render: (_, row) => <Link to={row.openVisitPath}>Mở lượt khám</Link> },
        ]} /> },
    ]} />
    <BookingDrawer open={booking !== null} prefill={booking} options={options?.calendar ?? null} onClose={() => setBooking(null)}
      onBooked={result => { setBooking(null); navigate(`/reception/appointments/${result.appointmentId}`); }} />
  </>;
}
