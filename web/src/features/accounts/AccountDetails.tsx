import { Alert, Button, Descriptions, Drawer, Space, Tag, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { type Account, formatAccountTime, roleLabels } from './types';

export default function AccountDetails({ id, onClose, onEdit }: {
  id: number; onClose: () => void; onEdit: (account: Account) => void;
}) {
  const [account, setAccount] = useState<Account | null>(null);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setAccount(null); setBusy(true); setError(null);
    void api<Account>(`/api/accounts/${id}`).then(value => { if (active) setAccount(value); })
      .catch(failure => { if (active) setError((failure as Error).message); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [id, revision]);
  return <Drawer open title="Chi tiết tài khoản" width={640} onClose={onClose} loading={busy}>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} />}
    {account && <>
      <Descriptions bordered column={1} items={[
        { key: 'id', label: 'Mã tài khoản', children: account.id },
        { key: 'code', label: 'Mã nhân viên', children: account.employeeCode ?? '—' },
        { key: 'name', label: 'Tên đăng nhập', children: account.userName },
        { key: 'fullName', label: 'Họ tên', children: account.fullName },
        { key: 'email', label: 'Email', children: account.email ?? '—' },
        { key: 'phone', label: 'Số điện thoại', children: account.phone ?? '—' },
        { key: 'type', label: 'Loại tài khoản', children: account.accountType === 'Staff' ? 'Nhân viên' : 'Công khai' },
        { key: 'roles', label: 'Vai trò được cấp', children: <Space wrap>{account.roles.map(role => <Tag key={role}>{roleLabels[role] ?? role}</Tag>)}</Space> },
        { key: 'status', label: 'Trạng thái', children: account.isActive ? 'Hoạt động' : 'Đã khóa' },
        { key: 'created', label: 'Ngày tạo', children: formatAccountTime(account.createdAt) },
        { key: 'updated', label: 'Cập nhật lần cuối', children: formatAccountTime(account.updatedAt) },
        { key: 'version', label: 'Phiên bản', children: account.version },
      ]} />
      <Typography.Paragraph style={{ marginTop: 16 }}>Quản lý tài khoản không xác minh liên kết hồ sơ bệnh nhân.</Typography.Paragraph>
      <Space wrap>
        {account.accountType === 'Staff' && <Button type="primary" onClick={() => onEdit(account)}>Sửa tài khoản</Button>}
        <Button disabled>Hỗ trợ khôi phục (S03 chưa sẵn sàng)</Button>
      </Space>
      <Typography.Paragraph type="secondary">Hoãn khôi phục đến khi có quy trình xác minh S03. Không đặt lại mật khẩu qua màn hình này.</Typography.Paragraph>
    </>}
  </Drawer>;
}
