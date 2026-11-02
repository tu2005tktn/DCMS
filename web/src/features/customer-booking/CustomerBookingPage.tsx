import { Alert, Button, Card, Empty, Input, Select, Spin, Steps, Typography } from 'antd';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import FamilyMemberPicker from '../family-members/FamilyMemberPicker';
import { clinicToday, useFamilyApi, validBirthDate, type FamilyMember } from '../family-members/types';
import '../family-members/familyMembers.css';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';

type Branch = { id: number; name: string };
type Dentist = { id: number; branchId: number; name: string; canDentist: boolean };
type Service = { id: number; branchId: number; name: string; publicDurationMinutes: number };
type Options = { branches: Branch[]; employees: Dentist[]; services: Service[] };
type Slot = { start: string; end: string; chairId: number; dentistId: number;
  scheduleVersion: number; assistantScheduleVersion: number | null; bookingSettingsVersion?: number };
type BookingResult = { appointmentId: string; code: string; version: number; status: string;
  startAt: string; endAt: string; notification: { code: string; message: string } };
type ContactErrors = { bookerName?: string; contact?: string; attendeeName?: string; attendeeBirthDate?: string };

const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
});
const formatClock = (value: string) => new Date(value).toLocaleTimeString('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
});
const formatBookingDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value)
  ? `${value.slice(8, 10)}/${value.slice(5, 7)}/${value.slice(0, 4)}` : value;
const tomorrow = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh',
  year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(Date.now() + 86_400_000));
const phonePattern = /^0\d{9}$/;
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const stepNames = ['Người khám', 'Chọn lịch', 'Liên hệ', 'Xem lại', 'Hoàn tất'];

