import { Alert, Button, Card, Checkbox, Descriptions, Form, Input, Select, Space, Spin, Table, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import PatientFormDrawer from '../patients/PatientFormDrawer';
import type { PatientDetail, PatientListItem, PatientPage } from '../patients/types';
import { checkInBlocker } from './eligibility';
import { clinicInput, clinicTime, emptySource, failureText, fromClinicInput, purposeLabels,
  type ReceptionDetail, type SourceOption, type Visit, type VisitSource } from './types';

type Values = { purpose: string; identityNote: string; source: VisitSource; reviewReason?: string; additionalConcern?: string;
  note?: string; arrival?: string; arrivalReason?: string };

export default function CheckInPage() {
  const { appointment } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [form] = Form.useForm<Values>();
  const purpose = Form.useWatch('purpose', form) as string | undefined;
  const [detail, setDetail] = useState<ReceptionDetail>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [conflict, setConflict] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [birthDate, setBirthDate] = useState('');
  const [patients, setPatients] = useState<PatientListItem[]>([]);
  const [patientTotal, setPatientTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [searching, setSearching] = useState(false);
  const [patient, setPatient] = useState<PatientDetail>();
  const [verified, setVerified] = useState(false);
  const [creating, setCreating] = useState(false);
  const [choices, setChoices] = useState<SourceOption[]>([]);
  const [sourceError, setSourceError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [retrospective, setRetrospective] = useState(!!params.get('arrival'));
  const arrivalParam = params.get('arrival');
  const inheritedArrival = arrivalParam && Number.isFinite(Date.parse(arrivalParam)) ? arrivalParam : null;
  const pending = useRef<{ payload: string; key: string } | null>(null);
  const selection = useRef(0);
  const feedback = useRef<HTMLDivElement>(null);
  const [now, setNow] = useState(Date.now());

  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 30_000); return () => clearInterval(timer); }, []);
  useEffect(() => {
    if (error) { feedback.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }); feedback.current?.focus({ preventScroll: true }); }
  }, [error]);

  async function reload() {
    setLoading(true); setError(undefined);
    try { setDetail(await api<ReceptionDetail>(`/api/reception/appointments/${appointment}`)); setConflict(false); }
    catch (e) { setError(failureText(e)); }
    finally { setLoading(false); }
  }
  useEffect(() => { void reload(); }, [appointment]);
  useEffect(() => {
    let active = true; setSearching(true);
    const timer = setTimeout(() => {
      const query = new URLSearchParams({ keyword, page: String(page), pageSize: '20' });
      if (birthDate) query.set('birthDate', birthDate);
      void api<PatientPage>(`/api/patients?${query}`).then(p => { if (active) { setPatients(p.items); setPatientTotal(p.total); } })
        .catch(e => { if (active) { setPatients([]); setError(failureText(e)); } }).finally(() => { if (active) setSearching(false); });
    }, 250);
    return () => { active = false; clearTimeout(timer); };
  }, [keyword, birthDate, page]);
  useEffect(() => {
    setChoices([]); setSourceError(undefined);
    if (!patient || !detail) return;
    let active = true;
    void api<SourceOption[]>(`/api/reception/sources?branchId=${detail.appointment.branchId}&patientId=${patient.id}`)
      .then(s => { if (active) setChoices(s); }).catch(e => { if (active) setSourceError(failureText(e)); });
    return () => { active = false; };
  }, [patient?.id, detail?.appointment.branchId]);

  async function choosePatient(id: number) {
    const revision = ++selection.current;
    setVerified(false); setPatient(undefined); form.setFieldValue('source', { ...emptySource });
    try { const selected = await api<PatientDetail>(`/api/patients/${id}`); if (selection.current === revision) setPatient(selected); }
    catch (e) { if (selection.current === revision) setError(failureText(e)); }
  }

  async function submit(values: Values) {
    if (!patient || !detail || !verified || busy || conflict) return;
    const blocker = checkInBlocker(detail.appointment);
    if (blocker) { setNow(Date.now()); setError(blocker); return; }
    const body = { appointment: detail.appointment.appointmentId, branchId: detail.appointment.branchId, patientId: patient.id,
      identityVerified: verified, identityNote: values.identityNote, purpose: values.purpose, source: values.source ?? emptySource,
      reviewReason: values.purpose === 'PostoperativeOrWarrantyReview' ? values.reviewReason ?? null : null,
      additionalConcern: values.additionalConcern?.trim() || null, note: values.note?.trim() || null,
      actualArrivalAt: retrospective && values.arrival
        ? inheritedArrival && values.arrival === clinicInput(new Date(inheritedArrival)) ? inheritedArrival : fromClinicInput(values.arrival) : null,
      arrivalReason: retrospective ? values.arrivalReason?.trim() || null : null, expectedVersion: detail.appointment.version };
    const payload = JSON.stringify(body);
    if (pending.current?.payload !== payload) pending.current = { payload, key: crypto.randomUUID() };
    setBusy(true); setError(undefined);
    try {
      const result = await api<Visit>('/api/reception/check-ins', { method: 'POST', body: JSON.stringify({ ...body, requestKey: pending.current.key }) });
      // Use the persisted ID then read back the committed visit on its own route.
      navigate(`/reception/visits/${result.visitId}`);
    } catch (e) { setError(failureText(e)); if (e instanceof ApiError && e.status === 409 && e.code === 'MSG111') setConflict(true); }
    finally { setBusy(false); }
  }

  if (loading && !detail) return <Spin aria-label="Đang tải lịch tiếp nhận" />;
  const a = detail?.appointment;
  const blocker = a ? checkInBlocker(a, now) : null;
  return <Space direction="vertical" size="large" style={{ width: '100%' }}>
    <Space><Link to="/reception">← Tiếp nhận và hàng chờ</Link><Typography.Title level={2}>Đối chiếu và check-in · S18</Typography.Title></Space>
    <div ref={feedback} tabIndex={-1}>{error && <Alert type="error" showIcon message={error} action={<Button onClick={() => void reload()} loading={loading}>Đọc lại lịch hiện tại</Button>} />}</div>
    {conflict && <Alert type="warning" message="Thông tin nhập được giữ lại. Đọc lại lịch hiện tại và đối chiếu trước khi gửi lại." />}
    {a && <Card title={`Lịch ${a.code}`}><Descriptions column={2} bordered items={[
      { key: 'booker', label: 'Người đặt', children: a.bookerName }, { key: 'contact', label: 'Liên hệ người đặt', children: [a.bookerPhone, a.bookerEmail].filter(Boolean).join(' · ') || 'Chưa rõ' },
      { key: 'attendee', label: 'Người khám dự kiến', children: a.attendeeName }, { key: 'dob', label: 'Ngày sinh dự kiến', children: a.attendeeBirthDate ?? 'Chưa rõ' },
      { key: 'patient', label: 'Patient đã gắn lịch', children: a.patientCode ?? 'Chưa gắn' }, { key: 'dentist', label: 'Bác sĩ', children: a.dentistName },
      { key: 'time', label: 'Giờ hẹn', children: clinicTime(a.startAt) }, { key: 'status', label: 'Trạng thái lịch', children: a.status },
    ]} /></Card>}
    {detail?.visit ? <Card title="Lịch đã tiếp nhận"><Typography.Paragraph>{detail.visit.patientName} · {detail.visit.visitCode}</Typography.Paragraph>
      <Link to={`/reception/visits/${detail.visit.visitId}`}>Mở lượt khám hiện có</Link></Card> : a && blocker ?
      <Alert type="warning" showIcon message="Chưa thể check-in lịch hẹn này" description={<Space direction="vertical">
        <Typography.Text>{blocker}</Typography.Text>
        <Typography.Text>Chọn lịch hợp lệ trong ngày tiếp nhận. Khách đến trực tiếp cần đặt một ca qua S11 trước khi check-in.</Typography.Text>
        <Space wrap><Link to="/reception">Về tiếp nhận hôm nay</Link><Link to={`/appointments/${a.appointmentId}`}>Mở chi tiết lịch hẹn</Link></Space>
      </Space>} /> : a && <>
      <Card title="1. Chọn đúng người thực tế đến khám">
        <Alert type="info" showIcon message="Đối chiếu tên, mã, ngày sinh khi có và liên hệ. Số điện thoại dùng chung không xác nhận danh tính." />
        <Space wrap style={{ margin: '16px 0' }}>
          <Input aria-label="Tìm Patient" placeholder="Mã / họ tên / liên hệ" value={keyword} maxLength={100} onChange={e => { setKeyword(e.target.value); setPage(1); }} />
          <Input aria-label="Ngày sinh đối chiếu" type="date" value={birthDate} onChange={e => { setBirthDate(e.target.value); setPage(1); }} />
          <Button onClick={() => setCreating(true)}>Chưa có hồ sơ · UC-38</Button>
        </Space>
        <Table<PatientListItem> rowKey="id" size="small" loading={searching} dataSource={patients} scroll={{ x: 700 }}
          pagination={{ current: page, total: patientTotal, pageSize: 20, onChange: setPage, showSizeChanger: false }}
          locale={{ emptyText: 'Không tìm thấy hồ sơ phù hợp. Kiểm tra trùng trước khi tạo mới.' }} columns={[
            { title: 'Mã', dataIndex: 'patientCode' }, { title: 'Họ tên', dataIndex: 'fullName' }, { title: 'Ngày sinh', render: (_, p) => p.birthDate ?? 'Chưa rõ' },
            { title: 'Liên hệ', render: (_, p) => p.phone ?? 'Chưa rõ' }, { title: 'Chọn', render: (_, p) => <Button disabled={busy} onClick={() => void choosePatient(p.id)}>Chọn {p.patientCode}</Button> },
          ]} />
        {patient && <Card size="small" title="Người thực tế được chọn">
          <Typography.Paragraph strong>{patient.patientCode} · {patient.fullName} · {patient.birthDate ?? 'Ngày sinh chưa rõ'} · {patient.phone ?? 'Liên hệ chưa rõ'}</Typography.Paragraph>
          {patient.guardianName && <Typography.Paragraph>Đại diện: {patient.guardianName} · {patient.guardianRelationship} · {patient.guardianPhone}</Typography.Paragraph>}
          <Checkbox checked={verified} onChange={e => setVerified(e.target.checked)}>Tôi đã đối chiếu trực tiếp và xác minh đây là đúng người đến khám</Checkbox>
        </Card>}
      </Card>
      <Card title="2. Xác nhận mục đích và giờ đến">
        <Form<Values> form={form} layout="vertical" disabled={busy} initialValues={{ purpose: 'InitialExamination', source: { ...emptySource },
          arrival: inheritedArrival ? clinicInput(new Date(inheritedArrival)) : clinicInput(), arrivalReason: inheritedArrival ? 'Khách đến trực tiếp trước khi xếp lịch walk-in' : undefined }} onFinish={v => void submit(v)}>
          <Form.Item name="identityNote" label="Căn cứ đối chiếu danh tính" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input.TextArea maxLength={500} rows={2} /></Form.Item>
          <Form.Item name="purpose" label="Mục đích lần đến" rules={[{ required: true }]}><Select onChange={() => form.setFieldValue('source', { ...emptySource })}
            options={Object.entries(purposeLabels).map(([value, label]) => ({ value, label }))} /></Form.Item>
          {['TreatmentContinuation', 'PostoperativeOrWarrantyReview'].includes(purpose ?? '') && <>
            <Alert type="warning" showIcon message="Nguồn thiếu hoặc đang bị hold cần bác sĩ đánh giá. Tiếp nhận không gỡ blocker hoặc xác nhận sẵn sàng điều trị." />
            {sourceError && <Alert type="error" message={sourceError} />}
            {(['Dossier', 'Item', 'Step', 'Activity'] as const).map(kind => <Form.Item key={kind} name={['source', `${kind.toLowerCase()}Id`]} label={{ Dossier: 'Hồ sơ điều trị nguồn', Item: 'Hạng mục nguồn', Step: 'Bước nguồn', Activity: 'Hoạt động cũ' }[kind]}>
              <Select allowClear placeholder="Chưa xác định · cần bác sĩ đánh giá" options={choices.filter(s => s.kind === kind).map(s => ({ value: s.id, label: `${s.label}${s.blocker ? ` · ${s.blocker}` : ''}` }))} /></Form.Item>)}
            {purpose === 'PostoperativeOrWarrantyReview' && <Form.Item name="reviewReason" label="Lý do kiểm tra"><Select allowClear options={[{ value: 'Postoperative', label: 'Sau điều trị' }, { value: 'Warranty', label: 'Bảo hành' }]} /></Form.Item>}
          </>}
          <Form.Item name="additionalConcern" label="Vấn đề mới / mối quan tâm bổ sung" rules={[{ max: 1000 }]}><Input.TextArea maxLength={1000} showCount rows={3} /></Form.Item>
          <Form.Item name="note" label="Ghi chú tiếp nhận" rules={[{ max: 1000 }]}><Input.TextArea maxLength={1000} rows={2} /></Form.Item>
          <Checkbox checked={retrospective} onChange={e => setRetrospective(e.target.checked)}>Nhập giờ đến thực tế trước thời điểm xác nhận</Checkbox>
          {!retrospective && <Typography.Paragraph>Giờ đến lấy thời điểm hiện tại khi server tiếp nhận.</Typography.Paragraph>}
          {retrospective && <>
            <Form.Item name="arrival" label="Giờ đến thực tế (Asia/Ho_Chi_Minh)" rules={[{ required: true }]}><Input type="datetime-local" /></Form.Item>
            <Form.Item name="arrivalReason" label="Lý do nhập hồi cứu" rules={[{ required: true, whitespace: true }, { max: 500 }]}><Input.TextArea maxLength={500} rows={2} /></Form.Item>
          </>}
          <Button aria-label="Xác nhận check-in" type="primary" htmlType="submit" loading={busy} disabled={busy || !patient || !verified || conflict || a.status !== 'Confirmed'}>Xác nhận check-in</Button>
        </Form>
      </Card>
    </>}
    <PatientFormDrawer open={creating} patient={null} onClose={() => setCreating(false)} onSaved={change => {
      selection.current++; setPatient(change.patient); setVerified(false); form.setFieldValue('source', { ...emptySource }); setCreating(false);
    }} />
  </Space>;
}
