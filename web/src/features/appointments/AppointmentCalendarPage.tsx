import { Alert, Button, Card, Empty, Radio, Select, Space, Spin, Tag, Typography, message } from 'antd';
import { useEffect, useMemo, useState } from 'react';
import { api } from '../../services/api';
import AppointmentDetailDrawer from './AppointmentDetailDrawer';
import BookingDrawer, { type BookingPrefill } from './BookingDrawer';
import {
  addDays, clinicToday, clock, formatDate, localDate, localMinute, mondayOf, statusColors, statusLabels, weekday,
  type AppointmentOptions, type AppointmentStatus, type CalendarPeriod, type CalendarView,
} from './types';

const SLOT = 15;
const ROW = 18; // px per 15 minutes
type Mode = 'DAY' | 'WEEK';
type Column = { key: string; title: string; dentistId: number; date: string };

function cellKind(periods: CalendarPeriod[], minute: number) {
  if (periods.some(p => p.kind !== 'Work' && p.startMinute <= minute && minute < p.endMinute)) return 'blocked';
  return periods.some(p => p.kind === 'Work' && p.startMinute <= minute && minute < p.endMinute) ? 'work' : 'off';
}

// S10 Appointment calendar (UC-05). Scope is decided by the server; filters only narrow what the server allows.
export default function AppointmentCalendarPage() {
  const [toast, toastContext] = message.useMessage();
  const [options, setOptions] = useState<AppointmentOptions | null>(null);
  const [optionsError, setOptionsError] = useState<string | null>(null);
  const [branchId, setBranchId] = useState<number>();
  const [dentistId, setDentistId] = useState<number>();
  const [status, setStatus] = useState<AppointmentStatus | 'ALL'>('ALL');
  const [mode, setMode] = useState<Mode>('DAY');
  const [date, setDate] = useState(clinicToday());
  const [view, setView] = useState<CalendarView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [booking, setBooking] = useState<BookingPrefill | null>(null);
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    api<AppointmentOptions>('/api/appointments/options')
      .then(result => { setOptions(result); setBranchId(result.branches[0]?.id); })
      .catch(failure => setOptionsError((failure as Error).message));
  }, []);

  const dentists = options?.dentists.filter(d => d.branchId === branchId) ?? [];
  // Week view shows one dentist across seven days.
  const weekDentist = dentistId ?? dentists[0]?.id;
  const from = mode === 'DAY' ? date : mondayOf(date);
  const to = mode === 'DAY' ? date : addDays(from, 6);

  useEffect(() => {
    if (!branchId) return;
    let active = true;
    const query = new URLSearchParams({ branchId: String(branchId), from, to });
    const filterDentist = mode === 'WEEK' ? weekDentist : dentistId;
    if (filterDentist) query.set('dentistId', String(filterDentist));
    if (status !== 'ALL') query.set('status', status);
    setLoading(true); setError(null);
    api<CalendarView>(`/api/appointments/calendar?${query}`)
      .then(result => { if (active) setView(result); })
      .catch(failure => { if (active) { setView(null); setError((failure as Error).message); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [branchId, dentistId, status, mode, from, to, weekDentist, revision]);

  const columns: Column[] = useMemo(() => {
    if (!view) return [];
    if (mode === 'WEEK') return weekDentist ? Array.from({ length: 7 }, (_, i) => addDays(from, i)).map(d =>
      ({ key: d, title: `${weekday(d)} ${formatDate(d).slice(0, 5)}`, dentistId: weekDentist, date: d })) : [];
    const ids = [...new Set(view.days.map(d => d.dentistId))].filter(id => !dentistId || id === dentistId);
    return ids.map(id => ({ key: String(id), title: dentists.find(d => d.id === id)?.name ?? `Nha sĩ #${id}`, dentistId: id, date }));
  }, [view, mode, from, date, dentistId, weekDentist, dentists]);

  // Grid hours cover every published period and appointment, at least 07:00–19:00.
  const [startMinute, endMinute] = useMemo(() => {
    let start = 7 * 60, end = 19 * 60;
    for (const d of view?.days ?? []) for (const p of d.periods) { start = Math.min(start, p.startMinute); end = Math.max(end, p.endMinute); }
    for (const a of view?.appointments ?? []) { start = Math.min(start, localMinute(a.startAt)); end = Math.max(end, localMinute(a.startAt) + a.durationMinutes); }
    return [Math.floor(start / 60) * 60, Math.min(1440, Math.ceil(end / 60) * 60)];
  }, [view]);
  const rows = Array.from({ length: (endMinute - startMinute) / SLOT }, (_, i) => startMinute + i * SLOT);

  function shift(direction: -1 | 1) { setDate(addDays(date, direction * (mode === 'DAY' ? 1 : 7))); }

  if (optionsError) return <Alert type="error" showIcon message={optionsError} />;
  if (!options) return <Card><Spin /></Card>;
  if (options.branches.length === 0) return <Empty description="Bạn chưa thuộc chi nhánh nào có lịch hẹn." />;

  return <>
    {toastContext}
    <div className="page-heading"><div><Typography.Title level={2}>Lịch hẹn</Typography.Title>
      <Typography.Paragraph>Lưới 15 phút theo giờ phòng khám. Bấm ô trống trong giờ làm để đặt lịch tại quầy; bấm lịch để xem chi tiết.
        {options.ownScheduleOnly && ' Bạn chỉ thấy lịch của chính mình.'}</Typography.Paragraph></div>
      <Button type="primary" onClick={() => setBooking({ branchId: branchId!, dentistId: mode === 'WEEK' ? weekDentist : dentistId, date })}>Đặt lịch</Button></div>
    <Card>
      <div className="calendar-toolbar">
        <Space wrap>
          <Radio.Group optionType="button" value={mode} onChange={e => setMode(e.target.value)} options={[{ value: 'DAY', label: 'Ngày' }, { value: 'WEEK', label: 'Tuần' }]} />
          <Button onClick={() => setDate(clinicToday())}>Hôm nay</Button>
          <Button onClick={() => shift(-1)}>‹ Trước</Button>
          <Button onClick={() => shift(1)}>Sau ›</Button>
          <Typography.Text strong>{mode === 'DAY' ? `${weekday(date)}, ${formatDate(date)}` : `${formatDate(from)} – ${formatDate(to)}`}</Typography.Text>
        </Space>
        <Space wrap>
          <Select aria-label="Chi nhánh" style={{ width: 220 }} value={branchId} options={options.branches.map(b => ({ value: b.id, label: b.name }))}
            onChange={value => { setBranchId(value); setDentistId(undefined); }} />
          <Select aria-label="Nha sĩ" style={{ width: 200 }} allowClear={mode === 'DAY'} placeholder="Tất cả nha sĩ" value={mode === 'WEEK' ? weekDentist : dentistId}
            options={dentists.map(d => ({ value: d.id, label: d.name }))} onChange={setDentistId} />
          <Select aria-label="Trạng thái" style={{ width: 170 }} value={status} onChange={setStatus}
            options={[{ value: 'ALL', label: 'Tất cả trạng thái' }, ...(Object.keys(statusLabels) as AppointmentStatus[]).map(s => ({ value: s, label: statusLabels[s] }))]} />
          <Button loading={loading} onClick={() => setRevision(v => v + 1)}>Tải lại</Button>
        </Space>
      </div>
      <Space className="calendar-legend" size="large">
        <span><i className="legend work" /> Giờ làm</span><span><i className="legend blocked" /> Nghỉ / block</span><span><i className="legend off" /> Ngoài lịch</span>
      </Space>
      {error ? <Alert type="error" showIcon message={error} description="Chưa tải được lịch — đây không phải là lịch trống."
        action={<Button size="small" onClick={() => setRevision(v => v + 1)}>Thử lại</Button>} />
        : !view ? <Spin /> : columns.length === 0 ? <Empty description="Chi nhánh chưa có nha sĩ trong phạm vi của bạn." />
        : <Spin spinning={loading}>
          {view.appointments.length === 0 && <Alert className="form-alert" type="info" showIcon message="Không có lịch hẹn phù hợp với bộ lọc đã chọn." />}
          <div className="calendar-scroll">
            <div className="calendar-grid" style={{ gridTemplateColumns: `56px repeat(${columns.length}, minmax(150px, 1fr))` }}>
              <div className="calendar-corner" />
              {columns.map(c => <div key={c.key} className="calendar-head">{c.title}</div>)}
              <div className="calendar-times">{rows.map(m => <div key={m} style={{ height: ROW }} className={m % 60 === 0 ? 'hour' : ''}>{m % 60 === 0 ? clock(m) : ''}</div>)}</div>
              {columns.map(c => {
                const periods = view.days.find(d => d.dentistId === c.dentistId && d.date === c.date)?.periods ?? [];
                const items = view.appointments.filter(a => a.dentistId === c.dentistId && localDate(a.startAt) === c.date);
                return <div key={c.key} className="calendar-column" style={{ height: rows.length * ROW }}>
                  {rows.map(m => {
                    const kind = cellKind(periods, m);
                    return <div key={m} className={`calendar-cell ${kind}${m % 60 === 0 ? ' hour' : ''}`} style={{ height: ROW }}
                      title={kind === 'work' ? `Đặt lịch ${clock(m)}` : kind === 'blocked' ? periods.find(p => p.kind !== 'Work' && p.startMinute <= m && m < p.endMinute)?.reason ?? 'Nghỉ' : 'Ngoài lịch làm việc'}
                      onClick={() => { if (kind === 'work') setBooking({ branchId: branchId!, dentistId: c.dentistId, date: c.date, minute: m }); }} />;
                  })}
                  {items.map(a => {
                    const top = (localMinute(a.startAt) - startMinute) / SLOT * ROW;
                    return <button type="button" key={a.id} className={`calendar-event status-${a.status}`} style={{ top, height: Math.max(ROW, a.durationMinutes / SLOT * ROW) - 2 }}
                      onClick={() => setSelected(a.id)} aria-label={`Lịch ${a.code}`}>
                      <strong>{clock(localMinute(a.startAt))}–{clock(localMinute(a.endAt))}</strong> {a.attendeeName}
                      <span>{a.serviceName ?? ''}{a.chairName ? ` · ${a.chairName}` : ''}</span>
                      <Tag color={statusColors[a.status]}>{statusLabels[a.status]}</Tag>
                    </button>;
                  })}
                </div>;
              })}
            </div>
          </div>
        </Spin>}
    </Card>
    <BookingDrawer open={booking !== null} prefill={booking} options={options} onClose={() => setBooking(null)} onBooked={result => {
      setBooking(null);
      void toast.success(`${result.notification.message} Mã lịch: ${result.code}`);
      setRevision(v => v + 1); // authoritative refresh from the server
    }} />
    <AppointmentDetailDrawer id={selected} onClose={() => setSelected(null)} />
  </>;
}
