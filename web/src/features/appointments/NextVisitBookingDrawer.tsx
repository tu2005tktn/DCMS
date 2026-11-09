import { Alert, Button, Checkbox, Descriptions, Drawer, Input, InputNumber, Space, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { addDays, clinicToday, formatClock, formatDate, newRequestKey, type AppointmentResult, type AvailabilitySlot } from './types';

export type NextVisitCandidate = {
  dossierId: string; dossierCode: string; itemId: string; serviceId: number; serviceName: string; region: string | null;
  estimatedStart: string | null; durationMinutes: number; dentistId: number; dentistName: string; branchId: number;
  status: 'Ready' | 'NeedsBooking' | 'Booked'; followUpId: string | null;
  appointment: { id: string; code: string; startAt: string } | null; earliestOn: string | null; latestOn: string | null;
  targetOn?: string | null; windowFrom?: string | null; windowTo?: string | null;
};
export type NextVisitPatient = { id: number; fullName: string; phone: string | null };
type DaySlots = { date: string; slots: AvailabilitySlot[] };
type PlannedDateChange = { itemId: string; serviceName: string; region: string | null; currentPlannedOn: string | null; proposedPlannedOn: string; isBookedItem: boolean };
type PlannedDatePreview = { dossierVersion: number; items: PlannedDateChange[] };

const slotChangedCodes = ['MSG25', 'MSG27', 'MSG28', 'MSG34'];
const scanDays = 30, maxSuggestedDays = 5;
const cleanPhone = (value: string) => /^0\d{9}$/.test(value.trim());
const cleanEmail = (value: string) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value.trim());
const diffDays = (a: string, b: string) => Math.round((Date.parse(`${a}T00:00:00Z`) - Date.parse(`${b}T00:00:00Z`)) / 86_400_000);
const itemLine = (name: string, region: string | null) => `${name}${region ? ` · ${region}` : ''}`;

// Days to look at, nearest to the planned date first (the day itself, then -1/+1, -2/+2 ...), never outside [min, max].
function scanOrder(anchor: string, min: string, max: string) {
  const days: string[] = [];
  const push = (day: string) => { if (day >= min && day <= max && !days.includes(day)) days.push(day); };
  push(anchor);
  for (let i = 1; i <= scanDays; i++) { push(addDays(anchor, -i)); push(addDays(anchor, i)); }
  return days;
}

