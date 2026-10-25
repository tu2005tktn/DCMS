import { Alert, Button, Descriptions, Drawer, Form, Input, InputNumber, Radio, Select, Space, Spin, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import {
  clinicToday, clock, formatClock, formatDate, kindLabels, localMinute, newRequestKey, sourceLabels,
  type AppointmentOptions, type AppointmentResult, type AvailabilitySlot,
} from './types';

export type BookingPrefill = { branchId: number; dentistId?: number; date: string; minute?: number };
type Values = {
  kind: 'Standard' | 'WalkIn'; source: string; bookerName: string; bookerContact: string; patientId?: number;
  attendeeName?: string; attendeeBirthDate?: string; relation: 'Self' | 'Relative'; serviceId?: number; reason?: string;
  branchId: number; dentistId?: number; chairId?: number; date: string; duration: number; exceptionReason?: string;
};
type PatientOption = { id: number; patientCode: string; fullName: string; birthDate: string | null; phone: string | null };
const slotChangedCodes = ['MSG25', 'MSG27', 'MSG28', 'MSG34'];

// S11 counter booking (UC-04). Slots come from Tư's availability for the exact staff duration; the booking itself is
// Vinh's shared POST /api/appointments with the staff session. Contract: docs/implementation/UC04-05-S10-S11.md §5.
export default function BookingDrawer({ open, prefill, options, onClose, onBooked }: {
  open: boolean; prefill: BookingPrefill | null; options: AppointmentOptions | null;
  onClose: () => void; onBooked: (result: AppointmentResult) => void;
}) {
  const auth = useAuth();
  const canSearchPatients = auth.user?.permissions.includes('patients.read') ?? false;
  const [form] = Form.useForm<Values>();
  const values = Form.useWatch([], form) as Values | undefined;
  const [slots, setSlots] = useState<AvailabilitySlot[] | null>(null);
  const [slotsError, setSlotsError] = useState<string | null>(null);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [slot, setSlot] = useState<AvailabilitySlot | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [patients, setPatients] = useState<PatientOption[]>([]);
  const requestKey = useRef<string | null>(null);
  const wantedMinute = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (!open || !prefill) return;
    form.resetFields();
    form.setFieldsValue({ kind: 'Standard', source: 'Phone', relation: 'Self', branchId: prefill.branchId, dentistId: prefill.dentistId,
      date: prefill.date, duration: 30 });
    wantedMinute.current = prefill.minute;
    setSlots(null); setSlot(null); setReviewing(false); setError(null); setSlotsError(null); requestKey.current = null;
  }, [open, prefill]);

  const branchId = values?.branchId;
  const branchDentists = options?.dentists.filter(x => x.branchId === branchId) ?? [];
  const branchChairs = options?.chairs.filter(x => x.branchId === branchId) ?? [];
  const branchServices = options?.services.filter(x => x.branchId === branchId) ?? [];
  const durationValid = !!values?.duration && Number.isInteger(values.duration) && values.duration >= 1 && values.duration <= 480;
  const canQuery = !!values?.branchId && !!values.dentistId && !!values.serviceId && !!values.date && durationValid;
  const needsException = !!slot && Date.parse(slot.start) < Date.now() + 60 * 60_000;
  const shownSlots = (slots ?? []).filter(s => !values?.chairId || s.chairId === values.chairId);

  // Any change to the resource selection invalidates the chosen slot; the server rechecks everything on confirm anyway.
  async function loadSlots() {
    if (!canQuery || !values) return;
    setLoadingSlots(true); setSlotsError(null); setSlot(null); setReviewing(false);
    try {
      const query = new URLSearchParams({ branchId: String(values.branchId), dentistId: String(values.dentistId),
        serviceId: String(values.serviceId), date: values.date, durationMinutes: String(values.duration) });
      const result = await api<{ slots: AvailabilitySlot[] }>(`/api/availability?${query}`);
      setSlots(result.slots);
      const wanted = wantedMinute.current;
      if (wanted !== undefined) {
        setSlot(result.slots.find(s => localMinute(s.start) === wanted && (!values.chairId || s.chairId === values.chairId)) ?? null);
        wantedMinute.current = undefined;
      }
    } catch (failure) { setSlots(null); setSlotsError((failure as Error).message); }
    finally { setLoadingSlots(false); }
  }

  useEffect(() => { if (open && canQuery) void loadSlots(); else { setSlots(null); setSlot(null); } },
    [open, values?.branchId, values?.dentistId, values?.serviceId, values?.date, values?.duration]);

  async function searchPatients(keyword: string) {
    if (!canSearchPatients || keyword.trim().length < 2) return;
    try { setPatients((await api<{ items: PatientOption[] }>(`/api/patients?keyword=${encodeURIComponent(keyword.trim())}&pageSize=20`)).items); }
    catch { setPatients([]); }
  }

  async function review() {
    setError(null);
    try { await form.validateFields(); } catch { return; }
    if (!slot) return;
    if (needsException && !form.getFieldValue('exceptionReason')?.trim()) {
      form.setFields([{ name: 'exceptionReason', errors: ['Vui lòng nhập Lý do ngoại lệ.'] }]); return;
    }
    setReviewing(true);
  }

  async function confirm() {
    if (!slot || !values || busy) return;
    requestKey.current ??= newRequestKey();
    setBusy(true); setError(null);
    const contact = values.bookerContact.trim();
    const patient = patients.find(p => p.id === values.patientId);
    try {
      const result = await api<AppointmentResult>('/api/appointments', { method: 'POST', body: JSON.stringify({
        branchId: values.branchId,
        booker: { name: values.bookerName.trim(), phone: contact.includes('@') ? null : contact, email: contact.includes('@') ? contact : null },
        attendee: { patientId: values.patientId ?? null, name: patient?.fullName ?? values.attendeeName?.trim() ?? null,
          birthDate: values.patientId ? null : values.attendeeBirthDate || null },
        attendeeRelation: values.relation, kind: values.kind, source: values.source, serviceId: values.serviceId ?? null,
        reason: values.reason?.trim() || null, dentistId: values.dentistId, assistantId: null, chairId: slot.chairId,
        startAt: slot.start, durationMinutes: values.duration, followUpId: null, bookingSettingsVersion: slot.bookingSettingsVersion,
        exceptionReason: needsException ? values.exceptionReason?.trim() : null,
        requestKey: requestKey.current, scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: slot.assistantScheduleVersion,
      }) });
      requestKey.current = null;
      onBooked(result);
    } catch (failure) {
      const apiError = failure as ApiError;
      setError(apiError);
      // A connection failure keeps the key so a retry cannot book twice; a rejected command gets a new key next time.
      if (apiError.status !== 0) requestKey.current = null;
      if (slotChangedCodes.includes(apiError.code)) { setReviewing(false); void loadSlots(); }
    } finally { setBusy(false); }
  }

  const dentistName = branchDentists.find(x => x.id === values?.dentistId)?.name;
  const chairName = (id: number) => branchChairs.find(x => x.id === id)?.name ?? `Ghế ${id}`;

  return <Drawer open={open} onClose={onClose} width={640} destroyOnClose title="Đặt lịch tại quầy (S11)"
    extra={<Space><Button onClick={onClose} disabled={busy}>Hủy</Button>
      {reviewing ? <Button type="primary" loading={busy} onClick={() => void confirm()}>Xác nhận đặt lịch</Button>
        : <Button type="primary" disabled={!slot} onClick={() => void review()}>Xem lại</Button>}</Space>}>
    {error && <Alert className="form-alert" type="error" showIcon message={error.message}
      description={slotChangedCodes.includes(error.code) ? 'Khung giờ đã thay đổi. Danh sách khung giờ đã được tải lại; thông tin đã nhập được giữ nguyên.' : undefined} />}
    <Form<Values> form={form} layout="vertical" disabled={reviewing || busy} style={{ display: reviewing ? 'none' : undefined }}>
      <div className="patient-form-grid">
        <Form.Item name="kind" label="Loại lịch"><Radio.Group optionType="button" options={[
          { value: 'Standard', label: kindLabels.Standard }, { value: 'WalkIn', label: kindLabels.WalkIn },
          { value: 'FollowUp', label: <Tooltip title="Cần module hướng dẫn tái khám (TV4), chưa có">{kindLabels.FollowUp}</Tooltip>, disabled: true }]} /></Form.Item>
        <Form.Item name="source" label="Nguồn" extra="Chỉ ghi nhận nguồn, không kết nối kênh.">
          <Select options={Object.entries(sourceLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
      </div>
      <Typography.Title level={5}>Người đặt</Typography.Title>
      <div className="patient-form-grid">
        <Form.Item name="bookerName" label="Họ tên người đặt" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Họ tên người đặt.' },
          { max: 100, message: 'Họ tên người đặt không được vượt quá 100 ký tự.' }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="bookerContact" label="SĐT (10 số) hoặc email" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Liên hệ người đặt.' },
          { validator: (_, v?: string) => !v || /^0\d{9}$/.test(v.trim()) || /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v.trim())
            ? Promise.resolve() : Promise.reject(new Error('Liên hệ người đặt không đúng định dạng.')) }]}><Input maxLength={254} /></Form.Item>
      </div>
      <Typography.Title level={5}>Người khám</Typography.Title>
      <div className="patient-form-grid">
        <Form.Item name="relation" label="Quan hệ với người đặt"><Radio.Group options={[{ value: 'Self', label: 'Bản thân' }, { value: 'Relative', label: 'Người thân' }]} /></Form.Item>
        {canSearchPatients && <Form.Item name="patientId" label="Hồ sơ bệnh nhân có sẵn (tùy chọn)">
          <Select showSearch allowClear filterOption={false} placeholder="Gõ mã, tên hoặc SĐT" onSearch={value => void searchPatients(value)}
            notFoundContent={null} options={patients.map(p => ({ value: p.id, label: `${p.patientCode} · ${p.fullName}${p.birthDate ? ` · ${formatDate(p.birthDate)}` : ''}` }))} /></Form.Item>}
      </div>
      {!values?.patientId && <div className="patient-form-grid">
        <Form.Item name="attendeeName" label="Họ tên người khám" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Họ tên người khám.' },
          { max: 100, message: 'Họ tên người khám không được vượt quá 100 ký tự.' }]}><Input maxLength={100} /></Form.Item>
        <Form.Item name="attendeeBirthDate" label="Ngày sinh (nếu biết)"><Input type="date" max={clinicToday()} /></Form.Item>
      </div>}
      <Typography.Title level={5}>Dịch vụ và tài nguyên</Typography.Title>
      <div className="patient-form-grid">
        <Form.Item name="serviceId" label="Dịch vụ" rules={[{ required: true, message: 'Vui lòng nhập Dịch vụ.' }]}>
          <Select options={branchServices.map(s => ({ value: s.id, label: `${s.name} · ${s.publicDurationMinutes} phút` }))}
            onChange={id => form.setFieldValue('duration', branchServices.find(s => s.id === id)?.publicDurationMinutes)} /></Form.Item>
        <Form.Item name="reason" label="Lý do khám (tùy chọn)" rules={[{ max: 1000, message: 'Lý do không được vượt quá 1000 ký tự.' }]}><Input maxLength={1000} /></Form.Item>
        <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Vui lòng nhập Chi nhánh.' }]}>
          <Select options={options?.branches.map(b => ({ value: b.id, label: b.name }))} onChange={() => form.setFieldsValue({ dentistId: undefined, chairId: undefined, serviceId: undefined })} /></Form.Item>
        <Form.Item name="dentistId" label="Nha sĩ" rules={[{ required: true, message: 'Vui lòng nhập Nha sĩ.' }]}>
          <Select options={branchDentists.map(d => ({ value: d.id, label: d.name }))} /></Form.Item>
        <Form.Item name="chairId" label="Ghế (để trống: tự chọn)"><Select allowClear options={branchChairs.map(c => ({ value: c.id, label: c.name }))} /></Form.Item>
        <Form.Item name="date" label="Ngày" rules={[{ required: true, message: 'Vui lòng nhập Ngày.' }]}><Input type="date" min={clinicToday()} /></Form.Item>
        <Form.Item name="duration" label="Thời lượng (phút)" extra="1–480 phút. Mặc định theo dịch vụ; có thể điều chỉnh cho ca này." rules={[{ required: true, message: 'Vui lòng nhập Thời lượng.' },
          { validator: (_, v?: number) => !v || (Number.isInteger(v) && v >= 1 && v <= 480) ? Promise.resolve() : Promise.reject(new Error('Thời lượng không đúng định dạng.')) }]}>
          <InputNumber min={1} max={480} precision={0} step={1} style={{ width: '100%' }} /></Form.Item>
      </div>
      <Typography.Title level={5}>Khung giờ hợp lệ</Typography.Title>
      {!canQuery ? <Typography.Text type="secondary">Chọn chi nhánh, nha sĩ, dịch vụ, ngày và thời lượng để xem khung giờ.</Typography.Text>
        : loadingSlots ? <Spin /> : slotsError ? <Alert type="error" showIcon message={slotsError} action={<Button size="small" onClick={() => void loadSlots()}>Thử lại</Button>} />
        : shownSlots.length === 0 ? <Alert type="info" showIcon message="Không còn khung giờ đủ tài nguyên cho thời lượng này. Chọn ngày, nha sĩ, ghế hoặc thời lượng khác." />
        : <div className="booking-slots">{shownSlots.map(s => {
          const selected = slot?.start === s.start && slot.chairId === s.chairId;
          return <Button key={`${s.start}-${s.chairId}`} type={selected ? 'primary' : 'default'} onClick={() => setSlot(s)}>
            {formatClock(s.start)}{values?.chairId ? '' : <Typography.Text type={selected ? undefined : 'secondary'} style={{ marginLeft: 6, color: selected ? '#fff' : undefined }}>{chairName(s.chairId)}</Typography.Text>}</Button>;
        })}</div>}
      {slot && <Typography.Paragraph className="patient-section">Đã chọn {formatClock(slot.start)}–{formatClock(slot.end)} · {chairName(slot.chairId)} · kết thúc dự kiến {clock(localMinute(slot.end))}</Typography.Paragraph>}
      {needsException && <Form.Item name="exceptionReason" label="Lý do ngoại lệ (bắt đầu trong vòng 60 phút)" rules={[{ max: 500, message: 'Lý do ngoại lệ không được vượt quá 500 ký tự.' }]}>
        <Input.TextArea rows={2} maxLength={500} /></Form.Item>}
    </Form>
    {reviewing && slot && values && <>
      <Typography.Title level={5}>Xem lại trước khi xác nhận</Typography.Title>
      <Descriptions column={1} bordered size="small" items={[
        { label: 'Loại / nguồn', children: `${kindLabels[values.kind]} · ${sourceLabels[values.source]}` },
        { label: 'Người đặt', children: `${values.bookerName} · ${values.bookerContact}` },
        { label: 'Người khám', children: <>{patients.find(p => p.id === values.patientId)?.fullName ?? values.attendeeName} <Tag>{values.relation === 'Self' ? 'Bản thân' : 'Người thân'}</Tag></> },
        { label: 'Dịch vụ', children: branchServices.find(s => s.id === values.serviceId)?.name },
        { label: 'Nha sĩ / ghế', children: `${dentistName} · ${chairName(slot.chairId)}` },
        { label: 'Thời gian', children: `${formatDate(values.date)} · ${formatClock(slot.start)}–${formatClock(slot.end)} (${values.duration} phút)` },
        ...(needsException ? [{ label: 'Lý do ngoại lệ', children: values.exceptionReason }] : []),
      ]} />
      <Button className="patient-section" disabled={busy} onClick={() => setReviewing(false)}>Sửa thông tin</Button>
    </>}
  </Drawer>;
}
