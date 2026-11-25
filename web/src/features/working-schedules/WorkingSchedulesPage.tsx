import { usePermissions } from '../authentication/usePermissions';
import { useCanPickBranch } from '../authentication/useCanPickBranch';
import { Alert, Button, Card, Empty, Input, Modal, Select, Space, Spin, Table, Tag, Tooltip, Typography, message } from 'antd';
import { CalendarOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import type { Affected, CapacityConflict, Options, Period } from './contracts';
import './workingSchedules.css';
import { validateSchedulePeriods } from './validation';

type BranchPeriod = Period & { branchId: number };
type Revision = { id: string; date: string; kind: string; version: number; intervals: BranchPeriod[]; reason: string; publishedBy: number; publishedAt: string };
type Saved = { dentistId: number; version: number; template: Revision | null; exception: Revision | null; futureChanges: Revision[]; legacyWeek: { weekStart: string; branchId: number }[] };
type Command = { dentistId: number; date: string; kind: string; expectedVersion: number; intervals: BranchPeriod[]; requestKey: string; reason: string };
const today = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const clock = (minute: number) => `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
const addDays = (value: string, days: number) => { const d = new Date(value + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const weekday = (date: string) => (new Date(date + 'T00:00:00Z').getUTCDay() + 6) % 7;
const dayName = (day: number) => day === 6 ? 'Chủ nhật' : `Thứ ${day + 2}`;
const time = (minute: number) => `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
const clinicTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const timeOptions = Array.from({ length: 49 }, (_, i) => ({ value: i * 30, label: time(i * 30) }));

export default function WorkingSchedulesPage() {
  const has = usePermissions();
  const [params] = useSearchParams();
  const [options, setOptions] = useState<Options>();
  const [employee, setEmployee] = useState<number>();
  const [date, setDate] = useState(today);
  const [endDate, setEndDate] = useState('');
  const [kind, setKind] = useState('Template');
  const [saved, setSaved] = useState<Saved>();
  const [periods, setPeriods] = useState<BranchPeriod[]>([]);
  const [reason, setReason] = useState('');
  const [baseline, setBaseline] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError>();
  const [success, setSuccess] = useState('');
  const [affected, setAffected] = useState<Affected[]>();
  const [capacity, setCapacity] = useState<CapacityConflict[]>();
  const [toast, toastContext] = message.useMessage();
  // Results of "Kiểm tra ảnh hưởng" / "Lưu và công bố" appear as pop-ups, not as banners at the top of the page.
  useEffect(() => { if (success) { void toast.success(success); setSuccess(''); } }, [success, toast]);
  useEffect(() => {
    if (affected && affected.length === 0) { void toast.info('Không có lịch Confirmed bị ảnh hưởng tại thời điểm kiểm tra.'); setAffected(undefined); }
  }, [affected, toast]);
  const canPickBranch = useCanPickBranch();
  const [viewBranch, setViewBranch] = useState<number>();
  const request = useRef<{ text: string; body: Command } | undefined>(undefined);
  const reviewed = useRef<string | undefined>(undefined);
  const generation = useRef(0);
  const rangeKeys = useRef(new Map<string, string>());
  const dirty = !!saved && baseline !== JSON.stringify({ periods, reason });
  const eligible = options?.branches.filter(b => options.employees.some(e => e.id === employee && e.branchId === b.id && (e.canView ?? e.canEdit))) ?? [];
  const showBranchPicker = canPickBranch && (options?.branches.length ?? 0) > 1;
  const fail = (e: unknown) => setError(e instanceof ApiError ? e : new ApiError(0, 'MSG49', 'Không thể kết nối. Vui lòng thử lại.'));
  const discardOkay = () => !dirty || window.confirm('Hủy các thay đổi chưa lưu?');
  const clearResults = () => { setAffected(undefined); setCapacity(undefined); setSuccess(''); };
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
      const own = result.employees.find(e => e.id === requested && (e.canView ?? e.canEdit)) ?? result.employees.find(e => e.self && (e.canView ?? e.canEdit)) ?? result.employees.find(e => (e.canView ?? e.canEdit));
      setEmployee(own?.id); setViewBranch(own?.branchId);
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
    if (issues.length) throw new ApiError(400, 'MSG40', issues.join('\n'));
    const body = { dentistId: employee!, date, kind, expectedVersion: saved!.version, intervals: periods, reason };
    const text = JSON.stringify(body);
    if (request.current?.text !== text) request.current = { text, body: { ...body, requestKey: crypto.randomUUID() } };
    return request.current!.body;
  };
  // Leave over several days: one single-date exception per day, checked first, then published from the last day back to the first.
  async function checkRange(publish: boolean) {
    setBusy(true); setError(undefined); clearResults();
    let version = saved!.version;
    try {
      command();
      if (endDate > addDays(date, 30)) throw new ApiError(400, 'MSG40', 'Chọn tối đa 31 ngày liên tiếp.');
      const dates: string[] = [];
      for (let d = date; d <= endDate; d = addDays(d, 1)) dates.push(d);
      const body = (d: string, expectedVersion: number) => {
        const content = { dentistId: employee!, date: d, kind: 'Exception', expectedVersion, intervals: periods.map(p => ({ ...p, day: weekday(d) })), reason };
        const text = JSON.stringify(content);
        if (!rangeKeys.current.has(text)) rangeKeys.current.set(text, crypto.randomUUID());
        return { ...content, requestKey: rangeKeys.current.get(text)! };
      };
      const impact: Affected[] = []; const full: CapacityConflict[] = [];
      for (const d of dates) {
        const preview = await api<{ affectedAppointments: Affected[]; capacityConflicts?: CapacityConflict[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(body(d, version)) });
        impact.push(...preview.affectedAppointments); full.push(...(preview.capacityConflicts ?? []));
      }
      if (impact.length) { setAffected(impact); return; }
      if (full.length) { setCapacity(full); return; }
      if (!publish) { setAffected([]); return; }
      for (const d of [...dates].reverse()) {
        const result = await api<{ schedule: Saved }>('/api/working-schedules/recurring/publish', { method: 'POST', body: JSON.stringify(body(d, version)) });
        version = result.schedule.version;
      }
      setEndDate(''); await load();
      setSuccess(`Đã công bố lịch nghỉ/ngoại lệ cho ${dates.length} ngày.`);
    } catch (e) {
      setSaved(current => current ? { ...current, version } : current);
      fail(e);
    } finally { setBusy(false); }
  }
  async function check(publish: boolean) {
    if (kind === 'Exception' && endDate > date) return checkRange(publish);
    setBusy(true); setError(undefined); clearResults();
    try {
      const body = command();
      if (!publish || reviewed.current !== body.requestKey) {
        const preview = await api<{ affectedAppointments: Affected[]; capacityConflicts?: CapacityConflict[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(body) });
        setAffected(preview.affectedAppointments);
        if (preview.affectedAppointments.length) return;
        if (preview.capacityConflicts?.length) { setCapacity(preview.capacityConflicts); return; }
        reviewed.current = body.requestKey;
        if (!publish) return;
      }
      const result = await api<{ schedule: Saved; notification: { message: string } }>('/api/working-schedules/recurring/publish', { method: 'POST', body: JSON.stringify(body) });
      useSaved(result.schedule); setSuccess(result.notification.message);
    } catch (e) {
      if (e instanceof ApiError && e.code === 'MSG117') {
        try { const again = await api<{ capacityConflicts?: CapacityConflict[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(command()) }); setCapacity(again.capacityConflicts); }
        catch (capacityError) { fail(capacityError); }
      } else fail(e);
      if (e instanceof ApiError && e.code === 'MSG32') {
        try { setAffected((await api<{ affectedAppointments: Affected[] }>('/api/working-schedules/recurring/preview', { method: 'POST', body: JSON.stringify(command()) })).affectedAppointments); }
        catch (impactError) { fail(impactError); }
      }
    } finally { setBusy(false); }
  }
  const changePeriod = (index: number, patch: Partial<BranchPeriod>) => edit(periods.map((p, i) => i === index ? { ...p, ...patch } : p));
  const revision = kind === 'Template' ? saved?.template : saved?.exception;
  return <Space direction="vertical" size="middle" style={{ width: '100%' }}>
    <Typography.Title level={2}>Lịch làm việc</Typography.Title>
    <Modal open={!!error} title={`${error?.status === 403 ? 'Không có quyền truy cập' : error?.status === 409 ? 'Xung đột · Cần kiểm tra lại' : error?.status === 400 ? 'Dữ liệu chưa hợp lệ' : 'Lỗi kết nối hoặc dịch vụ'} · ${error?.code}`} onCancel={() => setError(undefined)}
      footer={<Space><Button disabled={busy} onClick={() => { if (discardOkay()) void (options ? load() : loadOptions()); }}>Tải lại</Button><Button type="primary" onClick={() => setError(undefined)}>Đóng</Button></Space>}>{<div style={{ whiteSpace: 'pre-line' }}>{error?.message}</div>}</Modal>
    {toastContext}
    <Space wrap align="end" className="ws-filters">
      {showBranchPicker && <label className="ws-field">Chi nhánh <Select aria-label="Chi nhánh" value={viewBranch} disabled={busy} style={{ width: 230 }} options={options?.branches.map(b => ({ value: b.id, label: b.name }))}
        onChange={value => { if (!discardOkay()) return; setViewBranch(value); const first = options?.employees.find(e => e.branchId === value && (e.canView ?? e.canEdit)); setEmployee(first?.id); }} /></label>}
      <label className="ws-field">Nhân viên / bác sĩ <Select aria-label="Nhân viên / bác sĩ" value={employee} disabled={busy} style={{ width: 230 }} options={options?.employees.filter((e, i, all) => (e.canView ?? e.canEdit) && (!showBranchPicker || e.branchId === viewBranch) && all.findIndex(x => x.id === e.id && (x.canView ?? x.canEdit) && (!showBranchPicker || x.branchId === viewBranch)) === i).map(e => ({ value: e.id, label: e.name }))} onChange={value => { if (discardOkay()) { setEmployee(value); } }} /></label>
      <Select aria-label="Loại lịch" value={kind} disabled={busy} style={{ width: 230 }} options={[{ value: 'Template', label: 'Mẫu lặp hằng tuần' }, { value: 'Exception', label: 'Ngoại lệ ngày cụ thể' }]} onChange={value => { if (discardOkay()) setKind(value); }} />
      <label className="ws-field">{kind === 'Template' ? 'Áp dụng từ ngày' : 'Ngày ngoại lệ'} <Input aria-label="Ngày áp dụng" type="date" value={date} disabled={busy} onChange={e => { if (e.target.value && discardOkay()) { setDate(e.target.value); if (endDate && endDate <= e.target.value) setEndDate(''); } }} /></label>
      {kind === 'Exception' && <label className="ws-field">Đến ngày (nghỉ nhiều ngày) <Input aria-label="Đến ngày" type="date" value={endDate} min={addDays(date, 1)} disabled={busy} onChange={e => { setEndDate(e.target.value); clearResults(); }} /></label>}
    </Space>
    {busy && <Spin tip="Đang tải / kiểm tra lịch"><div style={{ height: 30 }} /></Spin>}
    {options && !options.employees.some(e => (e.canView ?? e.canEdit)) && <Empty description="Chưa có nhân viên được phân quyền" />}
    {saved && <>
      <Space wrap><Tag color={revision ? 'green' : 'default'}>{revision ? 'Đã công bố' : 'Chưa công bố'}</Tag>{dirty && <Typography.Text>Có thay đổi chưa lưu</Typography.Text>}</Space>
      {!!saved.futureChanges.length && <Card size="small" title="Thay đổi đã công bố trong tương lai">{saved.futureChanges.map(r => <div key={r.id}>{r.date} · {r.kind === 'Template' ? 'Mẫu lặp' : 'Ngoại lệ'} · Version {r.version}{r.reason ? ` · ${r.reason}` : ''}</div>)}</Card>}
      {kind === 'Exception' && <label>Lý do ngoại lệ <Input aria-label="Lý do ngoại lệ" value={reason} maxLength={500} disabled={!has('working-schedules.manage') || busy} onChange={e => { setReason(e.target.value); clearResults(); }} /></label>}
      <section className="working-week-editor" aria-label="Chỉnh lịch làm việc">
        <div className="working-week-heading">
          <div className="working-week-title"><CalendarOutlined /><h3>{kind === 'Template' ? 'Lịch làm việc hằng tuần' : 'Lịch làm việc trong ngày'}</h3></div>
          <span className="working-week-count">{new Set(periods.filter(p => p.kind === 'Work').map(p => p.day)).size} ngày làm việc</span>
        </div>
        <div className="working-week-columns" aria-hidden="true"><span>Ngày</span><span>Chi nhánh</span><span>Khung giờ làm việc</span></div>
        <div className="working-day-list">
        {Array.from({ length: 7 }, (_, day) => day).filter(day => kind === 'Template' || day === weekday(date)).map(day => {
          const dayBranch = periods.find(p => p.day === day)?.branchId;
          return <div key={day} className={`working-day-row${dayBranch ? '' : ' working-day-off'}`}>
            <div className="working-day-name"><strong>{dayName(day)}</strong><span className={`working-day-status${dayBranch ? ' is-working' : ''}`}>{dayBranch ? 'Có lịch' : 'Ngày nghỉ'}</span></div>
            <div className="working-day-branch">
              <Select aria-label={`Chi nhánh ${dayName(day)}`} value={dayBranch ?? 0} disabled={!has('working-schedules.manage') || busy} options={[{ value: 0, label: 'Không làm việc' }, ...eligible.map(b => ({ value: b.id, label: b.name }))]} onChange={value => {
                const rest = periods.filter(p => p.day !== day);
                edit(value ? [...rest, ...(dayBranch ? periods.filter(p => p.day === day).map(p => ({ ...p, branchId: value })) : [{ branchId: value, day, startMinute: 480, endMinute: 1020, kind: 'Work' as const, reason: '' }])] : rest);
              }} />
            </div>
            <div className="working-day-periods">
              {!dayBranch && <span className="working-day-empty">Không có ca làm việc</span>}
              {periods.map((p, i) => p.day === day && <div key={i} role="group" aria-label={`Khoảng ${i + 1} · ${time(p.startMinute)} → ${time(p.endMinute)}`} className={`working-period working-period-${p.kind.toLowerCase()}`}>
                <span className="working-period-number" title={`Khoảng ${i + 1}`}>{String(i + 1).padStart(2, '0')}</span>
                <Select className="working-period-kind" aria-label={`Loại khoảng ${i + 1}`} value={p.kind} disabled={!has('working-schedules.manage') || busy} options={[{ value: 'Work', label: 'Làm việc' }, { value: 'Break', label: 'Nghỉ' }]} onChange={value => changePeriod(i, { kind: value })} />
                <div className="working-period-times">
                  <Select aria-label={`Bắt đầu ${i + 1}`} showSearch optionFilterProp="label" value={p.startMinute} disabled={!has('working-schedules.manage') || busy} options={timeOptions.slice(0, -1)} onChange={value => changePeriod(i, { startMinute: value })} />
                  <span className="working-time-separator" aria-hidden="true">–</span>
                  <Select aria-label={`Kết thúc ${i + 1}`} showSearch optionFilterProp="label" value={p.endMinute} disabled={!has('working-schedules.manage') || busy} options={timeOptions.slice(1)} onChange={value => changePeriod(i, { endMinute: value })} />
                </div>
                <Input className="working-period-reason" aria-label={`Lý do ${i + 1}`} placeholder={p.kind === 'Work' ? 'Ghi chú (không bắt buộc)' : 'Lý do nghỉ'} value={p.reason} maxLength={500} disabled={!has('working-schedules.manage') || busy} onChange={e => changePeriod(i, { reason: e.target.value })} />
                <Tooltip title={`Xóa khoảng ${i + 1}`}><Button className="working-period-remove" type="text" danger icon={<DeleteOutlined />} aria-label={`Xóa khoảng ${i + 1}`} disabled={!has('working-schedules.manage') || busy} onClick={() => edit(periods.filter((_, index) => index !== i))} /></Tooltip>
              </div>)}
              {dayBranch && <Button className="working-period-add" type="text" aria-label="Thêm khoảng" icon={<PlusOutlined aria-hidden="true" />} disabled={!has('working-schedules.manage') || busy} onClick={() => edit([...periods, { branchId: dayBranch, day, startMinute: 480, endMinute: 720, kind: 'Work', reason: '' }])}>Thêm khoảng</Button>}
            </div>
          </div>;
        })}
        </div>
      </section>
      <Space wrap><Button disabled={!has('working-schedules.manage') || busy} onClick={() => void check(false)}>Kiểm tra ảnh hưởng</Button><Button type="primary" disabled={!has('working-schedules.manage') || busy} onClick={() => void check(true)}>Lưu và công bố</Button><Button disabled={!has('working-schedules.manage') || busy || !dirty} onClick={() => { if (discardOkay()) { useSaved(saved); setError(undefined); clearResults(); } }}>Hủy thay đổi chưa lưu</Button></Space>
      <Modal open={!!capacity?.length} title="Không đủ ghế để đăng ký" onCancel={() => setCapacity(undefined)}
        footer={<Button type="primary" onClick={() => setCapacity(undefined)}>Đóng</Button>}>
        <Alert type="warning" showIcon message="Khung giờ này đã có nhiều bác sĩ trực hơn số ghế của chi nhánh, nên chưa thể công bố." />
        <ul style={{ marginTop: 12 }}>{capacity?.map(c => <li key={`${c.branchId}-${c.day}-${c.startMinute}`}>{['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật'][c.day]} {clock(c.startMinute)}–{clock(c.endMinute)}: {c.doctors} bác sĩ cùng ca, chi nhánh chỉ có {c.chairs} ghế</li>)}</ul>
        <Typography.Text type="secondary">Hãy chọn khung giờ khác, hoặc nhờ quản lý tăng số ghế / đổi ca của bác sĩ khác.</Typography.Text>
      </Modal>
      <Modal open={!!affected?.length} title="Có lịch hẹn bị ảnh hưởng" width={720} onCancel={() => setAffected(undefined)}
        footer={<Button type="primary" onClick={() => setAffected(undefined)}>Đóng</Button>}><Alert type="warning" message={`MSG32 · Có ${affected?.length} lịch Confirmed bị ảnh hưởng. Giải quyết tại S12 trước khi công bố.`} /><Table rowKey="id" dataSource={affected} columns={[{ title: 'Mã lịch', dataIndex: 'code', render: (value: string, row: Affected) => <Link to={`/appointments/${row.id}`}>{value} · S12</Link> }, { title: 'Bắt đầu', dataIndex: 'start', render: clinicTime }, { title: 'Kết thúc', dataIndex: 'end', render: clinicTime }, { title: 'Người đến', dataIndex: 'attendee' }, { title: 'Trạng thái', dataIndex: 'status' }]} /></Modal>
    </>}
  </Space>;
}
