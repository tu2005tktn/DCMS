import { Alert, Button, Descriptions, Drawer, Form, Input, InputNumber, Radio, Select, Space, Spin, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import type { FollowUpHandoff } from '../follow-ups/types';
import {
  clinicToday, clock, formatClock, formatDate, kindLabels, localMinute, newRequestKey, sourceLabels,
  type AppointmentListItem, type AppointmentOptions, type AppointmentResult, type AvailabilitySlot,
} from './types';
import { useCanPickBranch } from '../authentication/useCanPickBranch';

export type BookingPrefill = { branchId: number; dentistId?: number; date: string; minute?: number;
  kind?: 'Standard' | 'WalkIn'; source?: string; serviceId?: number; durationMinutes?: number };
type Values = {
  kind: 'Standard' | 'WalkIn' | 'FollowUp'; source: string; bookerName: string; bookerContact: string; patientId?: number;
  attendeeName?: string; attendeeBirthDate?: string; relation: 'Self' | 'Relative'; serviceId?: number; reason?: string;
  branchId: number; dentistId?: number; chairId?: number; date: string; duration: number; exceptionReason?: string;
};
type PatientOption = { id: number; patientCode: string; fullName: string; birthDate: string | null; phone: string | null };
// docs/specs/CHAIR-CAPACITY-ANY-DENTIST-BOOKING.vi.md: 0 = any on-shift dentist with a free chair.
const ANY_DENTIST = 0;
const slotChangedCodes = ['MSG25', 'MSG27', 'MSG28', 'MSG34'];

// S11 counter booking (UC-04). Slots come from Tư's availability for the exact staff duration; the booking itself is
// Vinh's shared POST /api/appointments with the staff session. Contract: docs/implementation/UC04-05-S10-S11.md §5.
// `followUp` (S24, docs/specs/S24-FOLLOW-UP-INSTRUCTION-WEB.md): mở từ chỉ định quay lại — kind FollowUp + followUpId
// theo contract POST /api/appointments, patient/nha sĩ/thời lượng/cửa sổ khóa theo chỉ định, không tự chọn slot.
export default function BookingDrawer({ open, prefill, options, followUp, replacement, onClose, onBooked }: {
  open: boolean; prefill: BookingPrefill | null; options: AppointmentOptions | null;
  followUp?: FollowUpHandoff | null;
  replacement?: AppointmentListItem | null;
  onClose: () => void; onBooked: (result: AppointmentResult) => void;
}) {
  const auth = useAuth();
  const canPickBranch = useCanPickBranch();
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
  const requestPayload = useRef<string | null>(null);
  const slotRevision = useRef(0);
  const wantedMinute = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (!open || !prefill) return;
    form.resetFields();
    if (followUp) {
      form.setFieldsValue({ kind: 'FollowUp', source: 'Phone', relation: 'Self', branchId: followUp.branchId,
        dentistId: followUp.dentistId ?? undefined, date: prefill.date, duration: followUp.durationMinutes,
        patientId: followUp.patientId, bookerName: auth.user?.fullName ?? '' });
    } else {
      form.setFieldsValue({ kind: prefill.kind ?? 'Standard', source: prefill.source ?? 'Phone', relation: 'Self',
        branchId: prefill.branchId, dentistId: prefill.dentistId, date: prefill.date, serviceId: prefill.serviceId, duration: prefill.durationMinutes ?? 30,
        exceptionReason: replacement ? `Đặt thay khung giờ của lịch khách không tới ${replacement.code}.` : undefined });
    }
    wantedMinute.current = prefill.minute;
    setSlots(null); setSlot(null); setReviewing(false); setError(null); setSlotsError(null); requestKey.current = null; requestPayload.current = null;
  }, [open, prefill, followUp, replacement]);

  const branchId = values?.branchId;
  const branchDentists = options?.dentists.filter(x => x.branchId === branchId) ?? [];
  const branchChairs = options?.chairs.filter(x => x.branchId === branchId) ?? [];
  const branchServices = options?.services.filter(x => x.branchId === branchId) ?? [];
  // Ở chế độ tái khám các field kind/relation/duration/patientId không có Form.Item (hiển thị dạng khóa),
  // nên giá trị hiệu lực lấy từ bối cảnh chỉ định thay vì form watch.
  const effectiveKind = followUp ? 'FollowUp' as const : values?.kind;
  const effectiveDuration = followUp ? followUp.durationMinutes : values?.duration;
  // main (CLINIC-SLOTS-AND-SERVICE-DURATIONS.vi.md §27/§33) thay quy tắc 15–480 bội 15 bằng số phút nguyên 1–480;
  // server dùng chung ScheduleRules.ValidDuration nên client không được lệch về phía chặt hơn.
  const durationValid = !!effectiveDuration && Number.isInteger(effectiveDuration) && effectiveDuration >= 1 && effectiveDuration <= 480;
  const windowExpired = !!followUp && followUp.latestOn < clinicToday();
  const outsideWindow = !!followUp && !!values?.date && (values.date < followUp.earliestOn || values.date > followUp.latestOn);
  const canQuery = !!values?.branchId && values.dentistId != null && !!values.serviceId && !!values.date && durationValid
    && !windowExpired && !outsideWindow;
  const needsException = !!slot && Date.parse(slot.start) < Date.now() + 60 * 60_000;
  const shownSlots = (slots ?? []).filter(s => !values?.chairId || s.chairId === values.chairId);

  // Any change to the resource selection invalidates the chosen slot; the server rechecks everything on confirm anyway.
  async function loadSlots() {
    const revision = ++slotRevision.current;
    if (!canQuery || !values) return;
    setLoadingSlots(true); setSlotsError(null); setSlot(null); setReviewing(false);
    try {
      const query = new URLSearchParams({ branchId: String(values.branchId), dentistId: String(values.dentistId),
        serviceId: String(values.serviceId), date: values.date, durationMinutes: String(effectiveDuration) });
      const result = await api<{ slots: AvailabilitySlot[] }>(`/api/availability?${query}`);
      if (revision !== slotRevision.current) return;
      const available = replacement ? result.slots.filter(value => Date.parse(value.start) > Date.now() && Date.parse(value.end) <= Date.parse(replacement.endAt)) : result.slots;
      setSlots(available);
      const wanted = wantedMinute.current;
      if (wanted !== undefined) {
        setSlot(available.find(s => localMinute(s.start) === wanted && (!values.chairId || s.chairId === values.chairId)) ?? null);
        wantedMinute.current = undefined;
      }
    } catch (failure) { if (revision === slotRevision.current) { setSlots(null); setSlotsError((failure as Error).message); } }
    finally { if (revision === slotRevision.current) setLoadingSlots(false); }
  }

  useEffect(() => {
    if (open && canQuery) void loadSlots(); else { setSlots(null); setSlot(null); setLoadingSlots(false); }
    return () => { slotRevision.current++; };
  },
    [open, values?.branchId, values?.dentistId, values?.serviceId, values?.date, effectiveDuration]);

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
    setBusy(true); setError(null);
    const contact = values.bookerContact.trim();
    const patient = patients.find(p => p.id === values.patientId);
    try {
      const payload = {
        branchId: values.branchId,
        booker: { name: values.bookerName.trim(), phone: contact.includes('@') ? null : contact, email: contact.includes('@') ? contact : null },
        attendee: followUp
          ? { patientId: followUp.patientId, name: followUp.patientLabel, birthDate: null }
          : { patientId: values.patientId ?? null, name: patient?.fullName ?? values.attendeeName?.trim() ?? null,
            birthDate: values.patientId ? null : values.attendeeBirthDate || null },
        attendeeRelation: followUp ? 'Self' : values.relation, kind: effectiveKind,
        source: values.source, serviceId: values.serviceId ?? null,
        reason: values.reason?.trim() || null, dentistId: slot.dentistId, assistantId: null, chairId: slot.chairId,
        startAt: slot.start, durationMinutes: effectiveDuration,
        followUpId: followUp?.instructionId ?? null, bookingSettingsVersion: slot.bookingSettingsVersion,
        ...(replacement ? { replacesAppointmentId: replacement.id } : {}),
        exceptionReason: needsException ? values.exceptionReason?.trim() : null,
        scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: slot.assistantScheduleVersion,
      };
      const fingerprint = JSON.stringify(payload);
      if (requestPayload.current !== fingerprint || !requestKey.current) { requestPayload.current = fingerprint; requestKey.current = newRequestKey(); }
      const result = await api<AppointmentResult>('/api/appointments', { method: 'POST', body: JSON.stringify({ ...payload, requestKey: requestKey.current }) });
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

  const anyDentist = values?.dentistId === ANY_DENTIST;
  const dentistLabel = (id: number) => branchDentists.find(x => x.id === id)?.name ?? `Nha sĩ ${id}`;
  const dentistName = slot ? dentistLabel(slot.dentistId) : branchDentists.find(x => x.id === values?.dentistId)?.name;
  const chairName = (id: number) => branchChairs.find(x => x.id === id)?.name ?? `Ghế ${id}`;

  return <Drawer open={open} onClose={() => { if (!busy) onClose(); }} width={640} styles={{ wrapper: { maxWidth: '100vw' } }} destroyOnClose
    title={replacement ? 'Đặt lịch thay thế' : 'Đặt lịch tại quầy (S11)'} aria-label={replacement ? 'Đặt lịch thay thế' : 'Đặt lịch tại quầy (S11)'}
    extra={<Space><Button onClick={onClose} disabled={busy}>Hủy</Button>
      {reviewing ? <Button type="primary" loading={busy} onClick={() => void confirm()}>Xác nhận đặt lịch</Button>
        : <Button type="primary" disabled={!slot} onClick={() => void review()}>Xem lại</Button>}</Space>}>
    {replacement && <Alert className="form-alert" type="info" showIcon message={`Lịch ${replacement.code}: khách không tới. Nhập thông tin khách mới; thời gian phải kết thúc trước ${formatClock(replacement.endAt)}.`} />}
    {error && <Alert className="form-alert" type="error" showIcon message={error.message}
      description={slotChangedCodes.includes(error.code) ? 'Khung giờ đã thay đổi. Danh sách khung giờ đã được tải lại; thông tin đã nhập được giữ nguyên.'
        : error.code === 'MSG230' ? 'Chỉ định bị chặn (cửa sổ, điều kiện bước hoặc kết quả Labo) — xem lại chỉ định S24 trước khi đặt lịch; dữ liệu được giữ nguyên.'
        : error.code === 'MSG127' ? 'Chỉ định đã gắn với lịch hẹn khác — tải lại danh sách chỉ định trong S24.'
        : error.code === 'MSG06' ? 'Phiên bản không còn quyền với chỉ định hoặc bệnh nhân này — dữ liệu được giữ nguyên.'
        : undefined} />}
    <Form<Values> form={form} layout="vertical" disabled={reviewing || busy} style={{ display: reviewing ? 'none' : undefined }}>      <div className="patient-form-grid">
        {followUp ? <div><Typography.Text type="secondary">Loại lịch</Typography.Text>
          <div><Tag color="purple">{kindLabels.FollowUp}</Tag> <Typography.Text type="secondary">
            Theo chỉ định {followUp.instructionId.slice(0, 8)} · v{followUp.version}</Typography.Text></div></div>
          : <Form.Item name="kind" label="Loại lịch"><Radio.Group disabled={!!replacement} optionType="button" options={[
            { value: 'Standard', label: kindLabels.Standard }, { value: 'WalkIn', label: kindLabels.WalkIn },
            { value: 'FollowUp', label: <Tooltip title="Chỉ mở từ chỉ định quay lại S24">{kindLabels.FollowUp}</Tooltip>, disabled: !followUp }]} /></Form.Item>}
        <Form.Item name="source" label="Nguồn" extra="Chỉ ghi nhận nguồn, không kết nối kênh.">
          <Select disabled={!!replacement} options={Object.entries(sourceLabels).map(([value, label]) => ({ value, label }))} />
        </Form.Item>
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
      {followUp ? <div className="patient-form-grid">
        <div><Typography.Text type="secondary">Bệnh nhân theo chỉ định</Typography.Text>
          <div><strong>{followUp.patientLabel}</strong> · Bệnh nhân #{followUp.patientId}</div></div>
        <div><Typography.Text type="secondary">Quan hệ</Typography.Text>
          <div>Bản thân (khóa theo chỉ định)</div></div>
      </div> : <>
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
      </>}
      <Typography.Title level={5}>Dịch vụ và tài nguyên</Typography.Title>
      <div className="patient-form-grid">
        <Form.Item name="serviceId" label="Dịch vụ" rules={[{ required: true, message: 'Vui lòng nhập Dịch vụ.' }]}>
          <Select disabled={!!replacement} options={branchServices.map(s => ({ value: s.id, label: `${s.name} · ${s.publicDurationMinutes} phút` }))}
            onChange={id => { if (!followUp) form.setFieldValue('duration', branchServices.find(s => s.id === id)?.publicDurationMinutes); }} /></Form.Item>
        <Form.Item name="reason" label="Lý do khám (tùy chọn)" rules={[{ max: 1000, message: 'Lý do không được vượt quá 1000 ký tự.' }]}><Input maxLength={1000} /></Form.Item>
        <Form.Item name="branchId" label="Chi nhánh" hidden={!canPickBranch || (options?.branches.length ?? 0) <= 1} rules={[{ required: true, message: 'Vui lòng nhập Chi nhánh.' }]}>
          <Select disabled={!!followUp || !!replacement} options={options?.branches.map(b => ({ value: b.id, label: b.name }))}
            onChange={() => form.setFieldsValue({ dentistId: undefined, chairId: undefined, serviceId: undefined })} />
        </Form.Item>
        <Form.Item name="dentistId" label="Nha sĩ" extra={followUp && followUp.dentistId !== null ? 'Khóa theo nha sĩ của chỉ định.' : undefined}
          rules={[{ required: true, message: 'Vui lòng nhập Nha sĩ.' }]}>
          <Select disabled={!!replacement || !!followUp && followUp.dentistId !== null} options={[{ value: ANY_DENTIST, label: "Bất kỳ nha sĩ (ghế trống)" }, ...branchDentists.map(d => ({ value: d.id, label: d.name }))]} />
        </Form.Item>
        <Form.Item name="chairId" label="Ghế (để trống: tự chọn)"><Select allowClear options={branchChairs.map(c => ({ value: c.id, label: c.name }))} /></Form.Item>
        <Form.Item name="date" label="Ngày"
          extra={followUp ? `Trong cửa sổ ${formatDate(followUp.earliestOn)} – ${formatDate(followUp.latestOn)} của chỉ định.` : undefined}
          rules={[{ required: true, message: 'Vui lòng nhập Ngày.' }]}>
          <Input type="date" disabled={!!replacement} min={followUp && clinicToday() < followUp.earliestOn ? followUp.earliestOn : clinicToday()}
            max={followUp?.latestOn} />
        </Form.Item>
        {followUp ? <div><Typography.Text type="secondary">Thời lượng</Typography.Text>
          <div>{followUp.durationMinutes} phút (khóa theo chỉ định)</div></div>
          : <Form.Item name="duration" label="Thời lượng (phút)" extra="1–480 phút. Mặc định theo dịch vụ; có thể điều chỉnh cho ca này." rules={[{ required: true, message: 'Vui lòng nhập Thời lượng.' },
            { validator: (_, v?: number) => !v || (Number.isInteger(v) && v >= 1 && v <= 480) ? Promise.resolve() : Promise.reject(new Error('Thời lượng không đúng định dạng.')) }]}>
            <InputNumber min={1} max={480} precision={0} step={1} style={{ width: '100%' }} />
          </Form.Item>}
      </div>
      <Typography.Title level={5}>Khung giờ hợp lệ</Typography.Title>
      {followUp && windowExpired && <Alert type="error" showIcon style={{ marginBottom: 8 }}
        message={`Cửa sổ chỉ định đã qua (muộn nhất ${formatDate(followUp.latestOn)}).`}
        description="Cập nhật cửa sổ trong S24 trước khi tìm khung giờ — không tự đổi nha sĩ hay cửa sổ." />}
      {followUp && !windowExpired && outsideWindow && <Alert type="warning" showIcon style={{ marginBottom: 8 }}
        message="Ngày đang chọn nằm ngoài cửa sổ của chỉ định."
        description={`Chọn ngày trong khoảng ${formatDate(followUp.earliestOn)} – ${formatDate(followUp.latestOn)}.`} />}
      {!canQuery ? <Typography.Text type="secondary">Chọn chi nhánh, nha sĩ, dịch vụ, ngày và thời lượng để xem khung giờ.</Typography.Text>
        : loadingSlots ? <Spin /> : slotsError ? <Alert type="error" showIcon message={slotsError} action={<Button size="small" onClick={() => void loadSlots()}>Thử lại</Button>} />
        : shownSlots.length === 0 ? <Alert type="info" showIcon message="Không còn khung giờ đủ tài nguyên cho thời lượng này. Chọn ngày, nha sĩ, ghế hoặc thời lượng khác."
          description={followUp ? 'Theo chỉ định, nha sĩ/thời lượng/cửa sổ không tự thay đổi — xem lại chỉ định S24 nếu cần.' : undefined} />
        : <div className="booking-slots">{shownSlots.map(s => {
          const selected = slot?.start === s.start && slot.chairId === s.chairId;
          return <Button key={`${s.start}-${s.chairId}`} type={selected ? 'primary' : 'default'} onClick={() => setSlot(s)}>
            {formatClock(s.start)}{anyDentist && <Typography.Text type={selected ? undefined : 'secondary'} style={{ marginLeft: 6, color: selected ? '#fff' : undefined }}>{dentistLabel(s.dentistId)}</Typography.Text>}{values?.chairId ? '' : <Typography.Text type={selected ? undefined : 'secondary'} style={{ marginLeft: 6, color: selected ? '#fff' : undefined }}>{chairName(s.chairId)}</Typography.Text>}</Button>;
        })}</div>}
      {slot && <Typography.Paragraph className="patient-section">Đã chọn {formatClock(slot.start)}–{formatClock(slot.end)} · {dentistLabel(slot.dentistId)} · {chairName(slot.chairId)} · kết thúc dự kiến {clock(localMinute(slot.end))}</Typography.Paragraph>}
      {needsException && <Form.Item name="exceptionReason" label="Lý do ngoại lệ (bắt đầu trong vòng 60 phút)" rules={[{ max: 500, message: 'Lý do ngoại lệ không được vượt quá 500 ký tự.' }]}>
        <Input.TextArea rows={2} maxLength={500} /></Form.Item>}
    </Form>
    {reviewing && slot && values && <>
      <Typography.Title level={5}>Xem lại trước khi xác nhận</Typography.Title>
      <Descriptions column={1} bordered size="small" items={[
        { label: 'Loại / nguồn', children: `${kindLabels[effectiveKind ?? 'Standard']} · ${sourceLabels[values.source]}` },
        { label: 'Người đặt', children: `${values.bookerName} · ${values.bookerContact}` },
        { label: 'Người khám', children: followUp
          ? <>{followUp.patientLabel} <Tag>{kindLabels.FollowUp}</Tag></>
          : <>{patients.find(p => p.id === values.patientId)?.fullName ?? values.attendeeName} <Tag>{values.relation === 'Self' ? 'Bản thân' : 'Người thân'}</Tag></> },
        ...(followUp ? [{ label: 'Chỉ định', children: `${followUp.instructionId.slice(0, 8)} · v${followUp.version} · ${followUp.durationMinutes} phút · cửa sổ ${formatDate(followUp.earliestOn)}–${formatDate(followUp.latestOn)}` }] : []),
        { label: 'Dịch vụ', children: branchServices.find(s => s.id === values.serviceId)?.name },
        { label: 'Nha sĩ / ghế', children: `${dentistName} · ${chairName(slot.chairId)}` },
        { label: 'Thời gian', children: `${formatDate(values.date)} · ${formatClock(slot.start)}–${formatClock(slot.end)} (${effectiveDuration} phút)` },
        ...(needsException ? [{ label: 'Lý do ngoại lệ', children: values.exceptionReason }] : []),
      ]} />
      <Button className="patient-section" disabled={busy} onClick={() => setReviewing(false)}>Sửa thông tin</Button>
    </>}
  </Drawer>;
}
