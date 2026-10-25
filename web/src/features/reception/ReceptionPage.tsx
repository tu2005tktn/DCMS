import { Alert, Button, Card, Empty, Input, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../../services/api';
import BookingDrawer, { type BookingPrefill } from '../appointments/BookingDrawer';
import { clinicToday, statusLabels, type AppointmentStatus } from '../appointments/types';
import { clinicTime, failureText, purposeLabels, visitLabels, type ReceptionAppointment, type ReceptionOptions, type ReceptionView, type Visit } from './types';

export default function ReceptionPage() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [options, setOptions] = useState<ReceptionOptions>();
  const [branchId, setBranchId] = useState<number>();
  const [date, setDate] = useState(params.get('date') ?? clinicToday());
  const [dentistId, setDentistId] = useState<number>();
  const [appointmentStatus, setAppointmentStatus] = useState<string>();
  const [visitStatus, setVisitStatus] = useState<string>();
  const [keyword, setKeyword] = useState('');
  const [view, setView] = useState<ReceptionView>();
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  const [booking, setBooking] = useState<BookingPrefill | null>(null);
  const [walkInArrival, setWalkInArrival] = useState<string>();
  useEffect(() => { let active = true;
    api<ReceptionOptions>('/api/reception/options').then(o => { if (active) { setOptions(o); setBranchId(o.calendar.branches[0]?.id); } })
      .catch(e => { if (active) { setError(failureText(e)); setLoading(false); } });
    return () => { active = false; };
  }, []);
  useEffect(() => { const timer = setInterval(() => setRevision(v => v + 1), 30_000); return () => clearInterval(timer); }, []);
  useEffect(() => {
    if (!branchId) return;
    let active = true;
    const query = new URLSearchParams({ branchId: String(branchId), date, keyword });
    if (dentistId) query.set('dentistId', String(dentistId));
    if (appointmentStatus) query.set('appointmentStatus', appointmentStatus);
    if (visitStatus) query.set('visitStatus', visitStatus);
    setLoading(true); setError(undefined);
    const debounce = setTimeout(() => { void api<ReceptionView>(`/api/reception?${query}`).then(v => { if (active) setView(v); })
      .catch(e => { if (active) { setError(failureText(e)); setView(undefined); } }).finally(() => { if (active) setLoading(false); }); }, 200);
    return () => { active = false; clearTimeout(debounce); };
  }, [branchId, date, dentistId, appointmentStatus, visitStatus, keyword, revision]);

  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <Typography.Title level={2}>{date === clinicToday() ? 'Tiếp nhận hôm nay' : `Tiếp nhận ngày ${date}`}</Typography.Title>
    <Typography.Text type="secondary">Ngày tiếp nhận theo Asia/Ho_Chi_Minh. Xác nhận sẽ đến chưa đưa bệnh nhân vào hàng chờ.</Typography.Text>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />}
    {!options && loading && <Spin aria-label="Đang tải quyền tiếp nhận" />}
    {options && options.calendar.branches.length === 0 && <Empty description="Bạn chưa được phân công chi nhánh tiếp nhận." />}
    {options && <Card><Space wrap>
      <Select aria-label="Chi nhánh tiếp nhận" value={branchId} style={{ minWidth: 230 }} options={options.calendar.branches.map(x => ({ value: x.id, label: x.name }))}
        onChange={id => { setBranchId(id); setDentistId(undefined); setView(undefined); }} />
      <Input aria-label="Ngày tiếp nhận" type="date" value={date} onChange={e => { if (e.target.value) { setDate(e.target.value); setParams({ date: e.target.value }); setView(undefined); } }} />
      <Select aria-label="Bác sĩ tiếp nhận" placeholder="Tất cả bác sĩ" allowClear style={{ minWidth: 190 }} value={dentistId} onChange={setDentistId}
        options={options.calendar.dentists.filter(x => x.branchId === branchId).map(x => ({ value: x.id, label: x.name }))} />
      <Select aria-label="Trạng thái lịch hẹn" placeholder="Trạng thái lịch hẹn" allowClear style={{ minWidth: 190 }} value={appointmentStatus} onChange={setAppointmentStatus}
        options={Object.entries(statusLabels).map(([value, label]) => ({ value, label }))} />
      <Select aria-label="Trạng thái lượt khám" placeholder="Trạng thái lượt khám" allowClear style={{ minWidth: 180 }} value={visitStatus} onChange={setVisitStatus}
        options={Object.entries(visitLabels).map(([value, label]) => ({ value, label }))} />
      <Input aria-label="Tìm tên hoặc mã" placeholder="Tên / mã lịch / mã Patient" maxLength={100} showCount value={keyword} onChange={e => setKeyword(e.target.value)} />
      <Button onClick={() => setRevision(v => v + 1)} loading={loading}>Tải lại</Button>
      {options.canCheckIn && <Button type="primary" disabled={!branchId || date !== clinicToday()} onClick={() => {
        setWalkInArrival(new Date().toISOString()); setBooking({ branchId: branchId!, date, kind: 'WalkIn', source: 'InPerson' });
      }}>Khách chưa có lịch · S11</Button>}
    </Space></Card>}
    <Card title="Lịch hẹn chưa tiếp nhận">
      <Table<ReceptionAppointment> rowKey="appointmentId" loading={loading} dataSource={view?.notArrived ?? []} scroll={{ x: 1000 }} pagination={{ pageSize: 20 }} locale={{ emptyText: 'Chưa có lịch hẹn phù hợp.' }} columns={[
        { title: 'Mã / giờ hẹn', render: (_, a) => <>{a.code}<br />{clinicTime(a.startAt)}</> },
        { title: 'Người khám dự kiến', render: (_, a) => <>{a.attendeeName}<br />{a.patientCode ?? 'Chưa gắn Patient'}</> },
        { title: 'Người đặt', dataIndex: 'bookerName' }, { title: 'Bác sĩ', dataIndex: 'dentistName' },
        { title: 'Trạng thái lịch', render: (_, a) => <>{statusLabels[a.status as AppointmentStatus] ?? a.status}{a.noShowEligible && <Tag color="orange">Cần kiểm tra đến muộn</Tag>}</> },
        { title: 'Thao tác', render: (_, a) => options?.canCheckIn && a.status === 'Confirmed' ? <Link to={`/reception/check-in/${a.appointmentId}`}>Đối chiếu và check-in</Link> : <Link to={`/appointments/${a.appointmentId}`}>Chi tiết lịch</Link> },
      ]} />
    </Card>
    <Card title="Hàng chờ và lượt khám">
      <Table<Visit> rowKey="visitId" loading={loading} dataSource={view?.queue ?? []} scroll={{ x: 1200 }} pagination={{ pageSize: 20 }} locale={{ emptyText: 'Chưa có lượt khám phù hợp.' }} columns={[
        { title: 'Thứ tự', render: (_, v) => v.queue.position || '—' },
        { title: 'Patient', render: (_, v) => <Link to={`/patients/${v.patientId}`}>{v.patientCode}<br />{v.patientName}</Link> },
        { title: 'Giờ hẹn', render: (_, v) => clinicTime(v.appointmentAt) },
        { title: 'Giờ đến / phút chờ', render: (_, v) => <>{clinicTime(v.actualArrivalAt)}<br />{v.queue.waitingMinutes} phút</> },
        { title: 'Bác sĩ', dataIndex: 'dentistName' },
        { title: 'Lượt khám', render: (_, v) => <>{visitLabels[v.visitStatus]}{v.clinicalPriority && <Tag color={v.clinicalPriority === 'Urgent' ? 'red' : 'default'}>{v.clinicalPriority === 'Urgent' ? 'Ưu tiên bởi bác sĩ' : 'Thường quy'}</Tag>}</> },
        { title: 'Mục đích / nguồn / blocker', render: (_, v) => <>{purposeLabels[v.purpose]}<br />{v.sourceLinks.map(s => <Tag key={s.id}>{s.label}</Tag>)}{v.blockers.map(b => <div key={b}><Tag color="orange">{b}</Tag></div>)}</> },
        { title: 'Thao tác', render: (_, v) => <Link to={`/reception/visits/${v.visitId}`}>Mở lượt khám</Link> },
      ]} />
    </Card>
    <BookingDrawer open={booking !== null} prefill={booking} options={options?.calendar ?? null} onClose={() => setBooking(null)}
      onBooked={result => { setBooking(null); navigate(`/reception/check-in/${result.appointmentId}?arrival=${encodeURIComponent(walkInArrival ?? new Date().toISOString())}`); }} />
  </Space>;
}
