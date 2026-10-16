import { Alert, Button, Card, Descriptions, Empty, Form, Input, Popconfirm, Space, Spin, Switch, Tag, Typography } from 'antd';
import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../../services/api';
import { customerApi } from '../../services/customerApi';
import { useCustomerAuth } from '../customer-auth/CustomerAuthContext';

type LinkStatus = 'NOT_LINKED' | 'PENDING' | 'VERIFIED' | 'REJECTED' | 'REVOKED' | 'WITHDRAWN';
type LinkRequest = { id: number; requestCode: string; status: Exclude<LinkStatus, 'NOT_LINKED'>;
  requestedAt: string; decidedAt: string | null; publicNote: string | null; version: number;
  result: { code: string; message: string } | null };
type Overview = { account: { id: number; userName: string; fullName: string;
  verifiedEmail: string | null; verifiedPhone: string | null }; linkStatus: LinkStatus;
  current: LinkRequest | null; history: LinkRequest[]; canSubmit: boolean };
type RecordSummary = { patientCode: string; fullName: string; birthDate: string | null; linkedAt: string };
type LinkForm = { fullName: string; birthDate?: string; patientCode?: string;
  matchingInformation?: string; confirmSelf: boolean };
const formatTime = (value: string) => new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
const labels: Record<LinkStatus, string> = { NOT_LINKED: 'Chưa liên kết', PENDING: 'Đang chờ xác minh',
  VERIFIED: 'Đã xác minh', REJECTED: 'Bị từ chối', REVOKED: 'Đã thu hồi', WITHDRAWN: 'Đã rút' };

