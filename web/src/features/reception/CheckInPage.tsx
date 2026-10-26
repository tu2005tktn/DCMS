import { Alert, Button, Checkbox, Descriptions, Form, Input, Modal, Select, Space, Spin, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import PatientFormDrawer from '../patients/PatientFormDrawer';
import type { PatientDetail, PatientListItem } from '../patients/types';
import { formatDate, newRequestKey } from '../appointments/types';
import { arrivalClock, purposeLabels, type Course, type ReceptionDetail, type VisitResult } from './types';

type Values = { patientId?: number; purpose: string; note?: string; identityConfirmed?: boolean; backdate?: boolean; arrivalAt?: string; arrivalReason?: string; priorCourseId?: string };
export default function CheckInPage() {
  const { appointmentId } = useParams(); const auth = useAuth(); const [form] = Form.useForm<Values>();
  const navigate = useNavigate(); const [modal, modalContext] = Modal.useModal(); const [dirty, setDirty] = useState(false);
  const patientId = Form.useWatch('patientId', form) as number | undefined;
  const purpose = Form.useWatch('purpose', form) as string | undefined;
  const backdate = Form.useWatch('backdate', form) as boolean | undefined;
  const [detail, setDetail] = useState<ReceptionDetail>(); const [patient, setPatient] = useState<PatientDetail>();
  const [patients, setPatients] = useState<PatientListItem[]>([]); const [courses, setCourses] = useState<Course[]>([]);
  const [error, setError] = useState<Error>(); const [searchError, setSearchError] = useState<string>(); const [patientError, setPatientError] = useState<string>();
  const [loading, setLoading] = useState(false); const [busy, setBusy] = useState(false); const [creating, setCreating] = useState(false);
  const [result, setResult] = useState<VisitResult>(); const [loadedAt, setLoadedAt] = useState(0); const [tick, setTick] = useState(Date.now());
  const request = useRef<{ payload: string; key: string } | null>(null); const searchSerial = useRef(0);
  const canWrite = auth.user?.permissions.includes('reception.check-in') ?? false;
  useEffect(() => { const timer = window.setInterval(() => setTick(Date.now()), 5000); return () => window.clearInterval(timer); }, []);
  async function load(initial = false) {
    setLoading(true); setError(undefined);
    try {
      const value = await api<ReceptionDetail>(`/api/reception/appointments/${appointmentId}`);
      setDetail(value); setLoadedAt(Date.now()); setTick(Date.now());
      form.setFieldValue('identityConfirmed', false);
      if (initial) { form.setFieldsValue({ patientId: value.appointment.patientId ?? undefined, purpose: 'InitialExamination' }); request.current = null; }
    } catch (e) { setError(e as Error); } finally { setLoading(false); }
  }
  useEffect(() => { setDetail(undefined); setResult(undefined); form.resetFields(); void load(true); }, [appointmentId]);
  useEffect(() => {
    let active = true; setPatient(undefined); setPatientError(undefined); setSearchError(undefined); setCourses([]); form.setFieldValue('identityConfirmed', false); form.setFieldValue('priorCourseId', undefined);
    if (patientId) {
      api<PatientDetail>(`/api/patients/${patientId}`).then(value => { if (active) setPatient(value); })
        .catch(e => { if (active) setPatientError((e as Error).message); });
      api<Course[]>(`/api/reception/appointments/${appointmentId}/courses?patientId=${patientId}`).then(value => { if (active) setCourses(value); })
        .catch(e => { if (active) setSearchError((e as Error).message); });
    }
    return () => { active = false; };
  }, [patientId, appointmentId]);
  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => { if (!result && !detail?.visit && dirty) e.preventDefault(); };
    window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn);
  }, [dirty, result, detail?.visit]);
  function back() {
    if (!dirty || currentResult) { navigate('/reception'); return; }
    modal.confirm({ title: 'Bỏ thông tin tiếp nhận chưa lưu?', okText: 'Bỏ thay đổi', cancelText: 'Tiếp tục nhập', onOk: () => navigate('/reception') });
  }
  async function search(keyword: string) {
    const serial = ++searchSerial.current; setSearchError(undefined);
    if (!keyword.trim()) { setPatients([]); return; }
    try { const value = await api<{ items: PatientListItem[] }>(`/api/patients?keyword=${encodeURIComponent(keyword.trim())}&pageSize=20`);
      if (serial === searchSerial.current) setPatients(value.items);
    } catch (e) { if (serial === searchSerial.current) { setPatients([]); setSearchError((e as Error).message); } }
  }
  const a = detail?.appointment; const currentResult = result ?? detail?.visit;
  const serverNow = detail ? Date.parse(detail.serverNow) + Math.max(0, tick - loadedAt) : 0;
  const currentClinicDate = new Date(serverNow + 7 * 3600000).toISOString().slice(0, 10);
  const eligible = !!a?.canCheckIn && Date.parse(a.endAt) > serverNow && detail?.clinicDate === currentClinicDate;
  const feedback = useRef<HTMLDivElement>(null);
  async function submit(values: Values) {
    if (!detail || !patient || busy) return;
    if (!eligible) { setError(new Error('Lịch không còn đủ điều kiện tiếp nhận. Vui lòng kiểm tra hoặc xếp lại lịch.')); return; }
    const arrival = values.backdate && values.arrivalAt ? new Date(`${values.arrivalAt}+07:00`) : null;
    if (arrival && !Number.isFinite(arrival.getTime())) { form.setFields([{ name: 'arrivalAt', errors: ['Giờ đến không hợp lệ.'] }]); return; }
    const payload = { patientId: patient.id, expectedVersion: detail.appointment.version, identityConfirmed: values.identityConfirmed === true,
      arrivalAt: arrival?.toISOString() ?? null,
      arrivalReason: values.backdate ? values.arrivalReason?.trim() ?? null : null, purpose: values.purpose,
      priorCourseId: values.priorCourseId ?? null, note: values.note?.trim() || null };
    const serialized = JSON.stringify(payload);
    if (request.current?.payload !== serialized) request.current = { payload: serialized, key: newRequestKey() };
    setBusy(true); setError(undefined);
    try {
      const saved = await api<VisitResult>(`/api/reception/appointments/${appointmentId}/check-in`, {
        method: 'POST', body: JSON.stringify({ ...payload, requestKey: request.current!.key }),
      });
      setResult(saved);
    } catch (e) { setError(e as Error); window.setTimeout(() => feedback.current?.focus(), 0); }
    finally { setBusy(false); }
  }
  if (!detail && loading) return <Spin tip="Đang tải thông tin tiếp nhận" />;
  return <>
    {modalContext}<Space wrap><Typography.Title level={2}>Tiếp nhận bệnh nhân</Typography.Title><Button onClick={back}>Về danh sách tiếp nhận</Button></Space>
    <div ref={feedback} tabIndex={-1} role="region" aria-label="Kết quả tiếp nhận">{error && <Alert type="error" showIcon message={error.message}
      action={<Button onClick={() => void load()}>Tải lại lịch</Button>}
      description={error instanceof ApiError && error.code === 'MSG111' ? 'Dữ liệu đã thay đổi. Tải lại, đối chiếu và xác nhận danh tính lại; thông tin đang nhập được giữ.' : undefined} />}</div>
    {a && <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} style={{ marginTop: 16 }} items={[
      { key: 'attendee', label: 'Người khám trên lịch', children: a.attendeeName },
      { key: 'birth', label: 'Ngày sinh trên lịch', children: a.attendeeBirthDate ? formatDate(a.attendeeBirthDate) : 'Chưa có thông tin' },
      { key: 'booker', label: 'Người đặt', children: `${a.bookerName} · ${a.bookerPhone ?? a.bookerEmail ?? 'Chưa có liên hệ'}` },
      { key: 'appointment', label: 'Lịch hẹn', children: `${a.code} · ${arrivalClock(a.startAt)}` },
      { key: 'branch', label: 'Chi nhánh', children: a.branchName },
      { key: 'dentist', label: 'Bác sĩ / ghế', children: `${a.dentistName} · ${a.chairName}` },
    ]} />}
    {currentResult ? <Alert style={{ marginTop: 20 }} type="success" showIcon message="Đã tiếp nhận — mở lượt khám hiện có"
      description={<Space direction="vertical"><span>{currentResult.patientCode} · {currentResult.patientName} · Giờ đến {arrivalClock(currentResult.arrivalAt)}</span>
        <span>Trạng thái: {currentResult.status === 'Queued' ? 'Chờ khám' : currentResult.status}</span>
        <Link to={currentResult.openVisitPath}>Mở lượt khám</Link><Link to="/reception">Xem hàng chờ</Link></Space>} />
      : detail && (!canWrite ? <Alert type="info" message="Bạn chỉ có quyền xem lượt được phân công." /> : !eligible ? <Alert style={{ marginTop: 20 }} type="warning" showIcon message="Lịch chưa đủ điều kiện tiếp nhận"
        description={a?.ineligibleReason ?? 'Khoảng đặt lịch hoặc ngày tiếp nhận đã thay đổi. Vui lòng tải lại lịch.'} />
        : <Form<Values> form={form} layout="vertical" onFinish={values => void submit(values)} onValuesChange={() => setDirty(true)}
          disabled={busy || loading} style={{ marginTop: 20, maxWidth: 820 }}>
          <Typography.Paragraph>Đối chiếu người thực sự đến khám. Người đặt có thể khác người khám; không tự ghép hồ sơ bằng số điện thoại.</Typography.Paragraph>
          <Form.Item name="patientId" label="Hồ sơ bệnh nhân" rules={[{ required: true, message: 'Vui lòng chọn và đối chiếu hồ sơ bệnh nhân.' }]}>
            <Select showSearch filterOption={false} placeholder="Tìm tên, mã hoặc số điện thoại" onSearch={value => void search(value)}
              options={[...(patient && !patients.some(x => x.id === patient.id) ? [{ value: patient.id, label: `${patient.patientCode} · ${patient.fullName}` }] : []),
                ...patients.map(p => ({ value: p.id, label: `${p.patientCode} · ${p.fullName}${p.birthDate ? ` · ${formatDate(p.birthDate)}` : ''}` }))]} />
          </Form.Item>
          {searchError && <Alert type="error" message={searchError} />}
          {patientError && <Alert type="error" message={patientError} />}
          <Button onClick={() => setCreating(true)}>Tạo hồ sơ qua chức năng hồ sơ bệnh nhân</Button>
          {patient && <Descriptions title="Thông tin cần đối chiếu" size="small" column={1} items={[
            { key: 'code', label: 'Mã / họ tên', children: `${patient.patientCode} · ${patient.fullName}` },
            { key: 'birth', label: 'Ngày sinh', children: patient.birthDate ? formatDate(patient.birthDate) : 'Chưa biết' },
            { key: 'phone', label: 'Điện thoại', children: patient.phone ?? 'Chưa có' },
          ]} />}
          <Form.Item name="identityConfirmed" valuePropName="checked" rules={[{ validator: (_, value) => value === true ? Promise.resolve() : Promise.reject(new Error('Vui lòng đối chiếu và xác nhận đúng người đến khám.')) }]}>
            <Checkbox disabled={!patient || patient.isArchived}>Đã đối chiếu đúng người đến khám với lịch và hồ sơ đã chọn</Checkbox>
          </Form.Item>
          <Form.Item name="purpose" label="Lý do đến" rules={[{ required: true }]}><Select options={Object.entries(purposeLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
          {purpose !== 'InitialExamination' && <>
            <Form.Item name="priorCourseId" label="Đợt điều trị cũ (nếu có)"><Select allowClear disabled={courses.length === 0} placeholder="Chưa có đợt điều trị để chọn"
              options={courses.map(c => ({ value: c.id, label: c.label }))} /></Form.Item>
            <Alert type="info" message="Bác sĩ xác nhận nguồn và hạng mục chuyên môn sau. Thiếu đợt hoặc bước tùy chọn không chặn tiếp nhận." />
          </>}
          <Form.Item name="note" label="Ghi chú thêm"><Input.TextArea maxLength={1000} showCount rows={3} /></Form.Item>
          <Typography.Paragraph>Giờ đến mặc định là thời điểm hệ thống tiếp nhận.</Typography.Paragraph>
          {detail.canBackdate && <Form.Item name="backdate" valuePropName="checked"><Checkbox>Nhập giờ đến hồi cứu</Checkbox></Form.Item>}
          {backdate && <>
            <Form.Item name="arrivalAt" label="Giờ đến thực tế (giờ Việt Nam)" rules={[{ required: true }]}><Input type="datetime-local" /></Form.Item>
            <Form.Item name="arrivalReason" label="Lý do hồi cứu" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input.TextArea maxLength={500} /></Form.Item>
          </>}
          <Button type="primary" htmlType="submit" loading={busy} disabled={!patient || patient.isArchived}>Xác nhận Check-in</Button>
        </Form>)}
    <PatientFormDrawer open={creating} patient={null} onClose={() => setCreating(false)} onSaved={change => {
      setCreating(false); setPatient(change.patient); setDirty(true); form.setFieldValue('patientId', change.patient.id); form.setFieldValue('identityConfirmed', false);
    }} />
  </>;
}
