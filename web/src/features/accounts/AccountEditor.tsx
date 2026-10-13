import { Alert, Form, Input, Modal, Select, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api, type SystemMessage } from '../../services/api';
import { type Account, roleLabels } from './types';

type Policy = { requiredLength: number; requireDigit: boolean; requireLowercase: boolean; requireUppercase: boolean; requireNonAlphanumeric: boolean };
export default function AccountEditor({ account, roles, onClose, onSaved }: {
  account: Account | null; roles: string[]; onClose: () => void; onSaved: (notice: string) => void;
}) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [policy, setPolicy] = useState<Policy | null>(null);
  useEffect(() => {
    form.setFieldsValue(account ? { ...account, reason: '' } : { roles: [] });
    void api<Policy>('/api/auth/policy', {}, false).then(setPolicy).catch(e => setError((e as Error).message));
  }, [account, form]);
  async function save() {
    const values = await form.validateFields();
    setBusy(true); setError(null);
    try {
      const result = await api<{ account: Account; notification: SystemMessage }>(account ? `/api/accounts/${account.id}` : '/api/accounts', {
        method: account ? 'PUT' : 'POST', body: JSON.stringify({ ...values, version: account?.version,
          email: values.email || null, phone: values.phone || null }),
      });
      onSaved(result.notification.message);
    } catch (failure) { setError((failure as Error).message); }
    finally { setBusy(false); }
  }
  const required = (field: string) => [{ required: true, whitespace: true, message: `Vui lòng nhập ${field}.` }];
  return <Modal open title={account ? 'Cập nhật tài khoản nhân viên' : 'Tạo tài khoản nhân viên'}
    onCancel={onClose} onOk={() => void save().catch(() => {})} confirmLoading={busy} cancelButtonProps={{ disabled: busy }}
    closable={!busy} maskClosable={false} okText={account ? 'Xác nhận cập nhật' : 'Xác nhận tạo'} cancelText="Hủy" width={560}>
    {error && <Alert className="form-alert" type="error" showIcon message={error} />}
    <Form form={form} layout="vertical" disabled={busy} requiredMark={false}>
      <div className="form-section-label">Định danh</div>
      <Form.Item name="userName" label="Tên đăng nhập" rules={[...required('Tên đăng nhập'),
        { pattern: /^[A-Za-z0-9._-]{3,64}$/, message: 'Tên đăng nhập không đúng định dạng.' }]} extra="3–64 ký tự, gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.">
        <Input maxLength={64} autoComplete="off" />
      </Form.Item>
      {!account && <>
        <Form.Item name="employeeCode" label="Mã nhân viên" rules={[...required('Mã nhân viên'),
          { pattern: /^[A-Za-z0-9_-]{1,40}$/, message: 'Mã nhân viên không đúng định dạng.' }]}><Input maxLength={40} /></Form.Item>
        <Form.Item name="password" label="Mật khẩu ban đầu" rules={required('Mật khẩu')}><Input.Password autoComplete="new-password" maxLength={256} /></Form.Item>
        {policy && <Typography.Paragraph type="secondary" className="form-hint">Mật khẩu tối thiểu {policy.requiredLength} ký tự
          {policy.requireDigit ? ', có số' : ''}{policy.requireLowercase ? ', chữ thường' : ''}
          {policy.requireUppercase ? ', chữ hoa' : ''}{policy.requireNonAlphanumeric ? ', ký tự đặc biệt' : ''}.</Typography.Paragraph>}
      </>}
      <div className="form-section-label">Thông tin cá nhân</div>
      <div className="form-grid">
        <Form.Item name="fullName" label="Họ tên" rules={required('Họ tên')}><Input maxLength={160} /></Form.Item>
        <Form.Item name="phone" label="Số điện thoại" rules={[{ pattern: /^\+?[0-9 ()-]{7,24}$/, message: 'Số điện thoại không đúng định dạng.' }]}><Input maxLength={24} /></Form.Item>
        <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không đúng định dạng.' }]}><Input maxLength={254} /></Form.Item>
      </div>
      <div className="form-section-label">Vai trò</div>
      <Form.Item name="roles" label="Vai trò được cấp" rules={[{ required: true, type: 'array', min: 1, message: 'Vui lòng nhập Vai trò.' }]}>
        <Select mode="multiple" options={roles.map(value => ({ value, label: roleLabels[value] ?? value }))} placeholder="Chọn một hoặc nhiều vai trò" />
      </Form.Item>
      <Typography.Paragraph type="secondary" className="form-hint">Chủ phòng khám kế thừa quyền Nha sĩ. Quyền Quản trị viên và Trợ lý phải được cấp riêng.</Typography.Paragraph>
      {account && <Form.Item name="reason" label="Lý do cập nhật" rules={required('Lý do')}><Input.TextArea maxLength={500} rows={2} /></Form.Item>}
    </Form>
  </Modal>;
}