export default function CustomerPatientLinkPage() {
  const { state, expire } = useCustomerAuth();
  const token = state.status === 'authenticated' ? state.accessToken : null;
  const [form] = Form.useForm<LinkForm>();
  const [overview, setOverview] = useState<Overview | null>(null);
  const [record, setRecord] = useState<RecordSummary | null>(null);
  const [busy, setBusy] = useState(true);
  const [actionBusy, setActionBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const handleError = useCallback((failure: unknown) => {
    if (failure instanceof ApiError && failure.status === 401) {
      setOverview(null); setRecord(null); expire(); return;
    }
    setError((failure as Error).message);
  }, [expire]);

  const load = useCallback(async () => {
    if (!token) return;
    setBusy(true); setError(null);
    try {
      const value = await customerApi<Overview>('/api/patient-links/me', {}, token);
      setOverview(value);
      if (value.linkStatus !== 'VERIFIED') setRecord(null);
    } catch (failure) { handleError(failure); }
    finally { setBusy(false); }
  }, [token, handleError]);
  useEffect(() => { void load(); }, [load]);

  async function submit(values: LinkForm) {
    if (!token || actionBusy) return;
    setActionBusy(true); setError(null); setNotice(null);
    try {
      const result = await customerApi<{ notification: { message: string } }>('/api/patient-links/me/requests', {
        method: 'POST', body: JSON.stringify({ fullName: values.fullName.trim(), birthDate: values.birthDate || null,
          patientCode: values.patientCode?.trim() || null, matchingInformation: values.matchingInformation?.trim() || null,
          confirmSelf: values.confirmSelf }),
      }, token);
      setNotice(result.notification.message); setRecord(null); form.resetFields(); await load();
    } catch (failure) { handleError(failure); }
    finally { setActionBusy(false); }
  }

  async function withdraw() {
    if (!token || !overview?.current || actionBusy) return;
    setActionBusy(true); setError(null); setNotice(null);
    try {
      await customerApi(`/api/patient-links/me/requests/${overview.current.id}/withdraw`, {
        method: 'POST', body: JSON.stringify({ version: overview.current.version }),
      }, token);
      setNotice('Đã rút yêu cầu liên kết.'); await load();
    } catch (failure) {
      handleError(failure);
      if (!(failure instanceof ApiError && failure.status === 401)) await load();
    }
    finally { setActionBusy(false); }
  }

  async function viewRecord() {
    if (!token || actionBusy) return;
    setActionBusy(true); setError(null);
    try { setRecord(await customerApi<RecordSummary>('/api/patient-links/me/record', {}, token)); }
    catch (failure) { setRecord(null); handleError(failure); }
    finally { setActionBusy(false); }
  }

  return <div className="customer-link">
    <Typography.Title level={2}>Liên kết hồ sơ của tôi</Typography.Title>
    <Typography.Paragraph>Phòng khám xác minh danh tính trước khi cấp quyền xem hồ sơ. Đặt lịch cho người thân không tạo liên kết.</Typography.Paragraph>
    {notice && <Alert className="form-alert" type="success" showIcon message={notice} />}
    {error && <Alert className="form-alert" type="error" showIcon message={error}
      action={<Button onClick={() => void load()}>Thử lại</Button>} />}
    {busy && !overview ? <Spin size="large" /> : overview ? <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Card title="Tài khoản hiện tại"><Descriptions column={1} size="small" items={[
        { key: 'user', label: 'Tên đăng nhập', children: overview.account.userName },
        { key: 'id', label: 'Mã tài khoản', children: `#${overview.account.id}` },
        { key: 'contact', label: 'Liên hệ đã xác minh',
          children: overview.account.verifiedEmail ?? overview.account.verifiedPhone ?? 'Chưa có' },
      ]} /></Card>
      <Card title="Trạng thái liên kết" extra={<Button loading={busy} onClick={() => void load()}>Tải lại</Button>}>
        <Tag color={overview.linkStatus === 'VERIFIED' ? 'green' : overview.linkStatus === 'PENDING' ? 'blue' : 'default'}>
          {labels[overview.linkStatus]}</Tag>
        {overview.current && <Descriptions column={1} size="small" className="customer-link-details" items={[
          { key: 'code', label: 'Mã yêu cầu', children: overview.current.requestCode },
          { key: 'time', label: 'Gửi lúc', children: formatTime(overview.current.requestedAt) },
          ...(overview.current.decidedAt ? [{ key: 'decided', label: 'Xử lý lúc', children: formatTime(overview.current.decidedAt) }] : []),
          ...(overview.current.publicNote ? [{ key: 'note', label: 'Phòng khám', children: overview.current.publicNote }] : []),
        ]} />}
        {overview.linkStatus === 'NOT_LINKED' && <Typography.Paragraph>Chỉ gửi yêu cầu sau khi đã đến khám và được tiếp nhận tại phòng khám.</Typography.Paragraph>}
        {overview.linkStatus === 'PENDING' && <div><Typography.Paragraph>Yêu cầu đang chờ xác minh; bạn chưa có quyền xem hồ sơ.</Typography.Paragraph>
          <Popconfirm title="Rút yêu cầu liên kết?" okText="Rút yêu cầu" cancelText="Hủy" onConfirm={() => void withdraw()}>
            <Button loading={actionBusy}>Rút yêu cầu</Button></Popconfirm></div>}
        {overview.linkStatus === 'VERIFIED' && <div className="customer-link-details">
          <Button loading={actionBusy} onClick={() => void viewRecord()}>Xem hồ sơ của tôi</Button>
          {record && <Descriptions column={1} size="small" className="customer-link-details" items={[
            { key: 'code', label: 'Mã hồ sơ', children: record.patientCode },
            { key: 'name', label: 'Họ tên', children: record.fullName },
            { key: 'birth', label: 'Ngày sinh', children: record.birthDate ?? '—' },
          ]} />}</div>}
        {['REJECTED', 'REVOKED'].includes(overview.linkStatus) &&
          <Typography.Paragraph>Vui lòng liên hệ phòng khám hoặc bổ sung thông tin và gửi yêu cầu mới.</Typography.Paragraph>}
      </Card>
      {overview.canSubmit && <Card title="Gửi yêu cầu liên kết">
        <Form form={form} layout="vertical" initialValues={{ confirmSelf: false }} onFinish={values => void submit(values)} disabled={actionBusy}>
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập họ tên.' },
            { max: 160, message: 'Họ tên không được vượt quá 160 ký tự.' }]}><Input maxLength={160} /></Form.Item>
          <Form.Item name="birthDate" label="Ngày sinh (không bắt buộc)"><Input type="date"
            max={new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())} /></Form.Item>
          <Form.Item name="patientCode" label="Mã hồ sơ (nếu biết)" rules={[{ max: 40, message: 'Mã hồ sơ không được vượt quá 40 ký tự.' },
            { pattern: /^[A-Za-z0-9_-]*$/, message: 'Mã hồ sơ không đúng định dạng.' }]}><Input maxLength={40} /></Form.Item>
          <Form.Item name="matchingInformation" label="Thông tin đối chiếu" rules={[{ max: 1000, message: 'Không được vượt quá 1000 ký tự.' }]}>
            <Input.TextArea maxLength={1000} showCount /></Form.Item>
          <Form.Item name="confirmSelf" label="Xác nhận" valuePropName="checked"
            rules={[{ validator: async (_, value: boolean) => { if (!value) throw new Error('Vui lòng xác nhận đây là hồ sơ của chính bạn.'); } }]}>
            <Switch checkedChildren="Tôi xác nhận" unCheckedChildren="Chưa xác nhận" /></Form.Item>
          <Typography.Paragraph>Tôi xác nhận đây là hồ sơ của chính tôi, không phải của người thân.</Typography.Paragraph>
          <Button htmlType="submit" type="primary" loading={actionBusy}>Gửi yêu cầu</Button>
        </Form>
      </Card>}
      {overview.history.length > 0 && <Card title="Yêu cầu trước đây">
        {overview.history.filter(item => item.id !== overview.current?.id).map(item =>
          <Typography.Paragraph key={item.id}>{item.requestCode} · {formatTime(item.requestedAt)} · {labels[item.status]}</Typography.Paragraph>)}
      </Card>}
    </Space> : !busy && !error ? <Empty description="Không có thông tin liên kết" /> : null}
  </div>;
}