// Books the next treatment-plan item. The planned date is the target; slots only come from the chosen dentist's working
// schedule (GET /api/availability) and the server rechecks it. See docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md.
export default function NextVisitBookingDrawer({ open, candidate, patient, onClose, onBooked }: {
  open: boolean; candidate: NextVisitCandidate | null; patient: NextVisitPatient;
  onClose: () => void; onBooked: (result: AppointmentResult) => void;
}) {
  const has = usePermissions();
  const [date, setDate] = useState(''), [limitEnd, setLimitEnd] = useState('');
  const [days, setDays] = useState<DaySlots[]>([]);
  const [slots, setSlots] = useState<AvailabilitySlot[] | null>(null), [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [loading, setLoading] = useState(false), [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null), [error, setError] = useState<ApiError | string | null>(null);
  const [bookerName, setBookerName] = useState(''), [bookerContact, setBookerContact] = useState(''), [exceptionReason, setExceptionReason] = useState('');
  const [after, setAfter] = useState<{ result: AppointmentResult; preview: PlannedDatePreview } | null>(null);
  const [picked, setPicked] = useState<string[]>([]), [syncError, setSyncError] = useState<string | null>(null);
  const [patientConsent, setPatientConsent] = useState(false);
  const [durationMinutes, setDurationMinutes] = useState<number | null>(null);
  const requestKeys = useRef<{ instruction: string; appointment: string | null; sync: string }>({ instruction: newRequestKey(), appointment: null, sync: newRequestKey() });
  const followUpId = useRef<string | null>(null);
  const generation = useRef(0);
  const today = clinicToday();
  const durationValid = durationMinutes !== null && Number.isInteger(durationMinutes) && durationMinutes >= 1 && durationMinutes <= 480;
  const target = candidate?.targetOn ?? candidate?.estimatedStart ?? null;
  const windowed = !!candidate?.windowFrom && !!candidate.windowTo;
  const minDate = [today, candidate?.earliestOn, candidate?.windowFrom].reduce<string>((a, b) => (b && b > a ? b : a), today);
  // The latest bookable day: the plan window (at most 4 days after the planned date), also bounded by an older instruction.
  const lastDay = (c: NextVisitCandidate) => c.windowTo ? (c.latestOn && c.latestOn < c.windowTo ? c.latestOn : c.windowTo) : c.latestOn ?? addDays(today, scanDays);
  // Where the search starts: the planned date, or today when there is none (or it already passed).
  const anchorOf = (c: NextVisitCandidate) => { const planned = c.targetOn ?? c.estimatedStart; return planned && planned > minDate ? planned : minDate; };

  const availabilityOf = async (day: string) => {
    const query = new URLSearchParams({ branchId: String(candidate!.branchId), dentistId: String(candidate!.dentistId),
      serviceId: String(candidate!.serviceId), date: day, durationMinutes: String(durationMinutes) });
    return (await api<{ slots: AvailabilitySlot[] }>(`/api/availability?${query}`)).slots;
  };

  // Collects the days nearest to the planned date on which the dentist has a free slot and selects the nearest one.
  async function suggest(end: string) {
    if (!candidate || !durationValid) return;
    const run = ++generation.current;
    setLoading(true); setError(null); setNotice(null); setSlot(null); setSlots(null); setDays([]);
    const anchor = anchorOf(candidate);
    try {
      const found: DaySlots[] = [];
      for (const day of scanOrder(anchor, minDate, end)) {
        const result = await availabilityOf(day);
        if (run !== generation.current) return;
        // Inside a plan window every day is offered (at most 9); without a planned date only the nearest few.
        if (result.length > 0) { found.push({ date: day, slots: result }); if (!windowed && found.length >= maxSuggestedDays) break; }
      }
      if (found.length > 0) {
        setDays([...found].sort((a, b) => a.date.localeCompare(b.date)));
        setDate(found[0].date); setSlots(found[0].slots);
        return;
      }
      setDate(anchor); setSlots([]);
      setNotice(windowed
        ? `${candidate.dentistName} chưa có lịch làm còn trống trong khoảng ${formatDate(minDate)} – ${formatDate(end)} (tối đa 4 ngày sớm hoặc trễ so với ngày dự kiến). Liên hệ quản lý lịch làm việc hoặc đổi ngày dự kiến của hạng mục trong kế hoạch.`
        : `${candidate.dentistName} chưa có lịch làm còn trống trong ${scanDays} ngày kể từ ${formatDate(anchor)}. Liên hệ quản lý lịch làm việc hoặc chọn ngày khác.`);
    } catch (failure) { if (run === generation.current) { setSlots(null); setError((failure as Error).message); } }
    finally { if (run === generation.current) setLoading(false); }
  }

  async function loadDay(day: string) {
    if (!candidate || !durationValid) return;
    const run = ++generation.current;
    setLoading(true); setError(null); setNotice(null); setSlot(null); setSlots(null); setDate(day);
    try {
      const result = await availabilityOf(day);
      if (run !== generation.current) return;
      setSlots(result);
      if (result.length === 0) setNotice(`${candidate.dentistName} không có lịch làm còn trống ngày ${formatDate(day)}. Chọn ngày khác.`);
    } catch (failure) { if (run === generation.current) { setSlots(null); setError((failure as Error).message); } }
    finally { if (run === generation.current) setLoading(false); }
  }

  useEffect(() => {
    if (!open || !candidate) return;
    requestKeys.current = { instruction: newRequestKey(), appointment: null, sync: newRequestKey() };
    followUpId.current = candidate.followUpId;
    const end = lastDay(candidate);
    setLimitEnd(end); setBookerName(patient.fullName); setBookerContact(patient.phone ?? ''); setExceptionReason('');
    setAfter(null); setPicked([]); setSyncError(null);
    setPatientConsent(false);
    setDurationMinutes(candidate.durationMinutes);
    return () => { generation.current++; };
  }, [open, candidate?.itemId, candidate?.followUpId]);

  useEffect(() => {
    if (!open || !candidate || !durationValid) return;
    const timer = window.setTimeout(() => void suggest(lastDay(candidate)), 250);
    return () => { window.clearTimeout(timer); generation.current++; };
  }, [open, candidate?.itemId, candidate?.followUpId, durationMinutes]);

  const deviation = (day: string) => {
    if (!target) return null;
    const offset = diffDays(day, target);
    return offset === 0 ? 'Đúng ngày dự kiến' : offset < 0 ? `Sớm ${-offset} ngày` : `Trễ ${offset} ngày`;
  };

  const needsException = !!slot && Date.parse(slot.start) < Date.now() + 60 * 60_000;
  const contactValid = cleanPhone(bookerContact) || cleanEmail(bookerContact);
  const canConfirm = durationValid && !loading && patientConsent && has('appointments.create') && has('treatment.progress.manage') && !!slot && !busy && bookerName.trim().length > 0 && bookerName.trim().length <= 100 && contactValid
    && (!needsException || exceptionReason.trim().length > 0);

  async function confirm() {
    if (!candidate || !slot || !canConfirm) return;
    setBusy(true); setError(null);
    try {
      // The list this drawer was opened from can be stale (an earlier attempt may already have created the instruction).
      if (!followUpId.current) {
        const current = await api<{ items: NextVisitCandidate[] }>(`/api/patients/${patient.id}/next-visit-candidates`);
        followUpId.current = current.items.find(x => x.itemId === candidate.itemId)?.followUpId ?? null;
      }
      if (!followUpId.current) {
        // The instruction carries the bookable window itself, so the server keeps bookings within 4 days of the plan.
        const earliest = candidate.windowFrom ?? today, latest = candidate.windowTo ?? addDays(today, scanDays);
        const anchor = anchorOf(candidate) < earliest ? earliest : anchorOf(candidate) > latest ? latest : anchorOf(candidate);
        const created = await api<{ instruction: { id: string } }>('/api/follow-up-instructions', { method: 'POST', body: JSON.stringify({
          patientId: patient.id, branchId: candidate.branchId, source: { dossierId: candidate.dossierId, itemId: candidate.itemId, stepId: null, visitId: null, activityId: null },
          purpose: 'TreatmentContinuation', reason: `Điều trị theo kế hoạch: ${candidate.serviceName}`, guidance: null,
          earliestOn: earliest, targetOn: anchor, latestOn: latest,
          durationMinutes: candidate.durationMinutes, dentistId: candidate.dentistId, capability: null, requestKey: requestKeys.current.instruction }) });
        followUpId.current = created.instruction.id;
      }
      requestKeys.current.appointment ??= newRequestKey();
      const contact = bookerContact.trim();
      const reason = needsException ? exceptionReason.trim() : null;
      const result = await api<AppointmentResult>('/api/appointments', { method: 'POST', body: JSON.stringify({
        branchId: candidate.branchId, booker: { name: bookerName.trim(), phone: contact.includes('@') ? null : contact, email: contact.includes('@') ? contact : null },
        attendee: { patientId: patient.id, name: patient.fullName, birthDate: null }, attendeeRelation: 'Self', kind: 'FollowUp', source: 'InPerson',
        serviceId: candidate.serviceId, reason: `Điều trị theo kế hoạch: ${candidate.serviceName}${candidate.region ? ` · ${candidate.region}` : ''}`,
        dentistId: candidate.dentistId, assistantId: null, chairId: slot.chairId, startAt: slot.start, durationMinutes,
        followUpId: followUpId.current, bookingSettingsVersion: slot.bookingSettingsVersion,
        patientConsentConfirmed: patientConsent,
        exceptionReason: reason, requestKey: requestKeys.current.appointment,
        scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: slot.assistantScheduleVersion }) });
      requestKeys.current.appointment = null;
      // The appointment is saved. Offer to move the planned date(s) to match it; failing to do so never undoes the booking.
      let preview: PlannedDatePreview | null = null;
      try {
        preview = await api<PlannedDatePreview>(`/api/dossiers/${candidate.dossierId}/plan/items/${candidate.itemId}/planned-date-preview?plannedOn=${date}`);
      } catch { preview = null; }
      if (preview && (preview.items ?? []).length > 0) { setPicked(preview.items.map(x => x.itemId)); setSyncError(null); setAfter({ result, preview }); }
      else onBooked(result);
    } catch (failure) {
      const apiError = failure as ApiError;
      setError(apiError);
      // A connection failure keeps the key so a retry cannot book twice; a rejected command gets a new key next time.
      if (apiError.status !== 0) requestKeys.current.appointment = null;
      if (slotChangedCodes.includes(apiError.code)) void loadDay(date);
    } finally { setBusy(false); }
  }

  async function applyDates() {
    if (!candidate || !after) return;
    setBusy(true); setSyncError(null);
    try {
      await api(`/api/dossiers/${candidate.dossierId}/plan/planned-dates`, { method: 'POST', body: JSON.stringify({
        expectedVersion: after.preview.dossierVersion, requestKey: requestKeys.current.sync,
        changes: after.preview.items.filter(x => picked.includes(x.itemId)).map(x => ({ itemId: x.itemId, plannedOn: x.proposedPlannedOn })) }) });
      onBooked(after.result);
    } catch (failure) { setSyncError((failure as Error).message); }
    finally { setBusy(false); }
  }

  const errorText = typeof error === 'string' ? error : error?.message;
  const close = () => { if (busy) return; if (after) onBooked(after.result); else onClose(); };

  if (after && candidate) return <Drawer open={open} onClose={close} width={560} destroyOnClose title="Cập nhật ngày dự kiến theo lịch hẹn">
    <Alert className="form-alert" type="success" showIcon message={`Đã đặt lịch ${after.result.code} ngày ${formatDate(date)}.`} />
    <Typography.Paragraph className="patient-section">Ngày hẹn khác ngày dự kiến trong kế hoạch. Chọn các hạng mục cần cập nhật ngày dự kiến theo lịch hẹn; nếu giữ nguyên, kế hoạch không thay đổi.</Typography.Paragraph>
    {syncError && <Alert className="form-alert" role="alert" type="error" showIcon message={syncError} />}
    <div className="next-visit-sync" role="group" aria-label="Hạng mục đổi ngày dự kiến">
      {after.preview.items.map(x => <div key={x.itemId}><Checkbox checked={picked.includes(x.itemId)} disabled={busy}
        onChange={event => setPicked(event.target.checked ? [...picked, x.itemId] : picked.filter(id => id !== x.itemId))}>
        <strong>{itemLine(x.serviceName, x.region)}</strong>{x.isBookedItem && ' (vừa hẹn)'}: {x.currentPlannedOn ? formatDate(x.currentPlannedOn) : 'chưa có'} → {formatDate(x.proposedPlannedOn)}
      </Checkbox></div>)}
    </div>
    <Space className="patient-section">
      <Button type="primary" loading={busy} disabled={!has('treatment.items.update') || picked.length === 0} onClick={() => void applyDates()}>Cập nhật ngày dự kiến</Button>
      <Button disabled={busy} onClick={() => onBooked(after.result)}>Giữ nguyên</Button>
    </Space>
  </Drawer>;

  return <Drawer open={open} onClose={close} width={560} destroyOnClose title="Đặt lịch theo kế hoạch điều trị"
    extra={<Space><Button onClick={onClose} disabled={busy}>Hủy</Button>
      <Button type="primary" loading={busy} disabled={!canConfirm} onClick={() => void confirm()}>Xác nhận đặt lịch</Button></Space>}>
    {candidate && <>
      {errorText && <Alert className="form-alert" role="alert" type="error" showIcon message={errorText}
        description={typeof error !== 'string' && error && slotChangedCodes.includes(error.code) ? 'Khung giờ đã thay đổi. Danh sách khung giờ đã được tải lại; thông tin đã nhập được giữ nguyên.' : undefined} />}
      <Descriptions column={1} size="small" bordered items={[
        { label: 'Hạng mục', children: itemLine(candidate.serviceName, candidate.region) },
        { label: 'Phiếu', children: candidate.dossierCode },
        { label: 'Ngày dự kiến', children: target ? formatDate(target) : 'Chưa có' },
        ...(candidate.windowFrom && candidate.windowTo ? [{ label: 'Khoảng cho phép', children: `${formatDate(candidate.windowFrom)} – ${formatDate(candidate.windowTo)}` }] : []),
        { label: 'Bác sĩ phụ trách', children: candidate.dentistName },
      ]} />
      <div className="patient-section">
        <label htmlFor="next-visit-duration">Thời lượng (phút)</label>
        <InputNumber id="next-visit-duration" min={1} max={480} step={1} value={durationMinutes} disabled={busy}
          status={!durationValid ? 'error' : undefined} aria-invalid={!durationValid} aria-describedby="next-visit-duration-help"
          onChange={value => {
            generation.current++; setSlot(null); setSlots(null); setDays([]); setNotice(null); setError(null);
            setLoading(false); requestKeys.current.appointment = null; setDurationMinutes(value);
          }} />
        <Typography.Paragraph id="next-visit-duration-help" type={durationValid ? 'secondary' : 'danger'}>
          {durationValid ? '1–480 phút. Áp dụng cho lần hẹn này; đổi thời lượng cần chọn lại giờ.' : 'Nhập số phút nguyên từ 1 đến 480.'}
        </Typography.Paragraph>
      </div>
      <Typography.Title level={5} className="patient-section">Ngày và khung giờ</Typography.Title>
      {days.length > 0 && <div className="booking-slots" role="group" aria-label="Ngày gần ngày dự kiến có lịch của bác sĩ">
        {days.map(d => <Button key={d.date} type={d.date === date ? 'primary' : 'default'} aria-pressed={d.date === date} disabled={busy}
          onClick={() => { setDate(d.date); setSlots(d.slots); setSlot(null); setNotice(null); }}>
          {formatDate(d.date)}{deviation(d.date) ? ` · ${deviation(d.date)}` : ''}</Button>)}</div>}
      <label htmlFor="next-visit-date">Ngày hẹn (giờ Việt Nam)</label>
      <Input id="next-visit-date" type="date" value={date} min={minDate} max={limitEnd || undefined} disabled={busy}
        onChange={event => { const value = event.target.value; if (value) void loadDay(value); }} />
      {date && deviation(date) && <Typography.Text type="secondary" className="next-visit-deviation">
        Độ lệch so với ngày dự kiến: {deviation(date)}</Typography.Text>}
      {notice && <Alert className="form-alert" type={slots && slots.length > 0 ? 'info' : 'warning'} showIcon message={notice} />}
      {loading ? <Spin /> : slots && slots.length > 0 && <div className="booking-slots" role="group" aria-label="Khung giờ bác sĩ có lịch làm">
        {slots.map(s => {
          const selected = slot?.start === s.start && slot.chairId === s.chairId;
          return <Button key={`${s.start}-${s.chairId}`} type={selected ? 'primary' : 'default'} aria-pressed={selected} onClick={() => setSlot(s)}>{formatClock(s.start)}</Button>;
        })}</div>}
      {slot && <Typography.Paragraph className="patient-section">Đã chọn {formatDate(date)} · {formatClock(slot.start)}–{formatClock(slot.end)}</Typography.Paragraph>}
      {needsException && <><label htmlFor="next-visit-exception">Lý do ngoại lệ (bắt đầu trong vòng 60 phút)</label>
        <Input.TextArea id="next-visit-exception" rows={2} maxLength={500} value={exceptionReason} onChange={event => setExceptionReason(event.target.value)} /></>}
      <Typography.Title level={5} className="patient-section">Người liên hệ</Typography.Title>
      <label htmlFor="next-visit-booker">Họ tên người đặt</label>
      <Input id="next-visit-booker" maxLength={100} value={bookerName} onChange={event => setBookerName(event.target.value)} disabled={busy} />
      <label htmlFor="next-visit-contact">SĐT (10 số) hoặc email</label>
      <Input id="next-visit-contact" maxLength={254} value={bookerContact} status={bookerContact && !contactValid ? 'error' : undefined}
        onChange={event => setBookerContact(event.target.value)} disabled={busy} />
      {bookerContact && !contactValid && <Typography.Text type="danger">Liên hệ người đặt không đúng định dạng.</Typography.Text>}
      <div className="patient-section"><Checkbox checked={patientConsent} disabled={busy} onChange={event => setPatientConsent(event.target.checked)}>
        Đã có sự đồng ý của bệnh nhân
      </Checkbox></div>
    </>}
  </Drawer>;
}
import { usePermissions } from '../authentication/usePermissions';
