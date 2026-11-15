import { Alert, Button, Descriptions, Drawer, Empty, Input, Modal, Select, Space, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../authentication/usePermissions';
import { clinicToday, formatClock, formatDate, localDate, newRequestKey, type AppointmentListItem, type AppointmentOptions, type AvailabilitySlot, type StaffAppointmentDetail } from './types';
import ReplacementBooking from './ReplacementBooking';

type Mode = 'edit' | 'cancel' | 'no-show';
type ChangeResult = { notification: { message: string } };
const eligible = (appointment: AppointmentListItem) => appointment.status === 'Confirmed' && Date.parse(appointment.startAt) > Date.now();
const noShowEligible = (appointment: AppointmentListItem) => appointment.status === 'Confirmed' && Date.parse(appointment.startAt) <= Date.now();
const slotKey = (slot: AvailabilitySlot) => `${slot.start}:${slot.chairId}`;

export default function StaffAppointmentActions({ appointment, onChanged, size = 'small' }: {
  appointment: AppointmentListItem; onChanged: (notice: string) => void; size?: 'small' | 'middle';
}) {
  const has = usePermissions();
  const [mode, setMode] = useState<Mode | null>(null);
  const [replacement, setReplacement] = useState(false);
  const [clockRevision, tick] = useState(0);
  useEffect(() => {
    const deadline = Date.parse(appointment.status === 'Confirmed' ? appointment.startAt : appointment.endAt);
    if (!['Confirmed', 'NoShow'].includes(appointment.status) || deadline <= Date.now()) return;
    const timer = window.setTimeout(() => tick(value => value + 1), Math.min(2147483647, deadline - Date.now() + 1));
    return () => window.clearTimeout(timer);
  }, [appointment.status, appointment.startAt, appointment.endAt, clockRevision]);
  const canEdit = has('appointments.reschedule'), canCancel = has('appointments.cancel');
  const canNoShow = has('appointments.no-show') && noShowEligible(appointment);
  const canReplace = has('appointments.create') && appointment.status === 'NoShow' && Date.parse(appointment.endAt) > Date.now();
  if ((!eligible(appointment) || !canEdit && !canCancel) && !canNoShow && !canReplace && !mode && !replacement) return null;
  return <>
    <Space wrap>
      {eligible(appointment) && canEdit && <Button size={size} onClick={() => setMode('edit')}>Chỉnh sửa lịch</Button>}
      {eligible(appointment) && canCancel && <Button size={size} danger onClick={() => setMode('cancel')}>Hủy lịch</Button>}
      {canNoShow && <Button size={size} onClick={() => setMode('no-show')}>Khách không tới</Button>}
      {canReplace && <Button size={size} type="primary" onClick={() => setReplacement(true)}>Đặt lịch thay thế</Button>}
    </Space>
    {mode && <ChangeForm key={`${appointment.id}:${mode}`} appointment={appointment} mode={mode} onClose={() => setMode(null)}
      onChanged={notice => { setMode(null); onChanged(notice); }} />}
    {replacement && <ReplacementBooking source={appointment} onClose={() => setReplacement(false)} onBooked={message => { setReplacement(false); onChanged(message); }} />}
  </>;
}

function ChangeForm({ appointment, mode, onClose, onChanged }: {
  appointment: AppointmentListItem; mode: Mode; onClose: () => void; onChanged: (notice: string) => void;
}) {
  const has = usePermissions();
  const [current, setCurrent] = useState(appointment);
  const [date, setDate] = useState(localDate(appointment.startAt)), [dentist, setDentist] = useState(appointment.dentistId);
  const [options, setOptions] = useState<AppointmentOptions | null>(null), [optionsError, setOptionsError] = useState('');
  const [slots, setSlots] = useState<AvailabilitySlot[]>([]), [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [reason, setReason] = useState(mode === 'no-show' ? 'Khách không tới theo lịch hẹn.' : ''), [error, setError] = useState<ApiError | Error | null>(null);
  const [loading, setLoading] = useState(false), [busy, setBusy] = useState(false), [reloading, setReloading] = useState(false), [revision, setRevision] = useState(0);
  const pending = useRef<{ fingerprint: string; key: string } | null>(null), sending = useRef(false);
  const currentEligible = mode === 'no-show' ? noShowEligible(current) : eligible(current);
  const allowed = has(mode === 'edit' ? 'appointments.reschedule' : mode === 'no-show' ? 'appointments.no-show' : 'appointments.cancel') && currentEligible;
  useEffect(() => {
    if (mode !== 'edit') return;
    let active = true; const controller = new AbortController(); setOptionsError('');
    void api<AppointmentOptions>('/api/appointments/options', { signal: controller.signal }).then(value => { if (active) setOptions(value); })
      .catch(failure => { if (active) setOptionsError((failure as Error).message); });
    return () => { active = false; controller.abort(); };
  }, [mode, revision]);
  useEffect(() => {
    if (mode !== 'edit' || !allowed || !/^\d{4}-\d{2}-\d{2}$/.test(date) || !dentist) return;
    let active = true; const controller = new AbortController();
    setLoading(true); setSlots([]); setSlot(null);
    const query = new URLSearchParams({ date, dentistId: String(dentist) });
    void api<{ slots: AvailabilitySlot[] }>(`/api/appointments/${current.code}/availability?${query}`, { signal: controller.signal })
      .then(value => { if (active) setSlots(value.slots); })
      .catch(failure => { if (active) setError(failure as Error); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; controller.abort(); };
  }, [mode, current.code, current.version, allowed, date, dentist, revision]);

  async function reloadCurrent() {
    if (sending.current) return;
    setReloading(true); setError(null); setSlot(null); setSlots([]);
    try {
      const latest = await api<StaffAppointmentDetail>(`/api/appointments/${current.id}`);
      setCurrent(latest.appointment); setSlot(null); setSlots([]); pending.current = null; setRevision(value => value + 1);
    } catch (failure) { setError(failure as Error); }
    finally { setReloading(false); }
  }
  async function submit() {
    if (!allowed || !reason.trim() || sending.current || mode === 'edit' && (!slot || loading || optionsError)) return;
    const payload = { expectedVersion: current.version, reason: reason.trim(), ...(mode !== 'no-show' ? { clinicInitiated: true } : {}),
      ...(mode === 'edit' && slot ? { dentistId: dentist, newStart: slot.start, chairId: slot.chairId, scheduleVersion: slot.scheduleVersion,
        assistantScheduleVersion: slot.assistantScheduleVersion, bookingSettingsVersion: slot.bookingSettingsVersion } : {}) };
    const fingerprint = JSON.stringify(payload);
    if (pending.current?.fingerprint !== fingerprint) pending.current = { fingerprint, key: newRequestKey() };
    sending.current = true; setBusy(true); setError(null);
    try {
      const result = await api<ChangeResult>(`/api/appointments/${current.code}/${mode === 'edit' ? 'reschedule' : mode === 'no-show' ? 'no-show' : 'cancel'}`,
        { method: 'POST', body: JSON.stringify({ ...payload, requestKey: pending.current.key }) });
      onChanged(result.notification.message);
    } catch (failure) {
      const apiError = failure as ApiError; setError(apiError);
      if (apiError.status !== 0) pending.current = null;
      if (apiError.code === 'MSG34') { setSlot(null); setRevision(value => value + 1); }
    } finally { sending.current = false; setBusy(false); }
  }
  const valid = allowed && reason.trim().length > 0 && reason.trim().length <= 500 && !reloading
    && (mode !== 'edit' || !!slot && !loading && !optionsError);
  const feedback = <>
    {error && <Alert className="form-alert" type="error" showIcon message={error.message}
      action={error instanceof ApiError && error.code === 'MSG111' ? <Button aria-label="Tải lại phiên bản" loading={reloading} onClick={() => void reloadCurrent()}>Tải lại phiên bản</Button> : undefined} />}
    {!currentEligible && <Alert className="form-alert" type="warning" message="Lịch hẹn không còn ở trạng thái có thể thực hiện thao tác này." />}
  </>;
  const summary = <Descriptions bordered size="small" column={1} items={[
    { label: 'Mã lịch', children: current.code }, { label: 'Bệnh nhân', children: current.attendeeName },
    { label: 'Lịch hiện tại', children: `${formatDate(localDate(current.startAt))} · ${formatClock(current.startAt)}–${formatClock(current.endAt)}` },
    { label: 'Chi nhánh', children: current.branchName }, { label: 'Bác sĩ', children: current.dentistName },
    { label: 'Thời lượng', children: `${current.durationMinutes} phút` },
  ]} />;
  const reasonField = <div className="patient-section"><label htmlFor="staff-appointment-change-reason">Lý do {mode === 'edit' ? 'chỉnh sửa' : mode === 'no-show' ? 'khách không tới' : 'hủy lịch'} (bắt buộc)</label>
    <Input.TextArea id="staff-appointment-change-reason" value={reason} maxLength={500} showCount rows={3} disabled={busy}
      onChange={event => setReason(event.target.value)} /></div>;
  if (mode === 'cancel') return <Modal open title="Hủy lịch hẹn" onCancel={() => { if (!busy) onClose(); }} maskClosable={!busy} closable={!busy}
    okText="Xác nhận hủy lịch" cancelText="Giữ lịch hẹn" confirmLoading={busy} okButtonProps={{ 'aria-label': 'Xác nhận hủy lịch', danger: true, disabled: busy || !valid }}
    cancelButtonProps={{ disabled: busy }} onOk={() => void submit()}>
    {feedback}<Alert className="form-alert" type="warning" showIcon message="Lịch hẹn sẽ được hủy và khung giờ được giải phóng." />
    {summary}{reasonField}
  </Modal>;
  if (mode === 'no-show') return <Modal open title="Khách không tới" onCancel={() => { if (!busy) onClose(); }} maskClosable={!busy} closable={!busy}
    okText="Xác nhận khách không tới" cancelText="Giữ nguyên" confirmLoading={busy} okButtonProps={{ 'aria-label': 'Xác nhận khách không tới', disabled: busy || !valid }}
    cancelButtonProps={{ disabled: busy }} onOk={() => void submit()}>
    {feedback}
    {summary}{reasonField}
  </Modal>;
  return <Drawer open title="Chỉnh sửa lịch hẹn" aria-label="Chỉnh sửa lịch hẹn" width={560} styles={{ wrapper: { maxWidth: '100vw' } }} closable={!busy} maskClosable={!busy} onClose={() => { if (!busy) onClose(); }}
    footer={<Space wrap><Button disabled={busy} onClick={onClose}>Đóng</Button><Button type="primary" aria-label="Lưu thay đổi" disabled={busy || !valid} loading={busy} onClick={() => void submit()}>Lưu thay đổi</Button></Space>}>
    {feedback}{summary}
    {optionsError && <Alert className="form-alert" type="error" message={optionsError} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
    <div className="patient-section"><label htmlFor="staff-appointment-new-date">Ngày hẹn mới (giờ Việt Nam)</label>
      <Input id="staff-appointment-new-date" type="date" min={clinicToday()} max="2100-12-31" value={date} disabled={busy}
        onChange={event => { setDate(event.target.value); setSlot(null); setSlots([]); setError(null); }} /></div>
    <div className="patient-section"><label htmlFor="staff-appointment-new-dentist">Bác sĩ mới</label>
      <Select id="staff-appointment-new-dentist" aria-label="Bác sĩ mới" style={{ width: '100%' }} value={dentist} disabled={busy || !options}
        options={options?.dentists.filter(value => value.branchId === current.branchId).map(value => ({ value: value.id, label: value.name }))}
        onChange={value => { setDentist(value); setSlot(null); setSlots([]); setError(null); }} /></div>
    <Typography.Title level={5}>Khung giờ mới</Typography.Title>
    {loading || reloading ? <Spin /> : slots.length ? <div className="booking-slots" role="group" aria-label="Khung giờ chỉnh sửa lịch hẹn">
      {slots.map(value => <Button key={slotKey(value)} disabled={busy} type={slot && slotKey(slot) === slotKey(value) ? 'primary' : 'default'}
        aria-pressed={!!slot && slotKey(slot) === slotKey(value)} onClick={() => setSlot(value)}>{formatClock(value.start)}–{formatClock(value.end)}</Button>)}
    </div> : <Empty description="Không có khung giờ phù hợp. Chọn ngày hoặc bác sĩ khác." />}
    {slot && <Typography.Paragraph>Giờ mới đã chọn: {formatDate(date)} · {formatClock(slot.start)}–{formatClock(slot.end)}</Typography.Paragraph>}
    {reasonField}
  </Drawer>;
}
