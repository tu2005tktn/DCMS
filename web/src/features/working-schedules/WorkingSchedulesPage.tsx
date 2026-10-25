import { Alert, Button, Card, Empty, Input, InputNumber, Select, Space, Spin, Table, Tag, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import type { Affected, Availability, Options, Period } from './contracts';
import { validateAvailabilityDuration, validateSchedulePeriods } from './validation';

type BranchPeriod = Period & { branchId: number };
type Revision = { id: string; date: string; kind: string; version: number; intervals: BranchPeriod[]; reason: string; publishedBy: number; publishedAt: string };
type Saved = { dentistId: number; version: number; template: Revision | null; exception: Revision | null; futureChanges: Revision[]; legacyWeek: { weekStart: string; branchId: number }[] };
type Command = { dentistId: number; date: string; kind: string; expectedVersion: number; intervals: BranchPeriod[]; requestKey: string; reason: string };
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const addDays = (value: string, days: number) => { const date = new Date(value + 'T00:00:00Z'); date.setUTCDate(date.getUTCDate() + days); return date.toISOString().slice(0, 10); };
const weekday = (date: string) => (new Date(date + 'T00:00:00Z').getUTCDay() + 6) % 7;
const dayName = (day: number) => day === 6 ? 'Chủ nhật' : `Thứ ${day + 2}`;
const time = (minute: number) => `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
const clinicTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const timeOptions = Array.from({ length: 49 }, (_, i) => ({ value: i * 30, label: time(i * 30) }));

export default function WorkingSchedulesPage() {
  const [params] = useSearchParams();
  const [options, setOptions] = useState<Options>();
  const [employee, setEmployee] = useState<number>();
  const [date, setDate] = useState(today);
  const [kind, setKind] = useState('Template');
  const [saved, setSaved] = useState<Saved>();
  const [periods, setPeriods] = useState<BranchPeriod[]>([]);
  const [reason, setReason] = useState('');
  const [baseline, setBaseline] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError>();
  const [success, setSuccess] = useState('');
  const [affected, setAffected] = useState<Affected[]>();
  const [days, setDays] = useState<Availability[]>();
  const [branch, setBranch] = useState<number>();
  const [service, setService] = useState<number>();
  const [assistant, setAssistant] = useState<number>();
  const [duration, setDuration] = useState<number | null>(30);
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(() => addDays(today(), 6));
  const [mode, setMode] = useState('Staff');
  const request = useRef<{ text: string; body: Command } | undefined>(undefined);
  const reviewed = useRef<string | undefined>(undefined);
  const generation = useRef(0);
  const dirty = !!saved && baseline !== JSON.stringify({ periods, reason });
  const durationError = validateAvailabilityDuration(duration, mode === 'Public', options?.services.find(s => s.id === service)?.publicDurationMinutes);
  const eligible = options?.branches.filter(b => options.employees.some(e => e.id === employee && e.branchId === b.id && e.canEdit)) ?? [];
  const fail = (e: unknown) => setError(e instanceof ApiError ? e : new ApiError(0, 'MSG144', 'Không thể kết nối. Vui lòng thử lại.'));
  const discardOkay = () => !dirty || window.confirm('Hủy các thay đổi chưa lưu?');
  const clearResults = () => { setAffected(undefined); setDays(undefined); setSuccess(''); };
  const edit = (next: BranchPeriod[]) => { setPeriods(next); clearResults(); };
  const useSaved = (result: Saved) => {
    const revision = kind === 'Template' ? result.template : result.exception;
    const next = revision?.intervals ?? [];
    const note = revision?.reason ?? '';
    setSaved(result); setPeriods(next); setReason(note); setBaseline(JSON.stringify({ periods: next, reason: note })); request.current = undefined;
  };
  async function loadOptions() {
    setBusy(true); setError(undefined);
    try {
      const result = await api<Options>('/api/working-schedules/options'); setOptions(result);
      const requested = Number(params.get('dentistId'));
      const own = result.employees.find(e => e.id === requested && e.canEdit) ?? result.employees.find(e => e.self && e.canEdit) ?? result.employees.find(e => e.canEdit);
      setEmployee(own?.id); setBranch(own?.branchId); setService(result.services.find(s => s.branchId === own?.branchId)?.id);
    } catch (e) { fail(e); } finally { setBusy(false); }
  }
  useEffect(() => { void loadOptions(); }, []);
  async function load() {
    if (!employee) return;
    const token = ++generation.current;
    setBusy(true); setError(undefined); setSaved(undefined); clearResults();
    try {
      const result = await api<Saved>(`/api/working-schedules/recurring?dentistId=${employee}&date=${date}`);
      if (generation.current === token) useSaved(result);
    } catch (e) { if (generation.current === token) fail(e); }
    finally { if (generation.current === token) setBusy(false); }
  }
  useEffect(() => { void load(); return () => { generation.current++; }; }, [employee, date, kind]);
  useEffect(() => {
    const unload = (event: BeforeUnloadEvent) => { if (dirty) event.preventDefault(); };
    const navigation = (event: MouseEvent) => {
      if (dirty && (event.target as HTMLElement).closest('a') && !window.confirm('Hủy thay đổi chưa lưu để rời trang?')) { event.preventDefault(); event.stopPropagation(); }
    };
    window.addEventListener('beforeunload', unload); document.addEventListener('click', navigation, true);
    return () => { window.removeEventListener('beforeunload', unload); document.removeEventListener('click', navigation, true); };
  }, [dirty]);
  const command = (): Command => {
    const issues = validateSchedulePeriods(periods);
    if (periods.some(p => p.startMinute % 30 || p.endMinute % 30)) issues.push('Giờ làm và nghỉ phải theo bước 30 phút.');
    if (kind === 'Exception' && !reason.trim()) issues.push('Nhập lý do ngoại lệ, kể cả ngày không làm việc.');
    if (!date || date < today()) issues.push('Ngày áp dụng phải từ hôm nay theo giờ phòng khám.');
    if (issues.length) throw new ApiError(400, 'MSG110', issues.join('\n'));
    const body = { dentistId: employee!, date, kind, expectedVersion: saved!.version, intervals: periods, reason };
    const text = JSON.stringify(body);
    if (request.current?.text !== text) request.current = { text, body: { ...body, requestKey: crypto.randomUUID() } };
    return request.current!.body;
  };
  async function check(publish: boolean) {
    setBusy(true); setError(undefined); clearResults();
    try {
      const body = command();
      if (!publish || reviewed.current !== body.requestKey) {
        const preview = await api<{ affectedAppointments: Affected[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(body) });
        setAffected(preview.affectedAppointments);
        if (preview.affectedAppointments.length) return;
        reviewed.current = body.requestKey;
        if (!publish) return;
      }
      const result = await api<{ schedule: Saved; notification: { message: string } }>('/api/working-schedules/recurring/publish', { method: 'POST', body: JSON.stringify(body) });
      useSaved(result.schedule); setSuccess(result.notification.message);
    } catch (e) {
      fail(e);
      if (e instanceof ApiError && e.code === 'MSG38') {
        try { setAffected((await api<{ affectedAppointments: Affected[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(command()) })).affectedAppointments); }
        catch (impactError) { fail(impactError); }
      }
    } finally { setBusy(false); }
  }
  async function previewSlots() {
    setBusy(true); setError(undefined); setDays(undefined);
    try {
      if (durationError) throw new ApiError(400, 'MSG110', durationError);
      if (!from || !to || to < from || to > addDays(from, 30)) throw new ApiError(400, 'MSG110', 'Chọn khoảng preview từ 1 đến 31 ngày.');
      if (mode === 'Staff') {
        const result = await api<{ days: Availability[] }>('/api/working-schedules/recurring/preview-availability', { method: 'POST', body: JSON.stringify({ schedule: command(), branchId: branch, serviceId: service, from, to, durationMinutes: duration, assistantId: assistant }) });
        setDays(result.days);
      } else {
        const result: Availability[] = [];
        for (let day = from; day <= to; day = addDays(day, 1)) {
          const query = new URLSearchParams({ branchId: String(branch), dentistId: String(employee), serviceId: String(service), date: day, durationMinutes: String(duration) });
          if (assistant) query.set('assistantId', String(assistant));
          result.push(await api<Availability>(`/api/availability?${query}`, {}, false));
        }
        setDays(result);
      }
    } catch (e) { fail(e); } finally { setBusy(false); }
  }
  const changePeriod = (index: number, patch: Partial<BranchPeriod>) => edit(periods.map((p, i) => i === index ? { ...p, ...patch } : p));
  const revision = kind === 'Template' ? saved?.template : saved?.exception;
  return <Space direction="vertical" size="middle" style={{ width: '100%' }}>
    <Typography.Title level={2}>S09 · Lịch làm việc tuần</Typography.Title>
    <Typography.Paragraph>Mẫu lặp từ Thứ 2 đến Chủ nhật · Mỗi ngày một chi nhánh hoặc không làm việc · Ngoại lệ chỉ thay thế ngày được chọn · Giờ phòng khám (UTC+7).</Typography.Paragraph>
    {error && <Alert type="error" showIcon message={`${error.status === 403 ? 'Không có quyền truy cập' : error.status === 409 ? 'Xung đột · Cần kiểm tra lại' : error.status === 400 ? 'Dữ liệu chưa hợp lệ' : 'Lỗi kết nối hoặc dịch vụ'} · ${error.code}`} description={<div style={{ whiteSpace: 'pre-line' }}>{error.message}</div>} action={<Button disabled={busy} onClick={() => { if (discardOkay()) void (options ? load() : loadOptions()); }}>Tải lại</Button>} />}
    {success && <Alert type="success" message={success} showIcon />}
    <Space wrap>
      <label>Nhân viên / bác sĩ <Select aria-label="Nhân viên / bác sĩ" value={employee} disabled={busy} style={{ width: 230 }} options={options?.employees.filter((e, i, all) => e.canEdit && all.findIndex(x => x.id === e.id && x.canEdit) === i).map(e => ({ value: e.id, label: e.name }))} onChange={value => { if (discardOkay()) { setEmployee(value); const b = options?.employees.find(e => e.id === value && e.canEdit)?.branchId; setBranch(b); setService(options?.services.find(s => s.branchId === b)?.id); setAssistant(undefined); } }} /></label>
      <Select aria-label="Loại lịch" value={kind} disabled={busy} style={{ width: 230 }} options={[{ value: 'Template', label: 'Mẫu lặp hằng tuần' }, { value: 'Exception', label: 'Ngoại lệ ngày cụ thể' }]} onChange={value => { if (discardOkay()) setKind(value); }} />
      <label>{kind === 'Template' ? 'Áp dụng từ ngày' : 'Ngày ngoại lệ'} <Input aria-label="Ngày áp dụng" type="date" value={date} disabled={busy} onChange={e => { if (e.target.value && discardOkay()) setDate(e.target.value); }} /></label>
    </Space>
    {busy && <Spin tip="Đang tải / kiểm tra lịch"><div style={{ height: 30 }} /></Spin>}
    {options && !options.employees.some(e => e.canEdit) && <Empty description="Chưa có nhân viên được phân quyền" />}
    {saved && <>
      <Space wrap><Tag color={revision ? 'green' : 'default'}>{revision ? 'Đã công bố' : 'Chưa công bố'}</Tag><Typography.Text>Version {saved.version}{dirty ? ' · Có thay đổi chưa lưu' : ''}</Typography.Text>{revision && <Typography.Text>Hiệu lực: {revision.date} · Người công bố: {revision.publishedBy} · {clinicTime(revision.publishedAt)}</Typography.Text>}</Space>
      {saved.legacyWeek.length > 0 && <Alert type="info" message="Có lịch tuần cũ được giữ lại" description="Lịch cũ chỉ áp dụng đúng tuần đã lưu khi chưa có mẫu hoặc ngoại lệ có hiệu lực. Hãy nhập mẫu lặp mới để dùng cho các tuần sau." />}
      {!!saved.futureChanges.length && <Card size="small" title="Thay đổi đã công bố trong tương lai">{saved.futureChanges.map(r => <div key={r.id}>{r.date} · {r.kind === 'Template' ? 'Mẫu lặp' : 'Ngoại lệ'} · Version {r.version}{r.reason ? ` · ${r.reason}` : ''}</div>)}</Card>}
      {kind === 'Exception' && <label>Lý do ngoại lệ <Input aria-label="Lý do ngoại lệ" value={reason} maxLength={500} disabled={busy} onChange={e => { setReason(e.target.value); clearResults(); }} /></label>}
      <Typography.Text>Chọn chi nhánh để thêm giờ 08:00–17:00. Giờ làm/nghỉ theo bước 30 phút; slot đặt lịch vẫn theo đơn vị 15 phút. Nghỉ và Block cần lý do, nằm trọn trong giờ làm.</Typography.Text>
      <div style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(330px, 1fr))' }}>
        {Array.from({ length: 7 }, (_, day) => day).filter(day => kind === 'Template' || day === weekday(date)).map(day => {
          const dayBranch = periods.find(p => p.day === day)?.branchId;
          return <Card key={day} title={dayName(day)}>
            <Space direction="vertical" style={{ width: '100%' }}>
              <Select aria-label={`Chi nhánh ${dayName(day)}`} style={{ width: '100%' }} value={dayBranch ?? 0} disabled={busy} options={[{ value: 0, label: 'Không làm việc' }, ...eligible.map(b => ({ value: b.id, label: b.name }))]} onChange={value => {
                const rest = periods.filter(p => p.day !== day);
                edit(value ? [...rest, ...(dayBranch ? periods.filter(p => p.day === day).map(p => ({ ...p, branchId: value })) : [{ branchId: value, day, startMinute: 480, endMinute: 1020, kind: 'Work' as const, reason: '' }])] : rest);
              }} />
              {!dayBranch && <Typography.Text type="secondary">Không làm việc</Typography.Text>}
              {periods.map((p, i) => p.day === day && <div key={i} style={{ borderBottom: '1px solid #eee', paddingBottom: 8 }}>
                <Typography.Text>Khoảng {i + 1} · {time(p.startMinute)} → {time(p.endMinute)}</Typography.Text>
                <Space wrap>
                  <Select aria-label={`Loại khoảng ${i + 1}`} value={p.kind} disabled={busy} style={{ width: 115 }} options={[{ value: 'Work', label: 'Làm việc' }, { value: 'Break', label: 'Nghỉ' }, { value: 'Block', label: 'Block' }]} onChange={value => changePeriod(i, { kind: value })} />
                  <Select aria-label={`Bắt đầu ${i + 1}`} showSearch optionFilterProp="label" value={p.startMinute} disabled={busy} style={{ width: 100 }} options={timeOptions.slice(0, -1)} onChange={value => changePeriod(i, { startMinute: value })} />
                  <Select aria-label={`Kết thúc ${i + 1}`} showSearch optionFilterProp="label" value={p.endMinute} disabled={busy} style={{ width: 100 }} options={timeOptions.slice(1)} onChange={value => changePeriod(i, { endMinute: value })} />
                  <Button danger disabled={busy} onClick={() => edit(periods.filter((_, index) => index !== i))}>Xóa khoảng {i + 1}</Button>
                </Space>
                <Input aria-label={`Lý do ${i + 1}`} placeholder="Lý do nghỉ / block" value={p.reason} maxLength={500} disabled={busy} onChange={e => changePeriod(i, { reason: e.target.value })} />
              </div>)}
              <Button disabled={busy || !dayBranch} onClick={() => edit([...periods, { branchId: dayBranch!, day, startMinute: 480, endMinute: 720, kind: 'Work', reason: '' }])}>Thêm khoảng</Button>
            </Space>
          </Card>;
        })}
      </div>
      <Space wrap><Button disabled={busy} onClick={() => void check(false)}>Kiểm tra ảnh hưởng</Button><Button type="primary" disabled={busy} onClick={() => void check(true)}>Lưu và công bố</Button><Button disabled={busy || !dirty} onClick={() => { if (discardOkay()) { useSaved(saved); setError(undefined); clearResults(); } }}>Hủy thay đổi chưa lưu</Button></Space>
      {affected && (affected.length ? <><Alert type="warning" message={`MSG38 · Có ${affected.length} lịch Confirmed bị ảnh hưởng. Giải quyết tại S12 trước khi công bố.`} /><Table rowKey="id" dataSource={affected} columns={[{ title: 'Mã lịch', dataIndex: 'code', render: (value: string, row: Affected) => <Link to={`/appointments/${row.id}`}>{value} · S12</Link> }, { title: 'Bắt đầu', dataIndex: 'start', render: clinicTime }, { title: 'Kết thúc', dataIndex: 'end', render: clinicTime }, { title: 'Người đến', dataIndex: 'attendee' }, { title: 'Trạng thái', dataIndex: 'status' }]} /></> : <Alert type="info" message="Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra." />)}
      <Card title="Preview availability">
        <Space wrap>
          <Select aria-label="Chế độ preview" value={mode} disabled={busy} style={{ width: 240 }} options={[{ value: 'Staff', label: 'Nhân viên · bản đang sửa' }, { value: 'Public', label: 'Công khai · bản đã công bố' }]} onChange={value => { setMode(value); setDays(undefined); if (value === 'Public') setDuration(options?.services.find(s => s.id === service)?.publicDurationMinutes ?? 30); }} />
          <label>Từ ngày <Input aria-label="Preview từ ngày" type="date" value={from} disabled={busy} onChange={e => { setFrom(e.target.value); setDays(undefined); }} /></label>
          <label>Đến ngày <Input aria-label="Preview đến ngày" type="date" value={to} disabled={busy} onChange={e => { setTo(e.target.value); setDays(undefined); }} /></label>
          <Select aria-label="Chi nhánh preview" value={branch} style={{ width: 200 }} disabled={busy} options={eligible.map(b => ({ value: b.id, label: b.name }))} onChange={value => { const next = options?.services.find(s => s.branchId === value); setBranch(value); setService(next?.id); if (mode === 'Public') setDuration(next?.publicDurationMinutes ?? 30); setAssistant(undefined); setDays(undefined); }} />
          <Select aria-label="Dịch vụ" value={service} style={{ width: 200 }} disabled={busy} options={options?.services.filter(s => s.branchId === branch).map(s => ({ value: s.id, label: s.name }))} onChange={value => { setService(value); setDays(undefined); if (mode === 'Public') setDuration(options?.services.find(s => s.id === value)?.publicDurationMinutes ?? 30); }} />
          <InputNumber aria-label="Thời lượng phút" aria-invalid={!!durationError} status={durationError ? 'error' : undefined} min={1} max={480} step={1} changeOnBlur={false} value={duration} disabled={busy || mode === 'Public'}
            onInput={text => { setDuration(text.trim() === '' ? null : Number(text)); setDays(undefined); }}
            onChange={value => { setDuration(value); setDays(undefined); }} />
          <Select aria-label="Trợ lý" allowClear placeholder="Trợ lý (nếu có)" style={{ width: 200 }} value={assistant} disabled={busy} options={options?.employees.filter(e => e.branchId === branch && e.id !== employee).map(e => ({ value: e.id, label: e.name }))} onChange={value => { setAssistant(value); setDays(undefined); }} />
          <Button disabled={busy || !service || !options?.employees.find(e => e.id === employee && e.branchId === branch)?.canDentist} onClick={() => void previewSlots()}>Preview slot</Button>
        </Space>
        <Typography.Paragraph type={durationError ? 'danger' : 'secondary'} role={durationError ? 'alert' : undefined}>{durationError ?? (mode === 'Public' ? 'Thời lượng theo dịch vụ đã chọn.' : 'Thời lượng: số nguyên từ 1 đến 480 phút.')}</Typography.Paragraph>
        <Typography.Paragraph>Preview tối đa 31 ngày, không giữ chỗ. Mỗi slot dùng một ghế và cùng nhân sự trong toàn bộ khoảng.</Typography.Paragraph>
        {days && (days.every(d => !d.slots.length) ? <Empty description="Không có khung giờ đáp ứng tài nguyên" /> : <Table size="small" rowKey="start" dataSource={days.flatMap(d => d.slots)} columns={[{ title: 'Bắt đầu', dataIndex: 'start', render: clinicTime }, { title: 'Kết thúc', dataIndex: 'end', render: clinicTime }, { title: 'Ghế', dataIndex: 'chairId' }, { title: 'Version lịch', dataIndex: 'scheduleVersion' }]} />)}
      </Card>
    </>}
  </Space>;
}
