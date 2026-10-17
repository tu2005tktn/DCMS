import { Alert, Button, Form, Input, Modal, Select, Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError, type SystemMessage } from '../../services/api';
import { type Account, roleLabels } from './types';

type Policy = { requiredLength: number; requireDigit: boolean; requireLowercase: boolean; requireUppercase: boolean; requireNonAlphanumeric: boolean };
export default function AccountEditor({ account, roles, onClose, onSaved }: {
  account: Account | null; roles: string[]; onClose: () => void; onSaved: (notice: string) => void;
}) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [policy, setPolicy] = useState<Policy | null>(null);
  const [policyRevision, setPolicyRevision] = useState(0);
  const [dirty, setDirty] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [latest, setLatest] = useState<Account | null>(null);
  const [version, setVersion] = useState(account?.version);
  const submitting = useRef(false);
  const [modal, modalContext] = Modal.useModal();
  useEffect(() => {
    form.setFieldsValue(account ? { ...account, reason: '' } : { roles: [] });
  }, [account, form]);
  useEffect(() => {
    if (account) return;
    let active = true;
    setError(null);
    void api<Policy>('/api/auth/policy', {}, false).then(value => { if (active) setPolicy(value); })
      .catch(e => { if (active) setError((e as Error).message); });
    return () => { active = false; };
  }, [account, policyRevision]);
  useEffect(() => {
    if (!dirty) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);
  function close() {
    if (submitting.current) return;
    if (!dirty) { onClose(); return; }
    modal.confirm({ title: 'Bỏ thay đổi chưa lưu?', content: 'Nội dung đang nhập sẽ bị bỏ.', okText: 'Bỏ thay đổi', cancelText: 'Tiếp tục sửa', onOk: onClose });
  }
  async function reloadVersion() {
    if (!account || submitting.current) return;
    submitting.current = true; setBusy(true);
    try {
      const current = await api<Account>(`/api/accounts/${account.id}`);
      setLatest(current); setVersion(current.version); setConflict(false); setError(null);
    } catch (failure) { setError((failure as Error).message); }
    finally { submitting.current = false; setBusy(false); }
  }
  async function save() {
    if (submitting.current || (!account && !policy) || conflict) return;
    submitting.current = true;
    try {
      const values = await form.validateFields();
      setBusy(true); setError(null);
      const result = await api<{ account: Account; notification: SystemMessage }>(account ? `/api/accounts/${account.id}` : '/api/accounts', {
        method: account ? 'PUT' : 'POST', body: JSON.stringify({ ...values, version,
          userName: values.userName.trim(), fullName: values.fullName.trim(),
          employeeCode: values.employeeCode?.trim(), reason: values.reason?.trim(),
          email: values.email?.trim() || null, phone: values.phone?.trim() || null }),
      });
      setDirty(false);
      onSaved(result.notification.message);
    } catch (failure) {
      if (failure instanceof Error) setError(failure.message);
      if (failure instanceof ApiError && failure.code === 'MSG111') setConflict(true);
    }
    finally { submitting.current = false; setBusy(false); }
  }
  const required = (field: string) => [{ required: true, whitespace: true, message: `Vui lòng nhập ${field}.` }];
  return <Modal open title={account ? 'Cập nhật tài khoản nhân viên' : 'Tạo tài khoản nhân viên'}
    onCancel={close} onOk={() => void save().catch(() => {})} confirmLoading={busy} cancelButtonProps={{ disabled: busy }}
    okButtonProps={{ disabled: conflict || (!account && !policy), 'aria-label': account ? 'Xác nhận cập nhật' : 'Xác nhận tạo' }}
    closable={!busy} maskClosable={false} okText={account ? 'Xác nhận cập nhật' : 'Xác nhận tạo'} cancelText="Hủy">
    {modalContext}
    {error && <Alert className="form-alert" type="error" showIcon message={error} />}
    {!account && !policy && <Button onClick={() => setPolicyRevision(value => value + 1)}>Tải lại chính sách mật khẩu</Button>}
    {conflict && <Button loading={busy} onClick={() => void reloadVersion()}>Tải phiên bản mới, giữ bản nháp</Button>}
    {latest && <Alert className="form-alert" type="info" showIcon message="Kiểm tra dữ liệu mới nhất trước khi xác nhận lại" description={
      <span>Phiên bản {latest.version}: {latest.userName} · {latest.fullName} · {latest.email ?? '—'} · {latest.phone ?? '—'} · {latest.roles.map(role => roleLabels[role] ?? role).join(', ')} · {latest.isActive ? 'Hoạt động' : 'Đã khóa'}. Nội dung đang nhập được giữ nguyên.</span>
    } />}
    <Form form={form} layout="vertical" disabled={busy} requiredMark onValuesChange={() => setDirty(true)}>
      <Form.Item name="userName" label="Tên đăng nhập" rules={[...required('Tên đăng nhập'),
        { pattern: /^[A-Za-z0-9._-]{3,64}$/, transform: value => value?.trim(), message: 'Tên đăng nhập không đúng định dạng.' }]}>
        <Input maxLength={64} autoComplete="off" />
      </Form.Item>
      <Typography.Paragraph type="secondary">Tên đăng nhập: 3–64 ký tự, gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.</Typography.Paragraph>
      {!account && <>
        <Form.Item name="employeeCode" label="Mã nhân viên" rules={[...required('Mã nhân viên'),
          { pattern: /^[A-Za-z0-9_-]{1,40}$/, transform: value => value?.trim(), message: 'Mã nhân viên không đúng định dạng.' }]}><Input maxLength={40} /></Form.Item>
        <Form.Item name="password" label="Mật khẩu ban đầu" rules={[...required('Mật khẩu'), { validator: async (_, value: string) => {
          if (!value || !policy) return;
          if (value.length < policy.requiredLength || (policy.requireDigit && !/[0-9]/.test(value))
            || (policy.requireLowercase && !/[a-z]/.test(value)) || (policy.requireUppercase && !/[A-Z]/.test(value))
            || (policy.requireNonAlphanumeric && !/[^a-zA-Z0-9]/.test(value)))
            throw new Error('Mật khẩu chưa đáp ứng yêu cầu được hiển thị. Vui lòng kiểm tra và nhập lại.');
        } }]}><Input.Password autoComplete="new-password" maxLength={256} /></Form.Item>
        {policy && <Typography.Paragraph type="secondary">Mật khẩu tối thiểu {policy.requiredLength} ký tự
          {policy.requireDigit ? ', có số' : ''}{policy.requireLowercase ? ', chữ thường' : ''}
          {policy.requireUppercase ? ', chữ hoa' : ''}{policy.requireNonAlphanumeric ? ', ký tự đặc biệt' : ''}.</Typography.Paragraph>}
      </>}
      <Form.Item name="fullName" label="Họ tên" rules={required('Họ tên')}><Input maxLength={160} /></Form.Item>
      <Typography.Paragraph type="secondary">Cần ít nhất Email hoặc Số điện thoại; liên hệ chưa đồng nghĩa đã xác minh.</Typography.Paragraph>
      <Form.Item name="email" label="Email" dependencies={['phone']} rules={[{ type: 'email', message: 'Email không đúng định dạng.', transform: value => value?.trim() },
        { validator: async (_, value: string) => { if (!value?.trim() && !form.getFieldValue('phone')?.trim()) throw new Error('Vui lòng nhập Email hoặc số điện thoại.'); } }]}><Input maxLength={254} /></Form.Item>
      <Form.Item name="phone" label="Số điện thoại" rules={[{ validator: async (_, value: string) => {
        const phone = value?.trim();
        if (phone && (!/^\+?[0-9 ()-]{7,24}$/.test(phone) || !/^\+?[0-9]{7,24}$/.test(phone.replace(/[ ()-]/g, ''))))
          throw new Error('Số điện thoại không đúng định dạng.');
      } }]}><Input maxLength={24} /></Form.Item>
      <Form.Item name="roles" label="Vai trò được cấp" rules={[{ required: true, type: 'array', min: 1, message: 'Vui lòng nhập Vai trò.' }]}>
        <Select mode="multiple" options={roles.map(value => ({ value, label: roleLabels[value] ?? value }))} />
      </Form.Item>
      <Typography.Paragraph type="secondary">Chủ phòng khám kế thừa quyền Nha sĩ. Quyền Quản trị viên và Trợ lý phải được cấp riêng.</Typography.Paragraph>
      {account && <Form.Item name="reason" label="Lý do cập nhật" rules={required('Lý do')}><Input.TextArea maxLength={500} rows={2} /></Form.Item>}
    </Form>
  </Modal>;
}
