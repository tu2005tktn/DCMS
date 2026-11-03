import { Alert, Button, Descriptions, Drawer, Input, Space, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { addDays, clinicToday, formatClock, formatDate, newRequestKey, type AppointmentResult, type AvailabilitySlot } from './types';

export type NextVisitCandidate = {
  dossierId: string; dossierCode: string; itemId: string; serviceId: number; serviceName: string; region: string | null;
  estimatedStart: string | null; durationMinutes: number; dentistId: number; dentistName: string; branchId: number;
  status: 'Ready' | 'NeedsBooking' | 'Booked'; followUpId: string | null;
  appointment: { id: string; code: string; startAt: string } | null; earliestOn: string | null; latestOn: string | null;
};
export type NextVisitPatient = { id: number; fullName: string; phone: string | null };

const slotChangedCodes = ['MSG25', 'MSG27', 'MSG28', 'MSG34'];
const scanDays = 30;
const cleanPhone = (value: string) => /^0\d{9}$/.test(value.trim());
const cleanEmail = (value: string) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value.trim());

// Books the next treatment-plan item. Slots only come from the chosen dentist's working schedule (GET /api/availability);
// the server rechecks it when the appointment is created. See docs/specs/TREATMENT-NEXT-VISIT-BOOKING.md.
export default function NextVisitBookingDrawer({ open, candidate, patient, onClose, onBooked }: {
  open: boolean; candidate: NextVisitCandidate | null; patient: NextVisitPatient;
  onClose: () => void; onBooked: (result: AppointmentResult) => void;
}) {
  const [date, setDate] = useState(''), [windowEnd, setWindowEnd] = useState('');
  const [slots, setSlots] = useState<AvailabilitySlot[] | null>(null), [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [loading, setLoading] = useState(false), [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null), [error, setError] = useState<ApiError | string | null>(null);
  const [bookerName, setBookerName] = useState(''), [bookerContact, setBookerContact] = useState(''), [exceptionReason, setExceptionReason] = useState('');
  const requestKeys = useRef<{ instruction: string; appointment: string | null }>({ instruction: newRequestKey(), appointment: null });
  const followUpId = useRef<string | null>(null);
  const generation = useRef(0);
  const today = clinicToday();

  // Looks for the first day with a free slot of the dentist, starting at `from` and never past the instruction window.
  async function findSlots(from: string, end: string, scan: boolean) {
    if (!candidate) return;
    const run = ++generation.current;
    setLoading(true); setError(null); setNotice(null); setSlot(null); setSlots(null);
    try {
      const last = scan ? addDays(from, scanDays) < end ? addDays(from, scanDays) : end : from;
      for (let day = from; day <= last; day = addDays(day, 1)) {
        const query = new URLSearchParams({ branchId: String(candidate.branchId), dentistId: String(candidate.dentistId),
          serviceId: String(candidate.serviceId), date: day, durationMinutes: String(candidate.durationMinutes) });
        const result = await api<{ slots: AvailabilitySlot[] }>(`/api/availability?${query}`);
        if (run !== generation.current) return;
        if (result.slots.length > 0) {
          setDate(day); setSlots(result.slots);
          return;
        }
      }
      setDate(from); setSlots([]);
      setNotice(scan
        ? `${candidate.dentistName} chưa có lịch làm còn trống trong ${scanDays} ngày kể từ ${formatDate(from)}. Liên hệ quản lý lịch làm việc hoặc chọn ngày khác.`
        : `${candidate.dentistName} không có lịch làm còn trống ngày ${formatDate(from)}. Chọn ngày khác.`);
    } catch (failure) { if (run === generation.current) { setSlots(null); setError((failure as Error).message); } }
    finally { if (run === generation.current) setLoading(false); }
  }

  useEffect(() => {
    if (!open || !candidate) return;
    requestKeys.current = { instruction: newRequestKey(), appointment: null };
    followUpId.current = candidate.followUpId;
    const target = candidate.estimatedStart && candidate.estimatedStart > today ? candidate.estimatedStart : today;
    const start = candidate.earliestOn && candidate.earliestOn > target ? candidate.earliestOn : target;
    const end = candidate.latestOn ?? addDays(start, scanDays);
    setWindowEnd(end); setBookerName(patient.fullName); setBookerContact(patient.phone ?? ''); setExceptionReason('');
    void findSlots(start, end, true);
    return () => { generation.current++; };
  }, [open, candidate?.itemId, candidate?.followUpId]);

  const needsException = !!slot && Date.parse(slot.start) < Date.now() + 60 * 60_000;
  const contactValid = cleanPhone(bookerContact) || cleanEmail(bookerContact);
  const canConfirm = !!slot && !busy && bookerName.trim().length > 0 && bookerName.trim().length <= 100 && contactValid
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
        const target = date > today ? date : today;
        const created = await api<{ instruction: { id: string } }>('/api/follow-up-instructions', { method: 'POST', body: JSON.stringify({
          patientId: patient.id, branchId: candidate.branchId, source: { dossierId: candidate.dossierId, itemId: candidate.itemId, stepId: null, visitId: null, activityId: null },
          purpose: 'TreatmentContinuation', reason: `Điều trị theo kế hoạch: ${candidate.serviceName}`, guidance: null,
          earliestOn: today, targetOn: target, latestOn: windowEnd < target ? addDays(target, scanDays) : windowEnd,
          durationMinutes: candidate.durationMinutes, dentistId: candidate.dentistId, capability: null, requestKey: requestKeys.current.instruction }) });
        followUpId.current = created.instruction.id;
      }
      requestKeys.current.appointment ??= newRequestKey();
      const contact = bookerContact.trim();
      const result = await api<AppointmentResult>('/api/appointments', { method: 'POST', body: JSON.stringify({
        branchId: candidate.branchId, booker: { name: bookerName.trim(), phone: contact.includes('@') ? null : contact, email: contact.includes('@') ? contact : null },
        attendee: { patientId: patient.id, name: patient.fullName, birthDate: null }, attendeeRelation: 'Self', kind: 'FollowUp', source: 'InPerson',
        serviceId: candidate.serviceId, reason: `Điều trị theo kế hoạch: ${candidate.serviceName}${candidate.region ? ` · ${candidate.region}` : ''}`,
        dentistId: candidate.dentistId, assistantId: null, chairId: slot.chairId, startAt: slot.start, durationMinutes: candidate.durationMinutes,
        followUpId: followUpId.current, bookingSettingsVersion: slot.bookingSettingsVersion,
        exceptionReason: needsException ? exceptionReason.trim() : null, requestKey: requestKeys.current.appointment,
        scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: slot.assistantScheduleVersion }) });
      requestKeys.current.appointment = null;
      onBooked(result);
    } catch (failure) {
      const apiError = failure as ApiError;
      setError(apiError);
      // A connection failure keeps the key so a retry cannot book twice; a rejected command gets a new key next time.
      if (apiError.status !== 0) requestKeys.current.appointment = null;
      if (slotChangedCodes.includes(apiError.code)) void findSlots(date, windowEnd, false);
    } finally { setBusy(false); }
  }

  const minDate = candidate?.earliestOn && candidate.earliestOn > today ? candidate.earliestOn : today;
  const errorText = typeof error === 'string' ? error : error?.message;
  return <Drawer open={open} onClose={() => { if (!busy) onClose(); }} width={560} destroyOnClose title="Đặt lịch theo kế hoạch điều trị"
    extra={<Space><Button onClick={onClose} disabled={busy}>Hủy</Button>
      <Button type="primary" loading={busy} disabled={!canConfirm} onClick={() => void confirm()}>Xác nhận đặt lịch</Button></Space>}>
    {candidate && <>
      {errorText && <Alert className="form-alert" role="alert" type="error" showIcon message={errorText}
        description={typeof error !== 'string' && error && slotChangedCodes.includes(error.code) ? 'Khung giờ đã thay đổi. Danh sách khung giờ đã được tải lại; thông tin đã nhập được giữ nguyên.' : undefined} />}
      <Descriptions column={1} size="small" bordered items={[
        { label: 'Hạng mục', children: `${candidate.serviceName}${candidate.region ? ` · ${candidate.region}` : ''}` },
        { label: 'Phiếu', children: candidate.dossierCode },
        { label: 'Ngày dự kiến', children: candidate.estimatedStart ? formatDate(candidate.estimatedStart) : 'Chưa có' },
        { label: 'Bác sĩ phụ trách', children: candidate.dentistName },
        { label: 'Thời lượng', children: `${candidate.durationMinutes} phút` },
      ]} />
      <Typography.Title level={5} className="patient-section">Ngày và khung giờ</Typography.Title>
      <label htmlFor="next-visit-date">Ngày hẹn (giờ Việt Nam)</label>
      <Input id="next-visit-date" type="date" value={date} min={minDate} max={windowEnd || undefined} disabled={busy}
        onChange={event => { const value = event.target.value; if (value) { setDate(value); void findSlots(value, windowEnd, false); } }} />
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
    </>}
  </Drawer>;
}