export default function CustomerBookingPage() {
  const { state } = useCustomerAuth();
  return <BookingForm key={state.status === 'authenticated' ? state.user.id : 'anonymous'} />;
}
function BookingForm() {
  const [params] = useSearchParams();
  const familyRequest = useFamilyApi();
  const [selectedMember, setSelectedMember] = useState<FamilyMember | null>(null);
  const [familyError, setFamilyError] = useState('');
  const [familyLoading, setFamilyLoading] = useState(false);
  const [familyRevision, setFamilyRevision] = useState(0);
  const familySelection = useRef(0);
  const [attendeeBirthDate, setAttendeeBirthDate] = useState('');
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
  const prefilledAccount = useRef<number | null>(null);
  const account = state.status === 'authenticated' ? state.user : null;
  const linkedMemberId = params.get('familyMemberId');
  useEffect(() => {
    if (!linkedMemberId || !account?.id) return;
    let active = true; const controller = new AbortController(); const selection = ++familySelection.current;
    setRelation('Relative'); setFamilyLoading(true); setFamilyError('');
    setSelectedMember(null); setAttendeeName(''); setAttendeeBirthDate('');
    void familyRequest<FamilyMember>(`/${encodeURIComponent(linkedMemberId)}`, { signal: controller.signal })
      .then(member => { if (active && selection === familySelection.current) { setSelectedMember(member); setAttendeeName(member.fullName); setAttendeeBirthDate(member.birthDate ?? ''); } })
      .catch(failure => { if (active && selection === familySelection.current) setFamilyError(failure instanceof ApiError && failure.status === 404 ? 'Người thân đã xóa hoặc không còn khả dụng. Hãy chọn lại người thân đã lưu.' : (failure as Error).message); })
      .finally(() => { if (active && selection === familySelection.current) setFamilyLoading(false); });
    return () => { active = false; controller.abort(); };
  }, [linkedMemberId, account?.id, familyRequest, familyRevision]);
  function chooseMember(member: FamilyMember | null) {
    familySelection.current++; setFamilyLoading(false); setFamilyError('');
    setSelectedMember(member); setAttendeeName(member?.fullName ?? ''); setAttendeeBirthDate(member?.birthDate ?? '');
    setFieldErrors(previous => ({ ...previous, attendeeName: undefined, attendeeBirthDate: undefined }));
  }
  useEffect(() => {
    if (!account || prefilledAccount.current === account.id) return;
    const changedAccount = prefilledAccount.current !== null;
    prefilledAccount.current = account.id;
    const accountContact = account.phoneNumber || account.email || '';
    setBookerName(value => changedAccount ? account.fullName : value || account.fullName);
    setContact(value => changedAccount ? accountContact : value || accountContact);
    setAttendeeName(value => relation === 'Self' ? changedAccount ? account.fullName : value || account.fullName : changedAccount ? '' : value);
  }, [account?.id, account?.fullName, account?.phoneNumber, account?.email, relation]);

  const services = useMemo(() => options?.services.filter(item => item.branchId === branchId) ?? [], [options, branchId]);
  const dentists = useMemo(() => options?.employees.filter(item => item.branchId === branchId && item.canDentist) ?? [], [options, branchId]);
  const service = services[0];
  const serviceId = service?.id ?? null;
  const dentist = dentists.find(item => item.id === dentistId);
  const branch = options?.branches.find(item => item.id === branchId);
  const slotGroups = useMemo(() => [
    { label: 'Buổi sáng', items: slots.filter(item => Number(formatClock(item.start).slice(0, 2)) < 12) },
    { label: 'Buổi chiều', items: slots.filter(item => Number(formatClock(item.start).slice(0, 2)) >= 12) },
  ].filter(group => group.items.length > 0), [slots]);

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
    if (relation === 'Relative' && !validBirthDate(attendeeBirthDate)) next.attendeeBirthDate = 'Ngày sinh không hợp lệ hoặc sau ngày hiện tại.';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  }

  async function confirm() {
    if (!token || !slot || !service || !branchId || !dentistId || submittingRef.current || authExpired || !validateContact()) return;
    submittingRef.current = true; setSubmitting(true); setError(null);
    const cleanContact = contact.trim();
    const payload = {
      branchId, booker: { name: bookerName.trim(), phone: phonePattern.test(cleanContact) ? cleanContact : null,
        email: phonePattern.test(cleanContact) ? null : cleanContact },
      attendee: { patientId: null, name: attendeeName.trim(), birthDate: relation === 'Relative' ? attendeeBirthDate || null : null },
      attendeeRelation: relation, kind: 'Standard', source: 'App', serviceId: service.id, reason: reason.trim() || null,
      dentistId, assistantId: null, chairId: null, startAt: slot.start,
      durationMinutes: service.publicDurationMinutes, followUpId: null, exceptionReason: null, bookingSettingsVersion: slot.bookingSettingsVersion,
      scheduleVersion: slot.scheduleVersion, assistantScheduleVersion: null,
    };
    const fingerprint = JSON.stringify(payload);
    const key = retryRef.current?.fingerprint === fingerprint ? retryRef.current.key : crypto.randomUUID();
    retryRef.current = { fingerprint, key };
    try {
      const value = await customerApi<BookingResult>('/api/appointments', {
        method: 'POST', body: JSON.stringify({ ...payload, requestKey: key }),
      }, token);
      setResult(value); setStep(4);
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401 && token) {
        setAuthExpired(true); expire();
        setError('Phiên khách hàng đã hết hạn. Vui lòng đăng nhập lại trước khi xác nhận lịch.');
      } else {
        const code = failure instanceof ApiError ? failure.code : '';
        setError(`${code ? `${code}: ` : ''}${(failure as Error).message}`);
        if (['MSG24', 'MSG25', 'MSG26', 'MSG27', 'MSG28', 'MSG111'].includes(code)) {
          setSlot(null); setStep(1); retryRef.current = null;
          const query = new URLSearchParams({ branchId: String(branchId), dentistId: String(dentistId),
            serviceId: String(service.id), date, durationMinutes: String(service.publicDurationMinutes) });
          void customerApi<{ slots: Slot[] }>(`/api/availability?${query}`, {}, token)
            .then(value => setSlots(value.slots)).catch(() => setSlots([]));
        }
      }
    } finally { submittingRef.current = false; setSubmitting(false); }
  }

  if (state.status === 'initializing') return <main className="customer-portal page"><Spin size="large" /></main>;
  if (state.status === 'error') return <main className="customer-portal page"><Alert type="error" showIcon message={state.message}
    action={<Button onClick={() => void reload()}>Thử lại</Button>} /></main>;

  return <main className="customer-portal"><div className="customer-content customer-booking">
    <div className="customer-booking-header"><Link to={state.status === 'authenticated' ? '/customer/home' : '/customer/sign-in'} className="customer-back">← Quay lại</Link>
      <span className="customer-kicker">Đặt lịch trực tuyến</span><h1>Đặt lịch khám</h1>
      <Alert type="info" showIcon message="Khám lần đầu hoặc đợt khám mới"
        description="Dịch vụ cho lần khám này là Khám. Nếu đang tiếp tục một phác đồ điều trị, hãy liên hệ phòng khám để đặt buổi theo chỉ định của nha sĩ." />
      <p>Chọn khung giờ phù hợp. Phòng khám sẽ xác nhận lịch ngay khi đặt thành công.</p></div>
    <div className="customer-booking-progress"><span>Bước {step + 1}/5</span><strong>{stepNames[step]}</strong></div>
    <Steps current={step} size="small" items={stepNames.map(title => ({ title }))} />
    {error && <Alert className="form-alert" type="error" showIcon message={error}
      action={authExpired ? <Link to="/customer/sign-in">Đăng nhập</Link> : undefined} />}
    {optionsBusy ? <Spin tip="Đang tải dữ liệu đặt lịch"><div className="customer-loading" /></Spin> : <Card className="customer-booking-card">
      {step === 0 && <div className="customer-booking-section">
        <h2>Bạn đặt lịch cho ai?</h2><p>Chúng tôi sẽ dùng lựa chọn này để hiển thị đúng thông tin người khám.</p>
        <div className="customer-relation-options" role="group" aria-label="Người khám">
          <Button className="customer-relation-option" aria-pressed={relation === 'Self'} onClick={() => {
            if (relation !== 'Self') { familySelection.current++; setFamilyLoading(false); setFamilyError(''); setRelation('Self'); setSelectedMember(null); setAttendeeBirthDate(''); if (account) setAttendeeName(account.fullName); }
          }}><strong>Bản thân</strong><span>Tôi là người đến khám</span></Button>
          <Button className="customer-relation-option" aria-pressed={relation === 'Relative'} onClick={() => {
            if (relation !== 'Relative') { setRelation('Relative'); chooseMember(null); }
          }}><strong>Người thân</strong><span>Tôi đặt lịch cho một người khác</span></Button>
        </div>
        {relation === 'Relative' && <Alert type="info" showIcon message="Đặt lịch cho người thân không cấp quyền xem hồ sơ bệnh án của họ." />}
        {relation === 'Relative' && <>
          {familyLoading && <p role="status">Đang tải người thân đã chọn…</p>}
          {familyError && <Alert type="error" showIcon message={familyError} action={<Button onClick={() => setFamilyRevision(r => r + 1)}>Thử lại</Button>} />}
          <FamilyMemberPicker selected={selectedMember} onChange={chooseMember} />
        </>}
        <div className="customer-booking-actions"><Button type="primary" disabled={familyLoading || (relation === 'Relative' && !selectedMember)} onClick={() => setStep(1)}>Tiếp tục</Button></div>
      </div>}
      {step === 1 && <div className="customer-booking-section">
        <h2>Chọn nơi khám và khung giờ</h2>
        <p>Chọn nơi khám và ngày phù hợp để xem các giờ còn trống.</p>
        <div className="customer-schedule-layout">
          <section className="customer-schedule-filters" aria-label="Thông tin lịch khám">
            <div className="customer-booking-fields">
              <div><label htmlFor="booking-branch">Chi nhánh</label><Select id="booking-branch" className="customer-field" value={branchId} placeholder="Chọn chi nhánh"
                options={options?.branches.map(item => ({ value: item.id, label: item.name }))}
                onChange={value => { setBranchId(value); setDentistId(null); }} /></div>
              <div><label htmlFor="booking-service">Dịch vụ</label><Input id="booking-service" readOnly className="customer-field"
                value={service ? `Khám · ${service.publicDurationMinutes} phút` : 'Chưa có dịch vụ Khám khả dụng tại chi nhánh này'} /></div>
              <div><label htmlFor="booking-dentist">Bác sĩ</label><Select id="booking-dentist" className="customer-field" value={dentistId} placeholder="Chọn bác sĩ"
                options={dentists.map(item => ({ value: item.id, label: item.name }))} onChange={setDentistId} /></div>
              <div><label htmlFor="booking-date">Ngày khám</label><Input id="booking-date" className="customer-field" type="date" value={date} onChange={event => setDate(event.target.value)} /></div>
            </div>
            {!options?.branches.length && <Empty description="Chưa có chi nhánh nhận đặt lịch" />}
          </section>
          <section className="customer-schedule-times" aria-labelledby="customer-schedule-times-title">
            <div className="customer-schedule-times-heading"><h3 id="customer-schedule-times-title">Chọn khung giờ khám</h3>
              <span>Giờ Việt Nam</span></div>
            <div aria-live="polite" className="customer-schedule-times-body">
              {slotsBusy ? <div><Spin tip="Đang tìm khung giờ"><div className="customer-loading" /></Spin></div> : slotGroups.length ? slotGroups.map(group =>
                <section className="customer-slot-group" key={group.label}><h4>{group.label}</h4>
                  <div className="customer-slots" role="group" aria-label={`Khung giờ ${group.label.toLowerCase()}`}>
                    {group.items.map(item => <Button key={`${item.start}-${item.chairId}`} type={slot === item ? 'primary' : 'default'}
                      aria-pressed={slot === item} onClick={() => { setSlot(item); retryRef.current = null; }}>{formatClock(item.start)}–{formatClock(item.end)}</Button>)}
                  </div></section>) : branchId && serviceId && dentistId ?
                  <Empty description="Không có khung giờ phù hợp. Hãy chọn ngày hoặc bác sĩ khác." /> :
                  <p>Chọn chi nhánh, bác sĩ và ngày để xem khung giờ khám.</p>}
            </div>
          </section>
          <aside className="customer-schedule-summary" aria-label="Lịch đã chọn">
            <h3>Lịch đã chọn</h3>
            <dl>
              <div><dt>Chi nhánh</dt><dd>{branch?.name ?? 'Chưa chọn'}</dd></div>
              <div><dt>Dịch vụ</dt><dd>{service ? `${service.name} · ${service.publicDurationMinutes} phút` : 'Chưa chọn'}</dd></div>
              <div><dt>Bác sĩ</dt><dd>{dentist?.name ?? 'Chưa chọn'}</dd></div>
              <div><dt>Ngày khám</dt><dd>{formatBookingDate(date)}</dd></div>
              <div className="customer-schedule-summary-time"><dt>Giờ khám</dt><dd>{slot ? formatClock(slot.start) : 'Chưa chọn giờ'}</dd></div>
            </dl>
            <p>Thông tin này chưa phải lịch hẹn đã xác nhận.</p>
          </aside>
        </div>
        <details className="customer-booking-reason">
          <summary>{reason ? 'Lý do khám đã nhập (có thể sửa)' : 'Thêm lý do khám (không bắt buộc)'}</summary>
          <label htmlFor="booking-reason">Lý do khám</label>
          <Input.TextArea id="booking-reason" value={reason} maxLength={1000} onChange={event => setReason(event.target.value)} />
        </details>
        <div className="customer-booking-actions"><Button onClick={() => setStep(0)}>Quay lại</Button>
          <Button type="primary" disabled={!slot} onClick={() => { setError(null); setStep(2); }}>Tiếp tục</Button></div>
      </div>}
      {step === 2 && <div className="customer-booking-section">
        <h2>Thông tin người đặt và người khám</h2><p>Phòng khám dùng thông tin liên hệ để trao đổi về lịch hẹn.</p>
        {slot && <div className="customer-booking-selection"><span>Lịch đang đặt</span><strong>{formatTime(slot.start)}</strong>
          <span>{branch?.name} · {dentist?.name}</span></div>}
        <div className="customer-booking-contact-grid">
          <fieldset className="customer-booking-person"><legend>Người đặt lịch</legend><p>Người phòng khám sẽ liên hệ khi cần.</p>
            <div className="customer-booking-fields">
              <label>Họ tên người đặt<Input value={bookerName} maxLength={100} onChange={event => setBookerName(event.target.value)}
                status={fieldErrors.bookerName ? 'error' : undefined} />{fieldErrors.bookerName && <Typography.Text type="danger">{fieldErrors.bookerName}</Typography.Text>}</label>
              <label>Số điện thoại Việt Nam hoặc email<Input value={contact} onChange={event => setContact(event.target.value)}
                status={fieldErrors.contact ? 'error' : undefined} />{fieldErrors.contact && <Typography.Text type="danger">{fieldErrors.contact}</Typography.Text>}</label>
            </div></fieldset>
          <fieldset className="customer-booking-person"><legend>Người đến khám</legend><p>{relation === 'Self' ? 'Thông tin của bạn.' : 'Thông tin người thân sẽ đến khám.'}</p>
            <label>Họ tên người khám<Input value={attendeeName} maxLength={100} onChange={event => setAttendeeName(event.target.value)}
              status={fieldErrors.attendeeName ? 'error' : undefined} />{fieldErrors.attendeeName && <Typography.Text type="danger">{fieldErrors.attendeeName}</Typography.Text>}</label>
            {relation === 'Relative' && <label>Ngày sinh người khám (không bắt buộc)<Input id="booking-attendee-birth" type="date" max={clinicToday()} value={attendeeBirthDate}
              onChange={e => { setAttendeeBirthDate(e.target.value); setFieldErrors(previous => ({ ...previous, attendeeBirthDate: undefined })); }}
              aria-invalid={!!fieldErrors.attendeeBirthDate} status={fieldErrors.attendeeBirthDate ? 'error' : undefined}
              aria-describedby={fieldErrors.attendeeBirthDate ? 'booking-attendee-birth-error' : undefined} />
              {fieldErrors.attendeeBirthDate && <span className="customer-field-error" id="booking-attendee-birth-error">{fieldErrors.attendeeBirthDate}</span>}</label>}
          </fieldset>
        </div>
        {relation === 'Relative' && <Alert type="info" showIcon message="Đặt lịch cho người thân không cấp quyền xem hồ sơ bệnh án của họ." />}
        <div className="customer-booking-actions"><Button onClick={() => setStep(1)}>Quay lại chọn lịch</Button>
          <Button type="primary" onClick={() => { if (validateContact()) { setError(null); setStep(3); } }}>Xem lại</Button></div>
      </div>}
      {step === 3 && <div className="customer-booking-section">
        <h2>Xem lại và xác nhận</h2><p>Kiểm tra thông tin trước khi gửi yêu cầu đặt lịch.</p>
        <h3 className="customer-review-heading">Lịch khám</h3>
        <dl className="customer-review-grid">
          <div className="customer-review-item"><dt>Chi nhánh</dt><dd>{branch?.name}</dd></div>
          <div className="customer-review-item"><dt>Dịch vụ</dt><dd>{service?.name}</dd></div>
          <div className="customer-review-item"><dt>Bác sĩ</dt><dd>{dentist?.name}</dd></div>
          <div className="customer-review-item"><dt>Thời gian</dt><dd>{slot && formatTime(slot.start)}</dd></div>
        </dl>
        <h3 className="customer-review-heading">Người đặt và người khám</h3>
        <dl className="customer-review-grid">
          <div className="customer-review-item"><dt>Người đặt</dt><dd>{bookerName} · {contact}</dd></div>
          <div className="customer-review-item"><dt>Người khám</dt><dd>{attendeeName} ({relation === 'Self' ? 'Bản thân' : 'Người thân'})</dd></div>
          {relation === 'Relative' && attendeeBirthDate && <div className="customer-review-item"><dt>Ngày sinh người khám</dt><dd>{formatBookingDate(attendeeBirthDate)}</dd></div>}
        </dl>
        <div className="customer-booking-actions"><Button onClick={() => setStep(2)}>Sửa thông tin</Button>
          <Button type="primary" loading={submitting} disabled={authExpired} onClick={() => void confirm()}>Xác nhận đặt lịch</Button></div>
      </div>}
      {step === 4 && result && <div className="customer-confirmation" role="status">
        <span className="customer-confirmation-mark" aria-hidden="true">✓</span>
        <h2>Đặt lịch thành công</h2><p>Lịch khám của bạn đã được xác nhận. Hãy lưu mã này để tiện liên hệ phòng khám.</p>
        <div className="customer-confirmation-code">{result.code}</div>
        <div className="customer-confirmation-details"><p>Trạng thái: <strong>{result.status === 'Confirmed' ? 'Đã xác nhận' : result.status}</strong></p>
          <p>Thời gian: <strong>{formatTime(result.startAt)} – {formatTime(result.endAt)}</strong></p></div>
        <div className="customer-confirmation-next">
          <Link className="customer-link-button customer-link-button-primary" to={`/customer/appointments/${encodeURIComponent(result.code)}`}>Xem và quản lý lịch hẹn</Link>
          <Link to="/customer/home">Về trang chủ</Link>
        </div>
      </div>}
    </Card>}
  </div></main>;
}
