import { Alert, Button, Card, Empty, Input, InputNumber, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import type { Affected, Availability, Options, Period, Schedule, ScheduleCommand } from './contracts';

const iso = (date: Date) => date.toISOString().slice(0, 10);
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const monday = (value: string) => { const date = new Date(value + 'T00:00:00Z'); date.setUTCDate(date.getUTCDate() - (date.getUTCDay() + 6) % 7); return iso(date); };
const dateAt = (start: string, day: number) => { const date = new Date(start + 'T00:00:00Z'); date.setUTCDate(date.getUTCDate() + day); return iso(date); };
const time = (minute: number) => `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
const minute = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3));
const clinicTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });

export default function WorkingSchedulesPage() {
  const [options, setOptions] = useState<Options>();
  const [branch, setBranch] = useState<number>();
  const [employee, setEmployee] = useState<number>();
  const [week, setWeek] = useState(() => monday(today()));
  const [saved, setSaved] = useState<Schedule>();
  const [periods, setPeriods] = useState<Period[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError>();
  const [success, setSuccess] = useState('');
  const [affected, setAffected] = useState<Affected[]>();
  const [slots, setSlots] = useState<Availability>();
  const [service, setService] = useState<number>();
  const [assistant, setAssistant] = useState<number>();
  const [duration, setDuration] = useState(30);
  const [previewDay, setPreviewDay] = useState(0);
  const [mode, setMode] = useState<'Staff' | 'Public'>('Staff');
  const commandRef = useRef<{ serialized: string; command: ScheduleCommand } | null>(null);
  const dirty = !!saved && JSON.stringify(periods) !== JSON.stringify(saved.intervals);
  const editable = options?.employees.some(e => e.branchId === branch && e.id === employee && e.canEdit) ?? false;
  const fail = (failure: unknown) => setError(failure instanceof ApiError ? failure : new ApiError(0, 'MSG144', 'Không thể kết nối. Vui lòng thử lại.'));

  useEffect(() => {
    const unload = (event: BeforeUnloadEvent) => { if (dirty) event.preventDefault(); };
    const navigation = (event: MouseEvent) => {
      const anchor = (event.target as HTMLElement).closest('a');
      if (dirty && anchor && anchor.getAttribute('href') !== '#' && !window.confirm('Hủy thay đổi chưa lưu để rời trang?')) { event.preventDefault(); event.stopPropagation(); }
    };
    window.addEventListener('beforeunload', unload); document.addEventListener('click', navigation, true);
    return () => { window.removeEventListener('beforeunload', unload); document.removeEventListener('click', navigation, true); };
  }, [dirty]);

  async function loadOptions() {
    setBusy(true); setError(undefined);
    try {
      const result = await api<Options>('/api/working-schedules/options'); setOptions(result);
      const own = result.employees.find(e => e.self && e.canEdit) ?? result.employees.find(e => e.canEdit);
      setBranch(own?.branchId); setEmployee(own?.id);
      setService(result.services.find(s => s.branchId === own?.branchId)?.id);
    } catch (e) { fail(e); } finally { setBusy(false); }
  }
  useEffect(() => { void loadOptions(); }, []);
  async function load() {
    if (!branch || !employee) return;
    setBusy(true); setError(undefined); setSaved(undefined); setAffected(undefined); setSlots(undefined); setSuccess('');
    try {
      const result = await api<Schedule>(`/api/working-schedules?branchId=${branch}&dentistId=${employee}&weekStart=${week}`);
      setSaved(result); setPeriods(result.intervals); commandRef.current = null;
    } catch (e) { fail(e); } finally { setBusy(false); }
  }
  useEffect(() => { void load(); }, [branch, employee, week]);
  const discardOkay = () => !dirty || window.confirm('Hủy các thay đổi chưa lưu?');
  const edit = (next: Period[]) => { setPeriods(next); setAffected(undefined); setSlots(undefined); setSuccess(''); };
  const command = (): ScheduleCommand => {
    const body = { branchId: branch!, dentistId: employee!, weekStart: week, expectedVersion: saved!.version, intervals: periods };
    const serialized = JSON.stringify(body);
    if (commandRef.current?.serialized !== serialized) commandRef.current = { serialized, command: { ...body, requestKey: crypto.randomUUID() } };
    return commandRef.current!.command;
  };
  async function preview(publish: boolean) {
    setBusy(true); setError(undefined); setSuccess(''); setSlots(undefined); setAffected(undefined);
    try {
      const body = command();
      if (publish) {
        const result = await api<{ schedule: Schedule; notification: { message: string } }>('/api/working-schedules/publish', { method: 'POST', body: JSON.stringify(body) });
        setSaved(result.schedule); setPeriods(result.schedule.intervals); commandRef.current = null; setSuccess(result.notification.message);
      } else {
        const result = await api<{ affectedAppointments: Affected[] }>('/api/working-schedules/preview', { method: 'POST', body: JSON.stringify(body) });
        setAffected(result.affectedAppointments);
      }
    } catch (e) {
      fail(e);
      if (e instanceof ApiError && e.code === 'MSG38') {
        try { const result = await api<{ affectedAppointments: Affected[] }>('/api/working-schedules/preview', { method: 'POST', body: JSON.stringify(command()) }); setAffected(result.affectedAppointments); }
        catch (impactError) { fail(impactError); }
      }
    } finally { setBusy(false); }
  }
  async function previewSlots() {
    setBusy(true); setError(undefined); setSlots(undefined);
    try {
      const query = { branchId: branch!, dentistId: employee!, serviceId: service!, date: dateAt(week, previewDay), durationMinutes: duration, assistantId: assistant };
      if (mode === 'Public') {
        const params = new URLSearchParams(Object.entries(query).filter(([,v]) => v !== undefined).map(([k,v]) => [k, String(v)]));
        // Public preview is always the published schedule and catalog 30/60-minute policy.
        setSlots(await api<Availability>(`/api/availability?${params}`, {}, false));
      } else setSlots(await api<Availability>('/api/working-schedules/preview-availability', { method: 'POST', body: JSON.stringify({ schedule: command(), query }) }));
    } catch (e) { fail(e); } finally { setBusy(false); }
  }
  const changePeriod = (index: number, patch: Partial<Period>) => edit(periods.map((p, i) => i === index ? { ...p, ...patch } : p));
  return <Space direction="vertical" size="middle" style={{ width: '100%' }}>
    <Typography.Title level={2}>S09 · Lịch làm việc tuần</Typography.Title>
    <Typography.Text>Múi giờ Asia/Ho_Chi_Minh · Đơn vị 15 phút · Giờ kết thúc không thuộc khoảng làm</Typography.Text>
    {error && <Alert showIcon type="error" message={`${error.status === 403 ? 'Không có quyền truy cập' : error.status === 409 ? 'Xung đột · Cần kiểm tra lại' : error.status === 400 ? 'Dữ liệu chưa hợp lệ' : 'Lỗi kết nối hoặc dịch vụ'} · ${error.code}`} description={error.message} action={<Button disabled={busy} onClick={() => { if (discardOkay()) void (options ? load() : loadOptions()); }}>Tải lại</Button>} />}
    {success && <Alert type="success" showIcon message={success} />}
    <Space wrap>
      <label>Chi nhánh <Select aria-label="Chi nhánh" style={{ width: 230 }} value={branch} disabled={busy} options={options?.branches.map(b => ({ value: b.id, label: b.name }))} onChange={value => {
        if (!discardOkay()) return; setBranch(value); setAssistant(undefined); setEmployee(options?.employees.find(e => e.branchId === value && e.self && e.canEdit)?.id ?? options?.employees.find(e => e.branchId === value && e.canEdit)?.id); setService(options?.services.find(s => s.branchId === value)?.id);
      }} /></label>
      <label>Nhân viên / bác sĩ <Select aria-label="Nhân viên / bác sĩ" style={{ width: 220 }} value={employee} disabled={busy} options={options?.employees.filter(e => e.branchId === branch && e.canEdit).map(e => ({ value: e.id, label: e.name }))} onChange={value => { if (discardOkay()) { setEmployee(value); setAssistant(undefined); } }} /></label>
      <label>Tuần bắt đầu <Input aria-label="Tuần bắt đầu" type="date" style={{ width: 170 }} value={week} disabled={busy} onChange={e => { if (e.target.value && discardOkay()) setWeek(monday(e.target.value)); }} /></label>
    </Space>
    {busy && <Spin tip="Đang tải / kiểm tra lịch"><div style={{ height: 35 }} /></Spin>}
    {!busy && options?.branches.length === 0 && <Empty description="Chưa có chi nhánh được phân quyền" />}
    {saved && <>
      <Space><Tag color={saved.status === 'Published' ? 'green' : 'default'}>{saved.status === 'Published' ? 'Đã công bố' : 'Chưa công bố'}</Tag><Typography.Text>Version {saved.version}{dirty ? ' · Có thay đổi chưa lưu' : ''}</Typography.Text>{saved.publishedAt && <Typography.Text>Công bố: {clinicTime(saved.publishedAt)}</Typography.Text>}</Space>
      <Typography.Text>Chú thích: Làm việc · Nghỉ · Block (nghỉ/block cần lý do và nằm trong một khoảng làm việc).</Typography.Text>
      <div style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))' }}>
        {Array.from({ length: 7 }, (_, day) => <Card key={day} title={`${day === 6 ? 'Chủ nhật' : `Thứ ${day + 2}`} · ${dateAt(week, day)}`}>
          <Space direction="vertical" style={{ width: '100%' }}>
            {!periods.some(p => p.day === day) && <Typography.Text type="secondary">Không có giờ làm</Typography.Text>}
            {periods.map((p, index) => p.day === day && <div key={index} style={{ borderBottom: '1px solid #eee', paddingBottom: 8 }}>
              <Space wrap>
                <Select aria-label={`Loại khoảng ${index + 1}`} value={p.kind} disabled={busy || !editable} style={{ width: 115 }} options={[{ value: 'Work', label: 'Làm việc' }, { value: 'Break', label: 'Nghỉ' }, { value: 'Block', label: 'Block' }]} onChange={kind => changePeriod(index, { kind })} />
                <Input aria-label={`Bắt đầu ${index + 1}`} type="time" step={900} value={time(p.startMinute)} style={{ width: 110 }} disabled={busy || !editable} onChange={e => { if (e.target.value) changePeriod(index, { startMinute: minute(e.target.value) }); }} />
                <Input aria-label={`Kết thúc ${index + 1}`} type="time" step={900} value={p.endMinute === 1440 ? '23:59' : time(p.endMinute)} style={{ width: 110 }} disabled={busy || !editable} onChange={e => { if (e.target.value) changePeriod(index, { endMinute: minute(e.target.value) }); }} />
                <Button disabled={busy || !editable} onClick={() => changePeriod(index, { endMinute: 1440 })}>Đến 24:00</Button>
                <Button danger disabled={busy || !editable} onClick={() => edit(periods.filter((_, i) => i !== index))}>Xóa khoảng {index + 1}</Button>
              </Space>
              {p.endMinute === 1440 && <Typography.Text> Kết thúc 24:00</Typography.Text>}
              <Input aria-label={`Lý do ${index + 1}`} placeholder="Lý do nghỉ / block" maxLength={500} value={p.reason} disabled={busy || !editable} onChange={e => changePeriod(index, { reason: e.target.value })} />
            </div>)}
            <Button disabled={busy || !editable} onClick={() => edit([...periods, { day, startMinute: 480, endMinute: 720, kind: 'Work', reason: '' }])}>Thêm khoảng</Button>
          </Space>
        </Card>)}
      </div>
      <Space wrap>
        <Button disabled={busy || !editable} onClick={() => void preview(false)}>Kiểm tra ảnh hưởng</Button>
        <Button type="primary" disabled={busy || !editable} onClick={() => void preview(true)}>Lưu và công bố</Button>
        <Button disabled={busy || !dirty} onClick={() => { if (discardOkay()) { edit(saved.intervals); commandRef.current = null; setError(undefined); } }}>Hủy thay đổi chưa lưu</Button>
      </Space>
      {affected && (affected.length > 0 ? <><Alert type="warning" showIcon message={`MSG38 · Có ${affected.length} lịch Confirmed bị ảnh hưởng. Giải quyết tại S12 trước khi công bố.`} /><Table rowKey="id" dataSource={affected} columns={[
        { title: 'Mã lịch', dataIndex: 'code', render: (code: string, row: Affected) => <Link to={`/appointments/${row.id}`}>{code} · S12</Link> },
        { title: 'Bắt đầu', dataIndex: 'start', render: clinicTime }, { title: 'Kết thúc', dataIndex: 'end', render: clinicTime }, { title: 'Người đến', dataIndex: 'attendee' }, { title: 'Trạng thái', dataIndex: 'status' },
      ]} /></> : <Alert type="info" message="Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra." />)}
      <Card title="Preview availability">
        <Space wrap>
          <Select aria-label="Chế độ preview" value={mode} options={[{ value: 'Staff', label: 'Nhân viên · bản đang sửa' }, { value: 'Public', label: 'Công khai · bản đã công bố' }]} onChange={value => { setMode(value); setSlots(undefined); if (value === 'Public') setDuration(options?.services.find(s => s.id === service)?.publicDurationMinutes ?? 30); }} style={{ width: 240 }} />
          <Select aria-label="Ngày preview" value={previewDay} options={Array.from({ length: 7 }, (_, value) => ({ value, label: dateAt(week, value) }))} onChange={value => { setPreviewDay(value); setSlots(undefined); }} />
          <Select aria-label="Dịch vụ" value={service} placeholder="Dịch vụ" style={{ width: 200 }} options={options?.services.filter(s => s.branchId === branch).map(s => ({ value: s.id, label: s.name }))} onChange={value => { setService(value); setSlots(undefined); if (mode === 'Public') setDuration(options?.services.find(s => s.id === value)?.publicDurationMinutes ?? 30); }} />
          <InputNumber aria-label="Thời lượng phút" min={15} max={480} step={15} value={duration} disabled={mode === 'Public'} onChange={value => { setDuration(value ?? 30); setSlots(undefined); }} />
          <Select aria-label="Trợ lý" allowClear placeholder="Trợ lý (nếu có)" style={{ width: 200 }} value={assistant} options={options?.employees.filter(e => e.branchId === branch && e.id !== employee).map(e => ({ value: e.id, label: e.name }))} onChange={value => { setAssistant(value); setSlots(undefined); }} />
          <Button disabled={busy || !service || !options?.employees.find(e => e.id === employee && e.branchId === branch)?.canDentist} onClick={() => void previewSlots()}>Preview slot</Button>
        </Space>
        <Typography.Paragraph>Preview không giữ chỗ. Mỗi slot dùng một ghế và cùng nhân sự trong toàn bộ khoảng.</Typography.Paragraph>
        {slots && (slots.slots.length === 0 ? <Empty description="Không có khung giờ đáp ứng tài nguyên" /> : <Table size="small" rowKey="start" dataSource={slots.slots} columns={[
          { title: 'Bắt đầu', dataIndex: 'start', render: clinicTime }, { title: 'Kết thúc', dataIndex: 'end', render: clinicTime }, { title: 'Ghế', dataIndex: 'chairId' }, { title: 'Version lịch', dataIndex: 'scheduleVersion' },
        ]} />)}
      </Card>
    </>}
  </Space>;
}
