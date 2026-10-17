import { Alert, Button, Card, Empty, Input, Select, Space, Spin, Steps, Typography } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';

type Branch = { id: number; name: string };
type Dentist = { id: number; branchId: number; name: string; canDentist: boolean };
type Service = { id: number; branchId: number; name: string; publicDurationMinutes: number };
type Options = { branches: Branch[]; employees: Dentist[]; services: Service[] };
type Slot = { start: string; end: string; chairId: number; dentistId: number;
  scheduleVersion: number; assistantScheduleVersion: number | null };
type BookingResult = { appointmentId: string; code: string; version: number; status: string;
  startAt: string; endAt: string; notification: { code: string; message: string } };
type ContactErrors = { bookerName?: string; contact?: string; attendeeName?: string };

const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
});
const tomorrow = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(Date.now() + 86_400_000));
const phonePattern = /^0\d{9}$/;
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function CustomerBookingPage() {
  const { state, expire, reload } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : undefined;
  const [options, setOptions] = useState<Options | null>(null);
  const [optionsBusy, setOptionsBusy] = useState(true);
  const [slots, setSlots] = useState<Slot[]>([]);
  const [slotsBusy, setSlotsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [authExpired, setAuthExpired] = useState(false);
  const [step, setStep] = useState(0);
  const [relation, setRelation] = useState<'Self' | 'Relative'>('Self');
  const [branchId, setBranchId] = useState<number | null>(null);
  const [serviceId, setServiceId] = useState<number | null>(null);
  const [dentistId, setDentistId] = useState<number | null>(null);
  const [date, setDate] = useState(tomorrow);
  const [reason, setReason] = useState('');
  const [slot, setSlot] = useState<Slot | null>(null);
  const [bookerName, setBookerName] = useState(state.status === 'authenticated' ? state.user.fullName : '');
  const [contact, setContact] = useState('');
  const [attendeeName, setAttendeeName] = useState(state.status === 'authenticated' ? state.user.fullName : '');
  const [fieldErrors, setFieldErrors] = useState<ContactErrors>({});
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<BookingResult | null>(null);
  const submittingRef = useRef(false);
  const retryRef = useRef<{ fingerprint: string; key: string } | null>(null);

  const services = useMemo(() => options?.services.filter(item => item.branchId === branchId) ?? [], [options, branchId]);
  const dentists = useMemo(() => options?.employees.filter(item => item.branchId === branchId && item.canDentist) ?? [], [options, branchId]);
  const service = services.find(item => item.id === serviceId);
  const dentist = dentists.find(item => item.id === dentistId);
  const branch = options?.branches.find(item => item.id === branchId);

  useEffect(() => {
    if (state.status === 'initializing' || state.status === 'error') return;
    let active = true;
    setOptionsBusy(true);
    customerApi<Options>('/api/availability/options', {}, token)
      .then(value => { if (active) setOptions(value); })
      .catch(failure => { if (active) setError((failure as Error).message); })
      .finally(() => { if (active) setOptionsBusy(false); });
    return () => { active = false; };
  }, [state.status, token]);

  useEffect(() => {
    setSlots([]); setSlot(null); retryRef.current = null;
    if (!branchId || !dentistId || !service || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return;
    let active = true;
    setSlotsBusy(true); setError(null);
    const query = new URLSearchParams({ branchId: String(branchId), dentistId: String(dentistId),
      serviceId: String(service.id), date, durationMinutes: String(service.publicDurationMinutes) });
    customerApi<{ slots: Slot[] }>(`/api/availability?${query}`, {}, token)
      .then(value => { if (active) setSlots(value.slots); })
      .catch(failure => { if (active) setError((failure as Error).message); })
      .finally(() => { if (active) setSlotsBusy(false); });
    return () => { active = false; };
  }, [branchId, dentistId, serviceId, date, service?.publicDurationMinutes, token]);

  function validateContact() {
    const next: ContactErrors = {};
    if (!bookerName.trim() || bookerName.trim().length > 100) next.bookerName = 'Nhập họ tên người đặt (1–100 ký tự).';
    if (!phonePattern.test(contact.trim()) && !emailPattern.test(contact.trim()))
      next.contact = 'Nhập số điện thoại Việt Nam hoặc email hợp lệ.';
    if (!attendeeName.trim() || attendeeName.trim().length > 100) next.attendeeName = 'Nhập họ tên người khám (1–100 ký tự).';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  }

  async function confirm() {
    if (!slot || !service || !branchId || !dentistId || submittingRef.current || authExpired || !validateContact()) return;
    submittingRef.current = true; setSubmitting(true); setError(null);
    const cleanContact = contact.trim();
    const payload = {
      branchId, booker: { name: bookerName.trim(), phone: phonePattern.test(cleanContact) ? cleanContact : null,
        email: phonePattern.test(cleanContact) ? null : cleanContact },
      attendee: { patientId: null, name: attendeeName.trim(), birthDate: null },
      attendeeRelation: relation, kind: 'Standard', source: 'App', serviceId: service.id, reason: reason.trim() || null,
      dentistId, assistantId: null, chairId: null, startAt: slot.start,
      durationMinutes: service.publicDurationMinutes, followUpId: null, exceptionReason: null,
      scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: null,
    };
    const fingerprint = JSON.stringify(payload);
    const key = retryRef.current?.fingerprint === fingerprint ? retryRef.current.key : crypto.randomUUID();
    retryRef.current = { fingerprint, key };
    try {
      const value = await customerApi<BookingResult>('/api/appointments', {
        method: 'POST', body: JSON.stringify({ ...payload, requestKey: key }),
      }, token);
      setResult(value); setStep(3);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401 && token) {
        setAuthExpired(true); expire();
        setError('Phiên khách hàng đã hết hạn. Vui lòng đăng nhập lại trước khi xác nhận lịch.');
      } else {
        const code = failure instanceof ApiError ? failure.code : '';
        setError(`${code ? `${code}: ` : ''}${(failure as Error).message}`);
        if (['MSG24', 'MSG25', 'MSG26', 'MSG27', 'MSG28', 'MSG111'].includes(code)) {
          setSlot(null); setStep(0); retryRef.current = null;
          const query = new URLSearchParams({ branchId: String(branchId), dentistId: String(dentistId),
            serviceId: String(service.id), date, durationMinutes: String(service.publicDurationMinutes) });
          void customerApi<{ slots: Slot[] }>(`/api/availability?${query}`, {}, token)
            .then(value => setSlots(value.slots)).catch(() => setSlots([]));
        }
      }
    } finally { submittingRef.current = false; setSubmitting(false); }
  }

  if (state.status === 'initializing') return <main className="page"><Spin size="large" /></main>;
  if (state.status === 'error') return <main className="page"><Alert type="error" showIcon message={state.message}
    action={<Button onClick={() => void reload()}>Thử lại</Button>} /></main>;

  return <main className="customer-content customer-booking">
    <Space className="customer-back"><Link to={state.status === 'authenticated' ? '/customer/home' : '/customer/sign-in'}>← Quay lại</Link></Space>
    <Typography.Title level={2}>Đặt lịch khám</Typography.Title>
    <Steps current={step} size="small" items={[{ title: 'Chọn lịch' }, { title: 'Thông tin' }, { title: 'Xem lại' }, { title: 'Hoàn tất' }]} />
    {error && <Alert className="form-alert" type="error" showIcon message={error}
      action={authExpired ? <Link to="/customer/sign-in">Đăng nhập</Link> : undefined} />}
    {optionsBusy ? <Spin tip="Đang tải dữ liệu đặt lịch"><div className="customer-loading" /></Spin> : <Card>
      {step === 0 && <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <div><Typography.Text strong>Người khám</Typography.Text><div><Space>
          <Button type={relation === 'Self' ? 'primary' : 'default'} onClick={() => setRelation('Self')}>Bản thân</Button>
          <Button type={relation === 'Relative' ? 'primary' : 'default'} onClick={() => setRelation('Relative')}>Người thân</Button>
        </Space></div></div>
        <label>Chi nhánh<Select className="customer-field" value={branchId} placeholder="Chọn chi nhánh"
          options={options?.branches.map(item => ({ value: item.id, label: item.name }))}
          onChange={value => { setBranchId(value); setServiceId(null); setDentistId(null); }} /></label>
        {!options?.branches.length && <Empty description="Chưa có chi nhánh nhận đặt lịch" />}
        <label>Dịch vụ<Select className="customer-field" value={serviceId} placeholder="Chọn dịch vụ"
          options={services.map(item => ({ value: item.id, label: `${item.name} · ${item.publicDurationMinutes} phút` }))}
          onChange={setServiceId} /></label>
        <label>Lý do khám (không bắt buộc)<Input.TextArea value={reason} maxLength={1000} onChange={event => setReason(event.target.value)} /></label>
        <label>Bác sĩ<Select className="customer-field" value={dentistId} placeholder="Chọn bác sĩ"
          options={dentists.map(item => ({ value: item.id, label: item.name }))} onChange={setDentistId} /></label>
        <label>Ngày khám<Input className="customer-field" type="date" value={date} onChange={event => setDate(event.target.value)} /></label>
        <div><Typography.Text strong>Khung giờ · Asia/Ho_Chi_Minh</Typography.Text>
          {slotsBusy ? <div><Spin /></div> : slots.length ? <div className="customer-slots">
            {slots.map(item => <Button key={`${item.start}-${item.chairId}`} type={slot === item ? 'primary' : 'default'}
              onClick={() => { setSlot(item); retryRef.current = null; }}>{formatTime(item.start)}</Button>)}
          </div> : branchId && serviceId && dentistId ? <Empty description="Không có khung giờ phù hợp. Hãy chọn ngày hoặc bác sĩ khác." /> : null}
        </div>
        <Button type="primary" disabled={!slot} onClick={() => { setError(null); setStep(1); }}>Tiếp tục</Button>
      </Space>}
      {step === 1 && <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Typography.Title level={4}>Người đặt và người khám</Typography.Title>
        <label>Họ tên người đặt<Input value={bookerName} maxLength={100} onChange={event => setBookerName(event.target.value)}
          status={fieldErrors.bookerName ? 'error' : undefined} />{fieldErrors.bookerName && <Typography.Text type="danger">{fieldErrors.bookerName}</Typography.Text>}</label>
        <label>Số điện thoại Việt Nam hoặc email<Input value={contact} onChange={event => setContact(event.target.value)}
          status={fieldErrors.contact ? 'error' : undefined} />{fieldErrors.contact && <Typography.Text type="danger">{fieldErrors.contact}</Typography.Text>}</label>
        <label>Họ tên người khám<Input value={attendeeName} maxLength={100} onChange={event => setAttendeeName(event.target.value)}
          status={fieldErrors.attendeeName ? 'error' : undefined} />{fieldErrors.attendeeName && <Typography.Text type="danger">{fieldErrors.attendeeName}</Typography.Text>}</label>
        {relation === 'Relative' && <Alert type="info" message="Đặt lịch cho người thân không cấp quyền xem hồ sơ bệnh án của họ." />}
        <Space><Button onClick={() => setStep(0)}>Quay lại chọn lịch</Button>
          <Button type="primary" onClick={() => { if (validateContact()) { setError(null); setStep(2); } }}>Xem lại</Button></Space>
      </Space>}
      {step === 2 && <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <Typography.Title level={4}>Xem lại và xác nhận</Typography.Title>
        <Typography.Text>Chi nhánh: {branch?.name}</Typography.Text>
        <Typography.Text>Bác sĩ: {dentist?.name}</Typography.Text>
        <Typography.Text>Dịch vụ: {service?.name}</Typography.Text>
        <Typography.Text>Thời gian: {slot && formatTime(slot.start)}</Typography.Text>
        <Typography.Text>Người đặt: {bookerName} · {contact}</Typography.Text>
        <Typography.Text>Người khám: {attendeeName} ({relation === 'Self' ? 'Bản thân' : 'Người thân'})</Typography.Text>
        <Space><Button onClick={() => setStep(1)}>Sửa thông tin</Button>
          <Button type="primary" loading={submitting} disabled={authExpired} onClick={() => void confirm()}>Xác nhận đặt lịch</Button></Space>
      </Space>}
      {step === 3 && result && <Space direction="vertical" size="middle">
        <Typography.Title level={3}>Đặt lịch thành công</Typography.Title>
        <Typography.Title level={2}>{result.code}</Typography.Title>
        <Typography.Text>Trạng thái: {result.status}</Typography.Text>
        <Typography.Text>Thời gian: {formatTime(result.startAt)} – {formatTime(result.endAt)}</Typography.Text>
        <Typography.Text>Hãy lưu mã lịch hẹn để liên hệ phòng khám khi cần.</Typography.Text>
      </Space>}
    </Card>}
  </main>;
}
