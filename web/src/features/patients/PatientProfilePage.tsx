import PatientExaminationsTab from '../examinations/PatientExaminationsTab';
import ExaminationTreatmentHandoff from '../examinations/ExaminationTreatmentHandoff';
import { Alert, Button, Card, Descriptions, Empty, Form, Input, message, Modal, Result, Skeleton, Space, Tabs, Tag, Timeline, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api, ApiError } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import { formatDate, formatTime } from '../patient-links/types';
import PatientFormDrawer from './PatientFormDrawer';
import PatientAppointmentsTab from './PatientAppointmentsTab';
import { auditActionLabels, genderLabels, type PatientAuditEntry, type PatientChange, type PatientDetail, type PatientReference } from './types';
import DossierList from '../treatment/DossierList';
import '../treatment/treatment.css';
import './patientTreatment.css';

const unknown = <Typography.Text type="secondary">Chưa rõ</Typography.Text>;
const empty = <Typography.Text type="secondary">—</Typography.Text>;

// Tabs whose source features are not built yet.
const reference = (value: PatientReference | null) => value ? `${value.name}${value.active ? '' : ' (ngừng dùng)'}` : '—';

function Pending({ feature }: { feature: string }) {
  return <Alert type="info" showIcon message="Chức năng chưa triển khai"
    description={`Dữ liệu ${feature} sẽ hiển thị tại đây khi chức năng tương ứng được triển khai. Hiện chưa có dữ liệu để hiển thị.`} />;
}

