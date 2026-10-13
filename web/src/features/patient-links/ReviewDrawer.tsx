import { Alert, Button, Col, Descriptions, Drawer, Empty, Form, Input, Modal, Row, Space, Spin, Table, Tabs, Tag, Timeline, Typography, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../../services/api';
import {
  auditLabels, formatDate, formatTime, identityMismatches, statusColors, statusLabels,
  type LinkAudit, type LinkDecisionResult, type LinkReviewDetail, type PatientCandidate,
} from './types';

type DecisionMode = 'reject' | 'revoke';
type Props = { id: number | null; onClose: () => void; onChanged: () => void };

// S08 comparison panel: account and proposed Patient side by side, then Verify / Reject / Revoke.
export default function ReviewDrawer({ id, onClose, onChanged }: Props) {
  const [toast, toastContext] = message.useMessage();
  const [modal, modalContext] = Modal.useModal();
  const [detail, setDetail] = useState<LinkReviewDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [candidates, setCandidates] = useState<PatientCandidate[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [patientId, setPatientId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [decision, setDecision] = useState<DecisionMode | null>(null);
  const [audit, setAudit] = useState<LinkAudit[] | null>(null);
  const [auditError, setAuditError] = useState<string | null>(null);
  const [verifyForm] = Form.useForm<{ verificationNote: string }>();
  const [decisionForm] = Form.useForm<{ decisionReason: string; publicNote?: string }>();
  const latest = useRef(0);

  async function load(target: number) {
    const request = ++latest.current;
    setLoading(true); setError(null);
    try {
      const result = await api<LinkReviewDetail>(`/api/patient-link-reviews/${target}`);
      if (request !== latest.current) return;
      setDetail(result); setAudit(null); setCandidates(null); setPatientId(null); setSearchError(null);
      verifyForm.resetFields();
    } catch (failure) { if (request === latest.current) setError(failure as ApiError); }
    finally { if (request === latest.current) setLoading(false); }
  }
  useEffect(() => { if (id != null) void load(id); else { latest.current++; setDetail(null); } }, [id]);

  async function search(keyword: string) {
    if (!detail) return;
    setSearchError(null);
    if (!keyword.trim()) { setSearchError('Vui lòng nhập Từ khóa.'); return; }
    setSearching(true);
    try {
      const result = await api<PatientCandidate[]>(`/api/patient-link-reviews/${detail.id}/patients?keyword=${encodeURIComponent(keyword.trim())}`);
      setCandidates(result); setPatientId(null);
    } catch (failure) { setSearchError((failure as Error).message); }
    finally { setSearching(false); }
  }

  async function loadAudit() {
    if (!detail) return;
    setAuditError(null);
    try { setAudit(await api<LinkAudit[]>(`/api/patient-link-reviews/${detail.id}/audit`)); }
    catch (failure) { setAuditError((failure as Error).message); }
  }

  async function submit(path: string, body: object) {
    if (!detail || saving) return;
    setSaving(true); setError(null);
    try {
      const result = await api<LinkDecisionResult>(`/api/patient-link-reviews/${detail.id}/${path}`, { method: 'POST', body: JSON.stringify(body) });
      void toast.success(`Đã lưu. Thông báo gửi người dùng: ${result.notification.message}`);
      setDetail(result.request); setDecision(null); setCandidates(null); setPatientId(null); setAudit(null);
      decisionForm.resetFields(); verifyForm.resetFields();
      onChanged();
    } catch (failure) { setError(failure as ApiError); }
    finally { setSaving(false); }
  }

  async function verify() {
    if (!detail) return;
    const values = await verifyForm.validateFields().catch(() => null);
    if (!values) return;
    const patient = candidates?.find(item => item.patient.id === patientId)?.patient;
    if (!patient) { setSearchError('Chọn hồ sơ Patient của chính người yêu cầu trước khi xác minh.'); return; }
    if (identityMismatches(detail, patient).length) return;
    modal.confirm({
      title: 'Xác nhận đúng hồ sơ tự thân?',
      content: <>Liên kết tài khoản <b>{detail.account.userName}</b> với hồ sơ <b>{patient.patientCode} — {patient.fullName}</b>.
        Tài khoản sẽ được xem hồ sơ của chính mình sau khi lưu.</>,
      okText: 'Xác minh', cancelText: 'Hủy',
      onOk: () => submit('verify', { version: detail.version, patientId: patient.id, verificationNote: values.verificationNote }),
    });
  }

  async function decide() {
    if (!detail || !decision) return;
    const values = await decisionForm.validateFields().catch(() => null);
    if (!values) return;
    const note = verifyForm.getFieldValue('verificationNote') as string | undefined;
    await submit(decision, decision === 'reject'
      ? { version: detail.version, decisionReason: values.decisionReason, publicNote: values.publicNote || null, verificationNote: note || null }
      : { version: detail.version, decisionReason: values.decisionReason, publicNote: values.publicNote || null });
  }

  const stale = error?.code === 'MSG111';
  const selectedPatient = candidates?.find(item => item.patient.id === patientId)?.patient;
  const selectedMismatches = detail && selectedPatient ? identityMismatches(detail, selectedPatient) : [];
  return <Drawer open={id != null} onClose={onClose} width={960} destroyOnClose
    title={detail ? <Space>{detail.requestCode}<Tag color={statusColors[detail.status]}>{statusLabels[detail.status]}</Tag></Space> : 'Đối chiếu yêu cầu'}>
    {toastContext}{modalContext}
    {error && <Alert className="form-alert" type={stale ? 'warning' : 'error'} message={error.message} showIcon
      action={detail && <Button size="small" onClick={() => void load(detail.id)}>Tải lại</Button>} />}
    {loading && !detail && <Spin />}
    {detail && <Tabs onChange={key => { if (key === 'audit' && !audit) void loadAudit(); }} items={[
      { key: 'review', label: 'Đối chiếu', children: <Spin spinning={loading}>
        {detail.conflict.accountHasActiveLink && <Alert className="form-alert" type="warning" showIcon
          message={`Tài khoản đã có liên kết tự thân đang hiệu lực (${detail.conflict.activeRequestCode}). Không thể xác minh thêm.`} />}
        {!detail.account.isActive && <Alert className="form-alert" type="warning" showIcon message="Tài khoản đang bị khóa." />}
        <Row gutter={[16, 16]}>
          <Col xs={24} md={12}><Descriptions title="Tài khoản" column={1} size="small" bordered items={[
            { label: 'Tên đăng nhập', children: <>{detail.account.userName} <Typography.Text type="secondary">#{detail.account.id}</Typography.Text></> },
            { label: 'Họ tên tài khoản', children: detail.account.fullName },
            { label: 'Email', children: <Contact value={detail.account.email} confirmed={detail.account.emailConfirmed} /> },
            { label: 'Số điện thoại', children: <Contact value={detail.account.phone} confirmed={detail.account.phoneConfirmed} /> },
            { label: 'Tạo lúc', children: formatTime(detail.account.createdAt) },
          ]} /></Col>
          <Col xs={24} md={12}><Descriptions title="Thông tin tự khai" column={1} size="small" bordered items={[
            { label: 'Họ tên', children: detail.declaredFullName },
            { label: 'Ngày sinh', children: formatDate(detail.declaredBirthDate) },
            { label: 'Mã hồ sơ', children: detail.declaredPatientCode ?? '—' },
            { label: 'Thông tin đối chiếu', children: <span className="multiline">{detail.matchingInformation ?? '—'}</span> },
            { label: 'Gửi lúc', children: formatTime(detail.requestedAt) },
          ]} /></Col>
        </Row>
        <Typography.Paragraph type="secondary" className="link-hint">Thông tin liên hệ và tự khai không phải căn cứ duy nhất; đối chiếu giấy tờ hoặc lượt khám theo quy trình phòng khám.</Typography.Paragraph>

        {detail.patient && <Descriptions title="Hồ sơ đã liên kết" column={2} size="small" bordered items={[
          { label: 'Mã hồ sơ', children: detail.patient.patientCode }, { label: 'Họ tên', children: detail.patient.fullName },
          { label: 'Ngày sinh', children: formatDate(detail.patient.birthDate) }, { label: 'SĐT', children: detail.patient.phone ?? '—' },
        ]} />}

        {detail.status === 'PENDING' && <>
          <Typography.Title level={5} className="link-section">Hồ sơ Patient</Typography.Title>
          <Input.Search key={detail.id} placeholder="Mã hồ sơ, họ tên hoặc số điện thoại" maxLength={100} enterButton="Tra cứu"
            defaultValue={detail.declaredPatientCode ?? detail.declaredFullName} loading={searching} onSearch={value => void search(value)} />
          {searchError && <Alert className="link-inline-alert" type="error" message={searchError} showIcon />}
          {candidates && <Table<PatientCandidate> className="link-candidates" rowKey={item => item.patient.id} size="small" pagination={false}
            dataSource={candidates} locale={{ emptyText: <Empty description="Không tìm thấy hồ sơ phù hợp." /> }}
            rowSelection={{ type: 'radio', selectedRowKeys: patientId ? [patientId] : [],
              onChange: keys => setPatientId(Number(keys[0])), getCheckboxProps: item => ({ disabled: item.hasActiveLink }) }}
            columns={[
              { title: 'Mã hồ sơ', render: (_, item) => item.patient.patientCode },
              { title: 'Họ tên', render: (_, item) => item.patient.fullName },
              { title: 'Ngày sinh', render: (_, item) => formatDate(item.patient.birthDate) },
              { title: 'SĐT', render: (_, item) => item.patient.phone ?? '—' },
              { title: 'Đối chiếu', render: (_, item) => {
                const mismatched = identityMismatches(detail, item.patient);
                return <Space wrap size={4}>
                  {item.hasActiveLink && <Tag color="red">Đã có liên kết</Tag>}
                  {mismatched.length ? mismatched.map(field => <Tag key={field} color="orange">Lệch {field.toLowerCase()}</Tag>)
                    : <Tag color="green">Khớp thông tin tự khai</Tag>}
                </Space>;
              } },
            ]} />}
          {selectedMismatches.length > 0 && <Alert className="link-inline-alert" type="warning" showIcon
            message={`Hồ sơ đã chọn lệch ${selectedMismatches.join(', ').toLowerCase()} so với thông tin tự khai — không thể xác minh.`}
            description="Kiểm tra lại giấy tờ với người yêu cầu. Nếu không đúng người, chọn Từ chối và ghi lý do; người dùng có thể gửi lại yêu cầu với thông tin chính xác." />}
          <Form form={verifyForm} layout="vertical" className="link-section">
            <Form.Item name="verificationNote" label="Kết quả đối chiếu (phương pháp / bằng chứng)"
              rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Kết quả đối chiếu.' }, { max: 1000, message: 'Kết quả đối chiếu không được vượt quá 1000 ký tự.' }]}>
              <Input.TextArea rows={3} maxLength={1000} showCount placeholder="Ví dụ: đối chiếu CCCD tại quầy lễ tân, khớp họ tên và ngày sinh" />
            </Form.Item>
          </Form>
          <Space wrap>
            <Button type="primary" loading={saving} disabled={detail.conflict.accountHasActiveLink || selectedMismatches.length > 0} onClick={() => void verify()}>Xác minh</Button>
            <Button danger disabled={saving} onClick={() => setDecision('reject')}>Từ chối</Button>
          </Space>
        </>}

        {detail.status === 'VERIFIED' && <Space className="link-section"><Button danger loading={saving} onClick={() => setDecision('revoke')}>Thu hồi liên kết</Button></Space>}

        {(detail.reviewedAt || detail.revokedAt || detail.withdrawnAt) && <Descriptions title="Kết quả xử lý" className="link-section" column={1} size="small" bordered items={[
          { label: 'Người xác minh / từ chối', children: detail.reviewerName ? `${detail.reviewerName} — ${formatTime(detail.reviewedAt)}` : '—' },
          { label: 'Người thu hồi', children: detail.revokerName ? `${detail.revokerName} — ${formatTime(detail.revokedAt)}` : '—' },
          { label: 'Rút yêu cầu', children: formatTime(detail.withdrawnAt) },
          { label: 'Kết quả đối chiếu (nội bộ)', children: detail.verificationNote ?? '—' },
          { label: 'Lý do quyết định (nội bộ)', children: detail.decisionReason ?? '—' },
          { label: 'Nội dung gửi người dùng', children: detail.publicNote ?? '—' },
        ]} />}
      </Spin> },
      { key: 'audit', label: 'Lịch sử', children: auditError
        ? <Alert type="error" message={auditError} showIcon action={<Button size="small" onClick={() => void loadAudit()}>Thử lại</Button>} />
        : !audit ? <Spin /> : audit.length === 0 ? <Empty description="Chưa có lịch sử." />
        : <Timeline items={audit.map(entry => ({ children: <>
            <Typography.Text strong>{auditLabels[entry.action] ?? entry.action}</Typography.Text> — {entry.actorName} — {formatTime(entry.occurredAt)}
            {entry.reason && <div>Ghi chú: {entry.reason}</div>}
            <details><summary>Dữ liệu trước/sau</summary><pre>{JSON.stringify({ before: JSON.parse(entry.beforeJson), after: JSON.parse(entry.afterJson) }, null, 2)}</pre></details>
          </> }))} /> },
    ]} />}

    <Modal open={decision != null} title={decision === 'reject' ? 'Từ chối yêu cầu liên kết' : 'Thu hồi liên kết'} okText="Xác nhận"
      okButtonProps={{ danger: true, loading: saving }} cancelText="Hủy" onCancel={() => setDecision(null)} onOk={() => void decide()} destroyOnClose>
      <Typography.Paragraph>{decision === 'revoke'
        ? 'Tài khoản sẽ mất quyền xem hồ sơ ngay ở lần truy cập tiếp theo. Hồ sơ lâm sàng được giữ nguyên.'
        : 'Yêu cầu sẽ không được cấp quyền xem hồ sơ. Người dùng có thể gửi yêu cầu mới.'}</Typography.Paragraph>
      <Form form={decisionForm} layout="vertical">
        <Form.Item name="decisionReason" label="Lý do (nội bộ)"
          rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }, { max: 500, message: 'Lý do không được vượt quá 500 ký tự.' }]}>
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
        <Form.Item name="publicNote" label="Nội dung gửi người dùng (tùy chọn)" extra="Không ghi thông tin hồ sơ của người khác hoặc ghi chú nội bộ."
          rules={[{ max: 500, message: 'Nội dung gửi người dùng không được vượt quá 500 ký tự.' }]}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  </Drawer>;
}

function Contact({ value, confirmed }: { value: string | null; confirmed: boolean }) {
  if (!value) return <>—</>;
  return <>{value} <Tag color={confirmed ? 'green' : 'default'}>{confirmed ? 'Đã xác minh' : 'Chưa xác minh'}</Tag></>;
}
