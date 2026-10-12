import { Alert, Button, Card, Drawer, Empty, Form, Input, Modal, Space, Table, Tag, Typography, message } from 'antd';
import { useEffect, useRef, useState } from 'react';
import { api, type SystemMessage } from '../../services/api';
import { useAuth } from '../authentication/AuthContext';
import AccountEditor from './AccountEditor';
import { type Account, type AccountPage, roleLabels } from './types';

type Audit = { id: number; actorId: number; targetId: number; occurredAt: string; action: string; reason: string; beforeJson: string; afterJson: string };
export default function AccountsPage() {
  const auth = useAuth();
  const [toast, toastContext] = message.useMessage();
  const [data, setData] = useState<AccountPage>({ items: [], total: 0, page: 1, pageSize: 10 });
  const [roles, setRoles] = useState<string[]>([]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<Account | null | undefined>(undefined);
  const [stateTarget, setStateTarget] = useState<Account | null>(null);
  const [stateBusy, setStateBusy] = useState(false);
  const [stateError, setStateError] = useState<string | null>(null);
  const [reasonForm] = Form.useForm();
  const [auditTarget, setAuditTarget] = useState<Account | null>(null);
  const [audit, setAudit] = useState<Audit[]>([]);
  const [auditBusy, setAuditBusy] = useState(false);
  const [auditError, setAuditError] = useState<string | null>(null);
  const auditRequest = useRef(0);
  useEffect(() => {
    let active = true;
    setBusy(true); setError(null);
    Promise.all([api<AccountPage>(`/api/accounts?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`), api<string[]>('/api/accounts/roles')])
      .then(([result, allowed]) => { if (active) { setData(result); setRoles(allowed); } })
      .catch(failure => { if (active) { setError((failure as Error).message); void auth.refresh().catch(() => {}); } })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [search, page, pageSize, revision]);
  async function changeState() {
    const values = await reasonForm.validateFields();
    if (!stateTarget) return;
    setStateBusy(true); setStateError(null);
    try {
      const result = await api<{ notification: SystemMessage }>(`/api/accounts/${stateTarget.id}/state`, {
        method: 'PUT', body: JSON.stringify({ version: stateTarget.version, locked: stateTarget.isActive, reason: values.reason }),
      });
      void toast.success(result.notification.message); setStateTarget(null); setRevision(value => value + 1);
      await auth.refresh();
    } catch (failure) { setStateError((failure as Error).message); }
    finally { setStateBusy(false); }
  }
  async function viewAudit(account: Account) {
    const request = ++auditRequest.current;
    setAuditTarget(account); setAudit([]); setAuditError(null); setAuditBusy(true);
    try { const items = await api<Audit[]>(`/api/accounts/${account.id}/audit`); if (request === auditRequest.current) setAudit(items); }
    catch (failure) { if (request === auditRequest.current) setAuditError((failure as Error).message); }
    finally { if (request === auditRequest.current) setAuditBusy(false); }
  }
  return <>
    {toastContext}
    <div className="page-heading"><div><Typography.Title level={2}>Quản lý tài khoản</Typography.Title>
      <Typography.Paragraph>Tạo tài khoản nhân viên, cấp vai trò và quản lý trạng thái truy cập.</Typography.Paragraph></div>
      <Button type="primary" onClick={() => setEditor(null)} disabled={roles.length === 0}>Tạo nhân viên</Button></div>
    <Card>
      <div className="table-tools"><Input.Search placeholder="Tìm tên đăng nhập, họ tên hoặc email" allowClear maxLength={160}
        onSearch={value => { setSearch(value); setPage(1); }} enterButton="Tìm kiếm" style={{ maxWidth: 460 }} />
        <Button onClick={() => setRevision(value => value + 1)} loading={busy}>Tải lại</Button></div>
      {error && <Alert className="form-alert" type="error" message={error} showIcon />}
      <Table<Account> rowKey="id" dataSource={error ? [] : data.items} loading={busy} scroll={{ x: 1000 }}
        locale={{ emptyText: <Empty description={error ? 'Chưa tải được dữ liệu.' : 'Không tìm thấy kết quả phù hợp.'} /> }}
        pagination={{ current: page, pageSize, total: data.total, showSizeChanger: true, pageSizeOptions: [10, 20, 50],
          onChange: (nextPage, size) => { setPage(size !== pageSize ? 1 : nextPage); setPageSize(size); } }}
        columns={[
          { title: 'Tên đăng nhập', dataIndex: 'userName' },
          { title: 'Họ tên', dataIndex: 'fullName' },
          { title: 'Loại', render: (_, account) => account.accountType === 'Staff' ? 'Nhân viên' : 'Công khai' },
          { title: 'Vai trò', render: (_, account) => <Space wrap>{account.roles.map(role => <Tag key={role}>{roleLabels[role] ?? role}</Tag>)}</Space> },
          { title: 'Trạng thái', render: (_, account) => <Tag color={account.isActive ? 'success' : 'error'}>{account.isActive ? 'Hoạt động' : 'Đã khóa'}</Tag> },
          { title: 'Thao tác', render: (_, account) => <Space>
            {account.accountType === 'Staff' && <Button size="small" onClick={() => setEditor(account)}>Sửa</Button>}
            <Button size="small" danger={account.isActive} onClick={() => { setStateTarget(account); setStateError(null); reasonForm.resetFields(); }}>{account.isActive ? 'Khóa' : 'Mở khóa'}</Button>
            <Button size="small" onClick={() => void viewAudit(account)}>Audit</Button>
          </Space> },
        ]} />
    </Card>
    {editor !== undefined && <AccountEditor account={editor} roles={roles} onClose={() => setEditor(undefined)} onSaved={notice => {
      setEditor(undefined); void toast.success(notice); setRevision(value => value + 1); void auth.refresh().catch(() => {});
    }} />}
    <Modal open={!!stateTarget} title={stateTarget?.isActive ? 'Xác nhận khóa tài khoản' : 'Xác nhận mở khóa tài khoản'}
      onCancel={() => setStateTarget(null)} onOk={() => void changeState().catch(() => {})} confirmLoading={stateBusy}
      maskClosable={false} closable={!stateBusy} cancelButtonProps={{ disabled: stateBusy }} okText="Xác nhận" cancelText="Hủy">
      <Typography.Paragraph>{stateTarget?.userName}: {stateTarget?.isActive
        ? 'Các phiên hiện tại sẽ bị vô hiệu hóa ngay. Nếu khóa tài khoản của bạn, bạn sẽ cần một quản trị viên khác để mở khóa.'
        : 'Tài khoản có thể đăng nhập lại. Mở khóa không xác minh hoặc khôi phục liên kết bệnh án.'}</Typography.Paragraph>
      {stateError && <Alert className="form-alert" type="error" message={stateError} showIcon />}
      <Form form={reasonForm} layout="vertical" disabled={stateBusy}><Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập Lý do.' }]}>
        <Input.TextArea rows={3} maxLength={500} /></Form.Item></Form>
    </Modal>
    <Drawer open={!!auditTarget} title={`Audit · ${auditTarget?.userName ?? ''}`} width={640}
      onClose={() => { auditRequest.current++; setAuditTarget(null); }} loading={auditBusy}>
      {auditError && <Alert type="error" showIcon message={auditError} />}
      {!auditBusy && !auditError && audit.length === 0 && <Empty description="Không tìm thấy kết quả phù hợp." />}
      {audit.map(entry => <Card key={entry.id} size="small" className="audit-entry" title={entry.action}>
        <Typography.Paragraph>Người thực hiện #{entry.actorId} · Đối tượng #{entry.targetId}<br />{new Date(entry.occurredAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}<br />{entry.reason}</Typography.Paragraph>
        <details><summary>Xem dữ liệu trước và sau</summary><pre>{JSON.stringify({ before: JSON.parse(entry.beforeJson), after: JSON.parse(entry.afterJson) }, null, 2)}</pre></details>
      </Card>)}
    </Drawer>
  </>;
}