// S06 Patient Profile and History (UC-08, UC-09, UC-38). Route IDs are never trusted: the server rechecks every request.
export default function PatientProfilePage() {
  const { id } = useParams();
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [search] = useSearchParams();
  const canManage = auth.user?.permissions.includes('patients.manage') ?? false;
  const [toast, toastContext] = message.useMessage();
  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [failure, setFailure] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [editing, setEditing] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const [audit, setAudit] = useState<PatientAuditEntry[] | null>(null);
  const [auditError, setAuditError] = useState<string | null>(null);
  const [tab, setTab] = useState(search.get('tab') ?? 'basic');
  useEffect(()=>{setTab(search.get('tab')??'basic');},[id,location.search]);
  const sourceExaminationVisitId=search.get('sourceExaminationVisitId');
  const [auditOpen, setAuditOpen] = useState(false);
  const visitId = search.get('visitId') ?? undefined;

  const noticeShown = useRef(false);
  useEffect(() => {
    const notice = (location.state as { notice?: string } | null)?.notice;
    if (!notice || noticeShown.current) return;
    noticeShown.current = true;
    void toast.success(notice);
    navigate(location.pathname, { replace: true, state: null });
  }, []);

  useEffect(() => {
    let active = true;
    setFailure(null);
    api<PatientDetail>(`/api/patients/${id}`)
      .then(result => { if (active) setPatient(result); })
      .catch(error => { if (active) setFailure(error as ApiError); });
    return () => { active = false; };
  }, [id, revision]);

  useEffect(() => {
    if (!auditOpen) return;
    let active = true;
    setAudit(null); setAuditError(null);
    api<PatientAuditEntry[]>(`/api/patients/${id}/audit`)
      .then(result => { if (active) setAudit(result); })
      .catch(error => { if (active) setAuditError((error as Error).message); });
    return () => { active = false; };
  }, [auditOpen, id, revision]);

  function saved(change: PatientChange) {
    setPatient(change.patient);
    setRevision(value => value + 1);
    void toast.success(change.notification.message);
  }

  // The message holder stays mounted while loading so the create notice (MSG222) is not lost.
  if (failure && !patient) return <>{toastContext}<Result status={failure.status === 403 ? '403' : failure.status === 404 ? '404' : 'error'} title={failure.message}
    extra={<Space><Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button><Link to="/patients">Về danh sách bệnh nhân</Link></Space>} /></>;
  if (!patient) return <>{toastContext}<Card><Skeleton active /></Card></>;

  const age = patient.birthDate ? (() => {
    const born = new Date(`${patient.birthDate.slice(0, 10)}T00:00:00Z`);
    if (Number.isNaN(born.getTime())) return null;
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
    const year = Number(parts.find(part => part.type === 'year')?.value);
    const month = Number(parts.find(part => part.type === 'month')?.value);
    const day = Number(parts.find(part => part.type === 'day')?.value);
    const years = year - born.getUTCFullYear() - (month < born.getUTCMonth() + 1 ||
      (month === born.getUTCMonth() + 1 && day < born.getUTCDate()) ? 1 : 0);
    return years >= 0 && years < 150 ? years : null;
  })() : null;

  const patientBanner = <Card className="patient-banner">
      <div className="patient-banner-row">
        <div>
          <Typography.Title level={3} className="patient-name">{patient.fullName}</Typography.Title>
          <Space wrap size={[16, 4]}>
            <Typography.Text strong>{patient.patientCode}</Typography.Text>
            <span>{age === null ? 'Chưa rõ tuổi' : `${age} tuổi`}</span>
            <span>Ngày sinh: {patient.birthDate ? formatDate(patient.birthDate) : unknown}</span>
            <span>Giới tính: {patient.gender ? genderLabels[patient.gender] : unknown}</span>
            <span>SĐT: {patient.phone ?? empty}</span>
            {patient.isArchived ? <Tag>Ngừng sử dụng</Tag> : <Tag color="green">Đang sử dụng</Tag>}
          </Space>
        </div>
        <Button onClick={() => setRevision(value => value + 1)}>Tải lại</Button>
      </div>
      {failure && <Alert className="patient-section" type="error" showIcon message={failure.message} />}
      {patient.isArchived && <Alert className="patient-section" type="warning" showIcon message="Hồ sơ đã ngừng sử dụng"
        description={`${formatTime(patient.archivedAt)} · Lý do: ${patient.archiveReason ?? '—'}. Khôi phục để chỉnh sửa.`} />}
    </Card>;

  return <main className="patient-treatment-shell">
    {toastContext}
    <Tabs className="patient-treatment-tabs" activeKey={tab} onChange={setTab}
      tabBarExtraContent={{
        left: <div className="patient-treatment-toolbar">
          <Button type="text" onClick={() => navigate(-1)} aria-label="Quay lại">←</Button>
          <Typography.Title level={3}>HỒ SƠ KHÁCH HÀNG</Typography.Title>
        </div>,
        right: <Link to="/patients" aria-label="Đóng hồ sơ khách hàng" className="patient-treatment-close">×</Link>,
      }} items={[
        { key: 'basic', label: 'Thông tin cơ bản', children: <>
          {patientBanner}
          <Card className="patient-basic-card">
          {canManage && <Space className="patient-actions">
            <Button type="primary" disabled={patient.isArchived} onClick={() => setEditing(true)}>Sửa thông tin</Button>
            <Button danger={!patient.isArchived} onClick={() => setArchiving(true)}>{patient.isArchived ? 'Khôi phục hồ sơ' : 'Ngừng sử dụng'}</Button>
          </Space>}
          <Descriptions bordered column={{ xs: 1, md: 2 }} size="small" items={[
            { label: 'Mã bệnh nhân', children: patient.patientCode },
            { label: 'Họ tên', children: patient.fullName },
            { label: 'Ngày sinh', children: patient.birthDate ? formatDate(patient.birthDate) : unknown },
            { label: 'Giới tính', children: patient.gender ? genderLabels[patient.gender] : unknown },
            { label: 'Số điện thoại', children: patient.phone ?? empty },
            { label: 'Địa chỉ', children: patient.address ?? empty },
            { label: 'Ngày lập hồ sơ', children: patient.profileDate ? formatDate(patient.profileDate) : empty },
            { label: 'Nguồn', children: reference(patient.source) },
            { label: 'Phân nhóm khách hàng', children: reference(patient.customerGroup) },
            { label: 'Nghề nghiệp', children: reference(patient.occupation) },
            { label: 'Tiền sử (bệnh nhân khai)', span: 2, children: patient.intakeHistory.length || patient.intakeHistoryOther ? <>
              {patient.intakeHistory.map(x => <Tag key={x.id}>{x.name}{x.active ? '' : ' (ngừng dùng)'}</Tag>)}
              {patient.intakeHistoryOther && <div>{patient.intakeHistoryOther}</div>}
              {patient.intakeRecordedAt && <Typography.Text type="secondary">Ghi nhận bởi {patient.intakeRecordedByName ?? '—'} lúc {formatTime(patient.intakeRecordedAt)}</Typography.Text>}
            </> : empty },
            { label: 'Ghi chú hành chính', span: 2, children: patient.note ?? empty },
          ]} />
          <Typography.Title level={5} className="patient-section">Người giám hộ</Typography.Title>
          {patient.guardianName ? <Descriptions bordered column={{ xs: 1, md: 3 }} size="small" items={[
            { label: 'Họ tên', children: patient.guardianName },
            { label: 'Quan hệ', children: patient.guardianRelationship ?? empty },
            { label: 'Số điện thoại', children: patient.guardianPhone ?? empty },
          ]} /> : <Typography.Text type="secondary">Không có người giám hộ.</Typography.Text>}
          <Typography.Title level={5} className="patient-section">Liên kết tài khoản ứng dụng</Typography.Title>
          <Typography.Paragraph>{patient.link.verifiedAccountUserName
            ? <>Đã liên kết với tài khoản <Typography.Text strong>{patient.link.verifiedAccountUserName}</Typography.Text> từ {formatTime(patient.link.verifiedAt)}.</>
            : 'Chưa có tài khoản nào được xác minh liên kết với hồ sơ này.'}
            {' '}<Typography.Text type="secondary">Liên kết chỉ được xác minh hoặc thu hồi tại màn Xác minh liên kết hồ sơ (S08).</Typography.Text></Typography.Paragraph>
          <Typography.Paragraph type="secondary">Tạo lúc {formatTime(patient.createdAt)} · Cập nhật lúc {formatTime(patient.updatedAt)} · Phiên bản {patient.version}</Typography.Paragraph>
          <Button onClick={() => setAuditOpen(value => !value)} aria-expanded={auditOpen}>
            {auditOpen ? 'Ẩn lịch sử thay đổi' : 'Xem lịch sử thay đổi'}
          </Button>
          {auditOpen && <section className="patient-audit-panel" aria-label="Lịch sử thay đổi">
            {auditError ? <Alert type="error" showIcon message={auditError} action={<Button size="small" onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />
              : !audit ? <Skeleton active /> : audit.length === 0 ? <Empty description="Chưa có lịch sử thay đổi." />
              : <Timeline items={audit.map(entry => ({ key: entry.id, children: <div className="audit-entry">
                <Typography.Text strong>{auditActionLabels[entry.action] ?? entry.action}</Typography.Text>
                {' · '}{entry.actorName}{' · '}<Typography.Text type="secondary">{formatTime(entry.occurredAt)}</Typography.Text>
                {entry.reason && <div>Lý do: {entry.reason}</div>}
                {entry.changes.length > 0 && <ul className="patient-changes">{entry.changes.map(change =>
                  <li key={change.field}>{change.field}: <Typography.Text delete type="secondary">{change.before ?? 'trống'}</Typography.Text> → {change.after ?? 'trống'}</li>)}</ul>}
              </div> }))} />}
          </section>}
          </Card>
        </> },
        { key:'examinations',label:'Khám',children:<PatientExaminationsTab key={patient.id} patientId={patient.id} active={tab==='examinations'}/> },
        { key: 'treatment', label: 'Điều trị & Thanh toán', children: sourceExaminationVisitId ? <ExaminationTreatmentHandoff patientId={patient.id} visitId={sourceExaminationVisitId} version={search.get('examinationVersion')}/> : <DossierList patientId={patient.id} visitId={visitId} patientName={patient.fullName} patientCode={patient.patientCode} patientAge={age} /> },
        { key: 'files', label: 'Hình ảnh', children: <Link to={`/patients/${patient.id}/documents`}>Mở hình ảnh và tài liệu của bệnh nhân</Link> },
        { key: 'appointments', label: 'Lịch hẹn', children: <PatientAppointmentsTab key={patient.id} patientId={patient.id} active={tab === 'appointments'} revision={revision} /> },
        { key: 'prescriptions', label: 'Đơn thuốc', children: <Pending feature="đơn thuốc (S23)" /> },
      ]} />
    <PatientFormDrawer open={editing} patient={patient} onClose={() => setEditing(false)} onSaved={change => { setEditing(false); saved(change); }} />
    <ArchiveModal patient={patient} open={archiving} onClose={() => setArchiving(false)} onSaved={change => { setArchiving(false); saved(change); }} />
  </main>;
}

function ArchiveModal({ patient, open, onClose, onSaved }: {
  patient: PatientDetail; open: boolean; onClose: () => void; onSaved: (change: PatientChange) => void;
}) {
  const [form] = Form.useForm<{ reason: string }>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const restoring = patient.isArchived;
  useEffect(() => { if (open) { form.resetFields(); setError(null); } }, [open]);

  async function submit({ reason }: { reason: string }) {
    setBusy(true); setError(null);
    try {
      onSaved(await api<PatientChange>(`/api/patients/${patient.id}/${restoring ? 'restore' : 'archive'}`,
        { method: 'POST', body: JSON.stringify({ version: patient.version, reason }) }));
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }

  return <Modal open={open} onCancel={onClose} confirmLoading={busy} onOk={() => form.submit()} destroyOnClose
    title={`${restoring ? 'Khôi phục' : 'Ngừng sử dụng'} hồ sơ ${patient.patientCode} · ${patient.fullName}`}
    okText={restoring ? 'Khôi phục' : 'Ngừng sử dụng'} okButtonProps={{ danger: !restoring }} cancelText="Hủy">
    <Typography.Paragraph>{restoring
      ? 'Hồ sơ sẽ dùng lại được cho tìm kiếm và chỉnh sửa.'
      : 'Hồ sơ không bị xóa; lịch sử và liên kết cũ được giữ nguyên. Hồ sơ bị ẩn khỏi tìm kiếm mặc định và không sửa được cho tới khi khôi phục.'}</Typography.Paragraph>
    {error && <Alert className="form-alert" type="error" message={error} showIcon />}
    <Form form={form} layout="vertical" onFinish={values => void submit(values)}>
      <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' },
        { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' }]}><Input.TextArea rows={3} maxLength={500} showCount /></Form.Item>
    </Form>
  </Modal>;
}
